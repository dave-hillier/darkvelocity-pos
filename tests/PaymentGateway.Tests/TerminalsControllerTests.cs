using System.Net;
using System.Net.Http.Json;
using DarkVelocity.PaymentGateway.Api.Dtos;
using DarkVelocity.Shared.Contracts.Hal;
using FluentAssertions;

namespace DarkVelocity.PaymentGateway.Tests;

public class TerminalsControllerTests : IClassFixture<PaymentGatewayApiFixture>
{
    private readonly PaymentGatewayApiFixture _fixture;
    private readonly HttpClient _client;

    public TerminalsControllerTests(PaymentGatewayApiFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.Client;
    }

    [Fact]
    public async Task CreateTerminal_CreatesTerminalWithRegistrationCode()
    {
        var request = new CreateTerminalRequest(
            Label: "New Terminal",
            DeviceType: "simulated",
            LocationName: "Front Counter");

        var response = await _client.PostAsJsonAsync("/api/v1/terminals", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var terminal = await response.Content.ReadFromJsonAsync<TerminalRegistrationDto>();
        terminal.Should().NotBeNull();
        terminal!.Label.Should().Be("New Terminal");
        terminal.DeviceType.Should().Be("simulated");
        terminal.Status.Should().Be("pending");
        terminal.IsRegistered.Should().BeFalse();
        terminal.RegistrationCode.Should().NotBeNullOrEmpty();
        terminal.RegistrationCode.Should().HaveLength(8);
    }

    [Fact]
    public async Task GetAll_ReturnsTerminals()
    {
        var response = await _client.GetAsync("/api/v1/terminals");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var collection = await response.Content.ReadFromJsonAsync<HalCollection<TerminalDto>>();
        collection.Should().NotBeNull();
        collection!.Embedded.Items.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetById_ReturnsTerminal()
    {
        var response = await _client.GetAsync($"/api/v1/terminals/{_fixture.TestTerminalId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var terminal = await response.Content.ReadFromJsonAsync<TerminalDto>();
        terminal.Should().NotBeNull();
        terminal!.Id.Should().Be(_fixture.TestTerminalId);
        terminal.Status.Should().Be("online");
        terminal.IsRegistered.Should().BeTrue();
    }

    [Fact]
    public async Task RegisterTerminal_RegistersWithValidCode()
    {
        // Create a new terminal
        var createRequest = new CreateTerminalRequest(
            Label: "Terminal to Register",
            DeviceType: "simulated");

        var createResponse = await _client.PostAsJsonAsync("/api/v1/terminals", createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<TerminalRegistrationDto>();

        // Register it
        var registerRequest = new RegisterTerminalRequest(
            RegistrationCode: created!.RegistrationCode!);

        var response = await _client.PostAsJsonAsync(
            $"/api/v1/terminals/{created.Id}/register",
            registerRequest);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var registered = await response.Content.ReadFromJsonAsync<TerminalDto>();
        registered!.IsRegistered.Should().BeTrue();
        registered.Status.Should().Be("online");
        registered.SerialNumber.Should().StartWith("SIM-");
    }

    [Fact]
    public async Task RegisterTerminal_InvalidCode_ReturnsBadRequest()
    {
        var createRequest = new CreateTerminalRequest(Label: "Terminal", DeviceType: "simulated");
        var createResponse = await _client.PostAsJsonAsync("/api/v1/terminals", createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<TerminalRegistrationDto>();

        var registerRequest = new RegisterTerminalRequest(RegistrationCode: "WRONGCOD");

        var response = await _client.PostAsJsonAsync(
            $"/api/v1/terminals/{created!.Id}/register",
            registerRequest);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UpdateTerminal_UpdatesLabel()
    {
        var request = new UpdateTerminalRequest(Label: "Updated Terminal Name");

        var response = await _client.PatchAsJsonAsync(
            $"/api/v1/terminals/{_fixture.TestTerminalId}",
            request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var terminal = await response.Content.ReadFromJsonAsync<TerminalDto>();
        terminal!.Label.Should().Be("Updated Terminal Name");
    }

    [Fact]
    public async Task DeleteTerminal_DeletesTerminal()
    {
        // Create a terminal to delete
        var createRequest = new CreateTerminalRequest(Label: "Terminal to Delete", DeviceType: "simulated");
        var createResponse = await _client.PostAsJsonAsync("/api/v1/terminals", createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<TerminalDto>();

        // Delete it
        var response = await _client.DeleteAsync($"/api/v1/terminals/{created!.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify it's gone
        var getResponse = await _client.GetAsync($"/api/v1/terminals/{created.Id}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CollectPaymentMethod_LinksPaymentIntentToTerminal()
    {
        // Create a payment intent
        var piRequest = new CreatePaymentIntentRequest(Amount: 2000, Currency: "usd", Channel: "pos");
        var piResponse = await _client.PostAsJsonAsync("/api/v1/payment_intents", piRequest);
        var pi = await piResponse.Content.ReadFromJsonAsync<PaymentIntentDto>();

        // Collect payment method
        var collectRequest = new TerminalCollectPaymentRequest(PaymentIntentId: pi!.Id);
        var response = await _client.PostAsJsonAsync(
            $"/api/v1/terminals/{_fixture.TestTerminalId}/collect_payment_method",
            collectRequest);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var action = await response.Content.ReadFromJsonAsync<TerminalReaderActionDto>();
        action!.ActionType.Should().Be("collect_payment_method");
        action.Status.Should().Be("in_progress");
    }

    [Fact]
    public async Task ProcessPayment_ProcessesCardPresentPayment()
    {
        // Create a payment intent
        var piRequest = new CreatePaymentIntentRequest(Amount: 3500, Currency: "usd", Channel: "pos");
        var piResponse = await _client.PostAsJsonAsync("/api/v1/payment_intents", piRequest);
        var pi = await piResponse.Content.ReadFromJsonAsync<PaymentIntentDto>();

        // Process payment
        var processRequest = new TerminalCollectPaymentRequest(PaymentIntentId: pi!.Id);
        var response = await _client.PostAsJsonAsync(
            $"/api/v1/terminals/{_fixture.TestTerminalId}/process_payment",
            processRequest);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var action = await response.Content.ReadFromJsonAsync<TerminalReaderActionDto>();
        action!.ActionType.Should().Be("process_payment");
        action.Status.Should().Be("succeeded");

        // Verify payment intent is succeeded
        var piGetResponse = await _client.GetAsync($"/api/v1/payment_intents/{pi.Id}");
        var updatedPi = await piGetResponse.Content.ReadFromJsonAsync<PaymentIntentDto>();
        updatedPi!.Status.Should().Be("succeeded");
        updatedPi.PaymentMethodType.Should().Be("card_present");
    }

    [Fact]
    public async Task ProcessPayment_OfflineTerminal_ReturnsBadRequest()
    {
        // Create an unregistered terminal
        var createRequest = new CreateTerminalRequest(Label: "Offline Terminal", DeviceType: "simulated");
        var createResponse = await _client.PostAsJsonAsync("/api/v1/terminals", createRequest);
        var terminal = await createResponse.Content.ReadFromJsonAsync<TerminalDto>();

        // Create a payment intent
        var piRequest = new CreatePaymentIntentRequest(Amount: 1000, Currency: "usd", Channel: "pos");
        var piResponse = await _client.PostAsJsonAsync("/api/v1/payment_intents", piRequest);
        var pi = await piResponse.Content.ReadFromJsonAsync<PaymentIntentDto>();

        // Try to process on unregistered terminal
        var processRequest = new TerminalCollectPaymentRequest(PaymentIntentId: pi!.Id);
        var response = await _client.PostAsJsonAsync(
            $"/api/v1/terminals/{terminal!.Id}/process_payment",
            processRequest);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
