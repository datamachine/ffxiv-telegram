namespace FFXIVTelegram.Tests.TestDoubles;

using System.Reflection;
using Dalamud.Configuration;
using Dalamud.Interface;
using Dalamud.Plugin;

internal class DalamudPluginInterfaceTestDouble : DispatchProxy
{
    public IPluginConfiguration? PluginConfiguration { get; set; }

    public IUiBuilder? UiBuilder { get; set; }

    public IPluginConfiguration? SavedConfiguration { get; private set; }

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
