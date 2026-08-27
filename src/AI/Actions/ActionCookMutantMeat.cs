// ActionCookMutantMeat.cs — GOAP action for campfire cooking
using StalkerALifeSandbox.Crafting;
using StalkerALifeSandbox.Entities.Needs;

namespace StalkerALifeSandbox.AI.Actions;

/// <summary>
/// GOAP action: NPC sits at campfire, cooks mutant meat, and eats it.
/// Requires: IsAtCampfire = true, HasRawMeat = true.
/// Effects: IsHungry = false, HasRawMeat = false.
/// </summary>
public sealed class ActionCookMutantMeat
{
    private readonly MutantCookingSystem _cooking = new();

    public float CookTimeSec { get; set; } = 20f;
    private float _timer;

    public Dictionary<string, bool> GetPreconditions() => new()
    {
        ["IsAtCampfire"] = true,
        ["HasRawMeat"]   = true
    };

    public Dictionary<string, bool> GetEffects() => new()
    {
        ["IsHungry"]   = false,
        ["HasRawMeat"] = false
    };

    public bool IsValid(bool isAtCampfire, bool hasRawMeat) =>
        isAtCampfire && hasRawMeat;

    /// <summary>
    /// Tick the cooking action. Returns true when done.
    /// </summary>
    public bool Tick(
        MutantMeatType meatType,
        SurvivalNeeds needs,
        int vodkaCount,
        float deltaSec,
        out int vodkaConsumed)
    {
        vodkaConsumed = 0;
        _timer += deltaSec;

        if (_timer >= CookTimeSec)
        {
            _timer = 0f;
            var meal = _cooking.Cook(meatType);
            _cooking.Eat(meal, needs, vodkaCount, out vodkaConsumed);
            return true;
        }
        return false;
    }

    public void Reset() => _timer = 0f;
}
