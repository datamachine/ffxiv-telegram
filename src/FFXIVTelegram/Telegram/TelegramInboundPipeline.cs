namespace FFXIVTelegram.Telegram;

using FFXIVTelegram.Chat;
using FFXIVTelegram.Interop;

public sealed class TelegramInboundPipeline
{
    private readonly RouteResolver routeResolver;
    private readonly TelegramReplyMap replyMap;
    private readonly Func<RouteContext> routeContextProvider;
    private readonly IChatInjectionQueue injectionQueue;
    private readonly Action<ChatRoute>? onInjected;
    private readonly Func<string, CancellationToken, Task>? notifyFailureAsync;

    public TelegramInboundPipeline(
        RouteResolver routeResolver,
        TelegramReplyMap replyMap,
        Func<RouteContext> routeContextProvider,
        IChatInjectionQueue injectionQueue,
        Action<ChatRoute>? onInjected = null,
        Func<string, CancellationToken, Task>? notifyFailureAsync = null)
    {
        this.routeResolver = routeResolver ?? throw new ArgumentNullException(nameof(routeResolver));
        this.replyMap = replyMap ?? throw new ArgumentNullException(nameof(replyMap));
        this.routeContextProvider = routeContextProvider ?? throw new ArgumentNullException(nameof(routeContextProvider));
        this.injectionQueue = injectionQueue ?? throw new ArgumentNullException(nameof(injectionQueue));
        this.onInjected = onInjected;
        this.notifyFailureAsync = notifyFailureAsync;
    }

    public async Task<bool> HandleAsync(TelegramInboundMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        ChatRoute? replyRoute = null;
        if (message.ReplyToMessageId is long replyToMessageId && this.replyMap.TryGetRoute(replyToMessageId, out var storedReplyRoute))
        {
            replyRoute = storedReplyRoute;
        }

        var resolution = this.routeResolver.Resolve(message.Text, replyRoute, this.routeContextProvider());
        if (!resolution.IsSuccess)
        {
            await this.NotifyFailureAsync(resolution.ErrorMessage, cancellationToken).ConfigureAwait(false);
            return false;
        }

        try
        {
            await this.injectionQueue.EnqueueAsync(resolution.Route!, resolution.MessageText, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            await this.NotifyFailureAsync($"Message injection failed: {exception.Message}", cancellationToken).ConfigureAwait(false);
            return false;
        }

        this.onInjected?.Invoke(resolution.Route!);
        return true;
    }

    private async Task NotifyFailureAsync(string? errorMessage, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(errorMessage) || this.notifyFailureAsync is null)
        {
            return;
        }

        try
        {
            await this.notifyFailureAsync(errorMessage, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            // Failure notifications are best-effort.
        }
    }
}
