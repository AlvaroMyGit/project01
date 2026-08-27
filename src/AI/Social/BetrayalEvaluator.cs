// BetrayalEvaluator.cs — Desperation & treason contract solver
using StalkerALifeSandbox.AI.Blackboards;
using StalkerALifeSandbox.Core;
using StalkerALifeSandbox.Entities.Characters;
using StalkerALifeSandbox.Entities.Needs;
using StalkerALifeSandbox.PDA;
using System.Numerics;

namespace StalkerALifeSandbox.AI.Social;

/// <summary>Event fired when a traitor is caught in the act.</summary>
public readonly struct TreasonCaughtEvent
{
    public string TraitorId     { get; init; }
    public string VictimId      { get; init; }
    public string TraitorFaction{ get; init; }
}

/// <summary>
/// Evaluates desperation logic and treason choices per Spec C.
/// </summary>
public sealed class BetrayalEvaluator
{
    /// <summary>
    /// Checks if the Stalker is desperate based on survival needs.
    /// Triggered when Hunger > 85%, Gold < 200 RU, or Ammo < 10 rnds.
    /// </summary>
    public bool IsDesperate(SurvivalNeeds needs)
    {
        return needs.Hunger > 85f || 
               needs.GoldAmount < 200 || 
               needs.AmmoCount < 10;
    }

    /// <summary>
    /// Evaluates if an NPC will accept a shady hit contract to kill an ally.
    /// Treason Logic: If desperate and Trustworthiness < 50, they accept.
    /// </summary>
    public bool WillAcceptShadyContract(SurvivalNeeds needs, StalkerAttributes attributes)
    {
        if (IsDesperate(needs) && attributes.Trustworthiness < 50)
        {
            return true;
        }
        return false;
    }

    /// <summary>
    /// Executes the witness check when the traitor performs the hit.
    /// Spec C: If seen by an observer, Trustworthiness drops by -50,
    /// a treason warning broadcasts on the PDA, and the traitor's 
    /// faction standing flips to Hostile (which will be handled by FactionMatrix).
    /// </summary>
    public void ExecuteWitnessCheck(
        string traitorId,
        string traitorFaction,
        string victimId,
        StalkerAttributes traitorAttributes,
        IEnumerable<(string Id, Vector3 Pos, bool IsLookingAtTarget)> observers)
    {
        bool wasWitnessed = false;

        foreach (var observer in observers)
        {
            if (observer.Id == traitorId || observer.Id == victimId) continue;

            if (observer.IsLookingAtTarget)
            {
                wasWitnessed = true;
                break;
            }
        }

        if (wasWitnessed)
        {
            // Drop trust
            traitorAttributes.AddTrustworthiness(-50);

            // Broadcast PDA Warning
            EventBus.Publish(new TreasonCaughtEvent
            {
                TraitorId = traitorId,
                VictimId = victimId,
                TraitorFaction = traitorFaction
            });

            // Note: FactionMatrix listener or similar should catch TreasonCaughtEvent 
            // and flip the standing of the Traitor specifically to Hostile.
        }
    }
}
