using System.Buffers;
using System.Net.Sockets;
using System.Text;

using Server;

using Shared;
using Shared.Packet;

namespace Server.JsonApi;


public static class JsonApi {
    public const ushort PACKET_TYPE = 0x5453; // ascii "ST" (0x53 0x54) from preamble, but swapped because of endianness
    public const string PREAMBLE = "{\"API_JSON_REQUEST\":";


    public static readonly Logger Logger = new Logger("JsonApi");


    public static async Task<bool> HandleAPIRequest(
        Server server,
        Socket socket,
        PacketHeader header,
        IMemoryOwner<byte> memory
    ) {
        // check if it is enabled
        if (!Settings.Instance.JsonApi.Enabled) {
            return false;
        }

        // check packet type
        if ((ushort) header.Type != JsonApi.PACKET_TYPE) {
            server.Logger.Warn($"Accepted connection for client {socket.RemoteEndPoint}");
            return false;
        }

        // check entire header length
        string headerStr = Encoding.UTF8.GetString(memory.Memory.Span[..Constants.HeaderSize].ToArray());
        if (headerStr != JsonApi.PREAMBLE) {
            server.Logger.Warn($"Accepted connection for client {socket.RemoteEndPoint}");
            return false;
        }

        Context ctx = new Context(server, socket);

        // not if there were too many failed attempts in the past
        if (BlockClients.IsBlocked(ctx)) {
            JsonApi.Logger.Info($"Rejected blocked client {socket.RemoteEndPoint}.");
            return true;
        }

        // receive & parse JSON
        ApiPacket? p = await ApiPacket.Read(ctx, headerStr);
        if (p == null) {
            BlockClients.Fail(ctx);
            return true;
        }

        // verify basic request structure & token
        ApiRequest req = p.API_JSON_REQUEST!;
        ctx.request = req;
        if (!req.IsValid(ctx)) {
            BlockClients.Fail(ctx);
            return true;
        }

        // process request
        if (!await req.Process(ctx)) {
            BlockClients.Fail(ctx);
            return true;
        }

        BlockClients.Redeem(ctx);
        return true;
    }
}
