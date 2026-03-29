namespace FFXIVTelegram.Interop;

public interface IFrameworkDispatcher
{
    Task RunAsync(Action action, CancellationToken cancellationToken = default);
}
