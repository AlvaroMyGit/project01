// Mutant.cs — Mutant creature entity
using System.Numerics;
using StalkerALifeSandbox.AI.Blackboards;

namespace StalkerALifeSandbox.Entities.Mutants;

/// <summary>
/// Represents a Zone mutant (Bloodsucker, Boar, Chimera, etc.).
/// Uses a simplified blackboard for perception and movement.
/// </summary>
public sealed class Mutant
{
    public string PersonalityTrait { get; }
    public string   Id          { get; }
    public string   Species     { get; }
    public DietType Diet        { get; }
    public bool     IsAlive     { get; set; } = true;
    public Vector3  Position    { get; set; }

    public float Health   { get; set; } = 100f;
    public float MaxHealth { get; set; } = 100f;
    public float Damage   { get; set; } = 25f;
    public float Speed    { get; set; } = 4f;
    public MutantDamageKind DamageKind { get; set; } = MutantDamageKind.Slash;

    // Spec A: Mutant Hunger Decay & Feeding Restoration Math
    public float Hunger { get; private set; }
    public float HungerDrainRate { get; set; } = 16.6f / 3600f; // ~16.6 per game hour → hunts in ~4h
    public bool IsHuntingPhase => Hunger > 60f; // Actively hunts above 60

    public NPCBlackboard Blackboard { get; }

    public Mutant(string id, string species, DietType diet)
    {
        Id        = id;
        Species   = species;
        Diet      = diet;
        Blackboard = new NPCBlackboard(id);
        PersonalityTrait = PickRandomPersonality(species);
    }

    private static string PickRandomPersonality(string species)
    {
        string[] possible = species switch {
            "Bloodsucker" => new[] { "Territorial", "Hunter", "Wanderer" },
            "Boar" => new[] { "Roamer", "Opportunist", "Aggressive" },
            "Chimera" => new[] { "Solitary", "Ambush", "Restless" },
            "Flesh" => new[] { "Cowardly", "Scavenger", "Slow" },
            _ => new[] { "Neutral", "Aggressive", "Cowardly" }
        };
        return possible[Random.Shared.Next(possible.Length)];
    }

    public void TakeDamage(float amount)
    {
        Health -= amount;
        if (Health <= 0f) { Health = 0f; IsAlive = false; }
    }

    public void FeedOnCorpse()
    {
        Hunger = Math.Max(0f, Hunger - 50f);
        Health = Math.Min(MaxHealth, Health + (MaxHealth * 0.25f));
    }

    public void Tick(float deltaGameSec)
    {
        if (!IsAlive) return;
        Hunger = Math.Min(100f, Hunger + HungerDrainRate * deltaGameSec);
        Blackboard.CurrentPosition = Position;
    }

    public override string ToString() =>
        $"[Mutant:{Id}] {Species} ({Diet}) HP={Health:F0} Hng={Hunger:F1} Alive={IsAlive}";
}
