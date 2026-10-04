# Mashup Launcher (public, open source) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A portable, open-source Windows launcher where anyone can install public or local game mashups, point it at their game folders (auto-detected where possible), and turn a mashup ON/OFF safely, starting with GTA V x Minecraft.

**Architecture:** One .NET Framework 4.8 WinForms exe (no runtime to install, no Bash/Java/MSVC for players) built from small focused `.cs` files with Roslyn. A mashup is a folder with a `mashup.json` manifest that declares install steps (copy packaged files, download official third-party files, set up a Minecraft Fabric profile), the host and guest games, and how to start them. The launcher executes steps natively, records exactly what it added (install record + backups) and removes exactly that on OFF. Public mashups come from a reviewed `library/index.json` in the repo; their zips are SHA-256 pinned. GitHub Actions builds the launcher and each mashup's mod files into release assets.

**Tech Stack:** C# (Roslyn csc, .NET Framework 4.8: WinForms, `System.Web.Script.Serialization.JavaScriptSerializer`, `System.IO.Compression.ZipFile`), GitHub Actions (windows-latest: MSVC, JDK 25, Gradle), the existing GTA V x Minecraft sources (C++ ScriptHookV/ReShade plugin, Fabric mod).

**Spec:** this conversation's requirements, condensed in Global Constraints below (no separate spec file).

## Global Constraints

- Runs entirely on the user's PC from wherever its folder is unzipped (portable). All its data (mashups, settings, install records, backups, logs, Minecraft profile data) lives under the launcher folder. No server, no accounts, no telemetry.
- Network only for user-triggered downloads: the public index and mashup zips (github.com / raw.githubusercontent.com), official third-party files (dev-c.com ScriptHookV, reshade.me, meta.fabricmc.net / maven.fabricmc.net, api.modrinth.com / cdn.modrinth.com).
- Never redistribute game files or third-party binaries whose licence forbids it (ScriptHookV, ReShade): they are downloaded from their official sites at install time.
- Minecraft runs from the player's own Minecraft Launcher (real account). The public build must NOT ship or start the offline Gradle dev client.
- Single-player/offline only: refuse to install into a game folder with anti-cheat unless the manifest declares how the mashup keeps that game offline (`antiCheat.offlineArgs`), and show that to the user.
- Before any install the user sees a trust screen listing every file written, every download (with domain), and the source (reviewed public index vs local/unverified).
- ON adds files; OFF removes exactly the files it added and restores any backed-up originals. A crash mid-install must leave a state that OFF/FIX can clean.
- Licence MIT, keeping rehan-remade/universal-modder's MIT notice for the GTA x Minecraft code derived from it. Repo name `ticoalmeidalee/mashup-launcher`, created private, flipped public only after the owner reviews.
- Exe target: .NET Framework 4.8 (ships with Windows 10/11). Build: `launcher\build.bat` (Roslyn from VS Build Tools or the GitHub runner).

## Review Focus

