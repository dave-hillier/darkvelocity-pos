using System.Net;
using System.Net.Http.Json;
using DarkVelocity.Auth.Api.Dtos;
using DarkVelocity.Shared.Contracts.Hal;
using FluentAssertions;

namespace DarkVelocity.Auth.Tests;

public class UsersControllerTests : IClassFixture<AuthApiFixture>
{
    private readonly AuthApiFixture _fixture;
    private readonly HttpClient _client;

    public UsersControllerTests(AuthApiFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.Client;
    }

    [Fact]
    public async Task GetUsers_ReturnsUsersList()
    {
        // Act
        var response = await _client.GetAsync($"/api/locations/{_fixture.TestLocationId}/users");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<HalCollection<UserDto>>();
        result.Should().NotBeNull();
        result!.Count.Should().BeGreaterThan(0);
        result.Embedded.Items.Should().Contain(u => u.Username == "testuser");
    }

    [Fact]
    public async Task GetUser_WithValidId_ReturnsUser()
    {
        // Act
        var response = await _client.GetAsync(
            $"/api/locations/{_fixture.TestLocationId}/users/{_fixture.TestUserId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<UserDto>();
        result.Should().NotBeNull();
        result!.Id.Should().Be(_fixture.TestUserId);
        result.Username.Should().Be("testuser");
        result.Links.Should().ContainKey("self");
    }

    [Fact]
    public async Task GetUser_WithInvalidId_ReturnsNotFound()
    {
        // Act
        var response = await _client.GetAsync(
            $"/api/locations/{_fixture.TestLocationId}/users/{Guid.NewGuid()}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CreateUser_WithValidData_ReturnsCreatedUser()
    {
        // Arrange
        var request = new CreateUserRequest(
            Username: "newuser",
            FirstName: "New",
            LastName: "User",
            Email: "new@example.com",
            Pin: "5678",
            UserGroupId: _fixture.TestUserGroupId,
            HomeLocationId: _fixture.TestLocationId
        );

        // Act
        var response = await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/users", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var result = await response.Content.ReadFromJsonAsync<UserDto>();
        result.Should().NotBeNull();
        result!.Username.Should().Be("newuser");
        result.FirstName.Should().Be("New");
        result.LastName.Should().Be("User");

        // Verify user can login with new PIN
        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest("5678", _fixture.TestLocationId));
        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task CreateUser_WithDuplicateUsername_ReturnsConflict()
    {
        // Arrange
        var request = new CreateUserRequest(
            Username: "testuser", // Already exists
            FirstName: "Another",
            LastName: "User",
            Email: "another@example.com",
            Pin: "9999",
            UserGroupId: _fixture.TestUserGroupId,
            HomeLocationId: _fixture.TestLocationId
        );

        // Act
        var response = await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/users", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task UpdateUser_WithValidData_ReturnsUpdatedUser()
    {
        // Arrange
        var request = new UpdateUserRequest(
            FirstName: "Updated",
            LastName: "Name",
            Email: null,
            Pin: null,
            UserGroupId: null,
            IsActive: null
        );

        // Act
        var response = await _client.PutAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/users/{_fixture.TestUserId}", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<UserDto>();
        result.Should().NotBeNull();
        result!.FirstName.Should().Be("Updated");
        result.LastName.Should().Be("Name");
    }

    [Fact]
    public async Task UpdateUser_ChangePin_NewPinWorks()
    {
        // Arrange - Create a new user for this test
        var createRequest = new CreateUserRequest(
            Username: "pinchangeuser",
            FirstName: "Pin",
            LastName: "Change",
            Email: null,
            Pin: "1111",
            UserGroupId: _fixture.TestUserGroupId,
            HomeLocationId: _fixture.TestLocationId
        );
        var createResponse = await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/users", createRequest);
        var createdUser = await createResponse.Content.ReadFromJsonAsync<UserDto>();

        // Act - Update PIN
        var updateRequest = new UpdateUserRequest(
            FirstName: null,
            LastName: null,
            Email: null,
            Pin: "2222",
            UserGroupId: null,
            IsActive: null
        );
        await _client.PutAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/users/{createdUser!.Id}", updateRequest);

        // Assert - Old PIN fails
        var oldPinResponse = await _client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest("1111", _fixture.TestLocationId));
        oldPinResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // Assert - New PIN works
        var newPinResponse = await _client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest("2222", _fixture.TestLocationId));
        newPinResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task DeleteUser_DeactivatesUser()
    {
        // Arrange - Create a user to delete
        var createRequest = new CreateUserRequest(
            Username: "userToDelete",
            FirstName: "Delete",
            LastName: "Me",
            Email: null,
            Pin: "3333",
            UserGroupId: _fixture.TestUserGroupId,
            HomeLocationId: _fixture.TestLocationId
        );
        var createResponse = await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/users", createRequest);
        var createdUser = await createResponse.Content.ReadFromJsonAsync<UserDto>();

        // Act
        var deleteResponse = await _client.DeleteAsync(
            $"/api/locations/{_fixture.TestLocationId}/users/{createdUser!.Id}");

        // Assert
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify user cannot login
        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest("3333", _fixture.TestLocationId));
        loginResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
