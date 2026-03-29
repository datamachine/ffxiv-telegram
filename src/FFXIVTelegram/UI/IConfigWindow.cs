namespace FFXIVTelegram.UI;

using FFXIVTelegram.Telegram;

public interface IConfigWindow
{
    bool IsOpen { get; set; }

    TelegramConnectionState ConnectionState { get; set; }

    void Draw();
}
