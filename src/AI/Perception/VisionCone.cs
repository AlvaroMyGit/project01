// VisionCone.cs — Modified by LightLevel & Fog
using System.Numerics;
using StalkerALifeSandbox.AI.Blackboards;

namespace StalkerALifeSandbox.AI.Perception;

/// <summary>
/// Simulates a directional vision cone for an NPC.
/// Spec D: SightRange = BaseSight * LightLevel * VisibilityMod + FlashlightBonus
/// </summary>
public sealed class VisionCone
{
    public float BaseSight { get; set; } = 80f;
    public float HalfAngle { get; set; } = 55f;  // degrees

    /// <summary>
    /// Run a perception sweep. <paramref name="facing"/> is the
    /// unit-vector the NPC is looking along.
    /// Uses environmental factors (light, visibility/fog) and equipment states.
    /// </summary>
    public void Sweep(
        NPCBlackboard bb,
        Vector3 facing,
        float lightLevel,
        float visibilityMod,
        bool hasFlashlightOn,
        bool hasNVGOn,
        float gameTime,
        IEnumerable<(string Id, Vector3 Pos, bool IsFlashlightOn)> candidates)
    {
        float cosHalf = MathF.Cos(HalfAngle * MathF.PI / 180f);

        // Spec D: Night Vision Goggles grant full night vision with zero light footprint.
        float effectiveLight = hasNVGOn ? 1.0f : Math.Clamp(lightLevel, 0.05f, 1.0f);
        
        // Spec D: SightRange = BaseSight * LightLevel * VisibilityMod + FlashlightBonus
        float flashlightBonus = (!hasNVGOn && hasFlashlightOn) ? 20f : 0f;
        float myRange = (BaseSight * effectiveLight * visibilityMod) + flashlightBonus;

        foreach (var (id, pos, isTargetFlashlightOn) in candidates)
        {
            var delta = pos - bb.CurrentPosition;
            float dist = delta.Length();
            if (dist < 0.01f) continue;

            // Spec D: Flashlight beacon is visible up to 80m away at night
            float effectiveRangeForTarget = myRange;
            if (isTargetFlashlightOn && lightLevel < 0.5f)
            {
                effectiveRangeForTarget = MathF.Max(myRange, 80f);
            }

            if (dist > effectiveRangeForTarget) continue;

            var dir = Vector3.Normalize(delta);
            float dot = Vector3.Dot(facing, dir);
            
            // Allow 360-degree detection if target is extremely close, else restrict to cone
            if (dist > 3f && dot < cosHalf) 
            {
                // If they have a flashlight on and we are looking generally in their direction,
                // give a bit more leeway on the angle.
                if (!(isTargetFlashlightOn && dot > 0f))
                {
                    continue;
                }
            }

            // Raycast LOS check would go here in a real engine
            bb.RegisterSighting(id, pos, gameTime);
        }
    }
}
