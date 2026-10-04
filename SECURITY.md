# Security

## Reporting

Please report security problems privately through GitHub: **Security → Report a vulnerability** on this repository
(private vulnerability reporting). Don't open a public issue for them. Expect a reply within a week.

In scope: the launcher (for example, a mashup or zip that can write outside the game folder, a check that can be
bypassed, a download that isn't verified), the public index, and the mashups in this repository.

## What the launcher guarantees

Mashups are code that runs on your PC: a mashup's game files (a ScriptHookV plugin, a Minecraft mod) run with your
permissions when you play. The launcher can't make an untrusted mod safe, so it makes trust visible and limits what an
install can touch:

- **Writes stay inside the game folder you picked** (and the launcher's own folder). Every path in a manifest and every
  zip entry is checked; absolute paths, `..` and zip-slip entries are rejected.
- **Downloads are https-only.** Public packages must match the SHA-256 in `library/index.json`; download steps with a
  `sha256` must match it; Fabric API must match Modrinth's SHA-512. Files from sites that only serve their latest version
  (`page` downloads) can't be pinned, and the trust screen names the site.
- **Before installing, the trust screen** lists every file, every download with its site, the Minecraft profile, any
  anti-cheat note, and whether the mashup came from the reviewed index or from you.
- **ON is recorded before it happens**, so OFF removes exactly what ON added and restores what it replaced, even after a
  crash.
- **Anti-cheat**: a game folder with BattlEye or EasyAntiCheat is refused unless the mashup declares how it keeps the
  game offline.

## What it doesn't do

- It doesn't sandbox the mods themselves: only install mashups from people you trust, and prefer reviewed ones.
- Review of the public index is done by people reading the manifest and package; it lowers the risk, it doesn't remove it.
