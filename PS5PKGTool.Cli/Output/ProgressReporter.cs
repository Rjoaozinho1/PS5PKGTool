using PS5PKGTool.Core.Services;
using PS5PKGTool.Ffpfsc;

namespace PS5PKGTool.Cli.Output;

internal enum ProgressMode
{
    /// <summary>No progress output (--quiet).</summary>
    Off,

    /// <summary>One line per stage change, for logs and pipes.</summary>
    Lines,

    /// <summary>A single line rewritten in place with '\r'.</summary>
    Terminal
}

/// <summary>
/// Writes progress to stderr. The library reports from thread-pool threads (it wraps reporters in
/// <see cref="Progress{T}"/>), sometimes after the operation has finished, so every call takes a lock
/// and reports after <see cref="Finish"/> are dropped.
/// </summary>
internal sealed class ProgressReporter : IProgress<Ps5ImageConversionProgress>, IProgress<Ps5ScanProgress>
{
    private const long RedrawIntervalMs = 100;

    private readonly TextWriter _error;
    private readonly ProgressMode _mode;
    private readonly Func<long> _clockMs;
    private readonly object _gate = new();
    private string? _stage;
    private long _lastDrawMs;
    private int _lastWidth;
    private bool _lineOpen;
    private bool _finished;

    public ProgressReporter(TextWriter error, ProgressMode mode, Func<long>? clockMs = null)
    {
        _error = error;
        _mode = mode;
        _clockMs = clockMs ?? (() => Environment.TickCount64);
    }

    public void Report(Ps5ImageConversionProgress value) => Update(value.Stage, value.Completed, value.Total, sizes: true);

    public void Report(Ps5ScanProgress value) => Update("Scanning", value.Processed, value.Total, sizes: false);

    /// <summary>
    /// Ends progress after success. In terminal mode the line is erased rather than kept, because throttled
    /// redraws can leave a stale percentage (such as "Verifying 0%") above the result line.
    /// </summary>
    public void Complete()
    {
        lock (_gate)
        {
            if (_finished) return;
            _finished = true;
            if (_lineOpen) _error.Write("\r" + new string(' ', _lastWidth) + "\r");
        }
    }

    /// <summary>Ends the progress line. Safe to call more than once.</summary>
    public void Finish()
    {
        lock (_gate)
        {
            if (_finished) return;
            _finished = true;
            if (_lineOpen) _error.WriteLine();
        }
    }

    internal static string Format(string stage, long completed, long total, bool sizes)
    {
        if (total <= 0) return stage;
        long percent = Math.Clamp(completed * 100 / total, 0, 100);
        string amount = sizes
            ? $"{SizeFormat.Bytes(completed)} / {SizeFormat.Bytes(total)}"
            : $"{completed} / {total}";
        return $"{stage}  {percent}%  ({amount})";
    }

    private void Update(string stage, long completed, long total, bool sizes)
    {
        lock (_gate)
        {
            if (_mode == ProgressMode.Off || _finished) return;
            bool stageChanged = !string.Equals(stage, _stage, StringComparison.Ordinal);
            _stage = stage;

            if (_mode == ProgressMode.Lines)
            {
                if (stageChanged) _error.WriteLine(stage);
                return;
            }

            long now = _clockMs();
            if (!stageChanged && now - _lastDrawMs < RedrawIntervalMs) return;
            _lastDrawMs = now;
            string text = Format(stage, completed, total, sizes);
            _error.Write("\r" + text.PadRight(_lastWidth));
            _lastWidth = text.Length;
            _lineOpen = true;
        }
    }
}
