namespace FFXIVTelegram.Interop;

public interface IChatInjectionCommandTarget
{
    Task EnqueueRawAsync(string inputText, CancellationToken cancellationToken = default);
}
