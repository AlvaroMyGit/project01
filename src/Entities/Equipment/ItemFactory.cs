namespace StalkerALifeSandbox.Entities.Equipment;

/// <summary>
/// Constructs strongly-typed item instances (weapons, armour, helmets,
/// detectors, artifacts) from the raw data in <see cref="ItemRegistry"/>.
/// </summary>
public sealed class ItemFactory
{
    // ── Singleton ────────────────────────────────────────────────────────
    public static ItemFactory Instance { get; } = new(ItemRegistry.Instance);

    private readonly ItemRegistry _registry;

    public ItemFactory(ItemRegistry registry)
    {
        _registry = registry;
    }

    // ── Fallback tables (shared with SpawnLoadoutResolver via ItemDatabase) ──

    internal static readonly Dictionary<string, (float bullet, float slash, float rad, float anomaly, string? patch)>
        ArmorFallback = new()
    {
        ["arm_leather"] = (0.15f, 0.14f, 0.07f, 0.11f, null),
        ["arm_stalker"] = (0.35f, 0.12f, 0.15f, 0.35f, null),
        ["arm_bandit"]  = (0.25f, 0.07f, 0.04f, 0.06f, "Bandit"),
        ["arm_duty"]    = (0.55f, 0.27f, 0.04f, 0.08f, "Duty"),
        ["arm_freedom"] = (0.40f, 0.40f, 0.16f, 0.06f, "Freedom"),
        ["arm_seva"]    = (0.45f, 0.07f, 0.71f, 0.48f, "Ecologist"),
        ["arm_exo"]     = (0.80f, 0.27f, 0.19f, 0.21f, null),
    };

    internal static readonly Dictionary<string, string> DefaultGammaIds =
        new(StringComparer.OrdinalIgnoreCase)
    {
        ["arm_leather"]  = "trenchcoat_outfit",
        ["arm_stalker"]  = "stalker_outfit",
        ["arm_bandit"]   = "bandit_novice_outfit",
        ["arm_duty"]     = "dolg_heavy_outfit",
        ["arm_freedom"]  = "light_freedom_outfit",
        ["arm_seva"]     = "ecolog_outfit_orange",
        ["arm_exo"]      = "stalker_proto_exo_outfit",
        ["helm_bandana"] = "helm_bandana",
        ["helm_hardhat"] = "helm_hardhat",
        ["helm_tactic"]  = "helm_battle",
        ["helm_exo"]     = "helm_exo",
        ["helm_gas"]     = "helm_m50",
        ["plate_ceramic"]= "af_plates",
        ["plate_titan"]  = "af_plates_up",
        ["art_fireball"] = "af_fireball",
        ["art_gravi"]    = "af_gravi",
        ["art_soul"]     = "af_soul",
        ["art_jellyfish"]= "af_medusa",
        ["art_nightstar"]= "af_night_star",
        ["art_compass"]  = "af_compass",
    };

    private static readonly (string id, float minRarity)[] ArtifactTiers =
    {
        ("art_fireball",  0.0f),
        ("art_soul",      0.15f),
        ("art_gravi",     0.30f),
        ("art_jellyfish", 0.50f),
        ("art_nightstar", 0.70f),
        ("art_compass",   0.88f),
    };

    // ── Factory methods ──────────────────────────────────────────────────

    public WeaponItem CreateWeapon(string id, float condition = 0.85f)
    {
        _registry.TryGet(id, out var def);
        int mag = def != null && def.MagSize > 0 ? def.MagSize : 30;
        return new WeaponItem
        {
            Id          = id,
            DisplayName = def?.Name ?? id,
            BaseValue   = def?.BaseValue ?? 0f,
            Condition   = condition,
            Damage      = def != null && def.Damage   > 0 ? def.Damage   : 25f,
            Accuracy    = def != null && def.Accuracy > 0 ? def.Accuracy : 0.7f,
            FireRate    = def != null && def.FireRate  > 0 ? def.FireRate  : 5f,
            MagSize     = mag,
            CurrentMag  = mag
        };
    }

    public ArmorItem CreateArmor(string id, string? factionPatch, float condition = 0.9f)
    {
        _registry.TryGet(id, out var def);
        string? gammaId = def?.GammaId ?? DefaultGammaIds.GetValueOrDefault(id);
        var protection = ResolveProtection(id, gammaId, "Armor");

        if (string.IsNullOrEmpty(factionPatch))
        {
            if (def?.FactionPatch != null)
                factionPatch = def.FactionPatch;
            else if (ArmorFallback.TryGetValue(id, out var fb))
                factionPatch = fb.patch;
        }

        return new ArmorItem
        {
            Id            = id,
            DisplayName   = def?.Name ?? id,
            BaseValue     = def?.BaseValue ?? 0f,
            Condition     = condition,
            GammaId       = gammaId,
            Protection    = protection,
            FactionPatchId= factionPatch
        };
    }

    public HelmetItem CreateHelmet(string id, float condition = 0.9f)
    {
        _registry.TryGet(id, out var def);
        string? gammaId = def?.GammaId ?? DefaultGammaIds.GetValueOrDefault(id);
        return new HelmetItem
        {
            Id          = id,
            DisplayName = def?.Name ?? id,
            BaseValue   = def?.BaseValue ?? 0f,
            Condition   = condition,
            GammaId     = gammaId,
            Protection  = ResolveProtection(id, gammaId, "Helmet")
        };
    }

    public static DetectorItem? CreateDetector(string id)
    {
        var tier = id switch
        {
            "det_echo"  => DetectorTier.Echo,
            "det_bear"  => DetectorTier.Bear,
            "det_veles" => DetectorTier.Veles,
            "det_sva"   => DetectorTier.SVA,
            _           => DetectorTier.Echo
        };
        return new DetectorItem { Id = id, Tier = tier };
    }

    /// <summary>Roll a canonical artifact id from a 0–1 rarity score (north = rarer).</summary>
    public static string PickArtifactId(float rarityScore)
    {
        string picked = ArtifactTiers[0].id;
        foreach (var (id, min) in ArtifactTiers)
        {
            if (rarityScore >= min) picked = id;
        }
        return picked;
    }

    public ProtectionStats ResolveGammaProtection(string itemId, string category)
    {
        _registry.TryGet(itemId, out var def);
        string? gammaId = def?.GammaId ?? DefaultGammaIds.GetValueOrDefault(itemId);
        return ResolveProtection(itemId, gammaId, category);
    }

    // ── Internal helpers ─────────────────────────────────────────────────

    private ProtectionStats ResolveProtection(string itemId, string? gammaId, string category)
    {
        GammaProtectionLoader.EnsureLoaded();
        if (!string.IsNullOrEmpty(gammaId))
        {
            var stats = GammaProtectionLoader.Resolve(gammaId, category);
            if (stats.Bullet > 0f || stats.Slash > 0f || stats.Rad > 0f || stats.AnomalyComposite > 0f)
                return stats;
        }

        if (ArmorFallback.TryGetValue(itemId, out var fb))
        {
            float a = fb.anomaly;
            return new ProtectionStats(
                Bullet: fb.bullet, Slash: fb.slash, Rad: fb.rad,
                Burn: a * 0.8f, Shock: a * 0.7f, Chemical: a * 0.85f,
                Psi: a * 0.6f, Strike: a * 0.55f, Explosion: a * 0.5f);
        }

        if (_registry.TryGet(itemId, out var def) && def.RadResist > 0f)
            return new ProtectionStats(Rad: def.RadResist);

        return ProtectionStats.Zero;
    }
}
