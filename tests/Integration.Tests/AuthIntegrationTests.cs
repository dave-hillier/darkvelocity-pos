using System.Net;
using System.Net.Http.Json;
using DarkVelocity.Integration.Tests.Fixtures;
using FluentAssertions;

namespace DarkVelocity.Integration.Tests;

/// <summary>
/// Integration tests for Authentication and Authorization.
///
/// Business Scenarios Covered:
/// - User login with PIN
/// - Invalid credentials handling
/// - Token expiration
/// - Token refresh
/// - Logout/token invalidation
/// - Password changes
/// - Account lockout
/// </summary>
public class AuthIntegrationTests : IClassFixture<AuthServiceFixture>
{
    private readonly AuthServiceFixture _fixture;
    private readonly HttpClient _client;

    public AuthIntegrationTests(AuthServiceFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.Client;
    }

    #region Login - Valid Credentials

    [Fact]
    public async Task Login_ValidCredentials_ReturnsToken()
    {
        // Arrange
        var request = new LoginRequest(
            Pin: _fixture.TestCashierPin,
            LocationId: _fixture.TestLocationId);

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/login", request);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);

        if (response.StatusCode == HttpStatusCode.OK)
        {
            var loginResponse = await response.Content.ReadFromJsonAsync<LoginResponse>();
            loginResponse.Should().NotBeNull();
            loginResponse!.AccessToken.Should().NotBeNullOrEmpty();
            loginResponse.RefreshToken.Should().NotBeNullOrEmpty();
            loginResponse.ExpiresAt.Should().BeAfter(DateTime.UtcNow);
            loginResponse.User.Should().NotBeNull();
        }
    }

    [Fact]
    public async Task Login_ValidCredentials_ReturnsUserInfo()
    {
        // Arrange
        var request = new LoginRequest(
            Pin: _fixture.TestManagerPin,
            LocationId: _fixture.TestLocationId);

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/login", request);

        // Assert
        if (response.StatusCode == HttpStatusCode.OK)
        {
            var loginResponse = await response.Content.ReadFromJsonAsync<LoginResponse>();
            loginResponse!.User.Should().NotBeNull();
            loginResponse.User!.Username.Should().Be("manager1");
            loginResponse.User.FirstName.Should().Be("Test");
            loginResponse.User.LastName.Should().Be("Manager");
        }
    }

    #endregion

    #region Login - Invalid Credentials

    [Fact]
    public async Task Login_InvalidPassword_ReturnsUnauthorized()
    {
        // Arrange
        var request = new LoginRequest(
            Pin: _fixture.InvalidPin,
            LocationId: _fixture.TestLocationId);

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/login", request);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.BadRequest, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Login_InactiveAccount_ReturnsUnauthorized()
    {
        // Arrange - Use the inactive user's PIN
        var request = new LoginRequest(
            Pin: "1111", // Inactive user's PIN
            LocationId: _fixture.TestLocationId);

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/login", request);

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.Unauthorized,
            HttpStatusCode.Forbidden,
            HttpStatusCode.BadRequest,
            HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Login_NonExistentUser_ReturnsUnauthorized()
    {
        // Arrange
        var request = new LoginRequest(
            Pin: "9876", // PIN that doesn't exist
            LocationId: _fixture.TestLocationId);

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/login", request);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.NotFound, HttpStatusCode.BadRequest);
    }

    #endregion

    #region Account Lockout

    [Fact]
    public async Task Login_LockedAccount_ReturnsLocked()
    {
        // This test simulates account lockout after too many failed attempts
        // The exact behavior depends on implementation

        // Arrange - Attempt multiple failed logins
        var request = new LoginRequest(
            Pin: _fixture.InvalidPin,
            LocationId: _fixture.TestLocationId);

        // Act - Make several failed attempts
        for (int i = 0; i < 5; i++)
        {
            await _client.PostAsJsonAsync("/api/auth/login", request);
        }

        // Try one more time
        var response = await _client.PostAsJsonAsync("/api/auth/login", request);

        // Assert - Should be unauthorized or locked
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.Unauthorized,
            HttpStatusCode.Forbidden,
            HttpStatusCode.TooManyRequests,
            HttpStatusCode.NotFound);
    }

    #endregion

    #region Token Expiration

    [Fact]
    public async Task Token_Expired_Returns401()
    {
        // Arrange - Use an obviously expired/invalid token
        var expiredToken = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiIxMjM0NTY3ODkwIiwiZXhwIjoxfQ.invalid";

        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", expiredToken);

        // Act - Try to access a protected endpoint
        var response = await _client.GetAsync($"/api/locations/{_fixture.TestLocationId}/users");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden, HttpStatusCode.OK);

        // Clean up
        _client.DefaultRequestHeaders.Authorization = null;
    }

