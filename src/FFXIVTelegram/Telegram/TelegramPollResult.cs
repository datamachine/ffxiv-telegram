namespace FFXIVTelegram.Telegram;

public sealed record TelegramPollResult(
    long NextOffset,
    IReadOnlyList<TelegramInboundMessage> AcceptedMessages)
{
    public static TelegramPollResult Empty(long nextOffset)
    {
        return new TelegramPollResult(nextOffset, Array.Empty<TelegramInboundMessage>());
    }
}
