#!/usr/bin/env python3
"""Capture or compare a simulation baseline.

Why this exists: a timed run cannot be compared with another. It drops a
variable number of ticks depending on machine load and population, so two runs
of the same wall-clock length simulate different amounts of the world. This
drives the headless fixed-tick mode instead, where the tick count is the input,
so every run covers exactly the same span of game time.

  capture:  scripts/sim_baseline.py --capture            # writes baselines/default.json
  compare:  scripts/sim_baseline.py                      # runs and diffs against it

The sim still uses Random.Shared in places, so counters vary run to run. Treat a
small diff as noise and a large one as signal; --repeat gives a spread to judge
against.
"""
import argparse, json, os, pathlib, re, subprocess, sys, statistics

ROOT = pathlib.Path(__file__).resolve().parent.parent
BASELINE_DIR = ROOT / "baselines"

# Fixed settings — changing these invalidates the baseline, so they live here
# rather than in the caller's shell.
ENV = {
    "STALKER_TIME_FACTOR": "150",
    "STALKER_HEADLESS_TICKS": "2400",   # 10.00 game-hours at TF=150
    "STALKER_REST_PORT": "8123",
}

# label -> (regex, group count). Every metric is game-time normalised or a raw
# count over a fixed tick span, so all of them are comparable between runs.
PATTERNS = {
    "stalkers_alive":     r"Population: stalkers (\d+) alive",
    "stalkers_peak":      r"alive \(peak (\d+)",
    "mutants_alive":      r"\| mutants (\d+) alive",
    "combat_total":       r"Combat encounters: (\d+) total",
    "combat_vs_mutant_w": r"vs mutant: W=(\d+)",
    "combat_vs_mutant_l": r"vs mutant: W=\d+ L=(\d+)",
    "mutants_killed":     r"mutants killed=(\d+)",
    "casualties_total":   r"Stalker casualties: (\d+) total",
    "death_gunfire":      r"gunfire=(\d+)",
    "death_mutant":       r"gunfire=\d+ mutant=(\d+)",
    "death_emission":     r"emission=(\d+)",
    "death_betrayal":     r"betrayal=(\d+)",
    "missions_accepted":  r"Missions: accepted=(\d+)",
    "missions_completed": r"Missions: accepted=\d+ completed=(\d+)",
    "morale_avg":         r"Morale \(alive\): avg=(\d+)",
    "morale_planners":    r"planners \[n=\d+ avg=(\d+)",
    "morale_followers":   r"followers \[n=\d+ avg=(\d+)",
    "squad_spread":       r"avg within-squad spread (\d+)",
    "drinks":             r"Socialising: drinks=(\d+)",
    "emission_storms":    r"Emission storms: (\d+)",
    "goap_tasks":         r"GOAP tasks completed: (\d+)",
    "goap_replans":       r"GOAP replans \(1Hz\): (\d+)",
    "rank_promotions":    r"Rank promotions: (\d+)",
    "ticks_dropped":      r"Ticks: \d+ executed, (\d+) dropped",
}


def run_once() -> dict:
    env = {**os.environ, **ENV}
    subprocess.run(
        ["dotnet", "run", "--no-build", "-c", "Debug"],
        cwd=ROOT, env=env, check=True,
        stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)

    log = max((ROOT / "logs").glob("sim_*.log"), key=lambda p: p.stat().st_mtime)
    text = log.read_text(errors="replace")
    report = text[text.index("FINAL REPORT"):] if "FINAL REPORT" in text else text

    out = {}
    for key, pat in PATTERNS.items():
        m = re.search(pat, report)
        out[key] = int(m.group(1)) if m else None
    return out


def summarise(runs: list) -> dict:
    keys = runs[0].keys()
    return {k: round(statistics.mean([r[k] for r in runs if r[k] is not None]), 1)
            if any(r[k] is not None for r in runs) else None
            for k in keys}


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--capture", action="store_true", help="write the result as the new baseline")
    ap.add_argument("--repeat", type=int, default=1, help="runs to average (the sim is not fully deterministic)")
    ap.add_argument("--name", default="default")
    args = ap.parse_args()

    BASELINE_DIR.mkdir(exist_ok=True)
    path = BASELINE_DIR / f"{args.name}.json"

    runs = []
    for i in range(args.repeat):
        print(f"run {i + 1}/{args.repeat} ...", flush=True)
        runs.append(run_once())
    current = summarise(runs)
    current["_settings"] = ENV
    current["_repeat"] = args.repeat

    if args.capture:
        path.write_text(json.dumps(current, indent=2) + "\n")
        print(f"\nbaseline written to {path.relative_to(ROOT)}")
        return 0

    if not path.exists():
        print(f"no baseline at {path.relative_to(ROOT)} — run with --capture first")
        return 1

    base = json.loads(path.read_text())
    print(f"\n{'metric':<22}{'baseline':>10}{'current':>10}{'delta':>12}")
    print("-" * 54)
    for k in PATTERNS:
        b, c = base.get(k), current.get(k)
        if b is None or c is None:
            print(f"{k:<22}{'?':>10}{'?':>10}{'':>12}")
            continue
        d = c - b
        pct = f"{d / b * 100:+.0f}%" if b else ("--" if d == 0 else "new")
        print(f"{k:<22}{b:>10}{c:>10}{f'{d:+g} ({pct})':>12}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
