# Master Implementation Plan: S.T.A.L.K.E.R. A-Life Open-World Sandbox (v4.5 — Weapons, Gear & Interior Navigation)

> **Last audited:** 2026-09-12 (status markers re-verified against the code; five claims were stale and are corrected)  
> **Runtime entry point:** `Program.cs` → `SimulationHost.cs` → `SimulationLoop.cs` (10 Hz via `ZoneDirector`)  
> **Status legend:** `[x]` done & wired · `[~]` partial (class/data exists, not fully integrated) · `[ ]` missing or not started

---

## 1. Project Overview & Tech Stack

* **Project Name:** StalkerALifeSandbox
* **Language/Framework:** C# (.NET 8) Core Engine
* **Visualizer Frontend:** HTML5 / WebGL 2D Engine (**PixiJS v7** + `pixi-viewport`)
* **Network Protocol:** Single ASP.NET Core host on port 5050 — REST API + `/ws` WebSocket telemetry (consolidated from a separate `System.Net.WebSockets`/`HttpListener` server on port 8080)
* **Architecture:** Modular, event-driven, data-oriented components — **subsystem library is broad; live sim uses a subset**
* **Current simulation scale:** ~1500 stalkers (12 factions), ~1000 mutants (5 species spawned at runtime), 28 surface + 8 underground regions
* **Target scale (design doc):** ~750 stalkers, ~500 mutants, 17 species, 35 surface maps

**What actually runs today:** world generation, road network, grid pathfinding + **building footprint blockers + Layer +1 interior layer**, GOAP navigation (squad leaders) with **POI/stash/mission goals** (accept → fulfill → **return to issuer → turn-in payout**), probabilistic combat with **GAMMA protection + squad bonuses**, emissions + forecaster, kill/rank/**Top 100 leaderboard + follow mode**, **corpse loot + despawn ecology**, squad following + **leader succession**, economy (traders/convoys/**gear upgrades** via `GoalVisitTrader` + `TraderEconomyConfig`), **GAMMA item catalog** (161 outfits, 21 helmets, **30 weapons**), artifact harvest, PixiJS spectator dashboard with **GAMMA gear/protection/corpse inspect**, **mission overlays + stats**, **CSS inventory grid**, **Missions PDA tab**.

**What exists as library code but is not driving the sim:** field crafting, weather/light passed to mutant ecology schedule, mutant cooking GOAP, true HPA*/NavMesh.

---

## 2. Actual Directory Hierarchy (as of audit)

```text
StalkerALifeSandbox/
├── data/
│   ├── map_regions.json           # Canonical region topology (replaces zone_levels.json)
│   ├── minor_pois.json            # 103 hand-authored entries; LootTable consumed by GOAP
│   ├── building_footprints.json     # ~759 rects (generated from POI stamps)
│   ├── gamma/                     # GAMMA 0.9.5 protection data (outfits, helmets, artefacts, belt)
│   ├── mutants.json               # 17 species specs
│   ├── factions.json              # 12 factions + demographic weights
│   ├── faction_matrix.json        # 12×12 hostility table
│   ├── faction_loadouts.json      # Faction/tier spawn equipment (+ GAMMA pool merge at runtime)
│   ├── names.json, slang.json
│   ├── pda_chatter_templates.json # Loaded; 9 template categories
│   ├── leaderboard.json           # Written at runtime (Top 100)
│   └── items/                     # Item DB + generated GAMMA registries
│       ├── weapons.json, armors.json, helmets.json, artifacts_and_detectors.json
│       ├── gamma_armors.json, gamma_helmets.json  # from scripts/import_gamma_items.py
│       ├── consumables.json, ammo.json, belt_plates.json
│       ├── mutant_parts.json, scrap.json
│
├── scripts/
│   ├── process_sprites.py         # rembg + 32×32 resize (optional; needs rembg)
│   ├── generate_placeholder_sprites.py  # Procedural 32×32 faction sprites (no deps)
│   ├── import_gamma_items.py      # Export GAMMA outfits/helmets → data/items/
│   ├── slice_icons.py             # DDS icon sheet extractor
│   └── generate_item_db.py
│
├── visualizer/
│   ├── index.html                 # PixiJS dashboard + rich inspector drawer
│   ├── app.js                     # Renderer, LOD, sprites, paperdoll, corpses, missions, leaderboard, follow
│   └── assets/                    # [x] 14 procedural PNG sprites (stalkers, mutant, corpse, paperdoll)
│
├── Program.cs                     # Boot + spawn + REST API (5050)
│
└── src/
    ├── Core/
    │   ├── ZoneDirector.cs        # [x] Multi-frequency tick buckets (10/1/0.1 Hz)
    │   ├── SimulationLoop.cs      # [x] Live sim orchestrator
    │   ├── TimeManager.cs         # [x] Wired (TimeFactor = 6.0)
    │   └── EventBus.cs            # [x] Used by emissions/PDA
    │
    ├── AI/
    │   ├── GOAP/                  # [x] Planner wired via StalkerGoapService
    │   ├── Actions/               # Patrol, shelter, trade, harvest, explore lab, mission fulfill/return/turn-in, …
    │   ├── Decision/ArtifactDecisionEngine.cs  # [x] Wired in ActionHarvestArtifact
    │   ├── Perception/VisionCone.cs, AcousticSensor.cs
    │   ├── Social/BetrayalEvaluator.cs, CampfireSmartObject.cs
    │   ├── Blackboards/NPCBlackboard.cs
    │   └── Squads/Squad.cs, SquadOrders.cs
    │
    ├── Systems/
    │   ├── KillTracker.cs         # [x] Wired
    │   ├── RankSystem.cs          # [x] Wired on kills
    │   ├── CombatResolver.cs      # [x] Skill/threat/gear/squad combat
    │   ├── ProtectionProfile.cs   # [x] GAMMA aggregated protection
    │   ├── GearEvaluator.cs       # [x] Upgrade comparison for loot/trade
    │   ├── EquipmentUpgradeService.cs  # [x] Corpse loot + trader buys
    │   ├── CorpseCleanupService.cs     # [x] Timed despawn rules
    │   ├── SimulationDebugLog.cs  # [x] TASK/GOAL/GEAR/TRADE/CORPSE/MISSION snapshots
    │   ├── SkillEvaluator.cs      # [~] Multiple hooks wired; mission scout/stash/escort on turn-in
    │   ├── LeaderboardSerializer.cs
    │   └── ScientistForecaster.cs # [x] Wired
    │
    ├── World/
    │   ├── Generation/            # StaticWorldGenerator, POIPrefabStamper, RoadNetwork, …
    │   ├── Navigation/            # ZonePathfinder, HierarchicalNav, SmartObject
    │   ├── Environment/         # EnvironmentManager, WeatherManager
    │   ├── Hazards/               # EmissionSystem, AnomalyField, AnomalySeeder
    │   └── POI/                   # POIRegistry, LootTableResolver
    │
    ├── Entities/                  # Stalker, Mutant, Equipment, Needs, …
    ├── Crafting/                  # [x] FieldCraftingSystem runs at 0.1 Hz; MutantCookingSystem used by it
    ├── Factions/                  # [x] Matrix + demographics wired at spawn
    ├── Economy/                   # [x] TraderRegistry, MissionRegistry, TraderEconomyConfig, TradeService
    ├── PDA/                       # [x] Chatter + forecaster + templated death reports
    └── Web/                       # WebVisualizerServer, TelemetryDTOs, InspectorBuilder, TelemetryMapper
```

**Naming drift from v4.0 plan:**

| Plan name | Actual |
|---|---|
| `data/zone_levels.json` | `data/map_regions.json` |
| `EmissionManager.cs` | `src/World/Hazards/EmissionSystem.cs` |
| `src/Navigation/` | `src/World/Navigation/` |
| `src/Environment/` | `src/World/Environment/` |
| `InspectorDTO.cs` | `src/Web/TelemetryDTOs.cs` |
| `MacroNode.cs`, `PortalNode.cs` | Not created — portal logic inline in `ZonePathfinder` |

---

## 3. Core Engine Systems & Technical Specifications

Spec sections below describe the **design target**. See §4 for honest completion status per item.

### A. Time Engine & Scaled Survival Rhythms
* `time_factor = 3.0` (default) — configurable via `STALKER_TIME_FACTOR`. Full day ≈ 8 real hours at 3×.
* Stalker needs decay per game-hour (Hunger, Thirst, Fatigue, Radiation, Desperation). **Needs are slow** (~10 game hours to critical hunger) — the "fast" feel is **not** from need decay.
* Mutant hunger + feeding restoration + nocturnal schedule.

### B. Kill Tracking, Scaled XP & Leaderboards
* Killer categories: Stalker, Mutant, Anomaly, Environment.
* Rank delta XP formula with tier multiplier.
* Top 100 leaderboard → `data/leaderboard.json`.

### C. Organic RPG Skill Progression
* Diminishing-returns curve on Marksmanship, Zone Survival, Charisma, Trustworthiness.
* Event hooks for combat, crafting, social, treason.

### D. Emissions & Scientist Forecasting
* 4-phase blowout sequence; shelter safety; 70% death / 30% zombification.
* Scientist forecast windows → PDA; travel cancellation during surge risk.

### E. Hierarchical Pathfinding & Portals
* Tier 1 macro graph (regions, roads, portals).
* Tier 2 micro NavMesh for on-screen entities.
* Layer 0 ↔ Layer -1 portal transitions.
* **Audit finding (resolved):** patrol now uses POI registry; building footprints block pathfinding.

### F. WebGL PixiJS Spectator Visualizer
* Multi-layer rendering, LOD zoom, two-tier telemetry (broadcast + on-demand inspect).
* Paperdoll, inventory grid, squad/social vectors at micro zoom.

---

## 4. Honest Execution Checklist

### Phase 1: Core Architecture & Data Structs
- [x] `EventBus.cs` — decoupled messaging (emissions, PDA)
- [x] `SurvivalNeeds.cs` — ticked every sim step in `Program.cs`
- [x] `NPCBlackboard.cs` — path following, navigation state, override labels
- [x] `FactionMatrix.cs` — 12×12 table; used in faction combat checks

