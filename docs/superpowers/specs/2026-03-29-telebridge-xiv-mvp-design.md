# TeleBridge XIV MVP Design

## Overview

TeleBridge XIV is a greenfield Dalamud plugin for Final Fantasy XIV that bridges selected in-game chat channels with a Telegram bot. The MVP supports:

- Forwarding in-game `Tell`, `Party`, and `Free Company` messages to Telegram
- Accepting inbound Telegram messages from one authorized private chat
- Routing inbound Telegram messages into the game through the native chat-processing path
- Using explicit routing tags, Telegram reply context, or last active route to resolve destination

This spec is intentionally limited to the MVP bridge. It excludes persistent retry queues, multi-user support, advanced channel coverage, and nonessential polish.

## Goals

- Build a single Dalamud plugin assembly that is easy to install and reason about
- Isolate Telegram integration, route resolution, and native chat injection behind clear service boundaries
- Keep the initial runtime model simple for one user while preserving extension points for future multi-user authorization and richer routing
- Validate native chat injection separately before wiring it to Telegram inbound traffic

## Non-Goals

- Supporting multiple authorized Telegram chats in MVP
- Persisting unsent or failed messages across plugin reloads
- Supporting group chats, Telegram channels, or public bot use
- Supporting `Say`, `Yell`, `Shout`, alliance chat, linkshells, or cross-world linkshells in MVP
- Adding advanced moderation, templating, or analytics features

## Approved Product Decisions

- Scope: MVP bridge only
- Runtime model: single current user flow, but internal boundaries should remain compatible with future multi-user expansion
- Inbound routing: explicit tag first, then Telegram reply inheritance, then last active route
- Supported in-game channels in MVP: `Tell`, `Party`, `Free Company`
- Failure mode: best effort only; log and drop on failure
- Telegram authorization: first-run bot handshake via `/start`
- Telegram chat type: private 1:1 chats only

## Recommended Approach

Use a layered single-plugin architecture. Keep deployment simple by shipping one Dalamud plugin, but split responsibilities into focused services instead of collapsing behavior into the plugin entrypoint.

Why this approach:

- It keeps the native interop boundary narrow and auditable
- It allows pure logic such as route parsing and auth filtering to stay testable
- It avoids overcommitting to an external bridge service before the MVP proves out
- It leaves space for future multi-user auth and richer routing without rewriting the plugin core

## Architecture

### Composition Root

`TeleBridgePlugin` is the composition root. It owns plugin lifecycle, slash-command registration, configuration loading, UI registration, service construction, and disposal.

Required Dalamud services requested via constructor injection:

- `IDalamudPluginInterface`
- `IChatGui`
- `IPluginLog`
- `IGameInteropProvider`
- `ICommandManager`
- `IFramework`

### Internal Components

#### `TeleBridgeConfiguration`

Persisted plugin configuration holding:

- Telegram bot token
- Authorized Telegram chat id once claimed
- Per-channel forwarding toggles for `Tell`, `Party`, and `Free Company`
- Bridge enabled/disabled state if needed for operational control

The token must be masked in the UI. Sensitive values must never be written to logs.

#### `TelegramBridgeService`

Owns Telegram integration:

- Bot client creation
- Long-polling loop for updates
- `/start` handshake flow
- Outbound sends to the authorized Telegram chat
- Connection and authorization state reporting for UI consumption

This service accepts only private-chat updates for MVP. The first valid private `/start` after a bot token is configured claims the authorized `ChatId`. All later messages from other chat ids are ignored and logged at an appropriate level without exposing message contents unnecessarily.

#### `GameChatMonitor`

Subscribes to `IChatGui.ChatMessage`, filters supported `XivChatType` values, normalizes outgoing messages, and updates route-tracking state for last active channel resolution.

Responsibilities:

- Recognize only supported chat channels
- Strip or normalize payload artifacts that should not appear in Telegram output
- Format forwarded messages consistently, for example `[FC] <Player Name>: Hello`
- Update last active route when supported chat activity is observed

#### `RouteResolver`

