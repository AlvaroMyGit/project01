# Next-Phase Feature Brainstorm: S.T.A.L.K.E.R. A-Life Open-World Sandbox

> **Companion doc to:** `IMPLEMENTATION_PLAN.md` (v4.5)
> **Status:** Raw brainstorm — not yet scoped, prioritized, or broken into tasks
> **Purpose:** Candidate feature pool for the next implementation phase. Each idea below is a *starting point* for an agent to scope out (define files touched, data changes, sim-loop hooks, effort) before implementation begins.
> **Context:** As of v4.5, the sandbox is "100% aligned with v4.0 design intent" per the honest execution checklist — GOAP AI, factions, economy, missions, emissions, and a live PixiJS visualizer are all wired into the sim loop. This doc looks past that baseline toward what makes the world feel more alive, more player-relevant, and more systemically deep.

---

## 0. Known Loose Threads (library code that exists but isn't wired in)

These aren't new ideas — they're existing classes sitting unused in the sim loop. Cheapest wins in the whole list; wiring them up doesn't require new design, just integration work.

| Item | File(s) | What's missing |
|---|---|---|
| Field crafting | `src/Crafting/FieldCraftingSystem.cs` | Never called from `SimulationLoop` / macro tick |
| Mutant cooking | `src/Crafting/MutantCookingSystem.cs` | Class exists; not in sim loop |
| Perception-based combat | `src/AI/Perception/VisionCone.cs`, `AcousticSensor.cs` | Combat is still pure proximity; these aren't consulted |
| PDA task UI | `src/PDA/TaskManager.cs`, `src/UI/PDAInterface/PDAInterfacePanel.cs` | Not in sim loop |
| True hierarchical pathfinding | `HierarchicalNav.cs` (macro graph exists) | Sim still uses grid A* via `ZonePathfinder`; no `MacroNode`/`PortalNode` |
| Nocturnal underground sleep | — | Flagged `[ ]` in plan, never enforced |
| Dynamic leaderboard note tags | — | Flagged `[ ]` in plan |
| Personal memory / grudges | `src/Factions/PersonalMemory.cs` | Exists but has no gameplay payoff yet — see §1 |

---

## 1. Living World / Emergent Narrative

**Theme:** The sim already tracks huge amounts of telemetry (kills, ranks, betrayals, missions) but doesn't turn it into *story*. This theme is about surfacing emergent narrative.

- **Faction warfare with real stakes** — squads capture/lose POIs; a territory map shifts over time based on squad strength at contested regions. Currently the 12×12 hostility matrix (`FactionMatrix.cs`) only gates combat — nothing changes on the map as a result.
- **Legendary stalkers** — a stalker crossing kill/emission-survival thresholds gets a name/title, unique PDA chatter, maybe a portrait/sprite variant. Gives the Top 100 leaderboard emotional weight beyond a score.
- **Rumor / intel economy** — PDA chatter propagates (partially wired already via `PDANetwork.cs` + `GoapWorldStateSync.cs`) but rumors are always true. Add false/stale rumors that GOAP plans around anyway — creates misinformation, wasted trips, paranoia.
- **Grudges and alliances** — persistent memory between *individual* stalkers, not just factions. `PersonalMemory.cs` already exists as a class — this is its natural payoff: "Wolf killed my squadmate, I hunt him now."
- **Chronicle / history log** — an auto-generated "Zone history" feed narrating emissions survived, faction rises/falls, notable deaths, betrayals. Lightweight storyteller layer reading off `EventBus`.

---

## 2. Player Agency

**Theme:** Right now the visualizer is pure spectator. This theme introduces direct or indirect player control.

- **Possess a stalker** — step out of spectator mode into direct control of one NPC; issue orders to their squad while everyone else keeps simulating.
- **Faction commander mode** — give high-level orders to a faction ("hold Garbage," "raid Yantar stash") and watch GOAP-driven squads execute.
- **Drop events** — player manually triggers an emission, spawns a mutant pack, or airdrops loot into a region and observes the population's reaction.

---

## 3. Economy & Survival Depth

**Theme:** The economy loop exists (traders, convoys, mission payouts) but is fairly static. This theme adds friction and consequence.

