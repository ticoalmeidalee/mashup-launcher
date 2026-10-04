"""Oracle for the Minecraft guns (no GTA): each trigger pull must reach the host as {"t":"shoot"}, semi-auto guns
once per click, auto guns repeatedly while held; holding one is reported in {"t":"screen","gun":1}.
Optional: screenshot the Minecraft window in first and third person (pass an output folder).
Usage: python guns_test.py [port] [shot folder]"""
import asyncio, json, os, subprocess, sys, time
import websockets

if len(sys.argv) < 2:
    sys.exit("give the port explicitly (25599 is the live game)")
PORT = int(sys.argv[1])
SHOTS = sys.argv[2] if len(sys.argv) > 2 else None
F5 = (62, 62 | 0x40000000)


def screenshot(path):
    """PrintWindow the Minecraft window (works while it is behind other windows)."""
    ps = r"""
Add-Type -AssemblyName System.Drawing
Add-Type @'
using System; using System.Runtime.InteropServices; using System.Text;
public static class W {
 public delegate bool P(IntPtr h, IntPtr l);
 [DllImport("user32.dll")] public static extern bool EnumWindows(P p, IntPtr l);
 [DllImport("user32.dll")] public static extern int GetWindowText(IntPtr h, StringBuilder s, int n);
 [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
 [DllImport("user32.dll")] public static extern bool GetClientRect(IntPtr h, out R r);
 [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr h, IntPtr dc, uint f);
 public struct R { public int L,T,Rt,B; }
 public static IntPtr Find(string t) { IntPtr f = IntPtr.Zero; EnumWindows((h,l) => { var s = new StringBuilder(256); GetWindowText(h, s, 256);
   if (IsWindowVisible(h) && s.ToString().StartsWith(t)) { f = h; return false; } return true; }, IntPtr.Zero); return f; }
}
'@
$h = [W]::Find('Minecraft'); $r = New-Object W+R; [W]::GetClientRect($h, [ref]$r) | Out-Null
$b = New-Object Drawing.Bitmap $r.Rt, $r.B; $g = [Drawing.Graphics]::FromImage($b); $dc = $g.GetHdc()
[W]::PrintWindow($h, $dc, 3) | Out-Null; $g.ReleaseHdc($dc); $b.Save('PATH')
""".replace("PATH", path)
    subprocess.run(["powershell", "-NoProfile", "-Command", ps], check=False)


async def main():
    async with websockets.connect(f"ws://127.0.0.1:{PORT}") as ws:
        state = {"shots": [], "gun": None}

        async def reader():
            async for m in ws:
                d = json.loads(m)
                if d.get("t") == "shoot":
                    state["shots"].append(d["w"])
                elif d.get("t") == "screen":
                    state["gun"] = d.get("gun")

        async def cam():
            f = 0
            while True:
                f += 1
                await ws.send(json.dumps({"t": "cam", "f": f, "p": [0.5, 65.62, 0.5], "r": [0, 5, 0], "fov": 70, "fp": True,
                                          "pl": [0.5, 64.0, 0.5], "h": 0.0, "gun": False}))
                await asyncio.sleep(1 / 60)

        tasks = [asyncio.create_task(reader()), asyncio.create_task(cam())]
        await ws.send(json.dumps({"t": "ground", "c": [v for x in range(-4, 5) for z in range(-4, 5) for v in (x, z, 63, 63)]}))
        await ws.send(json.dumps({"t": "cmd", "c": "item replace entity @a hotbar.0 with passthrough:pistol"}))
        await ws.send(json.dumps({"t": "cmd", "c": "item replace entity @a hotbar.3 with passthrough:assault_rifle"}))
        await asyncio.sleep(1.0)
        results = []

        def check(name, ok):
            results.append(ok)
            print(("PASS " if ok else "FAIL ") + name)

        async def use(hold):
            await ws.send(json.dumps({"t": "key", "k": "use", "down": True}))
            await asyncio.sleep(hold)
            await ws.send(json.dumps({"t": "key", "k": "use", "down": False}))
            await asyncio.sleep(0.4)

        await ws.send(json.dumps({"t": "slot", "n": 0}))
        await asyncio.sleep(2.2)  # the screen/gun report repeats every 2 s at most
        check("holding the pistol is reported (gun=1)", state["gun"] == 1)
        if SHOTS:
            screenshot(os.path.join(SHOTS, "pistol_fp.png"))
        state["shots"].clear()
        await use(0.1)
        check("one click of the pistol = one shot", state["shots"] == ["pistol"])
        state["shots"].clear()
        await use(0.1)
        await use(0.1)
        check("two more clicks after the cooldown = two shots", state["shots"] == ["pistol", "pistol"])

        await ws.send(json.dumps({"t": "slot", "n": 3}))
        await asyncio.sleep(0.5)
        if SHOTS:
            screenshot(os.path.join(SHOTS, "rifle_fp.png"))
        state["shots"].clear()
        await use(1.0)
        n = len(state["shots"])
        check(f"holding the assault rifle for 1 s fires automatically ({n} shots, expect ~6-7)", 5 <= n <= 8 and set(state["shots"]) == {"assault_rifle"})

        if SHOTS:  # third person (F5) for Steve's two-handed aim
            await ws.send(json.dumps({"t": "kbd", "sc": F5[0], "kc": F5[1], "m": 0, "a": 1}))
            await ws.send(json.dumps({"t": "kbd", "sc": F5[0], "kc": F5[1], "m": 0, "a": 0}))
            await asyncio.sleep(0.6)
            screenshot(os.path.join(SHOTS, "rifle_tp.png"))
            for _ in range(2):  # back to first person
                await ws.send(json.dumps({"t": "kbd", "sc": F5[0], "kc": F5[1], "m": 0, "a": 1}))
                await ws.send(json.dumps({"t": "kbd", "sc": F5[0], "kc": F5[1], "m": 0, "a": 0}))
                await asyncio.sleep(0.2)

        await ws.send(json.dumps({"t": "slot", "n": 1}))
        await asyncio.sleep(2.2)
        check("switching to the sword clears it (gun=0)", state["gun"] == 0)
        for t in tasks:
            t.cancel()
        print(f"{sum(results)}/{len(results)} passed")


asyncio.run(main())
