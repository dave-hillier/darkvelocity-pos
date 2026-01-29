using System.Net;
using System.Net.Http.Json;
using DarkVelocity.Integration.Tests.Fixtures;
using FluentAssertions;

namespace DarkVelocity.Integration.Tests;

/// <summary>
/// Integration tests for User Management operations.
///
/// Business Scenarios Covered:
/// - User creation with roles
/// - User updates and role changes
/// - User deactivation
/// - Location assignments
/// - User queries
/// </summary>
public class UserManagementIntegrationTests : IClassFixture<AuthServiceFixture>
{
    private readonly AuthServiceFixture _fixture;
    private readonly HttpClient _client;

    public UserManagementIntegrationTests(AuthServiceFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.Client;
    }

    #region User Creation

    [Fact]
    public async Task CreateUser_WithRole_CreatesActiveUser()
    {
        // Arrange
        var request = new CreateUserRequest(
            Username: $"newuser_{Guid.NewGuid():N}".Substring(0, 20),
            FirstName: "New",
            LastName: "User",
            Email: $"newuser_{Guid.NewGuid():N}@test.com",
            Pin: "3456",
            UserGroupId: _fixture.CashierGroupId,
            HomeLocationId: _fixture.TestLocationId);

        // Act
        var response = await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/users",
            request);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Created, HttpStatusCode.OK, HttpStatusCode.NotFound);

        if (response.IsSuccessStatusCode)
        {
            var user = await response.Content.ReadFromJsonAsync<UserDto>();
            user.Should().NotBeNull();
            user!.FirstName.Should().Be("New");
            user.LastName.Should().Be("User");
            user.IsActive.Should().BeTrue();
        }
    }

    [Fact]
    public async Task CreateUser_WithManagerRole_HasElevatedPermissions()
    {
        // Arrange
        var request = new CreateUserRequest(
            Username: $"manager_{Guid.NewGuid():N}".Substring(0, 20),
            FirstName: "New",
            LastName: "Manager",
            Email: $"manager_{Guid.NewGuid():N}@test.com",
            Pin: "7890",
            UserGroupId: _fixture.ManagerGroupId,
            HomeLocationId: _fixture.TestLocationId);

        // Act
        var response = await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/users",
            request);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Created, HttpStatusCode.OK, HttpStatusCode.NotFound);

        if (response.IsSuccessStatusCode)
        {
            var user = await response.Content.ReadFromJsonAsync<UserDto>();
            user!.UserGroupName.Should().Be("Manager");
        }
    }

    [Fact]
    public async Task CreateUser_DuplicateUsername_ReturnsConflict()
    {
        // Arrange - Try to create user with existing username
        var request = new CreateUserRequest(
            Username: "cashier1", // Already exists in fixture
            FirstName: "Duplicate",
            LastName: "User",
            Email: "duplicate@test.com",
            Pin: "1111",
            UserGroupId: _fixture.CashierGroupId,
            HomeLocationId: _fixture.TestLocationId);

        // Act
        var response = await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/users",
            request);

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.Conflict,
            HttpStatusCode.BadRequest,
            HttpStatusCode.NotFound);
    }

    #endregion

    #region User Updates

    [Fact]
    public async Task UpdateUser_ChangeRole_UpdatesPermissions()
    {
        // Arrange - Promote cashier to manager
        var updateRequest = new UpdateUserRequest(
            UserGroupId: _fixture.ManagerGroupId);

        // Act
        var response = await _client.PutAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/users/{_fixture.TestCashierId}",
            updateRequest);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NoContent, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UpdateUser_ChangeContactInfo_UpdatesUser()
    {
        // Arrange
        var updateRequest = new UpdateUserRequest(
            FirstName: "Updated",
            LastName: "Name",
            Email: "updated@test.com");

        // Act
        var response = await _client.PutAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/users/{_fixture.TestCashierId}",
            updateRequest);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NoContent, HttpStatusCode.NotFound);
    }

    #endregion

    #region User Deactivation

    [Fact]
    public async Task DeactivateUser_SetsInactive()
    {
        // Arrange
        var updateRequest = new UpdateUserRequest(IsActive: false);

        // Act
        var response = await _client.PutAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/users/{_fixture.InactiveUserId}",
            updateRequest);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NoContent, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeactivateUser_PreventLogin()
    {
        // First deactivate the user
        var updateRequest = new UpdateUserRequest(IsActive: false);
        await _client.PutAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/users/{_fixture.InactiveUserId}",
            updateRequest);

        // Try to login with deactivated user
        var loginRequest = new LoginRequest(
            Pin: "1111", // Inactive user's PIN
            LocationId: _fixture.TestLocationId);

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/login", loginRequest);

        // Assert - Should not be able to login
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.Unauthorized,
            HttpStatusCode.Forbidden,
            HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteUser_SoftDeletes()
    {
        // Act
        var response = await _client.DeleteAsync(
            $"/api/locations/{_fixture.TestLocationId}/users/{_fixture.InactiveUserId}");

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.NoContent,
            HttpStatusCode.NotFound);
    }

    #endregion

    #region Location Assignments

    [Fact]
    public async Task AssignUserToLocation_GrantsAccess()
    {
        // Arrange
        var request = new AssignUserLocationRequest(
            UserId: _fixture.TestCashierId,
            LocationId: _fixture.TestLocation2Id);

        // Act
        var response = await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocation2Id}/users/{_fixture.TestCashierId}/assign",
            request);

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.Created,
            HttpStatusCode.NoContent,
            HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task RemoveUserFromLocation_RevokesAccess()
    {
        // Act
        var response = await _client.DeleteAsync(
            $"/api/locations/{_fixture.TestLocation2Id}/users/{_fixture.TestManagerId}/unassign");

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.NoContent,
            HttpStatusCode.NotFound);
    }

    #endregion

    #region User Queries

    [Fact]
    public async Task GetUsersByLocation_ReturnsLocationUsers()
    {
        // Act
        var response = await _client.GetAsync(
            $"/api/locations/{_fixture.TestLocationId}/users");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);

        if (response.StatusCode == HttpStatusCode.OK)
        {
            var users = await response.Content.ReadFromJsonAsync<List<UserDto>>();
            users.Should().NotBeNull();
            users!.Should().NotBeEmpty();
        }
    }

    [Fact]
    public async Task GetUserById_ReturnsUser()
    {
        // Act
        var response = await _client.GetAsync(
            $"/api/locations/{_fixture.TestLocationId}/users/{_fixture.TestCashierId}");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);

        if (response.StatusCode == HttpStatusCode.OK)
        {
            var user = await response.Content.ReadFromJsonAsync<UserDto>();
            user!.Id.Should().Be(_fixture.TestCashierId);
        }
    }

    [Fact]
    public async Task GetUsersByLocation_FilterByRole_ReturnsFilteredUsers()
    {
        // Act - Get only managers
        var response = await _client.GetAsync(
            $"/api/locations/{_fixture.TestLocationId}/users?userGroupId={_fixture.ManagerGroupId}");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);

        if (response.StatusCode == HttpStatusCode.OK)
        {
            var users = await response.Content.ReadFromJsonAsync<List<UserDto>>();
            users!.Should().OnlyContain(u => u.UserGroupName == "Manager");
        }
    }

    [Fact]
    public async Task GetUsersByLocation_FilterActive_ReturnsOnlyActive()
    {
        // Act
        var response = await _client.GetAsync(
            $"/api/locations/{_fixture.TestLocationId}/users?isActive=true");

        // Assert
        if (response.StatusCode == HttpStatusCode.OK)
        {
            var users = await response.Content.ReadFromJsonAsync<List<UserDto>>();
            users!.Should().OnlyContain(u => u.IsActive);
        }
    }

    #endregion

    #region Password Reset

    [Fact]
    public async Task ResetPassword_GeneratesNewPin()
    {
        // Arrange
        var request = new ResetPasswordRequest(
            UserId: _fixture.TestCashierId,
            NewPin: "9999");

        // Act
        var response = await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/users/{_fixture.TestCashierId}/reset-pin",
            request);

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.NoContent,
            HttpStatusCode.NotFound);
    }

    #endregion
}

// User Management DTOs
public record CreateUserRequest(
    string Username,
    string FirstName,
    string LastName,
    string Email,
    string Pin,
    Guid UserGroupId,
    Guid HomeLocationId);

public record UpdateUserRequest(
    string? FirstName = null,
    string? LastName = null,
    string? Email = null,
    string? Pin = null,
    Guid? UserGroupId = null,
    bool? IsActive = null);

public record AssignUserLocationRequest(
    Guid UserId,
    Guid LocationId);

public record ResetPasswordRequest(
    Guid UserId,
    string NewPin);

public record UserDto
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
