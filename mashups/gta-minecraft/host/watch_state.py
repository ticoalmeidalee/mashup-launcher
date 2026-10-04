"""Read-only: prints the GTA plugin's live state (speed, movement mode, keys, flight, survival) twice a second,
as relayed by the Minecraft mod. Sends nothing to either game. Usage: python watch_state.py [seconds]"""
import asyncio, json, sys, time
import websockets

MODES = {0: "sneak", 1: "walk", 2: "sprint"}


async def main():
    end = time.time() + (float(sys.argv[1]) if len(sys.argv) > 1 else 60)
    async with websockets.connect("ws://127.0.0.1:25599") as ws:
        while time.time() < end:
            try:
                d = json.loads(await asyncio.wait_for(ws.recv(), 2))
            except asyncio.TimeoutError:
                continue
            if d.get("t") == "gtastate":
                print(f"speed {d.get('spd', 0):5.2f} m/s  mode {MODES.get(d.get('mode'), '?'):6}  rate {d.get('rate', 0):.3f}  "
                      f"keys [{d.get('keys', '')}]  armed {d.get('armed')}  flying {d.get('fly')}  survival {d.get('surv')}", flush=True)


asyncio.run(main())
