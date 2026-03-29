namespace FFXIVTelegram.Tests.Chat;

using FFXIVTelegram.Chat;
using Xunit;

public sealed class RouteResolverTests
{
    [Fact]
    public void ExplicitTagWinsOverReplyAndLastActive()
    {
        var resolver = new RouteResolver(new RouteTagParser());

        var result = resolver.Resolve("/fc hello", replyRoute: ChatRoute.Party(), lastActiveRoute: ChatRoute.Tell("Alice"));

        Assert.Equal(ChatRoute.FreeCompany(), result.Route);
        Assert.Equal("hello", result.MessageText);
    }

    [Fact]
    public void SlashRFailsWhenLastActiveRouteIsNotTell()
    {
        var resolver = new RouteResolver(new RouteTagParser());

        var result = resolver.Resolve("/r hello", replyRoute: null, lastActiveRoute: ChatRoute.Party());

        Assert.False(result.IsSuccess);
        Assert.Equal("Route could not be resolved.", result.ErrorMessage);
        Assert.Null(result.Route);
    }

    [Fact]
    public void SlashRUsesTheMostRecentTellTarget()
    {
        var resolver = new RouteResolver(new RouteTagParser());

        var result = resolver.Resolve("/r hello", replyRoute: null, lastActiveRoute: ChatRoute.Tell("Alice"));

        Assert.True(result.IsSuccess);
        Assert.Equal(ChatRoute.Tell("Alice"), result.Route);
        Assert.Equal("hello", result.MessageText);
    }
}
