// ArtifactDecisionEngine.cs — Keep & Equip vs. Sell to Trader solver
using StalkerALifeSandbox.Entities.Needs;
using StalkerALifeSandbox.World.Hazards;

namespace StalkerALifeSandbox.AI.Decision;

/// <summary>Decision outcome for an artifact.</summary>
public enum ArtifactDecision { EquipInBelt, SellToTrader, Stash }

/// <summary>
/// Evaluates whether a Stalker should equip a found artifact in their
/// belt slot or sell it to the nearest trader for gold.
/// </summary>
public sealed class ArtifactDecisionEngine
{
    // Rarity threshold above which the NPC prefers to keep the artifact
    public float EquipRarityThreshold  { get; set; } = 0.65f;
    
    // Gold threshold below which a stalker is cash-hungry (sell bias)
    public float GoldDesperationMark   { get; set; } = 500f;

    /// <summary>
    /// Solve the Keep vs. Sell decision.
    /// High rarity = equip. Low gold = sell. Otherwise stash.
    /// </summary>
    public ArtifactDecision Decide(ArtifactData artifact, SurvivalNeeds needs, bool hasFreeSlot)
    {
        // Desperate for money → always sell unless artifact is extremely rare
        if (needs.GoldAmount < GoldDesperationMark && artifact.RarityScore < 0.85f)
            return ArtifactDecision.SellToTrader;

        // High-rarity artifact + free belt slot → equip
        if (artifact.RarityScore >= EquipRarityThreshold && hasFreeSlot)
            return ArtifactDecision.EquipInBelt;

        // Low-rarity and not desperate → stash for later
        return ArtifactDecision.Stash;
    }
}
