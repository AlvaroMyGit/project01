using System.Numerics;
using StalkerALifeSandbox.AI.Blackboards;
using StalkerALifeSandbox.AI.Perception;

namespace StalkerALifeSandbox.Tests;

/// <summary>
/// VisionCone and AcousticSensor were written at the start of the project and
/// never called once, because nothing tracked which way an NPC was facing.
/// These pin the facing input and the two sensors now that they are wired up.
/// </summary>
public class PerceptionTests
{
    private static NPCBlackboard At(Vector3 pos)
    {
        var bb = new NPCBlackboard("obs") { CurrentPosition = pos };
        return bb;
    }

    // ── Facing ──────────────────────────────────────────────────────────────

    [Fact]
    public void FacingFollowsMovementAndIsAUnitVector()
    {
        var bb = At(Vector3.Zero);
        bb.FaceToward(Vector3.Zero, new Vector3(100f, 0f, 0f));

        Assert.Equal(1f, bb.Facing.Length(), 3);
        Assert.Equal(1f, bb.Facing.X, 3);
    }

    [Fact]
    public void StandingStillHoldsTheLastFacing()
    {
        // A stopped stalker keeps looking where they last walked rather than
        // snapping to a default.
        var bb = At(Vector3.Zero);
        bb.FaceToward(Vector3.Zero, new Vector3(0f, 0f, 50f));
        var held = bb.Facing;

        bb.FaceToward(Vector3.Zero, Vector3.Zero);   // zero-length move

        Assert.Equal(held, bb.Facing);
    }

    [Fact]
    public void FacingDegreesIsACompassBearing()
    {
        var bb = At(Vector3.Zero);
        bb.FaceToward(Vector3.Zero, new Vector3(0f, 0f, 10f));
        Assert.Equal(0f, bb.FacingDegrees, 1);        // +Z is north

        bb.FaceToward(Vector3.Zero, new Vector3(10f, 0f, 0f));
        Assert.Equal(90f, bb.FacingDegrees, 1);       // +X is east
    }

    // ── Vision ──────────────────────────────────────────────────────────────

    [Fact]
    public void SomethingAheadAndCloseIsSeen()
    {
        var bb = At(Vector3.Zero);
        bb.FaceToward(Vector3.Zero, new Vector3(0f, 0f, 1f));

        new VisionCone().Sweep(bb, bb.CurrentPosition, bb.Facing, 1f, 1f, false, false, 0f,
            new[] { ("target", new Vector3(0f, 0f, 40f), false) });

        Assert.True(bb.KnownEntities.ContainsKey("target"));
    }

    [Fact]
    public void SomethingDirectlyBehindIsNotSeen()
    {
        // The whole point of a cone: proximity alone is not detection.
        var bb = At(Vector3.Zero);
        bb.FaceToward(Vector3.Zero, new Vector3(0f, 0f, 1f));

        new VisionCone().Sweep(bb, bb.CurrentPosition, bb.Facing, 1f, 1f, false, false, 0f,
            new[] { ("behind", new Vector3(0f, 0f, -40f), false) });

        Assert.False(bb.KnownEntities.ContainsKey("behind"));
    }

    [Fact]
    public void DarknessShortensSightRange()
    {
        var far = new Vector3(0f, 0f, 70f);

        var day = At(Vector3.Zero); day.FaceToward(Vector3.Zero, Vector3.UnitZ);
        new VisionCone().Sweep(day, day.CurrentPosition, day.Facing, 1.0f, 1f, false, false, 0f,
            new[] { ("t", far, false) });

        var night = At(Vector3.Zero); night.FaceToward(Vector3.Zero, Vector3.UnitZ);
        new VisionCone().Sweep(night, night.CurrentPosition, night.Facing, 0.1f, 1f, false, false, 0f,
            new[] { ("t", far, false) });

        Assert.True(day.KnownEntities.ContainsKey("t"));
        Assert.False(night.KnownEntities.ContainsKey("t"));
    }

    [Fact]
    public void NightVisionIgnoresTheDark()
    {
        var bb = At(Vector3.Zero);
        bb.FaceToward(Vector3.Zero, Vector3.UnitZ);

        new VisionCone().Sweep(bb, bb.CurrentPosition, bb.Facing, 0.05f, 1f, false, hasNVGOn: true, 0f,
            new[] { ("t", new Vector3(0f, 0f, 70f), false) });

        Assert.True(bb.KnownEntities.ContainsKey("t"));
    }

    // ── Hearing ─────────────────────────────────────────────────────────────

    [Fact]
    public void AGunshotNearbyIsHeardEvenFacingAway()
    {
        var bb = At(Vector3.Zero);
        bb.FaceToward(Vector3.Zero, new Vector3(0f, 0f, -1f));   // facing away

        var bus = new NoiseBus();
        bus.EmitGunshot("shooter", new Vector3(0f, 0f, 30f), "cordon");

        new AcousticSensor().Process(bb, bb.CurrentPosition, 0f, rainIntensity: 0f, bus.Current);

        Assert.True(bb.KnownEntities.ContainsKey("shooter"));
    }

