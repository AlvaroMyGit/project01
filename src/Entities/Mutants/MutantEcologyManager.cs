using System.Text.Json;
using System.Text.Json.Serialization;
using StalkerALifeSandbox.World.Environment;

namespace StalkerALifeSandbox.Entities.Mutants;

/// <summary>
/// Manages the roster of the 17 G.A.M.M.A. species and dictates spawn weights
/// by latitude/threat plus diurnal activity.
/// </summary>
public sealed class MutantEcologyManager
{
    private readonly Dictionary<MutantSpecies, MutantSpec> _specs = new();

    public IReadOnlyDictionary<MutantSpecies, MutantSpec> Specs => _specs;

    public MutantEcologyManager()
    {
        InitializeSpecs();
    }

    private void InitializeSpecs()
    {
        string path = Path.Combine("data", "mutants.json");
        if (File.Exists(path))
        {
            var options = new JsonSerializerOptions { Converters = { new JsonStringEnumConverter() } };
            string json = File.ReadAllText(path);
            var specs = JsonSerializer.Deserialize<List<MutantSpec>>(json, options);
            if (specs != null)
            {
                foreach (var spec in specs)
                    _specs[spec.Species] = spec;
            }
        }
    }

    public MutantSpec GetSpec(MutantSpecies species) =>
        _specs.TryGetValue(species, out var spec)
            ? spec
            : new MutantSpec { Species = species, Name = species.ToString() };

    /// <summary>Resolve HP, damage, and speed — uses JSON overrides when present, else species defaults.</summary>
    public (float Health, float Damage, float Speed) GetCombatStats(MutantSpecies species)
    {
        var spec = GetSpec(species);
        var defaults = DefaultCombatStats(species);
        return (
            spec.BaseHealth > 0 ? spec.BaseHealth : defaults.Health,
            spec.DamageBase > 0 ? spec.DamageBase : defaults.Damage,
            spec.MovementSpeed > 0 ? spec.MovementSpeed : defaults.Speed);
    }

    private static (float Health, float Damage, float Speed) DefaultCombatStats(MutantSpecies species) =>
        species switch
        {
            MutantSpecies.Tushkano => (40f, 8f, 5f),
            MutantSpecies.Rat => (35f, 10f, 5f),
            MutantSpecies.Flesh => (80f, 15f, 3f),
            MutantSpecies.Boar => (120f, 22f, 4f),
            MutantSpecies.Dog => (70f, 18f, 5f),
            MutantSpecies.BlindDog => (65f, 16f, 5f),
            MutantSpecies.Pseudodog => (90f, 24f, 5.5f),
            MutantSpecies.Snork => (100f, 28f, 6f),
            MutantSpecies.Bloodsucker => (110f, 32f, 4.5f),
            MutantSpecies.PsyDog => (95f, 26f, 5f),
            MutantSpecies.Controller => (130f, 35f, 3f),
            MutantSpecies.Burer => (100f, 30f, 3.5f),
            MutantSpecies.Poltergeist => (90f, 28f, 4f),
            MutantSpecies.Fracture => (85f, 22f, 5f),
            MutantSpecies.Lurker => (75f, 20f, 5f),
            MutantSpecies.Chimera => (200f, 55f, 7f),
            MutantSpecies.Pseudogiant => (350f, 70f, 2.5f),
            _ => (100f, 25f, 4f)
        };

    public static MutantDamageKind GetDamageKind(MutantSpecies species) =>
        species switch
        {
            MutantSpecies.Bloodsucker or MutantSpecies.Chimera or MutantSpecies.Snork
                or MutantSpecies.Fracture or MutantSpecies.Lurker or MutantSpecies.Pseudogiant
                => MutantDamageKind.Slash,
            MutantSpecies.Controller or MutantSpecies.PsyDog or MutantSpecies.Poltergeist
                => MutantDamageKind.Psi,
            MutantSpecies.Burer => MutantDamageKind.Impact,
            MutantSpecies.Boar => MutantDamageKind.Impact,
            _ => MutantDamageKind.Bite
        };

    /// <summary>
    /// Roll a species weighted by regional threat (0=south safe → 1=north CNPP).
    /// Anomaly Gamma: rodents & boars in the south; bloodsuckers mid-north; chimeras at the center.
    /// </summary>
    public MutantSpecies RollSpecies(float threatLevel)
    {
        var pool = BuildSpawnPool(threatLevel);
        float total = pool.Sum(p => p.Weight);
        float roll = Random.Shared.NextSingle() * total;
        float cumulative = 0f;
        foreach (var (species, weight) in pool)
        {
            cumulative += weight;
            if (roll <= cumulative)
                return species;
        }
        return pool[^1].Species;
    }

    public bool ShouldSleepInDen(MutantSpecies species, EnvironmentManager env, WeatherManager weather)
    {
        if (!_specs.TryGetValue(species, out var spec))
            return false;

        if (spec.IsNocturnal)
        {
            if (!env.IsNight && weather.CurrentWeather != WeatherType.Storm &&
                weather.CurrentWeather != WeatherType.Fog)
                return true;
        }
        else if (env.IsNight && weather.CurrentWeather != WeatherType.Storm)
        {
            return true;
        }

        return false;
    }

    private static List<(MutantSpecies Species, float Weight)> BuildSpawnPool(float threat)
    {
        var pool = new List<(MutantSpecies, float)>();

        void Add(MutantSpecies s, float w) => pool.Add((s, w));

        // South / Cordon band
        if (threat < 0.28f)
        {
            Add(MutantSpecies.Tushkano, 22f);
            Add(MutantSpecies.Rat, 18f);
            Add(MutantSpecies.Dog, 14f);
            Add(MutantSpecies.BlindDog, 12f);
            Add(MutantSpecies.Flesh, 16f);
            Add(MutantSpecies.Boar, 18f);
        }
        // Mid-Zone
        else if (threat < 0.55f)
        {
            Add(MutantSpecies.Boar, 14f);
            Add(MutantSpecies.Flesh, 12f);
            Add(MutantSpecies.BlindDog, 14f);
            Add(MutantSpecies.Pseudodog, 12f);
            Add(MutantSpecies.Snork, 16f);
            Add(MutantSpecies.Bloodsucker, 8f);
            Add(MutantSpecies.Fracture, 8f);
            Add(MutantSpecies.Lurker, 6f);
        }
        // Deep Wild
        else if (threat < 0.82f)
        {
            Add(MutantSpecies.Snork, 12f);
            Add(MutantSpecies.Bloodsucker, 18f);
            Add(MutantSpecies.PsyDog, 10f);
            Add(MutantSpecies.Burer, 10f);
            Add(MutantSpecies.Controller, 6f);
            Add(MutantSpecies.Poltergeist, 8f);
            Add(MutantSpecies.Fracture, 8f);
            Add(MutantSpecies.Lurker, 10f);
            Add(MutantSpecies.Pseudodog, 6f);
        }
        // North / CNPP approaches
        else
        {
            Add(MutantSpecies.Bloodsucker, 10f);
            Add(MutantSpecies.Controller, 12f);
            Add(MutantSpecies.Burer, 10f);
            Add(MutantSpecies.Poltergeist, 12f);
            Add(MutantSpecies.Chimera, 14f);
            Add(MutantSpecies.Pseudogiant, 8f);
            Add(MutantSpecies.Snork, 6f);
            Add(MutantSpecies.PsyDog, 8f);
        }

        return pool;
    }
}
