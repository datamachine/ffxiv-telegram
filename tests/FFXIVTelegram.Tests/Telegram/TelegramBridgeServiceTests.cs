namespace FFXIVTelegram.Tests.Telegram;

using System.Threading.Tasks;
using FFXIVTelegram.Configuration;
using FFXIVTelegram.Telegram;
using FFXIVTelegram.Tests.TestDoubles;
using Xunit;

public sealed class TelegramBridgeServiceTests
{
    [Fact]
    public async Task FirstPrivateStartClaimsAuthorizedChat()
    {
        var service = this.CreateService();
        await service.HandleIncomingTextAsync(chatId: 42, isPrivateChat: true, text: "/start");

        Assert.Equal(42, service.Configuration.AuthorizedChatId);
        Assert.Equal(TelegramConnectionState.Connected, service.ConnectionState);
    }

    [Fact]
    public async Task IgnoresMessagesFromUnauthorizedChat()
    {
        var service = this.CreateService(authorizedChatId: 42);
        var result = await service.HandleIncomingTextAsync(chatId: 99, isPrivateChat: true, text: "hello");

        Assert.Equal(TelegramInboundResult.IgnoredUnauthorizedChat, result);
    }

    [Fact]
    public async Task RejectsNonPrivateStartWithoutClaimingAuthorization()
    {
        var service = this.CreateService();
        var result = await service.HandleIncomingTextAsync(chatId: 42, isPrivateChat: false, text: "/start");

        Assert.Equal(TelegramInboundResult.IgnoredUnsupportedChatType, result);
        Assert.Null(service.Configuration.AuthorizedChatId);
        Assert.Equal(TelegramConnectionState.WaitingForStart, service.ConnectionState);
    }

    private TelegramBridgeService CreateService(long? authorizedChatId = null)
    {
        var configuration = new FfxivTelegramConfiguration
        {
            TelegramBotToken = "token",
            AuthorizedChatId = authorizedChatId,
        };
        var plugin = DalamudPluginInterfaceTestDouble.Create(configuration, out _, out _);
        var store = new ConfigurationStore(plugin);

        return new TelegramBridgeService(
            configuration,
            new StubTelegramClientAdapter(),
            store);
    }

    private sealed class StubTelegramClientAdapter : ITelegramClientAdapter
    {
        public Task<System.Collections.Generic.IReadOnlyList<TelegramUpdate>> GetUpdatesAsync(long offset, System.Threading.CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<TelegramSendResult> SendTextAsync(long chatId, string text, System.Threading.CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
    }
}
