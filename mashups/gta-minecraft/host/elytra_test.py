"""Oracle for F4 game-mode toggle and the player's elytra (no GTA): F4 alone flips creative/survival, wearing the
elytra is reported ({"t":"screen","ely":1}) so the host arms flight, and a flight keeps the player's own elytra on
when it ends (a lent one is taken back). Usage: python elytra_test.py port log   (25599 is the live game)"""
import asyncio, json, re, sys
import websockets

if len(sys.argv) < 3:
    sys.exit("usage: elytra_test.py port log (25599 is the live game)")
PORT, LOG = int(sys.argv[1]), sys.argv[2]
F4 = (61, 61 | 0x40000000)


async def main():
    async with websockets.connect(f"ws://127.0.0.1:{PORT}") as ws:
        state = {"ely": None, "surv": None}

        async def reader():
            async for m in ws:
                d = json.loads(m)
                if d.get("t") == "screen":
                    state["ely"], state["surv"] = d.get("ely"), d.get("surv")

        async def cam():
            f = 0
            while True:
                f += 1
                await ws.send(json.dumps({"t": "cam", "f": f, "p": [0.5, 65.62, 0.5], "r": [0, 5, 0], "fov": 70, "fp": True,
                                          "pl": [0.5, 64.0, 0.5], "h": 0.0, "gun": False}))
                await asyncio.sleep(1 / 60)

        async def server_says(cmd):
            mark = len(open(LOG, encoding="utf-8", errors="replace").read())
            await ws.send(json.dumps({"t": "cmd", "c": cmd}))
            await asyncio.sleep(0.6)
            return open(LOG, encoding="utf-8", errors="replace").read()[mark:]

        async def wait(key, want, timeout=3.0):
            for _ in range(int(timeout * 20)):
                if state[key] == want:
                    return True
                await asyncio.sleep(0.05)
            return False

        tasks = [asyncio.create_task(reader()), asyncio.create_task(cam())]
        await ws.send(json.dumps({"t": "ground", "c": [v for x in range(-4, 5) for z in range(-4, 5) for v in (x, z, 63, 63)]}))
        await ws.send(json.dumps({"t": "cmd", "c": "gamemode creative @a"}))
        results = []

        def check(name, ok):
            results.append(ok)
            print(("PASS " if ok else "FAIL ") + name)

        await asyncio.sleep(1.0)
        for want in (1, 0):
            await ws.send(json.dumps({"t": "kbd", "sc": F4[0], "kc": F4[1], "m": 0, "a": 1}))
            await ws.send(json.dumps({"t": "kbd", "sc": F4[0], "kc": F4[1], "m": 0, "a": 0}))
            check(f"F4 alone flips to {'survival' if want else 'creative'}", await wait("surv", want))

        await ws.send(json.dumps({"t": "cmd", "c": "item replace entity @a armor.chest with minecraft:elytra"}))
        check("wearing the elytra is reported (ely=1)", await wait("ely", 1))
        await ws.send(json.dumps({"t": "glide", "on": True, "speed": 1.0}))
        await asyncio.sleep(1.0)
        await ws.send(json.dumps({"t": "glide", "on": False}))
        await asyncio.sleep(0.6)
        out = await server_says("execute if items entity @p armor.chest minecraft:elytra")
        check("after a flight the player's own elytra is still worn", "Test passed" in out)

        await ws.send(json.dumps({"t": "cmd", "c": "item replace entity @a armor.chest with minecraft:air"}))
        check("taking it off is reported (ely=0)", await wait("ely", 0))
        await ws.send(json.dumps({"t": "glide", "on": True, "speed": 1.0}))
        await asyncio.sleep(1.0)
        await ws.send(json.dumps({"t": "glide", "on": False}))
        await asyncio.sleep(0.6)
        out = await server_says("execute if items entity @p armor.chest minecraft:elytra")
        check("a lent elytra (none worn before) is taken back after the flight", "Test failed" in out)
        out = await server_says("data get entity @p abilities.flying")
        check("creative flies again after landing", "1b" in out)
        for t in tasks:
            t.cancel()
        print(f"{sum(results)}/{len(results)} passed")


asyncio.run(main())
