using System.Net;
using System.Net.Http.Json;
using DarkVelocity.Hardware.Api.Dtos;
using DarkVelocity.Shared.Contracts.Hal;
using FluentAssertions;

namespace DarkVelocity.Hardware.Tests;

public class PrintersControllerTests : IClassFixture<HardwareApiFixture>
{
    private readonly HardwareApiFixture _fixture;
    private readonly HttpClient _client;

    public PrintersControllerTests(HardwareApiFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.Client;
    }

    [Fact]
    public async Task GetAll_ReturnsPrinters()
    {
        var response = await _client.GetAsync($"/api/locations/{_fixture.TestLocationId}/printers");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var collection = await response.Content.ReadFromJsonAsync<HalCollection<PrinterDto>>();
        collection.Should().NotBeNull();
        collection!.Embedded.Items.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetAll_FiltersByActive()
    {
        var response = await _client.GetAsync($"/api/locations/{_fixture.TestLocationId}/printers?activeOnly=true");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var collection = await response.Content.ReadFromJsonAsync<HalCollection<PrinterDto>>();
        collection.Should().NotBeNull();
        collection!.Embedded.Items.Should().OnlyContain(p => p.IsActive);
    }

    [Fact]
    public async Task GetAll_FiltersByType()
    {
        var response = await _client.GetAsync($"/api/locations/{_fixture.TestLocationId}/printers?printerType=kitchen");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var collection = await response.Content.ReadFromJsonAsync<HalCollection<PrinterDto>>();
        collection.Should().NotBeNull();
        collection!.Embedded.Items.Should().OnlyContain(p => p.PrinterType == "kitchen");
    }

    [Fact]
    public async Task GetById_ReturnsPrinter()
    {
        var response = await _client.GetAsync($"/api/locations/{_fixture.TestLocationId}/printers/{_fixture.TestPrinterId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var printer = await response.Content.ReadFromJsonAsync<PrinterDto>();
        printer.Should().NotBeNull();
        printer!.Id.Should().Be(_fixture.TestPrinterId);
        printer.Name.Should().Be("Receipt Printer 1");
        printer.PrinterType.Should().Be("receipt");
    }

    [Fact]
    public async Task GetById_NotFound_ReturnsNotFound()
    {
        var response = await _client.GetAsync($"/api/locations/{_fixture.TestLocationId}/printers/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Create_CreatesPrinter()
    {
        var request = new CreatePrinterRequest(
            Name: "New Printer",
            PrinterType: "receipt",
            ConnectionType: "usb",
            UsbVendorId: "0416",
            UsbProductId: "5011",
            PaperWidth: 58);

        var response = await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/printers",
            request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var printer = await response.Content.ReadFromJsonAsync<PrinterDto>();
        printer.Should().NotBeNull();
        printer!.Name.Should().Be("New Printer");
        printer.PrinterType.Should().Be("receipt");
        printer.ConnectionType.Should().Be("usb");
        printer.PaperWidth.Should().Be(58);
        printer.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task Create_DuplicateName_ReturnsConflict()
    {
        var request = new CreatePrinterRequest(
            Name: "Receipt Printer 1",  // Already exists
            PrinterType: "receipt",
            ConnectionType: "network");

        var response = await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/printers",
            request);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Create_AsDefault_UnsetsOtherDefaults()
    {
        // First verify existing default
        var getResponse = await _client.GetAsync($"/api/locations/{_fixture.TestLocationId}/printers/{_fixture.TestPrinterId}");
        var existing = await getResponse.Content.ReadFromJsonAsync<PrinterDto>();
        existing!.IsDefault.Should().BeTrue();

        // Create new default
        var request = new CreatePrinterRequest(
            Name: $"New Default-{Guid.NewGuid():N}",
            PrinterType: "receipt",
            ConnectionType: "network",
            IsDefault: true);

        var response = await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/printers",
            request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var newPrinter = await response.Content.ReadFromJsonAsync<PrinterDto>();
        newPrinter!.IsDefault.Should().BeTrue();

        // Check old default is unset
        var checkResponse = await _client.GetAsync($"/api/locations/{_fixture.TestLocationId}/printers/{_fixture.TestPrinterId}");
        var oldDefault = await checkResponse.Content.ReadFromJsonAsync<PrinterDto>();
        oldDefault!.IsDefault.Should().BeFalse();
    }

    [Fact]
    public async Task Update_UpdatesPrinter()
    {
        // Create a printer to update
        var createRequest = new CreatePrinterRequest(
            Name: $"Update-Test-{Guid.NewGuid():N}",
            PrinterType: "receipt",
            ConnectionType: "network",
            IpAddress: "192.168.1.200");

        var createResponse = await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/printers",
            createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<PrinterDto>();

        // Update it
        var updateRequest = new UpdatePrinterRequest(
            Name: "Updated Printer",
            IpAddress: "192.168.1.201",
            Port: 9101);

        var response = await _client.PutAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/printers/{created!.Id}",
            updateRequest);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var updated = await response.Content.ReadFromJsonAsync<PrinterDto>();
        updated!.Name.Should().Be("Updated Printer");
        updated.IpAddress.Should().Be("192.168.1.201");
        updated.Port.Should().Be(9101);
    }

    [Fact]
    public async Task Delete_DeactivatesPrinterWithReferences()
    {
        // The test printer has a cash drawer referencing it
        var response = await _client.DeleteAsync(
            $"/api/locations/{_fixture.TestLocationId}/printers/{_fixture.TestPrinterId}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify it's deactivated, not deleted
        var getResponse = await _client.GetAsync(
            $"/api/locations/{_fixture.TestLocationId}/printers/{_fixture.TestPrinterId}");

        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var printer = await getResponse.Content.ReadFromJsonAsync<PrinterDto>();
        printer!.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task Delete_DeletesUnusedPrinter()
    {
        // Create a printer with no references
        var createRequest = new CreatePrinterRequest(
            Name: $"ToDelete-{Guid.NewGuid():N}",
            PrinterType: "label",
            ConnectionType: "usb");

        var createResponse = await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/printers",
            createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<PrinterDto>();

        // Delete it
        var response = await _client.DeleteAsync(
            $"/api/locations/{_fixture.TestLocationId}/printers/{created!.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify it's actually deleted
        var getResponse = await _client.GetAsync(
            $"/api/locations/{_fixture.TestLocationId}/printers/{created.Id}");

        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
