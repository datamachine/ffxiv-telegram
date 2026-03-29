namespace FFXIVTelegram.Configuration;

using Dalamud.Configuration;

public sealed class FfxivTelegramConfiguration : IPluginConfiguration
{
    public int Version { get; set; } = 1;

    public string TelegramBotToken { get; set; } = string.Empty;

    public bool HasTelegramBotToken => !string.IsNullOrWhiteSpace(this.TelegramBotToken);

    public long? AuthorizedChatId { get; set; }

    public bool HasAuthorizedChat => this.AuthorizedChatId.HasValue;

    public bool EnableTellForwarding { get; set; } = true;

    public bool EnablePartyForwarding { get; set; } = true;

    public bool EnableFreeCompanyForwarding { get; set; } = true;
}
