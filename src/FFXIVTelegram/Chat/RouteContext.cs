namespace FFXIVTelegram.Chat;

public sealed class RouteContext
{
    private RouteContext(ChatRoute? lastActiveRoute, ChatRoute.TellRoute? lastTellRoute)
    {
        this.LastActiveRoute = lastActiveRoute;
        this.LastTellRoute = lastTellRoute;
    }

    public ChatRoute? LastActiveRoute { get; }

    public ChatRoute.TellRoute? LastTellRoute { get; }

    public static RouteContext FromState(ChatRoute? lastActiveRoute, ChatRoute? lastTellRoute = null)
    {
        if (lastTellRoute is not null and not ChatRoute.TellRoute)
        {
            throw new ArgumentException("LastTellRoute must be a tell route.", nameof(lastTellRoute));
        }

        var normalizedTellRoute = lastTellRoute as ChatRoute.TellRoute ?? lastActiveRoute as ChatRoute.TellRoute;
        return new RouteContext(lastActiveRoute, normalizedTellRoute);
    }

    public static RouteContext FromLastActive(ChatRoute? lastActiveRoute)
    {
        return FromState(lastActiveRoute);
    }
}
