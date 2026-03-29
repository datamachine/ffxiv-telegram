namespace FFXIVTelegram.Telegram;

public sealed record TelegramUpdate(
    long UpdateId,
    long ChatId,
    bool IsPrivateChat,
    string? Text);
