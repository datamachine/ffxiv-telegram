namespace FFXIVTelegram.Notifications;

using FFXIVTelegram.Chat;
using FFXIVTelegram.Telegram;

public sealed class NotificationDispatcher : INotificationDispatcher
{
    private readonly TelegramBridgeService bridge;
    private readonly TelegramReplyMap replyMap;

    public NotificationDispatcher(TelegramBridgeService bridge, TelegramReplyMap replyMap)
    {
        this.bridge = bridge ?? throw new ArgumentNullException(nameof(bridge));
        this.replyMap = replyMap ?? throw new ArgumentNullException(nameof(replyMap));
    }

    public async Task SendAsync(string text, ChatRoute? replyRoute, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(text);

        var sendResult = await this.bridge.SendToAuthorizedChatAsync(text, cancellationToken).ConfigureAwait(false);

        if (sendResult.Success && sendResult.MessageId is long messageId && replyRoute is not null)
        {
            this.replyMap.Store(messageId, replyRoute);
        }
    }
}
