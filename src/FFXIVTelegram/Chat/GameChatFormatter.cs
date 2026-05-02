namespace FFXIVTelegram.Chat;

using System.Text;
using Dalamud.Game.Text;

public static class GameChatFormatter
{
    public static ForwardedChatMessage? Format(XivChatType type, string sender, string message)
    {
        ArgumentNullException.ThrowIfNull(sender);
        ArgumentNullException.ThrowIfNull(message);

        var normalizedSender = NormalizeSender(sender);
        var normalizedMessage = NormalizeMessage(message);

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

    internal static string NormalizeSender(string sender)
    {
        ArgumentNullException.ThrowIfNull(sender);

        return NormalizeText(sender, insertSeparatorOnPrivateUse: true);
    }

    internal static string NormalizeMessage(string message)
    {
        ArgumentNullException.ThrowIfNull(message);

        return NormalizeText(message, insertSeparatorOnPrivateUse: false);
    }

    private static bool IsPrivateUseCharacter(char character)
    {
        return character is >= '\uE000' and <= '\uF8FF';
    }

    private static string NormalizeText(string value, bool insertSeparatorOnPrivateUse)
    {
        var trimmed = value.Trim();
        if (trimmed.Length == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder(trimmed.Length);
        var pendingSeparator = false;
        var seenVisibleCharacter = false;

        foreach (var character in trimmed)
        {
            if (IsPrivateUseCharacter(character))
            {
                if (insertSeparatorOnPrivateUse && seenVisibleCharacter)
                {
                    pendingSeparator = true;
                }

                continue;
            }

            if (char.IsWhiteSpace(character))
            {
                if (!seenVisibleCharacter || pendingSeparator)
                {
                    continue;
                }

                if (builder.Length > 0 && !char.IsWhiteSpace(builder[^1]))
                {
                    builder.Append(' ');
                }

                continue;
            }

            if (pendingSeparator)
            {
                if (builder.Length > 0 && builder[^1] != '@')
                {
                    builder.Append('@');
                }

                pendingSeparator = false;
            }

            builder.Append(character);
            seenVisibleCharacter = true;
        }

        return builder.ToString().Trim();
    }
}
