# FFXIV Telegram MVP Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the first working version of the `FFXIV Telegram` Dalamud plugin: config UI, Telegram `/start` authorization, game-to-Telegram forwarding for `Tell`/`Party`/`Free Company`, and Telegram-to-game injection with tag, reply, and last-route resolution.

**Architecture:** Use one plugin assembly with small focused services. Keep Telegram API usage, route resolution, game chat monitoring, and native chat injection isolated behind explicit interfaces so pure logic stays unit-testable and the native interop path remains quarantined. Follow the local `../DalamudPlugins/plugins/XIVChat/XIVChatSource` patterns for `ProcessChatBox`, `GetUiModule()`, and `Framework.Update` handoff, but do not mirror the decompiled structure blindly.

**Tech Stack:** .NET 10, `Dalamud.NET.Sdk` API 14+, `Telegram.Bot`, `Dalamud.Bindings.ImGui`, `FFXIVClientStructs`, xUnit, local reference corpus in `../DalamudPlugins/`

---

## Planning Notes

- This repo is greenfield. The first implementation task must create the solution, plugin project, manifest, and test project.
- This session is not in a separate worktree. If implementation starts later, either create one first or commit after every completed task to preserve checkpoints.
- Use the approved spec at `docs/superpowers/specs/2026-03-29-ffxiv-telegram-mvp-design.md` as the scope boundary.

## Reference Checklist

Inspect these local references before implementing the matching task:

- `../DalamudPlugins/plugins/XIVChat/XIVChatSource/XIVChatPlugin/GameFunctions.cs`
- `../DalamudPlugins/plugins/XIVChat/XIVChatSource/XIVChatPlugin/Plugin.cs`
- `../DalamudPlugins/plugins/XIVChat/XIVChatSource/XIVChatPlugin/PluginUi.cs`

Focus points:

- `GameFunctions.ProcessChatBox(string message)`
- `((Framework)Framework.Instance()).GetUiModule()`
- `Framework.Update += ...` / `Framework.Update -= ...`

## File Structure

Create this structure and keep responsibilities narrow:

- `.gitignore`
  Repository ignores including `.idea/`, build outputs, and local plugin artifacts.
- `FFXIVTelegram.sln`
  Root solution file for plugin and tests.
- `Directory.Build.props`
  Shared .NET settings such as nullable, implicit usings, warning level, and deterministic builds.
- `src/FFXIVTelegram/FFXIVTelegram.csproj`
  Plugin project using `Dalamud.NET.Sdk/14.x`.
- `src/FFXIVTelegram/FFXIVTelegram.json`
  Dalamud plugin manifest metadata.
- `src/FFXIVTelegram/PluginConstants.cs`
  Shared constants for plugin name, command name, and manifest-friendly identifiers.
- `src/FFXIVTelegram/FfxivTelegramPlugin.cs`
  Composition root and lifecycle wiring.
- `src/FFXIVTelegram/Commands/CommandHandler.cs`
  Slash command registration and `/ffxivtelegram testinject` dispatch.
- `src/FFXIVTelegram/Configuration/FfxivTelegramConfiguration.cs`
  Persisted settings and channel toggles.
- `src/FFXIVTelegram/Configuration/ConfigurationStore.cs`
  Load/save wrapper over `IDalamudPluginInterface`.
- `src/FFXIVTelegram/Telegram/TelegramConnectionState.cs`
  Enum or model for `NotConfigured`, `WaitingForStart`, `Connected`, `Error`.
- `src/FFXIVTelegram/Telegram/ITelegramClientAdapter.cs`
  Narrow abstraction over `Telegram.Bot` for polling and sends.
- `src/FFXIVTelegram/Telegram/TelegramBridgeService.cs`
  Polling loop, `/start` authorization, inbound update filtering, outbound send logic.
- `src/FFXIVTelegram/Chat/ChatRoute.cs`
  Route model including `FreeCompany`, `Party`, `Tell(target)`.
- `src/FFXIVTelegram/Chat/RouteTagParser.cs`
  Parses `/fc`, `/p`, `/r`.
- `src/FFXIVTelegram/Chat/RouteResolver.cs`
  Tag > reply > last-active precedence.
