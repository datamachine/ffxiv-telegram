namespace FFXIVTelegram.Interop;

using FFXIVTelegram.Chat;

public interface IChatInjectionQueue
{
    Task EnqueueAsync(ChatRoute route, string message, CancellationToken cancellationToken = default);
}
