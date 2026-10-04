"""Does survival hurt Steve on its own while the host moves him? Streams the host's camera/feet for 10 s across
barrier ground that steps by 1-3 blocks and drops 6, and checks his health stays full: no suffocation at the
steps, no fall damage counted twice (GTA's falls arrive through the damage sync). Usage: python survival_walk_test.py port log"""
import asyncio, json, re, sys
import websockets

if len(sys.argv) < 3:
    sys.exit("usage: survival_walk_test.py port log (25599 is the live game)")
PORT, LOG = int(sys.argv[1]), sys.argv[2]


def ground_height(x):
    # a staircase of 1-3 block steps, then a 6-block drop at x = 24
    return 64 + [0, 1, 3, 2, 0, 3, 1, 0][(x // 3) % 8] if x < 24 else 58


async def main():
    async with websockets.connect(f"ws://127.0.0.1:{PORT}") as ws:
        async def health():
            mark = len(open(LOG, encoding="utf-8", errors="replace").read())
            await ws.send(json.dumps({"t": "cmd", "c": "data get entity @p Health"}))
            await asyncio.sleep(0.6)
            m = re.findall(r"has the following entity data: ([0-9.]+)f", open(LOG, encoding="utf-8", errors="replace").read()[mark:])
            return float(m[-1]) if m else None

        cols = []
        for x in range(-4, 40):
            for z in range(-3, 4):
                top = ground_height(x)
                cols += [x, z, top - 2, top - 1]  # solid from top-2 up to (and including) top-1: feet stand at y = top
        await ws.send(json.dumps({"t": "ground", "c": cols}))
        await ws.send(json.dumps({"t": "cmd", "c": "gamemode survival @a"}))
        await ws.send(json.dumps({"t": "cmd", "c": "effect give @a minecraft:instant_health 1 5 true"}))
        await asyncio.sleep(1.5)
        start = await health()
        # walk +x at Minecraft's walking speed (4.3 m/s), feet on the ground height of each column, camera behind
        t, f, x = 0.0, 0, 0.0
        while x < 38:
            f += 1
            fy = ground_height(int(x))
            await ws.send(json.dumps({"t": "cam", "f": f, "p": [x - 3.5, fy + 2.2, 0.5], "r": [-90, 15, 0], "fov": 70, "fp": False,
                                      "pl": [x, float(fy), 0.5], "h": -90.0, "gun": False}))
            await asyncio.sleep(1 / 60)
            x += 4.317 / 60
        await asyncio.sleep(1.0)
        end = await health()
        print(f"health {start} -> {end}")
        ok = start is not None and end is not None and end >= start - 0.01
        print(("PASS" if ok else "FAIL") + " survival costs nothing while the host walks Steve over steps and a 6-block drop")
        await ws.send(json.dumps({"t": "cmd", "c": "gamemode creative @a"}))
        await asyncio.sleep(0.3)


asyncio.run(main())
