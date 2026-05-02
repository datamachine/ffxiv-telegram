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
    public void FormatsPartyMessagesWithCrossWorldSenderNormalization()
    {
        var result = GameChatFormatter.Format(XivChatType.Party, "\uE000Ahrxa'a Epocan\uE001Adamantoise", "Hello!");

        Assert.NotNull(result);
        Assert.Equal("[P] <Ahrxa'a Epocan@Adamantoise>: Hello!", result!.Text);
        Assert.Equal(ChatRoute.Party(), result.Route);
    }

    [Fact]
    public void FormatsTellMessagesWithCrossWorldSenderNormalization()
    {
        var result = GameChatFormatter.Format(XivChatType.TellIncoming, "\uE000Lyric Leidenskraft\uE001Midgardsormr", "o/");

        Assert.NotNull(result);
        Assert.Equal("[Tell] <Lyric Leidenskraft@Midgardsormr>: o/", result!.Text);
        Assert.Equal(ChatRoute.Tell("Lyric Leidenskraft@Midgardsormr"), result.Route);
    }

    [Fact]
    public void FormatsPartyMessagesRemovingDecorativeIconGlyphsFromMessageText()
    {
        var result = GameChatFormatter.Format(XivChatType.Party, "\uE000Kura Saki\uE001Cactuar", "\uE040 Hello! \uE041");

        Assert.NotNull(result);
        Assert.Equal("[P] <Kura Saki@Cactuar>: Hello!", result!.Text);
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

    [Fact]
    public void NormalizeSenderStripsSlotGlyphsWithoutAddingWorldWhenNoCrossWorldMarkerExists()
    {
        Assert.Equal("Alice Example", GameChatFormatter.NormalizeSender("\uE000Alice Example"));
    }

    [Fact]
    public void ReturnsNullWhenMessageContainsOnlyDecorativeGlyphs()
    {
        var result = GameChatFormatter.Format(XivChatType.Party, "Alice Example", "\uE040   \uE041");

        Assert.Null(result);
    }
}
