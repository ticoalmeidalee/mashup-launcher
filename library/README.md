# The public mashup library

`index.json` is the list the launcher's **Browse public mashups** window shows. Every entry is reviewed before
it is merged, and the launcher refuses a package whose SHA-256 doesn't match the entry.

```json
{
  "format": 1,
  "mashups": [
    {
      "id": "gta-minecraft",
      "name": "GTA V x Minecraft",
      "version": "1.0.0",
      "tagline": "Real Minecraft inside GTA V story mode",
      "authors": ["ticoalmeidalee"],
      "url": "https://github.com/<owner>/<repo>/releases/download/<tag>/gta-minecraft-1.0.0.zip",
      "sha256": "<64 hex characters: the SHA-256 of that zip>"
    }
  ]
}
```

## Submitting a mashup

1. Build your mashup folder (`mashup.json`, `cover.png`, `README.md`, and the files its install steps copy) and test
   it with **Add .zip** in the launcher: ON, play, OFF, and check the game folder is back to stock.
2. Publish the zip somewhere permanent and public (a GitHub release is best) and compute its SHA-256
   (`certutil -hashfile your.zip SHA256`).
3. Open a pull request that adds one entry to `index.json`. The id must match your `mashup.json`.

## What gets merged

Read [CONTRIBUTING.md](../CONTRIBUTING.md) first. In short, a mashup is merged only if it:

- is single-player and offline, and declares `host.antiCheat.offlineArgs` for any game with anti-cheat;
- contains no game files and no third-party files whose licence forbids redistribution (download those from their
  official site with a `download` step instead);
- doesn't cheat online, bypass anti-cheat, DRM or ownership checks;
- does what its description says, and turns fully OFF.

Reviewers read the manifest and every file in the package. A new version is a new pull request with the new url and
SHA-256.
