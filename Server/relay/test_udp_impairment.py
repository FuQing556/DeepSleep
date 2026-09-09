"""Development-only one-client UDP delay/loss proxy; never deploy as a public relay."""
import asyncio
import random


class Impairment(asyncio.DatagramProtocol):
    def __init__(self):
        self.client = None
        self.random = random.Random(90209)
        self.received = self.dropped = 0

    def connection_made(self, transport):
        self.transport = transport

    def datagram_received(self, data, address):
        self.received += 1
        if address == ("127.0.0.1",7777):
            destination = self.client
        else:
            self.client = address
            destination = ("127.0.0.1",7777)
        if destination is None:
            return
        if self.random.random() < .03:
            self.dropped += 1
            return
        delay = .06 + self.random.uniform(-.02,.02)
        asyncio.get_running_loop().call_later(delay,self.transport.sendto,data,destination)


async def main():
    transport, proxy = await asyncio.get_running_loop().create_datagram_endpoint(Impairment,local_addr=("127.0.0.1",7778))
    print("UDP impairment ready: 60ms +/-20ms per direction, 3% loss",flush=True)
    try:
        await asyncio.sleep(90)
    finally:
        print(f"received={proxy.received} dropped={proxy.dropped}",flush=True)
        transport.close()


if __name__ == "__main__":
    asyncio.run(main())
