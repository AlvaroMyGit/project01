using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using StalkerALifeSandbox.Entities.Characters;
using StalkerALifeSandbox.Entities.Mutants;
using StalkerALifeSandbox.Systems;
using StalkerALifeSandbox.World.Hazards;
using StalkerALifeSandbox.AI.Squads;
using StalkerALifeSandbox.World.Generation;

namespace StalkerALifeSandbox.Core.Systems;

public sealed class EmissionTickSystem : ISimulationSystem
{
    private bool _stormWasActive;
    private readonly HashSet<string> _stormExposedStalkers = new();

    public void Tick(SimulationContext ctx, float gameDelta)
    {
        ctx.Emissions.Tick((float)ctx.Time.ElapsedGameSeconds);

        Stalker[] stalkers;
        Mutant[] mutants;
        lock (ctx.EntityLock)
        {
            stalkers = ctx.Stalkers.ToArray();
            mutants  = ctx.Mutants.ToArray();
        }

        var squadLeaders = stalkers
            .Where(s => s.IsAlive && s.IsSquadLeader && s.SquadId != null)
            .GroupBy(s => s.SquadId!)
            .ToDictionary(g => g.Key, g => g.First());

        ProcessEmissionPressure(ctx, gameDelta, squadLeaders, stalkers, mutants);
        ApplyAnomalyRadiation(ctx, stalkers);
        ApplyAnomalyExposure(ctx, gameDelta, stalkers);
        TickEmissionStormEnd(ctx, stalkers);
    }

    private static void ApplyAnomalyRadiation(SimulationContext ctx, Stalker[] stalkers)
    {
        var activeFields = ctx.Emissions.Fields;
        var radZones = ctx.Emissions.RadZones;
        
        foreach (var s in stalkers.Where(s => s.IsAlive))
        {
            float totalRad = 0f;

            // Anomaly fields
            var insideField = activeFields.FirstOrDefault(f => f.Contains(s.Position));
            if (insideField != null)
            {
                totalRad += insideField.Damage;
            }

            // Rad zones (additive)
            foreach (var rz in radZones)
            {
                if (Vector2.Distance(new Vector2(rz.X, rz.Y), new Vector2(s.Position.X, s.Position.Z)) <= rz.Radius)
                {
                    totalRad += rz.RadPerSec * rz.BaseIntensity;
                }
            }

            if (totalRad == 0f)
            {
                s.Needs.RadiationGainRate = 0f;
                continue;
            }

            var profile = ProtectionProfile.From(s);
            float radMit = 1f - Math.Clamp(profile.Rad, 0f, 0.90f);
            s.Needs.RadiationGainRate = totalRad * radMit / 3600f;

            if (Random.Shared.NextDouble() < 0.005) // ~1 log per 20 secs per irradiated stalker
            {
                SimulationDebugLog.HazardHit(
                    s.DisplayName.Split(' ')[0], "RadZone", totalRad * radMit);
            }
        }
    }

    private static void ApplyAnomalyExposure(SimulationContext ctx, float gameDelta, Stalker[] stalkers)
    {
        var activeFields = ctx.Emissions.Fields;
        foreach (var s in stalkers.Where(s => s.IsAlive))
        {
            var field = activeFields.FirstOrDefault(f => f.Contains(s.Position));
            if (field == null) continue;

            var profile = ProtectionProfile.From(s);
            float typeProt = profile.ForAnomalyType(field.Type);
            float exposure = field.Damage * (1f - Math.Clamp(typeProt, 0f, 0.90f)) * gameDelta;
            if (exposure <= 0.01f) continue;

            switch (field.Type)
            {
                case AnomalyType.Psi:
                    s.Needs.AdjustMorale(-exposure * 0.18f);
                    break;
                case AnomalyType.Fire:
                case AnomalyType.Chemical:
                    s.Needs.AdjustMorale(-exposure * 0.10f);
                    s.Needs.RadiationGainRate += exposure * 0.004f / 3600f;
                    break;
                case AnomalyType.Electro:
                    s.Needs.AdjustMorale(-exposure * 0.12f);
                    s.Needs.Exhaust(exposure * 0.10f);
                    break;
                case AnomalyType.Gravitational:
                    s.Needs.AdjustMorale(-exposure * 0.14f);
                    break;
            }

            if (Random.Shared.NextDouble() < 0.01) // ~1 log per 10 secs per stalker in field
            {
                SimulationDebugLog.HazardHit(
                    s.DisplayName.Split(' ')[0], field.Type.ToString(), exposure);
            }
        }
    }

