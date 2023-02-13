using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace CCWebSockets
{
    public class CCApi
    {
        private readonly WebSocket _ws;
        private readonly CancellationToken _ct;
        private Memory<byte> _buffer;

        public CCApi(WebSocket ws, CancellationToken ct)
        {
            _ws = ws;
            _ct = ct;
            _buffer = new Memory<byte>(new byte[1 * 1024 * 1024]);
        }

        public async ValueTask<string> ReceiveAsync()
        {
            var result = await _ws.ReceiveAsync(_buffer, _ct);
            var message = Encoding.ASCII.GetString(_buffer[..result.Count].Span);
            return message;
        }

        public async ValueTask<string> ReceiveAsync(TimeSpan timeout)
        {
            var timeoutCts = new CancellationTokenSource(timeout);
            var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(_ct, timeoutCts.Token);
            var result = await _ws.ReceiveAsync(_buffer, combinedCts.Token);
            var message = Encoding.ASCII.GetString(_buffer[..result.Count].Span);
            return message;
        }

        public async ValueTask SendAsync(string message)
        {
            var bytes = Encoding.ASCII.GetBytes(message);
            await _ws.SendAsync(bytes, WebSocketMessageType.Text, true, _ct);
        }

        public async ValueTask SendAsync(string message, int timeout)
        {
            var bytes = Encoding.ASCII.GetBytes(message);
            await _ws.SendAsync(bytes, WebSocketMessageType.Text, true, _ct);
        }

        public async Task CreateFileAsync(string path)
        {
            var message = "create\n";
            message += path;

            await SendAsync(message);
        }

        public async Task CreateDirectoryAsync(string path)
        {
            var message = "createdir\n";
            message += path;

            await SendAsync(message);
        }

        public async Task DeleteAsync(string path)
        {
            var message = "delete\n";
            message += path;

            await SendAsync(message);
        }

        public async Task WriteAsync(string path, string contents)
        {
            var message = "write\n";
            message += path + '\n';
            message += contents;

            await SendAsync(message);
        }

        public async Task MoveAsync(string oldPath, string newPath)
        {
            var message = "move\n";
            message += oldPath + '\n';
            message += newPath + '\n';

            await SendAsync(message);
        }

        public async Task<string> ReadAsync(string path)
        {
            var message = "read\n";
            message += path;

            await SendAsync(message);

            var response = await ReceiveAsync();

            if (response.StartsWith("ok\n"))
                return response[3..];
            else
                return null;
        }

        public async Task<bool> PingAsync()
        {
            var message = "ping\n";

            await SendAsync(message, 3);

            try
            {
                await ReceiveAsync(TimeSpan.FromSeconds(5));
                return true;
            }
            catch (OperationCanceledException)
            {
                return false;
            }
        }

        public async Task<(bool shouldClone, string[] manifest)> ReadCloneRequestAsync()
        {
            var message = await ReceiveAsync();

            if (message.Length == 0)
                return (false, null);

            var split = message.Split('\n');
            if (split[0] != "clone")
                return (false, null);

            return (true, split[1..^1]);
        }
    }
}
