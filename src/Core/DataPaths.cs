using System;
using System.IO;

namespace StalkerALifeSandbox.Core;

/// <summary>
/// Resolves paths to bundled data files relative to the application's base
/// directory — the folder the build copies <c>data/**</c> into — rather than
/// the current working directory. This makes data loading independent of where
/// the process is launched from.
/// </summary>
public static class DataPaths
{
    /// <summary>Absolute path to the bundled data directory.</summary>
    public static string Root { get; } = Path.Combine(AppContext.BaseDirectory, "data");

    /// <summary>
    /// Resolve a path inside the data directory, e.g.
    /// <c>DataPaths.Resolve("items", "ammo.json")</c>.
    /// </summary>
    public static string Resolve(params string[] segments)
    {
        string path = Root;
        foreach (var segment in segments)
            path = Path.Combine(path, segment);
        return path;
    }

    /// <summary>
    /// Resolve a path to a data file that must exist. Throws a clear
    /// <see cref="FileNotFoundException"/> at load time instead of failing
    /// later with an empty collection or a null reference.
    /// </summary>
    public static string Require(params string[] segments)
    {
        string path = Resolve(segments);
        if (!File.Exists(path) && !Directory.Exists(path))
            throw new FileNotFoundException(
                $"Required data file not found: '{path}'. Ensure data/** is present and " +
                "copied to the output directory (see the Content rule in the .csproj).",
                path);
        return path;
    }
}
