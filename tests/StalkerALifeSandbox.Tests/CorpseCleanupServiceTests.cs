using StalkerALifeSandbox.Entities.Characters;
using StalkerALifeSandbox.Systems;

namespace StalkerALifeSandbox.Tests;

// Pins the config-driven despawn behavior so the upcoming config->options
// refactor (Phase 4) cannot change it unnoticed. Assertions are written against
// the currently-configured thresholds rather than hardcoded seconds, so they
// hold regardless of environment overrides.
public class CorpseCleanupServiceTests
{
    [Fact]
    public void FreshStalkerCorpse_DespawnsOnlyAfterIdleThreshold()
    {
        float idle = CorpseCleanupService.StalkerIdleDespawnSec;
        var corpse = new Corpse { CorpseId = "c", SpawnGameTime = 0, LastInteractionGameTime = 0 };

        Assert.False(CorpseCleanupService.ShouldDespawn(corpse, idle - 1));
        Assert.True(CorpseCleanupService.ShouldDespawn(corpse, idle + 1));
    }

    [Fact]
    public void EatenCorpse_DespawnsAfterEatenThresholdSinceInteraction()
    {
        float eaten = CorpseCleanupService.StalkerEatenDespawnSec;
        var corpse = new Corpse
        {
            CorpseId = "c",
            SpawnGameTime = 0,
            LastInteractionGameTime = 100,
            IsEaten = true
        };

        Assert.False(CorpseCleanupService.ShouldDespawn(corpse, 100 + eaten - 1));
        Assert.True(CorpseCleanupService.ShouldDespawn(corpse, 100 + eaten + 1));
    }

    [Fact]
    public void MarkInteraction_SwitchesToInteractedThreshold()
    {
        float interacted = CorpseCleanupService.StalkerInteractedDespawnSec;
        var corpse = new Corpse { CorpseId = "c", SpawnGameTime = 0, LastInteractionGameTime = 0 };

        CorpseCleanupService.MarkInteraction(corpse, 50f);

        Assert.False(CorpseCleanupService.ShouldDespawn(corpse, 50 + interacted - 1));
        Assert.True(CorpseCleanupService.ShouldDespawn(corpse, 50 + interacted + 1));
    }

    [Fact]
    public void MutantCorpse_UsesMutantIdleThreshold()
    {
        float mutantIdle = CorpseCleanupService.MutantIdleDespawnSec;
        var corpse = new Corpse
        {
            CorpseId = "c",
            VictimFaction = "Mutant", // Corpse.IsMutant is derived from this
            SpawnGameTime = 0,
            LastInteractionGameTime = 0
        };

        Assert.True(corpse.IsMutant);
        Assert.False(CorpseCleanupService.ShouldDespawn(corpse, mutantIdle - 1));
        Assert.True(CorpseCleanupService.ShouldDespawn(corpse, mutantIdle + 1));
    }
}
