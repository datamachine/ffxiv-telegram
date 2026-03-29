namespace FFXIVTelegram.Tests.Integration;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FFXIVTelegram.Chat;
using FFXIVTelegram.Interop;
using FFXIVTelegram.Telegram;
using Xunit;

public sealed class InboundPipelineTests
{
    [Fact]
    public async Task ReplyMessageUsesStoredRouteBeforeLastActive()
    {
        var replyMap = new TelegramReplyMap(100, TimeSpan.FromMinutes(30));
        replyMap.Store(777, ChatRoute.Tell("Alice"));
        var routeContext = RouteContext.FromState(ChatRoute.Party());
        var injectionQueue = new RecordingChatInjectionQueue();
        var pipeline = new TelegramInboundPipeline(
            new RouteResolver(new RouteTagParser()),
            replyMap,
            () => routeContext,
            injectionQueue);

        var handled = await pipeline.HandleAsync(
            new TelegramInboundMessage(UpdateId: 1, MessageId: 2, ReplyToMessageId: 777, ChatId: 42, IsPrivateChat: true, Text: "hello back"));

        Assert.True(handled);
        var injection = Assert.Single(injectionQueue.Messages);
        Assert.Equal(ChatRoute.Tell("Alice"), injection.Route);
        Assert.Equal("hello back", injection.Message);
    }

    [Fact]
    public async Task ExplicitTagOverridesReplyRouteAndLastActive()
    {
        var replyMap = new TelegramReplyMap(100, TimeSpan.FromMinutes(30));
        replyMap.Store(777, ChatRoute.Tell("Alice"));
        var routeContext = RouteContext.FromState(ChatRoute.Party());
        var injectionQueue = new RecordingChatInjectionQueue();
        var pipeline = new TelegramInboundPipeline(
            new RouteResolver(new RouteTagParser()),
            replyMap,
            () => routeContext,
            injectionQueue);

        var handled = await pipeline.HandleAsync(
            new TelegramInboundMessage(UpdateId: 1, MessageId: 2, ReplyToMessageId: 777, ChatId: 42, IsPrivateChat: true, Text: "/fc hello"));

        Assert.True(handled);
        var injection = Assert.Single(injectionQueue.Messages);
        Assert.Equal(ChatRoute.FreeCompany(), injection.Route);
        Assert.Equal("hello", injection.Message);
    }

    [Fact]
    public async Task SuccessfulInboundMessageUpdatesLastRouteForLaterFallback()
    {
        var replyMap = new TelegramReplyMap(100, TimeSpan.FromMinutes(30));
        var routeContext = RouteContext.FromState(null);
        var injectionQueue = new RecordingChatInjectionQueue();
        var pipeline = new TelegramInboundPipeline(
            new RouteResolver(new RouteTagParser()),
            replyMap,
            () => routeContext,
            injectionQueue,
            route => routeContext = RouteContext.FromState(route));

        var firstHandled = await pipeline.HandleAsync(
            new TelegramInboundMessage(UpdateId: 1, MessageId: 2, ReplyToMessageId: null, ChatId: 42, IsPrivateChat: true, Text: "/p hello"));
        var secondHandled = await pipeline.HandleAsync(
            new TelegramInboundMessage(UpdateId: 2, MessageId: 3, ReplyToMessageId: null, ChatId: 42, IsPrivateChat: true, Text: "follow up"));

        Assert.True(firstHandled);
        Assert.True(secondHandled);
        Assert.Equal(2, injectionQueue.Messages.Count);
        Assert.Equal(ChatRoute.Party(), injectionQueue.Messages[0].Route);
        Assert.Equal("hello", injectionQueue.Messages[0].Message);
        Assert.Equal(ChatRoute.Party(), injectionQueue.Messages[1].Route);
        Assert.Equal("follow up", injectionQueue.Messages[1].Message);
    }

    [Fact]
    public async Task MissingRouteRejectsMessageWithoutInjection()
    {
        var replyMap = new TelegramReplyMap(100, TimeSpan.FromMinutes(30));
        var injectionQueue = new RecordingChatInjectionQueue();
        var notifications = new List<string>();
        var pipeline = new TelegramInboundPipeline(
            new RouteResolver(new RouteTagParser()),
            replyMap,
            () => RouteContext.FromState(null),
            injectionQueue,
            notifyFailureAsync: (message, _) =>
            {
                notifications.Add(message);
                return Task.CompletedTask;
            });

        var handled = await pipeline.HandleAsync(
            new TelegramInboundMessage(UpdateId: 1, MessageId: 2, ReplyToMessageId: null, ChatId: 42, IsPrivateChat: true, Text: "hello"));

        Assert.False(handled);
        Assert.Empty(injectionQueue.Messages);
        Assert.Equal(["Route could not be resolved."], notifications);
    }

    [Fact]
    public async Task InjectionFailureIsDroppedWithoutUpdatingRouteState()
    {
        var routeContext = RouteContext.FromState(null);
        var pipeline = new TelegramInboundPipeline(
            new RouteResolver(new RouteTagParser()),
            new TelegramReplyMap(100, TimeSpan.FromMinutes(30)),
            () => routeContext,
            new ThrowingChatInjectionQueue(),
            route => routeContext = RouteContext.FromState(route));

        var handled = await pipeline.HandleAsync(
            new TelegramInboundMessage(UpdateId: 1, MessageId: 2, ReplyToMessageId: null, ChatId: 42, IsPrivateChat: true, Text: "/p hello"));

        Assert.False(handled);
        Assert.Null(routeContext.LastActiveRoute);
        Assert.Null(routeContext.LastTellRoute);
    }

    private sealed class RecordingChatInjectionQueue : IChatInjectionQueue
    {
        public List<InjectedMessage> Messages { get; } = [];

        public Task EnqueueAsync(ChatRoute route, string message, CancellationToken cancellationToken = default)
        {
            this.Messages.Add(new InjectedMessage(route, message));
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingChatInjectionQueue : IChatInjectionQueue
    {
        public Task EnqueueAsync(ChatRoute route, string message, CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("boom");
        }
    }

    private sealed record InjectedMessage(ChatRoute Route, string Message);
}
