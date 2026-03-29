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
        long? authorizedChatId;
        bool enableTellForwarding;
        bool enablePartyForwarding;
        bool enableFreeCompanyForwarding;
        lock (this.configuration)
        {
            authorizedChatId = this.configuration.AuthorizedChatId;
            enableTellForwarding = this.configuration.EnableTellForwarding;
            enablePartyForwarding = this.configuration.EnablePartyForwarding;
            enableFreeCompanyForwarding = this.configuration.EnableFreeCompanyForwarding;
        }

        if (authorizedChatId is long currentAuthorizedChatId)
        {
            ImGui.TextUnformatted($"Authorized chat: {currentAuthorizedChatId}");
        }
        else if (this.ConnectionState == TelegramConnectionState.WaitingForStart)
        {
            ImGui.TextWrapped("Send /start to the bot from a private Telegram chat to authorize this client.");
        }

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

        if (ImGui.Checkbox("Forward tells", ref enableTellForwarding))
        {
            lock (this.configuration)
            {
                this.configuration.EnableTellForwarding = enableTellForwarding;
                this.configurationStore.Save(this.configuration);
            }
        }

        if (ImGui.Checkbox("Forward party chat", ref enablePartyForwarding))
        {
            lock (this.configuration)
            {
                this.configuration.EnablePartyForwarding = enablePartyForwarding;
                this.configurationStore.Save(this.configuration);
            }
        }

        if (ImGui.Checkbox("Forward free company chat", ref enableFreeCompanyForwarding))
        {
            lock (this.configuration)
            {
                this.configuration.EnableFreeCompanyForwarding = enableFreeCompanyForwarding;
                this.configurationStore.Save(this.configuration);
            }
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
        lock (this.configuration)
        {
            if (this.configuration.TelegramBotToken == normalizedToken)
            {
                return;
            }

            var previousToken = this.configuration.TelegramBotToken;
            var previousAuthorizedChatId = this.configuration.AuthorizedChatId;
            this.configuration.TelegramBotToken = normalizedToken;
            this.configuration.AuthorizedChatId = null;
            this.telegramBotTokenBuffer = normalizedToken;
            try
            {
                this.configurationStore.Save(this.configuration);
            }
            catch
            {
                this.configuration.TelegramBotToken = previousToken;
                this.configuration.AuthorizedChatId = previousAuthorizedChatId;
                this.telegramBotTokenBuffer = previousToken;
                throw;
            }
        }
    }
}
