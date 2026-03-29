namespace FFXIVTelegram.Chat;

using Dalamud.Game.Text;
using FFXIVTelegram.Telegram;

public sealed class GameChatMonitor : IDisposable
{
    private readonly TelegramReplyMap replyMap;
    private TelegramBridgeService? telegramBridge;

    public GameChatMonitor(TelegramReplyMap replyMap)
    {
        this.replyMap = replyMap ?? throw new ArgumentNullException(nameof(replyMap));
    }

    public TelegramBridgeService? TelegramBridge
    {
        get => this.telegramBridge;
        set => this.telegramBridge = value;
    }

    public async Task<TelegramSendResult?> ForwardAsync(
        XivChatType type,
        string sender,
        string message,
        CancellationToken cancellationToken = default)
    {
        var forwarded = GameChatFormatter.Format(type, sender, message);
        if (forwarded is null)
        {
            return null;
        }

        if (this.telegramBridge is null)
        {
            throw new InvalidOperationException("Telegram bridge is not configured.");
        }

        var sendResult = await this.telegramBridge.SendToAuthorizedChatAsync(forwarded.Text, cancellationToken).ConfigureAwait(false);
        if (sendResult.Success && sendResult.MessageId is long messageId)
        {
            this.replyMap.Store(messageId, forwarded.Route);
        }

        return sendResult;
    }

    public void Dispose()
    {
    }
}
