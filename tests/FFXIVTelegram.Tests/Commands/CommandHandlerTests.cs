namespace FFXIVTelegram.Tests.Commands;

using System;
using FFXIVTelegram.Commands;
using FFXIVTelegram.Tests.TestDoubles;
using Xunit;

public sealed class CommandHandlerTests
{
    [Fact]
    public void RegistersAndRemovesPluginCommand()
    {
        var commandManager = CommandManagerTestDouble.Create(out var proxy);
        var handler = new CommandHandler(commandManager, () => { });

        Assert.Contains(PluginConstants.CommandName, proxy.Handlers.Keys);

        handler.Dispose();

        Assert.DoesNotContain(PluginConstants.CommandName, proxy.Handlers.Keys);
    }

    [Fact]
    public void InvokingCommandWithoutArgumentsOpensSettings()
    {
        var commandManager = CommandManagerTestDouble.Create(out var proxy);
        var openCount = 0;
        using var handler = new CommandHandler(commandManager, () => openCount++);

        proxy.Handlers[PluginConstants.CommandName].Handler(PluginConstants.CommandName, string.Empty);

        Assert.Equal(1, openCount);
    }

    [Fact]
    public void InvokingCommandWithArgumentsStillOpensSettings()
    {
        var commandManager = CommandManagerTestDouble.Create(out var proxy);
        var openCount = 0;
        using var handler = new CommandHandler(commandManager, () => openCount++);

        proxy.Handlers[PluginConstants.CommandName].Handler(PluginConstants.CommandName, "ignored arguments");

        Assert.Equal(1, openCount);
    }
}
