# Mashup format (mashup.json, format 1)

A mashup is a folder in the launcher's `mashups\` folder (or a `.zip` of one) with:

- `mashup.json`: what this file describes;
- `cover.png` (optional, square): the library tile;
- `README.md`: what it does, controls, credits;
- the files its `copy` steps add to the game (usually under `payload\`).

The launcher writes `source.json` (where the package came from, reviewed or not) next to it, and keeps per-user state
(chosen game folder, settings, install record, backups) in its own `data\` folder.

## Example

```json
{
  "format": 1,
  "id": "my-mashup",
  "name": "Host x Guest",
  "version": "1.0.0",
  "tagline": "One line for the card",
  "description": "Two lines for the detail page.",
  "hint": "Shown above the log, e.g. what to click in the game.",
  "authors": ["you"],
  "license": "MIT",
  "accent": [61, 220, 132],
  "accent2": [40, 90, 200],
  "host": {
    "name": "Host Game",
    "exe": "Host.exe",
    "steamAppId": 123456,
    "process": "Host",
    "stub": "HostLauncher",
    "launch": "steam://rungameid/123456",
    "antiCheat": { "offlineArgs": "-offline", "note": "How the mashup keeps the game offline." }
  },
  "guest": {
    "name": "Minecraft", "kind": "minecraft-fabric",
    "minecraft": "26.3", "fabricLoader": "0.19.5", "fabricApi": "0.161.0+26.3", "linkPort": 25599
  },
  "runtimeFiles": ["MyMod.log"],
  "install": [
    { "kind": "download", "url": "https://example.com/tool.zip", "sha256": "...", "extract": { "bin/tool.dll": "tool.dll" }, "to": "game" },
    { "kind": "download", "url": "https://example.com/header.fxh", "sha256": "...", "save": "shaders/header.fxh", "to": "game" },
    { "kind": "copy", "from": "payload/game", "to": "game" },
    { "kind": "minecraft-profile", "jar": "payload/minecraft/my-mashup.jar" }
  ]
}
```

## Fields

| field | |
|---|---|
| `format` | `1` |
| `id` | lowercase letters, digits, dashes; must match the folder name in the public index |
| `name`, `version`, `tagline`, `description`, `hint`, `authors`, `license` | shown in the launcher |
| `accent`, `accent2` | `[r, g, b]`: ON button and toggles; the tile gradient when there is no cover |
| `host.exe` | **required**: how the launcher recognises the game folder (it refuses folders without it) |
| `host.steamAppId` | finds the game in the user's Steam libraries automatically |
| `host.process`, `host.stub` | the game's process (and its launcher stub's) names, without `.exe`: OFF closes the game, auto-OFF watches it |
| `host.launch` | how ON starts the game: `steam://rungameid/<host.steamAppId>`, or an `.exe` inside the game folder (nothing else is accepted) |
| `host.antiCheat.offlineArgs` | **required if the game folder has anti-cheat** (BattlEye, EasyAntiCheat): how the mashup keeps it offline (shown on the trust screen); without it, install is refused |
| `guest` | the second game. `kind: "minecraft-fabric"` adds a Fabric profile to the player's own Minecraft Launcher; `linkPort` is the local port the guest listens on (status shows "link ready") |
| `runtimeFiles` | files the mod creates in the game folder while running (logs): OFF deletes them unless they existed before ON |

## Install steps

Every target is relative to the game folder and can never leave it; every `from`/`jar` is relative to the mashup folder.
ON runs the steps in order and records each file before writing it; if a step fails, everything is undone. Files that
already exist are backed up and restored by OFF.

| kind | does |
|---|---|
| `copy` | copies `from` (a file, or a folder's whole tree) into the game folder |
| `download` | downloads `url` (https only). `sha256` pins it (use it whenever the file can't change). `page: true` + `pattern`: `url` is a web page, and the first link matching the regex is downloaded (for sites that only offer their latest version). `extract` maps archive members to target names (zips, and zips appended to an .exe); `save` puts a plain file at a path; with neither, the file keeps its name |
| `minecraft-profile` | needs `guest.kind: "minecraft-fabric"`. Installs the Fabric version, a launcher profile with its own game folder, Fabric API (checked against Modrinth's SHA-512) and the `jar` |

Download third-party files from their official site instead of putting them in your package when their licence doesn't
allow redistribution.

## Testing

`MashupLauncher.exe --add my-mashup.zip`, then turn it ON, play, turn it OFF, and check the game folder is exactly as it
was. Headless: `--list`, `--on`, `--off`, `--status <id>`.
