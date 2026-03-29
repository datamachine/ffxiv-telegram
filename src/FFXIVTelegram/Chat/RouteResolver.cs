namespace FFXIVTelegram.Chat;

public sealed class RouteResolver
{
    private readonly RouteTagParser tagParser;

    public RouteResolver(RouteTagParser tagParser)
    {
        this.tagParser = tagParser ?? throw new ArgumentNullException(nameof(tagParser));
    }

    public RouteResolution Resolve(string text, ChatRoute? replyRoute, ChatRoute? lastActiveRoute)
    {
        ArgumentNullException.ThrowIfNull(text);

        if (string.IsNullOrWhiteSpace(text))
        {
            return RouteResolution.Failure("Route could not be resolved.");
        }

        if (this.tagParser.TryParse(text, lastActiveRoute, out var tagged))
        {
            if (!tagged.IsSuccess)
            {
                return tagged;
            }

            if (string.IsNullOrWhiteSpace(tagged.MessageText))
            {
                return RouteResolution.Failure("Route could not be resolved.");
            }

            return tagged;
        }

        if (replyRoute is not null)
        {
            return RouteResolution.Success(replyRoute, text);
        }

        if (lastActiveRoute is not null)
        {
            return RouteResolution.Success(lastActiveRoute, text);
        }

        return RouteResolution.Failure("Route could not be resolved.");
    }
}