    private void ProcessEmissionPressure(SimulationContext ctx, float gameDelta, Dictionary<string, Stalker> squadLeaders, Stalker[] stalkers, Mutant[] mutants)
    {
        var phase = ctx.Emissions.CurrentPhase;
        if (phase is EmissionPhase.Dormant or EmissionPhase.Warning)
            return;

        var shelters = ctx.Stamper.Stamps.Where(s =>
            s.Type == POIType.MacroBase ||
            s.Type == POIType.MicroShelter ||
            s.Type == POIType.UndergroundLab).ToList();

        float intensity = ctx.Emissions.PhaseIntensity;
        float phaseRate = phase switch
        {
            EmissionPhase.Panic => 0.25f,
            EmissionPhase.Peak => 0.55f,
            EmissionPhase.Aftermath => 0.12f,
            _ => 0f
        };

        foreach (var s in stalkers.Where(s => s.IsAlive))
        {
            if (s.TrueFaction is "Zombified" or "Monolith") continue;
            if (IsNearShelter(s.Position, shelters)) continue;

            _stormExposedStalkers.Add(s.Id);

            double hitChance = 0.045 * intensity * phaseRate * gameDelta;
            float radMit = 1f - Math.Clamp(ProtectionProfile.From(s).Rad, 0f, 0.75f);
            hitChance *= radMit;
            if (Random.Shared.NextDouble() >= hitChance) continue;

            if (Random.Shared.NextDouble() < 0.70)
                KillStalkerFromEmission(ctx, s, squadLeaders);
            else
                ZombifyStalkerFromEmission(s);
        }

        foreach (var m in mutants.Where(m => m.IsAlive))
        {
            if (IsNearShelter(m.Position, shelters)) continue;

            if (Random.Shared.NextDouble() < 0.06 * intensity * phaseRate * gameDelta)
            {
                SimulationDebugLog.MutantEmissionDeath();
                m.IsAlive = false;
            }
        }
    }

    private static void ZombifyStalkerFromEmission(Stalker s)
    {
        SimulationDebugLog.StalkerZombified();
        s.TrueFaction = "Zombified";
        s.DisplayName = $"Zombified {s.DisplayName.Split(' ')[0]}";
    }

    private static bool IsNearShelter(Vector3 pos, List<WorldPOIBase> shelters) =>
        shelters.Any(shelter =>
            ShelterDistance(shelter, pos) <= (shelter.Radius > 0 ? shelter.Radius : 30f));

    private static float ShelterDistance(WorldPOIBase poi, Vector3 pos)
    {
        float horizontal = Vector2.Distance(
            new Vector2(poi.Position.X, poi.Position.Z),
            new Vector2(pos.X, pos.Z));
        float vertical = MathF.Abs(poi.Position.Y - pos.Y);
        return horizontal + vertical * 0.5f;
    }

    private void KillStalkerFromEmission(SimulationContext ctx, Stalker s, Dictionary<string, Stalker> squadLeaders)
    {
        s.IsAlive = false;
        ctx.PDA.UnregisterListener(s.Blackboard);
        SquadSuccession.OnLeaderDeath(s, ctx.Stalkers, ctx.RequestReplan, squadLeaders);
        KillTracker.RecordKill(s, "Emission", $"{(int)ctx.Time.HourOfDay:D2}:{(int)((ctx.Time.HourOfDay % 1) * 60):D2}");
        ctx.Corpses.Add(EquipmentUpgradeService.CreateStalkerCorpse(s, CauseOfDeath.Emission, (float)ctx.Time.ElapsedGameSeconds, "corpse_em"));
    }

    private void TickEmissionStormEnd(SimulationContext ctx, Stalker[] stalkers)
    {
        bool active = ctx.Emissions.IsStormActive;
        if (_stormWasActive && !active)
        {
            foreach (var id in _stormExposedStalkers)
            {
                var survivor = stalkers.FirstOrDefault(s => s.Id == id && s.IsAlive);
                if (survivor != null)
                    SkillEvaluator.RecordZoneSurvivalEvent(survivor, "emission_survived");
            }
            _stormExposedStalkers.Clear();
        }

        if (active && !_stormWasActive)
            _stormExposedStalkers.Clear();

        _stormWasActive = active;
    }
}
