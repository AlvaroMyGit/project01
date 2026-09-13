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
import argparse, hashlib, json, os, pathlib, re, subprocess, sys, statistics

ROOT = pathlib.Path(__file__).resolve().parent.parent
BASELINE_DIR = ROOT / "baselines"

# A headless run of the configured length should take single-digit minutes. If
# it exceeds this it is not slow, it is wrong — kill it rather than let it sit.
RUN_TIMEOUT_SEC = 900

# Fixed settings — changing these invalidates the baseline, so they live here
# rather than in the caller's shell.
ENV = {
    # Declares intent, so the app refuses to fall through to an unbounded server
    # run if the tick count fails to arrive. A misconfigured capture once ran for
    # an hour as a live server without anyone noticing.
    "STALKER_MODE": "headless",
    "STALKER_TIME_FACTOR": "150",
    # 7200 ticks = 720 simulated-real-seconds, which is the full initial-spawn
    # window. At 2400 the run ended a third of the way through the ramp, so
    # every absolute number was taken while the world was still filling up.
    "STALKER_HEADLESS_TICKS": "2400",  # profiling pass; 7200 for a steady-state baseline
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


def build_fingerprint() -> str:
    """Identity of the binary the runs will load.

    Learned the hard way: a capture left running in the background while the
    source was still being edited produced three runs across two different
    builds, and the resulting baseline flagged a phantom regression. Every run
    in a capture must load the same binary, so the fingerprint is recorded and
    re-checked between runs.
    """
    dll = ROOT / "bin" / "Debug" / "net8.0" / "StalkerALifeSandbox.dll"
    if not dll.exists():
        return "missing"
    return hashlib.sha256(dll.read_bytes()).hexdigest()[:12]


def run_once() -> dict:
    env = {**os.environ, **ENV}

    # Capture stdout rather than discarding it: the run has to PROVE it went
    # headless. Silently becoming a server run is the failure this guards.
    proc = subprocess.run(
        ["dotnet", "run", "--no-build", "-c", "Debug"],
        cwd=ROOT, env=env, capture_output=True, text=True,
        timeout=RUN_TIMEOUT_SEC)

    if proc.returncode != 0:
        raise RuntimeError(
            f"sim exited {proc.returncode}\n{proc.stderr[-2000:]}")
    if "[Mode] HEADLESS" not in proc.stdout:
        raise RuntimeError(
            "the run did not enter headless mode — it would not have terminated "
            f"on its own.\nstdout head:\n{proc.stdout[:600]}")
    if "[Mode] HEADLESS run complete" not in proc.stdout:
        raise RuntimeError("headless run started but did not report completion")

    log = max((ROOT / "logs").glob("sim_*.log"), key=lambda p: p.stat().st_mtime)
    text = log.read_text(errors="replace")
    report = text[text.index("FINAL REPORT"):] if "FINAL REPORT" in text else text

    out = {}
    for key, pat in PATTERNS.items():
        m = re.search(pat, report)
        out[key] = int(m.group(1)) if m else None
    return out


def summarise(runs: list) -> dict:
    """Mean plus observed spread.

    The spread is the point. The sim still uses Random.Shared, so counters move
    run to run by double digits on population metrics — without a recorded
    noise band a reader cannot tell a real effect from a reroll.
    """
    out = {}
    for k in runs[0].keys():
        vals = [r[k] for r in runs if r[k] is not None]
        if not vals:
            out[k] = None
            continue
        out[k] = {
            "mean": round(statistics.mean(vals), 1),
            "min": min(vals),
            "max": max(vals),
        }
    return out


def mean_of(entry):
    return entry["mean"] if isinstance(entry, dict) else entry


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--capture", action="store_true", help="write the result as the new baseline")
    ap.add_argument("--repeat", type=int, default=1, help="runs to average (the sim is not fully deterministic)")
    ap.add_argument("--name", default="default")
    args = ap.parse_args()

    BASELINE_DIR.mkdir(exist_ok=True)
    path = BASELINE_DIR / f"{args.name}.json"

    fingerprint = build_fingerprint()
    print(f"binary {fingerprint}")

    runs = []
    for i in range(args.repeat):
        print(f"run {i + 1}/{args.repeat} ...", flush=True)
        runs.append(run_once())
        if build_fingerprint() != fingerprint:
            print("\nABORT: the binary changed mid-capture — rebuild finished while "
                  "runs were in flight, so these runs are not comparable. "
                  "Re-run with the tree quiet.")
            return 2

    current = summarise(runs)
    current["_binary"] = fingerprint
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
    print(f"\n{'metric':<22}{'baseline':>10}{'current':>10}{'delta':>13}  verdict")
    print("-" * 68)
    for k in PATTERNS:
        be, ce = base.get(k), current.get(k)
        b, c = mean_of(be), mean_of(ce)
        if b is None or c is None:
            print(f"{k:<22}{'?':>10}{'?':>10}")
            continue
        d = c - b
        pct = f"{d / b * 100:+.0f}%" if b else ("--" if d == 0 else "new")

        # Signal only if the move clears the noise seen across repeats of BOTH
        # the baseline and this run. Anything inside that band is a reroll.
        band = 0.0
        for e in (be, ce):
            if isinstance(e, dict):
                band = max(band, (e["max"] - e["min"]) / 2)
        verdict = "" if abs(d) <= band else ("SIGNAL" if abs(d) > band * 2 else "maybe")
        print(f"{k:<22}{b:>10}{c:>10}{f'{d:+g} ({pct})':>13}  {verdict}")
    print("\n(blank verdict = inside the run-to-run noise band; raise --repeat to tighten it)")
    return 0


if __name__ == "__main__":
    sys.exit(main())
