namespace FFXIVTelegram.Notifications;

using FFXIVTelegram.Chat;

public interface INotificationDispatcher
{
    Task SendAsync(string text, ChatRoute? replyRoute, CancellationToken cancellationToken);
}
