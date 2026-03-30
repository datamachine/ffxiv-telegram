namespace FFXIVTelegram.Tests.Interop;

using System;
using System.Linq;
using System.Reflection;
using Dalamud.Plugin.Services;
using FFXIVTelegram.Interop;
using Xunit;

public sealed class XivChatGameChatExecutorTests
{
    [Fact]
    public void ConstructorRequiresGameGuiForUiModuleResolution()
    {
        var constructor = Assert.Single(typeof(XivChatGameChatExecutor).GetConstructors(BindingFlags.Instance | BindingFlags.Public));
        var parameterTypes = constructor.GetParameters().Select(parameter => parameter.ParameterType).ToArray();

        Assert.Equal([typeof(IGameGui)], parameterTypes);
    }

    [Fact]
    public void DoesNotDependOnSignatureScannedNativeFields()
    {
        var signatureAttributes = typeof(XivChatGameChatExecutor)
            .GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
            .SelectMany(field => field.GetCustomAttributesData())
            .Where(attribute => string.Equals(attribute.AttributeType.FullName, "Dalamud.Utility.Signatures.SignatureAttribute", StringComparison.Ordinal))
            .ToArray();

        Assert.Empty(signatureAttributes);
    }

    [Fact]
    public void ImplementsDisposableToReleaseNativeHooks()
    {
        Assert.False(typeof(IDisposable).IsAssignableFrom(typeof(XivChatGameChatExecutor)));
    }
}
