import asyncio
import json
import unittest
from websockets.asyncio.client import connect
from websockets.asyncio.server import serve
import relay


class RelayTests(unittest.IsolatedAsyncioTestCase):
    async def asyncSetUp(self):
        relay.rooms.clear()
        self.server = await serve(relay.handle,"127.0.0.1",0,max_size=16384)
        self.url = "ws://127.0.0.1:" + str(self.server.sockets[0].getsockname()[1])
        self.clients = []

    async def asyncTearDown(self):
        for ws in self.clients:
            await ws.close()
        self.server.close()
        await self.server.wait_closed()

    async def client(self, action, ticket="a"*32, version="1|test|test"):
        ws = await connect(self.url)
        self.clients.append(ws)
        await ws.send(json.dumps(dict(action=action,room="test-room-123",ticket=ticket,version=version)))
        return ws

    async def test_forward_reconnect_and_identity(self):
        host = await self.client("host")
        self.assertEqual(await host.recv(),"READY")
        wrong = await self.client("join",version="wrong")
        self.assertTrue((await wrong.recv()).startswith("ERROR:"))
        guest = await self.client("join")
        self.assertEqual(await guest.recv(),"READY")
        self.assertEqual(await host.recv(),"PEER")
        await guest.send(b"command")
        self.assertEqual(await host.recv(),b"command")
        await host.send(b"snapshot")
        self.assertEqual(await guest.recv(),b"snapshot")
        third = await self.client("join",ticket="b"*32)
        self.assertTrue((await third.recv()).startswith("ERROR:"))
        await guest.close()
        self.assertEqual(await host.recv(),"LEFT")
        impostor = await self.client("join",ticket="b"*32)
        self.assertTrue((await impostor.recv()).startswith("ERROR:"))
        again = await self.client("join")
        self.assertEqual(await again.recv(),"READY")
        self.assertEqual(await host.recv(),"PEER")
        await host.close()
        await asyncio.wait_for(again.wait_closed(),2)

    async def test_duplicate_room_and_malformed(self):
        host = await self.client("host")
        self.assertEqual(await host.recv(),"READY")
        duplicate = await self.client("host")
        self.assertTrue((await duplicate.recv()).startswith("ERROR:"))
        await host.send("not binary gameplay")
        self.assertTrue((await host.recv()).startswith("ERROR:"))


if __name__ == "__main__":
    unittest.main()
