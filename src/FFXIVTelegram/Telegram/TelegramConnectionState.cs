namespace FFXIVTelegram.Telegram;

public enum TelegramConnectionState
{
    NotConfigured,
    /// <summary>
    /// The bot token is configured and polling is active, but no private chat has claimed authorization yet.
    /// </summary>
    WaitingForStart,
    Connected,
    Error,
}