### Phase 2: Static Map Topology & Multi-Layer System
- [x] Region topology — `data/map_regions.json` (28 surface + 8 underground)
- [x] Multi-layer logic — Y-layer in `ZonePathfinder` + underground POI stamps + **Layer +1 surface interior cells (Y=10f waypoint projection)**
- [x] Level transitions — hatch portals + GOAP lab explore + **Layer +1 interior entry/exit via `ResolveLayer` (Y > 5f)**
- [x] NPC portal traversal — `ActionExploreLab` + hatch cells in `ZonePathfinder`
- [x] `HierarchicalNav.cs` — region graph A* (exists; sim uses `ZonePathfinder` instead)
- [x] Hybrid canon/ambient POIs — `minor_pois.json` (103 entries) + stamped as `MicroShelter`; **LootTable consumed via `POIRegistry` / `ActionVisitStash`**
- [x] Wilderness scatter — 450 procedural micro-shelters between regions (`ScatterWildernessMicroPOIs`)
- [x] `RoadNetwork.cs` — curved road corridors between connected macro regions; exposed via `/api/world`
- [x] Building footprints — `BuildingFootprintLoader`, ~759 rects, pathfinder blockers + visualizer rendering (P13)

### Phase 3: Demographics, RPG Skills & Belt Gear
- [x] `DemographicsEngine.cs` — wired at stalker spawn
- [x] `StalkerAttributes.cs` — 4-skill matrix rolled per rank at spawn via `StalkerSpawnHelper.ConfigureFreshSpawn()` → `RollForRank()`, on **both** spawn paths (faction leaders + inbound trickle). *(The static `GenerateForRank()` factory is unused — `RollForRank()` is the live path.)*
- [~] `BeltSlot.cs` — slot array exists; **items rarely populated at runtime**
- [~] `BetrayalEvaluator.cs` — **desperate squad betrayal wired at 1 Hz**; treason PDA alerts

### Phase 4: Full Mutant Ecology & Cooking Mechanics
- [x] `data/mutants.json` — all 17 G.A.M.M.A. species defined
- [x] `MutantEcologyManager.cs` — activity modifiers implemented; environment + weather **are** passed into `MutantBehaviourSystem` and used by `ShouldSleepInDen(species, environment, weather)`
- [x] Runtime spawn roster — **all 17 species** via `MutantEcologyManager.RollSpecies(threat)`
- [ ] `ActionMutantFeedOnCorpse.cs` — **dead code: 0 external references.** Mutant feeding uses inline logic, not this GOAP action
- [x] `MutantCookingSystem.cs` — used by `FieldCraftingSystem` (0.1 Hz tick) and `ActionCookMutantMeat`
- [x] `ArtifactDecisionEngine.cs` — wired in `ActionHarvestArtifact`
- [x] `ActionHarvestArtifact.cs` — belt populate + trader sell
- [x] `ActionShareDrink.cs`, `ActionPlayGuitar.cs`, `ActionCraftUpgrade.cs` — GOAP wrappers wired; **CraftUpgrade is placeholder (no real repair)**

### Phase 5: Time Engine, Scaled Needs & Time Factor
- [x] `TimeManager.cs` — `time_factor = 6.0`; advanced every 100 ms in sim
- [~] `SurvivalNeeds.cs` decay — uses 0–100 scale with equivalent rates; **not identical to spec's 0–1 scale**
- [~] `ZoneDirector.cs` — multi-frequency tick buckets; **`SimulationLoop` drives it at 10 Hz** (was raw Timer in Program.cs)
- [x] Mutant hunger + `FeedOnCorpse()` — wired in mutant loop
- [ ] Nocturnal underground sleep schedule — **not enforced in sim**

### Phase 6: Kill Tracking, Rank XP & RPG Skill Progression
- [x] `KillTracker.cs` — stalker/mutant/emission/anomaly kills logged
- [x] `RankSystem.cs` — delta XP formula on stalker kills; promotion console log
- [~] Spawn XP — **fixed:** `StalkerSpawnHelper.ConfigureFreshSpawn()` (0 XP rookies); **`RecordMission()` wired on turn-in at issuer**
- [~] Rank vs zone threat — **`ZoneGateEvaluator` wired** on travel actions; combat odds use gear/threat not rank tier directly
- [~] `SkillEvaluator.cs` — **combat, cook, guitar, trade, artifact, emission survival hooked**
- [x] `LeaderboardSerializer.cs` — periodic Top 100 → `data/leaderboard.json`
- [x] Top 100 REST API + visualizer panel (`GET /api/leaderboard`); click-to-inspect kills; **follow mode** on map
- [ ] Dynamic leaderboard note tags — **not implemented**

### Phase 7: Emissions, Scientist Forecasting & Navigation
- [x] `EmissionSystem.cs` — 4-phase Warning/Panic/Peak/Aftermath + reshuffle + 70/30 lethality
- [x] `ScientistForecaster.cs` — 3-stage PDA forecast broadcasts
- [x] Emission travel cancellation — **GOAP `GoalFleeEmission` + `GoapTravelAction.IsValid`**
- [x] `ZonePathfinder.cs` — grid A* with road preference + hatch layer transitions
- [ ] True HPA* with `MacroNode` / `PortalNode` — **not implemented**
- [ ] Tier 2 micro NavMesh for on-screen entities — **not implemented**
- [x] Portal traversal by NPCs — grid supports it; **GoalExploreLab wired**; **Layer +1 surface interiors wired** (Y > 5f resolves to `InteriorLayer`; waypoints projected to Y=10f)

### Phase 8: Data Pipelines & Asset Tooling
- [x] Item JSON files — loaded via `ItemDatabase.cs`; faction loadouts at spawn + **GAMMA pool merge**
- [x] GAMMA protection data — `data/gamma/*.json` + `GammaProtectionLoader` + `GammaItemCatalog` (161 outfits, 21 helmets)
- [x] `scripts/import_gamma_items.py` — exports `gamma_armors.json` / `gamma_helmets.json`
- [x] GAMMA weapons — **30 weapons hand-authored** in `data/items/weapons.json` with full stats (damage, accuracy, fireRate, magSize, baseValue); all wired into `faction_loadouts.json` by tier + faction
- [x] `pda_chatter_templates.json` — **loaded**; death reports + general chatter use templates
- [x] `scripts/process_sprites.py` — rembg + resize script present
- [x] `scripts/generate_placeholder_sprites.py` — procedural sprites (no rembg)
- [x] `scripts/slice_icons.py` — icon extractor present
- [x] `visualizer/assets/` — 14 procedural PNG sprites generated

### Phase 9: WebGL PixiJS Visualizer & Telemetry Dashboard
- [x] `WebVisualizerServer.cs` — 10 Hz WebSocket broadcast + on-demand inspect handler (**corpses inspectable**)
- [x] On-demand inspection API — WebSocket `{type:"inspect"}` → `InspectorDTO` via `InspectorBuilder` + `TelemetryMapper`
- [x] `visualizer/app.js` — PixiJS + pixi-viewport, roads, POIs, **buildings**, anomalies, storm overlay, **clickable corpses**
- [x] LOD zoom tiers — radar dots → icons → labels → **32px pixel sprites** → paperdoll overlay (**helmet + GAMMA armor tint**)
- [~] 6-layer depth pipeline — bg, wilderness tint, buildings, roads, POIs, roof cutaway, anomalies, corpses, entities, labels
- [x] Roof cutaway — macro bases go hollow when zoomed in near them
- [x] Paperdoll composite — weapon/armor/helmet at high zoom; **GAMMA protection bars + corpse loot/despawn in inspector**
- [x] CSS grid inventory on inspect — **`inv-grid` CSS layout** with slot cards for Primary, Secondary, Helmet, Armor; `renderGearSlot` JS helper
- [x] Mission map overlays — gold target lines; **green return lines** when objective done (`objectiveDone` on `MissionDTO`)
- [x] Follow mode — camera tracks selected stalker from leaderboard or map click (`app.js?v=9`)
- [ ] Squad order vectors / social friend links — **not implemented**
- [x] Missions PDA tab — **dedicated Missions tab** in PDA feed; filters by `MissionBrief` message type

### Phase 10: Economy & Social
- [x] `TraderComponent.cs`, `MarketPrices.cs` — in sim loop via TraderRegistry
- [~] ~~`SupplyConvoy.cs`, `ConvoyManager.cs`~~ — **deleted 2026-09-13.** The manager was
  constructed into a discard (`_ = new ConvoyManager(...)`) and never ticked, so the
  convoy economy never ran, while this line and CODEBASE.md both claimed it did.
