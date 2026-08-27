using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace StalkerALifeSandbox.Web
{
    public class TelemetryFrame
    {
        [JsonPropertyName("tick")]
        public long Tick { get; set; }

        [JsonPropertyName("timeOfDay")]
        public string TimeOfDay { get; set; } = string.Empty;

        [JsonPropertyName("weather")]
        public string Weather { get; set; } = string.Empty;

        [JsonPropertyName("stormActive")]
        public bool StormActive { get; set; }

        [JsonPropertyName("emissionPhase")]
        public string EmissionPhase { get; set; } = "Dormant";

        [JsonPropertyName("anomalyFields")]
        public List<AnomalyFieldDTO> AnomalyFields { get; set; } = new();

        [JsonPropertyName("entities")]
        public List<EntityDTO> Entities { get; set; } = new List<EntityDTO>();

        [JsonPropertyName("corpses")]
        public List<CorpseDTO> Corpses { get; set; } = new();

        [JsonPropertyName("missionStats")]
        public MissionStatsDTO? MissionStats { get; set; }
    }

    public class MissionDTO
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = "";

        [JsonPropertyName("issuerName")]
        public string IssuerName { get; set; } = "";

        [JsonPropertyName("issuerFaction")]
        public string IssuerFaction { get; set; } = "";

        [JsonPropertyName("targetLabel")]
        public string TargetLabel { get; set; } = "";

        [JsonPropertyName("targetPosition")]
        public PositionDTO? TargetPosition { get; set; }

        [JsonPropertyName("targetThreat")]
        public float TargetThreat { get; set; }

        [JsonPropertyName("rewardGold")]
        public float RewardGold { get; set; }

        [JsonPropertyName("brief")]
        public string Brief { get; set; } = "";

        [JsonPropertyName("objectiveDone")]
        public bool ObjectiveDone { get; set; }

        [JsonPropertyName("issuerPosition")]
        public PositionDTO? IssuerPosition { get; set; }
    }

    public class MissionStatsDTO
    {
        [JsonPropertyName("activeCount")]
        public int ActiveCount { get; set; }

        [JsonPropertyName("leadersActive")]
        public int LeadersActive { get; set; }

        [JsonPropertyName("scoutCount")]
        public int ScoutCount { get; set; }

        [JsonPropertyName("stashCount")]
        public int StashCount { get; set; }

        [JsonPropertyName("escortCount")]
        public int EscortCount { get; set; }

        [JsonPropertyName("acceptedLifetime")]
        public long AcceptedLifetime { get; set; }

        [JsonPropertyName("completedLifetime")]
        public long CompletedLifetime { get; set; }

        [JsonPropertyName("totalOffers")]
        public int TotalOffers { get; set; }

        [JsonPropertyName("basesWithOffers")]
        public int BasesWithOffers { get; set; }
    }

    public class AnomalyFieldDTO
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;

        [JsonPropertyName("center")]
        public AnomalyCenter Center { get; set; } = new();

        [JsonPropertyName("radius")]
        public float Radius { get; set; }

        [JsonPropertyName("intensity")]
        public float Intensity { get; set; }

        [JsonPropertyName("isStatic")]
        public bool IsStatic { get; set; }
    }

    public class AnomalyCenter
    {
        [JsonPropertyName("x")]
        public float X { get; set; }

        [JsonPropertyName("y")]
        public float Y { get; set; }

        [JsonPropertyName("z")]
        public float Z { get; set; }
    }

    public class EntityDTO
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("faction")]
        public string Faction { get; set; } = string.Empty;

        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty; // "stalker", "mutant"

        [JsonPropertyName("position")]
        public PositionDTO Position { get; set; } = new PositionDTO();

        [JsonPropertyName("levelId")]
        public string LevelId { get; set; } = string.Empty;

        [JsonPropertyName("layerIndex")]
        public int LayerIndex { get; set; } // -1 (underground), 0 (surface), 1 (interior)

        [JsonPropertyName("health")]
        public int Health { get; set; }

        [JsonPropertyName("currentGoal")]
        public string CurrentGoal { get; set; } = string.Empty;

        [JsonPropertyName("equipment")]
        public EquipmentDTO? Equipment { get; set; }

        [JsonPropertyName("desperation")]
        public bool Desperation { get; set; }

        [JsonPropertyName("activity")]
        public string Activity { get; set; } = "";

        [JsonPropertyName("mission")]
        public MissionDTO? Mission { get; set; }

        [JsonPropertyName("squadId")]
        public string? SquadId { get; set; }

        [JsonPropertyName("isSquadLeader")]
        public bool IsSquadLeader { get; set; }
    }

    public class PositionDTO
    {
        [JsonPropertyName("x")]
        public float X { get; set; }

        [JsonPropertyName("y")]
        public float Y { get; set; }
    }

    public class EquipmentDTO
    {
        [JsonPropertyName("weapon")]
        public string Weapon { get; set; } = string.Empty;

        [JsonPropertyName("armor")]
        public string Armor { get; set; } = string.Empty;

        [JsonPropertyName("helmet")]
        public string Helmet { get; set; } = string.Empty;

        [JsonPropertyName("secondaryWeapon")]
        public string SecondaryWeapon { get; set; } = string.Empty;

        [JsonPropertyName("belt")]
        public List<string> Belt { get; set; } = new List<string>();

        [JsonPropertyName("weaponId")]
        public string WeaponId { get; set; } = "";

        [JsonPropertyName("armorId")]
        public string ArmorId { get; set; } = "";

        [JsonPropertyName("helmetId")]
        public string HelmetId { get; set; } = "";

        [JsonPropertyName("secondaryWeaponId")]
        public string SecondaryWeaponId { get; set; } = "";

        [JsonPropertyName("weaponCondition")]
        public float WeaponCondition { get; set; }

        [JsonPropertyName("armorCondition")]
        public float ArmorCondition { get; set; }

        [JsonPropertyName("helmetCondition")]
        public float HelmetCondition { get; set; }

        [JsonPropertyName("weaponGammaId")]
        public string? WeaponGammaId { get; set; }

        [JsonPropertyName("armorGammaId")]
        public string? ArmorGammaId { get; set; }

        [JsonPropertyName("helmetGammaId")]
        public string? HelmetGammaId { get; set; }

        [JsonPropertyName("protection")]
        public ProtectionStatsDTO? Protection { get; set; }
    }

    public class ProtectionStatsDTO
    {
        [JsonPropertyName("bullet")]
        public float Bullet { get; set; }

        [JsonPropertyName("slash")]
        public float Slash { get; set; }

        [JsonPropertyName("rad")]
        public float Rad { get; set; }

        [JsonPropertyName("burn")]
        public float Burn { get; set; }

        [JsonPropertyName("shock")]
        public float Shock { get; set; }

        [JsonPropertyName("chemical")]
        public float Chemical { get; set; }

        [JsonPropertyName("psi")]
        public float Psi { get; set; }

        [JsonPropertyName("strike")]
        public float Strike { get; set; }

        [JsonPropertyName("explosion")]
        public float Explosion { get; set; }
    }

    public class GearItemDTO
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = "";

        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonPropertyName("gammaId")]
        public string? GammaId { get; set; }

        [JsonPropertyName("condition")]
        public float Condition { get; set; }
    }

    public class CorpseLootDTO
    {
        [JsonPropertyName("isLooted")]
        public bool IsLooted { get; set; }

        [JsonPropertyName("primaryWeapon")]
        public GearItemDTO? PrimaryWeapon { get; set; }

        [JsonPropertyName("secondaryWeapon")]
        public GearItemDTO? SecondaryWeapon { get; set; }

        [JsonPropertyName("armor")]
        public GearItemDTO? Armor { get; set; }

        [JsonPropertyName("helmet")]
        public GearItemDTO? Helmet { get; set; }
    }

    public class CorpseDTO
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = "";

        [JsonPropertyName("victimName")]
        public string VictimName { get; set; } = "";

        [JsonPropertyName("victimFaction")]
        public string VictimFaction { get; set; } = "";

        [JsonPropertyName("position")]
        public PositionDTO Position { get; set; } = new();

        [JsonPropertyName("cause")]
        public string Cause { get; set; } = "";

        [JsonPropertyName("isEaten")]
        public bool IsEaten { get; set; }

        [JsonPropertyName("isReported")]
        public bool IsReported { get; set; }

        [JsonPropertyName("isMutant")]
        public bool IsMutant { get; set; }

        [JsonPropertyName("isLooted")]
        public bool IsLooted { get; set; }

        [JsonPropertyName("isPatchIntact")]
        public bool IsPatchIntact { get; set; }

        [JsonPropertyName("ageSec")]
        public float AgeSec { get; set; }

        [JsonPropertyName("despawnSec")]
        public float DespawnSec { get; set; }

        [JsonPropertyName("loot")]
        public CorpseLootDTO? Loot { get; set; }
    }

    /// <summary>On-demand entity inspect payload (WebSocket response).</summary>
    public class InspectorDTO
    {
        [JsonPropertyName("entityId")]
        public string EntityId { get; set; } = "";

        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonPropertyName("faction")]
        public string Faction { get; set; } = "";

        [JsonPropertyName("apparentFaction")]
        public string ApparentFaction { get; set; } = "";

        [JsonPropertyName("type")]
        public string Type { get; set; } = "";

        [JsonPropertyName("isAlive")]
        public bool IsAlive { get; set; }

        [JsonPropertyName("health")]
        public int Health { get; set; }

        [JsonPropertyName("layerIndex")]
        public int LayerIndex { get; set; }

        [JsonPropertyName("levelId")]
        public string LevelId { get; set; } = "";

        [JsonPropertyName("rank")]
        public string Rank { get; set; } = "";

        [JsonPropertyName("currentGoal")]
        public string CurrentGoal { get; set; } = "";

        [JsonPropertyName("activity")]
        public string Activity { get; set; } = "";

        [JsonPropertyName("desperation")]
        public bool Desperation { get; set; }

        [JsonPropertyName("position")]
        public PositionDTO Position { get; set; } = new();

        [JsonPropertyName("needs")]
        public NeedsDTO? Needs { get; set; }

        [JsonPropertyName("skills")]
        public SkillsDTO? Skills { get; set; }

        [JsonPropertyName("equipment")]
        public EquipmentDTO? Equipment { get; set; }

        [JsonPropertyName("suspicionLevel")]
        public float SuspicionLevel { get; set; }

        [JsonPropertyName("threatMemory")]
        public Dictionary<string, float> ThreatMemory { get; set; } = new();

        [JsonPropertyName("squadId")]
        public string? SquadId { get; set; }

        [JsonPropertyName("isSquadLeader")]
        public bool IsSquadLeader { get; set; }

        [JsonPropertyName("corpseLoot")]
        public CorpseLootDTO? CorpseLoot { get; set; }

        [JsonPropertyName("causeOfDeath")]
        public string? CauseOfDeath { get; set; }

        [JsonPropertyName("isReported")]
        public bool IsReported { get; set; }

        [JsonPropertyName("isLooted")]
        public bool IsLooted { get; set; }

        [JsonPropertyName("isPatchIntact")]
        public bool IsPatchIntact { get; set; }

        [JsonPropertyName("ageSec")]
        public float AgeSec { get; set; }

        [JsonPropertyName("despawnSec")]
        public float DespawnSec { get; set; }

        [JsonPropertyName("mission")]
        public MissionDTO? Mission { get; set; }

        [JsonPropertyName("missionsCompleted")]
        public int MissionsCompleted { get; set; }

        [JsonPropertyName("hasMissionOffer")]
        public bool HasMissionOffer { get; set; }

        [JsonPropertyName("killStats")]
        public KillStatsDTO? KillStats { get; set; }

        [JsonPropertyName("lastCookEvent")]
        public string? LastCookEvent { get; set; }

        [JsonPropertyName("lastCraftEvent")]
        public string? LastCraftEvent { get; set; }

        [JsonPropertyName("rawMeatCount")]
        public int RawMeatCount { get; set; }

        [JsonPropertyName("scrapCount")]
        public int ScrapCount { get; set; }

        [JsonPropertyName("vodkaCount")]
        public int VodkaCount { get; set; }
    }

    public class KillStatsDTO
    {
        [JsonPropertyName("total")]
        public int Total { get; set; }

        [JsonPropertyName("stalkerKills")]
        public int StalkerKills { get; set; }

        [JsonPropertyName("mutantKills")]
        public int MutantKills { get; set; }
    }

    public class LeaderboardEntryDTO
    {
        [JsonPropertyName("position")]
        public int Position { get; set; }

        [JsonPropertyName("id")]
        public string Id { get; set; } = "";

        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonPropertyName("faction")]
        public string Faction { get; set; } = "";

        [JsonPropertyName("rank")]
        public string Rank { get; set; } = "";

        [JsonPropertyName("xp")]
        public int Xp { get; set; }

        [JsonPropertyName("kills")]
        public int Kills { get; set; }

        [JsonPropertyName("stalkerKills")]
        public int StalkerKills { get; set; }

        [JsonPropertyName("mutantKills")]
        public int MutantKills { get; set; }

        [JsonPropertyName("missions")]
        public int Missions { get; set; }

        [JsonPropertyName("positionCoords")]
        public PositionDTO? PositionCoords { get; set; }
    }

    public class NeedsDTO
    {
        [JsonPropertyName("hunger")]
        public float Hunger { get; set; }

        [JsonPropertyName("thirst")]
        public float Thirst { get; set; }

        [JsonPropertyName("radiation")]
        public float Radiation { get; set; }

        [JsonPropertyName("fatigue")]
        public float Fatigue { get; set; }

        [JsonPropertyName("morale")]
        public float Morale { get; set; }

        [JsonPropertyName("gold")]
        public float Gold { get; set; }

        [JsonPropertyName("ammo")]
        public int Ammo { get; set; }
    }

    public class SkillsDTO
    {
        [JsonPropertyName("marksmanship")]
        public int Marksmanship { get; set; }

        [JsonPropertyName("zoneSurvival")]
        public int ZoneSurvival { get; set; }

        [JsonPropertyName("charisma")]
        public int Charisma { get; set; }

        [JsonPropertyName("trustworthiness")]
        public int Trustworthiness { get; set; }
    }

    /// <summary>A single kill event for the live kill feed.</summary>
    public class KillEventDTO
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("timestamp")]
        public long Timestamp { get; set; }

        [JsonPropertyName("gameTime")]
        public string GameTime { get; set; } = string.Empty;

        [JsonPropertyName("victimName")]
        public string VictimName { get; set; } = string.Empty;

        [JsonPropertyName("victimFaction")]
        public string VictimFaction { get; set; } = string.Empty;

        [JsonPropertyName("victimType")]
        public string VictimType { get; set; } = string.Empty; // "stalker" or "mutant"

        [JsonPropertyName("killerName")]
        public string KillerName { get; set; } = string.Empty;

        [JsonPropertyName("killerFaction")]
        public string KillerFaction { get; set; } = string.Empty;

        [JsonPropertyName("killerType")]
        public string KillerType { get; set; } = string.Empty; // "stalker", "mutant", "emission", "anomaly"

        [JsonPropertyName("cause")]
        public string Cause { get; set; } = string.Empty; // "Gunfire", "Mutant", "Emission", etc.

        [JsonPropertyName("posX")]
        public float PosX { get; set; }

        [JsonPropertyName("posY")]
        public float PosY { get; set; }
    }
}
