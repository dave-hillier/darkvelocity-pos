using DarkVelocity.Host;
using DarkVelocity.Host.Grains;
using DarkVelocity.Host.State;
using FluentAssertions;
using System.Security.Cryptography;
using System.Text;

namespace DarkVelocity.Tests.Grains.Devices;

/// <summary>
/// End-to-end integration tests for trusted device registration.
/// Exercises the full OAuth 2.0 Device Authorization Grant (RFC 8628) flow
/// across DeviceAuthGrain, DeviceGrain, SessionGrain, and UserLookupGrain,
/// mirroring what POS and KDS apps do when registering with the backend.
/// </summary>
[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class TrustedDeviceRegistrationTests
{
    private readonly TestClusterFixture _fixture;

    public TrustedDeviceRegistrationTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    private static string HashPin(string pin)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(pin));
        return Convert.ToBase64String(bytes);
    }

    /// <summary>
    /// Runs the full device auth flow: initiate → authorize → poll for token.
    /// Returns the token response containing the deviceId, orgId, siteId, and tokens.
    /// </summary>
    private async Task<DeviceTokenResponse> RegisterDeviceAsync(
        Guid orgId, Guid siteId, Guid authorizedBy,
        string clientId, string deviceName, DeviceType appType)
    {
        var userCode = GrainKeys.GenerateUserCode();
        var authGrain = _fixture.Cluster.GrainFactory.GetGrain<IDeviceAuthGrain>(userCode);

        // Step 1: Device initiates code request
        var codeResponse = await authGrain.InitiateAsync(new DeviceCodeRequest(
            ClientId: clientId,
            Scope: $"device {appType.ToString().ToLowerInvariant()}"));

        // Step 2: Admin authorizes the device
        await authGrain.AuthorizeAsync(new AuthorizeDeviceCommand(
            AuthorizedBy: authorizedBy,
            OrganizationId: orgId,
            SiteId: siteId,
            DeviceName: deviceName,
            AppType: appType));

        // Step 3: Device polls for token
        var tokenResponse = await authGrain.GetTokenAsync(codeResponse.DeviceCode);
        return tokenResponse!;
    }

    // Given: A POS app requesting device authorization
    // When: The full RFC 8628 device auth flow is completed (initiate → authorize → poll)
    // Then: The device is registered as a trusted POS device with correct organization, site, and type
    [Fact]
    public async Task Given_PosApp_When_DeviceAuthFlowCompleted_Then_DeviceIsTrusted()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var adminUserId = Guid.NewGuid();

        // Act - Full device auth flow
        var tokenResponse = await RegisterDeviceAsync(
            orgId, siteId, adminUserId,
            clientId: "pos-app",
            deviceName: "Register 1",
            appType: DeviceType.Pos);

        // Assert - Token response
        tokenResponse.AccessToken.Should().NotBeNullOrEmpty();
        tokenResponse.RefreshToken.Should().NotBeNullOrEmpty();
        tokenResponse.OrganizationId.Should().Be(orgId);
        tokenResponse.SiteId.Should().Be(siteId);
        tokenResponse.TokenType.Should().Be("Bearer");

        // Assert - Device is registered and trusted
        var deviceGrain = _fixture.Cluster.GrainFactory.GetGrain<IDeviceGrain>(
            GrainKeys.Device(orgId, tokenResponse.DeviceId));

        var isAuthorized = await deviceGrain.IsAuthorizedAsync();
        isAuthorized.Should().BeTrue();

        var snapshot = await deviceGrain.GetSnapshotAsync();
        snapshot.Name.Should().Be("Register 1");
        snapshot.Type.Should().Be(DeviceType.Pos);
        snapshot.SiteId.Should().Be(siteId);
        snapshot.OrganizationId.Should().Be(orgId);
        snapshot.Status.Should().Be(DeviceStatus.Authorized);
    }

    // Given: A KDS app requesting device authorization
    // When: The full RFC 8628 device auth flow is completed (initiate → authorize → poll)
    // Then: The device is registered as a trusted KDS device for the kitchen
    [Fact]
    public async Task Given_KdsApp_When_DeviceAuthFlowCompleted_Then_DeviceIsTrusted()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var adminUserId = Guid.NewGuid();

        // Act - Full device auth flow for KDS
        var tokenResponse = await RegisterDeviceAsync(
            orgId, siteId, adminUserId,
            clientId: "kds-app",
            deviceName: "Kitchen Display",
            appType: DeviceType.Kds);

        // Assert - Device is registered as KDS type
        var deviceGrain = _fixture.Cluster.GrainFactory.GetGrain<IDeviceGrain>(
            GrainKeys.Device(orgId, tokenResponse.DeviceId));

        var isAuthorized = await deviceGrain.IsAuthorizedAsync();
        isAuthorized.Should().BeTrue();

        var snapshot = await deviceGrain.GetSnapshotAsync();
        snapshot.Name.Should().Be("Kitchen Display");
        snapshot.Type.Should().Be(DeviceType.Kds);
        snapshot.SiteId.Should().Be(siteId);
    }

    // Given: A restaurant site needing both POS and KDS devices
    // When: Both devices complete the device auth flow at the same site
    // Then: Both devices are independently trusted with distinct identities and correct types
    [Fact]
    public async Task Given_PosAndKds_When_BothRegisteredAtSameSite_Then_BothDevicesAreTrusted()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var adminUserId = Guid.NewGuid();

        // Act - Register POS device
        var posToken = await RegisterDeviceAsync(
            orgId, siteId, adminUserId,
            clientId: "pos-app",
            deviceName: "Register 1",
            appType: DeviceType.Pos);

        // Act - Register KDS device at the same site
        var kdsToken = await RegisterDeviceAsync(
            orgId, siteId, adminUserId,
            clientId: "kds-app",
            deviceName: "Kitchen Display",
            appType: DeviceType.Kds);

        // Assert - Both devices have distinct IDs
        posToken.DeviceId.Should().NotBe(kdsToken.DeviceId);

        // Assert - POS device is trusted
        var posGrain = _fixture.Cluster.GrainFactory.GetGrain<IDeviceGrain>(
            GrainKeys.Device(orgId, posToken.DeviceId));
        (await posGrain.IsAuthorizedAsync()).Should().BeTrue();
        var posSnapshot = await posGrain.GetSnapshotAsync();
        posSnapshot.Type.Should().Be(DeviceType.Pos);

        // Assert - KDS device is trusted
        var kdsGrain = _fixture.Cluster.GrainFactory.GetGrain<IDeviceGrain>(
            GrainKeys.Device(orgId, kdsToken.DeviceId));
        (await kdsGrain.IsAuthorizedAsync()).Should().BeTrue();
        var kdsSnapshot = await kdsGrain.GetSnapshotAsync();
        kdsSnapshot.Type.Should().Be(DeviceType.Kds);

        // Assert - Both at the same site
        posSnapshot.SiteId.Should().Be(siteId);
        kdsSnapshot.SiteId.Should().Be(siteId);
    }

    // Given: A trusted POS device and a staff member with a PIN
    // When: The staff member logs in via PIN on the trusted device
    // Then: A session is created, the device tracks the current user, and the session is valid
    [Fact]
    public async Task Given_TrustedPosDevice_When_UserPinLogin_Then_SessionCreatedOnDevice()
    {
        // Arrange - Register the POS device
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var adminUserId = Guid.NewGuid();

        var posToken = await RegisterDeviceAsync(
            orgId, siteId, adminUserId,
            clientId: "pos-app",
            deviceName: "Register 1",
            appType: DeviceType.Pos);

        var deviceGrain = _fixture.Cluster.GrainFactory.GetGrain<IDeviceGrain>(
            GrainKeys.Device(orgId, posToken.DeviceId));

        // Arrange - Create a staff member with PIN and site access
        var userId = Guid.NewGuid();
        var userGrain = _fixture.Cluster.GrainFactory.GetGrain<IUserGrain>(
            GrainKeys.User(orgId, userId));
        await userGrain.CreateAsync(new CreateUserCommand(orgId, "bartender@example.com", "Jane Doe"));
        await userGrain.GrantSiteAccessAsync(siteId);
        await userGrain.SetPinAsync("1234");

        var lookupGrain = _fixture.Cluster.GrainFactory.GetGrain<IUserLookupGrain>(
            GrainKeys.UserLookup(orgId));
        await lookupGrain.RegisterPinAsync(userId, HashPin("1234"));

        // Act - PIN login flow (mirrors AuthEndpoints.cs PIN login)
        // 1. Verify device is authorized
        var isDeviceAuthorized = await deviceGrain.IsAuthorizedAsync();
        isDeviceAuthorized.Should().BeTrue("device must be trusted before PIN login");

        // 2. Look up user by PIN
        var lookupResult = await lookupGrain.FindByPinHashAsync(HashPin("1234"), siteId);
        lookupResult.Should().NotBeNull("user should be found by PIN with site access");

        // 3. Create session on the device
        var sessionId = Guid.NewGuid();
        var sessionGrain = _fixture.Cluster.GrainFactory.GetGrain<ISessionGrain>(
            GrainKeys.Session(orgId, sessionId));
        var tokens = await sessionGrain.CreateAsync(new CreateSessionCommand(
            lookupResult!.UserId, orgId, siteId, posToken.DeviceId, "pin", null, null));

        // 4. Set current user on device
        await deviceGrain.SetCurrentUserAsync(lookupResult.UserId);

        // Assert - Session is valid
        (await sessionGrain.IsValidAsync()).Should().BeTrue();
        tokens.AccessToken.Should().NotBeNullOrEmpty();
        tokens.RefreshToken.Should().NotBeNullOrEmpty();

        // Assert - Device tracks the logged-in user
        var deviceSnapshot = await deviceGrain.GetSnapshotAsync();
        deviceSnapshot.CurrentUserId.Should().Be(userId);
    }

    // Given: A device that has never been registered
    // When: PIN login is attempted on the unregistered device
    // Then: The device is not authorized and login would be rejected
    [Fact]
    public async Task Given_UnregisteredDevice_When_PinLoginAttempted_Then_DeviceNotAuthorized()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var unregisteredDeviceId = Guid.NewGuid();

        var deviceGrain = _fixture.Cluster.GrainFactory.GetGrain<IDeviceGrain>(
            GrainKeys.Device(orgId, unregisteredDeviceId));

        // Act - Check device authorization (same guard as AuthEndpoints.cs:20)
        var isAuthorized = await deviceGrain.IsAuthorizedAsync();

        // Assert - Unregistered device is not authorized
        isAuthorized.Should().BeFalse();
        (await deviceGrain.ExistsAsync()).Should().BeFalse();
    }

    // Given: A POS device that was registered but subsequently suspended
    // When: PIN login is attempted on the suspended device
    // Then: The device is not authorized and login would be rejected
    [Fact]
    public async Task Given_SuspendedDevice_When_PinLoginAttempted_Then_DeviceNotAuthorized()
    {
        // Arrange - Register and then suspend the device
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var adminUserId = Guid.NewGuid();

        var posToken = await RegisterDeviceAsync(
            orgId, siteId, adminUserId,
            clientId: "pos-app",
            deviceName: "Register 1",
            appType: DeviceType.Pos);

        var deviceGrain = _fixture.Cluster.GrainFactory.GetGrain<IDeviceGrain>(
            GrainKeys.Device(orgId, posToken.DeviceId));

        // Verify it was authorized after registration
        (await deviceGrain.IsAuthorizedAsync()).Should().BeTrue();

        // Suspend the device
        await deviceGrain.SuspendAsync("Security concern - reported stolen");

        // Act - Check device authorization (same guard as AuthEndpoints.cs:20)
        var isAuthorized = await deviceGrain.IsAuthorizedAsync();

        // Assert - Suspended device is not authorized
        isAuthorized.Should().BeFalse();
    }

    // Given: A device that has initiated the auth flow but not yet been approved
    // When: An admin denies the authorization request
    // Then: The device poll returns no token and no device is registered
    [Fact]
    public async Task Given_PendingDeviceAuth_When_AdminDenies_Then_DevicePollGetsNoToken()
    {
        // Arrange
        var userCode = GrainKeys.GenerateUserCode();
        var authGrain = _fixture.Cluster.GrainFactory.GetGrain<IDeviceAuthGrain>(userCode);
        var orgId = Guid.NewGuid();

        var codeResponse = await authGrain.InitiateAsync(new DeviceCodeRequest(
            ClientId: "pos-app",
            Scope: "device pos"));

        // Act - Admin denies the device
        await authGrain.DenyAsync("Unrecognized device");

        // Act - Device polls for token
        var tokenResponse = await authGrain.GetTokenAsync(codeResponse.DeviceCode);

        // Assert - No token issued
        tokenResponse.Should().BeNull();

        // Assert - Auth status is denied
        var status = await authGrain.GetStatusAsync();
        status.Should().Be(DeviceAuthStatus.Denied);
    }
}
