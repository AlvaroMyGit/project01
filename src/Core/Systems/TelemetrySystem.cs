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
    private float _leaderboardAccum;

    public TelemetrySystem(WebVisualizerServer webVisualizer)
    {
        _webVisualizer = webVisualizer;
    }

    public void Tick(SimulationContext ctx, float gameDelta)
    {
        BroadcastTelemetry(ctx);

        _leaderboardAccum += gameDelta / ctx.Time.TimeFactor;
        if (_leaderboardAccum >= 5f)
        {
            _leaderboardAccum = 0f;
            lock(ctx.EntityLock)
            {
                LeaderboardSerializer.SaveLeaderboard(ctx.Stalkers.ToList(), "data/leaderboard.json");
            }
        }
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
                Weather = ctx.Time.ElapsedGameSeconds > 0 ? "Clear" : "Clear", // Weather logic removed for brevity if missing, can inject WeatherManager if needed.
                StormActive = ctx.Emissions.IsStormActive,
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
                    LevelId = s.CurrentLevelId,
                    Health = 100,
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
                    LevelId = "surface",
                    LayerIndex = 0,
                    Health = 100,
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
