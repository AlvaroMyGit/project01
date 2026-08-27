// ActionMutantFeedOnCorpse.cs — Mutant corpse feeding behavior
using System.Numerics;
using StalkerALifeSandbox.Core;
using StalkerALifeSandbox.Entities.Mutants;

namespace StalkerALifeSandbox.AI.Actions;

/// <summary>Event fired when a mutant begins feeding on a corpse.</summary>
public readonly struct MutantFeedingEvent
{
    public string MutantId   { get; init; }
    public string CorpseId   { get; init; }
    public Vector3 Location  { get; init; }
}

/// <summary>
/// GOAP action: Mutant moves to a fresh corpse and feeds,
/// setting IsEaten = true. Carnivores and Scavengers only.
/// </summary>
public sealed class ActionMutantFeedOnCorpse
{
    /// <summary>Feed duration in seconds.</summary>
    public float FeedDurationSec { get; set; } = 12f;
    private float _feedTimer;

    public bool CanExecute(MutantSpec spec, DietType diet)
    {
        return diet == DietType.Carnivore || diet == DietType.Scavenger;
    }

    /// <summary>
    /// Tick the feeding action. Returns true when complete.
    /// Publishes a MutantFeedingEvent when feeding starts.
    /// </summary>
    public bool Tick(
        string mutantId,
        string corpseId,
        ref bool isCorpseEaten,
        Vector3 location,
        float deltaSec,
        bool justStarted)
    {
        if (isCorpseEaten) return true; // already fed

        if (justStarted)
        {
            EventBus.Publish(new MutantFeedingEvent
            {
                MutantId = mutantId,
                CorpseId = corpseId,
                Location = location
            });
        }

        _feedTimer += deltaSec;
        if (_feedTimer >= FeedDurationSec)
        {
            isCorpseEaten = true;
            _feedTimer = 0f;
            return true;
        }
        return false;
    }

    public void Reset() => _feedTimer = 0f;
}
