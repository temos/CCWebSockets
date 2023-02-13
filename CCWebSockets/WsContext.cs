using System.Diagnostics;
using System.IO.Pipelines;
using System.Net.WebSockets;
using System.Text;

namespace CCWebSockets
{
    public class Synchronizer
    {
        const string LOCAL_PATH = "C://Users/user/Desktop/lua";

        private readonly WebSocket _ws;
        private readonly ILogger<Synchronizer> _logger;
        private readonly CancellationToken _ct;
        private readonly CCApi _ccApi;

        public string BasePath => Path.Combine(LOCAL_PATH, Id);
        public string Id { get; private set; }

        public Synchronizer(WebSocket ws, ILogger<Synchronizer> logger, CancellationToken ct)
        {
            _ws = ws;
            _logger = logger;
            _ct = ct;
            _ccApi = new CCApi(ws, ct);
        }

        public async Task HandleAsync()
        {
            Id = await _ccApi.ReceiveAsync();
            _logger.LogInformation("Computer {Id} connected", Id);

            UnlockDir(new DirectoryInfo(BasePath));

            var cloneMessage = await _ccApi.ReadCloneRequestAsync();
            if (cloneMessage.shouldClone)
                await CloneAsync(cloneMessage.manifest);

            using var watcher = new FileSystemWatcher(BasePath);
            watcher.IncludeSubdirectories = true;

            while (_ws.State == WebSocketState.Open && !_ct.IsCancellationRequested)
            {
                if (!await _ccApi.PingAsync())
                    break;

                var ev = watcher.WaitForChanged(WatcherChangeTypes.All, TimeSpan.FromSeconds(3));

                if (ev.TimedOut) continue;

                switch (ev.ChangeType)
                {
                    case WatcherChangeTypes.Created:
                        _logger.LogDebug("{Name} created", ev.Name);

                        if (Directory.Exists(Path.Combine(BasePath, ev.Name)))
                            await _ccApi.CreateDirectoryAsync(ev.Name);
                        else
                            await _ccApi.CreateFileAsync(ev.Name);
                        break;

                    case WatcherChangeTypes.Deleted:
                        _logger.LogDebug("{Name} deleted", ev.Name);

                        await _ccApi.DeleteAsync(ev.Name);
                        break;

                    case WatcherChangeTypes.Renamed:
                        _logger.LogDebug("{OldName} renamed to {NewName}", ev.OldName, ev.Name);

                        await _ccApi.MoveAsync(ev.OldName, ev.Name);
                        break;

                    case WatcherChangeTypes.Changed:
                        {
                            _logger.LogDebug("Contents of {Name} changed", ev.Name);

                            var fullPath = Path.Combine(BasePath, ev.Name);

                            //ignore change events of a directories
                            if (Directory.Exists(fullPath))
                                continue;

                            var info = new FileInfo(fullPath);
                            if (!info.Directory.Exists)
                                info.Directory.Create();

                            Thread.Sleep(10); // magic
                            using var fs = info.Open(FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                            using var reader = new StreamReader(fs);
                            var contents = reader.ReadToEnd();

                            await _ccApi.WriteAsync(ev.Name, contents);
                        }
                        break;
                }
            }
        }

        public async Task CloneAsync(string[] manifest)
        {
            //delete existing files
            var info = new DirectoryInfo(BasePath);
            foreach (var dir in info.EnumerateDirectories())
                dir.Delete(true);
            foreach (var file in info.EnumerateFiles())
                file.Delete();

            foreach (var file in manifest)
            {
                var contents = await _ccApi.ReadAsync(file);
                var fullPath = Path.Combine(BasePath, file.StartsWith("/") ? file[1..] : file);
                new FileInfo(fullPath).Directory.Create();
                File.WriteAllText(fullPath, contents);
            }
        }

        public void UnlockDir(DirectoryInfo dir)
        {
            if (!dir.Exists)
                dir.Create();

            dir.Attributes ^= FileAttributes.ReadOnly;

            foreach (var file in dir.EnumerateFiles())
                file.Attributes ^= FileAttributes.ReadOnly;

            foreach (var subdir in dir.EnumerateDirectories())
                UnlockDir(subdir);
        }

        public void LockDir(DirectoryInfo dir)
        {
            dir.Attributes |= FileAttributes.ReadOnly;

            foreach (var file in dir.EnumerateFiles())
                file.Attributes |= FileAttributes.ReadOnly;

            foreach (var subdir in dir.EnumerateDirectories())
                LockDir(subdir);
        }
    }
}
