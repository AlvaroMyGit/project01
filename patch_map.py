import json

updates = {
    "Pripyat": {"X": 0.66, "Y": 0.12},
    "Lab X-8": {"X": 0.66, "Y": 0.12},
    "Deserted Hospital": {"X": 0.28, "Y": 0.10},
    "Jupiter (Pripyat Industrial)": {"X": 0.52, "Y": 0.14},
    "Jupiter Underground": {"X": 0.52, "Y": 0.14},
    "Zaton": {"X": 0.35, "Y": 0.08},
    "Limansk": {"X": 0.28, "Y": 0.18},
    "Chernobyl NPP": {"X": 0.66, "Y": 0.06},
    "Sarcophagus": {"X": 0.66, "Y": 0.06},
    "Monolith Control Center": {"X": 0.66, "Y": 0.06},
    "Generators": {"X": 0.50, "Y": 0.03},
    "Warlab": {"X": 0.50, "Y": 0.03},
    "Red Forest": {"X": 0.45, "Y": 0.22},
    "Radar (Brain Scorcher)": {"X": 0.66, "Y": 0.22},
    "Lab X-19": {"X": 0.66, "Y": 0.22},
    "Kopachi": {"X": 0.55, "Y": 0.20},
    "Army Warehouses": {"X": 0.55, "Y": 0.36},
    "Dead City (Chernobyl-2 Settlement)": {"X": 0.25, "Y": 0.36},
    "Truck Cemetery": {"X": 0.75, "Y": 0.40},
    "Wild Territory": {"X": 0.40, "Y": 0.45},
    "Yantar": {"X": 0.25, "Y": 0.45},
    "Lab X-16": {"X": 0.25, "Y": 0.45},
    "Rostok": {"X": 0.50, "Y": 0.45},
    "Dark Valley": {"X": 0.75, "Y": 0.55},
    "Lab X-18": {"X": 0.75, "Y": 0.55},
    "Agroprom": {"X": 0.25, "Y": 0.60},
    "Agroprom Underground": {"X": 0.25, "Y": 0.60},
    "Garbage": {"X": 0.50, "Y": 0.60},
    "Cordon": {"X": 0.50, "Y": 0.85},
    "Great Swamps": {"X": 0.20, "Y": 0.80},
    "Meadow": {"X": 0.65, "Y": 0.85},
    "Chernobyl Town": {"X": 0.85, "Y": 0.65},
    "Zalissya": {"X": 0.70, "Y": 0.75}
}

with open('data/map_regions.json', 'r') as f:
    data = json.load(f)

for r in data['Regions']:
    if r['Name'] in updates:
        r['X'] = updates[r['Name']]['X']
        r['Y'] = updates[r['Name']]['Y']

with open('data/map_regions.json', 'w') as f:
    json.dump(data, f, indent=2)

print("Map coordinates updated for canon compliance.")
