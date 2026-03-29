namespace FFXIVTelegram.Telegram;

public sealed record TelegramSendResult(bool Success, long? MessageId = null, string? ErrorMessage = null)
{
    public static TelegramSendResult Ok(long messageId)
    {
        return new TelegramSendResult(true, messageId);
    }

    public static TelegramSendResult Failure(string errorMessage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(errorMessage);
        return new TelegramSendResult(false, null, errorMessage);
    }
}
