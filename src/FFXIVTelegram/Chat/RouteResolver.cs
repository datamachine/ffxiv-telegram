namespace FFXIVTelegram.Chat;

public sealed class RouteResolver
{
    private readonly RouteTagParser tagParser;

    public RouteResolver(RouteTagParser tagParser)
    {
        this.tagParser = tagParser ?? throw new ArgumentNullException(nameof(tagParser));
    }

    public RouteResolution Resolve(string text, ChatRoute? replyRoute, RouteContext context)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(context);

        if (string.IsNullOrWhiteSpace(text))
        {
            return RouteResolution.Failure("Route could not be resolved.");
        }

        if (this.tagParser.TryParse(text, context.LastTellRoute, out var tagged))
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

        if (context.LastActiveRoute is not null)
        {
            return RouteResolution.Success(context.LastActiveRoute, text);
        }

        return RouteResolution.Failure("Route could not be resolved.");
    }
}
