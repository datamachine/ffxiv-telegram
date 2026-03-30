## Summary

Add always-on self-message suppression to both sides of the bridge so the user does not see their own messages echoed back through Telegram or reinjected into FFXIV.

## Goals

- Suppress outbound FFXIV messages authored by the local player before they are forwarded to Telegram.
- Suppress inbound Telegram messages authored by bots before they are injected into FFXIV.
- Preserve legitimate human Telegram input from the authorized user.
- Keep the behavior always on with no UI or configuration toggle.

## Non-Goals

- No general duplicate-message detection based on content fingerprints.
- No temporary route muting or debounce windows.
- No group-chat authorization redesign beyond carrying sender metadata needed for filtering.

## Desired Behavior

### FFXIV to Telegram

The plugin should not forward a supported chat line to Telegram when the sender is the currently logged-in FFXIV character.

This applies to:

- Party chat authored by the local player
- Free Company chat authored by the local player
- Outgoing tells authored by the local player

This does not apply to:

- Incoming tells from other players
- Party/FC messages from other players

### Telegram to FFXIV

The plugin should ignore inbound Telegram updates when the sender is a bot account.

This means:

- Messages sent by the bridge bot itself are ignored
- Messages sent by any other bot account are ignored
- Messages sent by the authorized human user are still accepted

## Architecture

### Outbound Self Filter

Add local-player awareness at the `GameChatMonitor` boundary.

Rationale:

- The decision depends on runtime player identity, not formatting rules
- `GameChatFormatter` should remain a pure transformation layer
- Filtering before `SendToAuthorizedChatAsync` avoids unnecessary send attempts and reply-map writes

Implementation shape:

- Inject a local-player identity source into `GameChatMonitor`
- Normalize the local player name and sender name before comparison
- Skip forwarding when the chat type is one of the supported self-authored routes and the sender matches the local player

Preferred identity source:

- `IClientState.LocalPlayer`

If local player data is unavailable:

- Fail open and do not suppress, rather than dropping messages based on incomplete identity data

### Inbound Bot Filter

Extend the Telegram update model to carry sender metadata from the Telegram `from` object.

New data needed on updates:

- `from.id`
- `from.is_bot`

Filtering location:

- `TelegramBridgeService.HandleIncomingUpdateAsync`

Rationale:

- This is the earliest bridge-owned point where update metadata is available
- It prevents ignored messages from becoming accepted inbound messages
- It avoids polluting downstream routing, injection, and reply-context logic

## Data Model Changes

### TelegramUpdate

Extend the update record with sender metadata:

- `long? FromUserId`
- `bool IsFromBot`

No persistence change is required for this feature.

## Error Handling

- Missing Telegram sender metadata should not crash polling.
- If the Telegram payload does not contain a `from` object, treat the message as non-bot and continue with existing authorization rules.
- If the local player is unavailable, skip self-filtering for that event rather than blocking forwarding.

## Testing Strategy

### Unit Tests

Add coverage for:

- Party message from local player is not forwarded
- Free Company message from local player is not forwarded
- Outgoing tell from local player is not forwarded
- Same routes from another player are still forwarded
- Bot-authored Telegram update is ignored during polling
- Human-authored Telegram update from the authorized chat is still accepted

### Regression Protection

Existing tests for:

- inbound route resolution
- command injection
- accepted Telegram polling

should continue to pass unchanged except where update constructors need sender metadata parameters.

## Files Expected To Change

- `src/FFXIVTelegram/Chat/GameChatMonitor.cs`
- `src/FFXIVTelegram/Telegram/TelegramBridgeService.cs`
- `src/FFXIVTelegram/Telegram/TelegramUpdate.cs`
- `src/FFXIVTelegram/Telegram/TelegramHttpClientAdapter.cs`
- `src/FFXIVTelegram/FfxivTelegramPlugin.cs`
- `tests/FFXIVTelegram.Tests/Chat/GameChatMonitorTests.cs`
- `tests/FFXIVTelegram.Tests/Telegram/TelegramBridgeServiceTests.cs`
- `tests/FFXIVTelegram.Tests/Telegram/TelegramHttpClientAdapterTests.cs`

## Acceptance Criteria

- Sending `/p hello` as the local player no longer produces a Telegram echo of that same line.
- Sending an FC or outgoing tell as the local player is likewise not echoed to Telegram.
- Telegram messages authored by bots do not inject into FFXIV.
- Telegram messages authored by the authorized human still inject into FFXIV normally.