- `src/FFXIVTelegram/Chat/TelegramReplyMap.cs`
  Bounded in-memory message id to route map.
- `src/FFXIVTelegram/Chat/ForwardedChatMessage.cs`
  Normalized outbound message contract from game chat to Telegram send path.
- `src/FFXIVTelegram/Chat/GameChatFormatter.cs`
  Pure formatter and sanitization for supported `XivChatType` events.
- `src/FFXIVTelegram/Chat/GameChatMonitor.cs`
  `IChatGui.ChatMessage` subscription and orchestration into outbound bridge state.
- `src/FFXIVTelegram/Interop/IFrameworkDispatcher.cs`
  Wrapper for main-thread handoff.
- `src/FFXIVTelegram/Interop/FrameworkDispatcher.cs`
  `IFramework.Update` backed dispatcher.
- `src/FFXIVTelegram/Interop/IGameChatExecutor.cs`
  Native injection boundary.
- `src/FFXIVTelegram/Interop/XivChatGameChatExecutor.cs`
  `FFXIVClientStructs` + `GetUiModule()` + unmanaged payload handling.
- `src/FFXIVTelegram/Interop/ChatPayload.cs`
  Unmanaged payload shape modeled after the local XIVChat reference.
- `src/FFXIVTelegram/Interop/ChatInjectionService.cs`
  Queue, pacing, sanitization, and dispatch into `IGameChatExecutor`.
- `src/FFXIVTelegram/UI/ConfigWindow.cs`
  Dalamud Bindings ImGui configuration window.
- `src/FFXIVTelegram/UI/UiController.cs`
  Window registration, open/close wiring, and plugin UI entrypoints.
- `tests/FFXIVTelegram.Tests/FFXIVTelegram.Tests.csproj`
  Test project.
- `tests/FFXIVTelegram.Tests/Bootstrap/PluginConstantsTests.cs`
  Smoke tests for naming and command constants.
- `tests/FFXIVTelegram.Tests/Configuration/FfxivTelegramConfigurationTests.cs`
  Default settings and state transition tests.
- `tests/FFXIVTelegram.Tests/Chat/RouteResolverTests.cs`
  Route precedence coverage.
- `tests/FFXIVTelegram.Tests/Chat/TelegramReplyMapTests.cs`
  Capacity and expiration behavior.
- `tests/FFXIVTelegram.Tests/Chat/GameChatFormatterTests.cs`
  Formatting and supported-channel filtering.
- `tests/FFXIVTelegram.Tests/Telegram/TelegramBridgeServiceTests.cs`
  `/start`, auth filtering, and outbound send behavior.
- `tests/FFXIVTelegram.Tests/Interop/ChatInjectionServiceTests.cs`
  Queue spacing, dispatch, and failure handling.
- `tests/FFXIVTelegram.Tests/Integration/InboundPipelineTests.cs`
  Inbound Telegram text flowing through route resolution into injection submission.

### Task 1: Scaffold the Solution and Baseline Constants

**Files:**
- Create: `.gitignore`
- Create: `Directory.Build.props`
- Create: `FFXIVTelegram.sln`
- Create: `src/FFXIVTelegram/FFXIVTelegram.csproj`
- Create: `src/FFXIVTelegram/FFXIVTelegram.json`
- Create: `src/FFXIVTelegram/PluginConstants.cs`
- Create: `src/FFXIVTelegram/FfxivTelegramPlugin.cs`
- Create: `tests/FFXIVTelegram.Tests/FFXIVTelegram.Tests.csproj`
- Test: `tests/FFXIVTelegram.Tests/Bootstrap/PluginConstantsTests.cs`

- [ ] **Step 1: Write the failing smoke test for plugin identity**

```csharp
public sealed class PluginConstantsTests
{
    [Fact]
    public void UsesApprovedPluginNameAndCommand()
    {
        Assert.Equal("FFXIV Telegram", PluginConstants.PluginName);
        Assert.Equal("/ffxivtelegram", PluginConstants.CommandName);
    }
}
```

- [ ] **Step 2: Run the targeted test to verify the repo is not scaffolded yet**

Run: `dotnet test tests/FFXIVTelegram.Tests/FFXIVTelegram.Tests.csproj --filter FullyQualifiedName~PluginConstantsTests -v minimal`
Expected: FAIL because the solution or `PluginConstants` type does not exist yet

