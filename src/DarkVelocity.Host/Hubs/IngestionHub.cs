using Microsoft.AspNetCore.SignalR;

namespace DarkVelocity.Host.Hubs;

/// <summary>
/// SignalR hub for real-time invoice ingestion updates.
/// Back office clients connect to receive notifications about new pending items,
/// approvals, rejections, and poll completions.
/// </summary>
public class IngestionHub : Hub
{
    /// <summary>
    /// Join a site's ingestion channel to receive real-time updates.
    /// </summary>
    public async Task JoinSite(Guid orgId, Guid siteId)
    {
        var groupName = GetGroupName(orgId, siteId);
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
    }

    /// <summary>
    /// Leave a site's ingestion channel.
    /// </summary>
    public async Task LeaveSite(Guid orgId, Guid siteId)
    {
        var groupName = GetGroupName(orgId, siteId);
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
    }

    internal static string GetGroupName(Guid orgId, Guid siteId)
        => $"ingestion:{orgId}:{siteId}";
}
