# Notices and credits

Mashup Launcher is MIT licensed (see [LICENSE](LICENSE)). It includes, builds on, or downloads the following.

## Included (source)

### rehan-remade/universal-modder (MIT)

`mashups/gta-minecraft` is derived from the Minecraft x GTA V passthrough example in
https://github.com/rehan-remade/universal-modder (`examples/minecraft-gta5-passthrough`), extended with input
forwarding, Minecraft guns, movement, survival, melee and elytra changes. Its licence:

```
MIT License

Copyright (c) 2026 Rehan and universal-modder contributors

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT.
IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM,
DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR
OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE
OR OTHER DEALINGS IN THE SOFTWARE.
```

GTA V native names and hashes in `mashups/gta-minecraft/gta/src/natives.h` come from alloc8or's native DB
(https://github.com/alloc8or/gta5-nativedb-data).

### Bundled in the Minecraft mod

- Java-WebSocket 1.6.0 by TooTallNate (MIT), https://github.com/TooTallNate/Java-WebSocket
- Gradle wrapper (Apache 2.0)

## Downloaded at install time (not redistributed)

The GTA V x Minecraft package downloads these from their official sources when you turn it ON, and removes them when
you turn it OFF:

- **ScriptHookV** and its ASI loader, by Alexander Blade: https://www.dev-c.com/gtav/scripthookv/
- **ReShade** (with add-on support), by crosire: https://reshade.me
- **ReShade.fxh, ReShadeUI.fxh** from crosire/reshade-shaders: https://github.com/crosire/reshade-shaders
- **Fabric Loader** (Apache 2.0): https://fabricmc.net, and **Fabric API** (Apache 2.0) via Modrinth

## Build-time only

The ScriptHookV SDK and ReShade's add-on headers are downloaded by `mashups/gta-minecraft/gta/fetch_deps.ps1` to compile
the plugin; they are not part of the repository or the packages.

## Trademarks

Minecraft is a trademark of Mojang Studios / Microsoft. Grand Theft Auto and GTA V are trademarks of Rockstar Games /
Take-Two Interactive. Mashup Launcher and its mashups are fan projects, not affiliated with or endorsed by them. No game
files are included or redistributed.
