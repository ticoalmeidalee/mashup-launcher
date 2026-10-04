"""The mod's local link must refuse web pages: a browser always sends an Origin header (the GTA plugin and these
tools don't), so a handshake with one is rejected. Usage: python origin_test.py port   (25599 is the live game)"""
import asyncio, sys
import websockets

if len(sys.argv) < 2:
    sys.exit("give the port explicitly (25599 is the live game)")
PORT = int(sys.argv[1])


async def main():
    ok = 0
    try:
        async with websockets.connect(f"ws://127.0.0.1:{PORT}", origin="https://evil.example"):
            print("FAIL a browser-style connection (with Origin) was accepted")
    except Exception as e:
        print(f"PASS a browser-style connection (with Origin) is refused ({type(e).__name__})")
        ok += 1
    try:
        async with websockets.connect(f"ws://127.0.0.1:{PORT}") as ws:
            await ws.recv()  # the hello
            print("PASS a native connection (no Origin) still works")
            ok += 1
    except Exception as e:
        print(f"FAIL a native connection was refused ({type(e).__name__}: {e})")
    print(f"{ok}/2 passed")


asyncio.run(main())
