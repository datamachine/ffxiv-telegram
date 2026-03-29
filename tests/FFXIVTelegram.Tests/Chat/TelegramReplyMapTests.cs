namespace FFXIVTelegram.Tests.Chat;

using FFXIVTelegram.Chat;
using Xunit;

public sealed class TelegramReplyMapTests
{
    [Fact]
    public void ReplyMapReturnsStoredTellTarget()
    {
        var map = new TelegramReplyMap(capacity: 100, maxAge: TimeSpan.FromMinutes(30));
        map.Store(12345, ChatRoute.Tell("Alice"));

        Assert.True(map.TryGetRoute(12345, out var route));
        Assert.Equal(ChatRoute.Tell("Alice"), route);
    }
}
