using Microsoft.AspNetCore.SignalR;

namespace AnimeBackend.Api.Hubs;

// Event contract pushed to connected clients. Nothing fires yet: the scheduled
// source sync will emit LibraryUpdated when it spots new episodes, and the tag
// trigger engine will emit TriggerFired. The transport is in place so the
// frontend can subscribe before those land.
public interface INotificationsClient
{
    Task LibraryUpdated(string mediaId);

    Task TriggerFired(string ruleId, string mediaId);
}

public sealed class NotificationsHub : Hub<INotificationsClient>;
