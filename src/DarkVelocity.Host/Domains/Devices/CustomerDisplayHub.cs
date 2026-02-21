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

        // Notify the register that a display connected
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
    /// Messages include order updates, tip requests, payment status, etc.
    /// </summary>
    public async Task SendToDisplay(string deviceId, object message)
    {
        var groupName = GetDisplayGroup(deviceId);
        await Clients.OthersInGroup(groupName).SendAsync("DisplayMessage", message);
    }

    /// <summary>
    /// Display sends a response back to the register.
    /// Messages include tip selection, payment method choice, receipt type.
    /// </summary>
    public async Task SendToRegister(string deviceId, object message)
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
/// Service for pushing customer display events from grains or endpoint handlers.
/// </summary>
public class CustomerDisplayNotifier
{
    private readonly IHubContext<CustomerDisplayHub> _hubContext;

    public CustomerDisplayNotifier(IHubContext<CustomerDisplayHub> hubContext)
    {
        _hubContext = hubContext;
    }

    /// <summary>
    /// Send an order update to the customer display for a specific device.
    /// </summary>
    public async Task SendOrderUpdate(string deviceId, object orderData)
    {
        var group = CustomerDisplayHub.GetDisplayGroup(deviceId);
        await _hubContext.Clients.Group(group).SendAsync("DisplayMessage", new
        {
            type = "ORDER_UPDATED",
            order = orderData,
            timestamp = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Request tip selection on the customer display.
    /// </summary>
    public async Task RequestTip(string deviceId, object orderData)
    {
        var group = CustomerDisplayHub.GetDisplayGroup(deviceId);
        await _hubContext.Clients.Group(group).SendAsync("DisplayMessage", new
        {
            type = "REQUEST_TIP",
            order = orderData,
            timestamp = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Send payment complete notification to the customer display.
    /// </summary>
    public async Task SendPaymentComplete(string deviceId, decimal totalPaid, decimal changeAmount)
    {
        var group = CustomerDisplayHub.GetDisplayGroup(deviceId);
        await _hubContext.Clients.Group(group).SendAsync("DisplayMessage", new
        {
            type = "PAYMENT_COMPLETE",
            totalPaid,
            changeAmount,
            timestamp = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Reset the display to idle state.
    /// </summary>
    public async Task SendIdle(string deviceId)
    {
        var group = CustomerDisplayHub.GetDisplayGroup(deviceId);
        await _hubContext.Clients.Group(group).SendAsync("DisplayMessage", new
        {
            type = "IDLE",
            timestamp = DateTime.UtcNow
        });
    }
}
