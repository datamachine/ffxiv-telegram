namespace FFXIVTelegram.Chat;

public sealed class RouteTagParser
{
    public bool TryParse(string text, ChatRoute? lastActiveTellRoute, out RouteResolution resolution)
    {
        ArgumentNullException.ThrowIfNull(text);

        var trimmed = text.TrimStart();
        if (trimmed.Length == 0 || trimmed[0] != '/')
        {
            resolution = default!;
            return false;
        }

        var separatorIndex = trimmed.IndexOfAny([' ', '\t', '\r', '\n']);
        var tag = separatorIndex < 0 ? trimmed : trimmed[..separatorIndex];
        var messageText = separatorIndex < 0 ? string.Empty : trimmed[separatorIndex..].TrimStart();

        switch (tag.ToLowerInvariant())
        {
            case "/fc":
                resolution = RouteResolution.Success(ChatRoute.FreeCompany(), messageText);
                return true;
            case "/p":
                resolution = RouteResolution.Success(ChatRoute.Party(), messageText);
                return true;
            case "/r":
                if (lastActiveTellRoute is not ChatRoute.TellRoute)
                {
                    resolution = RouteResolution.Failure("Route could not be resolved.");
                    return true;
                }

                resolution = RouteResolution.Success(lastActiveTellRoute, messageText);
                return true;
            default:
                resolution = RouteResolution.Failure("Route tag unsupported.");
                return true;
        }
    }
}
