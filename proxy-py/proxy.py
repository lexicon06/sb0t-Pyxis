"""
sb0t WebSocket proxy relay (compatible with /scriptProxy.py pattern).

Client connects to wss://host/scriptProxy.py?name=sala&ip=1.2.3.4&port=62175
and this endpoint tunnels the WebSocket to the sb0t web port, injecting the
real client IP via X-Real-IP / X-Forwarded-For so sb0t shows the user's IP
(a trusted proxy is required for that to be applied).
"""

import asyncio
import os

from fastapi import FastAPI, Query, WebSocket
from websockets.asyncio.client import connect as ws_connect


app = FastAPI()

SB0T_HOST = os.getenv("SB0T_HOST", "127.0.0.1")
SB0T_PORT = int(os.getenv("SB0T_PORT", "62175"))
TOKEN = os.getenv("PROXY_TOKEN", "")


def _headers_kwarg(headers):
    try:
        from websockets import __version__

        major = int(__version__.split(".")[0])
        if major >= 14:
            return {"additional_headers": headers}
    except Exception:
        pass

    return {"extra_headers": headers}


async def _relay_to_sb0t(browser_ws, host, port, real_ip):
    uri = f"ws://{host}:{port}"
    kwargs = dict(open_timeout=10)
    kwargs.update(_headers_kwarg({"X-Real-IP": real_ip, "X-Forwarded-For": real_ip}))

    async with ws_connect(uri, **kwargs) as upstream:
        to_up = asyncio.create_task(_browser_to_upstream(browser_ws, upstream))
        to_bro = asyncio.create_task(_upstream_to_browser(upstream, browser_ws))
        done, pending = await asyncio.wait(
            {to_up, to_bro}, return_when=asyncio.FIRST_COMPLETED
        )
        for task in pending:
            task.cancel()


async def _browser_to_upstream(browser_ws, upstream):
    try:
        while True:
            message = await browser_ws.receive()
            if message["type"] == "websocket.disconnect":
                await upstream.close()
                return
            if message["type"] != "websocket.receive":
                continue
            if message.get("bytes") is not None:
                await upstream.send(message["bytes"])
            elif message.get("text") is not None:
                await upstream.send(message["text"])
    except Exception:
        try:
            await upstream.close()
        except Exception:
            pass


async def _upstream_to_browser(upstream, browser_ws):
    try:
        async for message in upstream:
            if isinstance(message, bytes):
                await browser_ws.send_bytes(message)
            else:
                await browser_ws.send_text(message)
    except Exception:
        try:
            await browser_ws.close()
        except Exception:
            pass


@app.get("/")
async def root():
    return {"endpoint": "/scriptProxy.py?name=sala&ip=...&port=...", "status": "ok"}


@app.websocket("/scriptProxy.py")
async def script_proxy(
    ws: WebSocket,
    name: str = Query(""),
    ip: str = Query(""),
    port: int = Query(0),
    token: str = Query(""),
):
    if TOKEN and token != TOKEN:
        await ws.close(code=1008)
        return

    await ws.accept()

    host = ip or SB0T_HOST
    target_port = port or SB0T_PORT
    real_ip = ws.client[0] if ws.client else SB0T_HOST

    try:
        await _relay_to_sb0t(ws, host, target_port, real_ip)
    except Exception:
        pass
    finally:
        try:
            await ws.close()
        except Exception:
            pass