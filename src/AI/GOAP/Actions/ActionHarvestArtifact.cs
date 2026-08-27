using System.Numerics;
using StalkerALifeSandbox.AI.Blackboards;
using StalkerALifeSandbox.AI.Decision;
using StalkerALifeSandbox.Economy;
using StalkerALifeSandbox.Entities.Characters;
using StalkerALifeSandbox.Entities.Equipment;
using StalkerALifeSandbox.Systems;
using StalkerALifeSandbox.World.Generation;
using StalkerALifeSandbox.World.Hazards;

namespace StalkerALifeSandbox.AI.GOAP.Actions;

public sealed class ActionHarvestArtifact : GoapTravelAction
{
    private static readonly ArtifactDecisionEngine ArtifactEngine = new();

    public override string Name => "HarvestArtifact";
    public override float BaseCost => 6f;

    protected override string ActivityLabel => "🔍 Artifact Hunt";
    protected override NavigationTargetType NavType => NavigationTargetType.PointOfInterest;

    public override Dictionary<string, bool> GetPreconditions() => new()
    {
        [GoapKeys.EmissionImminent] = false
    };

    public override Dictionary<string, bool> GetEffects() => new()
    {
        [GoapKeys.HasCompletedPatrol] = true,
        [GoapKeys.HasArtifact] = true
    };

    protected override Vector3? ResolveTarget(Stalker stalker)
    {
        float maxThreat = ZoneGateEvaluator.EffectiveComfort(stalker, stalker.Needs);
        var dens = Ctx!.Stamper.Stamps
            .Where(p => p.Type == POIType.MutantDen)
            .Where(p => p.ThreatLevel <= maxThreat + 0.08f)
            .ToList();
        if (dens.Count == 0) return null;
        var dest = dens[Random.Shared.Next(dens.Count)];
        return dest.Position + new Vector3(
            (float)(Random.Shared.NextDouble() - 0.5) * 80f, 0,
            (float)(Random.Shared.NextDouble() - 0.5) * 80f);
    }

    protected override string? DestinationLabel(Stalker stalker, Vector3 target)
    {
        var den = Ctx!.Stamper.Stamps
            .Where(p => p.Type == POIType.MutantDen)
            .OrderBy(p => Vector3.Distance(p.Position, target))
            .FirstOrDefault();
        return den != null ? $"Anomaly Field near {den.Name}" : "Anomaly Field";
    }

    public override void Exit(NPCBlackboard bb)
    {
        var stalker = Ctx?.GetStalker(bb.OwnerId);
        if (stalker != null)
        {
            SkillEvaluator.RecordZoneSurvivalEvent(stalker, "artifact_found");
            ResolveArtifactLoot(stalker);
        }
        GoapWorldStateSync.ApplyEffects(bb, GetEffects());
    }

    private void ResolveArtifactLoot(Stalker stalker)
    {
        if (Ctx == null) return;

        ItemDatabase.EnsureLoaded();

        float nx = stalker.Position.X / Ctx.WorldGen.Width;
        float ny = stalker.Position.Z / Ctx.WorldGen.Height;
        float latitude = Ctx.WorldGen.GetThreatLevel(nx, ny);
        float noise = (Random.Shared.NextSingle() * 0.4f) - 0.2f;
        float rarity = Math.Clamp(latitude * 0.65f + noise + 0.05f, 0f, 1f);

        string artId = ItemDatabase.PickArtifactId(rarity);
        var artifact = new ArtifactData(artId, rarity);
        var decision = ArtifactEngine.Decide(artifact, stalker.Needs, stalker.Belt.HasFreeSlot);

        switch (decision)
        {
            case ArtifactDecision.EquipInBelt:
                stalker.Belt.EquipArtifact(artifact);
                break;
            case ArtifactDecision.SellToTrader:
                if (Ctx!.Traders.FindNearest(stalker.Position, 200f) is { } site &&
                    TradeService.TrySellArtifact(stalker, site.Trader, artId, rarity))
                {
                    break;
                }
                stalker.Needs.GoldAmount += ItemDatabase.GetBaseValue(artId) * 0.55f;
                break;
            case ArtifactDecision.Stash:
                stalker.Equipment.AddItem(artId, 0.4f);
                break;
        }
    }
}
