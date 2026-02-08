using System.Net;
using DarkVelocity.E2E.Clients;
using DarkVelocity.E2E.Fixtures;
using FluentAssertions;

namespace DarkVelocity.E2E.Scenarios;

[Collection(E2ECollection.Name)]
public class OrganizationLifecycleTests
{
    private readonly DarkVelocityClient _client;

    public OrganizationLifecycleTests(ServiceFixture fixture)
    {
        _client = new DarkVelocityClient(fixture.HttpClient, fixture.BaseUrl);
    }

    [Fact]
    public async Task Can_create_organization_and_site()
    {
        var orgId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        _client.Authenticate(userId, orgId);

        // Create organization
        using var createOrgResponse = await _client.PostAsync("/api/orgs", new
        {
            name = $"E2E Test Org {orgId:N}",
            slug = $"e2e-{orgId:N}"
        });

        createOrgResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        createOrgResponse.GetString("name").Should().Contain("E2E Test Org");
        createOrgResponse.GetLink("self").Should().Contain("/api/orgs/");
        createOrgResponse.GetLink("sites").Should().NotBeNull();

        var orgSelfLink = createOrgResponse.GetLink("self")!;

        // GET organization
        using var getOrgResponse = await _client.GetAsync(orgSelfLink);

        getOrgResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        getOrgResponse.GetString("name").Should().Contain("E2E Test Org");

        // Create site
        using var createSiteResponse = await _client.PostAsync(
            createOrgResponse.GetLink("sites")!,
            new
            {
                name = "E2E Test Site",
                code = "E2E",
                address = new
                {
                    street = "123 Test Street",
                    city = "London",
                    state = "England",
                    postalCode = "SW1A 1AA",
                    country = "GB"
                },
                timezone = "Europe/London",
                currency = "GBP"
            });

        createSiteResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        createSiteResponse.GetString("name").Should().Be("E2E Test Site");
        createSiteResponse.GetLink("self").Should().NotBeNull();
        createSiteResponse.GetLink("organization").Should().NotBeNull();
        createSiteResponse.GetLink("orders").Should().NotBeNull();

        // GET site
        using var getSiteResponse = await _client.GetAsync(createSiteResponse.GetLink("self")!);

        getSiteResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        getSiteResponse.GetString("name").Should().Be("E2E Test Site");
    }
}
