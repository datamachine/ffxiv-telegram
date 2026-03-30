namespace FFXIVTelegram.Telegram;

public sealed record TelegramUpdate(
    long UpdateId,
    long MessageId,
    long? ReplyToMessageId,
    long ChatId,
    bool IsPrivateChat,
    string? Text,
    long? FromUserId = null,
    bool IsFromBot = false);
