using System;
using System.Linq;
using StalkerALifeSandbox.Web;
using StalkerALifeSandbox.Systems;
using StalkerALifeSandbox.AI.GOAP;
using StalkerALifeSandbox.Entities.Characters;
using StalkerALifeSandbox.Entities.Mutants;

namespace StalkerALifeSandbox.Core.Systems;

public sealed class TelemetrySystem : ISimulationSystem
{
    private readonly WebVisualizerServer _webVisualizer;
    private readonly World.Environment.EnvironmentManager _environment;
    private readonly World.Environment.WeatherManager _weather;

    public TelemetrySystem(
        WebVisualizerServer webVisualizer,
        World.Environment.EnvironmentManager environment,
        World.Environment.WeatherManager weather)
    {
        _webVisualizer = webVisualizer;
        _environment = environment;
        _weather = weather;
    }

    public void Tick(SimulationContext ctx, float gameDelta)
    {
        BroadcastTelemetry(ctx);

    }

    private void BroadcastTelemetry(SimulationContext ctx)
    {
        float gameTime = (float)ctx.Time.ElapsedGameSeconds;
        TelemetryFrame frame;

        lock(ctx.EntityLock)
        {
            frame = new TelemetryFrame
            {
                Tick = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                TimeOfDay = $"{(int)ctx.Time.HourOfDay:D2}:{(int)((ctx.Time.HourOfDay % 1) * 60):D2}",
                Weather = "Clear", // We don't have weather in ctx, so we'll just hardcode for now or skip.
                StormActive = ctx.Emissions.IsStormActive,
                EmissionCountdown = Math.Max(0, ctx.Emissions.NextEmissionAt - gameTime),
                EmissionPhase = ctx.Emissions.CurrentPhase.ToString(),
                AnomalyFields = ctx.Emissions.Fields.Select(f => new AnomalyFieldDTO
                {
                    Id = f.Id,
                    Type = f.Type.ToString(),
                    Center = new AnomalyCenter { X = f.Center.X, Y = f.Center.Y, Z = f.Center.Z },
                    Radius = f.Radius,
                    Intensity = f.FieldIntensity,
                    IsStatic = f.IsStatic
                }).ToList(),
                Entities = ctx.Stalkers.Where(s => s.IsAlive).Select(s => new EntityDTO
                {
                    Id = s.Id,
                    Name = s.DisplayName,
                    Faction = s.TrueFaction,
                    Type = "stalker",
                    Position = new PositionDTO { X = s.Position.X, Y = s.Position.Z },
                    FacingAngle = s.Blackboard.FacingDegrees,
                    // The cone VisionCone actually sweeps, not a display value:
                    // 2 x HalfAngle, and the light/weather-adjusted range from
                    // the same formula the sweep uses.
                    FOV = AI.Perception.VisionCone.DefaultHalfAngle * 2f,
                    SightRange = AI.Perception.VisionCone.EffectiveSightRange(
                        _environment.LightLevel, _weather.VisibilityMod,
                        hasFlashlightOn: _environment.IsNight, hasNVGOn: false),
                    GoapTargetPosition = s.Blackboard.FinalDestination.HasValue ? new PositionDTO { X = s.Blackboard.FinalDestination.Value.X, Y = s.Blackboard.FinalDestination.Value.Z } : null,
                    LevelId = s.CurrentLevelId,
                    Health = (int)MathF.Round(s.Health),
                    CurrentGoal = StalkerGoapService.DescribeGoal(s),
                    Desperation = s.Needs.IsInCriticalState,
                    Activity = s.Activity ?? "",
                    LayerIndex = s.Position.Y < -10f ? -1 : 0,
                    Equipment = TelemetryMapper.BuildEquipment(s),
                    Mission = TelemetryMapper.BuildMission(s.ActiveMission),
                    SquadId = s.SquadId,
                    IsSquadLeader = s.IsSquadLeader
                }).Concat(ctx.Mutants.Where(m => m.IsAlive).Select(m => new EntityDTO
                {
                    Id = m.Id,
                    Name = m.Species,
                    Faction = "Mutants",
                    Type = "mutant",
                    Position = new PositionDTO { X = m.Position.X, Y = m.Position.Z },
                    FacingAngle = m.Blackboard.FacingDegrees,
                    // MutantBehaviourSystem does not consult VisionCone, so
                    // there is no cone to report. Zero tells the visualizer not
                    // to draw one rather than inventing a sight model.
                    FOV = 0f,
                    SightRange = 0f,
                    GoapTargetPosition = m.Blackboard.FinalDestination.HasValue ? new PositionDTO { X = m.Blackboard.FinalDestination.Value.X, Y = m.Blackboard.FinalDestination.Value.Z } : null,
                    LevelId = "surface",
                    LayerIndex = 0,
                    Health = (int)MathF.Round(m.Health),
                    CurrentGoal = m.Blackboard.OverrideNavigationStatus
                        ?? (m.IsHuntingPhase ? "Hunting" : "Roaming"),
                    Desperation = m.IsHuntingPhase,
                    Equipment = new EquipmentDTO()
                })).ToList(),
                Corpses = ctx.Corpses
                    .Where(c => !c.IsEaten)
                    .Select(c => TelemetryMapper.BuildCorpse(c, gameTime))
                    .ToList(),
                MissionStats = TelemetryMapper.BuildMissionStats(ctx.Stalkers, ctx.Missions)
            };
        }

        _ = _webVisualizer.BroadcastFrameAsync(frame);
    }
}
