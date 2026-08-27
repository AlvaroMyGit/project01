namespace StalkerALifeSandbox.Systems;
using StalkerALifeSandbox.Entities.Characters;
using System;

/// <summary>
/// Organic diminishing-returns RPG skill engine.
/// Formula: Delta Attribute = BaseGain * (1 - (CurrentValue / 100))^1.5
/// </summary>
public static class SkillEvaluator
{
    private static float CalculateGain(float baseGain, int currentValue)
    {
        float ratio = Math.Clamp(currentValue / 100f, 0f, 1f);
        return baseGain * (float)Math.Pow(1f - ratio, 1.5);
    }

    public static void RecordMarksmanshipEvent(Stalker stalker, string eventType)
    {
        float baseGain = eventType switch
        {
            "hit" => 0.2f,
            "long_shot" => 0.5f,
            "kill" => 1.5f,
            _ => 0f
        };
        float gain = CalculateGain(baseGain, stalker.Attributes.Marksmanship);
        stalker.Attributes.AddMarksmanship(gain);
    }

    public static void RecordZoneSurvivalEvent(Stalker stalker, string eventType)
    {
        float baseGain = eventType switch
        {
            "harvest" => 0.8f,
            "cook" => 0.3f,
            "emission_survived" => 5.0f,
            "artifact_found" => 2.5f,
            "lab_explored" => 1.5f,
            _ => 0f
        };
        float gain = CalculateGain(baseGain, stalker.Attributes.ZoneSurvival);
        stalker.Attributes.AddZoneSurvival(gain);
    }

    public static void RecordCharismaEvent(Stalker stalker, string eventType)
    {
        float baseGain = eventType switch
        {
            "campfire_guitar" => 0.4f,
            "trade" => 0.2f,
            "squad_leadership" => 3.0f,
            _ => 0f
        };
        float gain = CalculateGain(baseGain, stalker.Attributes.Charisma);
        stalker.Attributes.AddCharisma(gain);
    }

    public static void RecordTrustworthinessEvent(Stalker stalker, string eventType)
    {
        float baseGain = eventType switch
        {
            "heal_ally" => 8.0f,
            "bounty_completed" => 5.0f,
            "treason" => -35.0f, // -25.0 to -50.0 average
            "loot_ally" => -35.0f,
            _ => 0f
        };
        
        if (baseGain < 0)
        {
            // Penalties apply directly or could also scale
            stalker.Attributes.AddTrustworthiness(baseGain);
        }
        else
        {
            float gain = CalculateGain(baseGain, stalker.Attributes.Trustworthiness);
            stalker.Attributes.AddTrustworthiness(gain);
        }
    }
}
