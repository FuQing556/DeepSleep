"""Private two-player relay. No game simulation, user database, or stored payloads."""
import asyncio
import json
import os
import re
import time
from collections import Counter
from http import HTTPStatus
from dataclasses import dataclass
from websockets.asyncio.server import serve
from websockets.exceptions import ConnectionClosed


@dataclass
class Room:
    host: object
    version: str
    guest: object = None
    ticket: str = ""


rooms = {}
connections = Counter()
MAX_ROOMS = int(os.getenv("MAX_ROOMS", "32"))
BYTES_PER_SECOND = int(os.getenv("BYTES_PER_SECOND", "1048576"))


def health_response(connection, request):
    """平台健康检查只证明进程存活，不泄露房间或身份。"""
    if request.path == "/health":
        return connection.respond(HTTPStatus.OK, "ok\n")
    return None


async def reject(ws, reason):
    await ws.send("ERROR:" + reason)
    await ws.close(code=1008)


async def handle(ws):
    ip = ws.remote_address[0]
    if connections[ip] >= 8 or sum(connections.values()) >= 128:
        await reject(ws, "Connection limit")
        return
    connections[ip] += 1
    room = None
    host = False
    code = ""
    try:
        raw = await asyncio.wait_for(ws.recv(), 8)
        if not isinstance(raw, str) or len(raw) > 512:
            await reject(ws, "Invalid registration")
            return
        data = json.loads(raw)
        if not isinstance(data, dict):
            await reject(ws, "Invalid registration object")
            return
        code, ticket, version = data.get("room", ""), data.get("ticket", ""), data.get("version", "")
        if not all(isinstance(v, str) for v in (code, ticket, version)) or not re.fullmatch(r"[A-Za-z0-9_-]{8,32}", code) or not re.fullmatch(r"[a-f0-9]{32}", ticket) or not 1 <= len(version) <= 100:
            await reject(ws, "Invalid room, identity or version")
            return
        if data.get("action") == "host":
            if code in rooms or len(rooms) >= MAX_ROOMS:
                await reject(ws, "Room exists or server full")
                return
            host = True
            room = rooms[code] = Room(ws, version)
            print("room_created active=" + str(len(rooms)), flush=True)
            await ws.send("READY")
        elif data.get("action") == "join":
            room = rooms.get(code)
            reason = "Room not found" if room is None else "Version mismatch" if room.version != version else "Room full" if room.guest is not None else "Slot reserved" if room.ticket and room.ticket != ticket else ""
            if reason:
                room = None
                print("join_rejected " + reason, flush=True)
                await reject(ws, reason)
                return
            room.ticket, room.guest = ticket, ws
            await ws.send("READY")
            await room.host.send("PEER")
            print("guest_joined", flush=True)
        else:
            await reject(ws, "Unknown action")
            return
        window = time.monotonic()
        total = 0
        async for message in ws:
            if not isinstance(message, bytes) or not 1 <= len(message) <= 16384:
                await reject(ws, "Invalid gameplay frame")
                break
            now = time.monotonic()
            if now - window >= 1:
                window, total = now, 0
            total += len(message)
            if total > BYTES_PER_SECOND:
                await reject(ws, "Bandwidth limit")
                break
            peer = room.guest if host else room.host
            if peer is not None:
                try:
                    await asyncio.wait_for(peer.send(message), 2)
                except (ConnectionClosed, asyncio.TimeoutError):
                    await peer.close()
    except (ConnectionClosed, asyncio.TimeoutError, ValueError, TypeError):
        pass
    finally:
        connections[ip] -= 1
        if not connections[ip]:
            del connections[ip]
        if room is not None and rooms.get(code) is room:
            if host:
                del rooms[code]
                if room.guest is not None:
                    await room.guest.close(code=1001, reason="Host left")
            elif room.guest is ws:
                room.guest = None
                try:
                    await room.host.send("LEFT")
                except ConnectionClosed:
                    pass


async def main():
    # Bind loopback by default. Container explicitly opts into its private interface.
    async with serve(handle, os.getenv("BIND", "127.0.0.1"), int(os.getenv("PORT", "8765")),
                     max_size=16384, max_queue=32, compression=None, ping_interval=10,
                     ping_timeout=10, close_timeout=3, process_request=health_response) as server:
        print("DeepSleep relay ready", flush=True)
        await server.serve_forever()


if __name__ == "__main__":
    asyncio.run(main())
