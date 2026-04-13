using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Authorization;

namespace SMRT_QC_Web.Hubs;

/// <summary>
/// SignalR Hub for broadcasting real-time notifications to connected clients.
/// Clients subscribe to role-specific groups so notifications are targeted.
/// Requires authentication — unauthenticated connections are rejected.
/// </summary>
[Authorize]
public class NotificationHub : Hub
{
    /// <summary>
    /// Called when a client connects. Adds the connection to the group
    /// matching the user's role/position so role-specific broadcasts work.
    /// </summary>
    public override async Task OnConnectedAsync()
    {
        // Read the user's role from the authentication claim
        var position = Context.User?.FindFirst("Position")?.Value
                    ?? (Context.User?.IsInRole("ADMIN") == true ? "ADMIN"
                    : Context.User?.IsInRole("MANAGER") == true ? "MANAGER"
                    : Context.User?.IsInRole("SUPERVISOR") == true ? "SUPERVISOR"
                    : "STAFF");

        // Join the group for this role so SendToRole works
        await Groups.AddToGroupAsync(Context.ConnectionId, position);

        // Also join an "ALL" group for broadcast-to-all messages
        await Groups.AddToGroupAsync(Context.ConnectionId, "ALL");

        await base.OnConnectedAsync();
    }

    /// <summary>
    /// Called when a client disconnects. Group membership is automatically
    /// cleaned up by SignalR; no manual removal needed.
    /// </summary>
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Server-callable method to send a notification to a specific role group.
    /// Controllers inject IHubContext&lt;NotificationHub&gt; and call this.
    /// </summary>
    /// <param name="position">Target role: ADMIN, MANAGER, SUPERVISOR, STAFF, or ALL.</param>
    /// <param name="message">Notification message text.</param>
    /// <param name="type">Notification type for client-side styling: info, warning, success, danger.</param>
    public static async Task SendToRole(
        IHubContext<NotificationHub> hub,
        string position,
        string message,
        string type = "info")
    {
        await hub.Clients.Group(position)
            .SendAsync("ReceiveNotification", new
            {
                message,
                type,
                time = DateTime.Now.ToString("HH:mm"),
                timestamp = DateTimeOffset.Now.ToUnixTimeMilliseconds()
            });
    }

    /// <summary>
    /// Client-invokable method: marks a notification as acknowledged on the client side.
    /// The actual DB update is done via HTTP/controller action.
    /// </summary>
    /// <param name="notificationId">The notification DB id to acknowledge.</param>
    public async Task AcknowledgeNotification(int notificationId)
    {
        // Relay the ack back to the same connection so the UI can update
        await Clients.Caller.SendAsync("NotificationAcknowledged", notificationId);
    }
}
