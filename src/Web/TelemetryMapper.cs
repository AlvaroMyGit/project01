using StalkerALifeSandbox.Economy;
using StalkerALifeSandbox.Entities.Characters;
using StalkerALifeSandbox.Entities.Equipment;
using StalkerALifeSandbox.Systems;
using StalkerALifeSandbox.Web;

namespace StalkerALifeSandbox.Web;

/// <summary>Maps simulation entities to telemetry / inspector DTOs.</summary>
public static class TelemetryMapper
{
    public static MissionDTO? BuildMission(StalkerMission? mission)
    {
        if (mission == null) return null;
        return new MissionDTO
        {
            Type = mission.Type.ToString(),
            IssuerName = mission.IssuerName,
            IssuerFaction = mission.IssuerFaction,
            TargetLabel = mission.TargetLabel,
            TargetPosition = new PositionDTO
            {
                X = mission.TargetPosition.X,
                Y = mission.TargetPosition.Z
            },
            IssuerPosition = new PositionDTO
            {
                X = mission.IssuerPosition.X,
                Y = mission.IssuerPosition.Z
            },
            TargetThreat = mission.TargetThreat,
            RewardGold = mission.RewardGold,
            Brief = mission.Brief,
            ObjectiveDone = mission.ObjectiveDone
        };
    }

    public static MissionStatsDTO BuildMissionStats(
        IEnumerable<Stalker> stalkers,
        MissionRegistry missions)
    {
        var alive = stalkers.Where(s => s.IsAlive).ToList();
        var active = alive.Where(s => s.ActiveMission != null).ToList();
        return new MissionStatsDTO
        {
            ActiveCount = active.Count,
            LeadersActive = active.Count(s => s.IsSquadLeader),
            ScoutCount = active.Count(s => s.ActiveMission!.Type == MissionType.ScoutPoi),
            StashCount = active.Count(s => s.ActiveMission!.Type == MissionType.RetrieveStash),
            EscortCount = active.Count(s => s.ActiveMission!.Type == MissionType.EscortConvoy),
            AcceptedLifetime = SimulationDebugLog.MissionsAccepted,
            CompletedLifetime = SimulationDebugLog.MissionsCompleted,
            TotalOffers = missions.OffersByIssuer.Values.Sum(o => o.Count),
            BasesWithOffers = missions.OffersByIssuer.Count
        };
    }

    public static KillStatsDTO BuildKillStats(Stalker s) => new()
    {
        Total = s.Rank.Kills,
        StalkerKills = s.Rank.StalkerKills,
        MutantKills = s.Rank.MutantKills
    };

    public static EquipmentDTO BuildEquipment(Stalker s)
    {
        var profile = ProtectionProfile.From(s);
        return new EquipmentDTO
        {
            Weapon = s.Equipment.PrimaryWeapon?.DisplayName ?? "None",
            Armor = s.Equipment.EquippedArmor?.DisplayName ?? "None",
            Helmet = s.Equipment.EquippedHelmet?.DisplayName ?? "None",
            SecondaryWeapon = s.Equipment.SecondaryWeapon?.DisplayName ?? "",
            WeaponId = s.Equipment.PrimaryWeapon?.Id ?? "",
            ArmorId = s.Equipment.EquippedArmor?.Id ?? "",
            HelmetId = s.Equipment.EquippedHelmet?.Id ?? "",
            SecondaryWeaponId = s.Equipment.SecondaryWeapon?.Id ?? "",
            WeaponCondition = s.Equipment.PrimaryWeapon?.Condition ?? 0f,
            ArmorCondition = s.Equipment.EquippedArmor?.Condition ?? 0f,
            HelmetCondition = s.Equipment.EquippedHelmet?.Condition ?? 0f,
            WeaponGammaId = ResolveGammaId(s.Equipment.PrimaryWeapon?.Id),
            ArmorGammaId = s.Equipment.EquippedArmor?.GammaId
                ?? ResolveGammaId(s.Equipment.EquippedArmor?.Id),
            HelmetGammaId = s.Equipment.EquippedHelmet?.GammaId
                ?? ResolveGammaId(s.Equipment.EquippedHelmet?.Id),
            Belt = s.Belt.Slots
                .Where(slot => slot.Type != BeltItemType.Empty)
                .Select(slot => slot.ItemId)
                .ToList(),
            Protection = ToProtectionDto(profile.Total)
        };
    }

