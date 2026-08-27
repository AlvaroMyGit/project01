// MutantSpec.cs — Stats, abilities (cloaking, psy-aura), trophies
namespace StalkerALifeSandbox.Entities.Mutants;

public enum MutantSpecies
{
    Tushkano,
    Rat,
    Flesh,
    Boar,
    Dog,
    BlindDog,
    Pseudodog,
    Snork,
    Bloodsucker,
    PsyDog,
    Controller,
    Burer,
    Poltergeist,
    Fracture,
    Lurker,
    Chimera,
    Pseudogiant
}

/// <summary>
/// Defines the innate capabilities and stats of a mutant species.
/// </summary>
public sealed class MutantSpec
{
    public MutantSpecies Species { get; init; }
    public string Name { get; init; } = "";
    public int BaseHealth { get; init; }
    public float MovementSpeed { get; init; }
    public float DamageBase { get; init; }
    
    // Abilities
    public bool CanCloak { get; init; }
    public bool HasPsyAura { get; init; }
    public bool IsNocturnal { get; init; }
    public bool IsSubterranean { get; init; }
    
    // Trophies
    public string TrophyId { get; init; } = "";
    public float TrophyDropChance { get; init; }
}
