namespace FFXIVTelegram.Notifications;

using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Plugin.Services;
using FFXIVTelegram.Configuration;

// English client only. Localization is intentionally out of scope for this iteration.
// A future iteration would replace the literal English suffixes below with a
// per-locale parser table keyed off the active client locale.
public sealed class SocialPresenceNotifier : IDisposable
{
    private readonly IChatGui chatGui;
    private readonly INotificationDispatcher dispatcher;
    private readonly FfxivTelegramConfiguration configuration;
    private readonly TimeProvider timeProvider;

    public SocialPresenceNotifier(
        IChatGui chatGui,
        INotificationDispatcher dispatcher,
        FfxivTelegramConfiguration configuration,
        TimeProvider timeProvider)
    {
        this.chatGui = chatGui ?? throw new ArgumentNullException(nameof(chatGui));
        this.dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        this.timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));

        this.chatGui.ChatMessage += this.OnChatMessage;
    }

    public void Dispose()
    {
        this.chatGui.ChatMessage -= this.OnChatMessage;
    }

    private void OnChatMessage(XivChatType type, int timestamp, ref SeString sender, ref SeString message, ref bool isHandled)
    {
    }
}
