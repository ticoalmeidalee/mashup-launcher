"""Oracle for the survival link and the host's quiet sync commands (no GTA): Minecraft reports survival
({"t":"screen","surv":1}), GTA's damage (sent as a quiet `damage` command) costs Steve health, and quiet commands
(time, weather) leave no chat. Usage: python survival_test.py [port] [minecraft log path]"""
import asyncio, json, re, sys, time
import websockets

if len(sys.argv) < 2:
    sys.exit("give the port explicitly (25599 is the live game)")
PORT = int(sys.argv[1])
LOG = sys.argv[2]


def log_tail(since):
    return open(LOG, encoding="utf-8", errors="replace").read()[since:]


async def main():
    async with websockets.connect(f"ws://127.0.0.1:{PORT}") as ws:
        state = {"surv": None}

        async def reader():
            async for m in ws:
                d = json.loads(m)
                if d.get("t") == "screen":
                    state["surv"] = d.get("surv")

        async def cam():
            f = 0
            while True:
                f += 1
                await ws.send(json.dumps({"t": "cam", "f": f, "p": [0.5, 65.62, 0.5], "r": [0, 5, 0], "fov": 70, "fp": True,
                                          "pl": [0.5, 64.0, 0.5], "h": 0.0, "gun": False}))
                await asyncio.sleep(1 / 60)

        tasks = [asyncio.create_task(reader()), asyncio.create_task(cam())]
        await ws.send(json.dumps({"t": "ground", "c": [v for x in range(-4, 5) for z in range(-4, 5) for v in (x, z, 63, 63)]}))
        results = []

        def check(name, ok):
            results.append(ok)
            print(("PASS " if ok else "FAIL ") + name)

        async def health():
            mark = len(open(LOG, encoding="utf-8", errors="replace").read())
            await ws.send(json.dumps({"t": "cmd", "c": "data get entity @p Health"}))
            await asyncio.sleep(0.6)
            m = re.findall(r"has the following entity data: ([0-9.]+)f", log_tail(mark))
            return float(m[-1]) if m else None

        await ws.send(json.dumps({"t": "cmd", "c": "gamemode survival @a"}))
        await asyncio.sleep(2.5)
        check("survival is reported to the host (surv=1)", state["surv"] == 1)
        await ws.send(json.dumps({"t": "cmd", "c": "effect clear @a"}))
        await ws.send(json.dumps({"t": "cmd", "c": "effect give @a minecraft:instant_health 1 5 true"}))
        await asyncio.sleep(1.0)
        before = await health()
        mark = len(open(LOG, encoding="utf-8", errors="replace").read())
        await ws.send(json.dumps({"t": "cmd", "q": True, "c": "damage @p 3.6 minecraft:generic"}))  # what a GTA pistol hit sends
        await ws.send(json.dumps({"t": "cmd", "q": True, "c": "time set 18000"}))
        await ws.send(json.dumps({"t": "cmd", "q": True, "c": "weather rain"}))
        await asyncio.sleep(1.0)
        quiet = log_tail(mark)
        after = await health()
        print(f"   health {before} -> {after}")
        check("a GTA hit (3.6) costs Steve health (3.6, less natural regen while we look)", before is not None and after is not None and 1.5 <= before - after <= 3.65)
        check("quiet sync commands leave no chat or log lines",
              "Set the time" not in quiet and "weather" not in quiet.lower() and "damage" not in quiet.lower())
        await ws.send(json.dumps({"t": "cmd", "c": "execute if predicate {condition:\"minecraft:time_check\",value:{min:17990,max:18010}} run say time-synced"}))
        await ws.send(json.dumps({"t": "cmd", "c": "gamemode creative @a"}))
        await asyncio.sleep(2.5)
        check("back in creative is reported (surv=0)", state["surv"] == 0)
        for t in tasks:
            t.cancel()
        print(f"{sum(results)}/{len(results)} passed")


asyncio.run(main())
