using System.Text;
using StalkerALifeSandbox.Entities.Characters;
using StalkerALifeSandbox.Entities.Equipment;

namespace StalkerALifeSandbox.UI.Inspector;

/// <summary>
/// Debug/inspection panel that displays a selected NPC's
/// health, inventory, goals, faction, and suspicion meters.
/// </summary>
public sealed class InspectorPanel
{
    public string? SelectedNpcId { get; set; }
    public bool    IsOpen        { get; set; }

    public string Render(Stalker npc)
    {
        var sb = new StringBuilder();
        
        sb.AppendLine("==================================================");
        sb.AppendLine($"   NPC INSPECTOR: {npc.DisplayName} [{npc.Id}]");
        sb.AppendLine("==================================================");
        sb.AppendLine($"STATUS: {(npc.IsAlive ? "ALIVE" : "DEAD")}");
        sb.AppendLine($"FACTION: {npc.TrueFaction} (Apparent: {npc.ApparentFaction})");
        sb.AppendLine($"RANK: {npc.Rank.CurrentRank} (XP: {npc.Rank.TotalXP})");
        sb.AppendLine($"POSITION: {npc.Position}");
        sb.AppendLine();
        
        sb.AppendLine("--- VITALS & NEEDS ---");
        sb.AppendLine($"Fatigue:   {npc.Needs.Fatigue:F1}%");
        sb.AppendLine($"Hunger:    {npc.Needs.Hunger:F1}%");
        sb.AppendLine($"Radiation: {npc.Needs.Radiation:F1} Sv");
        sb.AppendLine($"Morale:    {npc.Needs.Morale:F1}%");
        sb.AppendLine($"Gold:      {npc.Needs.GoldAmount:F0} RU");
        sb.AppendLine();

        sb.AppendLine("--- RPG SKILLS ---");
        sb.AppendLine($"Marksmanship: {npc.Attributes.Marksmanship}");
        sb.AppendLine($"ZoneSurvival: {npc.Attributes.ZoneSurvival}");
        sb.AppendLine($"Charisma:     {npc.Attributes.Charisma}");
        sb.AppendLine($"Trust:        {npc.Attributes.Trustworthiness}");
        sb.AppendLine();

        sb.AppendLine("--- EQUIPMENT & INVENTORY ---");
        sb.AppendLine($"Weapon: {(npc.Equipment.PrimaryWeapon != null ? npc.Equipment.PrimaryWeapon.Id : "None")}");
        sb.AppendLine($"Armor:  {(npc.Equipment.EquippedArmor != null ? npc.Equipment.EquippedArmor.Id : "None")}");
        sb.AppendLine($"Belt Slots:");
        for (int i = 0; i < npc.Belt.Slots.Count; i++)
        {
            var slot = npc.Belt.Slots[i];
            if (slot.Type == BeltItemType.Empty)
                sb.AppendLine($"  [{i + 1}] Empty");
            else
                sb.AppendLine($"  [{i + 1}] {slot.Type}: {slot.ItemId}");
        }
        sb.AppendLine($"Inventory Items: {npc.Equipment.Backpack.Count}");
        sb.AppendLine();

        sb.AppendLine("--- MEMORY & DISGUISE ---");
        sb.AppendLine($"Suspicion Meter: {npc.Blackboard.SuspicionLevel:F1}%");
        sb.AppendLine($"Threat Map Known Nodes: {npc.Blackboard.LocationThreatMemory.Count}");
        sb.AppendLine("==================================================");
        
        return sb.ToString();
    }
}
