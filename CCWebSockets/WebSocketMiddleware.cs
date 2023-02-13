using System.Net.WebSockets;

namespace CCWebSockets
{
    public class WebSocketMiddleware : IMiddleware
    {
        private readonly ILogger<WebSocketMiddleware> _logger;

        public WebSocketMiddleware(ILogger<WebSocketMiddleware> logger)
        {
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context, RequestDelegate next)
        {
            if (context.WebSockets.IsWebSocketRequest)
            {
                using var websocket = await context.WebSockets.AcceptWebSocketAsync();
                var lifetime = context.RequestServices.GetRequiredService<IHostApplicationLifetime>();
                var logger = context.RequestServices.GetRequiredService<ILogger<Synchronizer>>();
                var wsContext = new Synchronizer(websocket, logger, lifetime.ApplicationStopping);

                try
                {
                    await wsContext.HandleAsync();
                }
                finally
                {
                    if (wsContext.Id is not null)
                    {
                        _logger.LogInformation("Computer {ID} disconnected", wsContext.Id);
                    }
                    else
                    {
                        _logger.LogInformation("Unknown computer disconnected");
                    }

                    wsContext.LockDir(new DirectoryInfo(wsContext.BasePath));
                    await websocket.CloseAsync(WebSocketCloseStatus.InternalServerError, null, lifetime.ApplicationStopping);
                }
            }
            else
            {
                await next(context);
            }
        }
    }
}
