namespace FFXIVTelegram.Tests.TestDoubles;

using System.Reflection;
using Dalamud.Game.Command;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Configuration;
using Dalamud.Interface;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

internal class DalamudPluginInterfaceTestDouble : DispatchProxy
{
    public IPluginConfiguration? PluginConfiguration { get; set; }

    public IUiBuilder? UiBuilder { get; set; }

    public IPluginConfiguration? SavedConfiguration { get; private set; }

    public bool ThrowOnSave { get; set; }

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        if (targetMethod == null)
        {
            throw new ArgumentNullException(nameof(targetMethod));
        }

        switch (targetMethod.Name)
        {
            case "GetPluginConfig":
                return this.PluginConfiguration;
            case "SavePluginConfig":
                if (this.ThrowOnSave)
                {
                    throw new InvalidOperationException("save failed");
                }

                this.SavedConfiguration = (IPluginConfiguration?)args?[0];
                return null;
            case "get_UiBuilder":
                return this.UiBuilder;
            default:
                throw new NotSupportedException(targetMethod.Name);
        }
    }

    public static IDalamudPluginInterface Create(
        IPluginConfiguration? pluginConfiguration,
        out DalamudPluginInterfaceTestDouble proxy,
        out UiBuilderTestDouble uiBuilder)
    {
        var pluginInterface = DispatchProxy.Create<IDalamudPluginInterface, DalamudPluginInterfaceTestDouble>();
        proxy = (DalamudPluginInterfaceTestDouble)(object)pluginInterface;
        proxy.PluginConfiguration = pluginConfiguration;
        proxy.UiBuilder = UiBuilderTestDouble.Create(out uiBuilder);
        return pluginInterface;
    }
}

internal class UiBuilderTestDouble : DispatchProxy
{
    private Action? draw;

    private Action? openConfigUi;

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        if (targetMethod == null)
        {
            throw new ArgumentNullException(nameof(targetMethod));
        }

        switch (targetMethod.Name)
        {
            case "add_Draw":
                this.draw += (Action?)args?[0];
                return null;
            case "remove_Draw":
                this.draw -= (Action?)args?[0];
                return null;
            case "add_OpenConfigUi":
                this.openConfigUi += (Action?)args?[0];
                return null;
            case "remove_OpenConfigUi":
                this.openConfigUi -= (Action?)args?[0];
                return null;
            default:
                throw new NotSupportedException(targetMethod.Name);
        }
    }

    public static IUiBuilder Create(out UiBuilderTestDouble proxy)
    {
        var uiBuilder = DispatchProxy.Create<IUiBuilder, UiBuilderTestDouble>();
        proxy = (UiBuilderTestDouble)(object)uiBuilder;
        return uiBuilder;
    }

    public void RaiseDraw()
    {
        this.draw?.Invoke();
    }

    public void RaiseOpenConfigUi()
    {
        this.openConfigUi?.Invoke();
    }
}

internal class ChatGuiTestDouble : DispatchProxy
{
    private IChatGui.OnMessageDelegate? chatMessage;

    public int ChatMessageSubscriberCount => this.chatMessage?.GetInvocationList().Length ?? 0;

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        if (targetMethod == null)
        {
            throw new ArgumentNullException(nameof(targetMethod));
        }

        switch (targetMethod.Name)
        {
            case "add_ChatMessage":
                this.chatMessage += (IChatGui.OnMessageDelegate?)args?[0];
                return null;
            case "remove_ChatMessage":
                this.chatMessage -= (IChatGui.OnMessageDelegate?)args?[0];
                return null;
            default:
                throw new NotSupportedException(targetMethod.Name);
        }
    }

    public static IChatGui Create(out ChatGuiTestDouble proxy)
    {
        var chatGui = DispatchProxy.Create<IChatGui, ChatGuiTestDouble>();
        proxy = (ChatGuiTestDouble)(object)chatGui;
        return chatGui;
    }

    public void RaiseChatMessage(XivChatType type, int timestamp, SeString sender, SeString message, ref bool isHandled)
    {
        this.chatMessage?.Invoke(type, timestamp, ref sender, ref message, ref isHandled);
    }
}

internal class CommandManagerTestDouble : DispatchProxy
{
    private readonly Dictionary<string, CommandInfo> handlers = new(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, CommandInfo> Handlers => this.handlers;

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        if (targetMethod == null)
        {
            throw new ArgumentNullException(nameof(targetMethod));
        }

        switch (targetMethod.Name)
        {
            case "AddHandler":
                var command = (string)args![0]!;
                var info = (CommandInfo)args[1]!;
                this.handlers[command] = info;
                return true;
            case "RemoveHandler":
                return this.handlers.Remove((string)args![0]!);
            default:
                throw new NotSupportedException(targetMethod.Name);
        }
    }

    public static ICommandManager Create(out CommandManagerTestDouble proxy)
    {
        var commandManager = DispatchProxy.Create<ICommandManager, CommandManagerTestDouble>();
        proxy = (CommandManagerTestDouble)(object)commandManager;
        return commandManager;
    }
}
