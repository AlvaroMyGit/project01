using System.Text.RegularExpressions;

namespace StalkerALifeSandbox.Tests;

/// <summary>
/// The threading contract on SimulationLoop says the tick thread is the only
/// writer of entity state and that readers go through the published snapshot.
/// What it does not say is that most reads of the live lists skip EntityLock
/// entirely: a scan at the time this was written found 23 such reads against 7
/// that take the lock.
///
/// None of them is a live race. ZoneDirector runs every frequency bucket on one
/// thread, so the 1 Hz spawn cannot interleave with a 10 Hz tick and the lock is
/// currently guarding something already serialised. The risk is the next change,
/// not this one: splitting the behaviour tick across stalkers is the natural
/// follow-on from a spatial index, and it turns every unlocked read into an
/// intermittent "Collection was modified" while the locked ones give false
/// confidence that the question was considered.
///
/// This freezes the problem in the systems where it would bite first — the
/// 10 Hz ones, which are also the ones a parallel tick would split. Those must
/// take a snapshot under the lock and then read the snapshot. The 1 Hz and
/// lower systems are deliberately NOT covered yet; see the list below.
/// </summary>
public class EntityLockDisciplineTests
{
    /// <summary>
    /// Every 10 Hz system. These run inside the tick a parallel split would
    /// divide, so a live-list read here is the one that breaks first.
    /// </summary>
    public static TheoryData<string> HotPathSystems() => new()
    {
        "src/Core/Systems/StalkerBehaviourSystem.cs",
        "src/Core/Systems/MutantBehaviourSystem.cs",
        "src/Core/Systems/PerceptionSystem.cs",
        "src/Core/Systems/EmissionTickSystem.cs",
        "src/Core/Systems/TelemetrySystem.cs",
    };

    [Theory]
    [MemberData(nameof(HotPathSystems))]
    public void A10HzSystem_ReadsTheEntityListsOnlyUnderTheLock(string relativePath)
    {
        var file = RepoFile(relativePath);
        var offenders = UnlockedEntityReads(File.ReadAllText(file));

        Assert.True(offenders.Count == 0,
            $"{relativePath} reads the live entity lists outside lock (ctx.EntityLock):\n"
            + string.Join("\n", offenders.Select(o => $"  line {o.Line}: {o.Text}"))
            + "\n\nTake a snapshot under the lock at the top of Tick and read that instead — "
            + "StalkerBehaviourSystem.Tick shows the shape.");
    }

    /// <summary>
    /// The scanner has to actually work, or the theory above passes for the
    /// wrong reason. Feed it a known-bad and a known-good shape.
    /// </summary>
    [Fact]
    public void TheScanner_CatchesAnUnlockedReadAndAcceptsALockedOne()
    {
        const string bad = @"
            public void Tick(SimulationContext ctx, float d)
            {
                var x = ctx.Stalkers.FirstOrDefault(s => s.IsAlive);
            }";
        const string good = @"
            public void Tick(SimulationContext ctx, float d)
            {
                Stalker[] snap;
                lock (ctx.EntityLock) { snap = ctx.Stalkers.ToArray(); }
                var x = snap.FirstOrDefault(s => s.IsAlive);
            }";
        const string multiline = @"
            public void Tick(SimulationContext ctx, float d)
            {
                lock (ctx.EntityLock)
                {
                    foreach (var s in ctx.Stalkers) { }
                }
            }";

        Assert.Single(UnlockedEntityReads(bad));
        Assert.Empty(UnlockedEntityReads(good));
        Assert.Empty(UnlockedEntityReads(multiline));
    }

    /// <summary>
    /// Records the systems still outside the rule, so the number cannot grow
    /// unnoticed while the decision about them is pending. SocialSystem runs at
    /// 1 Hz and holds nine of them; it is the next candidate.
    /// </summary>
    [Fact]
    public void TheKnownRemainingOffenders_HaveNotMultiplied()
    {
        var known = new Dictionary<string, int>
        {
            ["src/Core/Systems/SocialSystem.cs"] = 9,
            ["src/Core/Systems/SpawnOrchestrator.cs"] = 1,
            ["src/Core/SimulationSnapshot.cs"] = 2,
            ["src/Core/SimulationLoop.cs"] = 3,
            ["src/Web/InspectorBuilder.cs"] = 1,
        };

        foreach (var (path, expected) in known)
        {
            int actual = UnlockedEntityReads(File.ReadAllText(RepoFile(path))).Count;
            Assert.True(actual <= expected,
                $"{path} now has {actual} unlocked entity reads, up from {expected}. "
                + "Fix the new one rather than raising this number.");
        }
    }

    // ── scanner ────────────────────────────────────────────────────────

    private sealed record Offence(int Line, string Text);

    /// <summary>
    /// Reads of <c>ctx.Stalkers</c> / <c>ctx.Mutants</c> that are not inside a
    /// <c>lock (ctx.EntityLock)</c> block, tracked by brace depth. String
    /// literals mentioning the names are skipped — SpawnOrchestrator logs
    /// "ctx.Stalkers" in three interpolated messages, which is not a read.
    /// </summary>
    private static List<Offence> UnlockedEntityReads(string source)
    {
        var lockOpen = new Regex(@"lock\s*\(\s*\w*\.?EntityLock\s*\)");
        var read = new Regex(@"(?<!\w)(?:ctx|_ctx)\.(?:Stalkers|Mutants)\b");

        var offences = new List<Offence>();
        int depth = 0, lockDepth = -1;
        bool inLock = false, awaitingBrace = false;
        var lines = source.Replace("\r\n", "\n").Split('\n');

        for (int i = 0; i < lines.Length; i++)
        {
            string raw = lines[i];
            string code = StripStringsAndComments(raw);
            bool opensLockHere = false;

            if (!inLock && lockOpen.IsMatch(code))
            {
                inLock = true;
                lockDepth = depth;
                opensLockHere = true;

                // The body may open on this line ("lock (x) { ... }") or on the
                // next. Until the brace appears, depth is still at lockDepth and
                // the exit check below would close the block immediately — which
                // is what the multi-line case in TheScanner_... was catching.
                awaitingBrace = !code.Contains('{');
            }

            if (!inLock && read.IsMatch(code))
                offences.Add(new Offence(i + 1, raw.Trim()));

            depth += code.Count(c => c == '{') - code.Count(c => c == '}');

            if (inLock && awaitingBrace && depth > lockDepth)
                awaitingBrace = false;

            // A one-line lock body opens and closes on its own line.
            if (inLock && !awaitingBrace && depth <= lockDepth)
                inLock = false;

            // ...including when it was opened on this very line.
            if (opensLockHere && !awaitingBrace && depth <= lockDepth)
                inLock = false;
        }

        return offences;
    }

    private static string StripStringsAndComments(string line)
    {
        int comment = line.IndexOf("//", StringComparison.Ordinal);
        if (comment >= 0) line = line[..comment];
        line = Regex.Replace(line, @"""(?:[^""\\]|\\.)*""", "\"\"");
        return line;
    }

    private static string RepoFile(string relativePath)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "project01.sln")))
            dir = dir.Parent;

        Assert.NotNull(dir);
        string full = Path.Combine(dir!.FullName, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Assert.True(File.Exists(full), $"{relativePath} not found from {AppContext.BaseDirectory}");
        return full;
    }
}
