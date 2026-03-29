namespace FFXIVTelegram.Interop;

using Dalamud.Plugin.Services;

public sealed class FrameworkDispatcher : IFrameworkDispatcher
{
    private readonly IFramework framework;

    public FrameworkDispatcher(IFramework framework)
    {
        this.framework = framework ?? throw new ArgumentNullException(nameof(framework));
    }

    public Task RunAsync(Action action, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(action);
        cancellationToken.ThrowIfCancellationRequested();
        return this.framework.RunOnFrameworkThread(action);
    }
}
