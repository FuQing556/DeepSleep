"""End-to-end TLS + room forwarding smoke test against a supplied public endpoint."""
import asyncio
import json
import secrets
import sys
from urllib.parse import urlparse
from websockets.asyncio.client import connect


async def run(endpoint):
    parsed = urlparse(endpoint)
    if parsed.scheme != "wss" and not (parsed.scheme == "ws" and parsed.hostname in ("localhost", "127.0.0.1")):
        raise ValueError("Use a wss:// endpoint (ws:// is allowed only for loopback)")
    room = secrets.token_hex(10)
    ticket = secrets.token_hex(16)
    async with connect(endpoint, open_timeout=15) as host:
        await host.send(json.dumps(dict(action="host", room=room, ticket=ticket, version="public-smoke-1")))
        assert await asyncio.wait_for(host.recv(), 5) == "READY"
        async with connect(endpoint, open_timeout=15) as guest:
            await guest.send(json.dumps(dict(action="join", room=room, ticket=ticket, version="public-smoke-1")))
            assert await asyncio.wait_for(guest.recv(), 5) == "READY"
            assert await asyncio.wait_for(host.recv(), 5) == "PEER"
            await guest.send(b"command-probe")
            assert await asyncio.wait_for(host.recv(), 5) == b"command-probe"
            await host.send(b"snapshot-probe")
            assert await asyncio.wait_for(guest.recv(), 5) == b"snapshot-probe"
        assert await asyncio.wait_for(host.recv(), 5) == "LEFT"
    print("PASS: connection, room creation, join, bidirectional forwarding, guest leave")


if __name__ == "__main__":
    asyncio.run(run(sys.argv[1]))
