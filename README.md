# FFXIV Telegram

`FFXIV Telegram` is a Dalamud plugin for Final Fantasy XIV that bridges selected in-game chat channels with a Telegram bot.

It can:

- forward supported in-game chat to Telegram
- accept Telegram messages from one authorized private chat
- inject those messages back into the game client through the native chat path

The current MVP supports:

- `Tell`
- `Party`
- `Free Company`

## Status

This repository currently contains the source code for the plugin. It is not packaged or published from this repo automatically.

## Requirements

- Windows with Final Fantasy XIV and XIVLauncher/Dalamud
- .NET 10 SDK for local builds
- a Telegram bot token
- a private Telegram chat with that bot

## What It Does

Outbound, game to Telegram:

- watches supported chat channels in game
- formats them as plain text such as `[FC] <Player Name>: Hello`
- sends them to the authorized Telegram chat
- stores reply context so Telegram replies can route back correctly

Inbound, Telegram to game:

- accepts messages from one authorized private Telegram chat only
- resolves the route in this order:
  1. explicit tag such as `/p`, `/fc`, or `/r`
  2. reply-to context from a forwarded Telegram message
  3. last active in-game route
- injects the resolved message on the framework thread
- spaces injected messages by about `500ms`

## Security Model

- Only private 1:1 Telegram chats are accepted.
- Group chats and channel posts are ignored.
- The first valid `/start` claims the authorized Telegram `ChatId`.
- Messages from any other Telegram chat are ignored.
- Changing the bot token clears the authorized chat and requires a new `/start`.

## Telegram Setup

1. Create a Telegram bot with `@BotFather`.
2. Copy the bot token.
3. Start a private chat with your bot in Telegram.
4. Load the plugin in Dalamud.
5. Open the plugin configuration window from the Dalamud plugin list.
6. Paste the bot token into `Telegram bot token`.
7. Wait for the connection state to show `WaitingForStart`.
8. Send `/start` to the bot from the same private chat you want to use.
9. Confirm the plugin shows `Connected`.

## Using the Plugin

### Forwarding to Telegram

Enable or disable forwarding for:

- tells
- party chat
- free company chat

When forwarding is enabled, supported messages are sent to Telegram automatically.

### Sending Messages Back to Game

You can send Telegram messages in three ways:

- send `/p hello` to speak in party chat
- send `/fc hello` to speak in free company chat
- send `/r hello` to reply to the most recent tell target known to the plugin

You can also reply directly to a forwarded Telegram message:

- replying to a forwarded tell routes back to that tell target
- replying to a forwarded party message routes to party chat
- replying to a forwarded free company message routes to free company chat

If you send untagged text and it is not a Telegram reply, the plugin falls back to the last active in-game route it knows about.

## Slash Command

The plugin registers:

- `/ffxivtelegram`

Current debug subcommand:

- `/ffxivtelegram testinject <text>`

This bypasses Telegram and sends raw text into the native chat execution path for local validation.

## Connection States

The configuration window reports one of these states:

- `NotConfigured`: no bot token is set
- `WaitingForStart`: token is set, but no Telegram chat has been authorized yet
- `Connected`: token is set and one private chat is authorized
- `Error`: the plugin hit a Telegram transport error with the current configuration

## Limitations

- Only `Tell`, `Party`, and `Free Company` are supported in the MVP.
- Only one Telegram chat is authorized at a time.
- Failed sends and failed injections are dropped; there is no persistent retry queue.
- Telegram traffic is plain text only.
- The plugin relies on native chat injection and should be treated as a power-user/developer plugin, not a casual utility.

## Building From Source

1. Install the .NET 10 SDK.
2. Ensure your local Dalamud development environment is available for `Dalamud.NET.Sdk`.
3. Build the solution:

```bash
dotnet build FFXIVTelegram.sln -v minimal
```

4. Run tests:

```bash
dotnet test tests/FFXIVTelegram.Tests/FFXIVTelegram.Tests.csproj -v minimal
```

The plugin project is at [src/FFXIVTelegram/FFXIVTelegram.csproj](src/FFXIVTelegram/FFXIVTelegram.csproj), and the plugin manifest is at [src/FFXIVTelegram/FFXIVTelegram.json](src/FFXIVTelegram/FFXIVTelegram.json).

## Rider Setup

This repo now includes shared Rider run configurations under [.run](.run):

- `Build Solution`
- `Test Suite`
- `Build And Test`

They use Rider's shared project-file run configuration support and the bundled Shell Script plugin. If you are using Rider on Windows, point Rider's shell support at a working `bash` interpreter such as Git Bash or WSL before running these configs.

Recommended Rider workflow:

1. Open [FFXIVTelegram.sln](FFXIVTelegram.sln).
2. Let Rider restore NuGet packages and index the solution.
3. Use `Build Solution` for plugin builds.
4. Use `Test Suite` for the full xUnit run.
5. Use `Build And Test` when you want a one-click verification pass.

### Rider Debugging

The plugin runs inside the live `ffxiv_dx11.exe` process through Dalamud, so there is no portable project file that can fully automate debugging from Rider.

Use this flow instead:

1. Build the plugin from Rider.
2. Launch FFXIV through XIVLauncher with Dalamud enabled and load the plugin.
3. In Rider, use `Run | Attach to Process` and attach the `.NET` debugger to `ffxiv_dx11.exe`.
4. If you want Rider waiting before the game process starts, use `Run | Attach to an Unstarted Process` and watch for `ffxiv_dx11.exe`.

JetBrains documentation used for this setup:

- shared run/debug configurations: <https://www.jetbrains.com/help/rider/Run_Debug_Configuration.html>
- shell script run/debug configurations: <https://www.jetbrains.com/help/rider/Run_Debug_Configuration_Shell_Script.html>
- attach to process and attach to an unstarted process: <https://www.jetbrains.com/help/rider/attach-to-process.html>

## Development Notes

- Target framework: `.NET 10`
- Dalamud API level: `14`
- Main command: `/ffxivtelegram`
- Plugin manifest version: `0.0.1.0`

## Manual Verification Checklist

Before calling a build ready for live use, validate this in-game:

- `/ffxivtelegram testinject hello`
- Telegram `/start` authorization from a private chat
- forwarding of tell, party, and free company messages
- reply routing from Telegram back to the correct channel
- untagged Telegram message fallback to the last active route
- plugin unload without lingering polling behavior
