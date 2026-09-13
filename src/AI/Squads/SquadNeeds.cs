using StalkerALifeSandbox.AI.Blackboards;
using StalkerALifeSandbox.Entities.Characters;

namespace StalkerALifeSandbox.AI.Squads;

/// <summary>
/// Surfaces what a squad needs onto its leader's blackboard, so the leader
/// plans for the group rather than only for themselves.
///
/// This is the answer to a problem that has run through the whole project:
/// squad followers do not run GOAP at all (`StalkerGoapService.ShouldPlan`
/// admits only leaders and solos), so a follower could never act on being
/// hungry, out of ammo, or miserable. Letting them plan was measured at 1.8x
/// over the 1 Hz tick budget and would dissolve squads as a coordination unit.
///
/// Delegation costs one O(population) pass per tick instead of hundreds of
/// extra A* searches, keeps the squad moving together, and reads better: a
/// leader visits a trader because his men are out of ammo.
///
/// Values are written as blackboard floats so goals stay pure functions of the
/// blackboard and remain unit-testable without a world.
/// </summary>
public static class SquadNeeds
{
    /// <summary>Worst hunger among the leader's living followers.</summary>
    public const string WorstHungerKey = "SquadWorstHunger";

    /// <summary>Worst thirst among the leader's living followers.</summary>
    public const string WorstThirstKey = "SquadWorstThirst";

    /// <summary>Lowest morale among the leader's living followers.</summary>
    public const string LowestMoraleKey = "SquadLowestMorale";

    /// <summary>How many living followers are out of ammunition.</summary>
    public const string OutOfAmmoKey = "SquadOutOfAmmo";

    /// <summary>Living followers under this leader, excluding the leader.</summary>
    public const string FollowerCountKey = "SquadFollowers";

    /// <summary>
    /// Recomputes every leader's squad summary. Call once per 1 Hz tick, before
    /// the leaders replan, so what they see is this tick's state.
    ///
    /// Leaders with no followers — and solo stalkers — are given a neutral
    /// summary rather than a stale one, so a leader whose squad has died stops
    /// planning around men who are no longer there.
    /// </summary>
    public static void Refresh(IReadOnlyList<Stalker> stalkers)
    {
        // Reset first: a leader who lost their last follower must not keep
        // acting on that follower's hunger.
        foreach (var s in stalkers)
        {
            if (!s.IsAlive) continue;
            if (!s.IsSquadLeader && s.SquadId != null) continue;
            Clear(s.Blackboard);
        }

        foreach (var follower in stalkers)
        {
            if (!follower.IsAlive || follower.IsSquadLeader || follower.SquadId == null)
                continue;

            var leader = FindLeader(stalkers, follower.SquadId);
            if (leader == null) continue;

            var bb = leader.Blackboard;
            var f = bb.WorldStateFloats;

            f[WorstHungerKey] = MathF.Max(f.GetValueOrDefault(WorstHungerKey), follower.Needs.Hunger);
            f[WorstThirstKey] = MathF.Max(f.GetValueOrDefault(WorstThirstKey), follower.Needs.Thirst);
            f[LowestMoraleKey] = MathF.Min(
                f.GetValueOrDefault(LowestMoraleKey, 100f), follower.Needs.Morale);
            f[FollowerCountKey] = f.GetValueOrDefault(FollowerCountKey) + 1;
            if (follower.Needs.IsOutOfAmmo)
                f[OutOfAmmoKey] = f.GetValueOrDefault(OutOfAmmoKey) + 1;
        }
    }

    private static Stalker? FindLeader(IReadOnlyList<Stalker> stalkers, string squadId)
    {
        foreach (var s in stalkers)
            if (s.IsAlive && s.IsSquadLeader && s.SquadId == squadId)
                return s;
        return null;
    }

    private static void Clear(NPCBlackboard bb)
    {
        var f = bb.WorldStateFloats;
        f[WorstHungerKey] = 0f;
        f[WorstThirstKey] = 0f;
        f[LowestMoraleKey] = 100f;   // nothing to worry about
        f[OutOfAmmoKey] = 0f;
        f[FollowerCountKey] = 0f;
    }

    // ── Read helpers, so goals do not repeat the key names ──────────────────

    public static float WorstHunger(NPCBlackboard bb) =>
        bb.WorldStateFloats.GetValueOrDefault(WorstHungerKey);

    public static float WorstThirst(NPCBlackboard bb) =>
        bb.WorldStateFloats.GetValueOrDefault(WorstThirstKey);

    public static float LowestMorale(NPCBlackboard bb) =>
        bb.WorldStateFloats.GetValueOrDefault(LowestMoraleKey, 100f);

    public static int OutOfAmmo(NPCBlackboard bb) =>
        (int)bb.WorldStateFloats.GetValueOrDefault(OutOfAmmoKey);

    public static int FollowerCount(NPCBlackboard bb) =>
        (int)bb.WorldStateFloats.GetValueOrDefault(FollowerCountKey);
}
