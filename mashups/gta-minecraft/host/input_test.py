"""Oracle for host keyboard/mouse forwarding (no GTA): Minecraft keybinds, screens, typing, the game-mode switcher.
Sends exactly what the GTA plugin sends ({"t":"kbd"|"text"|"mpos"|"mbtn"}) and checks Minecraft's own reports
({"t":"screen"}) and its log. Usage: python input_test.py [port] [minecraft log path]"""
import asyncio, json, sys, time
import websockets

if len(sys.argv) < 2:
    sys.exit("give the port explicitly (25599 is the live game)")
PORT = int(sys.argv[1])
LOG = sys.argv[2] if len(sys.argv) > 2 else None
M = 0x40000000  # SDLK_SCANCODE_MASK
KEYS = {"e": (8, ord("e")), "t": (23, ord("t")), "esc": (41, 27), "enter": (40, 13), "f3": (60, 60 | M), "f4": (61, 61 | M)}


async def main():
    async with websockets.connect(f"ws://127.0.0.1:{PORT}") as ws:
        screen = {"open": None}

        async def reader():
            async for m in ws:
                if '"screen"' in m:
                    screen["open"] = json.loads(m)["open"]

        async def cam():
            f = 0
            while True:
                f += 1
                await ws.send(json.dumps({"t": "cam", "f": f, "p": [0.5, 65.62, 0.5], "r": [0, 30, 0], "fov": 70, "fp": True,
                                          "pl": [0.5, 64.0, 0.5], "h": 0.0, "gun": False}))
                await asyncio.sleep(1 / 60)

        tasks = [asyncio.create_task(reader()), asyncio.create_task(cam())]
        await ws.send(json.dumps({"t": "ground", "c": [v for x in range(-4, 5) for z in range(-4, 5) for v in (x, z, 63, 63)]}))

        async def key(name, action):
            sc, kc = KEYS[name]
            await ws.send(json.dumps({"t": "kbd", "sc": sc, "kc": kc, "m": 0, "a": action}))

        async def tap(name):
            await key(name, 1)
            await asyncio.sleep(0.05)
            await key(name, 0)

        async def wait_screen(want, timeout=2.0):
            end = time.time() + timeout
            while time.time() < end:
                if screen["open"] == want:
                    return True
                await asyncio.sleep(0.05)
            return False

        results = []
        def check(name, ok):
            results.append(ok)
            print(("PASS " if ok else "FAIL ") + name)

        check("starts with no screen", await wait_screen(0, 3))
        await tap("e")
        check("E opens the inventory (screen reported open)", await wait_screen(1))
        await ws.send(json.dumps({"t": "mpos", "x": 0.5, "y": 0.5}))
        await ws.send(json.dumps({"t": "mbtn", "b": 1, "down": True, "m": 0, "x": 0.5, "y": 0.5}))
        await ws.send(json.dumps({"t": "mpos", "x": 0.55, "y": 0.55}))
        await ws.send(json.dumps({"t": "mbtn", "b": 1, "down": False, "m": 0, "x": 0.55, "y": 0.55}))
        await asyncio.sleep(0.3)
        check("cursor move, click and drag inside the inventory leave it open", screen["open"] == 1)
        await tap("esc")
        check("Esc closes it", await wait_screen(0))
        await tap("t")
        check("T opens chat", await wait_screen(1))
        await ws.send(json.dumps({"t": "text", "s": "/say hello-from-gta"}))
        await asyncio.sleep(0.2)
        await tap("enter")
        check("Enter sends and closes chat", await wait_screen(0))
        await key("f3", 1)
        await asyncio.sleep(0.05)
        await tap("f4")
        check("F3+F4 opens the game-mode switcher", await wait_screen(1))
        await asyncio.sleep(0.3)
        await key("f3", 0)
        check("releasing F3 picks a mode and closes it", await wait_screen(0))
        await asyncio.sleep(0.5)
        # the switcher moved off creative (the world's starting mode); then put creative back for the next run
        await ws.send(json.dumps({"t": "cmd", "c": "execute unless entity @a[gamemode=creative] run say mode-switched"}))
        await asyncio.sleep(0.5)
        await ws.send(json.dumps({"t": "cmd", "c": "gamemode creative @a"}))
        await asyncio.sleep(0.5)
        for t in tasks:
            t.cancel()

    if LOG:
        log = open(LOG, encoding="utf-8", errors="replace").read()
        check("the typed command ran (log shows hello-from-gta)", "hello-from-gta" in log)
        check("F3+F4 changed the game mode (server says mode-switched)", "mode-switched" in log)
        check("no input exceptions in the log", "event handler" not in log and "ReportedException" not in log)
    print(f"{sum(results)}/{len(results)} passed")


asyncio.run(main())
