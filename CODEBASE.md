# Codebase Documentation — S.T.A.L.K.E.R. A-Life Sandbox

> **Last updated:** 2026-09-14  
> **Engine:** C# (.NET 8) · **Visualizer:** HTML5 / PixiJS v7  
> **Entry point:** `Program.cs` → `SimulationLoop.cs` (10 Hz via `ZoneDirector`)

This document provides a detailed reference for every module, class, and data file in the project. For a high-level overview, see [README.md](README.md).

---

## Table of Contents

- [Architecture Overview](#architecture-overview)
- [Entry Point — Program.cs](#entry-point--programcs)
- [src/Core/ — Simulation Engine](#srccore--simulation-engine)
- [src/Core/Systems/ — Tick-Driven Subsystems](#srccoresystems--tick-driven-subsystems)
- [src/AI/ — Artificial Intelligence](#srcai--artificial-intelligence)
  - [GOAP Core](#goap-core)
  - [GOAP Actions](#goap-actions)
  - [GOAP Goals](#goap-goals)
  - [Decision](#decision)
  - [Perception](#perception)
  - [Social](#social)
  - [Squads](#squads)
  - [Blackboards](#blackboards)
- [src/Systems/ — Gameplay Systems](#srcsystems--gameplay-systems)
- [src/World/ — World Generation & Navigation](#srcworld--world-generation--navigation)
  - [Environment](#environment)
  - [Generation](#generation)
  - [Hazards](#hazards)
  - [Navigation](#navigation)
  - [POI](#poi)
- [src/Entities/ — Data Models](#srcentities--data-models)
  - [Characters](#characters)
  - [Equipment](#equipment)
  - [Mutants](#mutants)
  - [Needs](#needs)
- [src/Economy/ — Trade & Missions](#srceconomy--trade--missions)
- [src/Factions/ — Faction System](#srcfactions--faction-system)
- [src/Crafting/ — Field Crafting](#srccrafting--field-crafting)
- [src/PDA/ — Communication Network](#srcpda--communication-network)
- [src/Web/ — WebSocket & Telemetry](#srcweb--websocket--telemetry)
- [visualizer/ — Browser Dashboard](#visualizer--browser-dashboard)
- [data/ — JSON Data Tables](#data--json-data-tables)
- [scripts/ — Python Utilities](#scripts--python-utilities)
- [tests/ — Test Suite](#tests--test-suite)
- [Engineering Notes](#engineering-notes)
  - [Two failure modes that keep recurring](#two-failure-modes-that-keep-recurring)
  - [Invariants worth not breaking](#invariants-worth-not-breaking)
  - [Measurement](#measurement)
  - [Current equilibrium](#current-equilibrium)
  - [Tuning that is load-bearing](#tuning-that-is-load-bearing)
  - [Unwired code](#unwired-code)
- [scripts/ — Maintenance Utilities](#scripts--maintenance-utilities)

---

## Architecture Overview

The simulation follows a **modular, event-driven, data-oriented** architecture. All gameplay systems implement `ISimulationSystem` and are registered into frequency-based tick buckets managed by `ZoneDirector`:

| Frequency | Cadence | Systems |
|---|---|---|
| **High (10 Hz)** | Every 100ms | Emissions, perception, combat, movement, telemetry broadcast |
| **Low (1 Hz)** | Every 1s | GOAP replanning, needs decay, social evaluation, betrayal |
| **Macro (0.1 Hz)** | Every 10s | Corpse cleanup, field crafting |

Communication between decoupled systems is handled by a global `EventBus` using typed struct events. The simulation state is bundled into an immutable `SimulationContext` record passed to every subsystem on each tick.

```
┌──────────────────────────────────────────────────────────┐
│                      Program.cs                          │
│  (Bootstrap, data loading, world gen, REST API on 5050)  │
└──────────────┬───────────────────────────────────────────┘
               │
               ▼
┌──────────────────────────────────────────────────────────┐
│                    SimulationLoop.cs                      │
│         (Orchestrator — creates SimulationContext)        │
└──────────────┬───────────────────────────────────────────┘
               │
               ▼
┌──────────────────────────────────────────────────────────┐
│                    ZoneDirector.cs                        │
│        (Multi-frequency tick bucket scheduler)           │
│   ┌─────────┐   ┌─────────┐   ┌───────────┐            │
│   │ 10 Hz   │   │  1 Hz   │   │  0.1 Hz   │            │
│   │ Combat  │   │  GOAP   │   │  Economy  │            │
│   │ Move    │   │  Needs  │   │  Spawns   │            │
│   │ Telemetry│  │  Social │   │  Emissions│            │
│   └─────────┘   └─────────┘   └───────────┘            │
└──────────────────────────────────────────────────────────┘
               │
               ▼
┌──────────────────────────────────────────────────────────┐
│         WebVisualizerServer.cs (via ASP.NET Core)         │
│  "/ws" on port 5050 — WebSocket broadcast → app.js       │
└──────────────────────────────────────────────────────────┘
```

---

## Entry Point — Program.cs

**File:** [`Program.cs`](file:///home/alvaromendes/Documents/project01/Program.cs) (~40 lines)

`Program.Main` is a thin shell: it builds and starts a [`SimulationHost`](file:///home/alvaromendes/Documents/project01/src/Core/SimulationHost.cs), then configures the ASP.NET Core web host (CORS, static files) and maps the REST API via [`WebApiEndpoints.MapSimulationApi`](file:///home/alvaromendes/Documents/project01/src/Web/WebApiEndpoints.cs) before running on port 5050.

`SimulationHost` is the composition root and performs the full bootstrap sequence:

1. **Data Loading** — Loads `NameGenerator`, `DemographicsEngine`, `PDANetwork`, `FactionSpawnTable`, `ItemDatabase` from JSON data files (resolved via [`DataPaths`](file:///home/alvaromendes/Documents/project01/src/Core/DataPaths.cs) relative to the app base directory, not the working directory)
2. **World Generation** — Creates `StaticWorldGenerator` (1600×3200 world), stamps POIs via `POIPrefabStamper`, builds `RoadNetwork`, initializes `ZonePathfinder` grid, loads `BuildingFootprintLoader`, seeds anomaly fields via `AnomalySeeder`
3. **Faction Setup** — Spawns macro-base faction leaders, initializes `TraderRegistry` and `MissionRegistry`
4. **Simulation Init** — Configures `TimeManager`, `EnvironmentManager`, `WeatherManager`, `ZoneDirector`, and instantiates `SimulationLoop` (via a `SimulationDependencies` parameter object) with 12-minute staggered spawn toward a target of 750 stalkers and 500 mutants (the design-doc figures; the loop sustains ~410 at equilibrium, set by lethality rather than by the tick budget)
5. **Web Host** — `Program.Main` builds a `SimulationSettings`, starts a single ASP.NET Core host on the configured REST port (default 5050) with CORS scoped to the local dashboard origins, and serves the visualizer, the REST API, and the `/ws` WebSocket telemetry stream all from that one Kestrel instance (`app.UseWebSockets()` + `WebApiEndpoints.MapSimulationApi`) — there is no separate WebSocket server/port. Shutdown is graceful: `ApplicationStopping` calls `SimulationHost.Stop()` (disposes the tick timer, aborts connected WebSocket clients, flushes the final report), and the optional `STALKER_RUN_DURATION_SEC` auto-stop requests a graceful shutdown rather than calling `Environment.Exit`.

### REST API Endpoints

| Endpoint | Method | Description |
|---|---|---|
| `/` | GET | Serves the visualizer dashboard (`index.html`) |
| `/api/world` | GET | Full world geometry — regions, POIs, roads, buildings, rad zones, threat map |
| `/api/state` | GET | Current living entities, population demographics, mission stats, recent kills |
| `/api/leaderboard` | GET | Top 100 stalker rankings |
| `/api/factions` | GET | 12×12 diplomacy relationship matrix |
| `/ws` | WebSocket | Live telemetry stream (10 Hz `TelemetryFrame` broadcast) plus `inspect` and command (`set_speed`, `force_emission`, `force_weather`) messages — same host/port as the REST endpoints above |

---

## src/Core/ — Simulation Engine

### [`ISimulationSystem.cs`](file:///home/alvaromendes/Documents/project01/src/Core/ISimulationSystem.cs)
Interface contract for all modular simulation subsystems. Defines `Tick(SimulationContext ctx, float gameDelta)` called at the system's registered frequency.

### [`SimulationContext.cs`](file:///home/alvaromendes/Documents/project01/src/Core/SimulationContext.cs)
Lightweight immutable record bundling references to all live simulation state: `Stalkers`, `Mutants`, `EntityLock`, `Corpses`, `Time`, `Factions`, `WorldGen`, `Stamper`, `Pathfinder`, `Emissions`, `PDA`, `Traders`, `Missions`, `MacroPois`, `WildPoiCandidates`, and `RequestReplan`. Passed to every `ISimulationSystem.Tick()` call.

### [`SimulationLoop.cs`](file:///home/alvaromendes/Documents/project01/src/Core/SimulationLoop.cs) (230 lines)
Central orchestrator that initializes the simulation context, registers all `ISimulationSystem` implementations into their respective tick buckets, starts the high-resolution timer, and handles shutdown. Key methods: `Start()`, `RunHeadless(int)`, `ConfigureInitialSpawn()`, `RegisterStalkerListeners()`, `FlushDebugReport()`.

`RunHeadless(n)` runs exactly *n* ticks synchronously with no timer — the measurement harness. Two timed runs cannot be compared, because the loop drops a variable number of ticks under load; with the tick count as the input, two runs cover exactly the same span of game time. `DroppedTicks`/`ExecutedTicks` are reported alongside the effective TimeFactor they imply.

### [`TickProfiler.cs`](file:///home/alvaromendes/Documents/project01/src/Core/TickProfiler.cs)
Accumulates wall-clock time per simulation system and prints an attribution table in the final report. Sim-thread only, so the accumulators need no synchronisation. Built before any optimisation work because the loop was at ~89 ms/tick against a 100 ms budget and nobody knew where it went — three separate performance hypotheses turned out to be wrong, and it found the real cost each time.

### [`TimeManager.cs`](file:///home/alvaromendes/Documents/project01/src/Core/TimeManager.cs)
Tracks game clock progression. Converts real delta seconds to simulated game time using a configurable `TimeFactor` (default 3.0, overridable via `STALKER_TIME_FACTOR`). Exposes `ElapsedGameSeconds`, `HourOfDay`, `DayNumber`.

### [`ZoneDirector.cs`](file:///home/alvaromendes/Documents/project01/src/Core/ZoneDirector.cs)
Master tick scheduler distributing elapsed engine time into three accumulator buckets: `RegisterHighFrequency` (10 Hz), `RegisterLowFrequency` (1 Hz), `RegisterMacroFrequency` (0.1 Hz). The core `Tick(float deltaSeconds)` method drains accumulators and fires registered callbacks.

### [`EventBus.cs`](file:///home/alvaromendes/Documents/project01/src/Core/EventBus.cs)
Thread-safe global publish-subscribe hub keyed by struct event types. Methods: `Subscribe<T>()`, `Unsubscribe<T>()`, `Publish<T>()`, `ClearAll()`. Defined event structs include: `DeathLogEvent`, `BlowoutWarningEvent`, `EmissionPhaseChangedEvent`, `FactionNewsEvent`, `TradeOfferEvent`, `BountyEvent`, `MutantEncounterEvent`.

### [`DataPaths.cs`](file:///home/alvaromendes/Documents/project01/src/Core/DataPaths.cs)
Resolves bundled `data/**` files relative to `AppContext.BaseDirectory` (where the build copies them) rather than the current working directory, so data loading is independent of where the process is launched. `Resolve(params string[])` builds a path; `Require(...)` throws a clear `FileNotFoundException` at load time if the file is missing. All JSON loaders route through this. The csproj copies `data/**` to the output directory (`PreserveNewest`).

### [`SimulationSnapshot.cs`](file:///home/alvaromendes/Documents/project01/src/Core/SimulationSnapshot.cs)
Immutable point-in-time view of simulation state (entity pins, population/mission counts, PDA feed, top-100 leaderboard, and per-entity inspector payloads) consumed by the web layer. **Threading contract:** all simulation ticks run on a single timer thread — the sole writer of entity state — which builds a snapshot once per 1 Hz tick via `SimulationSnapshot.Build(...)`. `SimulationLoop` publishes it through a volatile reference (`CurrentSnapshot`); REST endpoints and the WebSocket inspect handler read it lock-free and never touch live entities, eliminating torn-read races.

### [`SimulationHost.cs`](file:///home/alvaromendes/Documents/project01/src/Core/SimulationHost.cs)
Composition root. Loads data, generates the world and entities, wires the `SimulationLoop`, and starts it (`Start()` also arms the optional `STALKER_RUN_DURATION_SEC` auto-stop). Exposes the read-only pieces the web layer needs (`WorldGen`, `Stamper`, `RoadNetwork`, `BuildingFootprints`, `Emissions`, `Factions`, threat map, and `Simulation`).

### [`SimulationDependencies.cs`](file:///home/alvaromendes/Documents/project01/src/Core/SimulationDependencies.cs)
Named parameter object grouping the ~20 collaborators `SimulationLoop` needs, replacing a long positional-argument constructor. Built once by `SimulationHost`.

### [`SimulationSettings.cs`](file:///home/alvaromendes/Documents/project01/src/Core/SimulationSettings.cs)
Central host configuration — the single REST/dashboard/WebSocket port, population targets, and allowed CORS origins — replacing values previously hard-coded in multiple places. `FromEnvironment()` applies a `STALKER_REST_PORT` override.

---

## src/Core/Systems/ — Tick-Driven Subsystems

All classes in this directory implement `ISimulationSystem`.

### [`CorpseCleanupSystem.cs`](file:///home/alvaromendes/Documents/project01/src/Core/Systems/CorpseCleanupSystem.cs)
Purges dead bodies from `CorpseRegistry` based on despawn rules defined in `CorpseCleanupService` (idle timeout, post-interaction timeout, post-feeding timeout).

### [`EmissionTickSystem.cs`](file:///home/alvaromendes/Documents/project01/src/Core/Systems/EmissionTickSystem.cs)
Ticks the emission state machine at 10 Hz. Damages unsheltered entities during blowout phases, applies anomaly radiation/elemental exposure, and triggers zombification or death with 70/30 probability split.

### [`FieldCraftingSystem.cs`](file:///home/alvaromendes/Documents/project01/src/Core/Systems/FieldCraftingSystem.cs)
Runs at 0.1 Hz. Handles scrap scavenging, passive mutant meat cooking, gear degradation over time, and field repair using salvaged components.

### [`MutantBehaviourSystem.cs`](file:///home/alvaromendes/Documents/project01/src/Core/Systems/MutantBehaviourSystem.cs)
Drives mutant AI at 10 Hz: feeding on nearby corpses, nocturnal den retreat, avoidance of safe bases, and wilderness wandering patterns.

### [`PerceptionSystem.cs`](file:///home/alvaromendes/Documents/project01/src/Core/Systems/PerceptionSystem.cs)
Runs `VisionCone` and `AcousticSensor` for every living stalker at 10 Hz, ahead
of `StalkerBehaviourSystem` so what a stalker knows is gathered before anything
acts on it. Reports shadow-mode coverage: of the hostile pairs the proximity
model would hand to combat, how many perception already knows about.

Two things make the "shadow" real rather than assumed. Nothing outside this
system reads `KnownEntities` — but `AcousticSensor` also raises
`LocationThreatMemory`, which `GoapWorldStateSync` turns into
`HeardDangerRumor`, so hearing is a live input to goal selection. Unguarded it
moved missions accepted by **-12%** and mutant deaths by **+51%** against the
stored baseline while the system was supposedly read-only. It is now gated
behind `PerceptionOptions.ThreatMemoryFeedsGoap`, off by default.

### [`SocialSystem.cs`](file:///home/alvaromendes/Documents/project01/src/Core/Systems/SocialSystem.cs)
Evaluates betrayal contracts for desperate low-trust stalkers at 1 Hz and ticks disguise suspicion accumulation against undercover stalkers.

### [`SpawnOrchestrator.cs`](file:///home/alvaromendes/Documents/project01/src/Core/Systems/SpawnOrchestrator.cs)
Manages the 12-minute staggered initial population deployment and ongoing trickle respawn. Uses rubber-band logic to maintain target population quotas. Key methods: `ConfigureInitialSpawn()`, `TickInitialSpawn()`, `TrickleRespawn()`.

### [`StalkerBehaviourSystem.cs`](file:///home/alvaromendes/Documents/project01/src/Core/Systems/StalkerBehaviourSystem.cs)
Executes 10 Hz combat checks (stalker-vs-stalker and stalker-vs-mutant), squad formation following, corpse discovery broadcasting, and active path advancement.

### [`TelemetrySystem.cs`](file:///home/alvaromendes/Documents/project01/src/Core/Systems/TelemetrySystem.cs)
Collects all live entities into a `TelemetryFrame` at 10 Hz and broadcasts via WebSocket to connected visualizer clients.

---

## src/AI/ — Artificial Intelligence

### GOAP Core

Located in `src/AI/GOAP/`:

| File | Class | Description |
|---|---|---|
| [`GOAPAction.cs`](file:///home/alvaromendes/Documents/project01/src/AI/GOAP/GOAPAction.cs) | `GOAPAction` | Abstract base for planner actions with preconditions, effects, cost evaluation, and execute lifecycle |
| [`GOAPGoal.cs`](file:///home/alvaromendes/Documents/project01/src/AI/GOAP/GOAPGoal.cs) | `GOAPGoal` | Abstract base for high-level desires returning 0–100 utility scores |
| [`GOAPPlanner.cs`](file:///home/alvaromendes/Documents/project01/src/AI/GOAP/GOAPPlanner.cs) | `GOAPPlanner` | A* backward-chaining planner building executable action sequences from goal state to current state |
| [`GoapContext.cs`](file:///home/alvaromendes/Documents/project01/src/AI/GOAP/GoapContext.cs) | `GoapContext` | Service locator holding references to world, navigation, economy, and mission services |
| [`GoapKeys.cs`](file:///home/alvaromendes/Documents/project01/src/AI/GOAP/GoapKeys.cs) | `GoapKeys`, `GoapTuning` | World-state boolean constants, plus `GoapTuning.MissionGiverRadius` — shared by the world-state sync and the payout gate, which must agree or a stalker plans around a flag the payout then refuses |
| [`GoapActionState.cs`](file:///home/alvaromendes/Documents/project01/src/AI/GOAP/GoapActionState.cs) | `GoapActionState` | Per-execution scratch for the action a stalker is currently running, held on `NPCBlackboard`. Actions are registered as single shared instances, so any mutable field on one is global rather than per-stalker — that bug broke the mission loop outright. Reset at one choke point before every `Enter` |
| [`Goals/GoalSocialise.cs`](file:///home/alvaromendes/Documents/project01/src/AI/GOAP/Goals/GoalSocialise.cs) | `GoalSocialise` | Gives socialising a goal of its own. The campfire actions previously declared `HasCompletedPatrol`, so a stalker could only socialise as a cheap way to call a patrol finished |
| [`Goals/GoalRecover.cs`](file:///home/alvaromendes/Documents/project01/src/AI/GOAP/Goals/GoalRecover.cs) | `GoalRecover` | Withdraw and patch up. Triggers late (below 30% health) — an earlier 75% threshold had the whole population permanently retreating and took gunfire deaths from 209 a run to under 10 |
| [`Actions/ActionTreatWounds.cs`](file:///home/alvaromendes/Documents/project01/src/AI/GOAP/Actions/ActionTreatWounds.cs) | `ActionTreatWounds` | Spends a carried dressing for 35% health. The **only** way to heal — resting deliberately does not, because free recovery makes attrition meaningless |
| [`Squads/SquadNeeds.cs`](file:///home/alvaromendes/Documents/project01/src/AI/Squads/SquadNeeds.cs) | `SquadNeeds` | Surfaces each squad's worst hunger/thirst, lowest morale and dry-rifle count onto its **leader's** blackboard, so leaders plan for men who cannot plan for themselves |
| [`GoapRuntime.cs`](file:///home/alvaromendes/Documents/project01/src/AI/GOAP/GoapRuntime.cs) | `GoapRuntime` | Per-stalker active plan execution state (current action index, entered flag, goal name) |
| [`GoapWorldStateSync.cs`](file:///home/alvaromendes/Documents/project01/src/AI/GOAP/GoapWorldStateSync.cs) | `GoapWorldStateSync` | Translates continuous physical state into discrete boolean flags on the blackboard before planning |
| [`StalkerGoapService.cs`](file:///home/alvaromendes/Documents/project01/src/AI/GOAP/StalkerGoapService.cs) | `StalkerGoapService` | Top-level GOAP coordinator: world-state sync + replanning at 1 Hz, action execution at 10 Hz |

### GOAP Actions

Located in `src/AI/GOAP/Actions/`:

| File | Description |
|---|---|
| `GoapTravelAction.cs` | Abstract base for all movement actions; handles zone gating, pathfinding, and threat cost evaluation |
| `ActionAcceptMission.cs` | Signs a contract at a base trader/commander |
| `ActionCookMutantMeatGoap.cs` | Campfire cooking to purge radiation and satisfy hunger |
| `ActionCraftUpgrade.cs` | Field repair using scrap components |
| `ActionExploreLab.cs` | Underground X-Lab exploration for rare loot |
| `ActionFulfillMission.cs` | Navigates to mission objective and completes the task (with ±30–90s random variance) |
| `ActionGoHome.cs` | Pathfinds to the stalker's home base |
| `ActionGoToMissionGiver.cs` | Moves to a contract-offering NPC |
| `ActionGoToShelter.cs` | Emergency shelter-seeking during emissions |
| `ActionHarvestArtifact.cs` | Anomaly field artifact extraction using detectors |
| `ActionInvestigateCorpseGoap.cs` | Corpse looting and death report filing |
| `ActionPatrolWilds.cs` | POI-driven perimeter patrol routes |
| `ActionPlayGuitar.cs` | Campfire social action emitting morale aura |
| `ActionRestAtBase.cs` | Base quarters rest regenerating fatigue and health |
| `ActionRestAtPOI.cs` | Short rest at intermediate outposts |
| `ActionReturnToMissionIssuer.cs` | Travel back to mission giver after objective completion |
| `ActionShareDrink.cs` | Vodka sharing with companions for morale and radiation purge |
| `ActionTradeRun.cs` | Trader visit for buying/selling supplies and gear |
| `ActionTurnInMission.cs` | Contract completion — collect rubles, XP, and reputation |
| `ActionVisitStash.cs` | Hidden stash retrieval |

### GOAP Goals

Located in `src/AI/GOAP/Goals/`:

| File | Utility Trigger |
|---|---|
| `GoalAcceptMission.cs` | No active job + near an issuer with contracts |
| `GoalCompleteMission.cs` | Highest priority after objective completion — return and turn in |
| `GoalCookFood.cs` | Carrying raw mutant meat + hungry + near campfire |
| `GoalExploreLab.cs` | Expert+ stalkers with high zone comfort seeking rare artifacts |
| `GoalFleeEmission.cs` | Maximum utility (~150) when blowout warnings sound |
| `GoalPatrol.cs` | Baseline idle desire for squad leaders to patrol territory |
| `GoalRepairGear.cs` | Weapon/armor below 70% condition + scrap available |
| `GoalRest.cs` | Utility climbs linearly with fatigue |
| `GoalSatisfyHunger.cs` | Utility climbs with hunger and thirst |
| `GoalSeekLoot.cs` | Low ammo or money — raids stashes and corpses |
| `GoalSeekShelter.cs` | Night, extreme weather, or high danger |
| `GoalVisitTrader.cs` | Holding valuable artifacts or critically low on ammo |

### Decision

| File | Class | Description |
|---|---|---|
| [`ArtifactDecisionEngine.cs`](file:///home/alvaromendes/Documents/project01/src/AI/Decision/ArtifactDecisionEngine.cs) | `ArtifactDecisionEngine` | Decides whether to equip, sell, or stash discovered artifacts based on rarity and desperation |
| [`ZoneGateEvaluator.cs`](file:///home/alvaromendes/Documents/project01/src/AI/Decision/ZoneGateEvaluator.cs) | `ZoneGateEvaluator` | Rank-based regional gating — prevents rookies from wandering into deadly northern zones |

### Perception

| File | Class | Description |
|---|---|---|
| [`AcousticSensor.cs`](file:///home/alvaromendes/Documents/project01/src/AI/Perception/AcousticSensor.cs) | `AcousticSensor` | Hearing simulation detecting gunfire, footsteps, anomaly pulses within 60m (muffled by rain). Returns how many events were heard; `recordThreat: false` keeps it from touching `LocationThreatMemory` |
| [`VisionCone.cs`](file:///home/alvaromendes/Documents/project01/src/AI/Perception/VisionCone.cs) | `VisionCone` | Directional 80m/110° sight sweep affected by sunlight, fog, NVG, and flashlights. Returns how many candidates were sighted |
| [`NoiseBus.cs`](file:///home/alvaromendes/Documents/project01/src/AI/Perception/NoiseBus.cs) | `NoiseBus` | One tick's worth of noise events. `StalkerBehaviourSystem` emits gunshots into it; `PerceptionSystem` consumes and drains it |
| [`PerceptionOptions.cs`](file:///home/alvaromendes/Documents/project01/src/AI/Perception/PerceptionOptions.cs) | `PerceptionOptions` | Immutable settings + `FromEnvironment()`. `Enabled` and `ThreatMemoryFeedsGoap` are the two switches that decide how much of perception is live |

> **Note:** Both sensors were complete from the start of the project and never
> called once. The missing input was `NPCBlackboard.Facing` — nothing tracked
> which way anyone was pointing. With that in place they run every tick, but
> **in shadow mode**: they fill `KnownEntities` and report coverage, while combat
> still selects targets by proximity. See `PerceptionSystem` for why the swap is
> staged rather than flipped.

### Social

| File | Class | Description |
|---|---|---|
| [`BetrayalEvaluator.cs`](file:///home/alvaromendes/Documents/project01/src/AI/Social/BetrayalEvaluator.cs) | `BetrayalEvaluator` | Evaluates desperate stalkers for squadmate betrayal; executes witness checks |
| [`CampfireSmartObject.cs`](file:///home/alvaromendes/Documents/project01/src/AI/Social/CampfireSmartObject.cs) | `CampfireSmartObject` | Social gathering node with seats, drink sharing, guitar playing, and combat dispersal |
| [`CampfireRegistry.cs`](file:///home/alvaromendes/Documents/project01/src/AI/Social/CampfireRegistry.cs) | `CampfireRegistry` | Holds the generated campfires and answers proximity queries. Generated at startup: one per macro base plus a fraction of micro-shelters |
| [`CampfireOptions.cs`](file:///home/alvaromendes/Documents/project01/src/AI/Social/CampfireOptions.cs) | `CampfireOptions` | Seats, proximity radius, morale-aura radius, social cooldown. `STALKER_CAMPFIRE_*` overrides. The aura radius must cover the proximity radius — nothing moves a stalker to the fire, so a smaller aura reaches nobody |
| [`SquadMoraleOptions.cs`](file:///home/alvaromendes/Documents/project01/src/AI/Social/SquadMoraleOptions.cs) | `SquadMoraleOptions`, `SquadMoraleEvent` | Leader-coupling half-life, coupling radius, mission share, squadmate-loss penalty. `STALKER_SQUAD_*` overrides |

### Squads

| File | Class | Description |
|---|---|---|
| [`Squad.cs`](file:///home/alvaromendes/Documents/project01/src/AI/Squads/Squad.cs) | `Squad`, `SquadBlackboard` | Leader-follower groups with shared target memory and waypoint propagation |
| [`SquadOrders.cs`](file:///home/alvaromendes/Documents/project01/src/AI/Squads/SquadOrders.cs) | `SquadOrder` enum | Tactical directives: FreeRoam, MoveTo, HoldPosition, AttackTarget, Retreat, Escort, SearchArea, MakeCamp |
| [`SquadSuccession.cs`](file:///home/alvaromendes/Documents/project01/src/AI/Squads/SquadSuccession.cs) | `SquadSuccession` | Leader death handling — promotion, merge into nearby squad, or disband |
| [`ScientistEscortMission.cs`](file:///home/alvaromendes/Documents/project01/src/AI/Squads/ScientistEscortMission.cs) | `ScientistEscortMission` | Escort scenario with periodic acoustic pulses attracting mutants |

### Blackboards

| File | Class | Description |
|---|---|---|
| [`NPCBlackboard.cs`](file:///home/alvaromendes/Documents/project01/src/AI/Blackboards/NPCBlackboard.cs) | `NPCBlackboard` | Per-entity memory: position, A* path, navigation state, combat state, threat memory, GOAP world-state flags, suspicion level |

---

## src/Systems/ — Gameplay Systems

| File | Class | Description |
|---|---|---|
| [`CombatBalanceConfig.cs`](file:///home/alvaromendes/Documents/project01/src/Systems/CombatBalanceConfig.cs) | `CombatBalanceConfig` | Static config for encounter rates, win chance bounds, sniper range (130m), squad ally bonus, weapon class modifiers |
| [`CombatResolver.cs`](file:///home/alvaromendes/Documents/project01/src/Systems/CombatResolver.cs) | `CombatResolver` | Computes combat outcomes from rank, weapon stats, armor protection, sniper bonuses, heavy suppression, and jamming |
| [`CorpseCleanupService.cs`](file:///home/alvaromendes/Documents/project01/src/Systems/CorpseCleanupService.cs) | `CorpseCleanupService` | Configurable despawn timers: 45min idle, 12min post-interact, 5min post-feeding |
| [`EquipmentUpgradeService.cs`](file:///home/alvaromendes/Documents/project01/src/Systems/EquipmentUpgradeService.cs) | `EquipmentUpgradeService` | Generates corpse gear snapshots, executes loot stripping, and drives trader gear purchases |
| [`GearEvaluator.cs`](file:///home/alvaromendes/Documents/project01/src/Systems/GearEvaluator.cs) | `GearEvaluator` | Scores weapons (DPS formula) and armor (protection aggregate) for upgrade comparison |
| [`KillTracker.cs`](file:///home/alvaromendes/Documents/project01/src/Systems/KillTracker.cs) | `KillTracker` | Thread-safe circular buffer (500 events) recording all casualties by category. Also the single point every stalker death passes through, so it publishes the squad grief pulse |
| [`KillTrackerOptions.cs`](file:///home/alvaromendes/Documents/project01/src/Systems/KillTrackerOptions.cs) | `KillTrackerOptions` | Immutable config record, installed via `KillTracker.Configure` |
| [`CorpseCleanupOptions.cs`](file:///home/alvaromendes/Documents/project01/src/Systems/CorpseCleanupOptions.cs) | `CorpseCleanupOptions` | Despawn thresholds in game seconds; `STALKER_CORPSE_*` overrides |
| [`LeaderboardSerializer.cs`](file:///home/alvaromendes/Documents/project01/src/Systems/LeaderboardSerializer.cs) | `LeaderboardSerializer` | Builds the Top 100 by XP/kills for the snapshot and API, and writes the final standings beside the run log at shutdown |
| [`ProtectionProfile.cs`](file:///home/alvaromendes/Documents/project01/src/Systems/ProtectionProfile.cs) | `ProtectionProfile` | Computes composite 9-channel defense by summing armor + helmet + belt items |
| [`RankSystem.cs`](file:///home/alvaromendes/Documents/project01/src/Systems/RankSystem.cs) | `RankSystem` | Awards XP on kills with rank-delta multipliers; triggers promotions across 8 tiers |
| [`ScientistForecaster.cs`](file:///home/alvaromendes/Documents/project01/src/Systems/ScientistForecaster.cs) | `ScientistForecaster` | Multi-stage emission PDA warnings (1-3hr, 30min, 15min before impact) |
| [`SimulationDebugLog.cs`](file:///home/alvaromendes/Documents/project01/src/Systems/SimulationDebugLog.cs) | `SimulationDebugLog` | Metrics aggregation — lifetime/interval counters, periodic snapshots, final report (`[COMBAT]`, `[GEAR]`, `[TRADE]`, `[CORPSE]`, `[MISSION]`, ...). Decides *what* to log and formats it; delegates actual writing to `DebugLogSink` |
| [`DebugLogSink.cs`](file:///home/alvaromendes/Documents/project01/src/Systems/DebugLogSink.cs) | `DebugLogSink` | The logging mechanism itself — console + append-only log file, lock-protected. Split out of `SimulationDebugLog` so metrics aggregation and I/O are separate concerns |
| [`SkillEvaluator.cs`](file:///home/alvaromendes/Documents/project01/src/Systems/SkillEvaluator.cs) | `SkillEvaluator` | Diminishing-returns skill progression: `Delta = BaseGain × (1 - Current/100)^1.5` |

---

## src/World/ — World Generation & Navigation

### Environment

| File | Class | Description |
|---|---|---|
| [`EnvironmentManager.cs`](file:///home/alvaromendes/Documents/project01/src/World/Environment/EnvironmentManager.cs) | `EnvironmentManager` | Time-of-day sunlight curves (0.05 night → 1.0 noon), twilight and night flags |
| [`WeatherManager.cs`](file:///home/alvaromendes/Documents/project01/src/World/Environment/WeatherManager.cs) | `WeatherManager` | Dynamic weather transitions (Clear/Overcast/Rain/Fog/Storm) with visibility and acoustic modifiers |

### Generation

| File | Class | Description |
|---|---|---|
| [`StaticWorldGenerator.cs`](file:///home/alvaromendes/Documents/project01/src/World/Generation/StaticWorldGenerator.cs) | `StaticWorldGenerator` | Loads canonical world layout from `map_regions.json`; manages region coordinates, radii, and threat levels |
| [`POIPrefabStamper.cs`](file:///home/alvaromendes/Documents/project01/src/World/Generation/POIPrefabStamper.cs) | `POIPrefabStamper` | Stamps macro bases, micro-shelters, underground labs, and mutant dens; creates hatch smart objects |
| [`RoadNetwork.cs`](file:///home/alvaromendes/Documents/project01/src/World/Generation/RoadNetwork.cs) | `RoadNetwork` | Constructs surface travel corridors and waypoints between connected regions |
| [`BuildingFootprint.cs`](file:///home/alvaromendes/Documents/project01/src/World/Generation/BuildingFootprint.cs) | `BuildingFootprint` | Data model for axis-aligned building rectangles (center, dimensions, door, threat, interior flag) |
| [`BuildingFootprintLoader.cs`](file:///home/alvaromendes/Documents/project01/src/World/Generation/BuildingFootprintLoader.cs) | `BuildingFootprintLoader` | Loads ~759 building footprint rects from JSON for pathfinding blockers |
| [`MinorPOI.cs`](file:///home/alvaromendes/Documents/project01/src/World/Generation/MinorPOI.cs) | `MinorPOI` | Model for sub-POIs (campfires, cellars, stashes, outposts) loaded from `minor_pois.json` |
| [`MinorPOILoader.cs`](file:///home/alvaromendes/Documents/project01/src/World/Generation/MinorPOILoader.cs) | `MinorPOILoader` | Deserializes minor POIs from JSON |
| [`WorldPOIBase.cs`](file:///home/alvaromendes/Documents/project01/src/World/Generation/WorldPOIBase.cs) | `WorldPOIBase` | Abstract base for stamped POIs (ID, name, position, threat, type, owner faction) |
| [`ZoneTopology.cs`](file:///home/alvaromendes/Documents/project01/src/World/Generation/ZoneTopology.cs) | `ZoneTopology` | Builds the global navigation graph linking regions and underground transitions |
| [`ZoneWorldGenerator.cs`](file:///home/alvaromendes/Documents/project01/src/World/Generation/ZoneWorldGenerator.cs) | `ZoneWorldGenerator` | Procedural south-to-north latitude-based threat generator with noise variance |

### Hazards

| File | Class | Description |
|---|---|---|
| [`AnomalyField.cs`](file:///home/alvaromendes/Documents/project01/src/World/Hazards/AnomalyField.cs) | `AnomalyField` | Hazard volume dealing elemental/psi damage and spawning artifacts post-emission |
| [`AnomalySeeder.cs`](file:///home/alvaromendes/Documents/project01/src/World/Hazards/AnomalySeeder.cs) | `AnomalySeeder` | Seeds 39 real-world Chernobyl anomaly clusters plus dynamic wilderness fields |
| [`EmissionSystem.cs`](file:///home/alvaromendes/Documents/project01/src/World/Hazards/EmissionSystem.cs) | `EmissionSystem` | 4-phase blowout lifecycle (Dormant → Warning → Panic → Peak → Aftermath) with anomaly reshuffling |
| [`EmissionOptions.cs`](file:///home/alvaromendes/Documents/project01/src/World/Hazards/EmissionOptions.cs) | `EmissionOptions` | Immutable emission timings in game seconds. Defaults are GAMMA-style 12–24 game hours between blowouts; `FromEnvironment()` reads `STALKER_EMISSION_MIN_SEC`, `_MAX_SEC`, `_WARNING_SEC`, `_PANIC_SEC`, `_PEAK_SEC`, `_AFTERMATH_SEC` |

### Navigation

| File | Class | Description |
|---|---|---|
| [`ZonePathfinder.cs`](file:///home/alvaromendes/Documents/project01/src/World/Navigation/ZonePathfinder.cs) | `ZonePathfinder` | High-resolution 2D/3D grid A* (40m cells) with road preference, anomaly avoidance, building blockers, and Layer +1 interior support |
| [`HierarchicalNav.cs`](file:///home/alvaromendes/Documents/project01/src/World/Navigation/HierarchicalNav.cs) | `HierarchicalNav` | Two-tier coarse region graph A* (exists; sim primarily uses `ZonePathfinder`) |
| [`SmartObject.cs`](file:///home/alvaromendes/Documents/project01/src/World/Navigation/SmartObject.cs) | `SmartObject` | Interactive world points (campfires, beds, hatches, stash caches, shop counters) with occupancy tracking |

### POI

| File | Class | Description |
|---|---|---|
| [`POIRegistry.cs`](file:///home/alvaromendes/Documents/project01/src/World/POI/POIRegistry.cs) | `POIRegistry` | Queryable index of all stamped POIs categorized by region, gameplay type, and loot |
| [`LootTableResolver.cs`](file:///home/alvaromendes/Documents/project01/src/World/POI/LootTableResolver.cs) | `LootTableResolver` | Resolves POI loot categories (ammo, bread, vodka, scrap, artifacts) into inventory items |
| [`MicroShelter.cs`](file:///home/alvaromendes/Documents/project01/src/World/POI/MicroShelter.cs) | `MicroShelter` | Small emission shelter with limited occupant capacity |
| [`SmartTerrainNode.cs`](file:///home/alvaromendes/Documents/project01/src/World/POI/SmartTerrainNode.cs) | `SmartTerrainNode` | Outpost settlements with faction ownership and population caps |

---

## src/Entities/ — Data Models

### Characters

| File | Class | Description |
|---|---|---|
| [`Stalker.cs`](file:///home/alvaromendes/Documents/project01/src/Entities/Characters/Stalker.cs) | `Stalker` | Main composite NPC root aggregating blackboard, needs, equipment, rank, skills, belt, and GOAP runtime |
| [`StalkerAttributes.cs`](file:///home/alvaromendes/Documents/project01/src/Entities/Characters/StalkerAttributes.cs) | `StalkerAttributes` | 4-skill RPG matrix: Marksmanship, ZoneSurvival, Charisma, Trustworthiness (0–100) |
| [`StalkerSpawnHelper.cs`](file:///home/alvaromendes/Documents/project01/src/Entities/Characters/StalkerSpawnHelper.cs) | `StalkerSpawnHelper` | Seeds initial skills and 45s combat grace period for freshly spawned stalkers |
| [`Corpse.cs`](file:///home/alvaromendes/Documents/project01/src/Entities/Characters/Corpse.cs) | `Corpse` | Dead entity tracking cause of death, patch intactness, inspection status, and gear snapshot |
| [`CorpseGearSnapshot.cs`](file:///home/alvaromendes/Documents/project01/src/Entities/Characters/CorpseGearSnapshot.cs) | `CorpseGearSnapshot` | DTO storing weapon, armor, helmet IDs and conditions from a dead stalker |
| [`CorpseRegistry.cs`](file:///home/alvaromendes/Documents/project01/src/Entities/Characters/CorpseRegistry.cs) | `CorpseRegistry` | Thread-safe registry with proximity queries and age-based purging |
| [`RankProgression.cs`](file:///home/alvaromendes/Documents/project01/src/Entities/Characters/RankProgression.cs) | `RankProgression` | XP tracking and rank progression across 8 G.A.M.M.A. tiers (Rookie → Legend) |

### Equipment

| File | Class | Description |
|---|---|---|
| [`ItemDatabase.cs`](file:///home/alvaromendes/Documents/project01/src/Entities/Equipment/ItemDatabase.cs) | `ItemDatabase` | Central loader for weapons, armors, helmets, consumables, ammo, and scrap from JSON catalogs |
| [`ItemRegistry.cs`](file:///home/alvaromendes/Documents/project01/src/Entities/Equipment/ItemRegistry.cs) | `ItemRegistry` | In-memory store for item definitions with dynamic lookup |
| [`ItemFactory.cs`](file:///home/alvaromendes/Documents/project01/src/Entities/Equipment/ItemFactory.cs) | `ItemFactory` | Creates concrete item instances by ID with condition and parameters |
| [`SpawnLoadoutResolver.cs`](file:///home/alvaromendes/Documents/project01/src/Entities/Equipment/SpawnLoadoutResolver.cs) | `SpawnLoadoutResolver` | Resolves starting gear by faction, rank tier, and role |
| [`WeaponItem.cs`](file:///home/alvaromendes/Documents/project01/src/Entities/Equipment/WeaponItem.cs) | `WeaponItem` | Firearm model: class, condition, damage, accuracy, fire rate, mag size, ammo count |
| [`ArmorItem.cs`](file:///home/alvaromendes/Documents/project01/src/Entities/Equipment/ArmorItem.cs) | `ArmorItem` | Body armor with condition degradation, faction patch, and protection stats |
| [`HelmetItem.cs`](file:///home/alvaromendes/Documents/project01/src/Entities/Equipment/HelmetItem.cs) | `HelmetItem` | Headwear with ballistic, slash, and environmental protection |
| [`EquipmentContainer.cs`](file:///home/alvaromendes/Documents/project01/src/Entities/Equipment/EquipmentContainer.cs) | `EquipmentContainer` | Paperdoll holding primary/secondary weapons, armor, helmet, detector, and backpack |
| [`BeltSlot.cs`](file:///home/alvaromendes/Documents/project01/src/Entities/Equipment/BeltSlot.cs) | `BeltSlot` | 1–6 slot container for artifacts, armor plates, and mutant pelts |
| [`DetectorItem.cs`](file:///home/alvaromendes/Documents/project01/src/Entities/Equipment/DetectorItem.cs) | `DetectorItem` | Artifact detector across 4 tiers (Echo, Bear, Veles, SVA) with range and risk stats |
| [`GammaItemCatalog.cs`](file:///home/alvaromendes/Documents/project01/src/Entities/Equipment/GammaItemCatalog.cs) | `GammaItemCatalog` | Catalog of raw Anomaly G.A.M.M.A. outfit, helmet, attachment, and weapon definitions |
| [`GammaProtectionLoader.cs`](file:///home/alvaromendes/Documents/project01/src/Entities/Equipment/GammaProtectionLoader.cs) | `GammaProtectionLoader` | Parser translating G.A.M.M.A. JSON stat tables into normalized `ProtectionStats` |
| [`ProtectionStats.cs`](file:///home/alvaromendes/Documents/project01/src/Entities/Equipment/ProtectionStats.cs) | `ProtectionStats` | Immutable record for 9 protection channels (bullet, slash, rad, burn, shock, chemical, psi, strike, explosion) |

### Mutants

| File | Class | Description |
|---|---|---|
| [`Mutant.cs`](file:///home/alvaromendes/Documents/project01/src/Entities/Mutants/Mutant.cs) | `Mutant` | Wild creature with species stats, hunger decay, hunting state, and corpse feeding |
| [`MutantSpec.cs`](file:///home/alvaromendes/Documents/project01/src/Entities/Mutants/MutantSpec.cs) | `MutantSpec` | Data definition per species: base health, damage, speed, diet, description |
| [`MutantEcologyManager.cs`](file:///home/alvaromendes/Documents/project01/src/Entities/Mutants/MutantEcologyManager.cs) | `MutantEcologyManager` | Manages 17 species, threat-weighted spawn rolls, and diurnal sleep schedules |
| [`DietType.cs`](file:///home/alvaromendes/Documents/project01/src/Entities/Mutants/DietType.cs) | `DietType` enum | Herbivore, Carnivore, Scavenger, Omnivore |
| [`MutantDamageKind.cs`](file:///home/alvaromendes/Documents/project01/src/Entities/Mutants/MutantDamageKind.cs) | `MutantDamageKind` enum | Slash, Bite, Bullet, Psi, Impact |

### Needs

| File | Class | Description |
|---|---|---|
| [`SurvivalNeeds.cs`](file:///home/alvaromendes/Documents/project01/src/Entities/Needs/SurvivalNeeds.cs) | `SurvivalNeeds` | Ticked at 1 Hz — tracks Hunger, Thirst, Radiation, Fatigue, Morale and Ammo (0–100 scales), plus Rubles |

---

## src/Economy/ — Trade & Missions

| File | Class | Description |
|---|---|---|
| [`TraderRegistry.cs`](file:///home/alvaromendes/Documents/project01/src/Economy/TraderRegistry.cs) | `TraderRegistry` | Bootstraps all macro-base trader locations and initial inventories |
| [`TraderComponent.cs`](file:///home/alvaromendes/Documents/project01/src/Economy/TraderComponent.cs) | `TraderComponent` | Shop inventory management, artifact buying, and faction-discounted pricing |
| [`TraderEconomyConfig.cs`](file:///home/alvaromendes/Documents/project01/src/Economy/TraderEconomyConfig.cs) | `TraderEconomyConfig` | Tunable params: 850 RU starting balance, 120 RU buy reserve, regional price ceilings |
| [`TradeService.cs`](file:///home/alvaromendes/Documents/project01/src/Economy/TradeService.cs) | `TradeService` | Full shopping logic — sell artifacts/loot, buy ammo/food/medkits, gear upgrades via `EquipmentUpgradeService` |
| [`MarketPrices.cs`](file:///home/alvaromendes/Documents/project01/src/Economy/MarketPrices.cs) | `MarketPrices` | Dynamic supply/demand pricing with latitude multipliers |
| [`MissionRegistry.cs`](file:///home/alvaromendes/Documents/project01/src/Economy/MissionRegistry.cs) | `MissionRegistry` | Generates, stores, and refreshes base contracts (scout, retrieve, escort) |
| [`MissionTypes.cs`](file:///home/alvaromendes/Documents/project01/src/Economy/MissionTypes.cs) | `MissionType` enum, `MissionOffer`, `StalkerMission` | Contract data models |

---

## src/Factions/ — Faction System

| File | Class | Description |
|---|---|---|
| [`FactionMatrix.cs`](file:///home/alvaromendes/Documents/project01/src/Factions/FactionMatrix.cs) | `FactionMatrix` | 12×12 diplomatic relations (Allied/Friendly/Neutral/Hostile/War) from `faction_matrix.json` |
| [`FactionSpawnTable.cs`](file:///home/alvaromendes/Documents/project01/src/Factions/FactionSpawnTable.cs) | `FactionSpawnTable` | Primary and secondary faction spawn mixes per region |
| [`DemographicsEngine.cs`](file:///home/alvaromendes/Documents/project01/src/Factions/DemographicsEngine.cs) | `DemographicsEngine` | Cultural backgrounds (Ukrainian, Russian, CIS, Western) and starting rank distributions |
| [`DisguiseSystem.cs`](file:///home/alvaromendes/Documents/project01/src/Factions/DisguiseSystem.cs) | `DisguiseSystem` | Suspicion accumulation based on rank, distance, night, armor patch, and accent |
| [`NameGenerator.cs`](file:///home/alvaromendes/Documents/project01/src/Factions/NameGenerator.cs) | `NameGenerator` | Lore-accurate name, surname, callsign, and alias generation |
| [`PersonalMemory.cs`](file:///home/alvaromendes/Documents/project01/src/Factions/PersonalMemory.cs) | `PersonalMemory` | Per-NPC individual opinion modifiers overriding faction diplomacy |

---

## src/Crafting/ — Field Crafting

| File | Class | Description |
|---|---|---|
| [`FieldCraftingSystem.cs`](file:///home/alvaromendes/Documents/project01/src/Crafting/FieldCraftingSystem.cs) | `FieldCraftingSystem` | Campfire field upgrades: scopes, extended mags, kevlar weave, rad lining using scrap parts |
| [`MutantCookingSystem.cs`](file:///home/alvaromendes/Documents/project01/src/Crafting/MutantCookingSystem.cs) | `MutantCookingSystem` | Campfire mutant cooking: radiation purging with vodka, nutrition/stamina/carry-weight buffs |

---

## src/PDA/ — Communication Network

| File | Class | Description |
|---|---|---|
| [`PDAMessage.cs`](file:///home/alvaromendes/Documents/project01/src/PDA/PDAMessage.cs) | `PDAMessage` | Data structure for news, death reports, blowout warnings, bounties, and chatter |
| [`PDANetwork.cs`](file:///home/alvaromendes/Documents/project01/src/PDA/PDANetwork.cs) | `PDANetwork` | Subscribes to `EventBus` events, formats templates with cultural slang, maintains the feed, and broadcasts rumors |
| [`TaskManager.cs`](file:///home/alvaromendes/Documents/project01/src/PDA/TaskManager.cs) | `TaskManager` | Auto-posts emergent supply/bounty contracts based on stalker critical needs |

---


## src/Web/ — WebSocket & Telemetry

| File | Class | Description |
|---|---|---|
| [`WebVisualizerServer.cs`](file:///home/alvaromendes/Documents/project01/src/Web/WebVisualizerServer.cs) | `WebVisualizerServer` | Connected-client hub broadcasting `TelemetryFrame` at 10 Hz and handling `inspect`/command requests. Owns no listener or port itself — sockets are accepted by ASP.NET Core's WebSocket middleware at `/ws` (mapped in `WebApiEndpoints`) and handed to `HandleConnectionAsync`, so telemetry shares the same Kestrel host/port as the REST API instead of a separate `HttpListener` server |
| [`TelemetryDTOs.cs`](file:///home/alvaromendes/Documents/project01/src/Web/TelemetryDTOs.cs) | Multiple DTOs | `TelemetryFrame`, `EntityDTO`, `InspectorDTO`, `CorpseDTO`, `MapDTO`, `MissionDTO`, and more |
| [`TelemetryMapper.cs`](file:///home/alvaromendes/Documents/project01/src/Web/TelemetryMapper.cs) | `TelemetryMapper` | Maps live entity objects to lightweight telemetry DTOs for serialization |
| [`InspectorBuilder.cs`](file:///home/alvaromendes/Documents/project01/src/Web/InspectorBuilder.cs) | `InspectorBuilder` | Builds rich `InspectorDTO` payloads for stalkers, mutants, corpses, and POIs |
| [`WebApiEndpoints.cs`](file:///home/alvaromendes/Documents/project01/src/Web/WebApiEndpoints.cs) | `WebApiEndpoints` | `MapSimulationApi` extension mapping the REST endpoints (`/api/world`, `/api/state`, `/api/leaderboard`, `/api/factions`, `/`) onto the web host |

---

## visualizer/ — Browser Dashboard

### [`app.js`](file:///home/alvaromendes/Documents/project01/visualizer/app.js) (1,000 lines)

PixiJS v7 + pixi-viewport rendering engine with:

- **Multi-tier LOD**: LOD_RADAR (0.15) → LOD_ICON (0.50) → LOD_SPRITE (1.20) → LOD_PAPERDOLL (2.50) with ROOF_CUTAWAY threshold at 1.0
- **Render layers**: Wilderness terrain, building footprints, roads, POIs, roofs, anomaly fields, radiation zones, corpses, mission lines, squads, entities, labels, and blowout storm overlays
- **WebSocket handling**: Live entity interpolation, sight cone rendering, and target path vectors
- **Interaction**: Click-to-inspect sending `{ type: "inspect", id: ... }` for detailed payloads
- **Follow mode**: Camera tracking from leaderboard or map click

### [`index.html`](file:///home/alvaromendes/Documents/project01/visualizer/index.html) (1,667 lines)

Full S.T.A.L.K.E.R.-themed dashboard with:

- **Top bar**: Game clock, weather, emission countdown, population counters, layer/time controls
- **Inspector tab**: Health/Hunger/Thirst/Rads/Fatigue/Morale meters, 4-skill bars, paperdoll slots with condition, active GOAP goal, contract brief, corpse loot panel
- **PDA tab**: Real-time event ticker with category filtering and Missions sub-tab
- **Leaderboard tab**: Top 100 sortable by Rank, XP, Kills, Faction
- **Factions tab**: Interactive 12×12 diplomacy matrix
- **Economy tab**: Trader stock listings

### `assets/`
14 procedural 32×32 PNG sprites for stalker factions, mutant types, corpses, and paperdoll gear overlays.

---

## data/ — JSON Data Tables

### Root Data Files

| File | Size | Description |
|---|---|---|
| `map_regions.json` | 21 KB | 28 surface + 8 underground regions with coordinates, threat levels, and connections |
| `factions.json` | 4.8 KB | 12 factions with demographic weights and rank curves |
| `faction_matrix.json` | 3.6 KB | 12×12 diplomatic relation states |
| `faction_loadouts.json` | 4 KB | Starting weapons/armor/supplies by faction and rank tier |
| `spawn_factions.json` | 4 KB | Per-region primary and secondary faction mixes |
| `minor_pois.json` | 32 KB | 103 hand-authored sub-POIs (campfires, stashes, cellars, outposts) |
| `building_footprints.json` | 250 KB | ~759 building rectangles for pathfinding and rendering |
| `mutants.json` | 2.2 KB | 17 species: health, damage, speed, diet, description |
| `names.json` | 1 KB | Cultural first names, surnames, and callsigns |
| `slang.json` | 0.6 KB | Greeting/alert phrases by cultural background |
| `pda_chatter_templates.json` | 3.1 KB | 9 template categories for PDA message formatting |

### `data/gamma/`
Raw Anomaly G.A.M.M.A. modpack data:

| File | Size | Content |
|---|---|---|
| `outfits.json` | 218 KB | 161 outfit protection definitions |
| `artefacts.json` | 148 KB | Artifact stats and effects |
| `belt-attachments.json` | 60 KB | Belt attachment modifiers |
| `helmets.json` | 20 KB | 21 helmet protection definitions |
| `weapons.json` | 3.3 KB | Weapon stat references |

### `data/items/`
Runtime item catalogs loaded by `ItemDatabase.cs`:

| File | Content |
|---|---|
| `weapons.json` | 30 weapons with full stats (damage, accuracy, fireRate, magSize, baseValue) |
| `armors.json` | Static armor definitions |
| `helmets.json` | Helmet definitions |
| `gamma_armors.json` | G.A.M.M.A. outfit imports (from `import_gamma_items.py`) |
| `gamma_helmets.json` | G.A.M.M.A. helmet imports |
| `artifacts_and_detectors.json` | Artifacts and 4-tier detectors |
| `consumables.json` | Food, drink, and medical items |
| `ammo.json` | Ammunition across 12 calibres |
| `belt_plates.json` | Ballistic plate inserts |
| `mutant_parts.json` | Harvested mutant components |
| `scrap.json` | Salvageable repair materials |

---

## scripts/ — Python Utilities

| File | Description |
|---|---|
| [`generate_item_db.py`](file:///home/alvaromendes/Documents/project01/scripts/generate_item_db.py) | Generates base item JSON files for all categories |
| [`generate_placeholder_sprites.py`](file:///home/alvaromendes/Documents/project01/scripts/generate_placeholder_sprites.py) | PIL script creating 32×32 pixel-art sprites for factions, mutants, corpses, paperdoll |
| [`import_gamma_items.py`](file:///home/alvaromendes/Documents/project01/scripts/import_gamma_items.py) | Exports G.A.M.M.A. armor/helmet definitions to sim-ready JSON |
| [`process_sprites.py`](file:///home/alvaromendes/Documents/project01/scripts/process_sprites.py) | Uses `rembg` + Pillow for background removal and sprite resizing |
| [`slice_icons.py`](file:///home/alvaromendes/Documents/project01/scripts/slice_icons.py) | Slices sprite sheets into individual UI tile icons |

---

## tests/ — Test Suite

Located in `tests/StalkerALifeSandbox.Tests/` (xUnit, **279 tests, 41.8% line coverage**, CI floor 38). Test parallelisation is disabled — several suites touch the static `EventBus`. `TestWorld.cs` builds one shared generated world, since world gen plus POI stamping is far too slow to repeat per test.

Several of these are **characterization** tests: they pin behaviour that was found to be wrong, so the fix is provably a fix rather than a hope. Where a comment says a test was inverted, it originally asserted the bug.

| File | Coverage |
|---|---|
| `ArchitectureTests.cs` | Verifies `SimulationContext` initialization and `ISimulationSystem` wiring |
| `CombatResolverTests.cs` | `CombatResolver` win-chance bounds, rank/armament/threat monotonicity, sniper range, squad-ally bonus |
| `CorpseCleanupServiceTests.cs` | Idle/eaten/interacted/mutant despawn thresholds |
| `CraftingAndCookingTests.cs` | `MutantCookingSystem` hunger reduction, vodka radiation purge, `FieldCraftingSystem` repairs, belt slot insertion |
| `EventBusTests.cs` | Event subscription, publication, and unsubscription on the decoupled `EventBus` |
| `FactionMatrixTests.cs` | `FactionMatrix` relation symmetry, Neutral fallback, hostile/friendly thresholds, `IndexOf` |
| `GOAPPlannerTests.cs` | Goal selection by utility, action chaining, cheapest-path preference, empty/null-plan edge cases |
| `ItemDatabaseTests.cs` | `ItemRegistry` registration and `ItemFactory` instantiation |
| `KillTrackerTests.cs` | Kill recording (stalker/mutant/unrecognized killers), cause override, recent-kills log |
| `MarketPricesTests.cs` | Latitude/supply multipliers, supply clamping, price formula |
| `NPCBlackboardTests.cs` | Path/waypoint state machine, sighting memory + pruning, `Reset`, navigation status text |
| `PDANetworkTests.cs` | Feed append + size cap, band/latitude fallbacks without a bound world |
| `POIRegistryTests.cs` | Name- and field-based POI classification, loot availability, patrol/loot/rest target picking |
| `RankProgressionTests.cs` | XP thresholds, monotonic rank, XP floor, kill/mission accounting |
| `SurvivalNeedsTests.cs` | Need decay over time, feeding, critical-state threshold, ammo consumption |
| `CampfireGatingCharacterizationTests.cs` | Pins the five behaviours gated on `IsAtCampfire` before that flag was widened |
| `CampfireRegistryTests.cs`, `CampfireSmartObjectTests.cs`, `CampfireMoraleIntegrationTests.cs` | Placement and proximity; seats, drink/guitar, combat snap; the publish → buffer → apply morale chain |
| `EmissionOptionsTests.cs`, `EmissionTickSystemTests.cs` | Cadence config and env overrides; shelter saves, Zombified/Monolith exemption, dormant zone harmless |
| `GoalSocialiseTests.cs` | Relevance gates, utility shape, the cooldown end to end, and the crossover decisions against `GoalAcceptMission` |
| `GoapActionCachingTests.cs` | Cached precondition/effect sets match what each action declares and stay constant |
| `GoapSharedActionStateTests.cs` | Regression guard: one stalker's `Enter` must not reach into another's travel state |
| `MissionLoopRegressionTests.cs` | Movement cannot overshoot at any TimeFactor; `TurnInMission` stays plannable at a distance but refuses to pay out there |
| `MissionTargetReachabilityTests.cs` | Every mission target is reachable from its issuer, and the pool is reproducible |
| `MoraleSinkTests.cs`, `SquadMoraleTests.cs` | Grief and combat stress; leader coupling, the shared mission pulse, and the coupling half-life |
| `MutantMovementTests.cs` | Distance per game second is identical at TimeFactor 3 and 150 |
| `SimulationSettingsTests.cs`, `SimulationSnapshotTests.cs` | Env overrides and scoped CORS; the snapshot carries copied values, not live references |
| `SquadSuccessionTests.cs` | Promote / merge / disband rules, so the O(n²) fix is provably equivalent |
| `StalkerHealthTests.cs` | Damage accumulates across exchanges instead of killing outright; armour never grants immunity |
| `ZoneDirectorTests.cs` | Bucket frequencies, game-delta vs real-delta, and the tick/game-time lockstep the measurement harness depends on |
| `TelemetryMapperTests.cs` | Mission/corpse DTO mapping and despawn-remaining computation |
| `TradeServiceTests.cs` | Buy/sell gating on rubles, purchased-item effects, trade-visit summaries |
| `TraderComponentTests.cs` | Dynamic pricing, faction price modifiers, stock/rubles mutation on buy/sell |
| `TraderRegistryTests.cs` | `Bootstrap` stock seeding by base name/faction/band, nearest/by-name lookup |
| `ZoneGateEvaluatorTests.cs` | Comfort-threat monotonicity, rank-gating, `MinRankForThreat` |
| `ZonePathfinderTests.cs` | Grid dimensions and A* `FindPath` between open points |
| `TestParallelization.cs` | Disables xUnit cross-collection parallelization while process-global static state remains (see roadmap Phase 4) |

Run tests with:
```bash
dotnet test tests/StalkerALifeSandbox.Tests/
```

### Continuous Integration

[`.github/workflows/ci.yml`](file:///home/alvaromendes/Documents/project01/.github/workflows/ci.yml) runs on every push and pull request to `main`: restore, build the solution in Release with warnings treated as errors (`-warnaserror`), then run the test suite under a **coverage gate** (coverlet.msbuild) that fails the job if total line coverage drops below the floor (currently 19%, just under the measured ~20.4%; raise it as coverage grows). Formatting and style conventions live in [`.editorconfig`](file:///home/alvaromendes/Documents/project01/.editorconfig); generated build output and logs are excluded via `.gitignore`.

---
---

## Engineering Notes

Consolidated from the former `IMPLEMENTATION_PLAN.md` and `next-phase.md`. This
section is the part of those documents worth carrying forward: the invariants,
the recurring failure modes, and the numbers behind the current tuning. Blow-by-blow
phase checklists were dropped — git history holds them.

### Two failure modes that keep recurring

**1. Data exists and nothing reads it.** Found *six* times, in code that looked
complete and compiled cleanly:

| What | Symptom until wired |
|---|---|
| `Mutant.Speed` | All three movement sites used per-tick constants ignoring `gameDelta`; mutants moved at `12 / TimeFactor` against a stalker's flat 4 — 2% speed at TimeFactor 150 |
| `Mutant.Health` / `WeaponItem.Damage` | Combat was one roll and the loser died outright; 100% lethality per encounter |
| `CurrentTargetId` / `CombatState` | Declared from the start, never set; each exchange re-picked an opponent with `FirstOrDefault`, spreading damage thin |
| `VisionCone` / `AcousticSensor` | Never called once. The missing input was `NPCBlackboard.Facing` — nothing tracked which way anyone pointed |
| `Stalker.IsWounded` | No consumer until the healing economy |
| `PersonalMemory`, `TaskManager` | Still unwired — see [Unwired code](#unwired-code) |

When adding a field, check that something *consumes* it in the same change. A
property with no reader is indistinguishable from a working feature at compile
time and from a tuning problem at runtime.

**2. A cheap filter sitting behind an expensive one.** Found three times, each
worth a large multiple:

| Site | The ordering mistake | Gain |
|---|---|---|
| `GOAPPlanner.BuildPlan` | Ran `action.IsValid(bb)` (resolves a destination — a scan over every POI) before checking whether the action's effects could serve the goal at all (two dictionary probes) | **5×** overall tick; `BuildPlan` 2.50 → 0.04 ms |
| `MissionRegistry.FindNearestIssuerWithOffer` | Tested eligibility (a walk over every offer a site holds) before distance (one subtraction and a square root) | 0.21 → 0.02 ms/call |
| `InspectorBuilder.FromStalker` | Recomputed `FindNearestIssuerWithOffer` per stalker per second — a value `GoapWorldStateSync` had already written to the blackboard that same tick | `SnapshotBuild` 63 ms → negligible |

Both filters are required and neither depends on the other, so reordering is
behaviour-preserving by construction. Ask the cheap question first.

### Invariants worth not breaking

**GOAP actions are shared singletons.** `StalkerGoapService.RegisterActions`
creates ONE instance of each action for the whole population. Per-execution
state on an action's own fields is therefore global. This caused a live bug in
which one stalker's `Enter` overwrote the flag another stalker's `Execute` was
reading: a finished traveller reported "not finished" forever, and one that
never got a path reported "arrived" instantly. Per-execution state belongs on
`NPCBlackboard.Action` (`GoapActionState`), reset at a single choke point in
`StalkerGoapService.Execute` before every `Enter`. Only bind-time `_ctx` is
legitimately instance state.

**`IsValid` is consulted by the planner, not just the runtime.** It must test
only what the planner cannot reason about — context availability, whether a
contract still exists. Anything another action can *achieve* belongs in
`GetPreconditions`. `ActionTurnInMission` once re-checked `IsAtMissionGiver`,
the very precondition `ActionReturnToMissionIssuer` exists to satisfy, which
made `[Return → TurnIn]` unbuildable and produced 1,457 NULL plans in one run.

**Rates must be TimeFactor-stable.** `rate × delta` is only a probability while
the product stays under 1, and the 1 Hz bucket passes `1.0 × TimeFactor` game
seconds — 150 at TimeFactor 150. Use `CombatResolver.EventChance`
(`1 − exp(−rate × delta)`). The same trap has bitten three times: mutant
movement, the betrayal roll, and a squad-morale coupling constant of `0.02`
(a 35 game-*second* half-life) that made followers close 95% of the morale gap
per tick, erasing every sink. Movement has the positional equivalent —
`CombatResolver.StepToward` clamps the step to the remaining distance, without
which a 60-unit stride oscillated forever across a 5-unit arrival tolerance and
no journey ever ended.

**A constant tuned at TimeFactor 3 means something else at 150.** All three
cases above are instances of this. Runs now default to 150.

**Single-writer threading.** The sim runs on one thread; web readers see an
immutable `SimulationSnapshot` published via `Volatile`. Snapshots must carry
copied values, never live references — pinned by `SimulationSnapshotTests`.

**Blackboard fields have owners and cadences.** `bb.CurrentPosition` is a 1 Hz
snapshot written by `Stalker.TickNeeds` and `GoapWorldStateSync`; squad follow
destinations and corpse selection read it at 10 Hz. Refreshing it from a 10 Hz
system — even to the same value — tightened squad spread by 36% and cost 5% of
the population. `LocationThreatMemory` is likewise a behavioural input, not a
scratch buffer: `GoapWorldStateSync` reads it into `HeardDangerRumor` as
`Values.Any(v => v >= 45)` across **every** key, so a key that no band lookup
matches still trips it. Threat tags must be band names (`South`, `MidZone`,
`DeepWild`, `North`), never level ids.

### Measurement

Reading the code confidently and being wrong has happened often enough to be
the default assumption. Measurement has contradicted a confident reading at
least six times: the socialising coefficient, the morale sinks erased by the
coupling constant, the entire Phase 7 performance hypothesis, and both
perception leaks. The profiler earned its place before any optimisation was
written; allocation — the instinctive target — was worth 3%, while an ordering
mistake no amount of reading found was worth 5×.

**Fixed-tick harness.** A timed run cannot be compared with another: it drops a
variable number of ticks under load, so two runs of equal wall-clock simulate
different spans of game time. `STALKER_MODE=headless` +
`STALKER_HEADLESS_TICKS=n` runs exactly *n* ticks with no timer. Dropped ticks
do **not** distort game-time-normalised metrics — `ZoneDirector` skips the clock
advance and the work together — but they do distort the report's real-time
figures (deaths per real minute, the startup/steady-state windows).

**`scripts/sim_baseline.py`** captures the report's counters and diffs them
against `baselines/default.json` with noise-band verdicts. `--capture` writes a
baseline, `--repeat n` widens the band with more runs.

Hygiene rules, each learned the hard way:

- **Never rebuild while a capture runs.** One baseline was captured across two
  binaries and flagged a phantom 7% morale regression against a
  behaviour-preserving refactor. The script now fingerprints the built DLL and
  aborts if it changes mid-capture.
- **A baseline is only a control if it was taken on the code being compared
  against.** A stale `default.json` manufactured mutant-population and
  death SIGNALs that a fresh capture on the same binary showed were simply the
  current normal. Re-capture after landing anything.
- **Kill the child, not the launcher.** `pkill -f "dotnet run"` kills the
  wrapper and leaves `bin/*/net8.0/StalkerALifeSandbox` running; one zombie ran
  4h11m and its log was picked up as the newest by the baseline script, which
  now parses the log path out of the run's own stdout.
- **State the mode.** A profiling run once silently became an unbounded server
  run for 58 minutes because `STALKER_HEADLESS_TICKS` never reached the process
  and execution fell through to the timer. `Program.Main` now prints the
  resolved mode and `STALKER_MODE=headless` without a tick count is fatal.
- **The harness resolves double-digit effects, not small ones.** Validate small
  changes with unit tests instead.
- **Watch for metrics that change meaning.** When combat became attritional the
  counters — which only fire on a kill — silently went from measuring *how much
  fighting* to *how much of it was fatal*, and the report's new wording broke
  `sim_baseline.py`'s regex into printing `?`.
- **A metric that flips sign between runs is noise.** `death_betrayal` came out
  +104% and then −47% across two A/Bs of the same change.
- **Measure past the spawn ramp.** The 720-second initial ramp is *exactly*
  7,200 ticks, so a 7,200-tick run ends where the ramp ends and
  `TrickleRespawn` has never executed. Equilibrium runs need 14,400.

### Current equilibrium

Measured at 14,400 ticks (60 game-hours), past the spawn ramp:

| | value |
|---|---|
| alive at end | **335** (peak 429) |
| spawned | 744 ramp + 770 trickle = 1,514 |
| casualties | 1,185 |
| combat | 13,484 exchanges, 1,197 fatal (**9% lethality**) |
| tick cost | **11.9 ms** against a 100 ms budget |

**The ceiling is lethality, not compute.** Respawn delivers steadily and deaths
consume it. The design's 750 target is unreachable while those two rates sit
where they are; reaching it is a spawn-versus-lethality decision, not an
optimisation task. At roughly eight times inside budget, no further performance
work is warranted at this scale.

Cumulative tick cost went 40.7 ms at 74 alive to ~12 ms at 420+ — about 18×
per-capita — across the planner reordering, the 1 Hz fixes, and the indexing
work.

### Tuning that is load-bearing

- **Emissions, 12–24 game hours** (`EmissionOptions`). They were once 600–1500
  game *seconds* — 78 a day — as a testing accommodation that became the
  default. `EmissionImminent` hard-gates `GoalCompleteMission`, `GoalSocialise`,
  `ActionFulfillMission`, `ActionReturnToMissionIssuer` and all wilderness
  travel, so ~19% of sim time was spent fleeing or forbidden from doing anything
  purposeful, against journeys of ~3 game minutes. Restoring canon cadence took
  missions completed 178 → 575 and average morale ~50 → 77.
- **Healing must cost something scarce.** A first attempt at wounded behaviour
  made the Zone non-lethal — five successive tunings moved gunfire deaths from
  209 to between 0 and 20.5, none of them restoring lethality. The diagnosis was
  that `ActionRestAtBase` is the most common action in the sim, so free healing
  outran chip damage and reducing the heal per rest just made stalkers rest
  more. Resting now heals nothing; `ActionTreatWounds` spends a purchased
  `MedkitCount` dressing. Recovery exists and lethality is unchanged (all
  metrics inside the noise band except 78 dressings used).
- **Squad delegation, not follower planning.** Followers do not run GOAP
  (`ShouldPlan` admits leaders and solos only), which made them unable to gain
  morale at all — measured at follower avg 43, max exactly 70, the spawn
  default, meaning *no follower had ever gained a point*. Letting them plan
  measured 1.8× over the 1 Hz budget and dissolves squads as the coordination
  unit `SquadSuccession`, betrayal and emission herding depend on. Instead
  `SquadNeeds.Refresh` walks the population once per 1 Hz tick and writes each
  squad's worst hunger, worst thirst, lowest morale and dry-rifle count onto the
  *leader's* blackboard, and three goals answer for the group. One O(population)
  pass against hundreds of extra A* searches — and a leader visits a trader
  *because his men are dry*.
- **Morale needs sinks.** With many sources and one weak decay it saturated at
  the ceiling — 83% of all morale gained was being discarded at the 100 cap.
  `CombatStressMorale` (−2.5 to a firefight survivor) and
  `SquadmateLossPenalty` (−9 to each living squadmate) restored variance.

### Unwired code

Verified against the tree, not inherited from the old docs — several entries
there were stale (`ConvoyManager` and `src/UI` have since been deleted; field
crafting, mutant cooking and perception are now wired).

| Item | File | State |
|---|---|---|
| Emergent task pool | `src/PDA/TaskManager.cs` | Orphan. Overlaps the live `MissionRegistry`; needs a reconciliation decision, not just wiring |
| Personal grudges | `src/Factions/PersonalMemory.cs` | Orphan. Natural payoff is stalker-level grudges ("Wolf killed my squadmate") |
| Hierarchical pathfinding | `src/World/Navigation/HierarchicalNav.cs` | Dead through `ZoneTopology`, which is itself unreferenced. The sim uses grid A* via `ZonePathfinder` |
| Scientist escort | `src/AI/Squads/ScientistEscortMission.cs` | Orphan; was blocked on `NoiseEvent`, which now exists |
| Smart terrain | `src/World/POI/SmartTerrainNode.cs` | Orphan |
| Mutant corpse feeding | `src/AI/Actions/ActionMutantFeedOnCorpse.cs` | Orphan; feeding already works via inline logic, so this only GOAP-ifies it |
| Squad object model | `src/AI/Squads/Squad.cs` (`SquadBlackboard`) | Orphan. Squads today are `SquadId` flags plus `SquadSuccession`; adopting the object model is its own refactor |


## scripts/ — Maintenance Utilities

Relocated from the repository root in Phase 0.

| File | Description |
|---|---|
| [`scripts/fix_collections.py`](scripts/fix_collections.py) | Maintenance script updating simulation collection references |
| [`scripts/patch_map.py`](scripts/patch_map.py) | Canonical coordinate patcher for `map_regions.json` |
| [`scripts/update_collections.py`](scripts/update_collections.py) | Utility for updating collection iteration patterns in system files |
| [`scripts/zone_map.html`](scripts/zone_map.html) | Standalone HTML zone map visualization (33 KB) |
