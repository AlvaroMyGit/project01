using System.Numerics;
using StalkerALifeSandbox.AI.GOAP;
using StalkerALifeSandbox.Entities.Characters;

namespace StalkerALifeSandbox.AI.Squads;

/// <summary>Handles squad leadership when a leader dies — promote, merge, or disband.</summary>
public static class SquadSuccession
{
    public const float MergeSearchRadius = 200f;
    public const int MaxSquadSize = 5;

    /// <summary>
    /// Called when a stalker dies. If they were squad leader, survivors are promoted,
    /// merged into a nearby squad, or disbanded to solo operators.
    /// </summary>
    public static void OnLeaderDeath(
        Stalker victim,
        IEnumerable<Stalker> allStalkers,
        Action<Stalker> requestReplan,
        Dictionary<string, Stalker>? squadLeaders = null)
    {
        if (!victim.IsSquadLeader || victim.SquadId == null) return;

        string squadId = victim.SquadId;
        var survivors = allStalkers
            .Where(s => s.IsAlive && s.SquadId == squadId && s.Id != victim.Id)
            .OrderBy(s => s.Id)
            .ToList();

        if (survivors.Count == 0)
        {
            squadLeaders?.Remove(squadId);
            return;
        }

        if (survivors.Count == 1)
        {
            var lone = survivors[0];
            var mergeLeader = FindMergeTarget(lone, allStalkers);
            if (mergeLeader != null)
            {
                lone.SquadId = mergeLeader.SquadId;
                lone.IsSquadLeader = false;
            }
            else
            {
                lone.SquadId = null;
                lone.IsSquadLeader = false;
            }
            requestReplan(lone);
            squadLeaders?.Remove(squadId);
            return;
        }

        var newLeader = survivors[0];
        newLeader.IsSquadLeader = true;
        foreach (var member in survivors.Skip(1))
            member.IsSquadLeader = false;

        foreach (var member in survivors)
            requestReplan(member);

        if (squadLeaders != null)
            squadLeaders[squadId] = newLeader;
    }

    private static Stalker? FindMergeTarget(Stalker lone, IEnumerable<Stalker> allStalkers)
    {
        // Enumerate once. This used to walk allStalkers in the outer Where and
        // then call allStalkers.Count(...) again for every candidate leader —
        // O(n^2) over the whole population, on every leader death, and deaths
        // run into the hundreds per session.
        var living = allStalkers.Where(s => s.IsAlive).ToList();

        var squadSizes = living
            .Where(s => s.SquadId != null)
            .GroupBy(s => s.SquadId!)
            .ToDictionary(g => g.Key, g => g.Count());

        Stalker? best = null;
        float bestDist = MergeSearchRadius;

        foreach (var leader in living.Where(s =>
                     s.IsSquadLeader && s.SquadId != null &&
                     s.Id != lone.Id && s.TrueFaction == lone.TrueFaction))
        {
            if (squadSizes.GetValueOrDefault(leader.SquadId!) >= MaxSquadSize) continue;

            float dist = Vector3.Distance(lone.Position, leader.Position);
            if (dist < bestDist)
            {
                bestDist = dist;
                best = leader;
            }
        }

        return best;
    }
}
