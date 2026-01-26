using System.Net;
using System.Net.Http.Json;
using DarkVelocity.Auth.Api.Dtos;
using FluentAssertions;

namespace DarkVelocity.Auth.Tests;

public class AuthControllerTests : IClassFixture<AuthApiFixture>
{
    private readonly AuthApiFixture _fixture;
    private readonly HttpClient _client;

    public AuthControllerTests(AuthApiFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.Client;
    }

    [Fact]
    public async Task Login_WithValidPin_ReturnsToken()
    {
        // Arrange
        var request = new LoginRequest(_fixture.TestUserPin, _fixture.TestLocationId);

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/login", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<LoginResponse>();
        result.Should().NotBeNull();
        result!.AccessToken.Should().NotBeNullOrEmpty();
        result.RefreshToken.Should().NotBeNullOrEmpty();
        result.User.Should().NotBeNull();
        result.User.Username.Should().Be("testuser");
        result.User.FirstName.Should().Be("Test");
        result.User.LastName.Should().Be("User");
    }

    [Fact]
    public async Task Login_WithInvalidPin_ReturnsUnauthorized()
    {
        // Arrange
        var request = new LoginRequest("9999", _fixture.TestLocationId);

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/login", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task LoginWithQr_WithValidToken_ReturnsToken()
    {
        // Arrange
        var request = new QrLoginRequest("test-qr-token", _fixture.TestLocationId);

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/login/qr", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<LoginResponse>();
        result.Should().NotBeNull();
        result!.AccessToken.Should().NotBeNullOrEmpty();
        result.User.Username.Should().Be("testuser");
    }

    [Fact]
    public async Task LoginWithQr_WithInvalidToken_ReturnsUnauthorized()
    {
        // Arrange
        var request = new QrLoginRequest("invalid-token", _fixture.TestLocationId);

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/login/qr", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Refresh_WithValidToken_ReturnsNewTokens()
    {
        // Arrange - First login to get tokens
        var loginRequest = new LoginRequest(_fixture.TestUserPin, _fixture.TestLocationId);
        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", loginRequest);
        var loginResult = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();

        var refreshRequest = new RefreshRequest(loginResult!.RefreshToken);

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/refresh", refreshRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<LoginResponse>();
        result.Should().NotBeNull();
        result!.AccessToken.Should().NotBeNullOrEmpty();
        result.RefreshToken.Should().NotBeNullOrEmpty();
        result.User.Username.Should().Be("testuser");
    }
}
