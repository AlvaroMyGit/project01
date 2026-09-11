using StalkerALifeSandbox.Entities.Characters;
using StalkerALifeSandbox.Entities.Mutants;
using StalkerALifeSandbox.Systems;

namespace StalkerALifeSandbox.Tests;

// KillTracker is a process-wide static log shared across the whole test run
// (by design — see roadmap Phase 4). Assertions use before/after deltas and
// "does the log contain X" checks rather than absolute counts, so tests stay
// correct regardless of what other tests recorded first.
public class KillTrackerTests
{
    private static Stalker MakeStalker(string id, string name, string faction = "Loner") =>
        new(id, name, faction);

    [Fact]
    public void RecordKill_StalkerVsStalker_IncrementsTotalAndAppearsInRecentKills()
    {
        int before = KillTracker.TotalKills;
        var victim = MakeStalker("v1", "Victim One");
        var killer = MakeStalker("k1", "Killer One", faction: "Bandit");

        KillTracker.RecordKill(victim, killer, gameTimeStr: "01:00");

        Assert.Equal(before + 1, KillTracker.TotalKills);
        var recent = KillTracker.GetRecentKills(500).ToList();
        var evt = recent.First(e => e.VictimName == "Victim One");
        Assert.Equal("Killer One", evt.KillerName);
        Assert.Equal("stalker", evt.KillerType);
        Assert.Equal("Gunfire", evt.Cause);
    }

    [Fact]
    public void RecordKill_MutantKiller_TagsKillerTypeAsMutant()
    {
        var victim = MakeStalker("v2", "Victim Two");
        var mutant = new Mutant("m1", "Boar", DietType.Herbivore);

        KillTracker.RecordKill(victim, mutant);

        var evt = KillTracker.GetRecentKills(500).First(e => e.VictimName == "Victim Two");
        Assert.Equal("mutant", evt.KillerType);
        Assert.Equal("Boar", evt.KillerName);
        Assert.Equal("Mutant", evt.Cause);
    }

    [Fact]
    public void RecordKill_UnrecognizedKiller_TagsAsEnvironment()
    {
        var victim = MakeStalker("v3", "Victim Three");

        KillTracker.RecordKill(victim, "SomeUnknownThing");

        var evt = KillTracker.GetRecentKills(500).First(e => e.VictimName == "Victim Three");
        Assert.Equal("environment", evt.KillerType);
    }

    [Fact]
    public void RecordKill_CauseOverride_TakesPrecedence()
    {
        var victim = MakeStalker("v4", "Victim Four");
        var mutant = new Mutant("m2", "Flesh", DietType.Herbivore);

        KillTracker.RecordKill(victim, mutant, causeOverride: "Explosion");

        var evt = KillTracker.GetRecentKills(500).First(e => e.VictimName == "Victim Four");
        Assert.Equal("Explosion", evt.Cause);
    }

    [Fact]
    public void RecordMutantKill_IncrementsTotalAndAppearsInRecentKills()
    {
        int before = KillTracker.TotalKills;
        var victim = new Mutant("m3", "Snork", DietType.Carnivore);
        var killer = MakeStalker("k2", "Killer Two");

        KillTracker.RecordMutantKill(victim, killer);

        Assert.Equal(before + 1, KillTracker.TotalKills);
        var evt = KillTracker.GetRecentKills(500).First(e => e.VictimName == "Snork");
        Assert.Equal("mutant", evt.VictimType);
        Assert.Equal("Killer Two", evt.KillerName);
    }
}
