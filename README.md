# S.T.A.L.K.E.R. A-Life Open-World Sandbox

A fully autonomous open-world life simulation inspired by the A-Life system from the S.T.A.L.K.E.R. game series. Hundreds of AI-driven stalkers and mutants live, fight, trade, and die across a procedurally generated Chernobyl Exclusion Zone — all observable in real-time through an interactive browser-based visualizer.

> **This is not a playable game in the traditional sense.** It is a living simulation that you *spectate*. Think of it as an ant farm set in the Zone — you watch emergent stories unfold as autonomous agents pursue their own goals, form squads, betray allies, survive emissions, hunt artifacts, and climb the ranks.

---

## ✨ Key Features

### 🤖 Autonomous AI Agents
- **~1,500 stalkers** across 12 canonical factions (Loners, Duty, Freedom, Military, Bandits, Ecologists, Monolith, Clear Sky, Mercenaries, Renegades, UNISG, Sin) with individual names, skills, equipment, and survival needs
- **~1,000 mutants** spanning 17 G.A.M.M.A.-accurate species (Boar, Flesh, Dog, Blind Dog, Pseudodog, Snork, Bloodsucker, Controller, Burer, Chimera, Pseudogiant, and more)
- **GOAP (Goal-Oriented Action Planning)** — stalkers autonomously decide what to do based on hunger, fear, greed, loyalty, and mission contracts
- **Squad dynamics** — leader-follower formations, leader succession on death, squad merging, and betrayal mechanics

### 🗺️ Living World
- **36 map regions** (28 surface + 8 underground) based on canonical S.T.A.L.K.E.R. geography, from the safe Cordon in the south to deadly Pripyat in the north
- **~700 stamped POIs** — macro bases, micro-shelters, stash locations, campfires, labs, and anomaly fields
- **~450 procedural wilderness shelters** scattered between road corridors
- **~759 building footprints** acting as pathfinding blockers and providing roof cutaway visuals
- **Road network** connecting all regions with curved travel corridors
- **Multi-layer navigation** — surface, underground labs (Layer -1), and building interiors (Layer +1)

### ⚔️ Combat & Equipment
- **Skill-based combat resolution** factoring in rank, weapon stats, armor protection, sniper range bonuses, heavy weapon suppression, squad ally bonuses, and weapon condition/jamming
- **161 G.A.M.M.A. outfits, 21 helmets, 30 weapons** with full stats (damage, accuracy, fire rate, magazine size)
- **9-channel protection system** — bullet, slash, radiation, burn, shock, chemical, psi, strike, explosion
- **Corpse looting** — dead stalkers drop gear snapshots that others can loot for upgrades
- **Faction-specific spawn loadouts** scaled by rank tier

### 🌩️ Environmental Hazards
- **Emissions (Blowouts)** — 4-phase events (Warning → Panic → Peak → Aftermath) with 70% lethality / 30% zombification for unsheltered stalkers
- **Anomaly fields** — 39 real-world Chernobyl locations (Kopachi, Red Forest, Duga, Pripyat) plus dynamic wilderness spawns
- **Scientist forecasting** — Ecologist researchers broadcast multi-stage PDA warnings before emissions hit
- **Dynamic weather** — Clear, Overcast, Rain, Fog, Storm cycles affecting visibility and acoustics

### 💰 Economy & Missions
- **Trader network** with dynamic supply/demand pricing across macro bases
- **Supply convoys** moving cargo between the southern border and northern outposts
- **Mission system** — stalkers accept contracts (scout POI, retrieve stash, escort convoy), travel to objectives, complete tasks, return to the issuer, and collect payment
- **Artifact hunting** — detector-equipped stalkers venture into anomaly fields for valuable artifacts to equip or sell

### 📊 Live Visualizer Dashboard
- **PixiJS WebGL renderer** with multi-tier LOD (radar dots → icons → pixel sprites → paperdoll overlays)
- **Real-time WebSocket telemetry** at 10 Hz
- **Interactive inspector** — click any stalker, mutant, or corpse to see full stats, gear, active GOAP goal, and mission status
- **PDA feed** with filtered tabs for deaths, missions, emissions, and faction chatter
- **Top 100 leaderboard** with click-to-follow camera tracking
- **12×12 faction diplomacy matrix** viewer
- **Mission map overlays** — gold lines to objectives, green lines for return trips
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

| Variable | Default | Description |
|---|---|---|
| `STALKER_TIME_FACTOR` | `3.0` | Game time multiplier (higher = faster simulation) |
| `STALKER_INITIAL_SPAWN_SEC` | `720` | Seconds to stagger initial population spawn (12 min) |
| `STALKER_SPAWN_GRACE_SEC` | `45` | Post-spawn combat immunity window |

### Running Tests

```bash
dotnet test tests/StalkerALifeSandbox.Tests/
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
│   ├── Economy/                  # Traders, missions, convoys, market prices
│   ├── Factions/                 # Faction matrix, demographics, disguises, memory
│   ├── Crafting/                 # Field crafting, mutant cooking (partially wired)
│   ├── PDA/                      # Communication network, chatter templates
│   ├── UI/                       # Server-side UI renderers
│   └── Web/                      # REST endpoints, WebSocket hub, telemetry DTOs, inspector
├── data/                         # JSON data tables (regions, factions, items, etc.)
├── visualizer/                   # Browser dashboard (HTML + PixiJS)
├── scripts/                      # Python asset & data generation utilities
├── tests/                        # xUnit test suite
└── logs/                         # Simulation run logs
```

> For a detailed breakdown of every file and class, see [CODEBASE.md](CODEBASE.md).

---

## 📊 Simulation Metrics (Sample 30-min Run)

| Metric | Value |
|---|---|
| Game time elapsed | ~1 in-game hour |
| Stalkers alive at end | ~163 |
| Mutants alive at end | ~196 |
| Combat encounters | ~1,088 |
| Gear loot events | ~223 |
| Corpses despawned | ~2,033 |
| Missions accepted | ~174 |
| Missions completed | ~37 |
| Distinct weapons observed in kills | 9+ |
| Rank distribution | Rookie: 133, Trainee: 20, Experienced: 9, Professional: 1 |

---

## 🗺️ Roadmap

The core simulation is feature-complete per the v4.5 design. Active brainstorming areas for future development include:

- **Faction territory warfare** — squads capture and lose POIs, shifting the faction map over time
- **Legendary stalkers** — notable NPCs earn titles and unique PDA presence
- **Player agency** — possess a stalker, issue faction-wide orders, or trigger events
- **Stealth & perception combat** — wire VisionCone and AcousticSensor into combat resolution
- **Save/load persistence** — resume campaigns across sessions
- **Replay & analytics** — timelapse scrubber, heatmaps, faction-strength graphs

See [next-phase.md](next-phase.md) for the full feature brainstorm.

---

## 📄 License

This project is a fan-made simulation inspired by the S.T.A.L.K.E.R. game series by GSC Game World. All faction names, location names, and lore references are the property of their respective owners. G.A.M.M.A. item data is sourced from the Anomaly G.A.M.M.A. modpack community.