- [ ] **Step 3: Create the solution, plugin project, manifest, and minimal implementation**

Model the test-project reference setup after `../TextToTalk/src/TextToTalk.Tests/TextToTalk.Tests.csproj` so `tests/FFXIVTelegram.Tests/FFXIVTelegram.Tests.csproj` can resolve `Dalamud.dll` and `FFXIVClientStructs.dll` from `$(DALAMUD_HOME)` or the platform-specific default dev path.

```xml
<Project Sdk="Dalamud.NET.Sdk/14.0.2">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <AllowUnsafeBlocks>true</AllowUnsafeBlocks>
    <RestorePackagesWithLockFile>true</RestorePackagesWithLockFile>
  </PropertyGroup>
</Project>
```

```csharp
internal static class PluginConstants
{
    internal const string PluginName = "FFXIV Telegram";
    internal const string CommandName = "/ffxivtelegram";
}
```

- [ ] **Step 4: Restore, build, and rerun the smoke test**

Run: `dotnet build FFXIVTelegram.sln -v minimal`
Expected: PASS

Run: `dotnet test tests/FFXIVTelegram.Tests/FFXIVTelegram.Tests.csproj --filter FullyQualifiedName~PluginConstantsTests -v minimal`
Expected: PASS

- [ ] **Step 5: Commit the scaffold**

```bash
git add .gitignore Directory.Build.props FFXIVTelegram.sln src/FFXIVTelegram tests/FFXIVTelegram.Tests
git commit -m "chore: scaffold FFXIV Telegram plugin and tests"
```

### Task 2: Add Configuration Persistence and the UI Shell

**Files:**
- Modify: `src/FFXIVTelegram/FfxivTelegramPlugin.cs`
- Create: `src/FFXIVTelegram/Configuration/FfxivTelegramConfiguration.cs`
- Create: `src/FFXIVTelegram/Configuration/ConfigurationStore.cs`
- Create: `src/FFXIVTelegram/Telegram/TelegramConnectionState.cs`
- Create: `src/FFXIVTelegram/UI/ConfigWindow.cs`
- Create: `src/FFXIVTelegram/UI/UiController.cs`
- Test: `tests/FFXIVTelegram.Tests/Configuration/FfxivTelegramConfigurationTests.cs`

- [ ] **Step 1: Write the failing configuration defaults test**

```csharp
[Fact]
public void DefaultsEnableOnlyApprovedChannels()
{
    var config = new FfxivTelegramConfiguration();

    Assert.True(config.EnableTellForwarding);
    Assert.True(config.EnablePartyForwarding);
    Assert.True(config.EnableFreeCompanyForwarding);
    Assert.Null(config.AuthorizedChatId);
}
```

- [ ] **Step 2: Run the configuration test and confirm the missing-type failure**

Run: `dotnet test tests/FFXIVTelegram.Tests/FFXIVTelegram.Tests.csproj --filter FullyQualifiedName~FfxivTelegramConfigurationTests -v minimal`
Expected: FAIL because `FfxivTelegramConfiguration` does not exist

- [ ] **Step 3: Implement config model, persistence wrapper, status enum, and a minimal config window**

```csharp
public sealed class FfxivTelegramConfiguration
{
    public string TelegramBotToken { get; set; } = string.Empty;
    public long? AuthorizedChatId { get; set; }
    public bool EnableTellForwarding { get; set; } = true;
    public bool EnablePartyForwarding { get; set; } = true;
    public bool EnableFreeCompanyForwarding { get; set; } = true;
}
```

```csharp
public enum TelegramConnectionState
{
    NotConfigured,
    WaitingForStart,
    Connected,
    Error,
}
```

- [ ] **Step 4: Build and rerun the focused tests**

Run: `dotnet test tests/FFXIVTelegram.Tests/FFXIVTelegram.Tests.csproj --filter FullyQualifiedName~FfxivTelegramConfigurationTests -v minimal`
Expected: PASS

Run: `dotnet build FFXIVTelegram.sln -v minimal`
Expected: PASS with the config window and UI controller compiling

- [ ] **Step 5: Commit the configuration and UI shell**

