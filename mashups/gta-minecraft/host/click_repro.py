"""Repro for 'clicks do nothing': act as GTA, then swing the sword and place a grass block.
Expect: a {"t":"melee"} event back for the swing, and a grass block on the ground for the place.
Run with Minecraft (passthrough mod) in its world. Arg: fp|tp (camera mode GTA reports, default fp)."""
import asyncio, json, sys
import websockets

FP = (sys.argv[1] if len(sys.argv) > 1 else "fp") == "fp"


async def main():
    async with websockets.connect("ws://127.0.0.1:25599") as ws:
        events = []

        async def reader():
            async for m in ws:
                events.append(m)

        rd = asyncio.create_task(reader())
        state = {"yaw": 0.0, "pitch": 60.0}

        async def cam():  # GTA sends this every frame: camera at eye height, feet one block above the ground
            f = 0
            while True:
                f += 1
                await ws.send(json.dumps({"t": "cam", "f": f, "p": [0.5, 65.62, 0.5], "r": [state["yaw"], state["pitch"], 0.0],
                                          "fov": 70, "fp": FP, "pl": [0.5, 64.0, 0.5], "h": 0.0, "gun": False}))
                await asyncio.sleep(1 / 60)

        task = asyncio.create_task(cam())
        await ws.send(json.dumps({"t": "ground", "c": [v for x in range(-8, 9) for z in range(-8, 9) for v in (x, z, 63, 63)]}))
        await ws.send(json.dumps({"t": "cmd", "c": "fill -8 64 -8 8 70 8 minecraft:air"}))
        await asyncio.sleep(1.5)

        async def click(k, slot):
            await ws.send(json.dumps({"t": "slot", "n": slot}))
            await asyncio.sleep(0.3)
            await ws.send(json.dumps({"t": "key", "k": k, "down": True}))
            await asyncio.sleep(0.1)
            await ws.send(json.dumps({"t": "key", "k": k, "down": False}))
            await asyncio.sleep(0.6)

        await click("attack", 1)   # diamond sword
        await click("use", 7)      # grass block, looking down at the barrier ground
        # how many grass blocks exist now (feedback lands in Minecraft's log)
        await ws.send(json.dumps({"t": "cmd", "c": "say DIAG grass=" }))
        await ws.send(json.dumps({"t": "cmd", "c": "execute store result storage diag n int 1 run fill -8 64 -8 8 66 8 minecraft:grass_block replace minecraft:grass_block"}))
        await ws.send(json.dumps({"t": "cmd", "c": "data get storage diag n"}))
        await asyncio.sleep(1.0)
        task.cancel(); rd.cancel()
        print("mode:", "first person" if FP else "third person")
        print("melee events:", sum('"melee"' in e for e in events))
        print("other events:", sorted({json.loads(e).get("t") for e in events if e.startswith("{")}))


asyncio.run(main())
