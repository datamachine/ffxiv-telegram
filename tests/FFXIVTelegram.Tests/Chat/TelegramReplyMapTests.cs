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

    [Fact]
    public void ReplyMapEvictsOldestEntryWhenCapacityIsExceeded()
    {
        var currentTime = DateTimeOffset.Parse("2026-03-29T12:00:00Z");
        var map = new TelegramReplyMap(capacity: 2, maxAge: TimeSpan.FromMinutes(30), utcNow: () => currentTime);

        map.Store(1, ChatRoute.Tell("Alice"));
        map.Store(2, ChatRoute.Party());
        map.Store(3, ChatRoute.FreeCompany());

        Assert.False(map.TryGetRoute(1, out _));
        Assert.True(map.TryGetRoute(2, out var secondRoute));
        Assert.True(map.TryGetRoute(3, out var thirdRoute));
        Assert.Equal(ChatRoute.Party(), secondRoute);
        Assert.Equal(ChatRoute.FreeCompany(), thirdRoute);
    }

    [Fact]
    public void ReplyMapExpiresEntriesPastMaxAge()
    {
        var currentTime = DateTimeOffset.Parse("2026-03-29T12:00:00Z");
        var map = new TelegramReplyMap(capacity: 2, maxAge: TimeSpan.FromMinutes(30), utcNow: () => currentTime);

        map.Store(1, ChatRoute.Tell("Alice"));
        currentTime = currentTime.AddMinutes(31);

        Assert.False(map.TryGetRoute(1, out _));
    }

    [Fact]
    public void ReplyMapOverwritesDuplicateMessageIds()
    {
        var currentTime = DateTimeOffset.Parse("2026-03-29T12:00:00Z");
        var map = new TelegramReplyMap(capacity: 2, maxAge: TimeSpan.FromMinutes(30), utcNow: () => currentTime);

        map.Store(1, ChatRoute.Tell("Alice"));
        map.Store(1, ChatRoute.Tell("Bob"));

        Assert.True(map.TryGetRoute(1, out var route));
        Assert.Equal(ChatRoute.Tell("Bob"), route);
    }
}