```bash
git add src/FFXIVTelegram/Configuration src/FFXIVTelegram/Telegram/TelegramConnectionState.cs src/FFXIVTelegram/UI src/FFXIVTelegram/FfxivTelegramPlugin.cs tests/FFXIVTelegram.Tests/Configuration
git commit -m "feat: add configuration persistence and UI shell"
```

### Task 3: Implement Route Parsing, Reply Context, and Last-Active Resolution

**Files:**
- Create: `src/FFXIVTelegram/Chat/ChatRoute.cs`
- Create: `src/FFXIVTelegram/Chat/RouteTagParser.cs`
- Create: `src/FFXIVTelegram/Chat/RouteResolver.cs`
- Create: `src/FFXIVTelegram/Chat/TelegramReplyMap.cs`
- Test: `tests/FFXIVTelegram.Tests/Chat/RouteResolverTests.cs`
- Test: `tests/FFXIVTelegram.Tests/Chat/TelegramReplyMapTests.cs`

- [ ] **Step 1: Write the failing route-precedence and reply-map tests**

```csharp
[Fact]
public void ExplicitTagWinsOverReplyAndLastActive()
{
    var resolver = new RouteResolver(new RouteTagParser());
    var result = resolver.Resolve("/fc hello", replyRoute: ChatRoute.Party(), lastActiveRoute: ChatRoute.Tell("Alice"));

    Assert.Equal(ChatRoute.FreeCompany(), result.Route);
    Assert.Equal("hello", result.MessageText);
}
```

```csharp
[Fact]
public void ReplyMapReturnsStoredTellTarget()
{
    var map = new TelegramReplyMap(capacity: 100, maxAge: TimeSpan.FromMinutes(30));
    map.Store(12345, ChatRoute.Tell("Alice"));

    Assert.True(map.TryGetRoute(12345, out var route));
    Assert.Equal(ChatRoute.Tell("Alice"), route);
}
```

- [ ] **Step 2: Run the chat tests and confirm they fail**

Run: `dotnet test tests/FFXIVTelegram.Tests/FFXIVTelegram.Tests.csproj --filter "FullyQualifiedName~RouteResolverTests|FullyQualifiedName~TelegramReplyMapTests" -v minimal`
Expected: FAIL because the route model and services do not exist

- [ ] **Step 3: Implement the route model and pure resolution logic**

```csharp
public abstract record ChatRoute
{
    public sealed record FreeCompanyRoute : ChatRoute;
    public sealed record PartyRoute : ChatRoute;
    public sealed record TellRoute(string Target) : ChatRoute;
}
```

```csharp
public RouteResolution Resolve(string text, ChatRoute? replyRoute, ChatRoute? lastActiveRoute)
{
    if (_tagParser.TryParse(text, out var tagged)) return tagged;
    if (replyRoute is not null) return RouteResolution.Success(replyRoute, text);
    if (lastActiveRoute is not null) return RouteResolution.Success(lastActiveRoute, text);
    return RouteResolution.Failure("Route could not be resolved.");
}
```

- [ ] **Step 4: Run the route tests again**

Run: `dotnet test tests/FFXIVTelegram.Tests/FFXIVTelegram.Tests.csproj --filter "FullyQualifiedName~RouteResolverTests|FullyQualifiedName~TelegramReplyMapTests" -v minimal`
Expected: PASS

- [ ] **Step 5: Commit the routing core**

```bash
git add src/FFXIVTelegram/Chat tests/FFXIVTelegram.Tests/Chat
git commit -m "feat: add route resolver and reply context mapping"
```

### Task 4: Build Telegram Authorization, Polling, and Outbound Send Behavior

**Files:**
- Create: `src/FFXIVTelegram/Telegram/ITelegramClientAdapter.cs`
- Create: `src/FFXIVTelegram/Telegram/TelegramBridgeService.cs`
- Modify: `src/FFXIVTelegram/Telegram/TelegramConnectionState.cs`
- Modify: `src/FFXIVTelegram/Configuration/FfxivTelegramConfiguration.cs`
- Test: `tests/FFXIVTelegram.Tests/Telegram/TelegramBridgeServiceTests.cs`

- [ ] **Step 1: Write the failing `/start` authorization and private-chat filtering tests**

