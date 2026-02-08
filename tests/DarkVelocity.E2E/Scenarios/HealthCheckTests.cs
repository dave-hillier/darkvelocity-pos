using System.Net;
using DarkVelocity.E2E.Fixtures;
using FluentAssertions;

namespace DarkVelocity.E2E.Scenarios;

[Collection(E2ECollection.Name)]
public class HealthCheckTests
{
    private readonly ServiceFixture _fixture;

    public HealthCheckTests(ServiceFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Health_endpoint_returns_healthy()
    {
        var response = await _fixture.HttpClient.GetAsync($"{_fixture.BaseUrl}/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
