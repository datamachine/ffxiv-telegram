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
    public void RouteContextDoesNotExposeAPublicConstructor()
    {
        var publicConstructors = typeof(RouteContext).GetConstructors(BindingFlags.Instance | BindingFlags.Public);

        Assert.Empty(publicConstructors);
    }

    [Fact]
    public void RouteContextRejectsConflictingTellState()
    {
        var exception = Assert.Throws<ArgumentException>(() => RouteContext.FromState(ChatRoute.Tell("Alice"), ChatRoute.Tell("Bob")));

        Assert.Equal("lastTellRoute", exception.ParamName);
    }

    [Fact]
    public void ExplicitTagWinsOverReplyAndLastActive()
    {
        var resolver = new RouteResolver(new RouteTagParser());

        var result = resolver.Resolve("/fc hello", replyRoute: ChatRoute.Party(), context: RouteContext.FromState(ChatRoute.Party(), ChatRoute.Tell("Alice")));

        Assert.Equal(ChatRoute.FreeCompany(), result.Route);
        Assert.Equal("hello", result.MessageText);
    }

    [Fact]
    public void TagOnlyInputFailsEvenWhenReplyAndLastActiveExist()
    {
        var resolver = new RouteResolver(new RouteTagParser());

        var result = resolver.Resolve("/fc", replyRoute: ChatRoute.Party(), context: RouteContext.FromState(ChatRoute.Party(), ChatRoute.Tell("Alice")));

        Assert.False(result.IsSuccess);
        Assert.Equal("Route could not be resolved.", result.ErrorMessage);
        Assert.Null(result.Route);
    }

    [Fact]
    public void WhitespaceOnlyInputFailsEvenWhenReplyAndLastActiveExist()
    {
        var resolver = new RouteResolver(new RouteTagParser());

        var result = resolver.Resolve("   ", replyRoute: ChatRoute.Party(), context: RouteContext.FromState(ChatRoute.Party(), ChatRoute.Tell("Alice")));

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

        var result = resolver.Resolve(text, replyRoute: ChatRoute.Party(), context: RouteContext.FromState(ChatRoute.Party(), ChatRoute.Tell("Alice")));

        Assert.False(result.IsSuccess);
        Assert.Equal("Route could not be resolved.", result.ErrorMessage);
        Assert.Null(result.Route);
    }

    [Fact]
    public void ReplyRouteWinsOverLastActiveForUntaggedInput()
    {
        var resolver = new RouteResolver(new RouteTagParser());

        var result = resolver.Resolve("hello back", replyRoute: ChatRoute.Party(), context: RouteContext.FromState(ChatRoute.Party(), ChatRoute.Tell("Alice")));

        Assert.True(result.IsSuccess);
        Assert.Equal(ChatRoute.Party(), result.Route);
        Assert.Equal("hello back", result.MessageText);
    }

    [Fact]
    public void LastActiveRouteIsUsedForUntaggedInput()
    {
        var resolver = new RouteResolver(new RouteTagParser());

        var result = resolver.Resolve("hello back", replyRoute: null, context: RouteContext.FromState(ChatRoute.Tell("Alice")));

        Assert.True(result.IsSuccess);
        Assert.Equal(ChatRoute.Tell("Alice"), result.Route);
        Assert.Equal("hello back", result.MessageText);
    }

    [Fact]
    public void SlashRUsesTheMostRecentTellTarget()
    {
        var resolver = new RouteResolver(new RouteTagParser());

        var result = resolver.Resolve("/r hello", replyRoute: null, context: RouteContext.FromState(ChatRoute.Tell("Alice"), null));

        Assert.True(result.IsSuccess);
        Assert.Equal(ChatRoute.Tell("Alice"), result.Route);
        Assert.Equal("hello", result.MessageText);
    }

    [Fact]
    public void SlashRUsesStoredTellTargetEvenWhenLastActiveRouteIsParty()
    {
        var resolver = new RouteResolver(new RouteTagParser());
        var context = RouteContext.FromState(ChatRoute.Party(), ChatRoute.Tell("Alice"));

        var result = resolver.Resolve("/r hello", replyRoute: null, context);

        Assert.True(result.IsSuccess);
        Assert.Equal(ChatRoute.Tell("Alice"), result.Route);
        Assert.Equal("hello", result.MessageText);
    }

    [Fact]
    public void UntaggedInputFallsBackToGenericLastActiveRoute()
    {
        var resolver = new RouteResolver(new RouteTagParser());
        var context = RouteContext.FromState(ChatRoute.Party(), ChatRoute.Tell("Alice"));

        var result = resolver.Resolve("hello", replyRoute: null, context);

        Assert.True(result.IsSuccess);
        Assert.Equal(ChatRoute.Party(), result.Route);
        Assert.Equal("hello", result.MessageText);
    }

    [Fact]
    public void UnsupportedTagFailsCleanly()
    {
        var resolver = new RouteResolver(new RouteTagParser());

        var result = resolver.Resolve("/guild hello", replyRoute: ChatRoute.Party(), context: RouteContext.FromState(ChatRoute.Party(), ChatRoute.Tell("Alice")));

        Assert.False(result.IsSuccess);
        Assert.Equal("Route tag unsupported.", result.ErrorMessage);
        Assert.Null(result.Route);
    }

    [Fact]
    public void NoRouteFailsCleanly()
    {
        var resolver = new RouteResolver(new RouteTagParser());

        var result = resolver.Resolve("hello", replyRoute: null, context: RouteContext.FromState(null));

        Assert.False(result.IsSuccess);
        Assert.Equal("Route could not be resolved.", result.ErrorMessage);
        Assert.Null(result.Route);
    }
}
