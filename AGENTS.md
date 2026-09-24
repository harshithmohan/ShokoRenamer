# AGENTS.md

## Project

A single-file Shoko relocation provider plugin. No config, no tests — one `Renamer.cs` implementing `IRelocationProvider`.

- **Framework**: .NET 10.0, C# latest, `EnableDynamicLoading`
- **Build**: `dotnet tool restore && dotnet build` (has a `.sln`, `global.json`)
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

Push to `main` → auto-bumps patch version → creates GitHub Release → builds with `shoko-build` → uploads archive → commits updated manifest to the **`metadata` branch only** (never `main`).

- Version format: `x.y.z` (patch bump, no prerelease)
- Portable `any` runtime (no RID in csproj)
- Channel: `Dev`; manifest pruned to 5 releases per channel
- `main` carries a stub `manifest.json` (`releases: []`); live manifest is on `metadata`
- Local build: `dotnet tool restore && dotnet build -c Release` (targets package stamps metadata from the stub)
- `Shoko.BuildTools.Targets` (csproj) stamps assembly metadata; `Shoko.BuildTools` tool (`shoko-build`) builds, zips, and updates the manifest

**Do not commit or push without asking the user.**