```csharp
[Fact]
public async Task FirstPrivateStartClaimsAuthorizedChat()
{
    var service = CreateService();
    await service.HandleIncomingTextAsync(chatId: 42, isPrivateChat: true, text: "/start");

    Assert.Equal(42, service.Configuration.AuthorizedChatId);
    Assert.Equal(TelegramConnectionState.Connected, service.ConnectionState);
}
```

```csharp
[Fact]
public async Task IgnoresMessagesFromUnauthorizedChat()
{
    var service = CreateService(authorizedChatId: 42);
    var result = await service.HandleIncomingTextAsync(chatId: 99, isPrivateChat: true, text: "hello");

    Assert.Equal(TelegramInboundResult.IgnoredUnauthorizedChat, result);
}
```

- [ ] **Step 2: Run the Telegram service tests to confirm failure**

Run: `dotnet test tests/FFXIVTelegram.Tests/FFXIVTelegram.Tests.csproj --filter FullyQualifiedName~TelegramBridgeServiceTests -v minimal`
Expected: FAIL because `TelegramBridgeService` and the adapter abstraction do not exist

- [ ] **Step 3: Implement the polling/auth service behind a narrow adapter**

Also update `src/FFXIVTelegram/FFXIVTelegram.csproj` to add a pinned `Telegram.Bot` package reference. Do not use floating or wildcard versions; commit the lockfile generated by restore.

```csharp
public interface ITelegramClientAdapter
{
    Task<IReadOnlyList<TelegramUpdate>> GetUpdatesAsync(long offset, CancellationToken cancellationToken);
    Task<TelegramSendResult> SendTextAsync(long chatId, string text, CancellationToken cancellationToken);
}
```

```csharp
if (!isPrivateChat) return TelegramInboundResult.IgnoredUnsupportedChatType;
if (text == "/start" && _configuration.AuthorizedChatId is null)
{
    _configuration.AuthorizedChatId = chatId;
    _store.Save();
    return TelegramInboundResult.Authorized;
}
```

- [ ] **Step 4: Run focused tests and a full build**

Run: `dotnet test tests/FFXIVTelegram.Tests/FFXIVTelegram.Tests.csproj --filter FullyQualifiedName~TelegramBridgeServiceTests -v minimal`
Expected: PASS

Run: `dotnet build FFXIVTelegram.sln -v minimal`
Expected: PASS

- [ ] **Step 5: Commit the Telegram bridge foundation**

```bash
git add src/FFXIVTelegram/Telegram src/FFXIVTelegram/Configuration tests/FFXIVTelegram.Tests/Telegram
git commit -m "feat: add telegram authorization and polling service"
```

### Task 5: Add Game Chat Monitoring and Outbound Forwarding

**Files:**
- Create: `src/FFXIVTelegram/Chat/ForwardedChatMessage.cs`
- Create: `src/FFXIVTelegram/Chat/GameChatFormatter.cs`
- Create: `src/FFXIVTelegram/Chat/GameChatMonitor.cs`
- Modify: `src/FFXIVTelegram/FfxivTelegramPlugin.cs`
- Test: `tests/FFXIVTelegram.Tests/Chat/GameChatFormatterTests.cs`

- [ ] **Step 1: Write the failing formatter tests for supported channels**

```csharp
[Fact]
public void FormatsFreeCompanyMessageForTelegram()
{
    var result = GameChatFormatter.Format(XivChatType.FreeCompany, "Alice Example", "Hello!");

    Assert.NotNull(result);
    Assert.Equal("[FC] <Alice Example>: Hello!", result!.Text);
    Assert.Equal(ChatRoute.FreeCompany(), result.Route);
}
```

- [ ] **Step 2: Run the formatter test and verify it fails**

Run: `dotnet test tests/FFXIVTelegram.Tests/FFXIVTelegram.Tests.csproj --filter FullyQualifiedName~GameChatFormatterTests -v minimal`
Expected: FAIL because the formatter and normalized message type do not exist

- [ ] **Step 3: Implement formatting, filtering, and monitor orchestration**

