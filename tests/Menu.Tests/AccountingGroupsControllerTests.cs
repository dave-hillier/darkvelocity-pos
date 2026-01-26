using System.Net;
using System.Net.Http.Json;
using DarkVelocity.Menu.Api.Dtos;
using DarkVelocity.Shared.Contracts.Hal;
using FluentAssertions;

namespace DarkVelocity.Menu.Tests;

public class AccountingGroupsControllerTests : IClassFixture<MenuApiFixture>
{
    private readonly MenuApiFixture _fixture;
    private readonly HttpClient _client;

    public AccountingGroupsControllerTests(MenuApiFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.Client;
    }

    [Fact]
    public async Task GetAll_ReturnsAccountingGroups()
    {
        var response = await _client.GetAsync("/api/accounting-groups");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var collection = await response.Content.ReadFromJsonAsync<HalCollection<AccountingGroupDto>>();
        collection.Should().NotBeNull();
        collection!.Embedded.Items.Should().NotBeEmpty();
        collection.Embedded.Items.Should().Contain(g => g.Name == "Food");
    }

    [Fact]
    public async Task GetById_ReturnsAccountingGroup()
    {
        var response = await _client.GetAsync($"/api/accounting-groups/{_fixture.TestAccountingGroupId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var group = await response.Content.ReadFromJsonAsync<AccountingGroupDto>();
        group.Should().NotBeNull();
        group!.Name.Should().Be("Food");
        group.TaxRate.Should().Be(0.20m);
    }

    [Fact]
    public async Task GetById_NotFound_ReturnsNotFound()
    {
        var response = await _client.GetAsync($"/api/accounting-groups/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Create_CreatesAccountingGroup()
    {
        var request = new CreateAccountingGroupRequest(
            Name: "Beverages",
            Description: "Drink items",
            TaxRate: 0.20m);

        var response = await _client.PostAsJsonAsync("/api/accounting-groups", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var group = await response.Content.ReadFromJsonAsync<AccountingGroupDto>();
        group.Should().NotBeNull();
        group!.Name.Should().Be("Beverages");
        group.TaxRate.Should().Be(0.20m);
        group.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task Update_UpdatesAccountingGroup()
    {
        // First create a new group
        var createRequest = new CreateAccountingGroupRequest("Alcohol", null, 0.20m);
        var createResponse = await _client.PostAsJsonAsync("/api/accounting-groups", createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<AccountingGroupDto>();

        // Now update it
        var updateRequest = new UpdateAccountingGroupRequest(
            Name: "Alcoholic Beverages",
            Description: "Alcohol items",
            TaxRate: 0.25m,
            IsActive: null);

        var response = await _client.PutAsJsonAsync($"/api/accounting-groups/{created!.Id}", updateRequest);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var updated = await response.Content.ReadFromJsonAsync<AccountingGroupDto>();
        updated.Should().NotBeNull();
        updated!.Name.Should().Be("Alcoholic Beverages");
        updated.TaxRate.Should().Be(0.25m);
    }

    [Fact]
    public async Task Delete_DeactivatesAccountingGroup()
    {
        // First create a new group
        var createRequest = new CreateAccountingGroupRequest("To Delete", null, 0.10m);
        var createResponse = await _client.PostAsJsonAsync("/api/accounting-groups", createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<AccountingGroupDto>();

        // Now delete it
        var response = await _client.DeleteAsync($"/api/accounting-groups/{created!.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify it's deactivated
        var getResponse = await _client.GetAsync($"/api/accounting-groups/{created.Id}");
        var group = await getResponse.Content.ReadFromJsonAsync<AccountingGroupDto>();
        group!.IsActive.Should().BeFalse();
    }
}
