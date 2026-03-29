# Repository Guidelines

## Project Structure & Module Organization

- `src/FFXIVTelegram/`: plugin source code. Key areas include `Chat/`, `Telegram/`, `Interop/`, `UI/`, `Commands/`, and `Configuration/`.
- `tests/FFXIVTelegram.Tests/`: xUnit test project with matching folders for feature areas plus `Integration/` and `TestDoubles/`.
- `docs/superpowers/`: design specs and implementation plans used during development.
- `README.md`: end-user setup and usage guide.

Keep new code close to the existing feature boundary. Example: Telegram polling changes belong under `src/FFXIVTelegram/Telegram/`, not in the plugin root.

## Build, Test, and Development Commands

- `dotnet build FFXIVTelegram.sln -v minimal`: builds the plugin and test projects.
- `dotnet test tests/FFXIVTelegram.Tests/FFXIVTelegram.Tests.csproj -v minimal`: runs the full automated test suite.
- `dotnet test tests/FFXIVTelegram.Tests/FFXIVTelegram.Tests.csproj --filter FullyQualifiedName~InboundPipelineTests -v minimal`: runs a focused test slice.

Run build and tests serially. The Dalamud packager writes shared outputs and can fail if multiple `dotnet` processes run in parallel.

## Coding Style & Naming Conventions

- Target framework is `.NET 10` with nullable reference types enabled.
- Use 4-space indentation and keep the existing brace and namespace style.
- Prefer small, focused classes with names that describe the role directly, such as `TelegramBridgeService` or `ChatInjectionService`.
- Use `PascalCase` for types and members, `camelCase` for locals and parameters, and keep test names descriptive sentence-style, e.g. `PollOnceDropsUpdatesReturnedForOldTokenAfterLiveTokenChange`.

## Testing Guidelines

- Tests use `xUnit`, `Microsoft.NET.Test.Sdk`, and `coverlet.collector`.
- Add or update tests for any behavior change, especially in routing, polling, native interop boundaries, and configuration persistence.
- Mirror production structure when possible: `Chat/` tests for chat logic, `Telegram/` tests for bot behavior, `Integration/` tests for end-to-end orchestration.

## Commit & Pull Request Guidelines

- Follow the existing commit style: lowercase type prefix plus summary, for example `feat: complete FFXIV Telegram MVP bridge` or `fix: wire game chat monitor and telegram adapter`.
- Keep commits scoped to one logical change.
- PRs should include: purpose, user-visible impact, verification commands run, and any remaining manual Dalamud/Telegram checks.

## Security & Configuration Notes

- Do not log or commit real Telegram bot tokens or chat IDs.
- This plugin is intentionally single-user and private-chat only; preserve that constraint unless the design explicitly changes.
