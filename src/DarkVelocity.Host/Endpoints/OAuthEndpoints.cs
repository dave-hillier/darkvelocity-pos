using DarkVelocity.Host.Auth;
using DarkVelocity.Host.Grains;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace DarkVelocity.Host.Endpoints;

public static class OAuthEndpoints
{
    private static readonly Guid DefaultOrgId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    public static WebApplication MapOAuthEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/oauth").WithTags("OAuth");

        // ========================================================================
        // OAuth 2.0 Authorization Endpoint
        // ========================================================================

        group.MapGet("/authorize", async (
            HttpContext context,
            IGrainFactory grainFactory,
            string response_type,
            string client_id,
            string redirect_uri,
            string? scope = null,
            string? state = null,
            string? nonce = null,
            string? code_challenge = null,
            string? code_challenge_method = null,
            string? provider = null) =>
        {
            // Validate response type
            if (response_type != "code" && response_type != "token")
            {
                return Results.BadRequest(new OAuthError("unsupported_response_type",
                    "Supported response types: code, token"));
            }

            // Validate client (in production, lookup from database)
            if (!IsValidClient(client_id, redirect_uri))
            {
                return Results.BadRequest(new OAuthError("invalid_client",
                    "Unknown client_id or invalid redirect_uri"));
            }

            // Generate and store state for CSRF protection
            var internalState = GrainKeys.GenerateOAuthState();
            var stateGrain = grainFactory.GetGrain<IOAuthStateGrain>(GrainKeys.OAuthState(internalState));
            await stateGrain.InitializeAsync(new OAuthStateRequest(
                provider ?? "google",
                redirect_uri,
                code_challenge,
                code_challenge_method,
                client_id,
                nonce,
                scope));

            // Combine internal state with client state for callback
            var combinedState = string.IsNullOrEmpty(state)
                ? internalState
                : $"{internalState}|{state}";

            var selectedProvider = (provider?.ToLowerInvariant() ?? "google") switch
            {
                "google" => "Google",
                "microsoft" => "Microsoft",
                _ => "Google"
            };

            var properties = new AuthenticationProperties
            {
                RedirectUri = $"/api/oauth/callback?state={Uri.EscapeDataString(combinedState)}&response_type={response_type}",
                Items = { ["LoginProvider"] = selectedProvider }
            };

            return Results.Challenge(properties, [selectedProvider]);
        }).WithName("OAuthAuthorize")
          .WithDescription("OAuth 2.0 Authorization Endpoint - initiates authorization flow");

        // ========================================================================
        // Legacy Login Endpoint (simplified authorization)
        // ========================================================================

        group.MapGet("/login/{provider}", async (
            HttpContext context,
            IGrainFactory grainFactory,
            string provider,
            string? returnUrl = null,
            string? code_challenge = null,
            string? code_challenge_method = null) =>
        {
            var redirectUri = returnUrl ?? "/";

            // Generate state for CSRF protection
            var state = GrainKeys.GenerateOAuthState();
            var stateGrain = grainFactory.GetGrain<IOAuthStateGrain>(GrainKeys.OAuthState(state));
            await stateGrain.InitializeAsync(new OAuthStateRequest(
                provider,
                redirectUri,
                code_challenge,
                code_challenge_method));

            var properties = new AuthenticationProperties
            {
                RedirectUri = $"/api/oauth/callback?state={Uri.EscapeDataString(state)}&response_type=token"
            };

            return provider.ToLowerInvariant() switch
            {
                "google" => Results.Challenge(properties, ["Google"]),
                "microsoft" => Results.Challenge(properties, ["Microsoft"]),
                _ => Results.BadRequest(new OAuthError("invalid_provider",
                    "Supported providers: google, microsoft"))
            };
        }).WithName("OAuthLogin")
          .WithDescription("Simplified OAuth login - redirects to provider");

        // ========================================================================
        // OAuth Callback Handler
        // ========================================================================