```csharp
public static ForwardedChatMessage? Format(XivChatType type, string sender, string message)
{
    return type switch
    {
        XivChatType.TellIncoming => new("[Tell] <" + sender + ">: " + message, ChatRoute.Tell(sender)),
        XivChatType.FreeCompany => new("[FC] <" + sender + ">: " + message, ChatRoute.FreeCompany()),
        XivChatType.Party => new("[P] <" + sender + ">: " + message, ChatRoute.Party()),
        _ => null,
    };
}
```

Only write to `TelegramReplyMap` after a Telegram send succeeds, so reply inheritance never points at a message the user never received.

- [ ] **Step 4: Run the formatter tests and a build**

Run: `dotnet test tests/FFXIVTelegram.Tests/FFXIVTelegram.Tests.csproj --filter FullyQualifiedName~GameChatFormatterTests -v minimal`
Expected: PASS

Run: `dotnet build FFXIVTelegram.sln -v minimal`
Expected: PASS with `GameChatMonitor` wired but not yet responsible for native injection

- [ ] **Step 5: Commit the outbound game-chat path**

```bash
git add src/FFXIVTelegram/Chat src/FFXIVTelegram/FfxivTelegramPlugin.cs tests/FFXIVTelegram.Tests/Chat/GameChatFormatterTests.cs
git commit -m "feat: forward supported game chat to telegram"
```

### Task 6: Implement the Native Injection Boundary and Queueing

**Files:**
- Create: `src/FFXIVTelegram/Interop/IFrameworkDispatcher.cs`
- Create: `src/FFXIVTelegram/Interop/FrameworkDispatcher.cs`
- Create: `src/FFXIVTelegram/Interop/IGameChatExecutor.cs`
- Create: `src/FFXIVTelegram/Interop/ChatPayload.cs`
- Create: `src/FFXIVTelegram/Interop/XivChatGameChatExecutor.cs`
- Create: `src/FFXIVTelegram/Interop/ChatInjectionService.cs`
- Create: `src/FFXIVTelegram/Commands/CommandHandler.cs`
- Modify: `src/FFXIVTelegram/FfxivTelegramPlugin.cs`
- Test: `tests/FFXIVTelegram.Tests/Interop/ChatInjectionServiceTests.cs`

- [ ] **Step 1: Write the failing queue-spacing and dispatcher tests**

```csharp
[Fact]
public async Task EnforcesSingleMessageAtATimeThroughFrameworkDispatcher()
{
    var executor = new FakeGameChatExecutor();
    var dispatcher = new ImmediateFrameworkDispatcher();
    var service = new ChatInjectionService(dispatcher, executor, minimumDelay: TimeSpan.FromMilliseconds(500));

    await service.EnqueueAsync(ChatRoute.Party(), "first");
    await service.EnqueueAsync(ChatRoute.Party(), "second");

    Assert.Equal(2, executor.Messages.Count);
}
```

- [ ] **Step 2: Run the interop service test and confirm failure**

Run: `dotnet test tests/FFXIVTelegram.Tests/FFXIVTelegram.Tests.csproj --filter FullyQualifiedName~ChatInjectionServiceTests -v minimal`
Expected: FAIL because the dispatcher, queue, and executor boundary do not exist

- [ ] **Step 3: Implement the dispatcher, queue, and XIVChat-modeled executor**

```csharp
public interface IGameChatExecutor
{
    void Execute(ChatRoute route, string message);
}
```

```csharp
// Before writing this file, inspect:
// ../DalamudPlugins/plugins/XIVChat/XIVChatSource/XIVChatPlugin/GameFunctions.cs
// ../DalamudPlugins/plugins/XIVChat/XIVChatSource/XIVChatPlugin/Plugin.cs
```

Implement `XivChatGameChatExecutor` so it:

- obtains the UI module from framework state
- builds the unmanaged payload in one dedicated type
- executes only from the framework thread
- exposes no pointer details outside `Interop/`

- [ ] **Step 4: Run tests and perform the manual in-game injection proof**

Run: `dotnet test tests/FFXIVTelegram.Tests/FFXIVTelegram.Tests.csproj --filter FullyQualifiedName~ChatInjectionServiceTests -v minimal`
Expected: PASS

Run: `dotnet build FFXIVTelegram.sln -v minimal`
Expected: PASS

Manual check:
- Load the plugin in a local Dalamud dev environment
- Run `/ffxivtelegram testinject hello`
- Expected: the message is submitted through the native chat path without crashing the client

