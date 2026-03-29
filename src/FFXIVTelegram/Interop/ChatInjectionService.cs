namespace FFXIVTelegram.Interop;

using FFXIVTelegram.Chat;

public sealed class ChatInjectionService
{
    private readonly IFrameworkDispatcher dispatcher;
    private readonly IGameChatExecutor executor;
    private readonly TimeSpan minimumDelay;
    private readonly Func<TimeSpan, CancellationToken, Task> delayAsync;
    private readonly Func<DateTimeOffset> utcNow;
    private readonly SemaphoreSlim gate = new(1, 1);
    private DateTimeOffset? lastInjectedAt;

    public ChatInjectionService(
        IFrameworkDispatcher dispatcher,
        IGameChatExecutor executor,
        TimeSpan minimumDelay,
        Func<TimeSpan, CancellationToken, Task>? delayAsync = null,
        Func<DateTimeOffset>? utcNow = null)
    {
        if (minimumDelay < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(minimumDelay));
        }

        this.dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        this.executor = executor ?? throw new ArgumentNullException(nameof(executor));
        this.minimumDelay = minimumDelay;
        this.delayAsync = delayAsync ?? Task.Delay;
        this.utcNow = utcNow ?? (() => DateTimeOffset.UtcNow);
    }

    public Task EnqueueAsync(ChatRoute route, string message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(route);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        return this.EnqueueInputAsync(this.FormatRouteInput(route, message.Trim()), cancellationToken);
    }

    public Task EnqueueRawAsync(string inputText, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(inputText);
        return this.EnqueueInputAsync(inputText.Trim(), cancellationToken);
    }

    private async Task EnqueueInputAsync(string inputText, CancellationToken cancellationToken)
    {
        await this.gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (this.lastInjectedAt is { } lastInjectedAt && this.minimumDelay > TimeSpan.Zero)
            {
                var remainingDelay = this.minimumDelay - (this.utcNow() - lastInjectedAt);
                if (remainingDelay > TimeSpan.Zero)
                {
                    await this.delayAsync(remainingDelay, cancellationToken).ConfigureAwait(false);
                }
            }

            await this.dispatcher.RunAsync(() => this.executor.Execute(inputText), cancellationToken).ConfigureAwait(false);
            this.lastInjectedAt = this.utcNow();
        }
        finally
        {
            this.gate.Release();
        }
    }

    private string FormatRouteInput(ChatRoute route, string message)
    {
        return route switch
        {
            ChatRoute.PartyRoute => $"/p {message}",
            ChatRoute.FreeCompanyRoute => $"/fc {message}",
            ChatRoute.TellRoute tell => $"/tell {tell.Target} {message}",
            _ => throw new InvalidOperationException("Unsupported chat route."),
        };
    }
}
