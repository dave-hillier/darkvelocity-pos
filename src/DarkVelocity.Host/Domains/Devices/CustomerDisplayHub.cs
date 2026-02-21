using DarkVelocity.Host.Grains;
using Microsoft.AspNetCore.SignalR;

namespace DarkVelocity.Host.Hubs;

/// <summary>
/// SignalR hub for customer-facing display communication.
/// Enables real-time message relay between POS register and customer display,
/// supporting cross-device setups where the display is on a separate tablet.
/// For same-device setups, the frontend uses BroadcastChannel instead.
/// </summary>
public class CustomerDisplayHub : Hub
{
    /// <summary>
    /// Display joins a device group to receive messages from the register.
    /// </summary>
    public async Task JoinDisplay(string deviceId)
    {
        var groupName = GetDisplayGroup(deviceId);
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);

        await Clients.OthersInGroup(groupName).SendAsync("DisplayConnected", new
        {
            connectionId = Context.ConnectionId,
            timestamp = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Display leaves a device group.
    /// </summary>
    public async Task LeaveDisplay(string deviceId)
    {
        var groupName = GetDisplayGroup(deviceId);
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);

        await Clients.OthersInGroup(groupName).SendAsync("DisplayDisconnected", new
        {
            connectionId = Context.ConnectionId,
            timestamp = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Register sends a command to the customer display.
    /// </summary>
    public async Task SendToDisplay(string deviceId, DisplayPayload message)
    {
        var groupName = GetDisplayGroup(deviceId);
        await Clients.OthersInGroup(groupName).SendAsync("DisplayMessage", message);
    }

    /// <summary>
    /// Display sends a response back to the register.
    /// </summary>
    public async Task SendToRegister(string deviceId, DisplayPayload message)
    {
        var groupName = GetDisplayGroup(deviceId);
        await Clients.OthersInGroup(groupName).SendAsync("RegisterMessage", message);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await base.OnDisconnectedAsync(exception);
    }

    internal static string GetDisplayGroup(string deviceId) => $"display:{deviceId}";
}

/// <summary>
/// Strongly-typed payload for messages exchanged through the display hub.
/// </summary>
[GenerateSerializer]
public record DisplayPayload(
    [property: Id(0)] string Type,
    [property: Id(1)] object? Order = null,
    [property: Id(2)] decimal? TipAmount = null,
    [property: Id(3)] int? TipPercent = null,
    [property: Id(4)] string? PaymentMethod = null,
    [property: Id(5)] string? ReceiptType = null,
    [property: Id(6)] decimal? TotalPaid = null,
    [property: Id(7)] decimal? ChangeAmount = null);

/// <summary>
/// Service for pushing customer display events from grains or endpoint handlers.
/// Uses the grain to update display mode, then pushes the message via SignalR.
/// </summary>
public class CustomerDisplayNotifier
{
    private readonly IHubContext<CustomerDisplayHub> _hubContext;
    private readonly IGrainFactory _grainFactory;

    public CustomerDisplayNotifier(IHubContext<CustomerDisplayHub> hubContext, IGrainFactory grainFactory)
    {
        _hubContext = hubContext;
        _grainFactory = grainFactory;
    }

    /// <summary>
    /// Send an order update to the customer display for a specific POS device.
    /// Looks up the paired display from the POS device grain.
    /// </summary>
    public async Task SendOrderUpdate(Guid orgId, Guid posDeviceId, object orderData)
    {
        var displayId = await GetPairedDisplayId(orgId, posDeviceId);
        if (displayId == null) return;

        await SetDisplayMode(orgId, displayId.Value, CustomerDisplayMode.Order);
        await SendToDisplay(posDeviceId.ToString(), new DisplayPayload("ORDER_UPDATED", Order: orderData));
    }

    /// <summary>
    /// Request tip selection on the customer display.
    /// </summary>
    public async Task RequestTip(Guid orgId, Guid posDeviceId, object orderData)
    {
        var displayId = await GetPairedDisplayId(orgId, posDeviceId);
        if (displayId == null) return;

        await SetDisplayMode(orgId, displayId.Value, CustomerDisplayMode.Tip);
        await SendToDisplay(posDeviceId.ToString(), new DisplayPayload("REQUEST_TIP", Order: orderData));
    }

    /// <summary>
    /// Send payment complete notification to the customer display.
    /// </summary>
    public async Task SendPaymentComplete(Guid orgId, Guid posDeviceId, decimal totalPaid, decimal changeAmount)
    {
        var displayId = await GetPairedDisplayId(orgId, posDeviceId);
        if (displayId == null) return;

        await SetDisplayMode(orgId, displayId.Value, CustomerDisplayMode.ThankYou);
        await SendToDisplay(posDeviceId.ToString(), new DisplayPayload("PAYMENT_COMPLETE", TotalPaid: totalPaid, ChangeAmount: changeAmount));
    }

    /// <summary>
    /// Reset the display to idle state.
    /// </summary>
    public async Task SendIdle(Guid orgId, Guid posDeviceId)
    {
        var displayId = await GetPairedDisplayId(orgId, posDeviceId);
        if (displayId == null) return;

        await SetDisplayMode(orgId, displayId.Value, CustomerDisplayMode.Idle);
        await SendToDisplay(posDeviceId.ToString(), new DisplayPayload("IDLE"));
    }

    private async Task<Guid?> GetPairedDisplayId(Guid orgId, Guid posDeviceId)
    {
        try
        {
            var posGrain = _grainFactory.GetGrain<IPosDeviceGrain>(
                $"{orgId}:posdevice:{posDeviceId}");
            var snapshot = await posGrain.GetSnapshotAsync();
            return snapshot.DefaultCustomerDisplayId;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    private async Task SetDisplayMode(Guid orgId, Guid displayId, CustomerDisplayMode mode)
    {
        try
        {
            var displayGrain = _grainFactory.GetGrain<ICustomerDisplayGrain>(
                GrainKeys.CustomerDisplay(orgId, displayId));
            await displayGrain.SetModeAsync(mode);
        }
        catch (InvalidOperationException)
        {
            // Display grain not initialized — ignore
        }
    }

    private async Task SendToDisplay(string deviceId, DisplayPayload payload)
    {
        var group = CustomerDisplayHub.GetDisplayGroup(deviceId);
        await _hubContext.Clients.Group(group).SendAsync("DisplayMessage", payload);
    }
}
