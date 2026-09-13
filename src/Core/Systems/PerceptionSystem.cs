using System.Numerics;
using StalkerALifeSandbox.AI.Perception;
using StalkerALifeSandbox.Entities.Characters;
using StalkerALifeSandbox.Systems;
using StalkerALifeSandbox.World.Environment;

namespace StalkerALifeSandbox.Core.Systems;

/// <summary>
/// Fills each stalker's <c>KnownEntities</c> from what they can actually see and
/// hear, rather than from bare proximity.
///
/// <see cref="VisionCone"/> and <see cref="AcousticSensor"/> have been complete
/// since the start of the project with nothing calling them — the missing piece
/// was that nothing tracked which way anyone was facing. With
/// <c>NPCBlackboard.Facing</c> in place they finally have their inputs.
///
/// RUNS IN SHADOW MODE by default. Perception fills <c>KnownEntities</c> and
/// reports how much of the proximity model's engagement set it covers, but
/// combat is NOT wired to it. Combat rates are tuned (see CombatBalanceConfig)
/// and swapping the detection model blind would destabilise them — the same
/// reason the mutant-speed and healing changes were measured before being
/// trusted.
///
/// "Shadow" is enforced, not assumed, and it took two rounds of measurement to
/// actually earn the word. Nothing outside this system reads
/// <c>KnownEntities</c>, but two other channels leaked:
///
/// <list type="number">
/// <item><description>
/// <see cref="AcousticSensor"/> raises <c>LocationThreatMemory</c>, which
/// <c>GoapWorldStateSync</c> turns into <c>HeardDangerRumor</c> — a live input
/// to goal selection. Unguarded it moved missions accepted -12%. Now gated
/// behind <see cref="PerceptionOptions.ThreatMemoryFeedsGoap"/>.
/// </description></item>
/// <item><description>
/// Refreshing <c>bb.CurrentPosition</c> so the sensors had an accurate origin.
/// That field is a 1 Hz snapshot, and squad follow destinations
/// (<c>Squad</c>) and corpse selection (<c>ActionInvestigateCorpseGoap</c>)
/// read it at 10 Hz. Writing it here tightened squad spread by 36% and cost 5%
/// of the population. The sensors now take an explicit origin instead.
/// </description></item>
/// </list>
///
/// Both were found by A/B-ing this system off against on over three runs each
/// on one frozen binary — neither was visible by reading the code.
/// </summary>
public sealed class PerceptionSystem : ISimulationSystem
{
    private readonly VisionCone _vision = new();
    private readonly AcousticSensor _hearing = new();
    private readonly EnvironmentManager _environment;
    private readonly WeatherManager _weather;
    private readonly NoiseBus _noise;

    private readonly PerceptionOptions _options;

    public PerceptionSystem(
        EnvironmentManager environment,
        WeatherManager weather,
        NoiseBus noise,
        PerceptionOptions? options = null)
    {
        _environment = environment;
        _weather = weather;
        _noise = noise;
        _options = options ?? new PerceptionOptions();
    }

    public void Tick(SimulationContext ctx, float gameDelta)
    {
        if (!_options.Enabled)
        {
            _noise.Clear();   // or emitters accumulate into a bus nobody drains
            return;
        }

        Stalker[] stalkers;
        lock (ctx.EntityLock) { stalkers = ctx.Stalkers.ToArray(); }

        float gameTime = (float)ctx.Time.ElapsedGameSeconds;
        float light = _environment.LightLevel;
        float visibility = _weather.VisibilityMod;
        float rain = _weather.RainIntensity;
        var noises = _noise.Current;

        int seen = 0, heard = 0, contested = 0, known = 0, observers = 0;

        foreach (var s in stalkers)
        {
            if (!s.IsAlive) continue;
            observers++;

            var bb = s.Blackboard;
            bb.PruneStaleEntities(gameTime, _options.MemoryGameSeconds);

            // Sense from the live entity position, and do NOT write it back to
            // bb.CurrentPosition. That field is a 1 Hz snapshot which squad
            // follow destinations and corpse selection read at 10 Hz;
            // refreshing it here moved population -5% and squad spread -36%
            // while this system was supposedly read-only.
            seen += _vision.Sweep(
                bb, s.Position, bb.Facing, light, visibility,
                hasFlashlightOn: _environment.IsNight,   // no equipment flag yet; night implies a light
                hasNVGOn: false,
                gameTime,
                Candidates(stalkers, s, _options.CandidateRadius));

            heard += _hearing.Process(
                bb, s.Position, gameTime, rain, noises, _options.ThreatMemoryFeedsGoap);

            // Coverage: of the hostile pairs the proximity model would hand to
            // combat this tick, how many does perception already know about?
            // Both sides count the same population — every in-range hostile
            // pair, every tick — so the ratio is meaningful. Counting raw
            // sightings against this instead would compare a sensor's output
            // to a pair census and report a number that means nothing.
            foreach (var other in stalkers)
            {
                if (!other.IsAlive || ReferenceEquals(other, s)) continue;
                if (Vector3.Distance(s.Position, other.Position) > _options.CombatEngageRange) continue;
                if (!ctx.Factions.AreHostile(s.TrueFaction, other.TrueFaction)) continue;

                contested++;
                if (bb.KnownEntities.ContainsKey(other.Id)) known++;
            }
        }

        // Drain what was heard. Noises are emitted by the behaviour systems
        // that run AFTER this one, so each tick hears the previous tick's
        // gunfire — a 100 ms lag that is both harmless and realistic. Clearing
        // at the top of the tick instead meant perception always read an empty
        // bus and nothing was ever heard.
        _noise.Clear();

        SimulationDebugLog.RecordPerception(observers, seen, heard, contested, known);
    }

    /// <summary>Everything close enough to be worth the cone maths.</summary>
    private static IEnumerable<(string Id, Vector3 Pos, bool IsFlashlightOn)> Candidates(
        Stalker[] all, Stalker observer, float radius)
    {
        foreach (var other in all)
        {
            if (!other.IsAlive || ReferenceEquals(other, observer)) continue;
            if (Vector3.Distance(observer.Position, other.Position) > radius) continue;
            yield return (other.Id, other.Position, false);
        }
    }
}
