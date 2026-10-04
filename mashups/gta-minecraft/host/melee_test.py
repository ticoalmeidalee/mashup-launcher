"""Oracle for Minecraft melee on GTA (no GTA): every swing at no Minecraft entity reaches the host as
{"t":"melee","dmg":x} with Minecraft's damage: the held item's attack damage scaled by the cooldown charge.
Usage: python melee_test.py port   (25599 is the live game)"""
import asyncio, json, sys
import websockets

if len(sys.argv) < 2:
    sys.exit("give the port explicitly (25599 is the live game)")
PORT = int(sys.argv[1])


async def main():
    async with websockets.connect(f"ws://127.0.0.1:{PORT}") as ws:
        hits = []

        async def reader():
            async for m in ws:
                d = json.loads(m)
                if d.get("t") == "melee":
                    hits.append(d["dmg"])

        async def cam():
            f = 0
            while True:
                f += 1
                await ws.send(json.dumps({"t": "cam", "f": f, "p": [0.5, 65.62, 0.5], "r": [0, 0, 0], "fov": 70, "fp": True,
                                          "pl": [0.5, 64.0, 0.5], "h": 0.0, "gun": False}))
                await asyncio.sleep(1 / 60)

        tasks = [asyncio.create_task(reader()), asyncio.create_task(cam())]
        await ws.send(json.dumps({"t": "ground", "c": [v for x in range(-4, 5) for z in range(-4, 5) for v in (x, z, 63, 63)]}))
        await ws.send(json.dumps({"t": "cmd", "c": "item replace entity @a hotbar.1 with minecraft:diamond_sword"}))
        await ws.send(json.dumps({"t": "cmd", "c": "item replace entity @a hotbar.7 with minecraft:grass_block 64"}))
        results = []

        def check(name, ok):
            results.append(ok)
            print(("PASS " if ok else "FAIL ") + name)

        async def swing():
            n = len(hits)
            await ws.send(json.dumps({"t": "key", "k": "attack", "down": True}))
            await asyncio.sleep(0.05)
            await ws.send(json.dumps({"t": "key", "k": "attack", "down": False}))
            await asyncio.sleep(0.3)
            return hits[n] if len(hits) > n else None

        await ws.send(json.dumps({"t": "slot", "n": 1}))
        await asyncio.sleep(1.5)  # full charge
        full = await swing()
        check(f"a full-charge diamond sword swing deals 7 ({full})", full is not None and abs(full - 7.0) < 0.05)
        spam = await swing()
        check(f"swinging again at once deals much less ({spam})", spam is not None and spam < 0.7 * full)
        await ws.send(json.dumps({"t": "slot", "n": 7}))
        await asyncio.sleep(1.5)
        fist = await swing()
        check(f"a block in hand hits like a fist: 1 ({fist})", fist is not None and abs(fist - 1.0) < 0.05)
        for t in tasks:
            t.cancel()
        print(f"{sum(results)}/{len(results)} passed")


asyncio.run(main())
