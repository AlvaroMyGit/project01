using StalkerALifeSandbox.AI.GOAP;
using StalkerALifeSandbox.AI.GOAP.Actions;

namespace StalkerALifeSandbox.Tests;

/// <summary>
/// Preconditions and effects are cached because the planner asks every
/// registered action at every node it expands — up to 500 iterations x 19
/// actions per plan, roughly 15 plans a tick. Every implementation returned a
/// freshly allocated dictionary, which made A* planning 24% of the entire tick
/// budget.
///
/// Caching is only safe while those sets stay constant, so that is what these
/// pin.
/// </summary>
public class GoapActionCachingTests
{
    private static IEnumerable<GOAPAction> AllActions() => new GOAPAction[]
    {
        new ActionGoToShelter(), new ActionGoHome(), new ActionPatrolWilds(),
        new ActionVisitStash(), new ActionRestAtPOI(), new ActionTradeRun(),
        new ActionHarvestArtifact(), new ActionExploreLab(), new ActionRestAtBase(),
        new ActionShareDrink(), new ActionPlayGuitar(), new ActionCraftUpgrade(),
        new ActionInvestigateCorpseGoap(), new ActionCookMutantMeatGoap(),
        new ActionGoToMissionGiver(), new ActionAcceptMission(),
        new ActionFulfillMission(), new ActionReturnToMissionIssuer(),
        new ActionTurnInMission()
    };

    [Fact]
    public void TheCachedSetsMatchWhatTheActionDeclares()
    {
        foreach (var a in AllActions())
        {
            Assert.Equal(a.GetPreconditions(), a.Preconditions);
            Assert.Equal(a.GetEffects(), a.Effects);
        }
    }

    [Fact]
    public void TheSameInstanceIsReturnedEveryTime()
    {
        // The whole point: no allocation per planner probe.
        foreach (var a in AllActions())
        {
            Assert.Same(a.Preconditions, a.Preconditions);
            Assert.Same(a.Effects, a.Effects);
        }
    }

    [Fact]
    public void DeclaredSetsAreStable_SoCachingCannotGoStale()
    {
        // If an action ever starts varying its preconditions with state, the
        // cache silently freezes the first answer. Two independent calls must
        // agree, or caching is invalid for that action.
        foreach (var a in AllActions())
        {
            Assert.Equal(a.GetPreconditions(), a.GetPreconditions());
            Assert.Equal(a.GetEffects(), a.GetEffects());
        }
    }

    [Fact]
    public void EveryActionDeclaresAtLeastOneEffect()
    {
        // An action with no effects can never satisfy a goal, so the planner
        // would carry it through every expansion for nothing.
        foreach (var a in AllActions())
            Assert.True(a.Effects.Count > 0, $"{a.Name} declares no effects");
    }
}
