using StalkerALifeSandbox.AI.Blackboards;
using StalkerALifeSandbox.Crafting;
using StalkerALifeSandbox.Systems;

namespace StalkerALifeSandbox.AI.GOAP.Actions;

/// <summary>
/// GOAP action: Stalker spends time at campfire repairing damaged weapons/armor
/// using scrap parts. Scrap is consumed; equipment condition is restored.
/// Requires: IsAtCampfire=true, HasGearDamage=true.
/// Effects:   GearRepaired=true, HasGearDamage=false.
/// </summary>
public sealed class ActionCraftUpgrade : GOAPAction
{
    private const float RepairTimeGameSec  = 40f;
    private const float WeaponRepairAmount = 0.20f;   // condition restored per repair session
    private const float ArmorRepairAmount  = 0.15f;
    private const float InsertBallisticMod = 0.05f;   // armor plate belt insert mod
    private const int   ScrapCostRepair    = 5;
    private const int   ScrapCostBeltInsert= 10;

    private GoapContext? _ctx;
    private float _timer;
    private bool _finished;

    public override string Name     => "CraftUpgrade";
    public override float BaseCost  => 3f;

    public void BindContext(GoapContext ctx) => _ctx = ctx;

    public override Dictionary<string, bool> GetPreconditions() => new()
    {
        [GoapKeys.IsAtCampfire] = true,
        [GoapKeys.HasGearDamage] = true
    };

    public override Dictionary<string, bool> GetEffects() => new()
    {
        [GoapKeys.GearRepaired]  = true,
        [GoapKeys.HasGearDamage] = false
    };

    public override bool IsValid(NPCBlackboard bb)
    {
        if (!bb.WorldStateBools.GetValueOrDefault(GoapKeys.IsAtCampfire)) return false;
        if (!bb.WorldStateBools.GetValueOrDefault(GoapKeys.HasGearDamage)) return false;
        var stalker = _ctx?.GetStalker(bb.OwnerId);
        return stalker != null && stalker.ScrapCount >= ScrapCostRepair;
    }

    public override void Enter(NPCBlackboard bb)
    {
        _timer    = RepairTimeGameSec;
        _finished = false;
        var stalker = _ctx?.GetStalker(bb.OwnerId);
        if (stalker != null)
        {
            stalker.Activity = "🔧 Field Repair";
            stalker.Blackboard.OverrideNavigationStatus = stalker.Activity;
        }
    }

    public override bool Execute(NPCBlackboard bb, float delta)
    {
        _timer -= delta;
        if (_timer > 0f) return false;

        var stalker = _ctx?.GetStalker(bb.OwnerId);
        if (stalker == null) return true;

        var crafting = new FieldCraftingSystem();

        // Field weapon repair
        if (stalker.Equipment.PrimaryWeapon?.Condition < 0.70f &&
            stalker.ScrapCount >= ScrapCostRepair)
        {
            bool success = crafting.TryCraftUpgrade(
                stalker.Attributes,
                FieldCraftingSystem.UpgradeType.WeaponScope, // reused as generic "tool repair"
                stalker.ScrapCount);

            if (success)
            {
                stalker.Equipment.PrimaryWeapon.Condition =
                    Math.Min(1f, stalker.Equipment.PrimaryWeapon.Condition + WeaponRepairAmount);
                stalker.ScrapCount -= ScrapCostRepair;
                SimulationDebugLog.WriteEvent("CRAFT",
                    $"{stalker.DisplayName} repaired {stalker.Equipment.PrimaryWeapon.DisplayName}" +
                    $" → condition {stalker.Equipment.PrimaryWeapon.Condition:P0}" +
                    $" (scrap used: {ScrapCostRepair})");
            }
            SkillEvaluator.RecordZoneSurvivalEvent(stalker, "harvest");
        }

        // Field armor repair
        if (stalker.Equipment.EquippedArmor?.Condition < 0.70f &&
            stalker.ScrapCount >= ScrapCostRepair)
        {
            bool success = crafting.TryCraftUpgrade(
                stalker.Attributes,
                FieldCraftingSystem.UpgradeType.ArmorKevlarWeave,
                stalker.ScrapCount);

            if (success)
            {
                stalker.Equipment.EquippedArmor.Condition =
                    Math.Min(1f, stalker.Equipment.EquippedArmor.Condition + ArmorRepairAmount);
                stalker.ScrapCount -= ScrapCostRepair;
                SimulationDebugLog.WriteEvent("CRAFT",
                    $"{stalker.DisplayName} repaired {stalker.Equipment.EquippedArmor.DisplayName}" +
                    $" → condition {stalker.Equipment.EquippedArmor.Condition:P0}" +
                    $" (scrap used: {ScrapCostRepair})");
            }
            SkillEvaluator.RecordZoneSurvivalEvent(stalker, "harvest");
        }

        // Belt insert craft: if scrap surplus, craft an armor plate insert
        if (stalker.ScrapCount >= ScrapCostBeltInsert && stalker.Belt.HasFreeSlot)
        {
            bool success = crafting.TryCraftUpgrade(
                stalker.Attributes,
                FieldCraftingSystem.UpgradeType.ArmorKevlarWeave,
                stalker.ScrapCount);

            if (success)
            {
                stalker.Belt.EquipArmorPlate("plate_field_scrap", InsertBallisticMod);
                stalker.ScrapCount -= ScrapCostBeltInsert;
                SimulationDebugLog.WriteEvent("CRAFT",
                    $"{stalker.DisplayName} crafted field scrap armor insert" +
                    $" (+{InsertBallisticMod:P0} ballistic) (scrap used: {ScrapCostBeltInsert})");
            }
        }

        _finished = true;
        return true;
    }

    public override void Exit(NPCBlackboard bb)
    {
        if (!_finished) return;
        GoapWorldStateSync.ApplyEffects(bb, GetEffects());
    }
}
