namespace FFXIVTelegram.Telegram;

public enum TelegramInboundResult
{
    Authorized,
    Accepted,
    IgnoredEmptyText,
    IgnoredUnsupportedChatType,
    IgnoredUnauthorizedChat,
}
