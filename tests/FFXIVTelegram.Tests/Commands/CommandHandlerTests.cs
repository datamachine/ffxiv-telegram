namespace FFXIVTelegram.Tests.Commands;

using System;
using System.Threading;
using System.Threading.Tasks;
using FFXIVTelegram.Commands;
using FFXIVTelegram.Interop;
using FFXIVTelegram.Tests.TestDoubles;
using Xunit;

public sealed class CommandHandlerTests
{
    [Fact]
    public void RegistersAndRemovesPluginCommand()
    {
        var commandManager = CommandManagerTestDouble.Create(out var proxy);
        var handler = new CommandHandler(commandManager, this.CreateInjectionService(out _));

        Assert.Contains(PluginConstants.CommandName, proxy.Handlers.Keys);

        handler.Dispose();

        Assert.DoesNotContain(PluginConstants.CommandName, proxy.Handlers.Keys);
    }

    [Fact]
    public async Task TestInjectSubcommandQueuesRawMessage()
    {
        var commandManager = CommandManagerTestDouble.Create(out var proxy);
        var injectionService = this.CreateInjectionService(out var executor);
        using var handler = new CommandHandler(commandManager, injectionService);

        proxy.Handlers[PluginConstants.CommandName].Handler(PluginConstants.CommandName, "testinject hello world");
        await Task.Yield();

        Assert.Equal(["hello world"], executor.Messages);
    }

    [Fact]
    public async Task TestInjectSubcommandReportsInjectionFailure()
    {
        var commandManager = CommandManagerTestDouble.Create(out var proxy);
        var failures = new List<string>();
        using var handler = new CommandHandler(
            commandManager,
            new ThrowingChatInjectionService(),
            failures.Add);

        proxy.Handlers[PluginConstants.CommandName].Handler(PluginConstants.CommandName, "testinject hello world");
        await Task.Yield();

        Assert.Equal(["Test injection failed: boom"], failures);
    }

    private ChatInjectionService CreateInjectionService(out RecordingGameChatExecutor executor)
    {
        executor = new RecordingGameChatExecutor();
        return new ChatInjectionService(new ImmediateFrameworkDispatcher(), executor, TimeSpan.Zero);
    }

    private sealed class ImmediateFrameworkDispatcher : IFrameworkDispatcher
    {
        public Task RunAsync(Action action, CancellationToken cancellationToken = default)
        {
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

    private sealed class ThrowingChatInjectionService : IChatInjectionCommandTarget
    {
        public Task EnqueueRawAsync(string inputText, CancellationToken cancellationToken = default)
        {
            return Task.FromException(new InvalidOperationException("boom"));
        }
    }
}
