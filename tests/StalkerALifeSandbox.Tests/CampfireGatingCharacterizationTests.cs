using System.Numerics;
using StalkerALifeSandbox.AI.Blackboards;
using StalkerALifeSandbox.AI.GOAP;
using StalkerALifeSandbox.AI.GOAP.Actions;
using StalkerALifeSandbox.AI.GOAP.Goals;
using StalkerALifeSandbox.AI.Social;
using StalkerALifeSandbox.Economy;
using StalkerALifeSandbox.Entities.Characters;
using StalkerALifeSandbox.Entities.Equipment;
using StalkerALifeSandbox.Entities.Needs;
using StalkerALifeSandbox.Factions;
using StalkerALifeSandbox.World.Generation;
using StalkerALifeSandbox.World.Hazards;
using StalkerALifeSandbox.World.Navigation;
using StalkerALifeSandbox.World.POI;
using StalkerALifeSandbox.Core;

namespace StalkerALifeSandbox.Tests;

/// <summary>
/// CHARACTERIZATION TESTS — these pin the behaviour of the `IsAtCampfire`
/// world-state flag exactly as it works today, BEFORE Phase 6A replaces the
/// `IdleAtBase` proxy with real campfire objects.
///
/// Why this matters: `IsAtCampfire` is load-bearing. Four actions gate on it
/// (ShareDrink, PlayGuitar, CraftUpgrade, CookMutantMeatGoap) and three goals
/// score off it (RepairGear hard-gates, CookFood +10, AcceptMission +8).
/// A fifth action — RestAtBase — *produces* it as a GOAP effect, which is how
/// the campfire cluster becomes reachable at all.
///
/// If Phase 6A tightens the flag to require proximity to a real campfire,
/// these tests are the guard rail: they should all still pass. Any that fail
/// indicate the change silently switched a behaviour off.
/// </summary>
public class CampfireGatingCharacterizationTests
{
    private static NPCBlackboard Bb(params (string Key, bool Value)[] flags)
    {
        var bb = new NPCBlackboard("npc-1");
        foreach (var (k, v) in flags) bb.WorldStateBools[k] = v;
        return bb;
    }

    private static SurvivalNeeds HungryNeeds()
    {
        var needs = new SurvivalNeeds();
        needs.Tick(3600f); // one game hour -> Hunger ~8.3, above CookFood's 5.0 threshold
        return needs;
    }

    // ── Producer: RestAtBase is what makes the campfire cluster reachable ────

    [Fact]
    public void RestAtBase_ProducesIsAtCampfire_AndDoesNotRequireIt()
    {
        var action = new ActionRestAtBase();

        Assert.True(action.GetEffects()[GoapKeys.IsAtCampfire],
            "RestAtBase must keep producing IsAtCampfire — it is how the planner "
            + "chains into ShareDrink/Guitar/Craft/Cook.");
        Assert.False(action.GetPreconditions().ContainsKey(GoapKeys.IsAtCampfire),
            "RestAtBase gates on CanRest, not IsAtCampfire.");
    }

    // ── Consumers: the four actions gated on the flag ───────────────────────

    [Fact]
    public void ShareDrinkAndPlayGuitar_RequireIsAtCampfire()
    {
        Assert.True(new ActionShareDrink().GetPreconditions()[GoapKeys.IsAtCampfire]);
        Assert.True(new ActionPlayGuitar().GetPreconditions()[GoapKeys.IsAtCampfire]);
    }

    [Fact]
    public void CraftUpgrade_RequiresCampfireAndGearDamage()
    {
        var pre = new ActionCraftUpgrade().GetPreconditions();
        Assert.True(pre[GoapKeys.IsAtCampfire]);
        Assert.True(pre[GoapKeys.HasGearDamage]);
    }

    [Fact]
    public void CookMutantMeat_RequiresCampfireAndRawMeat()
    {
        var pre = new ActionCookMutantMeatGoap().GetPreconditions();
        Assert.True(pre[GoapKeys.IsAtCampfire]);
        Assert.True(pre[GoapKeys.HasRawMeat]);
    }

    // ── Consumers: goal scoring ─────────────────────────────────────────────

    [Fact]
    public void GoalRepairGear_HardGatesOnCampfire()
    {
        var goal = new GoalRepairGear();
        var needs = new SurvivalNeeds();

        var without = Bb((GoapKeys.HasGearDamage, true), (GoapKeys.IsAtCampfire, false));
        var with = Bb((GoapKeys.HasGearDamage, true), (GoapKeys.IsAtCampfire, true));

        Assert.Equal(0f, goal.EvaluateUtility(without, needs));
        Assert.True(goal.EvaluateUtility(with, needs) > 0f,
            "RepairGear is HARD-gated on IsAtCampfire — losing the flag switches repair off entirely.");
    }

