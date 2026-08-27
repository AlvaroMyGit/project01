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
        Stalker? best = null;
        float bestDist = MergeSearchRadius;

        foreach (var leader in allStalkers.Where(s =>
                     s.IsAlive && s.IsSquadLeader && s.SquadId != null &&
                     s.Id != lone.Id && s.TrueFaction == lone.TrueFaction))
        {
            int squadSize = allStalkers.Count(s =>
                s.IsAlive && s.SquadId == leader.SquadId);
            if (squadSize >= MaxSquadSize) continue;

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
