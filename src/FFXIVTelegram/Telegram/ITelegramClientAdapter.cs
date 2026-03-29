namespace FFXIVTelegram.Telegram;

public interface ITelegramClientAdapter
{
    Task<IReadOnlyList<TelegramUpdate>> GetUpdatesAsync(long offset, CancellationToken cancellationToken);

    Task<TelegramSendResult> SendTextAsync(long chatId, string text, CancellationToken cancellationToken);
}
