# S.T.A.L.K.E.R. A-Life Open-World Sandbox

A fully autonomous open-world life simulation inspired by the A-Life system from the S.T.A.L.K.E.R. game series. Hundreds of AI-driven stalkers and mutants live, fight, trade, and die across a procedurally generated Chernobyl Exclusion Zone — all observable in real-time through an interactive browser-based visualizer.

> **This is not a playable game in the traditional sense.** It is a living simulation that you *spectate*. Think of it as an ant farm set in the Zone — you watch emergent stories unfold as autonomous agents pursue their own goals, form squads, betray allies, survive emissions, hunt artifacts, and climb the ranks.

---

## ✨ Key Features

### 🤖 Autonomous AI Agents
- **Up to 750 stalkers** across 12 canonical factions (Loners, Duty, Freedom, Military, Bandits, Ecologists, Monolith, Clear Sky, Mercenaries, Renegades, UNISG, Sin) with individual names, skills, equipment, and survival needs
- **Up to 500 mutants** spanning 17 G.A.M.M.A.-accurate species (Boar, Flesh, Dog, Blind Dog, Pseudodog, Snork, Bloodsucker, Controller, Burer, Chimera, Pseudogiant, and more)
- **GOAP (Goal-Oriented Action Planning)** — stalkers autonomously decide what to do based on hunger, fear, greed, loyalty, and mission contracts
- **Squad dynamics** — leader-follower formations, leader succession on death, squad merging, and betrayal mechanics
- **Shared squad morale** — a squad's mood follows its leader's fortunes, lifted by contracts turned in and dragged down by members lost, so squads visibly differ from one another

### 🗺️ Living World
- **36 map regions** (28 surface + 8 underground) based on canonical S.T.A.L.K.E.R. geography, from the safe Cordon in the south to deadly Pripyat in the north
- **~700 stamped POIs** — macro bases, micro-shelters, stash locations, campfires, labs, and anomaly fields
- **~450 procedural wilderness shelters** scattered between road corridors
- **~759 building footprints** acting as pathfinding blockers and providing roof cutaway visuals
- **Road network** connecting all regions with curved travel corridors
- **Multi-layer navigation** — surface, underground labs (Layer -1), and building interiors (Layer +1)

### ⚔️ Combat & Equipment
- **Attritional combat** — stalkers and mutants both have health, and a firefight is several exchanges rather than one roll. About 8% of exchanges are fatal; the rest leave someone wounded
- **Skill-based hit resolution** factoring in rank, weapon stats, armor protection, sniper range bonuses, heavy weapon suppression, squad ally bonuses, and weapon condition/jamming
- **161 G.A.M.M.A. outfits, 21 helmets, 30 weapons** with full stats (damage, accuracy, fire rate, magazine size)
- **9-channel protection system** — bullet, slash, radiation, burn, shock, chemical, psi, strike, explosion
- **Corpse looting** — dead stalkers drop gear snapshots that others can loot for upgrades
- **Faction-specific spawn loadouts** scaled by rank tier

### 🌩️ Environmental Hazards
- **Emissions (Blowouts)** — 4-phase events (Warning → Panic → Peak → Aftermath) with 70% lethality / 30% zombification for unsheltered stalkers, arriving every 12–24 game hours like a GAMMA surge rather than as weather
- **Anomaly fields** — 39 real-world Chernobyl locations (Kopachi, Red Forest, Duga, Pripyat) plus dynamic wilderness spawns
- **Scientist forecasting** — Ecologist researchers broadcast multi-stage PDA warnings before emissions hit
- **Dynamic weather** — Clear, Overcast, Rain, Fog, Storm cycles affecting visibility and acoustics

### 💰 Economy & Missions
- **Trader network** with dynamic supply/demand pricing across macro bases
- **Mission system** — stalkers accept contracts (scout POI, retrieve stash, escort convoy), travel to objectives, complete tasks, return to the issuer, and collect payment
- **Artifact hunting** — detector-equipped stalkers venture into anomaly fields for valuable artifacts to equip or sell

### 📊 Live Visualizer Dashboard
- **PixiJS WebGL renderer** with multi-tier LOD (radar dots → icons → pixel sprites → paperdoll overlays)
- **Real-time WebSocket telemetry** at 10 Hz
- **Interactive inspector** — click any stalker, mutant, or corpse to see full stats, gear, active GOAP goal, and mission status
- **PDA feed** with filtered tabs for deaths, missions, emissions, and faction chatter
- **Top 100 leaderboard** with click-to-follow camera tracking
- **12×12 faction diplomacy matrix** viewer
- **Mission map overlays** — amber lines to objectives, green lines for return trips
- **CSS inventory grid** with paperdoll gear slots