1. Game folder chosen by the user is wrong (not the game, or a non-Steam copy) -> the launcher must check the manifest's `host.exe` exists there and refuse with a clear message, never copy files into a random folder. (Task 5 test `locate_rejects_folder_without_exe`.)
2. ON interrupted halfway (crash, kill, power) -> the install record is written before each file, so OFF/FIX removes the partial set and restores backups. (Task 3 test `partial_install_is_removable`.)
3. A file the mashup wants to add already exists (another mod's dinput8.dll) -> back it up and restore it on OFF, never silently destroy it. (Task 3 test `existing_file_is_backed_up_and_restored`.)
4. Public index or zip tampered/changed -> SHA-256 mismatch must abort before anything is extracted. (Task 4 test `hash_mismatch_aborts`; Task 8 test `package_hash_mismatch_installs_nothing`.)
5. Path traversal in a mashup zip or manifest (`..\..\Windows\x.dll`, absolute paths) -> rejected; every write must land inside the game folder or the launcher folder. (Task 2 test `manifest_rejects_escaping_paths`; Task 8 test `zip_slip_rejected`.)

---

## File Structure

```
mashup-launcher/
  launcher/
    build.bat                 compile all *.cs -> ..\MashupLauncher.exe (and --test runs the self-tests)
    Program.cs                entry: GUI, or headless --list/--on/--off/--status/--test
    Paths.cs                  launcher-relative folders (mashups, data, logs, backups)
    Json.cs                   JavaScriptSerializer wrapper: parse to Dictionary/List, typed getters
    Manifest.cs               mashup.json model + validation (paths, ids, step kinds)
    Installer.cs              execute steps, install record, backups, uninstall
    Downloads.cs              HTTP with browser UA, SHA-256 verify, zip + exe-appended-zip extract
    GameLocator.cs            Steam libraries/appmanifest lookup, exe check, per-user folder choice
    MinecraftProfile.cs       Fabric profile json, mods download, launcher_profiles.json entry (backup)
    Engine.cs                 ON/OFF orchestration per mashup, host/guest process control, auto-off
    Library.cs                local mashups + public index fetch + package install
    Ui/Theme.cs, Ui/Controls.cs (Toggle, PowerButton, Card), Ui/MainForm.cs, Ui/TrustDialog.cs, Ui/CreateDialog.cs
    Tests.cs                  --test self-tests (plain asserts, temp folders, no network unless file://)
  mashups/
    gta-minecraft/
      mashup.json             manifest (Task 10)
      cover.png
      README.md               what it does, controls, credits
      src/mc/...              Fabric mod (from passthrough/src/mc)
      src/gta/...             ScriptHookV + ReShade plugin (from passthrough/src/gta, no third_party/)
      tools/                  gen_guns.py, host test scripts
  library/
    index.json                reviewed public mashups: id, name, version, url, sha256
    README.md                 how to submit a mashup
  .github/workflows/build.yml build launcher + gta-minecraft assets on push; release on tag
  .github/CODEOWNERS, .github/pull_request_template.md
  docs/superpowers/plans/2026-10-03-mashup-launcher-public.md
  LICENSE, NOTICE.md, README.md, SECURITY.md, CONTRIBUTING.md, .gitignore
```

### mashup.json (format v1)

```json
{
  "format": 1,
  "id": "gta-minecraft",
  "name": "GTA V x Minecraft",
  "version": "1.0.0",
  "tagline": "Real Minecraft inside GTA V story mode",
  "description": "...",
  "hint": "Pick Story Mode yourself in GTA. Never go online while ON.",
  "accent": [61, 220, 132], "accent2": [233, 98, 48],
  "authors": ["ticoalmeidalee"], "license": "MIT",
  "host": {
    "name": "GTA V", "steamAppId": 271590, "exe": "GTA5.exe",
    "process": "GTA5", "stub": "PlayGTAV", "launch": "steam://rungameid/271590",
    "antiCheat": { "offlineArgs": "-nobattleye -noBE", "note": "Story mode only. BattlEye off keeps GTA Online locked out." }
  },
  "guest": {
    "name": "Minecraft", "kind": "minecraft-fabric",
    "minecraft": "26.3", "fabricLoader": "0.19.5", "fabricApi": "0.161.0+26.3",
    "linkPort": 25599
  },
  "install": [
    { "kind": "download", "url": "https://www.dev-c.com/gtav/scripthookv/", "page": true, "pattern": "/files/ScriptHookV_[0-9.]+\\.zip",
      "extract": { "bin/ScriptHookV.dll": "ScriptHookV.dll", "bin/dinput8.dll": "dinput8.dll" }, "to": "game" },
    { "kind": "download", "url": "https://reshade.me/downloads/ReShade_Setup_6.8.0_Addon.exe",
      "extract": { "ReShade64.dll": "ReShade64.asi" }, "to": "game" },
    { "kind": "copy", "from": "payload/game", "to": "game" },
    { "kind": "minecraft-profile" }
  ]
}
```

Step kinds: `copy` (folder or file from the mashup package into `game`), `download` (URL, optional `sha256`, optional `page`+`pattern` to find the file link on an HTML page, `extract` map member->target or omitted to copy the file itself, `to`: `game`), `minecraft-profile` (uses `guest`). All targets are relative and must stay inside their root.

---

### Task 1: Repo scaffold, split launcher into focused files, self-test harness

**Files:**
- Create: `launcher/*.cs` as in File Structure (moved from `passthrough/launcher/MashupLauncher.cs`), `launcher/build.bat`, `launcher/Tests.cs`, `.gitignore`
- Test: `launcher/Tests.cs` (`MashupLauncher.exe --test`)

**Interfaces:**
- Produces: `static class Paths { string Root, Mashups, Data, Logs, Backups; }`, `static class Tests { static int Run(); static void Check(bool ok, string name); static string TempDir(); }`, `MashupLauncher.exe --test` exits 0 when all pass.

- [ ] **Step 1: Write the failing test** - `Tests.cs` with `Run()` calling `Check(Directory.Exists(Paths.Root), "root exists")` and `Check(Paths.Mashups.StartsWith(Paths.Root), "mashups under root")`.
- [ ] **Step 2: Run it to verify it fails** - `cd launcher && build.bat && ..\MashupLauncher.exe --test` -> build error: `Paths` / `--test` not defined.
- [ ] **Step 3: Implement** - copy the current launcher, split classes into the files above unchanged, add `Paths.Data = Root\data`, `Paths.Backups = Root\data\backups`, and in `Program.Main`: `if (args.Length > 0 && args[0] == "--test") return Tests.Run();`. `build.bat`:
  ```bat
  @echo off
  set FW=C:\Windows\Microsoft.NET\Framework64\v4.0.30319
  if not defined CSC set CSC=C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\Roslyn\csc.exe
  "%CSC%" /nologo /noconfig /target:winexe /optimize+ /out:"%~dp0..\MashupLauncher.exe" /r:%FW%\mscorlib.dll /r:%FW%\System.dll /r:%FW%\System.Core.dll /r:%FW%\System.Drawing.dll /r:%FW%\System.Windows.Forms.dll /r:%FW%\System.Web.Extensions.dll /r:%FW%\System.IO.Compression.dll /r:%FW%\System.IO.Compression.FileSystem.dll /recurse:"%~dp0*.cs"
  ```
- [ ] **Step 4: Run tests** - `..\MashupLauncher.exe --test` -> `2/2 passed`, exit 0.
- [ ] **Step 5: Commit** - `git add -A && git commit -m "Scaffold: launcher split into focused files, self-test harness"`

### Task 2: Manifest v1 model and validation

**Files:** Create `launcher/Json.cs`, `launcher/Manifest.cs`; Modify `launcher/Tests.cs`

**Interfaces:**
- Produces: `class Manifest { int Format; string Id, Name, Version, Tagline, Description, Hint; Color Accent, Accent2; HostSpec Host; GuestSpec Guest; List<Step> Install; string Dir; static Manifest Load(string dir); }`, `class HostSpec { string Name, Exe, Process, Stub, Launch, OfflineArgs, AntiCheatNote; int SteamAppId; }`, `class GuestSpec { string Name, Kind, Minecraft, FabricLoader, FabricApi; int LinkPort; }`, `class Step { string Kind, Url, From, To, Sha256, Pattern; bool Page; Dictionary<string,string> Extract; }`, `class ManifestException : Exception`, `static string SafeRelative(string p)` (throws on rooted or `..` paths).

- [ ] **Step 1: Failing tests** - in `Tests.Run()`:
  - `manifest_loads`: write a minimal valid `mashup.json` to a temp dir; `Manifest.Load` returns `Id=="t"`, `Host.Exe=="Game.exe"`, `Install.Count==1`.
  - `manifest_rejects_escaping_paths`: steps with `"to":"..\\.."`, `"from":"C:\\Windows"`, extract target `"..\\x.dll"` each throw `ManifestException`.
  - `manifest_rejects_unknown_step` and `manifest_rejects_bad_id` (`"id":"a/b"`).
- [ ] **Step 2: Run** - build fails (`Manifest` missing).
- [ ] **Step 3: Implement** - `Json.Parse(string) -> object` via `new JavaScriptSerializer { MaxJsonLength = int.MaxValue }.DeserializeObject`; typed getters `Str(d,k,def)`, `Int`, `Bool`, `Obj`, `Arr`. `SafeRelative`: `if (Path.IsPathRooted(p) || p.Split('\\','/').Contains("..")) throw new ManifestException(...)`. Id must match `^[a-z0-9][a-z0-9-]{1,40}$`. Step kinds whitelist: `copy`, `download`, `minecraft-profile`. `to` must be `game`.
- [ ] **Step 4: Run** - all pass.
- [ ] **Step 5: Commit** - `Manifest v1: mashup.json model with path, id and step validation`

### Task 3: Native installer with install record and backups

**Files:** Create `launcher/Installer.cs`; Modify `launcher/Tests.cs`

**Interfaces:**
- Consumes: `Manifest`, `Step`, `Paths.Data`, `Paths.Backups`.
- Produces: `class InstallRecord { List<string> Added; List<string> BackedUp; static InstallRecord Load(string mashupId); void Save(); }` stored at `data\<id>\installed.json`; `static class Installer { void AddFile(string mashupId, string gameDir, string relTarget, Action<string> writeTo); void RemoveAll(string mashupId, string gameDir); FileState State(string mashupId, string gameDir); }` where `FileState { Off, On, Partial }`.

- [ ] **Step 1: Failing tests**
  - `install_then_remove_leaves_folder_identical`: temp game dir with `a.txt`; `AddFile` x2 (`x.dll`, `sub/y.ini`); `State==On`; `RemoveAll`; listing equals original, `State==Off`.
  - `existing_file_is_backed_up_and_restored`: game dir has `dinput8.dll` ("theirs"); `AddFile("dinput8.dll")` writes "ours"; after `RemoveAll` content is "theirs".
  - `partial_install_is_removable`: `AddFile` x1 then simulate crash (new `InstallRecord.Load` from disk), `State==Partial` when a declared-but-missing file exists in the plan; `RemoveAll` leaves the folder original.
- [ ] **Step 2: Run** - fail (missing `Installer`).
- [ ] **Step 3: Implement** - order per file: record `Added` + `Save()` first, then back up an existing target to `data\backups\<id>\<rel>` (record `BackedUp`, `Save()`), then write via temp file + `File.Move`. `RemoveAll`: delete each `Added` (files, then now-empty folders it created), restore each `BackedUp`, delete the record. `State`: none of `Added` present -> Off; all present -> On; else Partial. Every path through `Path.GetFullPath` and asserted to start with the root.
- [ ] **Step 4: Run** - pass.
- [ ] **Step 5: Commit** - `Native installer: install record written before each file, backups restored on OFF`

### Task 4: Downloads with hash pinning and archive extraction

**Files:** Create `launcher/Downloads.cs`; Modify `launcher/Tests.cs`

**Interfaces:**
- Produces: `static class Downloads { byte[] Get(string url); string Sha256(byte[] data); byte[] Verified(string url, string sha256OrNull); string FindLink(string pageUrl, string pattern); Dictionary<string, byte[]> Extract(byte[] archive, IDictionary<string,string> members); }`. `Get` sends a browser User-Agent and Accept headers (dev-c.com rejects bare clients), TLS 1.2. `Extract` handles plain zips and zips appended to an .exe (ReShade setup): find the last End-Of-Central-Directory record and open from the start of the archive.

- [ ] **Step 1: Failing tests** (no network: `file://` URLs to temp files)
  - `hash_ok`: `Verified(file://…, correctSha)` returns the bytes.
  - `hash_mismatch_aborts`: wrong sha -> throws, returns nothing.
  - `extract_zip_members`: build a zip with `bin/a.dll` in a temp; `Extract(map {"bin/a.dll":"a.dll"})` returns one entry with the right bytes.
  - `extract_exe_appended_zip`: prepend 4 KB of junk bytes to that zip; extraction still works.
  - `find_link`: an HTML file containing `<a href="/files/ScriptHookV_3889.0.zip">` -> `FindLink` returns the absolute URL.
- [ ] **Step 2: Run** - fail.
- [ ] **Step 3: Implement** - `WebClient` with headers (file:// works through it); `ServicePointManager.SecurityProtocol = Tls12`; SHA-256 via `SHA256.Create()`; zip via `ZipArchive(new MemoryStream(bytes))`, for exe-appended: scan backwards for `PK\x05\x06`, read central directory offset/size, compute the archive start as `eocdPos - cdSize - cdOffset`, slice from there.
- [ ] **Step 4: Run** - pass.
- [ ] **Step 5: Commit** - `Downloads: browser UA, SHA-256 pinning, zip and exe-appended zip extraction`

### Task 5: Game folder auto-detection and per-user choice

**Files:** Create `launcher/GameLocator.cs`; Modify `launcher/Tests.cs`

**Interfaces:**
- Produces: `static class GameLocator { IEnumerable<string> SteamLibraries(string steamRoot = null); string FindSteamApp(int appId, string steamRoot = null); bool IsGameFolder(string dir, string exe); string Chosen(string mashupId); void Choose(string mashupId, string dir, string exe); }`. Per-user choice stored in `data\<id>\game-folder.txt`. Steam root from registry `HKCU\Software\Valve\Steam\SteamPath`, fallback `C:\Program Files (x86)\Steam`.

- [ ] **Step 1: Failing tests** (fake Steam tree in temp)
  - `steam_libraries_parsed`: `libraryfolders.vdf` with two `"path"` entries -> both returned (unescaping `\\`).
  - `find_app_by_manifest`: library 2 has `steamapps\appmanifest_271590.acf` with `"installdir" "Grand Theft Auto V"` -> path returned.
  - `locate_rejects_folder_without_exe`: `Choose(id, dirWithoutExe, "GTA5.exe")` throws with a message naming the exe.
- [ ] **Step 2: Run** - fail.
- [ ] **Step 3: Implement** - regex `"path"\s+"([^"]+)"` and `"installdir"\s+"([^"]+)"`; `IsGameFolder` = `File.Exists(Path.Combine(dir, exe))`.
- [ ] **Step 4: Run** - pass.
- [ ] **Step 5: Commit** - `Game folders: Steam library auto-detection, exe check, per-user choice`

### Task 6: Minecraft Fabric profile through the player's own launcher

**Files:** Create `launcher/MinecraftProfile.cs`; Modify `launcher/Tests.cs`

**Interfaces:**
- Consumes: `Downloads`, `GuestSpec`, `Installer` record conventions (its own record: `data\<id>\minecraft.json` listing what it added).
- Produces: `static class MinecraftProfile { string DotMinecraft(); void Install(Manifest m, byte[] modJar, string dotMinecraft = null); void Remove(Manifest m, string dotMinecraft = null); void OpenLauncher(); }`. Installs: `versions\fabric-loader-<loader>-<mc>\<same>.json` from `https://meta.fabricmc.net/v2/versions/loader/<mc>/<loader>/profile/json`; profile game dir `data\<id>\minecraft` with `mods\` = Fabric API (Modrinth version file, hash from the API) + the mashup's jar; a `launcher_profiles.json` entry `mashup-<id>` (file backed up to `data\backups\<id>\launcher_profiles.json` first). `Remove` deletes the profile entry and the version json it created (only if it created it). `OpenLauncher` starts the Microsoft Store Minecraft Launcher (`shell:AppsFolder\Microsoft.4297127D64EC6_8wekyb3d8bbwe!Minecraft`), falling back to `%ProgramFiles(x86)%\Minecraft Launcher\MinecraftLauncher.exe`.

- [ ] **Step 1: Failing tests** (fake `.minecraft` in temp; Fabric profile served from a `file://` fixture)
  - `profile_added_and_removed`: after `Install`, `launcher_profiles.json` has `profiles["mashup-t"].gameDir` under `data\t\minecraft` and `lastVersionId=="fabric-loader-0.19.5-26.3"`; other profiles untouched; after `Remove` the file equals the original bytes semantically (same profiles).
  - `existing_version_json_kept`: if the version json already existed, `Remove` leaves it.
- [ ] **Step 2: Run** - fail.
- [ ] **Step 3: Implement** - parse/serialize `launcher_profiles.json` with `JavaScriptSerializer` (preserve unknown keys), base URLs overridable for tests via an optional parameter.
- [ ] **Step 4: Run** - pass.
- [ ] **Step 5: Commit** - `Minecraft guest: Fabric profile in the player's own launcher, fully removable`

### Task 7: Engine on the new pieces, trust screen, anti-cheat guard, GUI integration

**Files:** Create `launcher/Engine.cs` (rewrite), `launcher/Ui/TrustDialog.cs`; Modify `launcher/Ui/MainForm.cs`, `launcher/Tests.cs`

**Interfaces:**
- Consumes: Tasks 2-6.
- Produces: `class Engine { Engine(Manifest m); string GameDir { get; } FileState State(); InstallPlan Plan(); void TurnOn(Settings s, IEnumerable<Engine> others); void TurnOff(Settings s); bool HostRunning(); bool LinkUp(); }`, `class InstallPlan { List<string> Files; List<(string url, string host)> Downloads; bool AntiCheatFound; string AntiCheatNote; bool Reviewed; }`, `static bool AntiCheatPresent(string gameDir)` (folders/files: `BattlEye`, `EasyAntiCheat`, `EasyAntiCheat_EOS`, `start_protected_game.exe`, `BEService*.exe`).

- [ ] **Step 1: Failing tests**
  - `anticheat_refused_without_offline_args`: temp game with `BattlEye\` and a manifest without `antiCheat.offlineArgs` -> `TurnOn` throws "uses anti-cheat".
  - `anticheat_allowed_with_offline_args`: same with offlineArgs -> `Plan().AntiCheatFound && AntiCheatNote != null`, install proceeds (copy-only manifest, no downloads).
  - `on_off_roundtrip_copy_only`: manifest with one `copy` step -> ON makes `State()==On`, OFF restores the original listing.
- [ ] **Step 2: Run** - fail.
- [ ] **Step 3: Implement** - Engine resolves the game folder (`GameLocator.Chosen` -> `FindSteamApp(host.steamAppId)` -> ask UI), runs steps through `Installer.AddFile` (downloads via `Downloads`, extract map), writes `args.txt`-style offline args only through a declared `copy` payload (no special casing). Keep the existing host start/retry/auto-off logic. GUI: detail page shows "Game folder: <path> [Change]" (FolderBrowserDialog -> `GameLocator.Choose`, errors shown); ON first shows `TrustDialog(Plan())` with the file list, download domains, anti-cheat note and "Reviewed public mashup" / "Local mashup: not reviewed" badge; Cancel aborts.
- [ ] **Step 4: Run** - pass; also manual: open GUI, confirm trust dialog lists the GTA files.
- [ ] **Step 5: Commit** - `Engine on manifest v1: trust screen, anti-cheat guard, game folder picker`

### Task 8: Public library and package install

**Files:** Create `launcher/Library.cs`, `library/index.json`, `library/README.md`; Modify `launcher/Ui/MainForm.cs`, `launcher/Tests.cs`

**Interfaces:**
- Produces: `class IndexEntry { string Id, Name, Version, Tagline, Url, Sha256; }`, `static class Library { List<Manifest> Local(); List<IndexEntry> Public(string indexUrl = DefaultIndex); void InstallPackage(IndexEntry e); void InstallLocalZip(string zipPath); }`, `DefaultIndex = "https://raw.githubusercontent.com/ticoalmeidalee/mashup-launcher/main/library/index.json"`. Installed packages go to `mashups\<id>\` with `source.json` (`{"reviewed": true, "sha256": ...}` for index installs, `false` for local zips).

- [ ] **Step 1: Failing tests**
  - `package_installs_from_index`: fixture index (file://) + zip with `mashup.json` -> `mashups\<id>\mashup.json` exists, `source.json.reviewed==true`.
  - `package_hash_mismatch_installs_nothing`: wrong sha -> throws, `mashups\<id>` absent.
  - `zip_slip_rejected`: zip entry `../../evil.txt` -> throws, nothing written outside.
  - `package_id_must_match_manifest`: index id differs from the zip's manifest id -> throws.
- [ ] **Step 2: Run** - fail.
- [ ] **Step 3: Implement** - verified download, extract to `mashups\.incoming\<id>` with every entry path checked via `Path.GetFullPath(...).StartsWith(target)`, validate `Manifest.Load`, then atomically move into place (replacing an older version only when it is OFF). GUI: a "Browse" tab next to "Library" listing public entries with Install/Update buttons and an "Add from .zip" button (local = unverified badge).
- [ ] **Step 4: Run** - pass.
- [ ] **Step 5: Commit** - `Public library: reviewed index, SHA-256 pinned packages, zip-slip safe install`

### Task 9: Create-mashup wizard

**Files:** Create `launcher/Ui/CreateDialog.cs`; Modify `launcher/Ui/MainForm.cs`, `launcher/Tests.cs`

**Interfaces:**
- Produces: `static class Scaffold { string Create(string name, string hostDir, string hostExe, string guestDir, string guestExe); }` -> writes `mashups\<id>\mashup.json` (host from the picked folder: exe, Steam app id when found, no install steps yet), `mashups\<id>\PROMPT.md` (a ready universal-modder prompt: "Build a passthrough mashup: <guest> inside <host>. Host folder ..., guest folder ... Follow skills/mashup-mods and the GTA x Minecraft example; output this mashup's payload and mashup.json install steps."), and returns the folder.

- [ ] **Step 1: Failing test** - `scaffold_writes_valid_manifest`: temp host dir with `Game.exe` -> `Manifest.Load(Scaffold.Create(...))` succeeds, `Host.Exe=="Game.exe"`, `PROMPT.md` mentions both folders.
- [ ] **Step 2: Run** - fail.
- [ ] **Step 3: Implement** - dialog: name, host game (dropdown of detected Steam games + Browse), guest game (same), Create -> scaffold, select it in the library, show buttons "Open folder" and "Copy universal-modder prompt" (clipboard) and, if `claude` is on PATH, "Open in Claude Code" (`cmd /c start claude` in that folder).
- [ ] **Step 4: Run** - pass.
- [ ] **Step 5: Commit** - `Create mashup: scaffold from picked game folders plus a universal-modder prompt`

### Task 10: GTA V x Minecraft as a package, CI builds and releases

**Files:** Create `mashups/gta-minecraft/{mashup.json,README.md,cover.png}`, `mashups/gta-minecraft/src/**` (copied from `passthrough/src/mc` and `passthrough/src/gta` minus `third_party/`, build outputs, `run/`), `mashups/gta-minecraft/build/package.ps1`, `.github/workflows/build.yml`

**Interfaces:**
- Consumes: manifest v1 step kinds.
- Produces: release assets `MashupLauncher.zip` (exe + mashups folder skeleton + LICENSE/README) and `gta-minecraft-<version>.zip` (mashup.json, cover.png, README.md, `payload/game/{MCPassthrough.asi, args.txt, ReShade.ini, ReShadePreset.ini, reshade-shaders/Shaders/*}`, `payload/minecraft/passthrough-<v>.jar`). `library/index.json` entry with the zip's URL + SHA-256 (updated by the release job).

- [ ] **Step 1: Failing check** - `.github/workflows/build.yml` job `test` runs `launcher\build.bat` then `MashupLauncher.exe --test`; push to the private repo and watch it fail on the missing package script.
- [ ] **Step 2: Implement** - workflow on `windows-latest`: setup JDK 25 (actions/setup-java temurin 25), `gta/fetch_deps.ps1` (PowerShell port of fetch_deps.sh: SDK headers only for building; nothing redistributed), `gta/build.bat`, `gradlew build`, `package.ps1` assembling the zips, `MashupLauncher.exe --test`; on tag `v*`: create a release with both zips and open a PR updating `library/index.json` with the new SHA-256.
- [ ] **Step 3: Run** - CI green on push; download the artifact; on this PC install it via the launcher's "Add from .zip" into a fresh folder and run ON/OFF against GTA (owner's game), verifying 8 files added/removed as before plus the Minecraft profile.
- [ ] **Step 4: Commit** - `GTA V x Minecraft package, CI build and tagged releases`

### Task 11: Docs, licence, protection, publish

**Files:** Create `README.md`, `LICENSE`, `NOTICE.md`, `SECURITY.md`, `CONTRIBUTING.md`, `.github/CODEOWNERS`, `.github/pull_request_template.md`

- [ ] **Step 1:** Write docs: README (what it is, download, quick start, safety model, making a mashup, credits), LICENSE MIT (c) 2026 ticoalmeidalee, NOTICE crediting rehan-remade/universal-modder (MIT, notice reproduced), ScriptHookV (Alexander Blade), ReShade (crosire), Fabric, and stating Minecraft/GTA trademarks belong to their owners; SECURITY.md (report via GitHub private advisory; mashups are code; what the launcher guarantees); CONTRIBUTING.md (mashup rules: single-player/offline only, no game files, no anti-cheat bypass or online cheating, no redistribution of restricted binaries, honest description; submissions by PR to `library/index.json` reviewed by CODEOWNERS).
- [ ] **Step 2:** `gh repo create ticoalmeidalee/mashup-launcher --private --source . --push`; confirm CI green.
- [ ] **Step 3:** Owner review gate: send the repo link, wait for an explicit "make it public".
- [ ] **Step 4:** `gh repo edit --visibility public --accept-visibility-change-consequences`; branch protection on `main` (PR required, 1 approving review from CODEOWNERS, status check `test` required, no force pushes) via `gh api -X PUT repos/ticoalmeidalee/mashup-launcher/branches/main/protection`; enable private vulnerability reporting and Dependabot alerts.
- [ ] **Step 5:** Tag `v1.0.0` -> release built; verify the public index URL serves and the launcher's Browse tab lists GTA V x Minecraft.
