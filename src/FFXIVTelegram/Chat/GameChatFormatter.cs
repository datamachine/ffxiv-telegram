namespace FFXIVTelegram.Chat;

using Dalamud.Game.Text;

public static class GameChatFormatter
{
    public static ForwardedChatMessage? Format(XivChatType type, string sender, string message)
    {
        ArgumentNullException.ThrowIfNull(sender);
        ArgumentNullException.ThrowIfNull(message);

        var normalizedSender = sender.Trim();
        var normalizedMessage = message.Trim();

        if (string.IsNullOrWhiteSpace(normalizedSender) || string.IsNullOrWhiteSpace(normalizedMessage))
        {
            return null;
        }

        return type switch
        {
            XivChatType.TellIncoming or XivChatType.TellOutgoing => new ForwardedChatMessage(
                "[Tell] <" + normalizedSender + ">: " + normalizedMessage,
                ChatRoute.Tell(normalizedSender)),
            XivChatType.Party => new ForwardedChatMessage(
                "[P] <" + normalizedSender + ">: " + normalizedMessage,
                ChatRoute.Party()),
            XivChatType.FreeCompany => new ForwardedChatMessage(
                "[FC] <" + normalizedSender + ">: " + normalizedMessage,
                ChatRoute.FreeCompany()),
            _ => null,
        };
    }
}
