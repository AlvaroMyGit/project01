// ActionInvestigateCorpse.cs — Body discovery & PDA patch reporting
using System.Numerics;
using StalkerALifeSandbox.Core;
using StalkerALifeSandbox.Entities.Characters;
using StalkerALifeSandbox.PDA;
using StalkerALifeSandbox.Systems;

namespace StalkerALifeSandbox.AI.Actions;

/// <summary>
/// GOAP action: NPC approaches an unreported corpse, investigates it,
/// then broadcasts a PDA death report based on whether the patch is intact.
/// Spec F: If patch intact → name/faction/cause. If not → unidentified notice.
/// </summary>
public sealed class ActionInvestigateCorpse
{
    public float InvestigateDurationSec { get; set; } = 5f;
    private float _timer;

    public Dictionary<string, bool> GetPreconditions() => new()
    {
        ["HasUnreportedCorpseNearby"] = true
    };

    public Dictionary<string, bool> GetEffects() => new()
    {
        ["HasUnreportedCorpseNearby"] = false
    };

    /// <summary>
    /// Tick the investigation. Returns true when the report is filed.
    /// </summary>
    public bool Tick(
        Stalker investigator,
        Corpse corpse,
        float gameTime,
        float deltaSec)
    {
        if (corpse.IsReported) return true;

        _timer += deltaSec;
        if (_timer < InvestigateDurationSec) return false;

        _timer = 0f;
        var looted = EquipmentUpgradeService.TryLootCorpse(investigator, corpse, gameTime, "investigate");
        if (looted.Count > 0)
            investigator.Activity = $"🎒 Stripped {string.Join(", ", looted)}";
        CorpseCleanupService.MarkInteraction(corpse, gameTime);
        FilePDAReport(investigator.Id, corpse);
        corpse.IsReported = true;
        return true;
    }

    private static void FilePDAReport(string investigatorId, Corpse corpse)
    {
        PDAMessage msg;

        if (corpse.IsPatchIntact)
        {
            // Full identification
            msg = new PDAMessage
            {
                MessageId   = $"death_{corpse.CorpseId}",
                MessageType = PDAMessageType.DeathReport,
                Headline    = $"{corpse.VictimName} ({corpse.VictimFaction}) found dead.",
                Body        = $"Cause: {corpse.CauseOfDeath}. Location reported by {investigatorId}.",
                IsUrgent    = false
            };
        }
        else
        {
            // Patch destroyed — unidentified notice
            msg = new PDAMessage
            {
                MessageId   = $"death_unknown_{corpse.CorpseId}",
                MessageType = PDAMessageType.DeathReport,
                Headline    = "Unidentified body found in the Zone.",
                Body        = $"No patch recovered. Reported by {investigatorId}.",
                IsUrgent    = false
            };
        }

        EventBus.Publish(new DeathLogEvent
        {
            VictimName = corpse.IsPatchIntact ? corpse.VictimName : "Unknown Stalker",
            KillerName = corpse.CauseOfDeath.ToString(),
            FactionId  = corpse.VictimFaction
        });
    }

    public void Reset() => _timer = 0f;
}
