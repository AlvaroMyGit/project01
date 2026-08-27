#!/usr/bin/env python3
"""Export GAMMA outfits/helmets into sim-ready JSON (optional; runtime catalog also registers them)."""

import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
GAMMA = ROOT / "data" / "gamma"
OUT = ROOT / "data" / "items"


def parse_cost(item: dict) -> float:
    raw = item.get("st_upgr_cost", "5000")
    try:
        return max(800.0, min(85000.0, float(raw) * 0.15))
    except ValueError:
        return 5000.0


def community_to_patch(community: str) -> str | None:
    return {
        "dolg": "Duty",
        "freedom": "Freedom",
        "bandit": "Bandit",
        "ecolog": "Ecologist",
        "killer": "Mercenary",
        "monolith": "Monolith",
        "csky": "Clear Sky",
        "army": "Military",
        "renegade": "Renegade",
        "greh": "Sin",
        "isg": "UNISG",
    }.get(community)


def export_category(source: Path, prefix: str, category: str) -> list[dict]:
    data = json.loads(source.read_text())
    rows = []
    for item in data.get("items", []):
        gid = item["id"]
        community = (item.get("ui_st_community") or "stalker").lower()
        row = {
            "id": f"{prefix}_{gid}",
            "name": gid.replace("_", " ").title(),
            "baseValue": parse_cost(item),
            "category": category,
            "gammaId": gid,
        }
        patch = community_to_patch(community)
        if patch and category == "Armor":
            row["factionPatch"] = patch
        rows.append(row)
    return rows


def main() -> None:
    OUT.mkdir(parents=True, exist_ok=True)
    armors = export_category(GAMMA / "outfits.json", "out", "Armor")
    helmets = export_category(GAMMA / "helmets.json", "helm", "Helmet")
    (OUT / "gamma_armors.json").write_text(json.dumps(armors, indent=2))
    (OUT / "gamma_helmets.json").write_text(json.dumps(helmets, indent=2))
    print(f"Wrote {len(armors)} armors and {len(helmets)} helmets to {OUT}")


if __name__ == "__main__":
    main()
