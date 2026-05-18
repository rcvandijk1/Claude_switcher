using System;
using System.Collections.Concurrent;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ClaudeSwitcher.Core;

namespace ClaudeSwitcher.App.Ipc;

/// <summary>
/// IChromeBridge backed by the named-pipe server. Sends capture/apply requests
/// to the connected Chrome extension and correlates responses by id.
/// </summary>
public sealed class PipeChromeBridge : IChromeBridge
{
    private readonly PipeServer _server;
    private readonly ConcurrentDictionary<string, TaskCompletionSource<JsonElement>> _pending = new();

    public PipeChromeBridge(PipeServer server)
    {
        _server = server;
        _server.MessageReceived += OnMessage;
        _server.Disconnected += OnDisconnected;
    }

    public bool IsConnected => _server.IsConnected;

    public async Task<string> CaptureCookiesJsonAsync(CancellationToken ct)
    {
        var id = Guid.NewGuid().ToString("N");
        var tcs = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending[id] = tcs;
        try
        {
            await _server.SendAsync(new { type = "capture", id }, ct);
            using var reg = ct.Register(() => tcs.TrySetCanceled(ct));
            var response = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(15), ct);
            if (response.TryGetProperty("error", out var err))
                throw new InvalidOperationException(err.GetString() ?? "extension error");
            return response.TryGetProperty("cookies", out var c) ? c.GetRawText() : "[]";
        }
        finally { _pending.TryRemove(id, out _); }
    }

    public async Task ApplyCookiesJsonAsync(string cookiesJson, CancellationToken ct)
    {
        var id = Guid.NewGuid().ToString("N");
        var tcs = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending[id] = tcs;
        try
        {
            using var doc = JsonDocument.Parse(cookiesJson);
            var payload = new
            {
                type = "apply",
                id,
                cookies = doc.RootElement
            };
            await _server.SendAsync(payload, ct);
            using var reg = ct.Register(() => tcs.TrySetCanceled(ct));
            var response = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(15), ct);
            if (response.TryGetProperty("error", out var err))
                throw new InvalidOperationException(err.GetString() ?? "extension error");
        }
        finally { _pending.TryRemove(id, out _); }
    }

    private void OnMessage(JsonElement msg)
    {
        if (!msg.TryGetProperty("type", out var typeEl)) return;
        var type = typeEl.GetString();
        if (type is "captureResult" or "applyResult"
            && msg.TryGetProperty("id", out var idEl)
            && idEl.GetString() is string id
            && _pending.TryGetValue(id, out var tcs))
        {
            tcs.TrySetResult(msg);
        }
        // "hello" and other types are advisory; no action required.
    }

    private void OnDisconnected()
    {
        foreach (var (_, tcs) in _pending)
            tcs.TrySetException(new InvalidOperationException("Chrome extension disconnected"));
        _pending.Clear();
    }
}
