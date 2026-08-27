// FieldCraftingSystem.cs — 0.1 Hz macro tick: inventory acquisition + passive crafting
using StalkerALifeSandbox.Crafting;
using StalkerALifeSandbox.Entities.Characters;
using StalkerALifeSandbox.Entities.Equipment;
using StalkerALifeSandbox.Entities.Mutants;
using StalkerALifeSandbox.Systems;

namespace StalkerALifeSandbox.Core.Systems;

/// <summary>
/// 0.1 Hz macro system. Responsible for:
/// <list type="bullet">
///   <item>Scavenging: awarding RawMeat and Scrap to idle stalkers (simulates loot gather)</item>
///   <item>Passive cooking/repair for non-GOAP-planning squad members (non-leaders)</item>
///   <item>Gear condition degradation from combat exposure</item>
/// </list>
/// Thread-safe: takes a snapshot of the entity list before iteration.
/// Zero allocations in inner loops (no LINQ in hot path).
/// </summary>
public sealed class FieldCraftingSystem : ISimulationSystem
{
    // ── Constants ───────────────────────────────────────────────────────
    private const float GearDegradePerMacroTick = 0.002f;  // per 0.1 Hz tick (~0.02% per second)
    private const float ScavengeChance          = 0.06f;   // 6% chance per field/idle stalker per tick
    private const float MeatScavengeChance      = 0.04f;   // 4% chance for meat find (was 2%)
    private const float VodkaFindChance         = 0.015f;  // 1.5% chance to find vodka
    private const float PassiveCookChance       = 0.08f;   // non-leaders cook passively (was 6%)
    private const float PassiveRepairChance     = 0.06f;   // non-leaders repair passively (was 4%)

    // Scrap rewards
    private const int ScrapMin = 2;
    private const int ScrapMax = 8;
    private const int ScrapCap = 30;
    private const int MeatCap  = 5;

    private readonly Crafting.FieldCraftingSystem _crafting = new();
    private readonly MutantCookingSystem          _cooking  = new();

    // ── Telemetry counters (reset each tick for the snapshot) ────────────
    public int LastTickCooks   { get; private set; }
    public int LastTickRepairs { get; private set; }
    public int LastTickGearDegraded { get; private set; }

    public void Tick(SimulationContext ctx, float gameDelta)
    {
        LastTickCooks        = 0;
        LastTickRepairs      = 0;
        LastTickGearDegraded = 0;

        Stalker[] snapshot;
        lock (ctx.EntityLock) { snapshot = ctx.Stalkers.ToArray(); }

        for (int i = 0; i < snapshot.Length; i++)
        {
            var s = snapshot[i];
            if (!s.IsAlive) continue;

            TickScavenging(s);
            TickGearDegradation(s);

            // GOAP squad leaders have ActionCookMutantMeat / ActionCraftUpgrade driven by the planner.
            // Apply passive cooking/repair only to non-leader squad members who lack a planner slot.
            if (s.IsSquadLeader) continue;

            TickPassiveCooking(s);
            TickPassiveRepair(s);
        }
    }

    // ── Scavenging ───────────────────────────────────────────────────────

    private static void TickScavenging(Stalker s)
    {
        // Accumulate during idle, scouting, retrieval, or active combat (battlefield loot)
        bool inField = s.IdleAtBase
            || s.Activity == "🔭 Scouting"
            || s.Activity == "🎒 Retrieving"
            || s.Activity == "⚔️ Combat";
        if (!inField) return;

        // Scrap from environment
        if (s.ScrapCount < ScrapCap && Random.Shared.NextSingle() < ScavengeChance)
            s.ScrapCount += Random.Shared.Next(ScrapMin, ScrapMax + 1);

        // Raw meat from kills / wilderness foraging
        if (s.RawMeatCount < MeatCap && Random.Shared.NextSingle() < MeatScavengeChance)
            s.RawMeatCount++;

        // Vodka occasional find
        if (s.VodkaCount < 3 && Random.Shared.NextSingle() < VodkaFindChance)
            s.VodkaCount++;
    }

    // ── Gear Degradation ─────────────────────────────────────────────────

