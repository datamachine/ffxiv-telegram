namespace FFXIVTelegram.Interop;

using System.Text;
using FFXIVTelegram.Chat;

public sealed class ChatInjectionService
    : IChatInjectionQueue, IChatInjectionCommandTarget, IDisposable
{
    private const int MaxInjectionUtf8Bytes = 500;

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
        var sanitizedMessage = SanitizeInputText(message, nameof(message));
        var inputText = this.FormatRouteInput(route, sanitizedMessage);
        ValidateSafeInputLength(inputText, nameof(message));
        return this.EnqueueInputAsync(inputText, cancellationToken);
    }

    public Task EnqueueRawAsync(string inputText, CancellationToken cancellationToken = default)
    {
        var sanitizedInput = SanitizeInputText(inputText, nameof(inputText));
        ValidateSafeInputLength(sanitizedInput, nameof(inputText));
        return this.EnqueueInputAsync(sanitizedInput, cancellationToken);
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

    private static string SanitizeInputText(string inputText, string paramName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(inputText, paramName);

        var sanitized = inputText
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Replace('\n', ' ')
            .Trim();

        ArgumentException.ThrowIfNullOrWhiteSpace(sanitized, paramName);
        return sanitized;
    }

    private static void ValidateSafeInputLength(string inputText, string paramName)
    {
        if (Encoding.UTF8.GetByteCount(inputText) > MaxInjectionUtf8Bytes)
        {
            throw new ArgumentException("Message exceeds FFXIV safe limits.", paramName);
        }
    }

    public void Dispose()
    {
        this.gate.Dispose();
    }
}