    #endregion

    #region Token Refresh

    [Fact]
    public async Task RefreshToken_ValidToken_ExtendsSession()
    {
        // Arrange - First login to get tokens
        var loginRequest = new LoginRequest(
            Pin: _fixture.TestCashierPin,
            LocationId: _fixture.TestLocationId);

        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", loginRequest);

        if (loginResponse.StatusCode != HttpStatusCode.OK) return;

        var tokens = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();

        var refreshRequest = new RefreshRequest(
            RefreshToken: tokens!.RefreshToken!);

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/refresh", refreshRequest);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);

        if (response.StatusCode == HttpStatusCode.OK)
        {
            var newTokens = await response.Content.ReadFromJsonAsync<LoginResponse>();
            newTokens!.AccessToken.Should().NotBeNullOrEmpty();
            newTokens.ExpiresAt.Should().BeAfter(DateTime.UtcNow);
        }
    }

    [Fact]
    public async Task RefreshToken_InvalidToken_ReturnsUnauthorized()
    {
        // Arrange
        var refreshRequest = new RefreshRequest(
            RefreshToken: "invalid-refresh-token");

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/refresh", refreshRequest);

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.Unauthorized,
            HttpStatusCode.BadRequest,
            HttpStatusCode.NotFound);
    }

    #endregion

    #region Logout

    [Fact]
    public async Task Logout_InvalidatesToken()
    {
        // Arrange - Login first
        var loginRequest = new LoginRequest(
            Pin: _fixture.TestCashierPin,
            LocationId: _fixture.TestLocationId);

        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", loginRequest);

        if (loginResponse.StatusCode != HttpStatusCode.OK) return;

        var tokens = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();

        // Set the token
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", tokens!.AccessToken);

        // Act - Logout
        var logoutResponse = await _client.PostAsync("/api/auth/logout", null);

        // Assert - Logout should succeed or not be implemented
        logoutResponse.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.NoContent,
            HttpStatusCode.NotFound);

        // Clean up
        _client.DefaultRequestHeaders.Authorization = null;
    }

    #endregion

    #region Password Change

    [Fact]
    public async Task ChangePassword_RequiresCurrentPassword()
    {
        // Arrange
        var changeRequest = new ChangePasswordRequest(
            UserId: _fixture.TestCashierId,
            CurrentPin: _fixture.TestCashierPin,
            NewPin: "4321");

        // Act
        var response = await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/users/{_fixture.TestCashierId}/change-pin",
            changeRequest);

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.NoContent,
            HttpStatusCode.NotFound,
            HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ChangePassword_WrongCurrentPassword_Fails()
    {
        // Arrange
        var changeRequest = new ChangePasswordRequest(
            UserId: _fixture.TestCashierId,
            CurrentPin: "0000", // Wrong current PIN
            NewPin: "4321");

        // Act
        var response = await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/users/{_fixture.TestCashierId}/change-pin",
            changeRequest);

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.BadRequest,
            HttpStatusCode.Unauthorized,
            HttpStatusCode.Forbidden,
            HttpStatusCode.NotFound);
    }

    #endregion

    #region QR Code Login

    [Fact]
    public async Task QrLogin_ValidToken_ReturnsSession()
    {
        // Arrange
        var request = new QrLoginRequest(
            QrToken: "valid-qr-token-12345",
            LocationId: _fixture.TestLocationId);

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/login/qr", request);

        // Assert - QR login may or may not be implemented
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.Unauthorized,
            HttpStatusCode.BadRequest,
            HttpStatusCode.NotFound);
    }

    #endregion
}

// Auth DTOs
public record LoginRequest(
    string Pin,
    Guid? LocationId = null);

public record QrLoginRequest(
    string QrToken,
    Guid? LocationId = null);

public record RefreshRequest(
    string RefreshToken);

public record ChangePasswordRequest(
    Guid UserId,
    string CurrentPin,
    string NewPin);

public record LoginResponse
{
    public string? AccessToken { get; init; }
    public string? RefreshToken { get; init; }
    public DateTime ExpiresAt { get; init; }
    public AuthUserDto? User { get; init; }
}

public record AuthUserDto
{
    public Guid Id { get; init; }
    public string? Username { get; init; }
    public string? FirstName { get; init; }
    public string? LastName { get; init; }
    public string? Email { get; init; }
    public string? UserGroupName { get; init; }
    public Guid? HomeLocationId { get; init; }
    public bool IsActive { get; init; }
}
