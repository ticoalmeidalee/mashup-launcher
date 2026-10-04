# GTA V x Minecraft

Real Minecraft running next to GTA V story mode and drawn into it. GTA's camera drives Minecraft's, GTA's ground is
Minecraft's collision, and Minecraft's picture (colour + depth) is composited into GTA's frame, so blocks sit on the
street and go behind lampposts. What happens in one game happens in the other.

## What you can do

- **Build** with Minecraft blocks in Los Santos; they become invisible GTA props, so people and cars stop at your walls.
- **Minecraft weapons, GTA effects**: TNT and creepers blow up in both games, crossbow fireworks burst as rockets,
  sword hits deal Minecraft damage with a knockback hop and a red hurt flash.
- **GTA guns in Minecraft hands**: pistol, SMG, assault rifle, pump shotgun, sniper and RPG as Minecraft items (Combat
  tab); every shot is a real GTA bullet.
- **Survival**: switch with F4. GTA's bullets, cars and falls cost hearts, and the police come for you.
- **Elytra**: wear it, jump, press Space in the air; fireworks boost.
- **Every Minecraft key** works from GTA: E inventory (with GTA's cursor), T chat, F3 debug, F3+F4 game modes.
- GTA's clock and weather drive Minecraft's.

## Controls

| key | |
|---|---|
| WASD, mouse, F (cars), phone | GTA, as usual |
| left / right mouse | Minecraft attack / use (break, place, swing, shoot) |
| 1-9, wheel | Minecraft hotbar |
| E, Q, T, L, F1-F3 | Minecraft inventory, drop, chat, advancements, HUD, screenshot, debug |
| Shift / Ctrl | sneak / sprint (Minecraft speeds) |
| F4 | creative <-> survival |
| F6 | experimental: GTA's own guns in Steve's hands |
| F7 / F8 | passthrough off/on / re-level the ground |

## Requirements

- **GTA V Legacy** on Steam (`GTA5.exe`; Enhanced isn't supported), **story mode only**.
- **Minecraft Java Edition** with the official Minecraft Launcher (your own account).
- Mashup Launcher installs everything else: ScriptHookV + its ASI loader (dev-c.com), ReShade 6.8.0 with add-on support
  (reshade.me), ReShade's shader headers (crosire/reshade-shaders), Fabric Loader + Fabric API, and this mod. Turning it
  OFF removes all of it, and the Minecraft profile.

ScriptHookV only runs on the GTA builds it supports: after a GTA update, wait for a new ScriptHookV.

## How to play

1. In Mashup Launcher, turn **GTA V x Minecraft** ON. It opens the Minecraft Launcher: pick the **GTA V x Minecraft**
   profile and press Play. It starts GTA V through Steam.
2. In GTA, pick **Story Mode** yourself. "Minecraft passthrough connected" appears once both are running.
3. When you're done, quit GTA: the launcher turns the mashup OFF (or press OFF).

Never take a modded GTA online. BattlEye stays off while this is ON, which keeps GTA Online locked out.

## Building from source

- GTA plugin: `gta\fetch_deps.ps1`, then `gta\build.bat` (Visual Studio C++ tools) -> `gta\build\MCPassthrough.asi`
- Minecraft mod: `cd mc && gradlew build` (JDK 25) -> `mc\build\libs\passthrough-*.jar`
- Package: `package.ps1` -> `dist\gta-minecraft-<version>.zip` + its SHA-256
- Tests without GTA: `host\*.py` (Python 3, `websockets`) against a running Minecraft; `gta\tests\` has a stand-in GTA.

## Credits

- Built on **rehan-remade/universal-modder**'s MIT-licensed Minecraft x GTA V passthrough example
  (https://github.com/rehan-remade/universal-modder), which made the core: the frame compositor, camera sync, ground
  barriers, explosions, mobs vs police, the Nether and elytra flight.
- ScriptHookV and its ASI loader by Alexander Blade; ReShade and its add-on API by crosire; Fabric; Java-WebSocket by
  TooTallNate (bundled in the mod); GTA V native names from alloc8or's native DB.
- Minecraft belongs to Mojang Studios and Microsoft, and GTA V to Rockstar Games and Take-Two. This is a fan project.
