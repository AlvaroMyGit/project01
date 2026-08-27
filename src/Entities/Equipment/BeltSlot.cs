// BeltSlot.cs — 1-6 slots for Artifacts, Armor Plates, and Mutant Pelts
using StalkerALifeSandbox.World.Hazards;

namespace StalkerALifeSandbox.Entities.Equipment;

public enum BeltItemType { Empty, Artifact, ArmorPlate, MutantPelt }

/// <summary>One item in the NPC's belt container.</summary>
public sealed class BeltSlotItem
{
    public BeltItemType Type         { get; init; } = BeltItemType.Empty;
    public string       ItemId       { get; init; } = "";
    public float        RarityScore  { get; init; }   // for artifacts
    public float        BallisticMod { get; init; }   // for armor plates (0.0 - 1.0 reduction)
    public float        RadResist    { get; init; }   // for mutant pelts
}

/// <summary>
/// Belt equipment container supporting 1-6 slots.
/// Can hold Artifacts (stat buffs), Armor Plates (ballistic absorption),
/// and Mutant Pelts (radiation/bleed resistance).
/// </summary>
public sealed class BeltSlot
{
    public const int MaxSlots = 6;
    private readonly BeltSlotItem[] _slots = new BeltSlotItem[MaxSlots];

    public BeltSlot()
    {
        for (int i = 0; i < MaxSlots; i++)
            _slots[i] = new BeltSlotItem();
    }

    public IReadOnlyList<BeltSlotItem> Slots => _slots;

    public bool HasFreeSlot => Array.Exists(_slots, s => s.Type == BeltItemType.Empty);

    /// <summary>Equip an artifact into the first free slot.</summary>
    public bool EquipArtifact(ArtifactData artifact)
    {
        for (int i = 0; i < MaxSlots; i++)
        {
            if (_slots[i].Type == BeltItemType.Empty)
            {
                _slots[i] = new BeltSlotItem
                {
                    Type = BeltItemType.Artifact,
                    ItemId = artifact.Id,
                    RarityScore = artifact.RarityScore
                };
                return true;
            }
        }
        return false;
    }

    /// <summary>Equip an armor plate into the first free slot.</summary>
    public bool EquipArmorPlate(string plateId, float ballisticMod)
    {
        for (int i = 0; i < MaxSlots; i++)
        {
            if (_slots[i].Type == BeltItemType.Empty)
            {
                _slots[i] = new BeltSlotItem
                {
                    Type = BeltItemType.ArmorPlate,
                    ItemId = plateId,
                    BallisticMod = ballisticMod
                };
                return true;
            }
        }
        return false;
    }

    /// <summary>Equip a mutant pelt into the first free slot.</summary>
    public bool EquipMutantPelt(string peltId, float radResist)
    {
        for (int i = 0; i < MaxSlots; i++)
        {
            if (_slots[i].Type == BeltItemType.Empty)
            {
                _slots[i] = new BeltSlotItem
                {
                    Type = BeltItemType.MutantPelt,
                    ItemId = peltId,
                    RadResist = radResist
                };
                return true;
            }
        }
        return false;
    }

    /// <summary>Remove an item from a slot by index.</summary>
    public void ClearSlot(int index)
    {
        if (index >= 0 && index < MaxSlots)
            _slots[index] = new BeltSlotItem();
    }

    /// <summary>Total ballistic damage reduction from all armor plates.</summary>
    public float TotalBallisticAbsorption =>
        _slots.Where(s => s.Type == BeltItemType.ArmorPlate).Sum(s => s.BallisticMod);

    /// <summary>Total radiation resistance from all mutant pelts.</summary>
    public float TotalRadResistance =>
        _slots.Where(s => s.Type == BeltItemType.MutantPelt).Sum(s => s.RadResist);
}
