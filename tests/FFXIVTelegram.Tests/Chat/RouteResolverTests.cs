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
    public void TagOnlyInputFailsEvenWhenReplyAndLastActiveExist()
    {
        var resolver = new RouteResolver(new RouteTagParser());

        var result = resolver.Resolve("/fc", replyRoute: ChatRoute.Party(), lastActiveRoute: ChatRoute.Tell("Alice"));

        Assert.False(result.IsSuccess);
        Assert.Equal("Route could not be resolved.", result.ErrorMessage);
        Assert.Null(result.Route);
    }

    [Fact]
    public void WhitespaceOnlyInputFailsEvenWhenReplyAndLastActiveExist()
    {
        var resolver = new RouteResolver(new RouteTagParser());

        var result = resolver.Resolve("   ", replyRoute: ChatRoute.Party(), lastActiveRoute: ChatRoute.Tell("Alice"));

        Assert.False(result.IsSuccess);
        Assert.Equal("Route could not be resolved.", result.ErrorMessage);
        Assert.Null(result.Route);
    }

    [Theory]
    [InlineData("/p")]
    [InlineData("/r")]
    public void TagOnlyInputFailsForAllSupportedRouteTags(string text)
    {
        var resolver = new RouteResolver(new RouteTagParser());

        var result = resolver.Resolve(text, replyRoute: ChatRoute.Party(), lastActiveRoute: ChatRoute.Tell("Alice"));

        Assert.False(result.IsSuccess);
        Assert.Equal("Route could not be resolved.", result.ErrorMessage);
        Assert.Null(result.Route);
    }

    [Fact]
    public void ReplyRouteWinsOverLastActiveForUntaggedInput()
    {
        var resolver = new RouteResolver(new RouteTagParser());

        var result = resolver.Resolve("hello back", replyRoute: ChatRoute.Party(), lastActiveRoute: ChatRoute.Tell("Alice"));

        Assert.True(result.IsSuccess);
        Assert.Equal(ChatRoute.Party(), result.Route);
        Assert.Equal("hello back", result.MessageText);
    }

    [Fact]
    public void LastActiveRouteIsUsedForUntaggedInput()
    {
        var resolver = new RouteResolver(new RouteTagParser());

        var result = resolver.Resolve("hello back", replyRoute: null, lastActiveRoute: ChatRoute.Tell("Alice"));

        Assert.True(result.IsSuccess);
        Assert.Equal(ChatRoute.Tell("Alice"), result.Route);
        Assert.Equal("hello back", result.MessageText);
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

    [Fact]
    public void SlashRFailsWhenNoTellTargetIsAvailable()
    {
        var resolver = new RouteResolver(new RouteTagParser());

        var result = resolver.Resolve("/r hello", replyRoute: null, lastActiveRoute: ChatRoute.Party());

        Assert.False(result.IsSuccess);
        Assert.Equal("Route could not be resolved.", result.ErrorMessage);
        Assert.Null(result.Route);
    }

    [Fact]
    public void SlashRFailsWhenNoLastActiveRouteExists()
    {
        var resolver = new RouteResolver(new RouteTagParser());

        var result = resolver.Resolve("/r hello", replyRoute: null, lastActiveRoute: null);

        Assert.False(result.IsSuccess);
        Assert.Equal("Route could not be resolved.", result.ErrorMessage);
        Assert.Null(result.Route);
    }

    [Fact]
    public void UnsupportedTagFailsCleanly()
    {
        var resolver = new RouteResolver(new RouteTagParser());

        var result = resolver.Resolve("/guild hello", replyRoute: ChatRoute.Party(), lastActiveRoute: ChatRoute.Tell("Alice"));

        Assert.False(result.IsSuccess);
        Assert.Equal("Route tag unsupported.", result.ErrorMessage);
        Assert.Null(result.Route);
    }

    [Fact]
    public void NoRouteFailsCleanly()
    {
        var resolver = new RouteResolver(new RouteTagParser());

        var result = resolver.Resolve("hello", replyRoute: null, lastActiveRoute: null);

        Assert.False(result.IsSuccess);
        Assert.Equal("Route could not be resolved.", result.ErrorMessage);
        Assert.Null(result.Route);
    }
}