        group.MapGet("/callback", async (
            HttpContext context,
            IGrainFactory grainFactory,
            JwtTokenService tokenService,
            string? state = null,
            string? response_type = null,
            string? error = null,
            string? error_description = null) =>
        {
            // Handle OAuth provider errors
            if (!string.IsNullOrEmpty(error))
            {
                var errorRedirect = "/?error=" + Uri.EscapeDataString(error);
                if (!string.IsNullOrEmpty(error_description))
                    errorRedirect += "&error_description=" + Uri.EscapeDataString(error_description);
                return Results.Redirect(errorRedirect);
            }

            // Validate state parameter
            if (string.IsNullOrEmpty(state))
            {
                return Results.Redirect("/?error=missing_state");
            }

            // Parse combined state (internal|client)
            var stateParts = state.Split('|', 2);
            var internalState = stateParts[0];
            var clientState = stateParts.Length > 1 ? stateParts[1] : null;

            // Validate and consume state
            var stateGrain = grainFactory.GetGrain<IOAuthStateGrain>(GrainKeys.OAuthState(internalState));
            var validation = await stateGrain.ValidateAndConsumeAsync();

            if (!validation.IsValid)
            {
                return Results.Redirect($"/?error={validation.Error}");
            }

            // Authenticate with cookie
            var result = await context.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            if (!result.Succeeded || result.Principal == null)
            {
                return Results.Redirect($"{validation.ReturnUrl}?error=auth_failed");
            }

            // Extract claims from OAuth provider
            var claims = result.Principal.Claims.ToList();
            var email = claims.FirstOrDefault(c => c.Type == ClaimTypes.Email)?.Value;
            var name = claims.FirstOrDefault(c => c.Type == ClaimTypes.Name)?.Value;
            var externalId = claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
            var pictureUrl = claims.FirstOrDefault(c => c.Type == "picture" || c.Type == "urn:google:picture")?.Value;

            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(externalId))
            {
                return Results.Redirect($"{validation.ReturnUrl}?error=missing_claims");
            }

            // Lookup or create user based on external identity
            var provider = validation.Provider ?? "google";
            var identityGrain = grainFactory.GetGrain<IExternalIdentityGrain>(
                GrainKeys.ExternalIdentity(provider, externalId));
            var existingIdentity = await identityGrain.GetLinkedUserAsync();

            Guid userId;
            Guid orgId;
            string displayName;
            IReadOnlyList<string> roles;

            if (existingIdentity != null)
            {
                // Existing user - update info if changed
                userId = existingIdentity.UserId;
                orgId = existingIdentity.OrganizationId;
                displayName = existingIdentity.Name ?? name ?? email;

                await identityGrain.UpdateInfoAsync(email, name, pictureUrl);

                // Get user details for roles
                var userGrain = grainFactory.GetGrain<IUserGrain>(GrainKeys.User(orgId, userId));
                var userState = await userGrain.GetStateAsync();
                roles = MapUserTypeToRoles(userState.Type);
            }
            else
            {
                // New user - create account
                userId = Guid.NewGuid();
                orgId = DefaultOrgId; // In production, this would be determined by domain/invitation
                displayName = name ?? email;
                roles = ["backoffice"]; // Default role for new OAuth users

                // Create user
                var userGrain = grainFactory.GetGrain<IUserGrain>(GrainKeys.User(orgId, userId));
                await userGrain.CreateAsync(new CreateUserCommand(
                    orgId,
                    email,
                    displayName,
                    State.UserType.Employee,
                    name?.Split(' ').FirstOrDefault(),
                    name?.Split(' ').Skip(1).FirstOrDefault()));

                // Link external identity
                await identityGrain.LinkAsync(new LinkExternalIdentityCommand(
                    userId,
                    orgId,
                    provider,
                    externalId,
                    email,
                    name,
                    pictureUrl));
            }

            // Sign out of cookie auth
            await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            var redirectTarget = validation.ReturnUrl ?? "http://localhost:5174";

            // Handle response types
            if (response_type == "code")
            {
                // Authorization Code Flow - issue code
                var code = GrainKeys.GenerateAuthorizationCode();
                var codeGrain = grainFactory.GetGrain<IAuthorizationCodeGrain>(GrainKeys.AuthorizationCode(code));
                await codeGrain.IssueAsync(new AuthorizationCodeRequest(
                    userId,
                    orgId,
                    validation.ClientId ?? "default",
                    redirectTarget,
                    validation.Scope,
                    validation.CodeChallenge,
                    validation.CodeChallengeMethod,
                    validation.Nonce,
                    displayName,
                    roles));

                var codeRedirect = AddQueryParam(redirectTarget, "code", code);
                if (!string.IsNullOrEmpty(clientState))
                    codeRedirect = AddQueryParam(codeRedirect, "state", clientState);

                return Results.Redirect(codeRedirect);
            }
            else
            {
                // Implicit Flow - issue tokens directly (fragment)
                var (accessToken, expires) = tokenService.GenerateAccessToken(
                    userId,
                    displayName,
                    orgId,
                    roles: roles);
                var refreshToken = tokenService.GenerateRefreshToken();

                var fragment = $"access_token={accessToken}" +
                    $"&token_type=Bearer" +
                    $"&expires_in={(int)(expires - DateTime.UtcNow).TotalSeconds}" +
                    $"&refresh_token={refreshToken}" +
                    $"&user_id={userId}" +
                    $"&display_name={Uri.EscapeDataString(displayName)}";

                if (!string.IsNullOrEmpty(clientState))
                    fragment += $"&state={Uri.EscapeDataString(clientState)}";

                var separator = redirectTarget.Contains('#') ? "&" : "#";
                return Results.Redirect($"{redirectTarget}{separator}{fragment}");
            }
        }).WithName("OAuthCallback")
          .WithDescription("OAuth callback handler - completes authorization flow");

        // ========================================================================
        // Token Endpoint (RFC 6749)
        // ========================================================================

        group.MapPost("/token", async (
            HttpContext context,
            IGrainFactory grainFactory,
            JwtTokenService tokenService) =>
        {
            // Read form data
            var form = await context.Request.ReadFormAsync();
            var grantType = form["grant_type"].FirstOrDefault();
            var clientId = form["client_id"].FirstOrDefault();
            var clientSecret = form["client_secret"].FirstOrDefault();

            // Also check Authorization header for client credentials
            if (string.IsNullOrEmpty(clientId))
            {
                var authHeader = context.Request.Headers.Authorization.FirstOrDefault();
                if (authHeader?.StartsWith("Basic ") == true)
                {
                    var encoded = authHeader["Basic ".Length..];
                    var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(encoded));
                    var parts = decoded.Split(':', 2);
                    clientId = parts[0];
                    clientSecret = parts.Length > 1 ? parts[1] : null;
                }
            }

            if (string.IsNullOrEmpty(clientId))
            {
                return Results.Json(new OAuthError("invalid_client", "client_id is required"),
                    statusCode: 401);
            }

            return grantType switch
            {
                "authorization_code" => await HandleAuthorizationCodeGrant(
                    form, clientId, grainFactory, tokenService),
                "refresh_token" => await HandleRefreshTokenGrant(
                    form, clientId, grainFactory, tokenService),
                "client_credentials" => HandleClientCredentialsGrant(
                    clientId, clientSecret, tokenService),
                _ => Results.Json(new OAuthError("unsupported_grant_type",
                    "Supported grant types: authorization_code, refresh_token, client_credentials"),
                    statusCode: 400)
            };
        }).WithName("OAuthToken")
          .WithDescription("OAuth 2.0 Token Endpoint - exchanges codes/refresh tokens for access tokens");

        // ========================================================================
        // Token Revocation Endpoint (RFC 7009)
        // ========================================================================

        group.MapPost("/revoke", async (
            HttpContext context,
            IGrainFactory grainFactory,
            JwtTokenService tokenService) =>
        {
            var form = await context.Request.ReadFormAsync();
            var token = form["token"].FirstOrDefault();
            var tokenTypeHint = form["token_type_hint"].FirstOrDefault();

            if (string.IsNullOrEmpty(token))
            {
                return Results.Json(new OAuthError("invalid_request", "token is required"),
                    statusCode: 400);
            }

            // Try to decode the token to get session info
            var principal = tokenService.ValidateToken(token);
            if (principal != null)
            {
                var sessionId = principal.FindFirst("session_id")?.Value;
                var orgId = principal.FindFirst("org_id")?.Value;

                if (!string.IsNullOrEmpty(sessionId) && !string.IsNullOrEmpty(orgId))
                {
                    var sessionGrain = grainFactory.GetGrain<ISessionGrain>(
                        GrainKeys.Session(Guid.Parse(orgId), Guid.Parse(sessionId)));
                    await sessionGrain.RevokeAsync();
                }
            }

            // Per RFC 7009, always return 200 even if token wasn't found
            return Results.Ok();
        }).WithName("OAuthRevoke")
          .WithDescription("OAuth 2.0 Token Revocation Endpoint");

        // ========================================================================
        // Token Introspection Endpoint (RFC 7662)
        // ========================================================================

        group.MapPost("/introspect", async (
            HttpContext context,
            JwtTokenService tokenService) =>
        {
            var form = await context.Request.ReadFormAsync();
            var token = form["token"].FirstOrDefault();

            if (string.IsNullOrEmpty(token))
            {
                return Results.Json(new { active = false });
            }

            var principal = tokenService.ValidateToken(token);
            if (principal == null)
            {
                return Results.Json(new { active = false });
            }

            var claims = principal.Claims.ToList();
            return Results.Json(new
            {
                active = true,
                sub = claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier || c.Type == JwtRegisteredClaimNames.Sub)?.Value,
                client_id = claims.FirstOrDefault(c => c.Type == "client_id")?.Value,
                username = claims.FirstOrDefault(c => c.Type == ClaimTypes.Name || c.Type == JwtRegisteredClaimNames.Name)?.Value,
                scope = claims.FirstOrDefault(c => c.Type == "scope")?.Value,
                exp = claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Exp)?.Value,
                iat = claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Iat)?.Value,
                org_id = claims.FirstOrDefault(c => c.Type == "org_id")?.Value,
                site_id = claims.FirstOrDefault(c => c.Type == "site_id")?.Value,
                token_type = "Bearer"
            });
        }).WithName("OAuthIntrospect")
          .WithDescription("OAuth 2.0 Token Introspection Endpoint (RFC 7662)");

        // ========================================================================
        // UserInfo Endpoint (OpenID Connect)
        // ========================================================================

        group.MapGet("/userinfo", async (HttpContext context, IGrainFactory grainFactory) =>
        {
            if (context.User.Identity?.IsAuthenticated != true)
                return Results.Unauthorized();

            var claims = context.User.Claims.ToList();
            var userId = claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier || c.Type == JwtRegisteredClaimNames.Sub)?.Value;
            var orgId = claims.FirstOrDefault(c => c.Type == "org_id")?.Value;

            var response = new Dictionary<string, object?>
            {
                ["sub"] = userId,
                ["name"] = claims.FirstOrDefault(c => c.Type == ClaimTypes.Name || c.Type == JwtRegisteredClaimNames.Name)?.Value,
                ["org_id"] = orgId,
                ["site_id"] = claims.FirstOrDefault(c => c.Type == "site_id")?.Value,
                ["roles"] = claims.Where(c => c.Type == ClaimTypes.Role).Select(c => c.Value).ToArray()
            };

            // Fetch additional user info if available
            if (Guid.TryParse(userId, out var uid) && Guid.TryParse(orgId, out var oid))
            {
                var userGrain = grainFactory.GetGrain<IUserGrain>(GrainKeys.User(oid, uid));
                var userState = await userGrain.GetStateAsync();
                if (userState.Id != Guid.Empty)
                {
                    response["email"] = userState.Email;
                    response["given_name"] = userState.FirstName;
                    response["family_name"] = userState.LastName;
                    response["preferred_username"] = userState.DisplayName;
                }
            }

            return Results.Json(response);
        }).RequireAuthorization()
          .WithName("OAuthUserInfo")
          .WithDescription("OpenID Connect UserInfo Endpoint");

        // ========================================================================
        // OpenID Connect Discovery Document
        // ========================================================================

        app.MapGet("/.well-known/openid-configuration", (HttpContext context, JwtSettings jwtSettings) =>
        {
            var baseUrl = $"{context.Request.Scheme}://{context.Request.Host}";
            return Results.Json(new
            {
                issuer = jwtSettings.Issuer,
                authorization_endpoint = $"{baseUrl}/api/oauth/authorize",
                token_endpoint = $"{baseUrl}/api/oauth/token",
                userinfo_endpoint = $"{baseUrl}/api/oauth/userinfo",
                revocation_endpoint = $"{baseUrl}/api/oauth/revoke",
                introspection_endpoint = $"{baseUrl}/api/oauth/introspect",
                jwks_uri = $"{baseUrl}/.well-known/jwks.json",
                response_types_supported = new[] { "code", "token", "id_token", "code token", "code id_token" },
                grant_types_supported = new[] { "authorization_code", "refresh_token", "client_credentials" },
                subject_types_supported = new[] { "public" },
                id_token_signing_alg_values_supported = new[] { "HS256", "RS256" },
                token_endpoint_auth_methods_supported = new[] { "client_secret_basic", "client_secret_post" },
                code_challenge_methods_supported = new[] { "plain", "S256" },
                scopes_supported = new[] { "openid", "profile", "email", "offline_access" },
                claims_supported = new[] { "sub", "name", "email", "org_id", "site_id", "roles" }
            });
        }).WithName("OpenIDConfiguration")
          .WithDescription("OpenID Connect Discovery Document")
          .WithTags("OAuth");

        // ========================================================================
        // JSON Web Key Set (JWKS) Endpoint
        // ========================================================================

        app.MapGet("/.well-known/jwks.json", (JwtSettings jwtSettings) =>
        {
            // For HMAC, we expose the key ID but not the secret
            // In production with RSA, this would expose the public key
            var keyId = ComputeKeyId(jwtSettings.SecretKey);
            return Results.Json(new
            {
                keys = new[]
                {
                    new
                    {
                        kty = "oct",
                        use = "sig",
                        kid = keyId,
                        alg = "HS256"
                    }
                }
            });
        }).WithName("JWKS")
          .WithDescription("JSON Web Key Set for token verification")
          .WithTags("OAuth");

        return app;
    }

    // ========================================================================
    // Helper Methods
    // ========================================================================

    private static async Task<IResult> HandleAuthorizationCodeGrant(
        IFormCollection form,
        string clientId,
        IGrainFactory grainFactory,
        JwtTokenService tokenService)
    {
        var code = form["code"].FirstOrDefault();
        var redirectUri = form["redirect_uri"].FirstOrDefault();
        var codeVerifier = form["code_verifier"].FirstOrDefault();

        if (string.IsNullOrEmpty(code))
        {
            return Results.Json(new OAuthError("invalid_request", "code is required"),
                statusCode: 400);
        }

        var codeGrain = grainFactory.GetGrain<IAuthorizationCodeGrain>(GrainKeys.AuthorizationCode(code));
        var exchange = await codeGrain.ExchangeAsync(clientId, codeVerifier);

        if (exchange == null)
        {
            return Results.Json(new OAuthError("invalid_grant",
                "Invalid, expired, or already used authorization code"),
                statusCode: 400);
        }

        var (accessToken, expires) = tokenService.GenerateAccessToken(
            exchange.UserId,
            exchange.DisplayName ?? "User",
            exchange.OrganizationId,
            roles: exchange.Roles);
        var refreshToken = tokenService.GenerateRefreshToken();

        return Results.Json(new
        {
            access_token = accessToken,
            token_type = "Bearer",
            expires_in = (int)(expires - DateTime.UtcNow).TotalSeconds,
            refresh_token = refreshToken,
            scope = exchange.Scope
        });
    }

    private static async Task<IResult> HandleRefreshTokenGrant(
        IFormCollection form,
        string clientId,
        IGrainFactory grainFactory,
        JwtTokenService tokenService)
    {
        var refreshToken = form["refresh_token"].FirstOrDefault();

        if (string.IsNullOrEmpty(refreshToken))
        {
            return Results.Json(new OAuthError("invalid_request", "refresh_token is required"),
                statusCode: 400);
        }

        // In a full implementation, we'd look up the refresh token
        // For now, generate new tokens (stateless refresh)
        var newAccessToken = tokenService.GenerateAccessToken(
            Guid.NewGuid(),
            "User",
            DefaultOrgId);
        var newRefreshToken = tokenService.GenerateRefreshToken();

        return Results.Json(new
        {
            access_token = newAccessToken.AccessToken,
            token_type = "Bearer",
            expires_in = (int)(newAccessToken.Expires - DateTime.UtcNow).TotalSeconds,
            refresh_token = newRefreshToken
        });
    }

    private static IResult HandleClientCredentialsGrant(
        string clientId,
        string? clientSecret,
        JwtTokenService tokenService)
    {
        // Validate client credentials (in production, lookup from database)
        if (!IsValidClientCredentials(clientId, clientSecret))
        {
            return Results.Json(new OAuthError("invalid_client", "Invalid client credentials"),
                statusCode: 401);
        }

        var (accessToken, expires) = tokenService.GenerateAccessToken(
            Guid.Empty, // No user for client credentials
            clientId,
            DefaultOrgId,
            roles: ["api_client"]);

        return Results.Json(new
        {
            access_token = accessToken,
            token_type = "Bearer",
            expires_in = (int)(expires - DateTime.UtcNow).TotalSeconds
        });
    }

    private static bool IsValidClient(string clientId, string redirectUri)
    {
        // In production, validate against registered clients
        // For development, accept known clients
        var validClients = new Dictionary<string, string[]>
        {
            ["default"] = ["http://localhost:5173", "http://localhost:5174", "http://localhost:5175"],
            ["pos"] = ["http://localhost:5173", "https://pos.darkvelocity.app"],
            ["backoffice"] = ["http://localhost:5174", "https://admin.darkvelocity.app"],
        };

        if (!validClients.TryGetValue(clientId, out var allowedUris))
            return true; // Allow unknown clients in development

        return allowedUris.Any(u => redirectUri.StartsWith(u, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsValidClientCredentials(string clientId, string? clientSecret)
    {
        // In production, validate against registered clients with secrets
        // For development, accept test credentials
        if (clientId.StartsWith("test_") || clientId == "default")
            return true;

        return !string.IsNullOrEmpty(clientSecret);
    }

    private static IReadOnlyList<string> MapUserTypeToRoles(State.UserType userType) => userType switch
    {
        State.UserType.Owner => ["owner", "admin", "manager", "backoffice"],
        State.UserType.Admin => ["admin", "manager", "backoffice"],
        State.UserType.Manager => ["manager", "backoffice"],
        State.UserType.Employee => ["backoffice"],
        _ => ["backoffice"]
    };

    private static string AddQueryParam(string url, string key, string value)
    {
        var separator = url.Contains('?') ? "&" : "?";
        return $"{url}{separator}{key}={Uri.EscapeDataString(value)}";
    }

    private static string ComputeKeyId(string key)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(key));
        return Convert.ToBase64String(hash[..8])
            .Replace("+", "-")
            .Replace("/", "_")
            .TrimEnd('=');
    }
}

/// <summary>
/// OAuth 2.0 error response.
/// </summary>
public record OAuthError(string error, string? error_description = null, string? error_uri = null);