- [ ] **Step 5: Commit the injection subsystem**

```bash
git add src/FFXIVTelegram/Interop src/FFXIVTelegram/Commands src/FFXIVTelegram/FfxivTelegramPlugin.cs tests/FFXIVTelegram.Tests/Interop
git commit -m "feat: add native chat injection queue and test command"
```

### Task 7: Wire the Full Inbound Pipeline and Final Verification

**Files:**
- Modify: `src/FFXIVTelegram/FfxivTelegramPlugin.cs`
- Modify: `src/FFXIVTelegram/Telegram/TelegramBridgeService.cs`
- Modify: `src/FFXIVTelegram/Chat/RouteResolver.cs`
- Modify: `src/FFXIVTelegram/Chat/GameChatMonitor.cs`
- Modify: `src/FFXIVTelegram/Interop/ChatInjectionService.cs`
- Modify: `src/FFXIVTelegram/UI/ConfigWindow.cs`
- Test: `tests/FFXIVTelegram.Tests/Integration/InboundPipelineTests.cs`

- [ ] **Step 1: Write the failing inbound pipeline test**

```csharp
[Fact]
public async Task ReplyMessageUsesStoredRouteBeforeLastActive()
{
    var replyMap = new TelegramReplyMap(100, TimeSpan.FromMinutes(30));
    replyMap.Store(777, ChatRoute.Tell("Alice"));
    var pipeline = CreateInboundPipeline(replyMap);

    await pipeline.HandleAsync(text: "hello back", replyToMessageId: 777, lastActiveRoute: ChatRoute.Party());

    Assert.Equal(ChatRoute.Tell("Alice"), pipeline.Executor.Messages.Single().Route);
}
```

- [ ] **Step 2: Run the integration test to verify failure**

Run: `dotnet test tests/FFXIVTelegram.Tests/FFXIVTelegram.Tests.csproj --filter FullyQualifiedName~InboundPipelineTests -v minimal`
Expected: FAIL because the end-to-end inbound orchestration is not wired yet

- [ ] **Step 3: Compose the services and finish plugin lifecycle handling**

```csharp
_telegramBridge = new TelegramBridgeService(...);
_routeResolver = new RouteResolver(...);
_injectionService = new ChatInjectionService(...);
_gameChatMonitor = new GameChatMonitor(...);
```

Ensure disposal shuts down:

- Telegram polling
- framework dispatcher subscriptions
- chat subscriptions
- command registrations
- transient reply-map and queue state

- [ ] **Step 4: Run the full automated and manual verification suite**

Run: `dotnet test tests/FFXIVTelegram.Tests/FFXIVTelegram.Tests.csproj -v minimal`
Expected: PASS

Run: `dotnet build FFXIVTelegram.sln -v minimal`
Expected: PASS

Manual verification:
1. Enter bot token in the config window
2. Observe `Waiting for /start`
3. Send `/start` from a private chat and observe `Connected`
4. Receive a tell, party, and free company message and confirm Telegram forwarding
5. Reply to the forwarded tell in Telegram and confirm it routes back to the same tell target
6. Send `/p hello` and confirm party routing
7. Send untagged text after using party in game and confirm last-route fallback
8. Unload the plugin and confirm no lingering polling or framework-update handlers remain

- [ ] **Step 5: Commit the fully wired MVP**

```bash
git add src/FFXIVTelegram tests/FFXIVTelegram.Tests
git commit -m "feat: complete FFXIV Telegram MVP bridge"
```

## Final Verification Checklist

- `dotnet test tests/FFXIVTelegram.Tests/FFXIVTelegram.Tests.csproj -v minimal`
- `dotnet build FFXIVTelegram.sln -v minimal`
- Manual `/ffxivtelegram testinject hello`
- Telegram `/start` private-chat authorization flow
- Telegram reply inheritance for forwarded tells
- Last-active fallback for untagged messages
- Clean plugin unload with polling cancellation and framework unsubscription

## Review Notes

Before executing this plan, verify again that:

- no task expands channel support beyond `Tell`, `Party`, `Free Company`
- no task introduces persistent retry storage
- no task accepts group chats or multiple authorized Telegram users
- interop work references the local XIVChat source before any signature or payload decisions are made