    public static CorpseDTO BuildCorpse(Corpse c, float gameTime)
    {
        float despawnSec = ComputeDespawnRemaining(c, gameTime);
        return new CorpseDTO
        {
            Id = c.CorpseId,
            VictimName = c.VictimName,
            VictimFaction = c.VictimFaction,
            Position = new PositionDTO { X = c.Position.X, Y = c.Position.Z },
            Cause = c.CauseOfDeath.ToString(),
            IsEaten = c.IsEaten,
            IsReported = c.IsReported,
            IsMutant = c.IsMutant,
            IsLooted = c.Loot?.IsLooted == true,
            IsPatchIntact = c.IsPatchIntact,
            AgeSec = Math.Max(0f, gameTime - c.SpawnGameTime),
            DespawnSec = despawnSec,
            Loot = BuildCorpseLoot(c.Loot)
        };
    }

    public static CorpseLootDTO? BuildCorpseLoot(CorpseGearSnapshot? loot)
    {
        if (loot == null) return null;
        return new CorpseLootDTO
        {
            IsLooted = loot.IsLooted,
            PrimaryWeapon = GearFromId(loot.PrimaryWeaponId, loot.PrimaryWeaponCondition),
            SecondaryWeapon = GearFromId(loot.SecondaryWeaponId, loot.SecondaryWeaponCondition),
            Armor = GearFromId(loot.ArmorId, loot.ArmorCondition),
            Helmet = GearFromId(loot.HelmetId, loot.HelmetCondition)
        };
    }

    public static ProtectionStatsDTO ToProtectionDto(ProtectionStats stats) => new()
    {
        Bullet = stats.Bullet,
        Slash = stats.Slash,
        Rad = stats.Rad,
        Burn = stats.Burn,
        Shock = stats.Shock,
        Chemical = stats.Chemical,
        Psi = stats.Psi,
        Strike = stats.Strike,
        Explosion = stats.Explosion
    };

    public static float ComputeDespawnRemaining(Corpse c, float gameTime)
    {
        if (CorpseCleanupService.ShouldDespawn(c, gameTime))
            return 0f;

        float sinceInteraction = gameTime - c.LastInteractionGameTime;
        float age = gameTime - c.SpawnGameTime;

        if (c.IsEaten)
            return Math.Max(0f, CorpseCleanupService.StalkerEatenDespawnSec - sinceInteraction);

        if (c.IsMutant)
        {
            bool interacted = c.IsReported || c.LastInteractionGameTime > c.SpawnGameTime + 0.01f;
            float limit = interacted
                ? CorpseCleanupService.MutantInteractedDespawnSec
                : CorpseCleanupService.MutantIdleDespawnSec;
            float elapsed = interacted ? sinceInteraction : age;
            return Math.Max(0f, limit - elapsed);
        }

        bool stalkerInteracted = c.IsReported || c.Loot?.IsLooted == true
            || c.LastInteractionGameTime > c.SpawnGameTime + 0.01f;
        float stalkerLimit = stalkerInteracted
            ? CorpseCleanupService.StalkerInteractedDespawnSec
            : CorpseCleanupService.StalkerIdleDespawnSec;
        float stalkerElapsed = stalkerInteracted ? sinceInteraction : age;
        return Math.Max(0f, stalkerLimit - stalkerElapsed);
    }

    private static GearItemDTO? GearFromId(string? id, float condition)
    {
        if (string.IsNullOrEmpty(id)) return null;
        ItemDatabase.EnsureLoaded();
        ItemDatabase.TryGet(id, out var def);
        return new GearItemDTO
        {
            Id = id,
            Name = def?.Name ?? id,
            GammaId = def?.GammaId ?? ResolveGammaId(id),
            Condition = condition
        };
    }

    private static string? ResolveGammaId(string? simId)
    {
        if (string.IsNullOrEmpty(simId)) return null;
        GammaItemCatalog.EnsureLoaded();
        return GammaItemCatalog.ResolveGammaId(simId);
    }
}
