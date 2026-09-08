using System.Diagnostics;
using System.Globalization;
using System.Text;
using Newtonsoft.Json;
using Nop.Core.Domain.Directory;

namespace Nop.Services.Catalog;

/// <summary>
/// Configuration for the Rust round sidecar
/// </summary>
public static class RoundSidecarSettings
{
    public const string UseRustRoundEnvironmentVariable = "NOP_USE_RUST_ROUND";
    public const string SidecarPathEnvironmentVariable = "NOP_ROUND_SIDECAR_PATH";
    public const string SidecarTimeoutMsEnvironmentVariable = "NOP_ROUND_SIDECAR_TIMEOUT_MS";

    public static bool IsEnabled =>
        string.Equals(
            Environment.GetEnvironmentVariable(UseRustRoundEnvironmentVariable),
            "1",
            StringComparison.Ordinal);

    public static TimeSpan Timeout =>
        int.TryParse(Environment.GetEnvironmentVariable(SidecarTimeoutMsEnvironmentVariable), out var timeoutMs)
            ? TimeSpan.FromMilliseconds(Math.Max(timeoutMs, 100))
            : TimeSpan.FromSeconds(2);
}

/// <summary>
/// JSON-lines sidecar client for Rust round implementation
/// </summary>
public sealed class RoundSidecarClient : IRoundSidecarClient, IDisposable
{
    private readonly object _sync = new();
    private Process _process;
    private StreamWriter _stdin;
    private StreamReader _stdout;
    private ulong _nextRequestId;

    public decimal Round(decimal value, RoundingType roundingType)
    {
        lock (_sync)
        {
            EnsureProcessStarted();

            var requestId = ++_nextRequestId;
            var request = new RoundSidecarRequest
            {
                Id = requestId,
                Value = value.ToString(CultureInfo.InvariantCulture),
                RoundingType = roundingType.ToString()
            };

            var payload = JsonConvert.SerializeObject(request);
            _stdin.WriteLine(payload);
            _stdin.Flush();

            var deadline = DateTime.UtcNow.Add(RoundSidecarSettings.Timeout);
            while (DateTime.UtcNow < deadline)
            {
                if (_stdout.EndOfStream)
                    throw new InvalidOperationException("Round sidecar exited unexpectedly");

                var line = _stdout.ReadLine();
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                var response = JsonConvert.DeserializeObject<RoundSidecarResponse>(line)
                    ?? throw new InvalidOperationException("Round sidecar returned invalid JSON");

                if (response.Id != requestId)
                    continue;

                if (!string.IsNullOrEmpty(response.Error))
                    throw new InvalidOperationException(response.Error);

                if (string.IsNullOrEmpty(response.Result))
                    throw new InvalidOperationException("Round sidecar returned an empty result");

                return decimal.Parse(response.Result, CultureInfo.InvariantCulture);
            }

            throw new TimeoutException("Round sidecar request timed out");
        }
    }

    public void Dispose()
    {
        lock (_sync)
            StopProcess();
    }

    private void EnsureProcessStarted()
    {
        if (_process is { HasExited: false })
            return;

        StopProcess();

        var sidecarPath = ResolveSidecarPath();
        if (!File.Exists(sidecarPath))
            throw new FileNotFoundException($"Round sidecar binary not found at {sidecarPath}", sidecarPath);

        var startInfo = new ProcessStartInfo
        {
            FileName = sidecarPath,
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardInputEncoding = Encoding.UTF8
        };

        _process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"Failed to start round sidecar at {sidecarPath}");

        _stdin = _process.StandardInput;
        _stdout = _process.StandardOutput;
    }

    private static string ResolveSidecarPath()
    {
        var configured = Environment.GetEnvironmentVariable(RoundSidecarSettings.SidecarPathEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(configured))
            return configured;

        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var release = Path.Combine(dir.FullName, "nop-round", "target", "release", "nop-round-sidecar");
            if (File.Exists(release))
                return release;

            var debug = Path.Combine(dir.FullName, "nop-round", "target", "debug", "nop-round-sidecar");
            if (File.Exists(debug))
                return debug;
        }

        return Path.Combine("nop-round", "target", "release", "nop-round-sidecar");
    }

    private void StopProcess()
    {
        try
        {
            _stdin?.Dispose();
        }
        catch
        {
            // ignored
        }

        try
        {
            _stdout?.Dispose();
        }
        catch
        {
            // ignored
        }

        if (_process is null)
            return;

        try
        {
            if (!_process.HasExited)
                _process.Kill(entireProcessTree: true);
        }
        catch
        {
            // ignored
        }
        finally
        {
            _process.Dispose();
            _process = null;
            _stdin = null;
            _stdout = null;
        }
    }

    private sealed class RoundSidecarRequest
    {
        [JsonProperty("id")]
        public ulong Id { get; set; }

        [JsonProperty("value")]
        public string Value { get; set; }

        [JsonProperty("rounding_type")]
        public string RoundingType { get; set; }
    }

    private sealed class RoundSidecarResponse
    {
        [JsonProperty("id")]
        public ulong Id { get; set; }

        [JsonProperty("result")]
        public string Result { get; set; }

        [JsonProperty("error")]
        public string Error { get; set; }
    }
}
