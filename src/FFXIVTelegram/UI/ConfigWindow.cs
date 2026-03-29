namespace FFXIVTelegram.UI;

using System;
using FFXIVTelegram.Configuration;
using FFXIVTelegram.Telegram;
using Dalamud.Bindings.ImGui;

public sealed class ConfigWindow
    : IConfigWindow
{
    private const string WindowName = "FFXIV Telegram Configuration";

    private readonly ConfigurationStore configurationStore;

    private readonly FfxivTelegramConfiguration configuration;

    private string telegramBotTokenBuffer;

    public ConfigWindow(FfxivTelegramConfiguration configuration, ConfigurationStore configurationStore)
    {
        this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        this.configurationStore = configurationStore ?? throw new ArgumentNullException(nameof(configurationStore));
        this.telegramBotTokenBuffer = this.configuration.TelegramBotToken;
    }

    public bool IsOpen { get; set; }

    public TelegramConnectionState ConnectionState { get; set; } = TelegramConnectionState.NotConfigured;

    public void Draw()
    {
        if (!this.IsOpen)
        {
            return;
        }

        var isOpen = this.IsOpen;
        if (!ImGui.Begin(WindowName, ref isOpen, ImGuiWindowFlags.AlwaysAutoResize))
        {
            this.IsOpen = isOpen;
            ImGui.End();
            return;
        }

        this.IsOpen = isOpen;

        ImGui.TextUnformatted($"Connection state: {this.ConnectionState}");
        ImGui.Spacing();

        var token = this.telegramBotTokenBuffer;
        if (ImGui.InputText("Telegram bot token", ref token, 512, ImGuiInputTextFlags.Password))
        {
            this.UpdateTelegramBotTokenBuffer(token);
        }

        if (ImGui.IsItemDeactivatedAfterEdit())
        {
            this.FinalizeTelegramBotTokenEdit();
        }

        ImGui.Spacing();

        var enableTellForwarding = this.configuration.EnableTellForwarding;
        if (ImGui.Checkbox("Forward tells", ref enableTellForwarding))
        {
            this.configuration.EnableTellForwarding = enableTellForwarding;
            this.configurationStore.Save(this.configuration);
        }

        var enablePartyForwarding = this.configuration.EnablePartyForwarding;
        if (ImGui.Checkbox("Forward party chat", ref enablePartyForwarding))
        {
            this.configuration.EnablePartyForwarding = enablePartyForwarding;
            this.configurationStore.Save(this.configuration);
        }

        var enableFreeCompanyForwarding = this.configuration.EnableFreeCompanyForwarding;
        if (ImGui.Checkbox("Forward free company chat", ref enableFreeCompanyForwarding))
        {
            this.configuration.EnableFreeCompanyForwarding = enableFreeCompanyForwarding;
            this.configurationStore.Save(this.configuration);
        }

        ImGui.End();
    }

    internal void UpdateTelegramBotTokenBuffer(string telegramBotTokenBuffer)
    {
        this.telegramBotTokenBuffer = telegramBotTokenBuffer;
    }

    internal void FinalizeTelegramBotTokenEdit()
    {
        var normalizedToken = this.telegramBotTokenBuffer.Trim();
        if (this.configuration.TelegramBotToken == normalizedToken)
        {
            return;
        }

        this.configuration.TelegramBotToken = normalizedToken;
        this.telegramBotTokenBuffer = normalizedToken;
        this.configurationStore.Save(this.configuration);
    }
}