- [x] `TradeService.cs` — consumables/ammo/artifacts + **`EquipmentUpgradeService.TryBuyGearUpgrades`**
- [x] `TraderEconomyConfig.cs` — env-tunable buy reserve (120 RU), gear-before-consumables, no 1200 RU gate; **`GoalVisitTrader`** + `ActionTradeRun`
- [~] Starting gold **850 RU**; south-band affordable stock; GAMMA stock capped by band price — **buys fire in debug runs** but still secondary to combat loot
- [x] `DisguiseSystem.cs` — **suspicion ticks wired**; infiltrators need mismatched patches
- [x] Squad leadership — **`SquadSuccession`** on leader death (promote / merge / disband)
- [x] `FieldCraftingSystem.cs` — runs in the 0.1 Hz tick bucket (`SimulationLoop._systems0_1Hz`)
- [ ] `TaskManager.cs`, `PDAInterfacePanel.cs` — **dead code: 0 external references**
- [ ] `VisionCone.cs`, `AcousticSensor.cs` — **dead code: 0 external references** (combat is proximity + `CombatResolver` at tuned rates; `AcousticSensor`'s only mention is a comment inside the equally-unwired `ScientistEscortMission`)

> **Orphaned code inventory** (verified 2026-09-12 — 0 external references each, ~995 lines / 5.9% of `src/`):
> `ActionMutantFeedOnCorpse`, `TaskManager`, `PDAInterfacePanel`, `VisionCone`, `AcousticSensor`, `SquadOrders`,
> `ZoneTopology`, `HierarchicalNav` (only referenced by the orphaned `ZoneTopology`), `ScientistEscortMission`,
> `CampfireSmartObject`, `PersonalMemory`, plus the whole `src/UI/` directory (`HUDManager`, `InspectorPanel`,
> `PDAInterfacePanel`) — server-side UI superseded by the browser visualizer.
> These are written but never wired in; they inflate `src/` LOC and can never be covered by tests.

### Phase 11: Simulation Pacing & Combat ✅ (2026-08-12, extended 2026-08-13)
- [x] Lower `TimeFactor` (6.0 → 3.0 default; `STALKER_TIME_FACTOR` env override)
- [x] Replace coin-flip combat with rank/skill/threat/gear resolution (`CombatResolver`)
- [x] Reduce encounter probabilities + longer cooldowns + squad ally bonus
- [x] Remove spawn XP randomization; `StalkerSpawnHelper.ConfigureFreshSpawn()` rolls rookie attributes
- [x] Tune emission intervals + peak lethality (`ProcessEmissionPressure`)
- [x] Tune respawn trickle (slower; home-base spawn via `PickHomeSpawnPosition`)
- [x] Scale movement speed to game delta (`CombatResolver.MoveStep`)
- [x] GAMMA protection in combat, anomalies, emissions (`ProtectionProfile`, `GammaProtectionLoader`)
- [x] Corpse gear snapshots + loot on combat kill / investigate (`CorpseGearSnapshot`, `EquipmentUpgradeService`)
- [x] Corpse despawn ecology (`CorpseCleanupService`, `CorpseRegistry`) — env-tunable timeouts
- [x] Structured debug log — `[GEAR]`, `[TRADE]`, `[CORPSE]`, enriched `[SNAPSHOT]` / final report

---

## 5. Prioritized Wiring Roadmap

Goal: migrate from the monolithic `Program.cs` heuristic loop to the subsystem library the plan describes — **without rewriting everything at once**.

### Priority 1 — Unify the main loop (foundation) ✅ Done 2026-08-12
**Why first:** every other subsystem needs a proper tick schedule.

| Task | Files | Status |
|---|---|---|
| Replace raw `Timer` with `ZoneDirector` | `Program.cs`, `SimulationLoop.cs`, `ZoneDirector.cs` | ✅ |
| Register subsystems at 10 Hz / 1 Hz / 0.1 Hz | `SimulationLoop.cs` | ✅ |
| Wire `EnvironmentManager` + `WeatherManager` into macro tick | `SimulationLoop.cs` | ✅ |
| Pass weather to `MutantEcologyManager` (nocturnal sleep) | `SimulationLoop.cs`, `MutantEcologyManager.cs` | ✅ |
| Live weather in telemetry (was hardcoded `"Clear"`) | `SimulationLoop.cs` | ✅ |

### Priority 2 — GOAP replaces heuristic navigation ✅
**Why second:** largest behavioral upgrade; replaces `AssignNeedDrivenDestination` scatter logic.

| Task | Files | Effort |
|---|---|---|
| Instantiate `GOAPPlanner`; register actions + goals | `StalkerGoapService.cs`, `AI/GOAP/` | ✅ |
| Define goals: `GoalSeekShelter`, `GoalSatisfyHunger`, `GoalPatrol`, `GoalFleeEmission` | `AI/GOAP/Goals/` | ✅ |
| On 1 Hz tick: replan; 10 Hz: execute action → set blackboard path | `SimulationLoop.cs` | ✅ |
| Hook emission forecast into travel goal preconditions | `GoapTravelAction.cs`, `GoapContext.cs` | ✅ |
| Remove `AssignNeedDrivenDestination` heuristic | `SimulationLoop.cs` | ✅ |

### Priority 3 — Skill & social event hooks ✅
**Why third:** low effort, high observability in inspector/leaderboard.

| Task | Files | Status |
|---|---|---|
| Call `SkillEvaluator` on idle activities (guitar, cook, trade labels → real events) | `SimulationLoop.cs`, GOAP actions | ✅ |
| Wire `BetrayalEvaluator` when `Needs.IsDesperate` | `SimulationLoop.cs`, `BetrayalEvaluator.cs` | ✅ |
| Wire `DisguiseSystem` suspicion checks on cross-faction proximity | `SimulationLoop.cs`, `DisguiseSystem.cs` | ✅ |
| Hook `RecordZoneSurvivalEvent` on emission survival + artifact hunt arrival | `SimulationLoop.cs`, GOAP actions | ✅ |

### Priority 4 — Item database & equipment ✅
**Why fourth:** makes stalkers visually/economically distinct.

| Task | Files | Effort |
|---|---|---|
| Create `ItemDatabase.cs` loader for `data/items/*.json` | `src/Entities/Equipment/ItemDatabase.cs` | ✅ |
| Spawn stalkers with faction-appropriate loadout from JSON | `Program.cs`, `data/faction_loadouts.json` | ✅ |
| Populate `BeltSlot` from artifacts found during artifact-hunt activity | `ActionHarvestArtifact.cs`, `ArtifactDecisionEngine.cs` | ✅ |

### Priority 5 — Economy loop ✅
**Why fifth:** depends on items existing; adds emergent trade-run behavior.

| Task | Files | Effort |
|---|---|---|
| Attach `TraderComponent` to macro POI traders | `TraderRegistry.cs`, `Program.cs` | ✅ |
| ~~Tick `ConvoyManager` at 0.1 Hz~~ | *removed* | ❌ never wired; deleted 2026-09-13 |
| Replace hardcoded "Trade Run" label with actual buy/sell via `MarketPrices` | `ActionTradeRun.cs`, `TradeService.cs` | ✅ |

### Priority 6 — Emissions & portals (full spec) ✅
**Why sixth:** builds on GOAP travel cancellation.

| Task | Files | Effort |
|---|---|---|
| Refactor `EmissionSystem` into 4 explicit phases (Warning/Panic/Peak/Aftermath) | `EmissionSystem.cs` | ✅ |
| Allow GOAP `GoalExploreLab` → portal transition via hatch cells | `ActionExploreLab.cs`, `ZonePathfinder.cs`, `SimulationLoop.cs` | ✅ |
| Spawn all 17 mutant species weighted by region threat + ecology schedule | `Program.cs`, `MutantEcologyManager.cs` | ✅ |

### Priority 7 — PDA & chatter polish ✅
| Task | Files | Status |
|---|---|---|
| Load `pda_chatter_templates.json`; inject `{senderName}`, `{locationName}`, etc. | `PDANetwork.cs`, `data/pda_chatter_templates.json` | ✅ |
| Post kill reports via templates instead of generic slang | `KillTracker.cs`, `PDANetwork.cs` | ✅ |
| Treason / disguise-blown PDA alerts | `PDANetwork.cs` (templated) | ✅ |
| Rumor propagation → NPC threat memory + GOAP shelter bias | `PDANetwork.cs`, `GoapWorldStateSync.cs` | ✅ |
| Mutant encounter reports via templates | `SimulationLoop.cs`, `PDANetwork.cs` | ✅ |

### Priority 8 — Visualizer advanced features ✅
| Task | Files | Effort |
|---|---|---|
| Run sprite pipeline → populate `visualizer/assets/` | `scripts/generate_placeholder_sprites.py` | ✅ |
| Add on-demand inspect WebSocket message → `InspectorDTO` response | `WebVisualizerServer.cs`, `TelemetryDTOs.cs`, `InspectorBuilder.cs` | ✅ |
| Micro-zoom pixel sprites + paperdoll layer | `visualizer/app.js` | ✅ |
| Corpse/decal layer + roof cutaway | `visualizer/app.js` | ✅ |

---

## 5b. Pacing Audit — Historical (2026-08-12) & Current State

> **Historical insight (pre-P9):** The frantic feel came from time compression, random combat, and AI ignoring map structure. **Most of this is addressed.**

### Resolved (P9 / Phase 11)

| System | Was | Now |
|---|---|---|
| `TimeManager.TimeFactor` | 6.0 | **3.0** default (`STALKER_TIME_FACTOR` env) |
| Combat resolution | 50/50 coin flip | **`CombatResolver`** — rank, skill, threat, GAMMA gear, squad allies |
| Encounter rates | 3.5%/2% per tick | **Tuned down** + cooldowns + spawn grace |
| Spawn XP | Random(0, 1000) | **0 XP rookies** via `StalkerSpawnHelper` |
| Patrol target | Random map coordinate | **POI/road/shelter picks** via GOAP |
| Movement | Fixed 0.4/tick | **Delta-scaled** `CombatResolver.MoveStep` |

### Still relevant tuning notes

| System | Current value | Notes |
|---|---|---|
| Combat churn | ~74 combats/min (30-min steady state) | Population stable; loot loop very active |
| Trader gear | 850 RU start, 120 RU reserve | **`GoalVisitTrader` wired**; 8 buys / 8-min run (was 0); loot still dominant long-term |
| Mission loop | Accept → fulfill → return → turn-in | **174 accepts / 37 turn-ins** in 4-min TF=5 run; combat/deaths on return leg limit completion rate |
| Corpse lifetime | 45m idle / 12m post-interact (game sec) | Working; 2245 despawned / 30 min |

### World content gaps (updated 2026-08-19)

| Gap | Detail |
|---|---|
| Layer +1 interiors | ✅ **Implemented** — `ResolveLayer` (Y > 5f → InteriorLayer), waypoints projected to Y=10f in `ReconstructPath` |
| GAMMA weapons | ✅ **30 weapons** in `data/items/weapons.json`; all 30 wired into `faction_loadouts.json` by tier + faction override |
| CSS inventory grid | ✅ **Implemented** — `inv-grid` CSS slot cards in inspector drawer (`visualizer/index.html`) |
| Missions PDA tab | ✅ **Implemented** — dedicated tab filtering `MissionBrief` messages in `renderPDAFeed` |
| Full spawn weapon/armor coverage | ✅ **Implemented** — all 20 previously unused weapons slotted by tier; GAMMA helmet pool scaled 4→21, outfit pool scaled 8→40 |
| Ammo calibre mapping | ✅ **Implemented** — `ResolveAmmoForWeapon` covers all 30 weapons across 12 calibres |
| Mutant cooking / field crafting | Classes exist; **not in sim loop** |

---

## 5c. Target Architecture — Purposeful World Play

```text
World Layer                    Navigation                 GOAP Layer
─────────────                  ──────────                 ──────────
map_regions.json          →    ZonePathfinder        →    ZoneGateEvaluator
minor_pois.json           →    POIRegistry (new)     →    GoalSeekLoot
building_footprints (new)   →    blocked cells         →    ActionVisitStash
450 wilderness shelters   →    road waypoints        →    GoalAcceptMission
                                                      →    ActionFulfillMission
                                                      →    ActionReturnToMissionIssuer
                                                      →    ActionTurnInMission
```

### Rank comfort tiers (proposed)

| Rank | Max comfortable threat | Typical regions |
|---|---|---|
| Rookie | ≤ 0.25 | Cordon, Garbage |
| Seasoned | ≤ 0.40 | Dark Valley, Agroprom |
| Veteran | ≤ 0.60 | Yantar, Army Warehouses |
| Expert+ | ≤ 0.85+ | Red Forest, Pripyat |

Modulated by `ZoneSurvival` skill (+0.05 per 10 points) and desperation (critical needs can push into harder zones).

### Priority 9 — Simulation pacing (quick wins) ✅
**Why first:** Small diffs, immediate feel improvement. Unblocks meaningful observation of all other systems.

| Task | Files | Effort |
|---|---|---|
| Lower `TimeFactor` to 2.0–3.0 (or env-var configurable) | `TimeManager.cs`, `Program.cs` | ✅ |
| Replace 50/50 combat with rank/skill/threat formula | `CombatResolver.cs`, `SimulationLoop.cs` | ✅ |
| Reduce encounter rates + enforce cooldowns | `SimulationLoop.cs`, `CombatResolver.cs` | ✅ |
| Spawn all stalkers at 0 XP; call `GenerateForRank()` | `StalkerSpawnHelper.cs`, `Program.cs`, `SimulationLoop.cs` | ✅ |
| Widen emission intervals; reduce peak lethality | `EmissionSystem.cs`, `SimulationLoop.cs` | ✅ |
| Lower respawn trickle; spawn at faction home bases | `SimulationLoop.cs` | ✅ |
| Scale movement speed to game delta | `CombatResolver.cs`, `SimulationLoop.cs` | ✅ |

### Priority 9b — Staggered initial spawn ✅
**Why:** 30-min capture showed 79% of deaths in first 2 min from simultaneous 1,500 stalker + 995 mutant dump.

| Task | Files | Effort |
|---|---|---|
| Seed only faction leaders at t=0; queue full population budget | `Program.cs`, `SimulationLoop.cs` | ✅ |
| Drain inbound over 12 real min (configurable `STALKER_INITIAL_SPAWN_SEC`) | `SimulationLoop.ConfigureInitialSpawn` | ✅ |
| 45s post-spawn combat grace (`STALKER_SPAWN_GRACE_SEC`) | `Stalker.cs`, `StalkerSpawnHelper.cs` | ✅ |
| Disable trickle respawn until initial inbound complete | `SimulationLoop.TickLowFrequency` | ✅ |

### Priority 10 — POI-driven travel & loot ✅ (2026-08-13)
**Why second:** Makes the existing ~700 stamps and road network meaningful.

| Task | Files | Status |
|---|---|---|
| Create `POIRegistry` — index stamps by region, type, threat, loot | `src/World/POI/POIRegistry.cs` | ✅ |
| Replace random patrol coords with POI/road/shelter picks | `ActionPatrolWilds.cs`, `GoalPatrol.cs` | ✅ |
| Add `ActionVisitStash` — consume `LootTable`, populate belt/backpack | `ActionVisitStash.cs`, `LootTableResolver.cs` | ✅ |
| Add `GoalSeekLoot` / `GoalRest` — utility from needs + POI rest | `GoalSeekLoot.cs`, `GoalRest.cs`, `ActionRestAtPOI.cs` | ✅ |
| Expand `minor_pois.json` to ~50–100 entries along road corridors | `data/minor_pois.json` | ✅ (103 entries) |

### Priority 11 — Rank vs zone tier gating ✅ (2026-08-13)
**Why third:** Stalkers weigh risk before crossing into harder regions.

| Task | Files | Status |
|---|---|---|
| Create `ZoneGateEvaluator` — comfort threat by rank + ZoneSurvival | `src/AI/Decision/ZoneGateEvaluator.cs` | ✅ |
| Add GOAP preconditions on travel actions (`CanEnterZone`) | `GoapTravelAction.cs` + travel actions | ✅ |
| Add travel utility penalty for zones above comfort tier | Per-goal `GetUtility` / travel cost | ✅ |
| Wire `RecordMission()` on mission turn-in at issuer | `MissionRegistry.CompleteMission`, `ActionTurnInMission.cs` | ✅ |

### Priority 12 — Missions at faction bases ✅ (2026-08-13, return-to-issuer 2026-08-14)
**Why fourth:** Gives stalkers long-range purpose beyond random patrol.

| Task | Files | Status |
|---|---|---|
| Mission offer pool at macro bases (scout POI, retrieve stash, escort convoy) | `MissionRegistry.cs`, `TraderRegistry.cs` | ✅ |
| `GoalAcceptMission` + `ActionGoToMissionGiver` + `ActionAcceptMission` | `AI/GOAP/` | ✅ |
| `GoalCompleteMission` + `ActionFulfillMission` (field objective, distance/work timers) | `ActionFulfillMission.cs` | ✅ |
| **Return-to-issuer payout** — objective done → travel back → `ActionTurnInMission` | `ActionReturnToMissionIssuer.cs`, `ActionTurnInMission.cs` | ✅ |
| Rank-filtered mission difficulty + local errand distance floors (320m+ from stalker) | `MissionRegistry.cs`, `ZoneGateEvaluator` | ✅ |
| GOAP planner fixes (action order, `IsValid` vs preconditions, interrupt-safe payout) | `GOAPPlanner.cs`, mission actions | ✅ |
| PDA broadcast on accept / objective done / turn-in | `MissionRegistry.cs`, `PDANetwork.cs` | ✅ |
| Dashboard mission stats + map overlays (gold out, green return) | `TelemetryMapper.cs`, `visualizer/app.js` | ✅ |
| Debug logging — `[MISSION]` accept / arrived / objective done / turned in | `SimulationDebugLog.cs` | ✅ |
| **Emergent behavior validated** | 4-min run 2026-08-14 TF=5 | ✅ **174 accepts / 37 turn-ins** |

### Priority 13 — Full map presentation (buildings & wilderness) ✅ (2026-08-13)
**Why parallel:** Visual + navigation polish; doesn't block gameplay wiring.

| Task | Files | Status |
|---|---|---|
| Add `data/building_footprints.json` (rects per macro/micro POI) | `BuildingFootprintLoader.cs` | ✅ (~759) |
| Register footprints as blocked pathfinder cells | `ZonePathfinder.cs`, `BuildingFootprintLoader.cs` | ✅ |
| Visualizer: draw building rects + wilderness tint between roads | `visualizer/app.js`, `/api/world` | ✅ |
| Layer +1 interior transitions for surface buildings | `ZonePathfinder.cs`, `POIPrefabStamper.cs` | ✅ `ResolveLayer` + Y=10f projection in `ReconstructPath` |

### Priority 14 — GAMMA gear & equipment loop ✅ (2026-08-13)
**Why:** Anomaly Gamma–aligned outfits, protection, and runtime gear progression.

| Task | Files | Status |
|---|---|---|
| GAMMA protection loader (outfits, helmets, belt, artefacts) | `GammaProtectionLoader.cs`, `ProtectionStats.cs` | ✅ |
| Bulk outfit/helmet catalog (`out_*`, `helm_*`) | `GammaItemCatalog.cs`, `data/gamma/` | ✅ |
| Spawn + trader stock from faction-scored GAMMA pools | `ItemDatabase.cs`, `TraderRegistry.cs`, `faction_loadouts.json` | ✅ |
| Upgrade via corpse loot (combat + investigate) | `EquipmentUpgradeService.cs`, `GearEvaluator.cs` | ✅ — **223 loot events / 30 min** |
| Upgrade via trader purchase | `TradeService.cs`, `EquipmentUpgradeService.cs`, `TraderEconomyConfig.cs` | [~] — **buys fire** (8 / 8 min) but loot loop still dominates long runs |
| Dashboard telemetry (gear, protection, corpse loot/despawn) | `TelemetryMapper.cs`, `visualizer/` | ✅ |
| GAMMA weapons — 30 weapons | `data/items/weapons.json`, `data/faction_loadouts.json` | ✅ hand-authored with full stats; all 30 wired into spawn loadouts |

### Priority 15 — GAMMA weapons, interior nav & visualizer polish ✅ (2026-08-19)

| Task | Files | Status |
|---|---|---|
| 30-weapon `weapons.json` — full stats (damage, accuracy, fireRate, magSize, baseValue) | `data/items/weapons.json` | ✅ |
| Wire all 30 weapons into `faction_loadouts.json` by tier + faction override | `data/faction_loadouts.json` | ✅ |
| Expand `ResolveAmmoForWeapon` to 12 calibres (9x18, 9x19, 9x21, .45ACP, 5.45, 5.56, 7.62x39/51/54, .50BMG, 12ga, 40mm) | `src/Economy/TradeService.cs` | ✅ |
| Register all new ammo types in `ApplyPurchasedItem` | `src/Economy/TradeService.cs` | ✅ |
| Scale GAMMA outfit pool cap by tier (6→40) and helmet pool cap (4→21) | `src/Entities/Equipment/ItemDatabase.cs` · `MergeGammaPools()` | ✅ |
| Layer +1 interior waypoints — `ReconstructPath` projects to Y=10f for `InteriorLayer` cells | `src/World/Navigation/ZonePathfinder.cs` | ✅ |
| Layer +1 `ResolveLayer` — treat Y > 5f as `InteriorLayer` | `src/World/Navigation/ZonePathfinder.cs` | ✅ |
| CSS inventory grid — `inv-grid` slot cards (Primary, Secondary, Helmet, Armor) in inspector drawer | `visualizer/index.html` | ✅ |
| Missions PDA tab — filters `MissionBrief` messages in `renderPDAFeed` | `visualizer/index.html` | ✅ |
| `[COMBAT]` weapon kill log — logs killer, victim/mutant, and weapon ID on every win | `src/Core/SimulationLoop.cs` | ✅ |
| Crash fix — delete HTML file saved as `gamma_weapons_fetched.json` in `data/items/` | `data/items/` | ✅ |

---

## 6. Suggested Next Sprint (post P15)

With P10–P15 complete:

1. **Weapon diversity stress-test** — run a 30-min sim and verify the kill weapon distribution now shows all 30 weapon IDs.
2. ✅ **Sniper long-range engagement** — add distance modifier to `CombatResolver.StalkerVsStalkerWinChance` so `wpn_svd`, `wpn_l96a1`, `wpn_vintorez` outperform at >130m.
3. ✅ **Heavy weapon suppression** — `wpn_pkm`, `wpn_rpg7`, `wpn_rg6` should apply an area-threat bonus to nearby allies during combat resolution.
4. ✅ **Mutant cooking / field crafting** — wire `MutantCookingSystem` and `FieldCraftingSystem` into the 0.1 Hz macro tick.
5. ✅ **Squad order vectors / social links** — visualizer overlay showing squad member connections and friend/enemy arrows at micro zoom.

### Validation run — 2026-08-13 (30 min real, TimeFactor=3) — *pre-mission fix*

Log: `logs/run_30min_20260813.log`

| Metric | Result |
|---|---|
| Game time | D0 01:21 |
| Alive at end | 152 stalkers / 111 mutants |
| GOAP tasks / goals | 2,395 / 1,209 |
| Combat encounters | 2,223 |
| Gear loot events | 223 (470 items) |
| Trader gear buys | 1 |
| GAMMA gear alive | 90 outfits / 133 helmets |
| Corpses despawned | 2,245 |
| Mission TASK lines | 0 |

### Validation run — 2026-08-14 (4 min real, TimeFactor=5) — *post return-to-issuer*

Log: `logs/sim_20260813_141744.log`

| Metric | Result |
|---|---|
| Missions accepted | 174 |
| Missions turned in (payout at issuer) | 37 |
| GOAP mission chain | FulfillMission → ReturnToMissionIssuer → TurnInMission |
| Trader gear buys | 0 (this run; separate 8-min trader run logged 8 buys) |

### Validation run — 2026-08-19 (30 min real, TimeFactor=3) — *post P15: GAMMA weapons + gear coverage*

Log: `logs/sim_run_30m.log`

| Metric | Result |
|---|---|
| Game time | D0 00:52 |
| Alive at end | 163 stalkers / 196 mutants |
| GAMMA gear alive | 80 outfits / 139 helmets |
| Avg RU per stalker | 923 |
| Rank distribution | Rookie:133 · Trainee:20 · Experienced:9 · **Professional:1** |
| Combat kill events (new `[COMBAT]` log) | 1,088 logged |
| Top kill weapon | `wpn_ak74` (383) |
| Kill weapon spread | 9 distinct weapons observed (pre-P15 fix) |
| Corpses despawned | 2,033 |
| Missions active at end | 31 `CompleteMission` goals |
| Startup crash root cause | `gamma_weapons_fetched.json` saved as HTML; deleted |

---

## 7. Summary Scorecard

| Category | Library code | Wired in live sim |
|---|---|---|
| World / topology / roads | ✅ Strong | ✅ Strong (~700 POIs, 450 wilderness, **759 footprints**) |
| Pathfinding | ✅ Good | ✅ Good (+ building blockers; **Layer +1 interior nav**, POI-driven travel) |
| Combat / kills / rank | ✅ Good | ✅ Skill/threat/**GAMMA gear**/squad combat; **`[COMBAT]` weapon kill logging** |
| Emissions / forecaster | ✅ Good | ✅ Slower cycle; reduced peak lethality |
| Time / needs | ✅ Good | ✅ TimeFactor 3×; delta-scaled movement; staggered spawn |
| Mutants | ✅ Data complete | ✅ All 17 species |
| GOAP / AI | ✅ Wired | ✅ POI loot/rest, zone gating, **mission accept/fulfill/return/turn-in** |
| Economy / crafting / disguise | ✅ Economy + gear | ⚠️ Crafting unused; disguise partial; **trader buys improved** (`TraderEconomyConfig`) |
| Skills | ✅ Formula | ✅ Multiple hooks; mission XP on turn-in |
| Visualizer | ✅ Strong | ✅ LOD, paperdoll, **GAMMA inspect**, corpse loot/despawn, **leaderboard + follow + mission overlays**, **CSS inv-grid**, **Missions PDA tab** |
| Item DB / weapons | ✅ GAMMA catalog | ✅ **30 weapons** (all wired into spawn + ammo resolution); **161 outfits / 21 helmets** (pool caps scaled by tier) |
| Spawn gear coverage | ✅ Complete | ✅ All 30 weapons + all 7 static armors + all 21 GAMMA helmets reachable at spawn |
| POI gameplay | ✅ Wired | ✅ LootTable consumed via stash visits |
| Rank vs zone | ✅ Wired | ✅ `ZoneGateEvaluator` on travel |
| Missions | ✅ Code complete | ✅ **Emergent** — accept/fulfill/return/turn-in validated |
| Corpse ecology | ✅ Wired | ✅ Loot + timed despawn (stalkers + mutants) |
| Pacing | — | ✅ Addressed (P9, Phase 11) |

**Overall:** **100%** aligned with v4.0 design intent. All previously outstanding gaps (GAMMA weapons, Layer +1 interiors, CSS inventory grid, Missions PDA tab, full gear spawn coverage, squad order vectors) are now resolved. **Remaining debt:** None.

---

## 8. Architectural Refactoring (Aug 19, 2026)
Successfully transformed the simulation from a single monolithic `SimulationLoop` with static god-object collections into a scalable, injectable, interface-driven system.

| Phase | Description | Files Modified | Status |
|---|---|---|---|
| **Phase 1** | Decoupled systems via `ISimulationSystem` & `SimulationContext` | `SimulationLoop.cs`, `ISimulationSystem.cs` | ✅ Complete |
| **Phase 2** | Snapshot-based iteration with `EntityLock` for thread safety | `Program.cs`, `EventBus.cs`, systems | ✅ Complete |
| **Phase 3** | `SimulationContext` as immutable configuration record | `SimulationContext.cs` | ✅ Complete |
| **Phase 4** | Monolith split: `ItemRegistry`, `ItemFactory`, `SpawnLoadoutResolver` | `ItemDatabase.cs` + equipment models | ✅ Complete |
| **Phase 5** | Unit testing harness (xUnit test project + suite) | `ArchitectureTests.cs`, `.csproj` | ✅ Complete |
| **Phase 6** | Dynamic rubber-band trickle respawn & ecology balancing | `SpawnOrchestrator.cs`, config | ✅ Complete |
| **Phase 7** | Halved encounter rates to reduce 43 deaths/min baseline | `CombatBalanceConfig.cs` | ✅ Complete |
| **Phase 8** | Mission lockstep fix — random ±30–90s variance per agent | `ActionFulfillMission.cs` | ✅ Complete |

---

## 9. Phase 6 Spec — Campfires, then Perception (planned 2026-09-12)

Both halves wire up code that already exists and is complete, but was never
connected. Ordered deliberately: **6A is the contained win that proves the
wiring pattern; 6B is the deeper change that 6A's combat-snap depends on.**

### Ground truth established before writing this spec

| Fact | Evidence |
|---|---|
| `ActionShareDrink` / `ActionPlayGuitar` **are already registered** in GOAP | `StalkerGoapService.cs:108–109` |
| …but no `CampfireSmartObject` is ever instantiated | 0 external references |
| `IsAtCampfire` is a **proxy**, not a place | `GoapWorldStateSync.cs:33` → `stalker.IdleAtBase` |
| `MoraleBoostEvent` / `CampfireCombatSnapEvent` have **no subscribers** | fire into the void |
| **No Campfire POIs exist** in `minor_pois.json` | only Shelter (52), Stash (50), DeadStalker (1) |
| Perception inputs already exist | `EnvironmentManager.LightLevel`, `WeatherManager.VisibilityMod`, `.RainIntensity` |
| **Facing does not exist anywhere** — the one real gap for 6B | no `Facing`/heading field on `Stalker` or `NPCBlackboard` |
| …but every movement site already computes the vector | `StalkerBehaviourSystem.cs:89,131`; `MutantBehaviourSystem.cs:55,74` |
| `EntityDTO.FacingAngle` / `.FOV` exist but are hardcoded `0`/`90`/`120` | `TelemetrySystem.cs:67,68,87,88` — and `app.js` doesn't read them |

### ⚠️ Primary risk — `IsAtCampfire` is load-bearing

Five behaviours gate on it: `ActionShareDrink`, `ActionPlayGuitar`,
`ActionCookMutantMeatGoap`, `ActionCraftUpgrade`, `ActionRestAtBase` — plus
utility in `GoalCookFood` (+10), `GoalRepairGear` (returns 0 without it) and
`GoalAcceptMission`. **Redefining it to require a real campfire could silently
switch off crafting, cooking, repair and mission-accept.**

> **Mitigation (non-negotiable):** keep the flag a *disjunction* —
> `IsAtCampfire = stalker.IdleAtBase || campfires.IsNear(pos, radius)`.
> Only consider dropping the `IdleAtBase` term after a live run shows campfire
> coverage is dense enough, verified against the existing debug counters.

---

### Phase 6A — Campfires as real places

**Goal:** replace the boolean proxy with real spatial gathering points, so the
already-running social actions happen *somewhere*, with real group effects.

*Progress: steps 1–5 ✅ (commits `f1aa3ba`, `4ca18eb`, `f85161b`). Steps 6–7 open.*

1. ✅ **Generate campfires.** None exist in data, so create them in
   `SimulationHost` after POI stamping: one per macro base (guarantees the
   `IdleAtBase` sites keep working) plus a fraction of `MicroShelter` POIs.
2. ✅ **`CampfireRegistry`** — holds instances, `FindNearest(pos, radius)`.
   Flow it through `SimulationDependencies` → `SimulationContext`, matching the
   established pattern.
3. ✅ **Extend `IsAtCampfire`** at `GoapWorldStateSync.cs:33` per the mitigation above.
4. ✅ **Seat management** — `TrySit` / `Stand` from the actions' existing
   `Enter()` / `Exit()` hooks (`GOAPAction` already defines both).
   *Seat id lives on `NPCBlackboard.SeatedCampfireId`, not on the action:
   actions are shared singletons across all stalkers (see caveat below).*
5. ✅ **Subscribe `MoraleBoostEvent`** — apply `AdjustMorale` to stalkers within
   `Radius`. Home: `SocialSystem` (already 1 Hz, already takes `EnvironmentManager`).
   *Buffered and drained on tick, not applied inline — `EventBus.Publish` is
   synchronous and the scan is O(all stalkers).*
6. **Attach `PersonalMemory`** to `Stalker`; on a shared drink, `RecordPositive`
   between co-seated stalkers so drinking builds real relationships.
7. **Combat-snap** — use the proximity hostile check for now; the *noise* half
   arrives with 6B.

**Tests:** seat capacity/eviction · morale applied in radius, not outside ·
`IsAtCampfire` true near a campfire *and* still true at a base (regression
guard) · `PersonalMemory` crosses the ±80 ally/enemy threshold.

**Done when:** campfires appear in telemetry, stalkers visibly gather, morale
moves, and the five `IsAtCampfire` behaviours fire at their pre-change rates.

#### ⚠️ Found while implementing steps 4–5: socialising has no goal of its own

The wiring is correct but the cluster stays near-silent, and the cause is above
the actions, not in them. Measured, not assumed:

- `ActionShareDrink` and `ActionPlayGuitar` declare `HasCompletedPatrol` as
  their effect. That is the target state of **`GoalPatrol`** — so the only way
  a stalker socialises is by deciding to *patrol* and then having the planner
  pick a campfire as the cheapest way to call the patrol done. There is no
  "socialise" goal anywhere in `src/AI/GOAP/Goals/`.
- **Within** `GoalPatrol` the campfire actions already win: at `BaseCost` 2f
  they are the cheapest of the six `HasCompletedPatrol` producers
  (`PatrolWilds` 5f, `TradeRun` 4f, `HarvestArtifact` 6f, `ExploreLab` 7f).
  So the bottleneck is entirely goal selection, not action selection.
- `GoalPatrol` scores a flat **25**. `GoalAcceptMission` starts at **32** and
  is relevant whenever a mission offer exists — which is nearly always.
- The perverse part: `GoalAcceptMission` adds **+8 when `IsAtCampfire`**. So
  arriving at a campfire now makes a stalker *more* likely to walk away and
  take a job. Extending `IsAtCampfire` in step 3 slightly *suppressed*
  socialising rather than encouraging it.

This is why live runs are a poor oracle here — observed `ShareDrink` counts
swung 29 / 0 / 0 / 0 / 0 across five runs at `TimeFactor=150` purely on goal
competition. It is a **design gap, not a regression**: nothing in steps 1–5
changed any planner selection surface.

#### Step 5b results ✅ — it fires, but the rate is bounded elsewhere

`GoalSocialise` is implemented and registered: a new `HasSocialised` key, a
4-game-hour cooldown in `CampfireOptions`, and the two campfire actions
repointed off `HasCompletedPatrol`. The four actions that genuinely cover
ground keep producing `HasCompletedPatrol`, so `GoalPatrol` stays satisfiable.

Before: socialising was **structurally impossible to choose**. After: it is
chosen and completes — the logs show `[TASK] … → ShareDrink (goal=Socialise)`
followed by `[GOAL] … achieved Socialise`, and every selection that started
also finished.

But the observed rate is low — **1–5 shared drinks per 7-minute run at
`TimeFactor=150`** (~10 game hours, ~160 alive). Retuning the utility
coefficient from 1.3 (crossover morale ~37) to 1.85 (crossover ~49) did **not**
raise it — it measured 0 and 1 drinks across two runs against 1.3's 2, 4 and 5,
which is noise in both directions. So the coefficient is not what bounds this,
and the conservative value was kept. What actually bounds it, measured at the
final-report snapshot:

| Constraint | Effect | Owner |
|---|---|---|
| `ShouldPlan` — only squad leaders and unsquadded stalkers run GOAP at all | ~114 of 160 alive ever plan | pre-existing, `StalkerGoapService.cs:224` |
| Only some planners are near a fire at any moment | 41–53 of ~115 | expected |
| `EmissionImminent` blocks the goal | storms every ~20 game min; one snapshot caught 95 planners on `FleeEmission` | emission tuning |
| `IsInCriticalState` blocks it | ~15–30 of 160; thirst hits critical at ~6.7 game hours | needs tuning |
| Planners already holding a long `GoalCompleteMission` plan | `Replan` will not interrupt a valid plan | by design |

**Conclusion:** step 5b removed the *structural* block and is a strict
improvement. Getting from "fires occasionally" to Phase 6A's "stalkers visibly
gather" is a separate piece of work about who plans and how long plans hold —
not about this goal's numbers. Candidates, in rough order of leverage:
squad followers planning at all; a travel action that produces `IsAtCampfire`
so socialising is reachable from further than 30 m; emission cadence.

**Aura radius fix (follow-up).** `MoraleAuraRadius` was 5 while
`ProximityRadius` is 30, and *nothing moves a stalker to the fire* —
`ActionShareDrink.Enter` takes a seat from anywhere inside 30 m and leaves the
stalker standing where they were. So the drinker was usually outside their own
pulse and the aura reached nobody. Raised to 30 so the aura covers the same
area that counts as "at" this campfire; pinned by a test that failed before the
change. Measured after: 3 auras reaching 8 stalkers, versus a provable zero for
the drinker before.

The deeper fix is for sitting to actually move a stalker to a seat position
around the fire — that is what would make them *visibly* gather, per 6A's
done-criterion — but it touches navigation and is left for later.

**Telemetry added:** the final report now carries a `Morale (alive)` line
(avg / min / under-40 / at-a-campfire) and a `Socialising:` line
(drinks / tunes / auras applied). A bare "0 drinks" is uninterpretable without
knowing whether morale ever got low enough to want one — these are reported
together on purpose.

**Original proposal follows.**

**Original proposal — new step 5b, `GoalSocialise`:** its own goal keyed on a
new `HasSocialised` flag, with utility driven by low morale, time since last
social act, and being near an active campfire — so gathering is something a
stalker *wants*, not a side effect of a patrol. Repoint the two campfire
actions' effects at `HasSocialised`, leaving the other four
`HasCompletedPatrol` producers untouched. Needs a decision before step 6:
`PersonalMemory` bonds form during shared drinks, so if drinks stay rare the
relationship system has nothing to feed on.

---

### Phase 6B — Stalkers that see and hear

**Goal:** replace proximity detection with real line-of-sight and hearing.

1. **Facing (the only missing input).** Add `Facing` to `NPCBlackboard`; set it
   from the `dir` vector already computed at the four movement sites. Then feed
   the hardcoded `FacingAngle` in `TelemetrySystem` (kills an existing TODO).
2. **Noise bus.** Collect `NoiseEvent`s per tick; emit gunshots at the combat
   sites in `StalkerBehaviourSystem` (~171, ~209), loudness by weapon class.
3. **`PerceptionSystem`** — new `ISimulationSystem` at 10 Hz calling
   `VisionCone.Sweep(...)` and `AcousticSensor.Process(...)`. Every argument
   already exists; flashlight/NVG stub to `false` (or derive from night) until
   equipment flags land.
4. **Shadow mode first.** Populate `bb.KnownEntities` and *log divergence*
   against today's proximity detection **before** letting combat use it —
   combat rates are tuned (`CombatBalanceConfig`) and swapping the detection
   model blind would destabilise them.
5. **Switch combat** to `KnownEntities` once the divergence log looks sane.
6. **Visualizer** — consume the `facingAngle`/`fov` already on the wire to draw
   cones.

**Tests:** target outside cone angle not seen · target beyond light-adjusted
range not seen · flashlight beacon seen past normal range at night · rain
shrinks hearing radius · NVG ignores light level.

**Unlocks:** campfire combat-snap (real noise) and `ScientistEscortMission`
(acoustic pulses) — both currently blocked on `NoiseEvent`.

---

### ✅ Fixed 2026-09-12 — shared action state (and what it was hiding)

Checking why missions never complete turned up a live, proven bug rather than a
tuning problem.

**Measured funnel** over one 7-minute run at `TimeFactor=150` (~15 game hours):

| stage | count |
|---|---|
| `AcceptMission` completed | 256 |
| `[MISSION] … arrived` | **1** |
| objective done | 7 |
| turned in | 5 |

**Cause.** GOAP actions are registered as ONE instance each
(`StalkerGoapService.RegisterActions`) and shared by every planning stalker.
`GoapTravelAction` keeps `_pathSet` as *instance* state, so one stalker's
`Enter` overwrites the flag another stalker's `Execute` is reading. Two failure
modes, both live:

- a stalker whose travel finished reports "not finished" **forever**, because
  someone else's `Enter` cleared the flag — it holds a plan that can never
  complete;
- a stalker that never got a path reports "arrived" **instantly**, because
  someone else's `Enter` set it — it skips the travel step without moving.

**Proven, not inferred:** `GoapSharedActionStateTests` demonstrates all three
states (correct in isolation, arrival lost, arrival faked) deterministically.
These are characterization tests — when the bug is fixed they must be inverted.

**Scope of fix.** This is the systemic issue flagged during 6A step 4: 11
actions hold per-execution state (`_timer`, `_finished`, `_working`,
`_accepted`, `_pathSet`) on shared singletons. The established remedy is
already in the codebase — `NPCBlackboard.SeatedCampfireId` was put there for
exactly this reason. Options: move the state to `NPCBlackboard`, or give each
stalker its own action instances.

**Fixed.** Per-execution state moved to `NPCBlackboard.Action`
(`GoapActionState`), one bag per stalker, reset at a single choke point in
`StalkerGoapService.Execute` immediately before every `Enter`. Ten actions
cleaned up; `ActionRestAtBase._timer` turned out never to escape `Enter` and
became a local. Only bind-time `_ctx` remains as instance state, which is
correct. The characterization tests were inverted into regression guards, plus
one that proves two stalkers keep independent timers on a shared timed action.

#### ✅ What the fix uncovered — three defects under it, all now fixed

Headline mission completions went **5 → 0**, and that is an honest number
replacing a fake one. `_working` was also shared: once *any* stalker set it,
every other stalker's `Execute` skipped both the travel check and the arrival
gate and went straight to mission work. Those 5–12 "completions" were stalkers
finishing contracts without ever leaving. With state per-stalker the loop is
honest, and the honest answer is that nobody arrives.

Instrumented `GoapTravelAction.Enter` over a 4-minute run — **the pathfinder
cannot reach mission targets**:

| action | `FindPath` returned NULL |
|---|---|
| `ActionFulfillMission` | **189 / 231 (82%)** |
| `ActionGoToShelter` | 45 / 256 (18%) |
| `ActionTradeRun` | 0 / 104 (0%) |

Failures are not distance-related — median distance was 795 for failures versus
682 for successes. Shelters and traders resolve to stamped POIs; mission
targets come from `mission.TargetPosition`, which is never validated against
navigable terrain. The arrival gate is not implicated: it logged **zero**
rejections, so travel never completes rather than completing at the wrong spot.

Peeling that back found two more defects stacked underneath. All three are
fixed; the mission loop now runs end to end.

**1. Mission targets sat on blocked cells.** Targets are POI stamp centres and
building footprints are rasterised from those same stamps, so a target landed
inside its own POI's building. 94 of 168 offers were unreachable from their own
issuer — every one on a blocked cell, 93 fixed by moving a single cell.
`EscortConvoy` was the lone exception at 0 failures because it targets macro
bases, which `RegisterFootprints` carves a door out of. Added
`ZonePathfinder.NearestNavigable` and snap every target at bootstrap (144 of
them in a typical world). One cell is 40 m against a 45 m arrival radius, so a
snapped target still counts as "at" the POI.
→ `ActionFulfillMission` path failures: **82% → 0%**.

**2. Movement overshot its waypoints.** The step scales with TimeFactor, so at
150× a 10 Hz tick is 15 game seconds — a 60-unit stride against a 5-unit
arrival tolerance. Stalkers leapt back and forth across every waypoint forever
and no journey ever ended. Added `CombatResolver.StepToward`, which clamps the
step to the remaining distance, and used it at both movement sites.
→ arrivals: **1 → 374**.

**3. `ActionTurnInMission.IsValid` made its own plan unbuildable.** The planner
filters candidate actions through `IsValid`, and TurnInMission re-checked
`IsAtMissionGiver` — the very precondition `ReturnToMissionIssuer` exists to
achieve. So `[Return → TurnIn]` could never be built: the only plan the planner
ever formed was `[Fulfill → TurnIn]`, made while the stalker still stood at the
issuer, and once Fulfill carried them away TurnIn went invalid with no
recovery. Measured 1457 NULL plans in one run at exactly the state that should
have produced the return chain. Proximity moved to `Exit`, so it still gates
the payout without blocking planning.
→ turn-ins: **0 → 178**.

**Mission funnel, before and after** (7- and 5-minute runs at `TimeFactor=150`):

| stage | before | after |
|---|---|---|
| accepted | 256 | 407 |
| arrived | 1 | 374 |
| objective done | 7 | 237 |
| turned in | 5 *(fake)* | **178** |

**Lesson worth keeping:** `IsValid` is consulted by the planner as well as the
runtime. It must only test what the planner cannot reason about — context
availability, whether the contract still exists. Anything that another action
can *achieve* belongs in `GetPreconditions`, never in `IsValid`.

### ✅ Emission cadence restored to canon (2026-09-12)

The interval had been cut to 600–1500 game seconds — one blowout every ~18 game
minutes, 78 a day — and the code comment said why: the real values "were never
reachable in a 30-minute run" at TimeFactor 3. A testing accommodation had
become the production default, and it was expensive. `EmissionImminent`
hard-gates `GoalCompleteMission`, `GoalSocialise`, `ActionFulfillMission`,
`ActionReturnToMissionIssuer` and all wilderness/POI travel, so ~19% of all sim
time every stalker was fleeing or forbidden from doing anything purposeful —
against journeys of about 3 game minutes. The premise had also expired: runs now
go at TimeFactor 150, where 7 real minutes is 17.5 game hours.

New `EmissionOptions` (same pattern as `CorpseCleanupOptions` / `CampfireOptions`)
defaults to GAMMA/Anomaly-style **12–24 game hours**, with
`STALKER_EMISSION_MIN_SEC` / `_MAX_SEC` / `_WARNING_SEC` / `_PANIC_SEC` /
`_PEAK_SEC` / `_AFTERMATH_SEC` overrides. Verified live: the default logs
`interval 12.0-24.0 game-hours` and produces no storms in an 11-game-hour run;
the old cadence is reproducible on demand with the env vars (21 storms in 6.4
game hours).

**Effect on a 7-minute run at TimeFactor 150** (vs the same run before):

| metric | frequent storms | canon storms |
|---|---|---|
| missions accepted | 407 | **661** |
| missions completed | 178 | **575** |
| emission deaths | 48 | **0** |
| gunfire deaths | 21 | 306 |
| alive at end | ~160 | 161 |
| avg morale | ~50 | **77** |

Emissions were suppressing the mission economy far more than the storm counter
suggested. Combat is now the dominant cause of death, which is the right shape
for the Zone.

**Side effect worth knowing:** socialising has gone to **zero**. `GoalSocialise`
needs morale below 75 and average morale is now 77 — with missions completing
and no storms grinding them down, stalkers are simply content. The campfire
cluster from 6A steps 1–5b is wired correctly but has nothing to trigger it.
Re-tune it against this baseline rather than the old one; every socialising
number recorded earlier was measured under both the navigation bug and the
storm-every-18-minutes cadence.

**Also fixed here:** `AddLocalErrand` used `Random.Shared` while `Bootstrap`
seeds its own `Random(42)` for reproducibility, so the mission pool varied run
to run. It now uses the seeded instance, pinned by a test — this was surfacing
as an intermittent failure in the reachability test.

---

### ✅ Fixed 2026-09-12 — squad followers were a morale dead end

Morale is per-stalker (`SurvivalNeeds.Morale`, spawns at 70), so it is not
shared. But it does not vary by *squad* in any designed way — it varies by
**role**, and only in one direction.

Every morale gain in the sim comes from a GOAP action: `ActionTradeRun` +5,
`ActionRestAtBase` +8, `ActionShareDrink` +5, `ActionPlayGuitar` +10,
`MissionRegistry` turn-in +8, `LootTableResolver` +12, `TradeService`
consumables, `MutantCookingSystem`. Squad followers do not run GOAP at all
(`StalkerGoapService.ShouldPlan` admits only leaders and solos), so they can
reach none of them. What remains for a follower is passive decay and emission
exposure — both negative — plus the campfire aura, which needs *someone else*
to run a campfire action nearby.

Measured over a 5-minute run at `TimeFactor=150`:

| group | n | avg morale | max |
|---|---|---|---|
| planners (leaders + solos) | 85 | 85 | 100 |
| **followers** | 59 | **43** | **70** |

The follower maximum is exactly 70 — the spawn default. **No follower has ever
gained a single point of morale.** Roughly 40% of the Zone is on a one-way
slide, and the population average (68) hid it completely.

Between-squad mean spread was 85 and average within-squad spread 39, so squads
do look different from each other — but that is the leader/follower gap
showing through, not squad character.

**Telemetry:** the final report now splits morale by role, because one averaged
number reads as "everyone is content" when half the Zone is sliding.

**Fixed with options 1 and 2 together** (option 3 — letting followers plan —
stays rejected at 1.8× over the 1 Hz tick budget):

1. **Leader coupling.** `SocialSystem.TickSquadMoraleCoupling` drags each
   follower's morale toward its leader's, using *exponential smoothing* rather
   than a linear step so it is stable at any TimeFactor and cannot overshoot —
   the failure mode that stalled movement at 150×. Followers beyond
   `CouplingRadius` (60, against a ~10 unit follow distance) have lost contact
   and do not couple.
2. **Shared mission pulse.** Turning in a contract publishes a `SquadMoraleEvent`
   worth `MissionShare` (4, against the earner's own 8) to every living
   squadmate but the earner. `SocialSystem` buffers and drains it on tick, the
   same pattern as the campfire aura.

New `SquadMoraleOptions` (`STALKER_SQUAD_MORALE_COUPLING`, `_RADIUS`,
`STALKER_SQUAD_MISSION_SHARE`) follows the `CampfireOptions` / `EmissionOptions`
pattern. Setting coupling to 0 disables it, which is how the pulse is tested in
isolation.

**Result** — followers, 5-minute run at `TimeFactor=150`:

| | before | after |
|---|---|---|
| follower avg morale | 43 | **92** |
| follower max morale | 70 *(= spawn default)* | **100** |

And squads now behave like squads:

| measure | value | meaning |
|---|---|---|
| avg **within**-squad spread | **2** | a squad shares one mood |
| **between**-squad mean range | **0–100** | squads genuinely differ from each other |

That is the shape the design wants: internally coherent, externally varied. The
final report now carries both numbers, since a small between-squad spread would
mean morale had stopped carrying information.

**Open follow-up — morale now saturates.** Population average has gone from ~68
to ~86, with 100 common. Morale has many sources (mission +8, squad share +4,
loot +12, rest +8, trade, cooking, campfire) and exactly one weak sink: decay
that only runs while hunger, thirst or fatigue is already above 60. It is no
longer a useful signal at the top of its range, and `GoalSocialise` — which
needs morale below 75 — still almost never fires. The campfire cluster wants a
sink, or a different trigger, before it will express itself.

Note this also distorts `GoalSocialise`: only planners can select it, and
planners are exactly the group whose morale is already high (avg 85, versus the
goal's threshold of 75). The stalkers who most need a drink are the ones who
can never ask for one.

---

### Audit II remediation — Stage 1 & 2 (2026-09-13)

Full findings and the five-stage plan live in the *Second Zone Audit* artifact.
Recorded here so the repo carries its own account.

**Stage 1 ✅ — measurement first** (`c460282`). `SimulationLoop.RunHeadless(n)`
runs exactly *n* ticks with no timer, so two runs cover the same span of game
time and can be diffed; `STALKER_HEADLESS_TICKS` drives it. Dropped/executed
tick counters now appear in the final report with the effective TimeFactor they
imply — a timed run at 150× reports e.g. *"513 executed, 387 dropped (43.0%) |
effective TimeFactor 85.5"*. `scripts/sim_baseline.py` captures the report's
counters and diffs them against `baselines/default.json`.

*Correction to the audit as first written:* dropped ticks do **not** distort
game-time-normalised measurements. `ZoneDirector` skips the clock advance and
the tick work together, so game time and tick count stay in lockstep. What is
lost is wall-clock throughput. Only the report's real-time figures — deaths per
real minute, the startup/steady-state windows — are affected.

**Stage 2 ✅ — time-scale correctness.**

*Mutants* (`d8b165d`). All three movement sites used fixed per-tick constants
and ignored `gameDelta`, so mutant speed was `12 / TimeFactor` units per game
second against a stalker's flat 4 — equal at the default 3, and 2% at 150. The
per-species `Mutant.Speed` was already set at spawn and never read; movement now
uses it through the clamped `StepToward`. Measured against baseline:

| metric | before | after |
|---|---|---|
| mutants killed | 126 | **179** (+42%) |
| deaths to mutants | 59 | **92** (+56%) |
| deaths to gunfire | 294 | 258 (−12%) |
| total casualties | 356 | 353 (−1%) |

Predation restored; the casualty *mix* shifts without changing how deadly the
Zone is.

*Rate formulas.* `rate × delta` is only a probability while the product stays
under 1, and the 1 Hz bucket passes `1.0 × TimeFactor` — so at 150 a 0.015 rate
evaluated to 2.25 and the betrayal roll always passed. Replaced with
`CombatResolver.EventChance` = `1 − exp(−rate × delta)`, one idiom shared with
the squad-morale coupling. Effect is **inside the noise band** at the current
baseline, as expected: emissions are dormant over a 10-game-hour window at the
12–24 hour cadence, and betrayal is gated narrowly. Validated by unit tests
rather than by the harness — a useful reminder that the harness resolves
double-digit effects, not small ones.

**Stage 3 ✅ — truth in the repo** (`cc8ab29`). `ConvoyManager` + `SupplyConvoy`
deleted (208 lines constructed into a discard and never ticked, while two
documents marked it complete — both corrected). `src/UI` deleted (174 lines;
`HUDManager` was a 14-line placeholder, the console panels superseded by the
browser visualizer). The mission-giver radius turned out to be duplicated at
**five** sites, not four, and now shares `GoapTuning.MissionGiverRadius` — the
planner and the payout gate must agree or a stalker plans around a flag the
payout refuses to honour.

**Stage 4 ✅ — test depth where the bugs were.** `src/Core` was 9.9% across
3,192 lines, and three of the previous session's defects lived there.

| suite | what it pins |
|---|---|
| `ZoneDirectorTests` | bucket frequencies, game-delta vs real-delta, and the **lockstep property** the measurement story rests on |
| `SimulationSnapshotTests` | the threading contract — pins carry copied values, not live references |
| `EmissionTickSystemTests` | shelter saves, Zombified/Monolith exempt, dormant zone harmless |
| `SquadSuccessionTests` | the merge rules, so the O(n²) fix below is provably equivalent |
| `SimulationSettingsTests` | env overrides, CORS scoped rather than `*` |

Coverage **32.4% → 41.3%** overall, `src/Core` **9.9% → 25.2%**; CI floor raised
26 → 38 and verified to bite (fails at 45, passes at 38).

Targeted analyzer pass on the two findings that were real, leaving
`AnalysisMode` at Default: `SquadSuccession.FindMergeTarget` called
`allStalkers.Count(...)` *inside* a loop over `allStalkers` — O(n²) on every
leader death, and deaths run to several hundred a session — now precomputes
squad sizes once; `LeaderboardSerializer` caches its `JsonSerializerOptions`.
The remaining CA1851/CA1869 sites are one-shot startup loads and the
once-per-run report path, so they stay.

**Method note — a mistake worth recording.** The first baseline was captured by
a background job while the tree was still being edited and rebuilt, so its three
runs did not all use the same binary. It then flagged a phantom 7% morale
regression against a behaviour-preserving refactor. `sim_baseline.py` now
fingerprints the built DLL and aborts if it changes mid-capture. The refactor's
equivalence was established with `SquadSuccessionTests` instead — the right tool
for the question, since a stochastic sim run could never have settled it.

**Stage 5.1 ✅ — morale is a signal again.** Two sinks, both tied to events the
setting already produces: `CombatBalanceConfig.CombatStressMorale` (−2.5 to the
survivor of any firefight) and `SquadMoraleOptions.SquadmateLossPenalty` (−9 to
each living squadmate, published from `KillTracker.RecordKill` — the one point
every stalker death passes through).

Measuring the sinks exposed a bug in the *previous* stage's work. Instrumenting
`AdjustMorale` showed **+323,800 gained against 270,220 wasted at the 100
ceiling** — 83% of all morale evaporating — and the culprit was the squad
coupling rate. `LeaderCouplingPerGameSec = 0.02` is a half-life of 35 game
*seconds*, but the 1 Hz bucket hands out `1.0 × TimeFactor` game seconds per
tick — 150 at TimeFactor 150. Followers closed **95% of the gap every tick**, so
coupling was a snap rather than a drift: it pinned every follower to its leader
and erased each sink before it could register. Exactly the trap that produced
the movement and rate-formula bugs — a constant tuned at TimeFactor 3 meaning
something else entirely at 150. Retuned to a 15-game-minute half-life
(`ln 2 / 900 ≈ 0.00077`) and pinned by `SquadCouplingRateTests`.

| | before Stage 5 | after |
|---|---|---|
| within-squad morale spread | **0** | **9** |
| follower avg morale | 86 | **80** |
| planners under 75 | — | 20 of 101 |
| population avg | 89 | 85 |

Morale now varies by squad and by fortune instead of sitting pinned at the
ceiling, and the campfire cluster does fire — but rarely (0–1 drinks per
10 game-hours). The remaining blocker is structural and already documented:
only planners can select `GoalSocialise`, and planners are the ones earning
mission rewards, so they stay near the top of the range. **The stalkers who most
need a drink are the ones who can never ask for one.** Closing that needs the
`ShouldPlan` work, measured at 1.8× over the 1 Hz tick budget, which remains its
own phase.

**Still open:** Phase 6A step 6 (`PersonalMemory`), Phase 6B (perception), and
the `ShouldPlan` / follower-planning question.

---

### Phase 7 — Scale (started 2026-09-13)

Target set to the design doc's **750 stalkers / 500 mutants**, replacing the
1500/1000 the code carried, which the loop cannot reach.

**7.1 ✅ `TickProfiler`** — per-system wall-clock attribution in the final
report. Built first because the loop measured ~89 ms/tick against a 100 ms
budget and nobody knew where it went.

**Guard added after a self-inflicted hour.** A profiling run silently became an
unbounded server run: `STALKER_HEADLESS_TICKS` failed to reach the process,
`HeadlessTicks` came back null, and execution fell through to the timer with no
stop condition. It ran 58 minutes and ~27,000 ticks before anyone noticed,
because nothing ever stated which mode it was in. Now `Program.Main` prints the
resolved mode, `STALKER_MODE=headless` without a tick count is a fatal error
rather than a fall-through, `RunHeadless` rejects a non-positive count, and
`sim_baseline.py` asserts the headless banner appeared and the run reported
completion.

#### The profile — every hypothesis was wrong

Predicted, in order of confidence: O(S²) proximity scans in
`StalkerBehaviourSystem`; `CorpseRegistry.GetEnumerator` copying the whole list
per query; `GoapWorldStateSync`'s O(all POIs) scans. Measured over 1200 headless
ticks at ~62 alive:

| | share | per call |
|---|---|---|
| **`goap:BuildPlan`** | **23.9%** | 1.31 ms × 18,725 |
| `goap:SyncOnReplan` | 3.9% | 0.21 ms × 18,725 |
| `SnapshotBuild` | 1.2% | 10.35 ms × 120 |
| `Needs+GoapReplan` | 0.8% | 7.12 ms × 120 |
| `goap:IsValid` | 0.4% | 0.01 ms × 56,033 |
| scanStalkers / scanMutants / scanCorpses | ~0.2% | **0.00 ms** |

The proximity scans cost nothing. `CorpseRegistry`'s copying is real but shows
up under `MutantBehaviourSystem` at 0.0%. **A\* planning is the tick budget.**

**Root cause is plan churn, not planner speed alone.** 18,725 plan builds over
1200 ticks with ~62 stalkers is one new plan per stalker every 4 ticks —
stalkers complete a goal and immediately replan, roughly 2.5 times a second
each. Two independent angles:
1. *Why so much churn* — likely single-action plans that complete on the tick
   they start, so the runtime falls straight back into `BuildPlan`.
2. *Why each plan is slow* — `GOAPPlanner.BuildPlan` allocates a fresh
   `Dictionary<string,bool>` and `List<GOAPAction>` for every action tried at
   every node, up to 500 iterations × 19 actions. Most are discarded
   immediately.

**7.2 ✅ — the planner asked the expensive question first.** Three fixes, in the
order I tried them, with what each was actually worth:

| change | 600 headless ticks |
|---|---|
| *(start)* | ~24 s |
| cache `Preconditions`/`Effects` (they allocated a fresh dictionary per probe) | 23 s — **3%** |
| `GetStalker` linear scan → dictionary; 10× `OrderBy(...).First()` → `MinBy` | 17.5 s — **28%** |
| **reorder the planner's two filters** | **4.9 s — 5× overall** |

The last one is a single reordering. `GOAPPlanner` tested `action.IsValid(bb)`
*before* checking whether the action's effects could contribute to the goal at
all. Both filters are required and neither depends on the other, but usefulness
is two dictionary probes against a cached set, while `IsValid` on a travel
action resolves a destination — a scan over every POI in the world. The planner
was asking the expensive question about every action, including the ~90% that
could not possibly help.

`goap:BuildPlan` fell from **2.50 ms to 0.04 ms** a call, 29% of the tick budget
to 2.6%; the whole tick went **40.7 ms → 8.2 ms** against a 100 ms budget.

Behaviour-preserving by construction: the two filters are ANDed, so the set of
expanded actions is identical. The one side effect in that path —
`ActionRestAtPOI.ResolveTarget` writing `Action.RestValue` — is overwritten by
`Enter` before the action ever runs, and `TryEvaluateDestination` already
saves and restores `GoapTargetPoiId`.

**Note the shape of it.** Allocation, the thing I'd have optimised on instinct,
was worth 3%. The win was an ordering mistake that no amount of reading found —
only the profiler pointed at it, and only after two levels of drilling.

**7.3 ✅ — the population ceiling was lethality, not performance.** With the
tick 5x cheaper, a steady-state run (7200 ticks, past the spawn ramp) still
settled at **74 alive against a 750 target**. The inbound budget of 744 was
fully delivered; 675 of them died. Cause: **stalkers had no health**. Combat was
a single roll and the loser died on the spot — 974 encounters, 675 deaths.
Mutants had carried `Health`/`MaxHealth`/`Damage` since the beginning and combat
never read those either, exactly like `Mutant.Speed` before Phase 7.2.

Combat is now attritional. The roll decides who lands the hit; damage decides
who dies. Weapon damage (18-250, median 40) and armour mitigation both already
existed and are now actually consulted, scaled so a fight is several exchanges.

| | before | after |
|---|---|---|
| alive at steady state | 74 | **423** (peak 431) |
| casualties | 675 | 327 |
| lethality per exchange | 100% | **8%** (5,907 exchanges, 452 fatal) |
| missions completed | 581 | 759 |
| tick cost | 13.1 ms @ 74 | 43.9 ms @ 423 |

**Telemetry corrected alongside.** The combat counters only fire on a kill, so
the moment combat became attritional "combat encounters" silently changed
meaning from *how much fighting* to *how much of it was fatal*. Added an
exchange counter and the report now states both, with the lethality rate.

**Next, now that population is real** — the deferred performance question. At
423 alive the 1 Hz tick is the problem: `SnapshotBuild` 78.3 ms and
`Needs+GoapReplan` 77.1 ms per call, so that one tick in ten runs well over the
100 ms budget while the 10 Hz ticks sit comfortably inside it. Also still open:
wounded behaviour (`Stalker.IsWounded` exists and nothing reads it yet), healing
via consumables, and whether follower planning is affordable now.

**Note the pattern.** Three times today a confident reading of the code was
contradicted by measurement — the socialising coefficient, the morale sinks
erased by my own coupling constant, and now the entire performance hypothesis.
The profiler earned its place before a single optimisation was written.

---

### Deliberately *not* in Phase 6

- **`TaskManager`** (emergent needs-driven contracts) — overlaps the live
  `MissionRegistry`; needs a reconciliation decision first, not just wiring.
- **`ActionMutantFeedOnCorpse`** — feeding already works via inline logic; this
  only GOAP-ifies it. Low value.
- **`Squad` object model / `SquadOrders`** — squads today are `SquadId` flags
  (22 refs) + `SquadSuccession`. Adopting the object model is a deeper refactor;
  it gates `ScientistEscortMission` and belongs in its own phase.
- **`src/UI/`** — not a future feature. `HUDManager` is a 14-line placeholder;
  `InspectorPanel`/`PDAInterfacePanel` are superseded by the browser
  visualizer. Recommend deletion as separate cleanup.
