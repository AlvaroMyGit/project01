using StalkerALifeSandbox.Entities.Characters;

namespace StalkerALifeSandbox.AI.GOAP;

/// <summary>
/// Per-execution scratch space for whichever GOAP action a stalker is currently
/// running.
///
/// This exists because GOAP actions are registered as SINGLE SHARED INSTANCES
/// across every planning stalker (see <c>StalkerGoapService.RegisterActions</c>).
/// Any mutable field on an action is therefore global, not per-stalker: one
/// stalker's <c>Enter</c> silently overwrites what another stalker's
/// <c>Execute</c> is reading. That bug broke the mission loop outright — 256
/// missions accepted against a single "arrived" — because
/// <c>GoapTravelAction._pathSet</c> was shared. See
/// <c>GoapSharedActionStateTests</c>.
///
/// One flat set of slots is enough rather than storage per action: a stalker
/// executes exactly one action at a time (<c>GoapRuntime.CurrentAction</c>),
/// with <c>Enter</c> called once before it and <c>Exit</c> once after. The
/// service calls <see cref="Reset"/> immediately before every <c>Enter</c>, so
/// no action can inherit a previous action's leftovers.
/// </summary>
public sealed class GoapActionState
{
    /// <summary>Countdown for timed actions (drink, guitar, craft, rest).</summary>
    public float Timer;

    /// <summary>Fatigue recovered per second by the current rest action.</summary>
    public float RestValue;

    /// <summary>A path was successfully set by <c>GoapTravelAction.Enter</c>.</summary>
    public bool PathSet;

    /// <summary>The action's one-shot work is done.</summary>
    public bool Finished;

    /// <summary>Mission work is under way (arrived and started the timer).</summary>
    public bool Working;

    /// <summary>The mission contract has been taken.</summary>
    public bool Accepted;

    /// <summary>Arrival has been written to the debug log once.</summary>
    public bool LoggedArrival;

    /// <summary>Corpse this stalker is investigating.</summary>
    public Corpse? TargetCorpse;

    public void Reset()
    {
        Timer = 0f;
        RestValue = 0f;
        PathSet = false;
        Finished = false;
        Working = false;
        Accepted = false;
        LoggedArrival = false;
        TargetCorpse = null;
    }
}
