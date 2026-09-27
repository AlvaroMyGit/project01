using System;
using System.Threading;

namespace StalkerALifeSandbox.Core;

/// <summary>
/// The simulation's single seeded random source, replacing <c>Random.Shared</c>.
///
/// Why this exists: world generation was already deterministic (StaticWorldGenerator,
/// POIPrefabStamper, RoadNetwork and friends all take a seed), but the running
/// simulation was not — 119 call sites drew from <c>Random.Shared</c>, which cannot
/// be seeded. So no run could be replayed, no crash re-entered, and no behavioural
/// regression bisected, because the two halves of a bisect differed by RNG as much
/// as by code.
///
/// The cost of not having this was concrete. Re-capturing the baseline at 7d0717d
/// showed a coherent 4-10% drift across combat, population and mission throughput
/// with death_betrayal nearly tripling, and there was no way to MEASURE whether that
/// came from the perception flip in 10509a5 or from the two changes in 7d0717d — it
/// had to be argued from the shape of the correlations instead. At a fixed seed that
/// is one run and a diff.
///
/// <para><b>Threading.</b> One stream, consumed in call order, so it is deterministic
/// only while a single thread draws from it. That matches the simulation's threading
/// contract (see <see cref="SimulationLoop"/>: the tick thread is the sole writer of
/// entity state), and world generation draws from it before the tick thread starts.
/// It does NOT survive parallelising the tick: two threads interleaving draws from
/// one stream produce a different sequence per run even at a fixed seed. Splitting
/// the behaviour tick across stalkers — the natural follow-on from a spatial index —
/// therefore needs per-entity substreams seeded from (seed, entity index), not this.
/// That is a deliberate boundary, not an oversight; <see cref="DrawCount"/> exists so
/// crossing it is detectable rather than silent.</para>
/// </summary>
public static class SimRandom
{
    private static Random _rng = new(DefaultSeed);
    private static long _draws;
    private static long _ids;

    /// <summary>
    /// Seed used when nothing sets one. Fixed rather than time-derived: a run that
    /// cannot be repeated by default is the problem this type was added to fix.
    /// </summary>
    public const int DefaultSeed = 42;

    /// <summary>The seed the current stream was built from.</summary>
    public static int Seed { get; private set; } = DefaultSeed;

    /// <summary>
    /// Draws taken since the last <see cref="Initialize"/>. Two runs at the same
    /// seed that cover the same span of game time must report the same count — if
    /// they do not, something outside this stream is steering the simulation (a
    /// second RNG, a <c>Guid</c>, filesystem ordering, or a parallel draw) and the
    /// seed is not actually pinning the run. Reported in the final debug report so
    /// the claim is checked rather than assumed.
    /// </summary>
    public static long DrawCount => Interlocked.Read(ref _draws);

    /// <summary>
    /// Rebuilds the stream from <paramref name="seed"/> and resets the counters.
    /// Call once, before world generation.
    /// </summary>
    public static void Initialize(int seed)
    {
        Seed = seed;
        _rng = new Random(seed);
        Interlocked.Exchange(ref _draws, 0);
        Interlocked.Exchange(ref _ids, 0);
    }

    /// <summary>
    /// An independent, named stream derived from the same seed.
    ///
    /// For subsystems that should respond to <c>STALKER_SEED</c> but must NOT be
    /// coupled to the order of the shared stream — weather, emissions, and the
    /// bootstrap mission pool. Those already owned private <c>Random</c> instances
    /// and were deliberately insulated from the rest of the draw sequence;
    /// MissionRegistry says so in a comment. Moving them onto the shared stream
    /// would have made adding a single draw anywhere upstream reshuffle every
    /// mission offer, which is a worse debugging story than the one being fixed.
    ///
    /// The name is hashed with FNV-1a rather than <c>string.GetHashCode</c>, which
    /// is randomised per process in .NET and would hand back a different stream on
    /// every run — the exact bug this type exists to remove.
    /// </summary>
    public static Random Stream(string name) => new(Seed ^ StableHash(name));

    /// <summary>
    /// FNV-1a. A process-stable substitute for <c>string.GetHashCode</c>, which is
    /// randomised per process in .NET and therefore returns a different value for
    /// the same string on every run.
    ///
    /// This is not a theoretical hazard. RoadNetwork.GenerateSurfaceWaypoints chose
    /// which way each road bends with
    /// <c>edgeKey.GetHashCode(StringComparison.Ordinal)</c> — the Ordinal overload
    /// fixes comparison semantics, not randomisation — so road geometry differed on
    /// every run. The rasterised road cells moved with it (484 one run, 495 the
    /// next), which changed GetMoveCost, which changed the A* paths, which moved
    /// every stalker. The world was never reproducible despite seed 42 being
    /// threaded through four generators, and it could not be seen because the
    /// unseeded simulation RNG produced far more visible noise on top of it.
    /// </summary>
    public static int StableHash(string s)
    {
        unchecked
        {
            uint h = 2166136261u;
            foreach (char c in s)
            {
                h ^= c;
                h *= 16777619u;
            }
            return (int)h;
        }
    }

    /// <summary>Reads <c>STALKER_SEED</c>, falling back to <see cref="DefaultSeed"/>.</summary>
    public static int SeedFromEnvironment()
    {
        string? raw = Environment.GetEnvironmentVariable("STALKER_SEED");
        return int.TryParse(raw, out int s) ? s : DefaultSeed;
    }

    public static double NextDouble()
    {
        Interlocked.Increment(ref _draws);
        return _rng.NextDouble();
    }

    public static float NextSingle()
    {
        Interlocked.Increment(ref _draws);
        return _rng.NextSingle();
    }

    public static int Next(int maxExclusive)
    {
        Interlocked.Increment(ref _draws);
        return _rng.Next(maxExclusive);
    }

    public static int Next(int minInclusive, int maxExclusive)
    {
        Interlocked.Increment(ref _draws);
        return _rng.Next(minInclusive, maxExclusive);
    }

    /// <summary>
    /// A short unique id, replacing <c>Guid.NewGuid().ToString()[..n]</c>.
    ///
    /// Guids were the last hole in replayability after the RNG: entity and anomaly
    /// ids differed every run, so two runs at the same seed produced logs that could
    /// not be diffed line by line even when they simulated identical worlds. A
    /// counter is deterministic, collision-free within a run, and still opaque
    /// enough to read as an id in the dashboard.
    ///
    /// Not drawn from the RNG stream on purpose: minting an id must not shift the
    /// sequence every other caller sees, or adding one log line would change the
    /// simulation.
    /// </summary>
    public static string NextId(int length = 8)
    {
        long n = Interlocked.Increment(ref _ids);
        string hex = n.ToString("x");
        return hex.Length >= length ? hex[^length..] : hex.PadLeft(length, '0');
    }
}
