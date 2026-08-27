// StalkerAttributes.cs — 4-Skill Matrix (Combat, Survival, Charisma, Trust)
namespace StalkerALifeSandbox.Entities.Characters;

/// <summary>
/// Defines the RPG core skills and progression for a Stalker.
/// Values range from 0 to 100.
/// </summary>
public sealed class StalkerAttributes
{
    private float _marksmanship;
    private float _zoneSurvival;
    private float _charisma;
    private float _trustworthiness;

    public int Marksmanship => (int)_marksmanship;
    public int ZoneSurvival => (int)_zoneSurvival;
    public int Charisma => (int)_charisma;
    public int Trustworthiness => (int)_trustworthiness;

    public StalkerAttributes()
    {
        // Default average rookie stats
        _marksmanship = 25f;
        _zoneSurvival = 25f;
        _charisma = 25f;
        _trustworthiness = 50f;
    }

    public void AddMarksmanship(float amount) => _marksmanship = Math.Clamp(_marksmanship + amount, 0f, 100f);
    public void AddZoneSurvival(float amount) => _zoneSurvival = Math.Clamp(_zoneSurvival + amount, 0f, 100f);
    public void AddCharisma(float amount) => _charisma = Math.Clamp(_charisma + amount, 0f, 100f);
    public void AddTrustworthiness(float amount) => _trustworthiness = Math.Clamp(_trustworthiness + amount, 0f, 100f);

    /// <summary>Roll skill baseline for a rank tier (Rookie = 0).</summary>
    public void RollForRank(StalkerRank rank = StalkerRank.Rookie)
    {
        int mult = Math.Clamp((int)rank, 0, RankProgression.MaxTier);
        var rng = Random.Shared;
        _marksmanship = Math.Clamp(rng.Next(10, 40) + mult * 10, 0, 100);
        _zoneSurvival = Math.Clamp(rng.Next(10, 40) + mult * 10, 0, 100);
        _charisma = Math.Clamp(rng.Next(10, 80), 0, 100);
        _trustworthiness = Math.Clamp(rng.Next(10, 90), 0, 100);
    }

    /// <summary>
    /// Create random attributes based on a rank/tier.
    /// </summary>
    public static StalkerAttributes GenerateForRank(int rankMultiplier)
    {
        var attr = new StalkerAttributes();
        attr.RollForRank((StalkerRank)Math.Clamp(rankMultiplier, 0, RankProgression.MaxTier));
        return attr;
    }
}