    [Fact]
    public void GoalRepairGear_ScoresWorseGearHigher()
    {
        var goal = new GoalRepairGear();
        var needs = new SurvivalNeeds();

        var jamming = Bb((GoapKeys.HasGearDamage, true), (GoapKeys.IsAtCampfire, true));
        jamming.WorldStateFloats["PrimaryWeaponCondition"] = 0.2f;

        var worn = Bb((GoapKeys.HasGearDamage, true), (GoapKeys.IsAtCampfire, true));
        worn.WorldStateFloats["PrimaryWeaponCondition"] = 0.9f;

        Assert.True(goal.EvaluateUtility(jamming, needs) > goal.EvaluateUtility(worn, needs));
    }

    [Fact]
    public void GoalCookFood_CampfireAddsExactlyTenUtility()
    {
        var goal = new GoalCookFood();
        var needs = HungryNeeds();

        float without = goal.EvaluateUtility(
            Bb((GoapKeys.HasRawMeat, true), (GoapKeys.IsAtCampfire, false)), needs);
        float with = goal.EvaluateUtility(
            Bb((GoapKeys.HasRawMeat, true), (GoapKeys.IsAtCampfire, true)), needs);

        Assert.True(without > 0f, "CookFood is only soft-gated — it must still fire away from a campfire.");
        Assert.Equal(10f, with - without, 0.01f);
    }

    [Fact]
    public void GoalAcceptMission_CampfireAddsEightUtility()
    {
        var goal = new GoalAcceptMission();
        var needs = new SurvivalNeeds();

        (string, bool)[] baseFlags =
        {
            (GoapKeys.HasMissionOffer, true),
            (GoapKeys.HasActiveMission, false),
            (GoapKeys.EmissionImminent, false),
            (GoapKeys.IsAtMissionGiver, false),
            (GoapKeys.IsAtHomeBase, false)
        };

        var without = Bb([.. baseFlags, (GoapKeys.IsAtCampfire, false)]);
        var with = Bb([.. baseFlags, (GoapKeys.IsAtCampfire, true)]);

        Assert.Equal(8f, goal.EvaluateUtility(with, needs) - goal.EvaluateUtility(without, needs), 0.01f);
    }

    // ── Producer mapping: the line Phase 6A will actually change ────────────

    [Fact]
    public void Sync_MapsIdleAtBaseToIsAtCampfire()
    {
        var ctx = SharedGoapContext.Value;

        var idle = new Stalker("s-idle", "Idle One", "Loner") { IdleAtBase = true };
        var roaming = new Stalker("s-roam", "Roamer", "Loner") { IdleAtBase = false };
        ctx.BindStalkers(new[] { idle, roaming });

        GoapWorldStateSync.Sync(idle, ctx);
        GoapWorldStateSync.Sync(roaming, ctx);

        Assert.True(idle.Blackboard.WorldStateBools[GoapKeys.IsAtCampfire],
            "A stalker idling at base must register as at-a-campfire. Phase 6A must keep this true "
            + "(keep the IdleAtBase disjunct) or RestAtBase's declared effect becomes a lie to the planner.");
        Assert.False(roaming.Blackboard.WorldStateBools[GoapKeys.IsAtCampfire]);
    }

    // ── New in 6A: proximity to a real campfire also sets the flag ──────────

    [Fact]
    public void Sync_SetsIsAtCampfire_WhenStandingNearARealCampfire()
    {
        // Same shared world, but a context whose registry has one campfire at origin.
        var ctx = TestWorld.Context(CampfireRegistry.Generate(
            new[] { new POIStamp { Id = "b", Name = "b", Type = POIType.MacroBase, Position = Vector3.Zero } },
            new CampfireOptions { ProximityRadius = 30f }));

        // Not idling at base — the flag must now come from position alone.
        var nearFire = new Stalker("s-near", "Near", "Loner")
        {
            IdleAtBase = false,
            Position = new Vector3(10f, 0f, 0f)
        };
        var farAway = new Stalker("s-far", "Far", "Loner")
        {
            IdleAtBase = false,
            Position = new Vector3(900f, 0f, 900f)
        };
        ctx.BindStalkers(new[] { nearFire, farAway });

        GoapWorldStateSync.Sync(nearFire, ctx);
        GoapWorldStateSync.Sync(farAway, ctx);

        Assert.True(nearFire.Blackboard.WorldStateBools[GoapKeys.IsAtCampfire],
            "Standing next to a real campfire should now count, without needing IdleAtBase.");
        Assert.False(farAway.Blackboard.WorldStateBools[GoapKeys.IsAtCampfire]);
    }

    /// <summary>
    /// Shared generated world (see <see cref="TestWorld"/>). The registry is
    /// deliberately EMPTY: with no campfires anywhere, IsNear() is always
    /// false, so these tests isolate the IdleAtBase disjunct and prove it
    /// still stands on its own after Phase 6A widened the flag.
    /// </summary>
    private static readonly Lazy<GoapContext> SharedGoapContext = new(() => TestWorld.Context());
}
