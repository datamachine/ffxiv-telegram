namespace FFXIVTelegram.Telegram;

public sealed record TelegramSendResult(bool Success, string? ErrorMessage = null)
{
    public static TelegramSendResult Ok()
    {
        return new TelegramSendResult(true);
    }

    public static TelegramSendResult Failure(string errorMessage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(errorMessage);
        return new TelegramSendResult(false, errorMessage);
    }
}
