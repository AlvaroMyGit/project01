using System.Diagnostics;

namespace StalkerALifeSandbox.Core;

/// <summary>
/// Accumulates wall-clock time per simulation system so the tick budget can be
/// attributed rather than guessed at.
///
/// Built because the loop measured ~89 ms/tick against a 100 ms budget with only
/// ~160 stalkers alive, and the population target is 750 — but which systems
/// spend that time was unknown. Optimising without this would be guessing, and
/// the guesses so far (that GoapWorldStateSync dominates) are exactly what needs
/// checking.
///
/// Sim-thread only, matching the SimulationLoop threading contract, so the
/// accumulators need no synchronisation. Enabled by default: a Stopwatch read
/// per system per tick is a handful of nanoseconds against millisecond budgets.
/// </summary>
public static class TickProfiler
{
    private static readonly Dictionary<string, double> _totalMs = new();
    private static readonly Dictionary<string, long> _calls = new();
    private static readonly Stopwatch _sw = Stopwatch.StartNew();

    public static bool Enabled { get; set; } = true;

    /// <summary>Times <paramref name="body"/> and attributes it to <paramref name="label"/>.</summary>
    public static void Measure(string label, Action body)
    {
        if (!Enabled) { body(); return; }

        double start = _sw.Elapsed.TotalMilliseconds;
        body();
        double elapsed = _sw.Elapsed.TotalMilliseconds - start;

        _totalMs[label] = _totalMs.GetValueOrDefault(label) + elapsed;
        _calls[label] = _calls.GetValueOrDefault(label) + 1;
    }

    /// <summary>Times a value-producing expression and attributes it.</summary>
    public static T Measure<T>(string label, Func<T> body)
    {
        if (!Enabled) return body();

        double start = _sw.Elapsed.TotalMilliseconds;
        T result = body();
        double elapsed = _sw.Elapsed.TotalMilliseconds - start;

        _totalMs[label] = _totalMs.GetValueOrDefault(label) + elapsed;
        _calls[label] = _calls.GetValueOrDefault(label) + 1;
        return result;
    }

    /// <summary>Systems by total time spent, worst first.</summary>
    public static IReadOnlyList<(string Label, double TotalMs, long Calls, double MsPerCall)> Report()
    {
        var rows = new List<(string, double, long, double)>();
        foreach (var (label, ms) in _totalMs)
        {
            long calls = _calls.GetValueOrDefault(label, 1);
            rows.Add((label, ms, calls, ms / Math.Max(1, calls)));
        }
        rows.Sort((a, b) => b.Item2.CompareTo(a.Item2));
        return rows;
    }

    public static double TotalMs
    {
        get
        {
            double sum = 0;
            foreach (var ms in _totalMs.Values) sum += ms;
            return sum;
        }
    }

    public static void Reset()
    {
        _totalMs.Clear();
        _calls.Clear();
    }
}
