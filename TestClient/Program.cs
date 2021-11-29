using System.Net.Sockets;
using System.Runtime.InteropServices;
using Shared;
using Shared.Packet;
using Shared.Packet.Packets;

TcpClient client = new TcpClient("127.0.0.1", 1027);
Guid ownId = new Guid();
Logger logger = new Logger("Client");
NetworkStream stream = client.GetStream();


// void WritePacket(Span<byte> data, IPacket packet) {
//     MemoryMarshal.Write();
// }
// stream.Write();