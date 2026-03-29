namespace FFXIVTelegram.Tests.Interop;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FFXIVTelegram.Chat;
using FFXIVTelegram.Interop;
using Xunit;

public sealed class ChatInjectionServiceTests
{
    [Fact]
    public async Task EnqueueAsyncDispatchesFormattedRouteCommands()
    {
        var dispatcher = new RecordingFrameworkDispatcher();
        var executor = new RecordingGameChatExecutor();
        var service = new ChatInjectionService(dispatcher, executor, TimeSpan.Zero);

        await service.EnqueueAsync(ChatRoute.Party(), "first");
        await service.EnqueueAsync(ChatRoute.FreeCompany(), "second");
        await service.EnqueueAsync(ChatRoute.Tell("Alice Example"), "third");

        Assert.Equal(3, dispatcher.InvocationCount);
        Assert.Equal(["/p first", "/fc second", "/tell Alice Example third"], executor.Messages);
    }

    [Fact]
    public async Task EnqueueRawAsyncBypassesRouteFormatting()
    {
        var dispatcher = new RecordingFrameworkDispatcher();
        var executor = new RecordingGameChatExecutor();
        var service = new ChatInjectionService(dispatcher, executor, TimeSpan.Zero);

        await service.EnqueueRawAsync("hello");

        Assert.Equal(["hello"], executor.Messages);
    }

    [Fact]
    public async Task EnqueueAsyncNormalizesLineBreaksBeforeDispatch()
    {
        var dispatcher = new RecordingFrameworkDispatcher();
        var executor = new RecordingGameChatExecutor();
        var service = new ChatInjectionService(dispatcher, executor, TimeSpan.Zero);

        await service.EnqueueAsync(ChatRoute.Party(), "first\r\nsecond\nthird\rfourth");

        Assert.Equal(["/p first second third fourth"], executor.Messages);
    }

    [Fact]
    public async Task EnqueueAsyncRejectsFormattedInputThatExceedsSafeUtf8Limit()
    {
        var dispatcher = new RecordingFrameworkDispatcher();
        var executor = new RecordingGameChatExecutor();
        var service = new ChatInjectionService(dispatcher, executor, TimeSpan.Zero);

        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.EnqueueAsync(ChatRoute.Party(), new string('a', 498)));

        Assert.Contains("safe limits", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(executor.Messages);
        Assert.Equal(0, dispatcher.InvocationCount);
    }

    [Fact]
    public async Task EnqueueRawAsyncRejectsInputThatExceedsSafeUtf8Limit()
    {
        var dispatcher = new RecordingFrameworkDispatcher();
        var executor = new RecordingGameChatExecutor();
        var service = new ChatInjectionService(dispatcher, executor, TimeSpan.Zero);

        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.EnqueueRawAsync(new string('a', 501)));

        Assert.Contains("safe limits", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(executor.Messages);
        Assert.Equal(0, dispatcher.InvocationCount);
    }

    [Fact]
    public async Task EnforcesSingleMessageAtATimeThroughFrameworkDispatcher()
    {
        var dispatcher = new RecordingFrameworkDispatcher();
        var executor = new RecordingGameChatExecutor();
        var delays = new List<TimeSpan>();
        var now = DateTimeOffset.UtcNow;
        var service = new ChatInjectionService(
            dispatcher,
            executor,
            TimeSpan.FromMilliseconds(500),
            (delay, _) =>
            {
                delays.Add(delay);
                return Task.CompletedTask;
            },
            () => now);

        await Task.WhenAll(
            service.EnqueueAsync(ChatRoute.Party(), "first"),
            service.EnqueueAsync(ChatRoute.Party(), "second"));

        Assert.Equal(2, executor.Messages.Count);
        Assert.Equal(2, dispatcher.InvocationCount);
        Assert.Single(delays);
        Assert.Equal(TimeSpan.FromMilliseconds(500), delays[0]);
    }

    [Fact]
    public async Task SkipsDelayWhenPreviousInjectionIsAlreadyOldEnough()
    {
        var dispatcher = new RecordingFrameworkDispatcher();
        var executor = new RecordingGameChatExecutor();
        var delays = new List<TimeSpan>();
        var now = DateTimeOffset.UtcNow;
        var service = new ChatInjectionService(
            dispatcher,
            executor,
            TimeSpan.FromMilliseconds(500),
            (delay, _) =>
            {
                delays.Add(delay);
                return Task.CompletedTask;
            },
            () => now);

        await service.EnqueueAsync(ChatRoute.Party(), "first");
        now = now.AddMilliseconds(600);
        await service.EnqueueAsync(ChatRoute.Party(), "second");

        Assert.Empty(delays);
        Assert.Equal(["/p first", "/p second"], executor.Messages);
    }

    private sealed class RecordingFrameworkDispatcher : IFrameworkDispatcher
    {
        public int InvocationCount { get; private set; }

        public Task RunAsync(Action action, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            this.InvocationCount++;
            action();
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingGameChatExecutor : IGameChatExecutor
    {
        public List<string> Messages { get; } = [];

        public void Execute(string inputText)
        {
            this.Messages.Add(inputText);
        }
    }
}