Pure logic service that resolves the destination for inbound Telegram text.

Resolution order:

1. Explicit route tag in the message, such as `/fc`, `/p`, or `/r`
2. Telegram reply context, if the inbound Telegram message is a reply to a previously forwarded bot message
3. Last active in-game route
4. Reject if none is available

This service should not know about Telegram API types or native injection details beyond the route metadata it consumes.

#### `TelegramReplyMap`

Bounded in-memory mapping from Telegram bot message ids to route metadata. It exists only to support Telegram reply inheritance.

Requirements:

- Insert an entry only after a forwarded Telegram message succeeds
- Store the minimal route data required to reconstitute the destination
- Apply a bounded capacity and stale-entry expiration to prevent unbounded growth
- Clear state on plugin unload

#### `ChatInjectionService`

Single boundary for game-bound message injection.

Responsibilities:

- Accept resolved route metadata plus sanitized text
- Queue inbound messages
- Enforce approximately 500ms spacing between injections
- Marshal execution onto the FFXIV main thread through `IFramework`
- Call the native chat execution adapter
- Log and drop failures with no retry persistence

No other component may invoke native chat execution directly.

#### `IGameChatExecutor`

Small adapter interface used by `ChatInjectionService`.

The concrete implementation uses `FFXIVClientStructs`, Dalamud interop, and the relevant UI module/chat-processing path to execute outbound text as if entered through the normal chat box. The rest of the plugin remains isolated from pointer and interop details.

#### `ConfigWindow`

Dalamud UI for:

- Entering the Telegram bot token
- Viewing authorization state
- Enabling or disabling supported forwarded channels
- Viewing bridge connection/listening status
- Triggering safe operational actions such as clearing authorization if included in MVP

## Data Flow

### Game to Telegram

1. `GameChatMonitor` receives a chat event from `IChatGui.ChatMessage`
2. If the channel is supported and enabled, the event is normalized into a Telegram-friendly text payload
3. If an authorized Telegram chat has been claimed, `TelegramBridgeService` sends the text to that chat
4. If send succeeds, `TelegramReplyMap` stores the Telegram message id and originating route metadata
5. If no authorized chat exists yet, or send fails, the message is logged and dropped

### Telegram to Game

1. `TelegramBridgeService` receives an update from Telegram long polling
2. Updates from unauthorized chat ids or non-private chats are discarded
3. `/start` in a private chat claims authorization if no chat id has been stored yet
4. For normal text input, `RouteResolver` determines the route using tag, reply inheritance, or last active route
5. If resolution succeeds, the text is sanitized and submitted to `ChatInjectionService`
6. `ChatInjectionService` waits for queue turn, dispatches to the framework thread, and calls `IGameChatExecutor`
7. If injection fails, the failure is logged and the message is dropped

## Routing Model

### Supported Routes

The MVP supports:

- `Tell`
- `Party`
- `Free Company`

The route abstraction should be explicit enough to grow later, not encoded as ad hoc strings throughout the codebase.

For MVP planning, the route model should distinguish at least:

- `FreeCompany`
- `Party`
- `Tell(target)` where `target` is the concrete in-game tell recipient identity needed for injection

### Explicit Tags

Inbound Telegram messages may start with a route tag. Initial supported tags should map cleanly onto MVP routes:

- `/fc` for Free Company
- `/p` for Party
- `/r` for the most recently tracked tell target

Tag parsing must strip the prefix before injection. Invalid or unsupported tags should cause a Telegram-visible error response rather than silent fallback.

For MVP, `/r` is shorthand only. Explicit freeform specification of a tell target from Telegram text is out of scope unless later planning proves it is required for safe tell injection.

### Reply Inheritance

When a Telegram user replies to a forwarded bot message, the route from that original forwarded message should be reused automatically. This makes Telegram replies context-aware without requiring repeated manual tags.

Reply inheritance applies only when the replied-to message id is present in `TelegramReplyMap`. For forwarded tells, the stored route must retain the concrete tell target needed to answer the same conversation. If the mapping has expired or does not exist, resolution falls through to last active route.

