namespace FFXIVTelegram.Configuration;

using Dalamud.Configuration;

public sealed class FfxivTelegramConfiguration : IPluginConfiguration
{
    public int Version { get; set; } = 1;

    public string TelegramBotToken { get; set; } = string.Empty;

    public long? AuthorizedChatId { get; set; }

    public bool EnableTellForwarding { get; set; } = true;

    public bool EnablePartyForwarding { get; set; } = true;

    public bool EnableFreeCompanyForwarding { get; set; } = true;
}
