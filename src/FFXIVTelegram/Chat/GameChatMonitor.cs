namespace FFXIVTelegram.Chat;

using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Plugin.Services;
using FFXIVTelegram.Configuration;
using FFXIVTelegram.Telegram;

public sealed class GameChatMonitor : IDisposable
{
    private readonly object routeStateGate = new();
    private readonly IChatGui chatGui;
    private readonly FfxivTelegramConfiguration configuration;
    private readonly TelegramBridgeService telegramBridge;
    private readonly TelegramReplyMap replyMap;
    private ChatRoute? lastActiveRoute;
    private ChatRoute.TellRoute? lastTellRoute;

    public GameChatMonitor(
        IChatGui chatGui,
        FfxivTelegramConfiguration configuration,
        TelegramBridgeService telegramBridge,
        TelegramReplyMap replyMap)
    {
        this.chatGui = chatGui ?? throw new ArgumentNullException(nameof(chatGui));
        this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        this.telegramBridge = telegramBridge ?? throw new ArgumentNullException(nameof(telegramBridge));
        this.replyMap = replyMap ?? throw new ArgumentNullException(nameof(replyMap));

        this.chatGui.ChatMessage += this.OnChatMessage;
    }

    public RouteContext CurrentRouteContext
    {
        get
        {
            lock (this.routeStateGate)
            {
                return RouteContext.FromState(this.lastActiveRoute, this.lastTellRoute);
            }
        }
    }

    public async Task<TelegramSendResult?> ForwardAsync(
        XivChatType type,
        string sender,
        string message,
        CancellationToken cancellationToken = default)
    {
        var forwarded = GameChatFormatter.Format(type, sender, message);
        if (forwarded is null)
        {
            return null;
        }

        this.UpdateRouteContext(forwarded.Route);

        if (!this.IsForwardingEnabled(forwarded.Route))
        {
            return null;
        }

        var sendResult = await this.telegramBridge.SendToAuthorizedChatAsync(forwarded.Text, cancellationToken).ConfigureAwait(false);
        if (sendResult.Success && sendResult.MessageId is long messageId)
        {
            this.replyMap.Store(messageId, forwarded.Route);
        }

        return sendResult;
    }

    public void Dispose()
    {
        this.chatGui.ChatMessage -= this.OnChatMessage;
    }

    public void RecordRouteUsage(ChatRoute route)
    {
        ArgumentNullException.ThrowIfNull(route);
        this.UpdateRouteContext(route);
    }

    private void OnChatMessage(XivChatType type, int timestamp, ref SeString sender, ref SeString message, ref bool isHandled)
    {
        _ = this.ForwardSafelyAsync(type, sender.TextValue, message.TextValue, CancellationToken.None);
    }

    private async Task ForwardSafelyAsync(XivChatType type, string sender, string message, CancellationToken cancellationToken)
    {
        try
        {
            await this.ForwardAsync(type, sender, message, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            // Chat forwarding is best-effort from the event hook.
        }
    }

    private bool IsForwardingEnabled(ChatRoute route)
    {
        lock (this.configuration)
        {
            return route switch
            {
                ChatRoute.TellRoute => this.configuration.EnableTellForwarding,
                ChatRoute.PartyRoute => this.configuration.EnablePartyForwarding,
                ChatRoute.FreeCompanyRoute => this.configuration.EnableFreeCompanyForwarding,
                _ => false,
            };
        }
    }

    private void UpdateRouteContext(ChatRoute route)
    {
        lock (this.routeStateGate)
        {
            this.lastActiveRoute = route;
            this.lastTellRoute = route as ChatRoute.TellRoute ?? this.lastTellRoute;
        }
    }
}
