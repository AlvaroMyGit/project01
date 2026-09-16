using StalkerALifeSandbox.AI;
using StalkerALifeSandbox.AI.Blackboards;
using StalkerALifeSandbox.AI.Decision;
using StalkerALifeSandbox.Economy;
using StalkerALifeSandbox.Entities.Needs;

namespace StalkerALifeSandbox.AI.GOAP.Goals;

/// <summary>
/// Visit a macro-base trader to restock supplies or buy gear upgrades.
///
/// This goal is the winning decision for roughly 80% of everything the sim
/// decides, and <see cref="GoalPatrol"/> — "roam when nothing urgent is
/// happening" — is selected close to never. Two attempts were made to fix
/// that; both are worth keeping on record, because the second attempt would
/// not have found the real shape of the problem without the first having
/// failed instructively.
///
/// <b>Attempt 1.</b> The goal had a "solvent but otherwise fine" fallback,
/// <c>Rubles &gt;= 450 =&gt; 36</c>, sitting above GoalPatrol's flat 25. It
/// looked exactly like the kind of oversight this codebase has found before
/// (a number picked in isolation, never checked against a sibling goal).
/// Removed it; measured over five runs against the stored baseline. The goal
/// mix did not move — VisitTrader still ~80%, Patrol still ~0%. The branch was
/// real and was not the problem, or at least not a large enough share of it to
/// detect. The removal is kept — it costs nothing and the branch had no
/// justification either way — but it did not fix what this doc is about.
///
/// <b>Attempt 2 (tried, measured, reverted).</b> Rather than guess a third time, this goal
/// was instrumented (see <see cref="Tag"/>) to record which branch actually
/// fires. The wealth tiers below — <c>Rubles &gt;= 700/1000/1500</c> — turned
/// out to account for 92% of every nonzero evaluation, overwhelmingly the
/// middle one. A same-run population snapshot explained the number: at steady
/// state (422 alive) the population averaged 963 RU, with 93.6% holding
/// &gt;= 700 and 45.5% holding &gt;= 1000. The ladder was not detecting rich
/// outliers, it was describing the population.
///
/// The obvious next move — raise the thresholds against that measured
/// distribution — was tried (1200/1800/2800) and made things worse in a new
/// way. Population average climbed from ~963 to ~1463 over the SAME run and
/// had not plateaued when the run ended, while the goal mix barely moved
/// (80.5% -&gt; 78.8%, inside single-run noise). The reason: this is a
/// self-stabilizing feedback loop, not a fixed distribution to calibrate
/// against once. Crossing a wealth tier is what triggers the trader visit that
/// spends the money back down, so the population's wealth naturally settles
/// near whatever the threshold is — a thermostat, not a fixed target. Raising
/// the threshold does not lower the hit rate; it relocates the equilibrium
/// upward, at the cost of chasing rubles that no longer get spent once gear
/// demand saturates population-wide (own gear improves monotonically over a
/// long run, so eventually there is nothing left worth buying and visits stop
/// draining anything).
///
/// So the real fix is not a numeric literal in this file. It needs either a
/// genuine ongoing ruble sink independent of the visit trigger, or a
/// wealth-relative rather than absolute-threshold utility. That is economy
/// design, not goal calibration, and is being left for its own pass rather
/// than bolted onto this one. See CODEBASE.md, "A self-stabilizing feedback
/// loop is not a distribution to calibrate against."
/// </summary>
public sealed class GoalVisitTrader : GOAPGoal
{
    public override string Name => "VisitTrader";

    public override bool IsRelevant(NPCBlackboard bb) =>
        !bb.WorldStateBools.GetValueOrDefault(GoapKeys.EmissionImminent);

    public override float EvaluateUtility(NPCBlackboard bb, SurvivalNeeds needs)
    {
        if (!IsRelevant(bb)) return 0f;

        // Men out of ammunition are the leader's problem: a squad with dry
        // rifles is in more trouble than a leader with a full one.
        // Hurt with nothing to treat it is the strongest reason to see a
        // trader: it is the only route back to fighting shape.
        if (!bb.WorldStateBools.GetValueOrDefault(GoapKeys.IsHealthy, true) &&
            !bb.WorldStateBools.GetValueOrDefault(GoapKeys.HasMedkit))
            return Tag("HurtNoMedkit", 58f);

        if (needs.IsOutOfAmmo) return Tag("OutOfAmmo", 48f);
        if (Squads.SquadNeeds.OutOfAmmo(bb) > 0) return Tag("SquadOutOfAmmo", 46f);

        // Scale utility strongly with excess wealth so they prioritize gearing
        // up over new jobs (which max at ~50). Left at the original values —
        // see the class doc — because raising them chases a self-correcting
        // equilibrium rather than fixing the share of decisions this
        // dominates. 92% of every nonzero evaluation of this goal lands in one
        // of these three branches, overwhelmingly Wealth1000.
        if (needs.Rubles >= 1500f) return Tag("Wealth1500", 60f);
        if (needs.Rubles >= 1000f) return Tag("Wealth1000", 52f); // Beats GoalAcceptMission's 50
        if (needs.Rubles >= 700f) return Tag("Wealth700", 42f);

        if (needs.Hunger > 45f || needs.Thirst > 45f) return Tag("HungerOrThirst45", 44f);
        if (Squads.SquadNeeds.WorstHunger(bb) > 45f ||
            Squads.SquadNeeds.WorstThirst(bb) > 45f) return Tag("SquadHungerOrThirst45", 43f);
        if (needs.Radiation > 35f) return Tag("Radiation35", 40f);
        if (needs.AmmoCount < 35) return Tag("AmmoUnder35", 38f);

        // No branch left for "solvent but otherwise fine" on purpose — see
        // Attempt 1 in the class doc. "Visit a trader" is meant to be an
        // errand for an actual need or a real windfall (>=700, already
        // above), not the default idle state, even though removing this one
        // branch alone did not visibly move the mix.
        return 0f;
    }

    /// <summary>
    /// Records which condition produced this goal's score. Not a display
    /// value — a durable diagnostic, the same kind of instrument as
    /// <c>GOAPPlanner</c>'s goal-selection counters, kept for the same reason:
    /// this is the goal that wins ~80% of every decision in the sim, and
    /// reasoning about why from source alone produced two wrong answers in a
    /// row before this told us directly. See <c>SimulationDebugLog</c>'s
    /// "VisitTrader reasons" report line.
    /// </summary>
    private static float Tag(string reason, float score)
    {
        Systems.SimulationDebugLog.RecordVisitTraderReason(reason);
        return score;
    }

    public override Dictionary<string, bool> GetTargetState() => new()
    {
        [GoapKeys.HasVisitedTrader] = true
    };
}
