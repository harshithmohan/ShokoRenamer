# ShokoRenamer

Automatically renames anime files using metadata from Shoko. Produces clean, consistent filenames with no configuration required.

## Format

```
{Anime} - {Episode} ({Resolution} {Codec} {Source}) ({CRC}) [{Group}].mkv
```

**Example:** `86 - 01 (1920x1080 HEVC BluRay) (6AA216A5) [Hi10].mkv`

Episode numbers use type prefixes: `C` (Credits), `S` (Special), `T` (Trailer), `P` (Parody), `O` (Other). Regular episodes have no prefix.

If media info is unavailable, resolution is parsed from the filename fallback.

## Installation

### Plugin Manager (Recommended)

1. Open the Shoko Web UI and navigate to **Settings → Plugins → Repositories**.
2. Add the manifest URL: `https://raw.githubusercontent.com/harshithmohan/ShokoRenamer/metadata/manifest.json`
3. Go to **Settings → Plugins → Browse** and find **ShokoRenamer**.
4. Click **Install** on the latest version.
5. Restart Shoko Server.

### Manual

1. Download the [latest release](https://github.com/harshithmohan/ShokoRenamer/releases/latest).
2. Extract and place the plugin into the Shoko plugins directory:
   - (Windows) `C:\ProgramData\ShokoServer\plugins`
   - (Docker) `/home/shoko/.shoko/Shoko.CLI/plugins`
3. Restart Shoko Server.

## Usage

1. Open the Shoko Web UI (port 8111 by default) and log in.
2. Navigate to **Utilities → File Rename**.
3. Click the cog wheel icon to open the renamer config panel.
4. Create a new renamer config, enter a name and select **ShokoRenamer** from the select box.
5. Add the files you wish to rename. The preview will show the resulting filenames.
6. Check **Move** to also move files to their destination folder, or leave unchecked to rename in place.
7. Save the config and click **Rename Files**.

### Renaming on Import

To rename files automatically when they are imported:

1. Enable **Rename On Import** and/or **Move On Import** in Shoko settings.
2. Set your ShokoRenamer config as the **Default**.

## Repository layout / release process

`manifest.json` on `main` is a **stub** — real identity (id, name, overview) with `"releases": []`. The manifest that is actually published — with releases, checksums and download URLs — lives on the **`metadata` branch**. Every push to `main` runs the release workflow: it auto-increments the version (patch bump of the latest release tag), creates a GitHub release with auto-generated notes, builds the archive with `shoko-build` (pruning the manifest to the five most recent releases per channel), uploads the archive, and commits the updated manifest back to the `metadata` branch only; nothing is ever written to `main`.

## Building

```
dotnet tool restore
dotnet build -c Release
```

The plugin is portable (`any`); no runtime identifier is pinned.
