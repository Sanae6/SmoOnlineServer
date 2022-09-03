using Server;
using Shared;
using System.Net.Sockets;
using System.Text.Json;

namespace Server.JsonApi;

public class Context {
    public Server server;
    public Socket socket;
    public ApiRequest? request;
    public Logger? logger;


    public Context(
        Server server,
        Socket socket
    ) {
        this.server = server;
        this.socket = socket;
    }


    public bool HasPermission(string perm) {
        if (this.request == null) { return false; }
        return Settings.Instance.JsonApi.Tokens[this.request!.Token!].Contains(perm);
    }


    public SortedSet<string> Permissions {
        get {
            if (this.request == null) { return new SortedSet<string>(); }
            return Settings.Instance.JsonApi.Tokens[this.request!.Token!];
        }
    }


    public async Task Send(object data) {
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(data);
        await this.socket.SendAsync(bytes, SocketFlags.None);
    }
}