- **Weapon degradation & maintenance** — durability loss on use, jamming, repair kits, gunsmith trader specialization. `FieldCraftingSystem` (see §0) is the natural home for this.
- **Supply chain fragility** — convoys (`ConvoyManager.cs`) can actually get ambushed and fail to deliver, causing real trader stock shortages instead of static `MarketPrices`.
- **Artifact market speculation** — prices fluctuate based on how many of a given artifact type are currently "alive" in the sim (scarcity-driven pricing instead of fixed `baseValue`).
- **Radiation/mutation as a slow-burn stat** — long-term exposure causes visible mutation effects or forced retirement, instead of just being an instant-death stat in `SurvivalNeeds.cs`.

---

## 4. AI / Behavior Depth

**Theme:** Combat and squad behavior currently resolve on proximity + stats. This theme makes positioning and perception matter.

- **Stealth & detection gameplay** — wire `VisionCone`/`AcousticSensor` (see §0) so combat isn't pure proximity: sneaking, ambushes, patrol routes reacting to noise.
- **Squad tactics using terrain** — leverage the existing 759 building footprints (`BuildingFootprintLoader.cs`) for cover-seeking, flanking, suppressive positioning instead of squads walking straight at each other.
- **Dynamic morale** — squads route around fights they're likely to lose based on *perceived* enemy strength (scouted/rumored), not just a raw threat number.
- **Environmental storytelling AI** — corpse/loot placement creates readable "something happened here" scenes other NPCs react to (partially there via `ActionInvestigateCorpse.cs` — could go further).

---

## 5. Visualizer / Meta Layer

**Theme:** The WebSocket telemetry (`WebVisualizerServer.cs`) already broadcasts everything live — this theme is about doing more with that stream.

- **Timelapse/replay scrubber** — log broadcast telemetry and let the user scrub back through a run like a DVR.
- **Population & econ analytics dashboard** — faction-strength-over-time graphs, death-cause heatmaps, RU circulation charts.
- **Squad/social link overlay** — visualize friend/rival/faction bonds as lines at micro zoom (already flagged as a "suggested next sprint" item in the existing plan).
- **Heat/danger map overlay** — recent-combat density rendered as a live heatmap so a viewer can "read" the Zone at a glance.

---

## 6. Systemic / Meta

**Theme:** Infrastructure that isn't a "feature" per se but unlocks everything else.

- **Save/load persistent world** — snapshot full sim state (stalkers, factions, territory, economy) so a campaign can resume across sessions instead of resetting every run. Currently only `data/leaderboard.json` persists.
- **Modding/plugin API** — the sim is already JSON-data-driven (`data/*.json` + `ItemDatabase.cs`); formalize a plugin loader so factions/items/mutants can be added without touching core code.
- **Scenario scripting** — designer-authored "events" (convoy ambush, faction war trigger, artifact rush) that can be scripted and replayed for balance testing.

---

## 7. Natural Pairings

Some ideas reinforce each other and are worth scoping as a single combined effort rather than separately:

| Pairing | Why |
|---|---|
| Legendary stalkers + Chronicle log | Chronicle log is the delivery mechanism for legendary-stalker moments |
| Stealth/detection + Squad tactics (terrain) | Both depend on perception + building-footprint awareness; share groundwork |
| Faction territory control + Population analytics dashboard | Territory shifts are best understood visually over time |
| Save/load + Scenario scripting | Scripted scenarios need a state snapshot format anyway |
| Weapon degradation + Field crafting wiring | Repair is the crafting system's most obvious use case |

---

## 8. Suggested Next Step for the Agent

This document is intentionally **unscoped** — no file-level task breakdowns, no effort estimates, no phase numbers. Before implementation:

1. Pick one theme (or one pairing from §7).
2. Audit which existing classes/data files it touches (cross-reference `IMPLEMENTATION_PLAN.md` §2 directory hierarchy).
3. Produce a phased task breakdown in the same style as `IMPLEMENTATION_PLAN.md` §5 (Prioritized Wiring Roadmap), including:
   - Files to modify/create
   - Data schema changes (`data/*.json`)
   - Sim-loop tick frequency the new system should run at (10 Hz / 1 Hz / 0.1 Hz)
   - Visualizer/telemetry changes needed to observe the new behavior
   - A validation run plan (mirroring the existing doc's "Validation run" sections) to confirm the feature is actually emergent in a live sim, not just present in code.