### Last Active Route

When a message has no explicit tag and no reply context, the plugin should use the last active supported in-game route observed by `GameChatMonitor`.

For tells, this means the most recently tracked tell target known to the plugin, not an unspecified generic reply state.

If no supported route has been observed yet, the plugin should reject the message and explain that the user must either provide a route tag or first establish a supported route in game.

## Telegram Authorization and Connection State

### Authorization

MVP authorization flow:

1. User enters a bot token in plugin config
2. Plugin starts Telegram long polling
3. Plugin waits for `/start` from a private Telegram chat
4. The first valid `/start` claims the authorized chat id
5. Later inbound messages are accepted only from that chat id

Unauthorized inbound messages must be dropped. Group chats and Telegram channels are out of scope and should be rejected for MVP even if technically reachable.

### Status States

Expose at least these states to the UI:

- `Not configured`
- `Waiting for /start`
- `Connected`
- `Error`

These states should be driven by observable runtime state, not inferred loosely from whether a token string exists.

For MVP:

- `Not configured` means no usable bot token is stored
- `Waiting for /start` means the bot token is configured and polling is active, but no authorized private chat has been claimed yet
- `Connected` means polling is healthy and an authorized private chat is present
- `Error` means polling or bot initialization has failed and requires user attention

## Error Handling and Guardrails

- Reject empty or whitespace-only Telegram text
- Reject unsupported or malformed route tags
- Reject text that exceeds safe limits for injection
- Never inject from the Telegram polling thread
- Never log the bot token
- Drop failed send or injection attempts after logging; do not retry persistently
- Bound in-memory structures such as queues and reply maps
- Cancel polling and queue work cleanly during plugin disposal

User-facing Telegram errors should be minimal and actionable, for example:

- no authorized chat yet
- route could not be resolved
- route tag unsupported
- message rejected for length or format

## Project Structure

Initial project structure should favor small, purpose-specific files and folders:

- `Configuration/`
- `Telegram/`
- `Chat/`
- `Interop/`
- `UI/`

The plugin should remain one assembly for MVP unless implementation proves a stronger separation is necessary.

## Native Interop Strategy

The native chat path is a quarantined subsystem.

Constraints:

- All direct `FFXIVClientStructs` usage lives behind `IGameChatExecutor`
- Framework-thread dispatch is mandatory before native execution
- Native injection is validated first through a controlled slash command before being coupled to Telegram inbound traffic

Implementation planning should include a verification step such as `/telebridge testinject <message>` to prove the execution path works safely on the main thread before enabling Telegram-driven injection.

## Testing and Verification Strategy

### Unit-Testable Logic

Plan tests around pure logic boundaries:

- route tag parsing
- route precedence rules
- reply-context lookup behavior
- authorization filtering
- connection/auth state transitions
- sanitization and rejection rules

Telegram and native interop should be abstracted so these tests do not require the game client or real Telegram traffic.

### Manual Integration Sequence

1. Plugin loads and configuration persists correctly
2. Telegram token is accepted and bridge enters `Waiting for /start`
3. Private `/start` claims authorization
4. Supported in-game messages forward to Telegram with expected formatting
5. Native test injection works through slash command on the framework thread
6. Tagged Telegram messages route correctly
7. Telegram replies inherit the original route correctly
8. Untagged messages fall back to last active route correctly
9. Disposal stops polling and clears transient runtime state safely

## Milestone Alignment

This MVP spec maps onto the planned phases as follows:

1. Foundation and UI bootstrap
2. Game-to-Telegram forwarding
3. Native interop validation through controlled injection
4. Telegram inbound bridge wired through main-thread dispatch
5. Routing polish limited to tags, reply inheritance, bounded queueing, and disposal cleanup

## Open Points for Planning

These are not unresolved product questions, but implementation-planning details to settle later:

- exact bounded sizes and expiration windows for the injection queue and reply map
- exact text sanitization rules based on available game and Telegram constraints
- exact command surface for diagnostics beyond `/telebridge` and test injection

They should be resolved in the implementation plan, not by expanding MVP scope.
