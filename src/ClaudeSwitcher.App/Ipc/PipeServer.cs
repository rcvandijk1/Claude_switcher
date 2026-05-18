using System;
using System.IO;
using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ClaudeSwitcher.App.Ipc;

/// <summary>
/// Accepts one named-pipe connection at a time from <c>claude-switcher-host.exe</c>
/// (the Chrome native-messaging bridge). Messages are length-prefixed UTF-8 JSON,
/// matching Chrome's native-messaging framing so the host forwards bytes
/// unchanged in either direction.
/// </summary>
public sealed class PipeServer : IAsyncDisposable
{
    public const string PipeName = "ClaudeSwitcher";

    private readonly CancellationTokenSource _cts = new();
    private NamedPipeServerStream? _current;
    private Task? _loopTask;

    public event Action? Connected;
    public event Action? Disconnected;
    public event Action<JsonElement>? MessageReceived;

    public bool IsConnected => _current is { IsConnected: true };

    public void Start()
    {
        _loopTask = Task.Run(() => RunLoopAsync(_cts.Token));
    }

    public async ValueTask DisposeAsync()
    {
        _cts.Cancel();
        try { _current?.Dispose(); } catch { }
        if (_loopTask is not null)
        {
            try { await _loopTask; } catch { }
        }
        _cts.Dispose();
    }

    public async Task SendAsync(object message, CancellationToken ct)
    {
        var pipe = _current ?? throw new InvalidOperationException("No client connected");
        var payload = JsonSerializer.SerializeToUtf8Bytes(message);
        var header = BitConverter.GetBytes(payload.Length);
        await pipe.WriteAsync(header.AsMemory(0, 4), ct);
        await pipe.WriteAsync(payload, ct);
        await pipe.FlushAsync(ct);
    }

    private async Task RunLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            NamedPipeServerStream? srv = null;
            try
            {
                srv = CreatePipe();
                _current = srv;
                await srv.WaitForConnectionAsync(ct);
                Connected?.Invoke();
                await ServeAsync(srv, ct);
            }
            catch (OperationCanceledException) { return; }
            catch (Exception)
            {
                // swallow; loop reopens the pipe.
            }
            finally
            {
                Disconnected?.Invoke();
                try { srv?.Dispose(); } catch { }
                _current = null;
            }
        }
    }

    private static NamedPipeServerStream CreatePipe()
    {
        var sid = WindowsIdentity.GetCurrent().User!;
        var security = new PipeSecurity();
        security.AddAccessRule(new PipeAccessRule(sid, PipeAccessRights.FullControl, AccessControlType.Allow));
        return NamedPipeServerStreamAcl.Create(
            PipeName,
            PipeDirection.InOut,
            maxNumberOfServerInstances: 1,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous,
            inBufferSize: 64 * 1024,
            outBufferSize: 64 * 1024,
            pipeSecurity: security);
    }

    private async Task ServeAsync(NamedPipeServerStream pipe, CancellationToken ct)
    {
        var header = new byte[4];
        while (pipe.IsConnected && !ct.IsCancellationRequested)
        {
            if (!await ReadExactAsync(pipe, header, 0, 4, ct)) return;
            var len = BitConverter.ToInt32(header, 0);
            if (len <= 0 || len > 1024 * 1024) return;

            var buf = new byte[len];
            if (!await ReadExactAsync(pipe, buf, 0, len, ct)) return;

            try
            {
                var msg = JsonSerializer.Deserialize<JsonElement>(buf);
                MessageReceived?.Invoke(msg);
            }
            catch (JsonException) { /* ignore malformed frame */ }
        }
    }

    private static async Task<bool> ReadExactAsync(Stream s, byte[] buf, int offset, int count, CancellationToken ct)
    {
        while (count > 0)
        {
            var n = await s.ReadAsync(buf.AsMemory(offset, count), ct);
            if (n == 0) return false;
            offset += n;
            count -= n;
        }
        return true;
    }
}
