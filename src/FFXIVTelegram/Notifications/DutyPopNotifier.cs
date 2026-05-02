namespace FFXIVTelegram.Notifications;

using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Plugin.Services;
using Dalamud.Utility;
using FFXIVTelegram.Configuration;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;

public sealed class DutyPopNotifier : IDisposable
{
    private const string AddonName = "ContentsFinderConfirm";
    private static readonly TimeSpan DedupeWindow = TimeSpan.FromSeconds(60);

    private readonly IAddonLifecycle addonLifecycle;
    private readonly INotificationDispatcher dispatcher;
    private readonly FfxivTelegramConfiguration configuration;
    private readonly TimeProvider timeProvider;
    private readonly object dedupeGate = new();
    private DateTimeOffset? lastFiredAt;

    public DutyPopNotifier(
        IAddonLifecycle addonLifecycle,
        INotificationDispatcher dispatcher,
        FfxivTelegramConfiguration configuration,
        TimeProvider timeProvider)
    {
        this.addonLifecycle = addonLifecycle ?? throw new ArgumentNullException(nameof(addonLifecycle));
        this.dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        this.timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));

        this.addonLifecycle.RegisterListener(AddonEvent.PostSetup, AddonName, this.OnAddonSetup);
    }

    public void Dispose()
    {
        this.addonLifecycle.UnregisterListener(AddonEvent.PostSetup, AddonName, this.OnAddonSetup);
    }

    internal async Task TryFireAsync(string? dutyName, CancellationToken cancellationToken)
    {
        if (!this.IsEnabled())
        {
            return;
        }

        if (!this.AcceptForDedupe())
        {
            return;
        }

        try
        {
            await this.dispatcher.SendAsync(FormatMessage(dutyName), replyRoute: null, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            // Notification dispatch is best-effort.
        }
    }

    private void OnAddonSetup(AddonEvent type, AddonArgs args)
    {
        string? dutyName = null;

        try
        {
            dutyName = ReadDutyNameFromAddon(args);
        }
        catch
        {
            dutyName = null;
        }

        _ = this.TryFireAsync(dutyName, CancellationToken.None);
    }

    internal static string? NormalizeDutyName(string? rawDutyName)
    {
        return string.IsNullOrWhiteSpace(rawDutyName)
            ? null
            : rawDutyName.Trim();
    }

    private static unsafe string? ReadDutyNameFromAddon(AddonArgs args)
    {
        if (args.Addon.IsNull)
        {
            return null;
        }

        var addon = (AddonContentsFinderConfirm*)args.Addon.Address;
        return ReadDutyTitleText(addon->AtkTextNode230);
    }

    private static unsafe string? ReadDutyTitleText(AtkTextNode* titleNode)
    {
        if (titleNode == null)
        {
            return null;
        }

        return NormalizeDutyName(titleNode->OriginalTextPointer.ExtractText());
    }

    private static string FormatMessage(string? dutyName)
    {
        return string.IsNullOrWhiteSpace(dutyName)
            ? "[Duty] A duty is ready"
            : "[Duty] " + dutyName + " is ready";
    }

    private bool IsEnabled()
    {
        return this.configuration.EnableDutyPopNotifications;
    }

    private bool AcceptForDedupe()
    {
        var now = this.timeProvider.GetUtcNow();

        lock (this.dedupeGate)
        {
            if (this.lastFiredAt is DateTimeOffset previous && now - previous < DedupeWindow)
            {
                return false;
            }

            this.lastFiredAt = now;
            return true;
        }
    }
}
