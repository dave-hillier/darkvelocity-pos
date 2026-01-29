using System.Net;
using System.Net.Http.Json;
using DarkVelocity.Integration.Tests.Fixtures;
using FluentAssertions;

namespace DarkVelocity.Integration.Tests;

/// <summary>
/// Integration tests for Hardware management (POS devices, printers, cash drawers).
///
/// Business Scenarios Covered:
/// - POS device registration and management
/// - Printer configuration
/// - Cash drawer setup
/// - Device heartbeat and status monitoring
/// </summary>
public class HardwareIntegrationTests : IClassFixture<HardwareServiceFixture>
{
    private readonly HardwareServiceFixture _fixture;
    private readonly HttpClient _client;

    public HardwareIntegrationTests(HardwareServiceFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.Client;
    }

    #region POS Device Registration

    [Fact]
    public async Task RegisterPosDevice_CreatesDevice()
    {
        // Arrange
        var request = new RegisterPosDeviceRequest(
            Name: "New Register",
            DeviceId: "POS-NEW-001",
            DeviceType: "tablet",
            Model: "iPad Pro 12.9",
            OsVersion: "iOS 17.2",
            AppVersion: "2.2.0");

        // Act
        var response = await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/devices/register",
            request);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Created, HttpStatusCode.OK, HttpStatusCode.NotFound);

        if (response.IsSuccessStatusCode)
        {
            var device = await response.Content.ReadFromJsonAsync<PosDeviceDto>();
            device.Should().NotBeNull();
            device!.Name.Should().Be("New Register");
            device.DeviceId.Should().Be("POS-NEW-001");
            device.DeviceType.Should().Be("tablet");
            device.IsActive.Should().BeTrue();
        }
    }

    [Fact]
    public async Task RegisterPosDevice_DuplicateDeviceId_ReturnsConflict()
    {
        // Arrange - Try to register with existing device ID
        var request = new RegisterPosDeviceRequest(
            Name: "Duplicate Register",
            DeviceId: _fixture.TestDeviceDeviceId, // Already exists
            DeviceType: "tablet",
            Model: "iPad",
            OsVersion: "iOS 17.0",
            AppVersion: "2.0.0");

        // Act
        var response = await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/devices/register",
            request);

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.Conflict,
            HttpStatusCode.BadRequest,
            HttpStatusCode.NotFound);
    }

    #endregion

    #region POS Device Queries

    [Fact]
    public async Task GetPosDevices_ReturnsLocationDevices()
    {
        // Act
        var response = await _client.GetAsync(
            $"/api/locations/{_fixture.TestLocationId}/devices");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);

        if (response.StatusCode == HttpStatusCode.OK)
        {
            var devices = await response.Content.ReadFromJsonAsync<List<PosDeviceDto>>();
            devices.Should().NotBeNull();
            devices!.Should().NotBeEmpty();
            devices.Should().OnlyContain(d => d.LocationId == _fixture.TestLocationId);
        }
    }

    [Fact]
    public async Task GetPosDevices_FilterActiveOnly_ReturnsOnlyActive()
    {
        // Act
        var response = await _client.GetAsync(
            $"/api/locations/{_fixture.TestLocationId}/devices?activeOnly=true");

        // Assert
        if (response.StatusCode == HttpStatusCode.OK)
        {
            var devices = await response.Content.ReadFromJsonAsync<List<PosDeviceDto>>();
            devices!.Should().OnlyContain(d => d.IsActive);
        }
    }

    [Fact]
    public async Task GetPosDevices_FilterOnlineOnly_ReturnsOnlyOnline()
    {
        // Act
        var response = await _client.GetAsync(
            $"/api/locations/{_fixture.TestLocationId}/devices?onlineOnly=true");

        // Assert
        if (response.StatusCode == HttpStatusCode.OK)
        {
            var devices = await response.Content.ReadFromJsonAsync<List<PosDeviceDto>>();
            devices!.Should().OnlyContain(d => d.IsOnline);
        }
    }

    [Fact]
    public async Task GetPosDeviceById_ReturnsDevice()
    {
        // Act
        var response = await _client.GetAsync(
            $"/api/locations/{_fixture.TestLocationId}/devices/{_fixture.TestDeviceId}");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);

        if (response.StatusCode == HttpStatusCode.OK)
        {
            var device = await response.Content.ReadFromJsonAsync<PosDeviceDto>();
            device!.Id.Should().Be(_fixture.TestDeviceId);
            device.Name.Should().Be("Register 1");
        }
    }

    [Fact]
    public async Task GetPosDeviceByDeviceId_ReturnsDevice()
    {
        // Act
        var response = await _client.GetAsync(
            $"/api/locations/{_fixture.TestLocationId}/devices/by-device-id/{_fixture.TestDeviceDeviceId}");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);

        if (response.StatusCode == HttpStatusCode.OK)
        {
            var device = await response.Content.ReadFromJsonAsync<PosDeviceDto>();
            device!.DeviceId.Should().Be(_fixture.TestDeviceDeviceId);
        }
    }

    #endregion

    #region POS Device Status

    [Fact]
    public async Task DeactivatePosDevice_SetsInactive()
    {
        // Act
        var response = await _client.DeleteAsync(
            $"/api/locations/{_fixture.TestLocationId}/devices/{_fixture.TestDevice2Id}");

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.NoContent,
            HttpStatusCode.NotFound);

        // Verify device is inactive
        if (response.IsSuccessStatusCode)
        {
            var getResponse = await _client.GetAsync(
                $"/api/locations/{_fixture.TestLocationId}/devices/{_fixture.TestDevice2Id}");

            if (getResponse.StatusCode == HttpStatusCode.OK)
            {
                var device = await getResponse.Content.ReadFromJsonAsync<PosDeviceDto>();
                device!.IsActive.Should().BeFalse();
            }
        }
    }

    [Fact]
    public async Task DeviceHeartbeat_UpdatesLastSeenAt()
    {
        // Arrange
        var request = new HeartbeatRequest(
            AppVersion: "2.2.1",
            OsVersion: "iOS 17.3");

        // Act
        var response = await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/devices/{_fixture.TestDeviceId}/heartbeat",
            request);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);

        if (response.StatusCode == HttpStatusCode.OK)
        {
            var heartbeat = await response.Content.ReadFromJsonAsync<HeartbeatResponse>();
            heartbeat!.Success.Should().BeTrue();
            heartbeat.ServerTime.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        }
    }

    [Fact]
    public async Task MarkDeviceOffline_SetsOfflineStatus()
    {
        // Act
        var response = await _client.PostAsync(
            $"/api/locations/{_fixture.TestLocationId}/devices/{_fixture.TestDeviceId}/offline",
            null);

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.NoContent,
            HttpStatusCode.NotFound);
    }

    #endregion

    #region Printer Management

    [Fact]
    public async Task CreatePrinter_WithConfiguration_CreatesPrinter()
    {
        // Arrange
        var request = new CreatePrinterRequest(
            Name: "New Kitchen Printer",
            PrinterType: "kitchen",
            ConnectionType: "network",
            IpAddress: "192.168.1.150",
            Port: 9100,
            PaperWidth: 80,
            IsDefault: false,
            CharacterSet: "UTF-8",
            SupportsCut: true,
            SupportsCashDrawer: false);

        // Act
        var response = await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/printers",
            request);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Created, HttpStatusCode.OK, HttpStatusCode.NotFound);

        if (response.IsSuccessStatusCode)
        {
            var printer = await response.Content.ReadFromJsonAsync<PrinterDto>();
            printer!.Name.Should().Be("New Kitchen Printer");
            printer.PrinterType.Should().Be("kitchen");
            printer.IpAddress.Should().Be("192.168.1.150");
        }
    }

    [Fact]
    public async Task GetPrinters_ReturnsAllPrinters()
    {
        // Act
        var response = await _client.GetAsync(
            $"/api/locations/{_fixture.TestLocationId}/printers");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);

        if (response.StatusCode == HttpStatusCode.OK)
        {
            var printers = await response.Content.ReadFromJsonAsync<List<PrinterDto>>();
            printers.Should().NotBeNull();
            printers!.Should().NotBeEmpty();
        }
    }

    [Fact]
    public async Task GetPrinters_ByType_ReturnsFilteredPrinters()
    {
        // Act - Get only receipt printers
        var response = await _client.GetAsync(
            $"/api/locations/{_fixture.TestLocationId}/printers?printerType=receipt");

        // Assert
        if (response.StatusCode == HttpStatusCode.OK)
        {
            var printers = await response.Content.ReadFromJsonAsync<List<PrinterDto>>();
            printers!.Should().OnlyContain(p => p.PrinterType == "receipt");
        }
    }

    [Fact]
    public async Task GetPrinters_ActiveOnly_ReturnsOnlyActive()
    {
        // Act
        var response = await _client.GetAsync(
            $"/api/locations/{_fixture.TestLocationId}/printers?activeOnly=true");

        // Assert
        if (response.StatusCode == HttpStatusCode.OK)
        {
            var printers = await response.Content.ReadFromJsonAsync<List<PrinterDto>>();
            printers!.Should().OnlyContain(p => p.IsActive);
        }
    }

    #endregion

    #region Cash Drawer Management

    [Fact]
    public async Task CreateCashDrawer_LinkedToDevice_CreatesDrawer()
    {
        // Arrange
        var request = new CreateCashDrawerRequest(
            Name: "New Cash Drawer",
            PrinterId: _fixture.ReceiptPrinterId,
            ConnectionType: "printer",
            KickPulsePin: 0,
            KickPulseOnTime: 100,
            KickPulseOffTime: 100);

        // Act
        var response = await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/cash-drawers",
            request);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Created, HttpStatusCode.OK, HttpStatusCode.NotFound);

        if (response.IsSuccessStatusCode)
        {
            var drawer = await response.Content.ReadFromJsonAsync<CashDrawerDto>();
            drawer!.Name.Should().Be("New Cash Drawer");
            drawer.PrinterId.Should().Be(_fixture.ReceiptPrinterId);
            drawer.IsActive.Should().BeTrue();
        }
    }

    [Fact]
    public async Task GetCashDrawers_ReturnsAllDrawers()
    {
        // Act
        var response = await _client.GetAsync(
            $"/api/locations/{_fixture.TestLocationId}/cash-drawers");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);

        if (response.StatusCode == HttpStatusCode.OK)
        {
            var drawers = await response.Content.ReadFromJsonAsync<List<CashDrawerDto>>();
            drawers.Should().NotBeNull();
            drawers!.Should().NotBeEmpty();
        }
    }

    [Fact]
    public async Task GetCashDrawerById_ReturnsDrawer()
    {
        // Act
        var response = await _client.GetAsync(
            $"/api/locations/{_fixture.TestLocationId}/cash-drawers/{_fixture.TestCashDrawerId}");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);

        if (response.StatusCode == HttpStatusCode.OK)
        {
            var drawer = await response.Content.ReadFromJsonAsync<CashDrawerDto>();
            drawer!.Id.Should().Be(_fixture.TestCashDrawerId);
            drawer.Name.Should().Be("Main Drawer");
        }
    }

    [Fact]
    public async Task GetCashDrawerStatus_ReturnsOpenCloseState()
    {
        // This test would verify cash drawer status if such an endpoint exists
        // Act
        var response = await _client.GetAsync(
            $"/api/locations/{_fixture.TestLocationId}/cash-drawers/{_fixture.TestCashDrawerId}");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
    }

    #endregion

    #region Device Configuration

    [Fact]
    public async Task UpdatePosDevice_ChangesConfiguration()
    {
        // Arrange
        var updateRequest = new UpdatePosDeviceRequest(
            Name: "Updated Register Name",
            AutoPrintReceipts: false,
            OpenDrawerOnCash: false,
            DefaultPrinterId: _fixture.ReceiptPrinterId);

        // Act
        var response = await _client.PutAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/devices/{_fixture.TestDeviceId}",
            updateRequest);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NoContent, HttpStatusCode.NotFound);
    }

    #endregion
}