    [Fact]
    public void RainMufflesHearing()
    {
        var bus = new NoiseBus();
        bus.EmitGunshot("shooter", new Vector3(0f, 0f, 45f));

        var dry = At(Vector3.Zero);
        new AcousticSensor().Process(dry, dry.CurrentPosition, 0f, rainIntensity: 0f, bus.Current);

        var wet = At(Vector3.Zero);
        new AcousticSensor().Process(wet, wet.CurrentPosition, 0f, rainIntensity: 1f, bus.Current);

        Assert.True(dry.KnownEntities.ContainsKey("shooter"));
        Assert.False(wet.KnownEntities.ContainsKey("shooter"));
    }

    [Fact]
    public void AGunshotCarriesFurtherThanAScuffle()
    {
        Assert.True(NoiseBus.GunshotLoudness > NoiseBus.MeleeLoudness);
    }

    [Fact]
    public void TheBusHoldsOneTickAndClears()
    {
        var bus = new NoiseBus();
        bus.EmitGunshot("a", Vector3.Zero);
        Assert.Single(bus.Current);

        bus.Clear();
        Assert.Empty(bus.Current);
    }

    [Fact]
    public void HearingRaisesLocationThreatMemory()
    {
        var bb = At(Vector3.Zero);
        var bus = new NoiseBus();
        bus.EmitGunshot("shooter", new Vector3(0f, 0f, 10f), regionId: "cordon");

        new AcousticSensor().Process(bb, bb.CurrentPosition, 0f, 0f, bus.Current, recordThreat: true);

        Assert.True(bb.LocationThreatMemory.GetValueOrDefault("cordon") > 0f);
    }

    // ── Shadow mode is enforced, not assumed ────────────────────────────────

    [Fact]
    public void ShadowMode_HearsTheShot_ButDoesNotRecordThreat()
    {
        // LocationThreatMemory is not an observation buffer: GoapWorldStateSync
        // reads it to derive HeardDangerRumor, so a write here steers goal
        // selection. Unguarded it moved missions accepted -12% against the
        // stored baseline while the system was supposedly read-only.
        var bb = At(Vector3.Zero);
        var bus = new NoiseBus();
        bus.EmitGunshot("shooter", new Vector3(0f, 0f, 10f), regionId: "cordon");

        int heard = new AcousticSensor().Process(bb, bb.CurrentPosition, 0f, 0f, bus.Current, recordThreat: false);

        Assert.Equal(1, heard);                                  // still perceived
        Assert.True(bb.KnownEntities.ContainsKey("shooter"));    // still recorded
        Assert.Empty(bb.LocationThreatMemory);                   // but inert
    }

    [Fact]
    public void SensorsDoNotWriteBackTheObserverPosition()
    {
        // bb.CurrentPosition is a 1 Hz snapshot. Squad follow destinations and
        // corpse selection read it at 10 Hz, so a sensor that refreshed it to
        // keep its own maths accurate would be steering the sim: measured at
        // -36% squad spread and -5% population while perception was supposedly
        // read-only. The sensors take an explicit origin for exactly this
        // reason, and must leave the field alone.
        var bb = At(new Vector3(1f, 0f, 1f));      // deliberately stale
        var stale = bb.CurrentPosition;
        var liveOrigin = new Vector3(500f, 0f, 500f);
        bb.FaceToward(Vector3.Zero, Vector3.UnitZ);

        new VisionCone().Sweep(bb, liveOrigin, bb.Facing, 1f, 1f, false, false, 0f,
            new[] { ("near-live", new Vector3(500f, 0f, 540f), false) });

        var bus = new NoiseBus();
        bus.EmitGunshot("shot", new Vector3(500f, 0f, 510f));
        new AcousticSensor().Process(bb, liveOrigin, 0f, 0f, bus.Current, recordThreat: false);

        // Both sensed relative to the origin they were handed...
        Assert.True(bb.KnownEntities.ContainsKey("near-live"));
        Assert.True(bb.KnownEntities.ContainsKey("shot"));
        // ...and neither touched the shared field.
        Assert.Equal(stale, bb.CurrentPosition);
    }

    [Fact]
    public void ShadowIsTheDefault()
    {
        var options = new PerceptionOptions();

        Assert.True(options.Enabled);
        Assert.False(options.ThreatMemoryFeedsGoap);
    }

    [Fact]
    public void ThreatTagsUseBandNames_NotLevelIds()
    {
        // GoapWorldStateSync reads LocationThreatMemory by band name, but
        // derives HeardDangerRumor from Values.Any(v => v >= 45) across every
        // key. A key no band lookup matches is therefore invisible to the
        // lookup and still trips the rumour — at +6 a shot, permanently, for
        // everyone. Pinning the band vocabulary keeps the two in the same
        // key space.
        var bands = new[] { "South", "MidZone", "DeepWild", "North" };

        foreach (float threat in new[] { 0.1f, 0.4f, 0.7f, 0.95f })
            Assert.Contains(
                StalkerALifeSandbox.World.Generation.ZoneWorldGenerator.GetBandName(threat),
                bands);

        // "surface" is a level id, and was the tag until this was caught.
        Assert.DoesNotContain("surface", bands);
    }

    [Fact]
    public void CoverageIsMeasuredAgainstTheRangeCombatActuallyUses()
    {
        // The divergence figure is only meaningful if both models are scored
        // over the same population. This pins the comparison radius to the one
        // StalkerBehaviourSystem engages at.
        Assert.Equal(160f, new PerceptionOptions().CombatEngageRange);
    }
}
