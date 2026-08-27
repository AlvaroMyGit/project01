using StalkerALifeSandbox.Economy;
using StalkerALifeSandbox.Entities.Characters;
using StalkerALifeSandbox.Entities.Equipment;
using StalkerALifeSandbox.Entities.Mutants;
using StalkerALifeSandbox.Systems;

namespace StalkerALifeSandbox.Web;

/// <summary>Builds rich on-demand inspector payloads for the WebGL visualizer.</summary>
public static class InspectorBuilder
{
    public static InspectorDTO? FromStalker(
        Stalker s,
        MissionRegistry? missions = null,
        TraderRegistry? traders = null)
    {
        if (!s.IsAlive) return null;

        bool hasOffer = missions != null && traders != null &&
                        missions.FindNearestIssuerWithOffer(s, traders) != null;

        return new InspectorDTO
        {
            EntityId = s.Id,
            Name = s.DisplayName,
            Faction = s.TrueFaction,
            ApparentFaction = s.ApparentFaction,
            Type = "stalker",
            IsAlive = true,
            Health = 100,
            LayerIndex = s.Position.Y < -10f ? -1 : 0,
            LevelId = s.CurrentLevelId,
            Rank = s.Rank.CurrentRank.ToString(),
            CurrentGoal = s.Blackboard.OverrideNavigationStatus
                ?? s.Blackboard.NavigationStatus,
            Activity = s.Activity,
            Desperation = s.Needs.IsInCriticalState,
            Position = new PositionDTO { X = s.Position.X, Y = s.Position.Z },
            Needs = new NeedsDTO
            {
                Hunger = s.Needs.Hunger,
                Thirst = s.Needs.Thirst,
                Radiation = s.Needs.Radiation,
                Fatigue = s.Needs.Fatigue,
                Morale = s.Needs.Morale,
                Gold = s.Needs.GoldAmount,
                Ammo = s.Needs.AmmoCount
            },
            Skills = new SkillsDTO
            {
                Marksmanship = s.Attributes.Marksmanship,
                ZoneSurvival = s.Attributes.ZoneSurvival,
                Charisma = s.Attributes.Charisma,
                Trustworthiness = s.Attributes.Trustworthiness
            },
            Equipment = TelemetryMapper.BuildEquipment(s),
            SuspicionLevel = s.Blackboard.SuspicionLevel,
            ThreatMemory = new Dictionary<string, float>(s.Blackboard.LocationThreatMemory),
            SquadId = s.SquadId,
            IsSquadLeader = s.IsSquadLeader,
            Mission = TelemetryMapper.BuildMission(s.ActiveMission),
            MissionsCompleted = s.Rank.Missions,
            HasMissionOffer = hasOffer && s.ActiveMission == null,
            KillStats = TelemetryMapper.BuildKillStats(s),
            RawMeatCount = s.RawMeatCount,
            ScrapCount   = s.ScrapCount,
            VodkaCount   = s.VodkaCount
        };
    }

    public static InspectorDTO? FromMutant(Mutant m)
    {
        if (!m.IsAlive) return null;

        return new InspectorDTO
        {
            EntityId = m.Id,
            Name = m.Species,
            Faction = "Mutants",
            Type = "mutant",
            IsAlive = true,
            Health = (int)m.Health,
            LayerIndex = 0,
            LevelId = "surface",
            CurrentGoal = m.Blackboard.OverrideNavigationStatus
                ?? (m.IsHuntingPhase ? "Hunting" : "Roaming"),
            Activity = m.PersonalityTrait,
            Desperation = m.IsHuntingPhase,
            Position = new PositionDTO { X = m.Position.X, Y = m.Position.Z },
            Needs = new NeedsDTO { Hunger = m.Hunger }
        };
    }

    public static InspectorDTO? FromCorpse(Corpse c, float gameTime)
    {
        if (c.IsEaten) return null;

        var dto = TelemetryMapper.BuildCorpse(c, gameTime);
        return new InspectorDTO
        {
            EntityId = c.CorpseId,
            Name = c.VictimName,
            Faction = c.VictimFaction,
            Type = c.IsMutant ? "corpse_mutant" : "corpse",
            IsAlive = false,
            Health = 0,
            LayerIndex = 0,
            LevelId = "surface",
            CurrentGoal = "Deceased",
            Activity = dto.IsLooted ? "Stripped" : dto.Loot != null ? "Gear on body" : "—",
            Position = dto.Position,
            CauseOfDeath = dto.Cause,
            IsReported = dto.IsReported,
            IsLooted = dto.IsLooted,
            IsPatchIntact = dto.IsPatchIntact,
            AgeSec = dto.AgeSec,
            DespawnSec = dto.DespawnSec,
            CorpseLoot = dto.Loot
        };
    }
}