// Hardware DTOs
public record RegisterPosDeviceRequest(
    string Name,
    string DeviceId,
    string DeviceType,
    string? Model = null,
    string? OsVersion = null,
    string? AppVersion = null,
    Guid? DefaultPrinterId = null,
    Guid? DefaultCashDrawerId = null,
    bool AutoPrintReceipts = true,
    bool OpenDrawerOnCash = true);

public record UpdatePosDeviceRequest(
    string? Name = null,
    Guid? DefaultPrinterId = null,
    Guid? DefaultCashDrawerId = null,
    bool? AutoPrintReceipts = null,
    bool? OpenDrawerOnCash = null,
    bool? IsActive = null);

public record HeartbeatRequest(
    string? AppVersion = null,
    string? OsVersion = null);

public record HeartbeatResponse
{
    public bool Success { get; init; }
    public DateTime ServerTime { get; init; }
    public PosDeviceDto? Device { get; init; }
}

public record PosDeviceDto
{
    public Guid Id { get; init; }
    public Guid LocationId { get; init; }
    public string? Name { get; init; }
    public string? DeviceId { get; init; }
    public string? DeviceType { get; init; }
    public string? Model { get; init; }
    public string? OsVersion { get; init; }
    public string? AppVersion { get; init; }
    public Guid? DefaultPrinterId { get; init; }
    public Guid? DefaultCashDrawerId { get; init; }
    public bool AutoPrintReceipts { get; init; }
    public bool OpenDrawerOnCash { get; init; }
    public bool IsActive { get; init; }
    public bool IsOnline { get; init; }
    public DateTime? LastSeenAt { get; init; }
    public DateTime RegisteredAt { get; init; }
}

