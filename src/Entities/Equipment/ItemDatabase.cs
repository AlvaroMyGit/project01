using StalkerALifeSandbox.Entities.Characters;

namespace StalkerALifeSandbox.Entities.Equipment;

// ─────────────────────────────────────────────────────────────────────────────
//  ItemDatabase  —  Backward-compatible static façade
//
//  Delegates all real work to the three focused sub-services:
//    • ItemRegistry          — raw item storage & lookup
//    • ItemFactory           — item instance construction
//    • SpawnLoadoutResolver  — faction gear tiers & spawn loadouts
//
//  All existing call-sites (GammaItemCatalog, GOAP actions, systems…) continue
//  to compile without change.  Migrate them to the sub-services incrementally.
// ─────────────────────────────────────────────────────────────────────────────
public static class ItemDatabase
{
    // ── Shared data type (kept here for backward compatibility) ──────────
    public sealed class ItemDefinition
    {
        public string  Id                   { get; set; } = "";
        public string  Name                 { get; set; } = "";
        public float   BaseValue            { get; set; }
        public string  Category             { get; set; } = "";
        public bool    IsArtifactOrSpecimen { get; set; }
        public float   BallisticMod         { get; set; }
        public float   RadResist            { get; set; }
        public string? GammaId             { get; set; }
        public string? FactionPatch        { get; set; }
        public float   Damage               { get; set; }
        public float   Accuracy             { get; set; }
        public float   FireRate             { get; set; }
        public int     MagSize              { get; set; }
    }

    // ── Bootstrap ────────────────────────────────────────────────────────

    /// <summary>Loads all data files. Safe to call multiple times.</summary>
    public static void EnsureLoaded() => SpawnLoadoutResolver.Instance.EnsureLoaded();

    // ── ItemRegistry façade ──────────────────────────────────────────────

    public static void RegisterItem(ItemDefinition def) =>
        ItemRegistry.Instance.Register(def);

    public static bool TryGet(string id, out ItemDefinition def) =>
        ItemRegistry.Instance.TryGet(id, out def!);

    public static IReadOnlyDictionary<string, ItemDefinition> All =>
        ItemRegistry.Instance.All;

    public static float GetBaseValue(string id)
    {
        EnsureLoaded();
        return ItemRegistry.Instance.GetBaseValue(id);
    }

    // ── ItemFactory façade ───────────────────────────────────────────────

    public static WeaponItem CreateWeapon(string id, float condition = 0.85f)
    {
        EnsureLoaded();
        return ItemFactory.Instance.CreateWeapon(id, condition);
    }

    public static ArmorItem CreateArmor(string id, string? factionPatch, float condition = 0.9f)
    {
        EnsureLoaded();
        return ItemFactory.Instance.CreateArmor(id, factionPatch, condition);
    }

    public static HelmetItem CreateHelmet(string id, float condition = 0.9f)
    {
        EnsureLoaded();
        return ItemFactory.Instance.CreateHelmet(id, condition);
    }

    public static DetectorItem? CreateDetector(string id)
    {
        EnsureLoaded();
        return ItemFactory.CreateDetector(id);
    }

    public static string PickArtifactId(float rarityScore)
    {
        EnsureLoaded();
        return ItemFactory.PickArtifactId(rarityScore);
    }

    public static ProtectionStats ResolveGammaProtection(string itemId, string category)
    {
        EnsureLoaded();
        return ItemFactory.Instance.ResolveGammaProtection(itemId, category);
    }

    // ── SpawnLoadoutResolver façade ──────────────────────────────────────

    public static int GetGearTier(string faction) =>
        SpawnLoadoutResolver.Instance.GetGearTier(faction);

    public static void ApplySpawnLoadout(Stalker stalker, bool isLeader = false) =>
        SpawnLoadoutResolver.Instance.ApplySpawnLoadout(stalker, isLeader);
}