### 📈 RPG Progression
- **4-skill matrix** — Marksmanship, Zone Survival, Charisma, Trustworthiness with diminishing-returns progression
- **8-tier rank system** — Rookie → Trainee → Experienced → Professional → Veteran → Expert → Master → Legend
- **Rank-based zone gating** — rookies stick to safe southern regions; veterans push into dangerous northern territory
- **Kill tracking** with XP awards scaled by rank differential

---

## 🛠️ Tech Stack

| Component | Technology |
|---|---|
| **Engine** | C# / .NET 8 |
| **Serialization** | System.Text.Json |
| **Web Server** | ASP.NET Core (port 5050) — serves the REST API, dashboard, and WebSocket telemetry |
| **WebSocket** | ASP.NET Core WebSocket middleware, `/ws` route (same host/port as above) |
| **Visualizer** | HTML5 / PixiJS v7 / pixi-viewport |
| **Data Format** | JSON |
| **Asset Tooling** | Python (Pillow, rembg) |
| **Testing** | xUnit |

---

## 🚀 Getting Started

### Prerequisites
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- A modern web browser (Chrome, Firefox, Edge)

### Build & Run

```bash
# Clone the repository
git clone <repo-url>
cd project01

# Build the project
dotnet build

# Run the simulation
dotnet run
```

The simulation will start and output diagnostic logs to the console. One ASP.NET Core host serves everything:

| Service | URL | Purpose |
|---|---|---|
| **Visualizer** | `http://localhost:5050` | Browser-based dashboard |
| **REST API** | `http://localhost:5050/api/*` | World, state, leaderboard, faction data |
| **WebSocket** | `ws://localhost:5050/ws` | Real-time telemetry stream |

Open `http://localhost:5050` in your browser to watch the Zone come alive.

### Environment Variables

**Run control**

| Variable | Default | Description |
|---|---|---|
| `STALKER_TIME_FACTOR` | `3.0` | Game time multiplier (higher = faster simulation) |
| `STALKER_REST_PORT` | `5050` | Port for the REST API, dashboard, and `/ws` telemetry |
| `STALKER_RUN_DURATION_SEC` | *(unset)* | Auto-stop after this many real seconds |
| `STALKER_MODE` | *(unset)* | Set to `headless` to require a measurement run; the process refuses to start a timer-driven run instead |
| `STALKER_HEADLESS_TICKS` | *(unset)* | Run exactly this many ticks with no timer, then exit |
| `STALKER_INITIAL_SPAWN_SEC` | `720` | Seconds to stagger initial population spawn (12 min) |
| `STALKER_SPAWN_GRACE_SEC` | `45` | Post-spawn combat immunity window |
| `STALKER_DEBUG_LOG` | `1` | Set to `0` to silence the run log |

**Tuning** — every one of these has an immutable options record behind it, so the defaults live in one documented place rather than scattered as literals.

| Variable | Default | Description |
|---|---|---|
| `STALKER_EMISSION_MIN_SEC` / `_MAX_SEC` | `43200` / `86400` | Gap between blowouts, in game seconds (12–24 game hours) |
| `STALKER_EMISSION_WARNING_SEC` | `120` | Siren lead time before the storm |
| `STALKER_EMISSION_PANIC_SEC` / `_PEAK_SEC` / `_AFTERMATH_SEC` | `15` / `30` / `15` | Phase durations |
| `STALKER_CAMPFIRE_SEATS` | `6` | Seats at each campfire |
| `STALKER_CAMPFIRE_RADIUS` | `30` | How close counts as "at" a campfire |
| `STALKER_CAMPFIRE_MICRO_SHARE` | `0.15` | Fraction of micro-shelters that also get a fire |
| `STALKER_SQUAD_MORALE_COUPLING` | `0.00077` | Rate at which a follower's morale converges on their leader's (a 15 game-minute half-life) |
| `STALKER_SQUAD_MORALE_RADIUS` | `60` | Beyond this a follower has lost contact and stops coupling |
| `STALKER_SQUAD_MISSION_SHARE` | `4` | Morale each squadmate gains when a member turns in a contract |
| `STALKER_SQUAD_LOSS_PENALTY` | `9` | Morale each squadmate loses when one of them is killed |
| `STALKER_CORPSE_*` | *(see `CorpseCleanupOptions`)* | Despawn thresholds per corpse state |
| `STALKER_STARTING_RUBLES` | `850` | Rubles each stalker spawns with |

### Running Tests

```bash
dotnet test project01.sln
```

