namespace FFXIVTelegram.Tests.Chat;

using Dalamud.Game.Text;
using FFXIVTelegram.Chat;
using Xunit;

public sealed class GameChatFormatterTests
{
    [Fact]
    public void FormatsTellMessagesWithTellPrefixAndTellRoute()
    {
        var result = GameChatFormatter.Format(XivChatType.TellIncoming, "Alice Example", "Hello!");

        Assert.NotNull(result);
        Assert.Equal("[Tell] <Alice Example>: Hello!", result!.Text);
        Assert.Equal(ChatRoute.Tell("Alice Example"), result.Route);
    }

    [Fact]
    public void FormatsOutgoingTellMessagesWithTellPrefixAndTellRoute()
    {
        var result = GameChatFormatter.Format(XivChatType.TellOutgoing, "Alice Example", "Hello!");

        Assert.NotNull(result);
        Assert.Equal("[Tell] <Alice Example>: Hello!", result!.Text);
        Assert.Equal(ChatRoute.Tell("Alice Example"), result.Route);
    }

    [Fact]
    public void FormatsPartyMessagesWithPartyPrefixAndPartyRoute()
    {
        var result = GameChatFormatter.Format(XivChatType.Party, "Alice Example", "Hello!");

        Assert.NotNull(result);
        Assert.Equal("[P] <Alice Example>: Hello!", result!.Text);
        Assert.Equal(ChatRoute.Party(), result.Route);
    }

    [Fact]
    public void FormatsFreeCompanyMessagesWithFreeCompanyPrefixAndFreeCompanyRoute()
    {
        var result = GameChatFormatter.Format(XivChatType.FreeCompany, "Alice Example", "Hello!");

        Assert.NotNull(result);
        Assert.Equal("[FC] <Alice Example>: Hello!", result!.Text);
        Assert.Equal(ChatRoute.FreeCompany(), result.Route);
    }

    [Fact]
    public void ReturnsNullForUnsupportedChatTypes()
    {
        var result = GameChatFormatter.Format(XivChatType.Say, "Alice Example", "Hello!");

        Assert.Null(result);
    }
}
