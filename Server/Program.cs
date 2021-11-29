using System.Buffers;
using System.Net.Sockets;

Server.Server server = new Server.Server();

await server.Listen(1027);