using System.Net.WebSockets;
using System.Text;
using ExtShiftingApp.M2;

namespace ExtShiftingApp.Repl;

public static class ReplEndpoint
{
    public static void MapRepl(this WebApplication app)
    {
        app.UseWebSockets();

        app.Map("/repl", async (HttpContext context, M2ProcessRunner m2) =>
        {
            if (!context.WebSockets.IsWebSocketRequest)
            {
                context.Response.StatusCode = 400;
                return;
            }

            using var ws = await context.WebSockets.AcceptWebSocketAsync();
            await using var session = m2.StartInteractiveSession();

            // Forward M2 output → WebSocket
            session.OutputReceived += async (_, line) =>
            {
                if (ws.State == WebSocketState.Open)
                {
                    var bytes = Encoding.UTF8.GetBytes(line + "\r\n");
                    await ws.SendAsync(bytes, WebSocketMessageType.Text, endOfMessage: true, CancellationToken.None);
                }
            };

            // Forward WebSocket input → M2 stdin
            var buffer = new byte[4096];
            while (ws.State == WebSocketState.Open)
            {
                var received = await ws.ReceiveAsync(buffer, context.RequestAborted);
                if (received.MessageType == WebSocketMessageType.Close) break;

                var line = Encoding.UTF8.GetString(buffer, 0, received.Count).TrimEnd('\n', '\r');
                await session.SendInputAsync(line, context.RequestAborted);
            }
        });
    }
}
