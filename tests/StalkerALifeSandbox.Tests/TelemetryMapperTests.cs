using StalkerALifeSandbox.Entities.Characters;
using StalkerALifeSandbox.Web;

namespace StalkerALifeSandbox.Tests;

public class TelemetryMapperTests
{
    [Fact]
    public void BuildMission_ReturnsNull_ForNoMission()
    {
        Assert.Null(TelemetryMapper.BuildMission(null));
    }

    [Fact]
    public void BuildCorpse_MapsCoreFields()
    {
        var corpse = new Corpse
        {
            CorpseId = "corpse_7",
            VictimName = "Fallen Loner",
            VictimFaction = "Loner",
            CauseOfDeath = CauseOfDeath.Mutant,
            SpawnGameTime = 100f
        };

        var dto = TelemetryMapper.BuildCorpse(corpse, gameTime: 160f);

        Assert.Equal("corpse_7", dto.Id);
        Assert.Equal("Fallen Loner", dto.VictimName);
        Assert.Equal("Mutant", dto.Cause);
        Assert.Equal(60f, dto.AgeSec, 0.01f);   // 160 - 100
        Assert.False(dto.IsMutant);          // victim faction is "Loner", not "Mutant"
    }

    [Fact]
    public void ComputeDespawnRemaining_FreshCorpse_CountsDownFromIdleThreshold()
    {
        var corpse = new Corpse { CorpseId = "c", SpawnGameTime = 0f, LastInteractionGameTime = 0f };

        float remaining = TelemetryMapper.ComputeDespawnRemaining(corpse, gameTime: 0f);

        Assert.True(remaining > 0f);
        Assert.Equal(StalkerALifeSandbox.Systems.CorpseCleanupService.StalkerIdleDespawnSec, remaining, 0.01f);
    }
}