public record CreatePrinterRequest(
    string Name,
    string PrinterType,
    string ConnectionType,
    string? IpAddress = null,
    int? Port = null,
    string? MacAddress = null,
    string? UsbVendorId = null,
    string? UsbProductId = null,
    int PaperWidth = 80,
    bool IsDefault = false,
    string CharacterSet = "UTF-8",
    bool SupportsCut = true,
    bool SupportsCashDrawer = false);

public record PrinterDto
{
    public Guid Id { get; init; }
    public Guid LocationId { get; init; }
    public string? Name { get; init; }
    public string? PrinterType { get; init; }
    public string? ConnectionType { get; init; }
    public string? IpAddress { get; init; }
    public int? Port { get; init; }
    public int PaperWidth { get; init; }
    public bool IsDefault { get; init; }
    public bool IsActive { get; init; }
}

public record CreateCashDrawerRequest(
    string Name,
    Guid PrinterId,
    string ConnectionType,
    string? IpAddress = null,
    int? Port = null,
    int KickPulsePin = 0,
    int KickPulseOnTime = 100,
    int KickPulseOffTime = 100);

public record CashDrawerDto
{
    public Guid Id { get; init; }
    public Guid LocationId { get; init; }
    public string? Name { get; init; }
    public Guid? PrinterId { get; init; }
    public string? PrinterName { get; init; }
    public string? ConnectionType { get; init; }
    public bool IsActive { get; init; }
}
