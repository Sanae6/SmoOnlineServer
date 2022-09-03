using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;

namespace Server.JsonApi;

public static class BlockClients
{
    private const int MAX_TRIES = 5;


    private static ConcurrentDictionary<IPAddress, int> Failures = new ConcurrentDictionary<IPAddress, int>();


    public static bool IsBlocked(Context ctx) {
        if (ctx.socket.RemoteEndPoint == null) { return true; }

        IPAddress ip = (ctx.socket.RemoteEndPoint as IPEndPoint)!.Address;

        int failures = BlockClients.Failures.GetValueOrDefault(ip, 0);
        return failures >= BlockClients.MAX_TRIES;
    }


    public static void Fail(Context ctx) {
        if (ctx.socket.RemoteEndPoint == null) { return; }

        IPAddress ip = (ctx.socket.RemoteEndPoint as IPEndPoint)!.Address;

        int failures = 1;
        BlockClients.Failures.AddOrUpdate(ip, 1, (k, v) => failures = v + 1);

        if (failures == BlockClients.MAX_TRIES) {
            JsonApi.Logger.Warn($"Block client {ctx.socket.RemoteEndPoint} because of too many failed requests.");
        }
    }


    public static void Redeem(Context ctx) {
        if (ctx.socket.RemoteEndPoint == null) { return; }

        IPAddress ip = (ctx.socket.RemoteEndPoint as IPEndPoint)!.Address;

        BlockClients.Failures.Remove(ip, out int val);
    }
}
