# AGENTS.md

## Project

A single-file Shoko relocation provider plugin. No config, no tests — one `Renamer.cs` implementing `IRelocationProvider`.

- **Framework**: .NET 10.0, C# latest, `EnableDynamicLoading`
- **Build**: `dotnet build` (has a `.sln`)
- **Plugin type**: `IRelocationProvider` (non-generic, no config) — not the old `IPlugin` system

**Namespace vs directory**: The directory is `Shoko.Plugin.Renamer` but the namespace is `ShokoRenamer`. The class names match: `ShokoRenamer : IRelocationProvider`.

## Key dependency

`Shoko.Abstractions` with `ExcludeAssets="runtime"`. The DLL is provided by ShokoServer at runtime, not bundled. Same for `Logging.Abstractions`.

## Architecture

**`Renamer.cs`** (namespace `ShokoRenamer`):
- `Plugin : IPlugin` — entry point, UUID via `UuidUtility.GetV5`
- `ShokoRenamer : IRelocationProvider` — primary constructor, handles all rename/move logic
- Respects `ctx.RenameEnabled`/`ctx.MoveEnabled` — sets `SkipRename`/`SkipMove` when disabled

## Gotchas

- **`PreferredTitle` is `ITitle?`, not `string`** — always use `.PreferredTitle?.Value ?? .Title`
- CRC hash type string is `"CRC32"` (case-sensitive)
- `IManagedFolder.DropFolderType` is a flags enum — use `HasFlag(DropFolderType.Destination)`, not `==`
- `ReplaceInvalidPathCharacters()` is an extension from `Shoko.Abstractions.Extensions`
- `IReadOnlyList<IShokoEpisode>` is the parameter type, don't `.ToList()` it

## CI (release.yml)

Push to `main` → auto-bumps patch version → publishes linux-x64 → creates GitHub Release → updates `manifest.json` → auto-commits via `git-auto-commit-action`.

- Version format: `x.y.z` (patch bump, no prerelease)
- Only `linux-x64` runtime
- Manifest keeps latest 5 releases
- `abstraction` field: major.minor.patch only (drops `-alpha.xx` suffix from package version)

**Do not commit or push without asking the user.**
