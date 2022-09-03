using System.Net.Sockets;

using System.Text;
using System.Text.Json;

using Shared;

namespace Server.JsonApi;

public class ApiPacket {
    public const ushort MAX_PACKET_SIZE = 512; // in bytes (including 20 byte header)


    public ApiRequest? API_JSON_REQUEST { get; set; }


    public static async Task<ApiPacket?> Read(Context ctx, string header) {
        string reqStr = header + await ApiPacket.GetRequestStr(ctx);

        ApiPacket? p = null;
        try { p = JsonSerializer.Deserialize<ApiPacket>(reqStr); }
        catch {
            JsonApi.Logger.Warn($"Invalid packet deserialize from {ctx.socket.RemoteEndPoint}: {reqStr}.");
            return null;
        }

        if (p == null) {
            JsonApi.Logger.Warn($"Invalid packet from {ctx.socket.RemoteEndPoint}: {reqStr}.");
            return null;
        }

        if (p.API_JSON_REQUEST == null) {
            JsonApi.Logger.Warn($"Invalid request from {ctx.socket.RemoteEndPoint}: {reqStr}.");
            return null;
        }

        return p;
    }


    private static async Task<string> GetRequestStr(Context ctx) {
        byte[] buffer = new byte[ApiPacket.MAX_PACKET_SIZE - Constants.HeaderSize];
        int size = await ctx.socket.ReceiveAsync(buffer, SocketFlags.None);
        return Encoding.UTF8.GetString(buffer, 0, size);
    }
}

