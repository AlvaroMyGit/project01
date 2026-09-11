using System;
using System.Collections.Generic;
using System.Linq;
using StalkerALifeSandbox.Economy;
using StalkerALifeSandbox.Systems;
using StalkerALifeSandbox.Web;

namespace StalkerALifeSandbox.Core;

/// <summary>Minimal map pin for the live entity list.</summary>
public sealed record EntityPin(string Id, string Name, string Faction, string Type, float X, float Y);

/// <summary>One PDA feed line, copied out of the live feed.</summary>
public sealed record FeedItem(float Time, string Type, string Headline, string Body, bool IsUrgent);

/// <summary>Aggregate mission activity counts.</summary>
public sealed record MissionCounters(
    int Active, int LeadersActive, int Scout, int Stash, int Escort,
    long AcceptedLifetime, long CompletedLifetime, int TotalOffers, int BasesWithOffers);

/// <summary>Population totals and per-faction breakdown.</summary>
public sealed record PopulationSnapshot(
    int Stalkers, int StalkerTarget, int Mutants, int MutantTarget, int Corpses,
    IReadOnlyDictionary<string, int> FactionCounts, MissionCounters Missions);

/// <summary>
/// Immutable, point-in-time view of simulation state consumed by the web layer.
/// Built on the simulation thread (the single writer of entity state) and
/// published by <see cref="SimulationLoop"/> via a volatile reference; web
/// request threads read it lock-free. Because it is built while no other thread
/// mutates entities, it never suffers the torn reads that reading live entities
/// from a request thread would. See the threading contract on SimulationLoop.
/// </summary>
public sealed record SimulationSnapshot(
    long BuiltAtUnixMs,
    IReadOnlyList<EntityPin> Entities,
    PopulationSnapshot Population,
    IReadOnlyList<FeedItem> Feed,
    IReadOnlyList<LeaderboardEntryDTO> Leaderboard,
    IReadOnlyDictionary<string, InspectorDTO> Inspectors)
{
    /// <summary>Placeholder served before the first tick has published a snapshot.</summary>
    public static readonly SimulationSnapshot Empty = new(
        0,
        Array.Empty<EntityPin>(),
        new PopulationSnapshot(0, 0, 0, 0, 0,
            new Dictionary<string, int>(),
            new MissionCounters(0, 0, 0, 0, 0, 0, 0, 0, 0)),
        Array.Empty<FeedItem>(),
        Array.Empty<LeaderboardEntryDTO>(),
        new Dictionary<string, InspectorDTO>());

    /// <summary>
    /// Builds a snapshot from live simulation state. MUST be called on the
    /// simulation thread so that no entity mutation is in flight.
    /// </summary>
    public static SimulationSnapshot Build(SimulationContext ctx, int stalkerTarget, int mutantTarget)
    {
        var aliveStalkers = ctx.Stalkers.Where(s => s.IsAlive).ToList();
        var aliveMutants = ctx.Mutants.Where(m => m.IsAlive).ToList();

        var entities = aliveStalkers
            .Select(s => new EntityPin(s.Id, s.DisplayName, s.TrueFaction, "stalker", s.Position.X, s.Position.Z))
            .Concat(aliveMutants
                .Select(m => new EntityPin(m.Id, m.Species, "Mutants", "mutant", m.Position.X, m.Position.Z)))
            .ToList();

        var factionCounts = aliveStalkers
            .GroupBy(s => s.TrueFaction)
            .ToDictionary(g => g.Key, g => g.Count());

        var missions = new MissionCounters(
            Active: aliveStalkers.Count(s => s.ActiveMission != null),
            LeadersActive: aliveStalkers.Count(s => s.IsSquadLeader && s.ActiveMission != null),
            Scout: aliveStalkers.Count(s => s.ActiveMission?.Type == MissionType.ScoutPoi),
            Stash: aliveStalkers.Count(s => s.ActiveMission?.Type == MissionType.RetrieveStash),
            Escort: aliveStalkers.Count(s => s.ActiveMission?.Type == MissionType.EscortConvoy),
            AcceptedLifetime: SimulationDebugLog.MissionsAccepted,
            CompletedLifetime: SimulationDebugLog.MissionsCompleted,
            TotalOffers: ctx.Missions.OffersByIssuer.Values.Sum(o => o.Count),
            BasesWithOffers: ctx.Missions.OffersByIssuer.Count);

        var population = new PopulationSnapshot(
            Stalkers: aliveStalkers.Count,
            StalkerTarget: stalkerTarget,
            Mutants: aliveMutants.Count,
            MutantTarget: mutantTarget,
            Corpses: ctx.Corpses.Count,
            FactionCounts: factionCounts,
            Missions: missions);

        var feed = ctx.PDA.Feed
            .TakeLast(40)
            .Select(m => new FeedItem(m.GameTime, m.MessageType.ToString(), m.Headline, m.Body, m.IsUrgent))
            .ToList();

        var leaderboard = LeaderboardSerializer.BuildTop100(aliveStalkers);

        // Per-entity inspector payloads, prebuilt here so the on-demand web
        // handler is a pure lock-free dictionary lookup. POIs are included
        // because FromPOI reads live stalker positions, which must not happen
        // on a request thread.
        var inspectors = new Dictionary<string, InspectorDTO>();
        foreach (var s in aliveStalkers)
        {
            var dto = InspectorBuilder.FromStalker(s, ctx.Missions, ctx.Traders);
            if (dto != null) inspectors[s.Id] = dto;
        }
        foreach (var m in aliveMutants)
        {
            var dto = InspectorBuilder.FromMutant(m);
            if (dto != null) inspectors[m.Id] = dto;
        }
        foreach (var poi in ctx.MacroPois.Concat(ctx.WildPoiCandidates))
        {
            if (inspectors.ContainsKey(poi.Id)) continue;
            var dto = InspectorBuilder.FromPOI(poi, ctx);
            if (dto != null) inspectors[poi.Id] = dto;
        }

        return new SimulationSnapshot(
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            entities, population, feed, leaderboard, inspectors);
    }
}
