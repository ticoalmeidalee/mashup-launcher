# Contributing

Thanks for helping. There are two kinds of contribution: code (the launcher, or a mashup's source in `mashups/`), and
listing a mashup in the public library.

## Rules for every mashup

A mashup is only accepted (in `mashups/` or in `library/index.json`) if it:

1. **Is single-player and offline.** Nothing that touches a game's online modes. Games with anti-cheat must declare
   `host.antiCheat.offlineArgs` and actually keep the game offline.
2. **Doesn't cheat, or bypass anti-cheat, DRM or ownership checks.** No offline-mode account bypasses, no cracked files.
3. **Contains no game files** (models, textures, sounds, code from the games) and no third-party files whose licence
   forbids redistribution. Download those from the official site with a `download` step instead.
4. **Does what it says**, with an honest README and credits for the work it builds on.
5. **Turns fully OFF**: after OFF, the game folder is exactly as it was before ON.

## Listing a mashup

See [library/README.md](library/README.md): publish the zip (a GitHub release is best), then open a pull request adding
one entry with its url and SHA-256. The mod's source must be public and its compiled files reproducible from it:
reviewers read the manifest and the source, rebuild, compare, then test it.

## Code

- Launcher: C# for .NET Framework 4.8 in `launcher/`. Run `test.bat` (it must pass); add a test in `launcher/Tests.*.cs`
  for every behaviour you add or fix. Keep the launcher a single exe with no other dependencies.
- Mashups: follow the build instructions in the mashup's README; keep `package.ps1` working so CI can build it.
- Keep pull requests focused; describe how you tested.

By contributing you agree your contribution is licensed under the MIT licence of this repository.
