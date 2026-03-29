namespace FFXIVTelegram.Tests.Chat;

using FFXIVTelegram.Chat;
using System.Linq;
using System.Reflection;
using Xunit;

public sealed class RouteResolverTests
{
    [Fact]
    public void RouteResolverExposesOnlyTheContextBasedResolveOverload()
    {
        var publicResolveMethods = typeof(RouteResolver)
            .GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .Where(method => method.Name == nameof(RouteResolver.Resolve))
            .ToArray();

        Assert.Single(publicResolveMethods);
        Assert.Equal(3, publicResolveMethods[0].GetParameters().Length);
        Assert.Equal(typeof(RouteContext), publicResolveMethods[0].GetParameters()[2].ParameterType);
    }

    [Fact]
    public void ExplicitTagWinsOverReplyAndLastActive()
    {
        var resolver = new RouteResolver(new RouteTagParser());

        var result = resolver.Resolve("/fc hello", replyRoute: ChatRoute.Party(), context: new RouteContext(ChatRoute.Party(), new ChatRoute.TellRoute("Alice")));

        Assert.Equal(ChatRoute.FreeCompany(), result.Route);
        Assert.Equal("hello", result.MessageText);
    }

    [Fact]
    public void TagOnlyInputFailsEvenWhenReplyAndLastActiveExist()
    {
        var resolver = new RouteResolver(new RouteTagParser());

        var result = resolver.Resolve("/fc", replyRoute: ChatRoute.Party(), context: new RouteContext(ChatRoute.Party(), new ChatRoute.TellRoute("Alice")));

        Assert.False(result.IsSuccess);
        Assert.Equal("Route could not be resolved.", result.ErrorMessage);
        Assert.Null(result.Route);
    }

    [Fact]
    public void WhitespaceOnlyInputFailsEvenWhenReplyAndLastActiveExist()
    {
        var resolver = new RouteResolver(new RouteTagParser());

        var result = resolver.Resolve("   ", replyRoute: ChatRoute.Party(), context: new RouteContext(ChatRoute.Party(), new ChatRoute.TellRoute("Alice")));

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

        var result = resolver.Resolve(text, replyRoute: ChatRoute.Party(), context: new RouteContext(ChatRoute.Party(), new ChatRoute.TellRoute("Alice")));

        Assert.False(result.IsSuccess);
        Assert.Equal("Route could not be resolved.", result.ErrorMessage);
        Assert.Null(result.Route);
    }

    [Fact]
    public void ReplyRouteWinsOverLastActiveForUntaggedInput()
    {
        var resolver = new RouteResolver(new RouteTagParser());

        var result = resolver.Resolve("hello back", replyRoute: ChatRoute.Party(), context: new RouteContext(ChatRoute.Party(), new ChatRoute.TellRoute("Alice")));

        Assert.True(result.IsSuccess);
        Assert.Equal(ChatRoute.Party(), result.Route);
        Assert.Equal("hello back", result.MessageText);
    }

    [Fact]
    public void LastActiveRouteIsUsedForUntaggedInput()
    {
        var resolver = new RouteResolver(new RouteTagParser());

        var result = resolver.Resolve("hello back", replyRoute: null, context: new RouteContext(new ChatRoute.TellRoute("Alice"), new ChatRoute.TellRoute("Alice")));

        Assert.True(result.IsSuccess);
        Assert.Equal(ChatRoute.Tell("Alice"), result.Route);
        Assert.Equal("hello back", result.MessageText);
    }

    [Fact]
    public void SlashRUsesTheMostRecentTellTarget()
    {
        var resolver = new RouteResolver(new RouteTagParser());

        var result = resolver.Resolve("/r hello", replyRoute: null, context: new RouteContext(new ChatRoute.TellRoute("Alice"), new ChatRoute.TellRoute("Alice")));

        Assert.True(result.IsSuccess);
        Assert.Equal(ChatRoute.Tell("Alice"), result.Route);
        Assert.Equal("hello", result.MessageText);
    }

    [Fact]
    public void SlashRUsesStoredTellTargetEvenWhenLastActiveRouteIsParty()
    {
        var resolver = new RouteResolver(new RouteTagParser());
        var context = new RouteContext(ChatRoute.Party(), new ChatRoute.TellRoute("Alice"));

        var result = resolver.Resolve("/r hello", replyRoute: null, context);

        Assert.True(result.IsSuccess);
        Assert.Equal(ChatRoute.Tell("Alice"), result.Route);
        Assert.Equal("hello", result.MessageText);
    }

    [Fact]
    public void UntaggedInputFallsBackToGenericLastActiveRoute()
    {
        var resolver = new RouteResolver(new RouteTagParser());
        var context = new RouteContext(ChatRoute.Party(), new ChatRoute.TellRoute("Alice"));

        var result = resolver.Resolve("hello", replyRoute: null, context);

        Assert.True(result.IsSuccess);
        Assert.Equal(ChatRoute.Party(), result.Route);
        Assert.Equal("hello", result.MessageText);
    }

    [Fact]
    public void UnsupportedTagFailsCleanly()
    {
        var resolver = new RouteResolver(new RouteTagParser());

        var result = resolver.Resolve("/guild hello", replyRoute: ChatRoute.Party(), context: new RouteContext(ChatRoute.Party(), new ChatRoute.TellRoute("Alice")));

        Assert.False(result.IsSuccess);
        Assert.Equal("Route tag unsupported.", result.ErrorMessage);
        Assert.Null(result.Route);
    }

    [Fact]
    public void NoRouteFailsCleanly()
    {
        var resolver = new RouteResolver(new RouteTagParser());

        var result = resolver.Resolve("hello", replyRoute: null, context: new RouteContext(null, null));

        Assert.False(result.IsSuccess);
        Assert.Equal("Route could not be resolved.", result.ErrorMessage);
        Assert.Null(result.Route);
    }
}
