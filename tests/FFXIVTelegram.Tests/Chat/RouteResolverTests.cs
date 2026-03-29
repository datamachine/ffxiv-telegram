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
}
