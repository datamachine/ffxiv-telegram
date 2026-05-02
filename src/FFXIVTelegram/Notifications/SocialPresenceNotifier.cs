namespace FFXIVTelegram.Notifications;

using System.Collections.Generic;
using Dalamud.Game.Chat;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Plugin.Services;
using FFXIVTelegram.Configuration;

// English client only. Localization is intentionally out of scope for this iteration.
// A future iteration would replace the literal English suffixes below with a
// per-locale parser table keyed off the active client locale.
public sealed class SocialPresenceNotifier : IDisposable
{
    private const string LoginSuffix = "has logged in";
    private const string LogoutSuffix = "has logged out";
    private const string FcPrefix = "[FC]";
    private static readonly TimeSpan DedupeWindow = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan DedupeEvictionAge = TimeSpan.FromSeconds(60);
    private readonly IChatGui chatGui;
    private readonly INotificationDispatcher dispatcher;
    private readonly FfxivTelegramConfiguration configuration;
    private readonly TimeProvider timeProvider;
    private readonly object dedupeGate = new();
    private readonly Dictionary<(SocialEventKind Kind, string Name), DateTimeOffset> lastFiredAt = new();

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

    private void OnChatMessage(IHandleableChatMessage message)
    {
        if (message.LogKind != XivChatType.SystemMessage)
        {
            return;
        }

        var text = message.Message.TextValue;
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        var parsed = TryParse(text);
        if (parsed is not (SocialEventKind kind, string name))
        {
            return;
        }

        if (!this.IsEnabled(kind))
        {
            return;
        }

        if (!this.AcceptForDedupe(kind, name))
        {
            return;
        }

        _ = this.DispatchSafelyAsync(kind, name);
    }

    internal static (SocialEventKind kind, string name)? TryParse(string text)
    {
        var trimmed = text.Trim();
        var isFc = false;
        if (trimmed.StartsWith(FcPrefix, StringComparison.Ordinal))
        {
            isFc = true;
            trimmed = trimmed[FcPrefix.Length..].TrimStart();
        }

        if (TryStripSuffix(trimmed, LoginSuffix, out var loginName))
        {
            return (isFc ? SocialEventKind.FcLogin : SocialEventKind.FriendLogin, loginName);
        }

        if (TryStripSuffix(trimmed, LogoutSuffix, out var logoutName))
        {
            return (isFc ? SocialEventKind.FcLogout : SocialEventKind.FriendLogout, logoutName);
        }

        return null;
    }

    private static bool TryStripSuffix(string trimmed, string suffix, out string name)
    {
        var withoutPeriod = trimmed.EndsWith('.') ? trimmed[..^1].TrimEnd() : trimmed;
        if (!withoutPeriod.EndsWith(suffix, StringComparison.Ordinal))
        {
            name = string.Empty;
            return false;
        }

        var captured = withoutPeriod[..^suffix.Length].TrimEnd();
        if (string.IsNullOrWhiteSpace(captured))
        {
            name = string.Empty;
            return false;
        }

        name = captured;
        return true;
    }

    private async Task DispatchSafelyAsync(SocialEventKind kind, string name)
    {
        try
        {
            var text = FormatMessage(kind, name);
            var replyRoute = FFXIVTelegram.Chat.ChatRoute.Tell(name);
            await this.dispatcher.SendAsync(text, replyRoute, CancellationToken.None).ConfigureAwait(false);
        }
        catch
        {
            // Notification dispatch is best-effort.
        }
    }

    private static string FormatMessage(SocialEventKind kind, string name)
    {
        return kind switch
        {
            SocialEventKind.FriendLogin => "[Friend] " + name + " logged in",
            SocialEventKind.FriendLogout => "[Friend] " + name + " logged out",
            SocialEventKind.FcLogin => "[FC] " + name + " logged in",
            SocialEventKind.FcLogout => "[FC] " + name + " logged out",
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
        };
    }

    private bool IsEnabled(SocialEventKind kind)
    {
        return kind switch
        {
            SocialEventKind.FriendLogin or SocialEventKind.FriendLogout => this.configuration.EnableFriendPresenceNotifications,
            SocialEventKind.FcLogin or SocialEventKind.FcLogout => this.configuration.EnableFreeCompanyPresenceNotifications,
            _ => false,
        };
    }

    private bool AcceptForDedupe(SocialEventKind kind, string name)
    {
        var now = this.timeProvider.GetUtcNow();
        var key = (kind, name);

        lock (this.dedupeGate)
        {
            this.EvictOldEntries(now);

            if (this.lastFiredAt.TryGetValue(key, out var lastFired) && now - lastFired < DedupeWindow)
            {
                return false;
            }

            this.lastFiredAt[key] = now;
            return true;
        }
    }

    private void EvictOldEntries(DateTimeOffset now)
    {
        var threshold = now - DedupeEvictionAge;
        var staleKeys = new List<(SocialEventKind Kind, string Name)>();

        foreach (var entry in this.lastFiredAt)
        {
            if (entry.Value < threshold)
            {
                staleKeys.Add(entry.Key);
            }
        }

        foreach (var key in staleKeys)
        {
            this.lastFiredAt.Remove(key);
        }
    }
}