    private void TickGearDegradation(Stalker s)
    {
        bool degraded = false;

        if (s.Equipment.PrimaryWeapon != null)
        {
            s.Equipment.PrimaryWeapon.Condition =
                Math.Max(0.10f, s.Equipment.PrimaryWeapon.Condition - GearDegradePerMacroTick);
            degraded = true;
        }
        if (s.Equipment.EquippedArmor != null)
        {
            s.Equipment.EquippedArmor.Condition =
                Math.Max(0.10f, s.Equipment.EquippedArmor.Condition - GearDegradePerMacroTick);
            degraded = true;
        }
        if (s.Equipment.EquippedHelmet != null)
        {
            s.Equipment.EquippedHelmet.Condition =
                Math.Max(0.10f, s.Equipment.EquippedHelmet.Condition - GearDegradePerMacroTick);
        }

        if (degraded) LastTickGearDegraded++;
    }

    // ── Passive cooking (non-leader squad members) ────────────────────────

    private void TickPassiveCooking(Stalker s)
    {
        if (s.RawMeatCount <= 0) return;
        if (s.Needs.Hunger < 5.0f) return;  // was 0.35f — hunger drains ~7 pts/30 real-min, threshold was never reached
        if (Random.Shared.NextSingle() >= PassiveCookChance) return;

        var meal = _cooking.Cook(MutantMeatType.Boar);
        _cooking.Eat(meal, s.Needs, s.VodkaCount, out int vodkaConsumed);

        bool purged = vodkaConsumed > 0;
        s.VodkaCount   = Math.Max(0, s.VodkaCount - vodkaConsumed);
        s.RawMeatCount = Math.Max(0, s.RawMeatCount - 1);

        SimulationDebugLog.WriteEvent("COOK",
            $"{s.DisplayName} (passive) cooked Boar meat" +
            $" | purged: {purged}" +
            $" | hunger → {s.Needs.Hunger:F2}");

        SkillEvaluator.RecordZoneSurvivalEvent(s, "cook");
        SimulationDebugLog.CookEvent();
        LastTickCooks++;
    }

    // ── Passive repair (non-leader squad members) ─────────────────────────

    private void TickPassiveRepair(Stalker s)
    {
        if (s.ScrapCount < 5) return;
        if (Random.Shared.NextSingle() >= PassiveRepairChance) return;

        bool repaired = false;

        if (s.Equipment.PrimaryWeapon?.Condition < 0.70f)
        {
            if (_crafting.TryCraftUpgrade(s.Attributes,
                    Crafting.FieldCraftingSystem.UpgradeType.WeaponScope, s.ScrapCount))
            {
                s.Equipment.PrimaryWeapon.Condition =
                    Math.Min(1f, s.Equipment.PrimaryWeapon.Condition + 0.15f);
                s.ScrapCount -= 5;
                repaired = true;
                SimulationDebugLog.WriteEvent("CRAFT",
                    $"{s.DisplayName} (passive) repaired {s.Equipment.PrimaryWeapon.DisplayName}" +
                    $" → {s.Equipment.PrimaryWeapon.Condition:P0}");
            }
        }

        if (!repaired && s.Equipment.EquippedArmor?.Condition < 0.70f && s.ScrapCount >= 5)
        {
            if (_crafting.TryCraftUpgrade(s.Attributes,
                    Crafting.FieldCraftingSystem.UpgradeType.ArmorKevlarWeave, s.ScrapCount))
            {
                s.Equipment.EquippedArmor.Condition =
                    Math.Min(1f, s.Equipment.EquippedArmor.Condition + 0.12f);
                s.ScrapCount -= 5;
                repaired = true;
                SimulationDebugLog.WriteEvent("CRAFT",
                    $"{s.DisplayName} (passive) repaired {s.Equipment.EquippedArmor.DisplayName}" +
                    $" → {s.Equipment.EquippedArmor.Condition:P0}");
            }
        }

        if (repaired)
        {
            SkillEvaluator.RecordZoneSurvivalEvent(s, "harvest");
            SimulationDebugLog.RepairEvent();
            LastTickRepairs++;
        }
    }
}
