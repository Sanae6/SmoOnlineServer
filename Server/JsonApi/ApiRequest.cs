namespace Server.JsonApi;

using System.Text.Json;
using System.Text.Json.Nodes;

using TypesDictionary = Dictionary<string, Func<Context, Task<bool>>>;

public class ApiRequest {
    public string? Token { get; set; }
    public string? Type { get; set; }
    public JsonNode? Data { get; set; }


    private static TypesDictionary Types = new TypesDictionary() {
        ["Status"]      = async (Context ctx) => await ApiRequestStatus.Send(ctx),
        ["Command"]     = async (Context ctx) => await ApiRequestCommand.Send(ctx),
        ["Permissions"] = async (Context ctx) => await ApiRequestPermissions.Send(ctx),
    };


    public dynamic? GetData() {
        if (this.Data == null) { return null; }
        if (this.Data is JsonArray) { return this.Data.AsArray(); } // TODO: better way?
        if (this.Data is JsonObject) { return this.Data.AsObject(); } // TODO: better way?
        if (this.Data is JsonValue) {
            JsonElement val = this.Data.GetValue<JsonElement>();
            JsonValueKind kind = val.ValueKind;
            if (kind == JsonValueKind.String) { return val.GetString(); }
            if (kind == JsonValueKind.Number) { return val.GetInt64(); } // TODO: floats
            if (kind == JsonValueKind.False)  { return false; }
            if (kind == JsonValueKind.True)   { return true; }
        }
        return null;
    }


    public async Task<bool> Process(Context ctx) {
        if (this.Type != null) {
            return await ApiRequest.Types[this.Type](ctx);
        }
        return false;
    }


    public bool IsValid(Context ctx) {
        if (this.Token == null) {
            JsonApi.Logger.Warn($"Invalid request missing Token from {ctx.socket.RemoteEndPoint}.");
            return false;
        }

        if (this.Type == null) {
            JsonApi.Logger.Warn($"Invalid request missing Type from {ctx.socket.RemoteEndPoint}.");
            return false;
        }

        if (!ApiRequest.Types.ContainsKey(this.Type)) {
            JsonApi.Logger.Warn($"Invalid Type \"{this.Type}\" from {ctx.socket.RemoteEndPoint}.");
            return false;
        }

        if (!Settings.Instance.JsonApi.Tokens.ContainsKey(this.Token)) {
            JsonApi.Logger.Warn($"Invalid Token from {ctx.socket.RemoteEndPoint}.");
            return false;
        }

        return true;
    }
}
