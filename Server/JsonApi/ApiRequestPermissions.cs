namespace Server.JsonApi;

public static class ApiRequestPermissions {
    public static async Task<bool> Send(Context ctx) {
        await Response.Send(ctx);
        return true;
    }


    private class Response {
        public string[]? Permissions { get; set; }


        public static async Task Send(Context ctx)
        {
            Response resp = new Response();
            resp.Permissions = ctx.Permissions.ToArray();
            await ctx.Send(resp);
        }
    }
}
