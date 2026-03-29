namespace FFXIVTelegram.Telegram;

public sealed record TelegramInboundMessage(
    long UpdateId,
    long MessageId,
    long? ReplyToMessageId,
    long ChatId,
    bool IsPrivateChat,
    string Text);
