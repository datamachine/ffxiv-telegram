# FFXIV Telegram

FFXIV Telegram bridges selected Final Fantasy XIV chat channels with a Telegram bot so you can read and answer supported messages from one private Telegram chat.

Maintained by Surye. Contact: `surye@datamachine.net`

## Install In Dalamud

Custom repo URL:

```text
https://datamachine.net/ffxiv-telegram/repo.json
```

1. Launch the game through XIVLauncher with Dalamud enabled.
2. Open the Dalamud Plugin Installer with `/xlplugins`.
3. Open the installer settings and find `Custom Plugin Repositories`.
4. Add `https://datamachine.net/ffxiv-telegram/repo.json`.
5. Refresh the plugin list, search for `FFXIV Telegram`, and install it.

## Telegram Setup

1. Create a bot with `@BotFather` and copy the bot token.
2. Start a private 1:1 Telegram chat with that bot.
3. Open the FFXIV Telegram configuration window from the Dalamud plugin list or with `/ffxivtelegram`.
4. Paste the token into `Telegram bot token`.
5. Wait for the plugin to show `WaitingForStart`.
6. Send `/start` to the bot from the same private chat you want to authorize.
7. Confirm the plugin now shows `Connected`.

Changing the bot token clears the authorized chat and requires a new `/start`.

## Supported Routes

Forwarding from game to Telegram is available for:

- `Tell`
- `Party`
- `Free Company`

Sending messages from Telegram back to the game supports:

- `/p hello` for party chat
- `/fc hello` for free company chat
- `/r hello` to reply to the most recent tell target known to the plugin

Telegram replies also preserve route context:

- reply to a forwarded tell to answer that tell target
- reply to a forwarded party message to send to party chat
- reply to a forwarded free company message to send to free company chat

If you send untagged text that is not a Telegram reply, the plugin falls back to the last active in-game route it knows about.

## Privacy And Single-User Behavior

- The plugin accepts messages from one authorized private Telegram chat only.
- Telegram groups and channels are ignored.
- The first valid `/start` claims the authorized chat for the current bot token.
- Messages from any other Telegram chat are ignored.
- Telegram traffic is plain text only.
- There is no persistent retry queue for failed sends or failed in-game injections.

Do not share your bot token. Public releases do not include any user token, chat ID, or runtime chat history.

## Maintainer Release Flow

Use stable tags only: `vX.Y.Z`

Build, verify, and generate release artifacts locally:

```bash
dotnet build FFXIVTelegram.sln -c Release -v minimal
dotnet test tests/FFXIVTelegram.Tests/FFXIVTelegram.Tests.csproj -v minimal
dotnet build src/FFXIVTelegram/FFXIVTelegram.csproj -c Release -o artifacts/plugin-release -v minimal
bash scripts/release/build-release.sh --tag vX.Y.Z --input artifacts/plugin-release --output artifacts/release
```

Expected local outputs:

- `artifacts/release/FFXIVTelegram-X.Y.Z.zip`
- `artifacts/release/repo.json`

After local verification, publish with a stable tag:

```bash
git tag vX.Y.Z
git push origin vX.Y.Z
```

The GitHub Actions publish workflow uploads the zip to GitHub Releases and deploys `repo.json` to GitHub Pages for the public custom repo URL above.
