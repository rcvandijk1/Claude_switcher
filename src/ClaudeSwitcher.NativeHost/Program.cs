using System.Diagnostics;
using System.IO.Pipes;

namespace ClaudeSwitcher.NativeHost;

/// <summary>
/// Chrome native-messaging host. Chrome launches this process per extension
/// connection, piping stdin/stdout. We forward those messages to the long-lived
/// WPF tray app over the named pipe <c>\\.\pipe\ClaudeSwitcher</c>. If the app
/// isn't running, we launch it first.
///
/// Native messaging frame: 4-byte little-endian length, then UTF-8 JSON.
/// We use the same framing on the named pipe so the forwarder is byte-symmetric.
/// </summary>
internal static class Program
{
    private const string PipeName = "ClaudeSwitcher";
    private const string AppExe = "ClaudeSwitcher.App.exe";
    private const int MaxMessageBytes = 1024 * 1024;

    private static async Task<int> Main()
    {
        try
        {
            using var pipe = await ConnectOrLaunchAsync();

            using var stdin = Console.OpenStandardInput();
            using var stdout = Console.OpenStandardOutput();

            var stdinToPipe = ForwardAsync(stdin, pipe);
            var pipeToStdout = ForwardAsync(pipe, stdout);

            await Task.WhenAny(stdinToPipe, pipeToStdout);
            return 0;
        }
        catch (Exception ex)
        {
            try
            {
                var log = Path.Combine(Path.GetTempPath(), "claude-switcher-host.log");
                File.AppendAllText(log, $"[{DateTime.UtcNow:O}] {ex}\n");
            }
            catch { }
            return 1;
        }
    }

    private static async Task<NamedPipeClientStream> ConnectOrLaunchAsync()
    {
        var pipe = new NamedPipeClientStream(".", PipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        try
        {
            await pipe.ConnectAsync(500);
            return pipe;
        }
        catch (TimeoutException) { }

        TryLaunchApp();

        for (var i = 0; i < 30; i++)
        {
            try
            {
                await pipe.ConnectAsync(500);
                return pipe;
            }
            catch (TimeoutException) { }
        }
        throw new IOException("Tray app did not start within timeout.");
    }

    private static void TryLaunchApp()
    {
        var hostDir = AppContext.BaseDirectory;
        var candidates = new[]
        {
            Path.Combine(hostDir, AppExe),
            Path.Combine(hostDir, "..", "ClaudeSwitcher.App", AppExe),
            Path.Combine(hostDir, "..", "..", "..", "..", "ClaudeSwitcher.App", "bin", "Debug", "net9.0-windows", AppExe),
        };
        foreach (var c in candidates)
        {
            var full = Path.GetFullPath(c);
            if (!File.Exists(full)) continue;
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = full,
                    UseShellExecute = true,
                    WorkingDirectory = Path.GetDirectoryName(full)
                });
                return;
            }
            catch { }
        }
    }

    private static async Task ForwardAsync(Stream from, Stream to)
    {
        var header = new byte[4];
        while (true)
        {
            if (!await ReadExactAsync(from, header, 0, 4)) return;
            var len = BitConverter.ToInt32(header, 0);
            if (len <= 0 || len > MaxMessageBytes) return;

            var buf = new byte[len];
            if (!await ReadExactAsync(from, buf, 0, len)) return;

            await to.WriteAsync(header.AsMemory(0, 4));
            await to.WriteAsync(buf.AsMemory(0, len));
            await to.FlushAsync();
        }
    }

    private static async Task<bool> ReadExactAsync(Stream s, byte[] buf, int offset, int count)
    {
        while (count > 0)
        {
            var n = await s.ReadAsync(buf.AsMemory(offset, count));
            if (n == 0) return false;
            offset += n;
            count -= n;
        }
        return true;
    }
}
