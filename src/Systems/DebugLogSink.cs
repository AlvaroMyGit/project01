namespace StalkerALifeSandbox.Systems;

/// <summary>
/// The actual logging mechanism (console + append-only file) behind
/// <see cref="SimulationDebugLog"/>. Split out so that class stays focused on
/// deciding WHAT to log (metrics, counters, formatted snapshots/reports)
/// rather than also owning HOW it gets written (console + file I/O, locking).
/// </summary>
public static class DebugLogSink
{
    private static readonly object _fileLock = new();
    private static string? _logPath;

    /// <summary>Path of the active log file, or null if none has been configured.</summary>
    public static string? LogPath => _logPath;

    /// <summary>Configures the log file path, creating its directory if needed.</summary>
    public static void Initialize(string logPath)
    {
        _logPath = logPath;
        Directory.CreateDirectory(Path.GetDirectoryName(_logPath)!);
    }

    /// <summary>Writes one categorized, timestamped line to the console and the log file.</summary>
    public static void WriteLine(string category, string message)
    {
        string line = $"[{DateTime.UtcNow:HH:mm:ss}] [{category}] {message}";
        Console.WriteLine(line);
        AppendRaw(line);
    }

    private static void AppendRaw(string text)
    {
        if (_logPath == null) return;
        lock (_fileLock)
            File.AppendAllText(_logPath, text + Environment.NewLine);
    }
}
