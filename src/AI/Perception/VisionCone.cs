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
    /// <summary>Unmodified sight range, before light and weather.</summary>
    public const float DefaultBaseSight = 80f;

    /// <summary>Half the cone's opening angle, in degrees. The full cone is twice this.</summary>
    public const float DefaultHalfAngle = 55f;

    /// <summary>Extra range from a lit torch at night.</summary>
    public const float FlashlightBonus = 20f;

    /// <summary>
    /// Light level at or below which a stalker lights a torch. Set to the
    /// horizon value, so the torch comes on as the sun goes down rather than
    /// on the clock.
    /// </summary>
    public const float TorchLightThreshold = 0.35f;

    /// <summary>
    /// Whether a stalker would have a torch lit at this light level.
    ///
    /// Callers used to pass <c>EnvironmentManager.IsNight</c>, which is a clock
    /// predicate (21:00-06:00) and disagreed with the light for three hours
    /// either side: at 20:59 a stalker stood in near-darkness with the torch
    /// still in their pocket, seeing 4 m, then lit it on the stroke of 21:00
    /// and saw 24 m. Dusk was darker than midnight. Keyed to light instead,
    /// the torch switches when it is actually needed. The 20 m step at the
    /// threshold stays, because a torch really does switch on all at once.
    /// </summary>
    public static bool TorchWouldBeLit(float lightLevel) => lightLevel <= TorchLightThreshold;

    public float BaseSight { get; set; } = DefaultBaseSight;
    public float HalfAngle { get; set; } = DefaultHalfAngle;  // degrees

    /// <summary>
    /// Spec D: SightRange = BaseSight * LightLevel * VisibilityMod + FlashlightBonus.
    ///
    /// Exposed as a static so the telemetry layer can report the same number the
    /// sweep actually uses. The visualizer draws vision cones from it, and a
    /// drawn cone that disagreed with the one being simulated would be worse
    /// than drawing none.
    /// </summary>
    public static float EffectiveSightRange(
        float lightLevel, float visibilityMod, bool hasFlashlightOn, bool hasNVGOn,
        float baseSight = DefaultBaseSight)
    {
        float effectiveLight = hasNVGOn ? 1.0f : Math.Clamp(lightLevel, 0.05f, 1.0f);
        float bonus = (!hasNVGOn && hasFlashlightOn) ? FlashlightBonus : 0f;
        return (baseSight * effectiveLight * visibilityMod) + bonus;
    }

    /// <summary>
    /// Run a perception sweep. <paramref name="origin"/> is where the NPC is
    /// sensing from and <paramref name="facing"/> the unit-vector they are
    /// looking along.
    /// Uses environmental factors (light, visibility/fog) and equipment states.
    ///
    /// The origin is passed in rather than read from
    /// <c>bb.CurrentPosition</c> deliberately. That field is refreshed at 1 Hz
    /// by <c>Stalker.TickNeeds</c>, and several 10 Hz consumers read it —
    /// squad follow destinations among them. A sensor that quietly refreshed
    /// it to keep itself accurate would tighten squad spread by a third, which
    /// is a behaviour change, not an observation.
    /// </summary>
    /// <returns>How many candidates were sighted this sweep.</returns>
    public int Sweep(
        NPCBlackboard bb,
        Vector3 origin,
        Vector3 facing,
        float lightLevel,
        float visibilityMod,
        bool hasFlashlightOn,
        bool hasNVGOn,
        float gameTime,
        IEnumerable<(string Id, Vector3 Pos, bool IsFlashlightOn)> candidates)
    {
        int sighted = 0;
        float cosHalf = MathF.Cos(HalfAngle * MathF.PI / 180f);

        // Spec D: Night Vision Goggles grant full night vision with zero light
        // footprint; range is otherwise light- and weather-scaled. Shared with
        // the telemetry layer so the drawn cone matches the simulated one.
        float myRange = EffectiveSightRange(lightLevel, visibilityMod, hasFlashlightOn, hasNVGOn, BaseSight);

        foreach (var (id, pos, isTargetFlashlightOn) in candidates)
        {
            var delta = pos - origin;
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
            sighted++;
        }

        return sighted;
    }
}
