namespace FFXIVTelegram.Tests.Telegram;

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FFXIVTelegram.Configuration;
using FFXIVTelegram.Telegram;
using Xunit;

public sealed class TelegramHttpClientAdapterTests
{
    [Fact]
    public void DefaultConstructorUsesLongPollCompatibleTimeout()
    {
        var configuration = new FfxivTelegramConfiguration
        {
            TelegramBotToken = "token",
        };

        using var adapter = new TelegramHttpClientAdapter(configuration);
        var field = typeof(TelegramHttpClientAdapter).GetField("httpClient", BindingFlags.Instance | BindingFlags.NonPublic);
        var httpClient = Assert.IsType<HttpClient>(field?.GetValue(adapter));

        Assert.Equal(TimeSpan.FromSeconds(35), httpClient.Timeout);
    }

    [Fact]
    public async Task GetUpdatesAsyncParsesPrivateMessagePayloads()
    {
        var configuration = new FfxivTelegramConfiguration
        {
            TelegramBotToken = "token",
        };
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                """
                {
                  "ok": true,
                  "result": [
                    {
                      "update_id": 10,
                      "message": {
                        "message_id": 100,
                        "text": "hello",
                        "chat": {
                          "id": 42,
                          "type": "private"
                        },
                        "reply_to_message": {
                          "message_id": 99
                        }
                      }
                    }
                  ]
                }
                """,
                Encoding.UTF8,
                "application/json"),
        });
        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.telegram.org/"),
        };
        using var adapter = new TelegramHttpClientAdapter(configuration, httpClient);

        var updates = await adapter.GetUpdatesAsync(offset: 5, CancellationToken.None);

        var update = Assert.Single(updates);
        Assert.Equal(10, update.UpdateId);
        Assert.Equal(100, update.MessageId);
        Assert.Equal(99, update.ReplyToMessageId);
        Assert.Equal(42, update.ChatId);
        Assert.True(update.IsPrivateChat);
        Assert.Equal("hello", update.Text);
        Assert.Equal("https://api.telegram.org/bottoken/getUpdates?offset=5&timeout=30", handler.RequestUris[0]?.ToString());
    }

    [Fact]
    public async Task SendTextAsyncReturnsMessageIdFromTelegramResponse()
    {
        var configuration = new FfxivTelegramConfiguration
        {
            TelegramBotToken = "token",
        };
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                """
                {
                  "ok": true,
                  "result": {
                    "message_id": 1234
                  }
                }
                """,
                Encoding.UTF8,
                "application/json"),
        });
        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.telegram.org/"),
        };
        using var adapter = new TelegramHttpClientAdapter(configuration, httpClient);

        var result = await adapter.SendTextAsync(42, "hello", CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(1234, result.MessageId);
        Assert.Equal("https://api.telegram.org/bottoken/sendMessage", handler.RequestUris[0]?.ToString());
    }

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler) : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> handler = handler;

        public List<Uri?> RequestUris { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            this.RequestUris.Add(request.RequestUri);
            return Task.FromResult(this.handler(request));
        }
    }
}