279 xUnit tests at 41.8% line coverage. CI builds with `-warnaserror` and fails the build below a 38% coverage floor, so the gate is a ratchet rather than decoration.

### Measuring the simulation

A timed run cannot be compared with another — the loop drops a variable number of ticks under load, so two runs of the same length simulate different amounts of world. Use the headless harness instead, where the tick count is the input:

```bash
STALKER_MODE=headless STALKER_HEADLESS_TICKS=7200 STALKER_TIME_FACTOR=150 dotnet run -c Release
```

`scripts/sim_baseline.py` wraps that, captures the run's counters as JSON, and diffs them against `baselines/default.json` — flagging whether a change clears the run-to-run noise band rather than leaving you to guess:

```bash
python3 scripts/sim_baseline.py --repeat 3          # compare against the baseline
python3 scripts/sim_baseline.py --capture --repeat 3  # accept the current behaviour as the new baseline
```

---

## 📁 Project Structure

```
StalkerALifeSandbox/
├── Program.cs                    # Entry point, bootstrap, REST API
├── src/
│   ├── Core/                     # Simulation loop, tick scheduler, time, events
│   ├── AI/                       # GOAP planner, actions, goals, perception, squads
│   ├── Systems/                  # Combat, ranks, kills, corpses, skills, debug logging
│   ├── World/                    # Map generation, navigation, weather, hazards, POIs
│   ├── Entities/                 # Stalker, Mutant, Equipment, Needs models
│   ├── Economy/                  # Traders, missions, market prices
│   ├── Factions/                 # Faction matrix, demographics, disguises, memory
│   ├── Crafting/                 # Field crafting, mutant cooking (partially wired)
│   ├── PDA/                      # Communication network, chatter templates
│   └── Web/                      # REST endpoints, WebSocket hub, telemetry DTOs, inspector
├── data/                         # JSON data tables (regions, factions, items, etc.)
├── visualizer/                   # Browser dashboard (HTML + PixiJS)
├── scripts/                      # Python asset & data generation utilities
├── tests/                        # xUnit test suite
└── logs/                         # Simulation run logs
```

> For a detailed breakdown of every file and class, see [CODEBASE.md](CODEBASE.md).

---

## 📊 Simulation Metrics

Averaged over three headless runs of 7,200 ticks — 30 game-hours at `TimeFactor=150`, past the 12-minute spawn ramp, so these are steady-state rather than a world still filling up. Captured by `scripts/sim_baseline.py`; the committed baseline carries the min/max spread alongside each mean.

| Metric | Value |
|---|---|
| Stalkers alive (steady state) | ~412 |
| Mutants alive | ~188 |
| Combat exchanges | ~5840 |
| …of which fatal | ~444 (**8% lethality**) |
| Stalker casualties | ~337 |
| Missions accepted / completed | ~732 / ~727 |
| GOAP tasks completed | ~411178 |
| Rank promotions | ~109 |
| Average morale | ~88 |
| Tick cost | ~13 ms against a 100 ms budget |

Nearly every contract accepted is now seen through to payout, and most firefights end with someone wounded rather than dead. Both took real work: the mission loop previously ran at 260 accepted / 0 completed, and combat was a single roll that killed the loser outright. See [IMPLEMENTATION_PLAN.md](IMPLEMENTATION_PLAN.md) for how.

---

## 🗺️ Roadmap

The core simulation is feature-complete per the v4.5 design. Nearest work first:

- **Wounded behaviour** — `Stalker.IsWounded` exists and nothing reads it yet; wounded stalkers should break contact and seek shelter or a medkit
- **Squad followers that plan** — only squad leaders and solos run GOAP, which is why followers can never earn morale and why the campfire cluster rarely fires. Previously unaffordable at 1.8× over the tick budget; the Phase 7 performance work may have changed that
- **Stealth & perception** — wire `VisionCone` and `AcousticSensor` into detection, replacing the current proximity checks
- **Faction territory warfare** — squads capture and lose POIs, shifting the faction map over time
- **Legendary stalkers** — notable NPCs earn titles and unique PDA presence
- **Player agency** — possess a stalker, issue faction-wide orders, or trigger events
- **Save/load persistence** — resume campaigns across sessions
- **Replay & analytics** — timelapse scrubber, heatmaps, faction-strength graphs

See [next-phase.md](next-phase.md) for the full feature brainstorm.

---

## 📄 License

This project is a fan-made simulation inspired by the S.T.A.L.K.E.R. game series by GSC Game World. All faction names, location names, and lore references are the property of their respective owners. G.A.M.M.A. item data is sourced from the Anomaly G.A.M.M.A. modpack community.
