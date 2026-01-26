using System.Net;
using System.Net.Http.Json;
using DarkVelocity.Hardware.Api.Dtos;
using DarkVelocity.Shared.Contracts.Hal;
using FluentAssertions;

namespace DarkVelocity.Hardware.Tests;

public class PosDevicesControllerTests : IClassFixture<HardwareApiFixture>
{
    private readonly HardwareApiFixture _fixture;
    private readonly HttpClient _client;

    public PosDevicesControllerTests(HardwareApiFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.Client;
    }

    [Fact]
    public async Task GetAll_ReturnsDevices()
    {
        var response = await _client.GetAsync($"/api/locations/{_fixture.TestLocationId}/devices");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var collection = await response.Content.ReadFromJsonAsync<HalCollection<PosDeviceDto>>();
        collection.Should().NotBeNull();
        collection!.Embedded.Items.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetAll_FiltersByActive()
    {
        var response = await _client.GetAsync($"/api/locations/{_fixture.TestLocationId}/devices?activeOnly=true");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var collection = await response.Content.ReadFromJsonAsync<HalCollection<PosDeviceDto>>();
        collection.Should().NotBeNull();
        collection!.Embedded.Items.Should().OnlyContain(d => d.IsActive);
    }

    [Fact]
    public async Task GetAll_FiltersByOnline()
    {
        var response = await _client.GetAsync($"/api/locations/{_fixture.TestLocationId}/devices?onlineOnly=true");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var collection = await response.Content.ReadFromJsonAsync<HalCollection<PosDeviceDto>>();
        collection.Should().NotBeNull();
        collection!.Embedded.Items.Should().OnlyContain(d => d.IsOnline);
    }

    [Fact]
    public async Task GetById_ReturnsDevice()
    {
        var response = await _client.GetAsync($"/api/locations/{_fixture.TestLocationId}/devices/{_fixture.TestDeviceId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var device = await response.Content.ReadFromJsonAsync<PosDeviceDto>();
        device.Should().NotBeNull();
        device!.Id.Should().Be(_fixture.TestDeviceId);
        device.Name.Should().StartWith("Register 1"); // May be updated by other tests
        device.DeviceId.Should().Be("device-abc-123");
    }

    [Fact]
    public async Task GetById_NotFound_ReturnsNotFound()
    {
        var response = await _client.GetAsync($"/api/locations/{_fixture.TestLocationId}/devices/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetByDeviceId_ReturnsDevice()
    {
        var response = await _client.GetAsync($"/api/locations/{_fixture.TestLocationId}/devices/by-device-id/device-abc-123");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var device = await response.Content.ReadFromJsonAsync<PosDeviceDto>();
        device.Should().NotBeNull();
        device!.DeviceId.Should().Be("device-abc-123");
    }

    [Fact]
    public async Task Register_RegistersNewDevice()
    {
        var deviceId = $"new-device-{Guid.NewGuid():N}";
        var request = new RegisterPosDeviceRequest(
            Name: "New Register",
            DeviceId: deviceId,
            DeviceType: "terminal",
            Model: "Sunmi T2",
            OsVersion: "Android 11",
            AppVersion: "1.0.0");

        var response = await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/devices/register",
            request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var device = await response.Content.ReadFromJsonAsync<PosDeviceDto>();
        device.Should().NotBeNull();
        device!.Name.Should().Be("New Register");
        device.DeviceId.Should().Be(deviceId);
        device.DeviceType.Should().Be("terminal");
        device.IsOnline.Should().BeTrue();
        device.RegisteredAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Register_ExistingDevice_UpdatesAndReturnsOk()
    {
        // Register same device again with updates
        var request = new RegisterPosDeviceRequest(
            Name: "Register 1 Updated",
            DeviceId: "device-abc-123",  // Existing device
            AppVersion: "2.0.0");

        var response = await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/devices/register",
            request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var device = await response.Content.ReadFromJsonAsync<PosDeviceDto>();
        device!.Name.Should().Be("Register 1 Updated");
        device.AppVersion.Should().Be("2.0.0");
    }

    [Fact]
    public async Task Register_DuplicateName_ReturnsConflict()
    {
        var request = new RegisterPosDeviceRequest(
            Name: "Register 1",  // Already exists
            DeviceId: $"different-device-{Guid.NewGuid():N}");

        var response = await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/devices/register",
            request);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Update_UpdatesDevice()
    {
        // Create a device to update
        var deviceId = $"update-test-{Guid.NewGuid():N}";
        var registerRequest = new RegisterPosDeviceRequest(
            Name: $"Update-Test-{Guid.NewGuid():N}",
            DeviceId: deviceId);

        var registerResponse = await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/devices/register",
            registerRequest);
        var registered = await registerResponse.Content.ReadFromJsonAsync<PosDeviceDto>();

        // Update it
        var updateRequest = new UpdatePosDeviceRequest(
            Name: "Updated Device",
            AppVersion: "3.0.0",
            AutoPrintReceipts: false,
            OpenDrawerOnCash: false);

        var response = await _client.PutAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/devices/{registered!.Id}",
            updateRequest);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var updated = await response.Content.ReadFromJsonAsync<PosDeviceDto>();
        updated!.Name.Should().Be("Updated Device");
        updated.AppVersion.Should().Be("3.0.0");
        updated.AutoPrintReceipts.Should().BeFalse();
        updated.OpenDrawerOnCash.Should().BeFalse();
    }

    [Fact]
    public async Task Update_AssignPrinterAndDrawer()
    {
        // Create a device
        var deviceId = $"assign-test-{Guid.NewGuid():N}";
        var registerRequest = new RegisterPosDeviceRequest(
            Name: $"Assign-Test-{Guid.NewGuid():N}",
            DeviceId: deviceId);

        var registerResponse = await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/devices/register",
            registerRequest);
        var registered = await registerResponse.Content.ReadFromJsonAsync<PosDeviceDto>();

        // Assign printer and drawer
        var updateRequest = new UpdatePosDeviceRequest(
            DefaultPrinterId: _fixture.TestPrinterId,
            DefaultCashDrawerId: _fixture.TestCashDrawerId);

        var response = await _client.PutAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/devices/{registered!.Id}",
            updateRequest);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var updated = await response.Content.ReadFromJsonAsync<PosDeviceDto>();
        updated!.DefaultPrinterId.Should().Be(_fixture.TestPrinterId);
        updated.DefaultCashDrawerId.Should().Be(_fixture.TestCashDrawerId);
        updated.DefaultPrinterName.Should().NotBeNullOrEmpty();
        updated.DefaultCashDrawerName.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Update_InvalidPrinter_ReturnsBadRequest()
    {
        var updateRequest = new UpdatePosDeviceRequest(
            DefaultPrinterId: Guid.NewGuid());  // Non-existent

        var response = await _client.PutAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/devices/{_fixture.TestDeviceId}",
            updateRequest);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Heartbeat_UpdatesLastSeen()
    {
        var request = new HeartbeatRequest(
            AppVersion: "1.1.0",
            OsVersion: "iOS 18");

        var response = await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/devices/{_fixture.TestDeviceId}/heartbeat",
            request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var heartbeat = await response.Content.ReadFromJsonAsync<HeartbeatResponse>();
        heartbeat.Should().NotBeNull();
        heartbeat!.Success.Should().BeTrue();
        heartbeat.Device.Should().NotBeNull();
        heartbeat.Device!.AppVersion.Should().Be("1.1.0");
        heartbeat.Device.OsVersion.Should().Be("iOS 18");
        heartbeat.Device.IsOnline.Should().BeTrue();
        heartbeat.Device.LastSeenAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task Heartbeat_NotFound_ReturnsNotFound()
    {
        var request = new HeartbeatRequest();

        var response = await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/devices/{Guid.NewGuid()}/heartbeat",
            request);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task MarkOffline_MarksDeviceOffline()
    {
        // Create an online device
        var deviceId = $"offline-test-{Guid.NewGuid():N}";
        var registerRequest = new RegisterPosDeviceRequest(
            Name: $"Offline-Test-{Guid.NewGuid():N}",
            DeviceId: deviceId);

        var registerResponse = await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/devices/register",
            registerRequest);
        var registered = await registerResponse.Content.ReadFromJsonAsync<PosDeviceDto>();
        registered!.IsOnline.Should().BeTrue();

        // Mark offline
        var response = await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/devices/{registered.Id}/offline",
            new { });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify
        var getResponse = await _client.GetAsync(
            $"/api/locations/{_fixture.TestLocationId}/devices/{registered.Id}");
        var device = await getResponse.Content.ReadFromJsonAsync<PosDeviceDto>();
        device!.IsOnline.Should().BeFalse();
    }

    [Fact]
    public async Task Delete_DeletesDevice()
    {
        // Create a device to delete
        var deviceId = $"delete-test-{Guid.NewGuid():N}";
        var registerRequest = new RegisterPosDeviceRequest(
            Name: $"Delete-Test-{Guid.NewGuid():N}",
            DeviceId: deviceId);

        var registerResponse = await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/devices/register",
            registerRequest);
        var registered = await registerResponse.Content.ReadFromJsonAsync<PosDeviceDto>();

        // Delete it
        var response = await _client.DeleteAsync(
            $"/api/locations/{_fixture.TestLocationId}/devices/{registered!.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify it's deleted
        var getResponse = await _client.GetAsync(
            $"/api/locations/{_fixture.TestLocationId}/devices/{registered.Id}");

        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
