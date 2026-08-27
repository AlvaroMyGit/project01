import json
import os

def write_db(filename, data):
    with open(f"data/{filename}.json", "w") as f:
        json.dump(data, f, indent=4)

weapons = [
    {"id": "wpn_pm", "name": "PMm", "type": "pistol", "damage": 22, "weight": 0.73, "ammo_class": "ammo_9x18", "cost": 1200},
    {"id": "wpn_fort", "name": "Fort-12", "type": "pistol", "damage": 25, "weight": 0.83, "ammo_class": "ammo_9x18", "cost": 1500},
    {"id": "wpn_ak74u", "name": "AKS-74U", "type": "assault_rifle", "damage": 38, "weight": 2.7, "ammo_class": "ammo_5.45x39", "cost": 5000},
    {"id": "wpn_ak74", "name": "AK-74", "type": "assault_rifle", "damage": 42, "weight": 3.3, "ammo_class": "ammo_5.45x39", "cost": 7500},
    {"id": "wpn_lr300", "name": "TRs-301", "type": "assault_rifle", "damage": 45, "weight": 2.7, "ammo_class": "ammo_5.56x45", "cost": 12000},
    {"id": "wpn_vintorez", "name": "VSS Vintorez", "type": "sniper", "damage": 75, "weight": 3.2, "ammo_class": "ammo_9x39", "cost": 25000},
    {"id": "wpn_bm16", "name": "BM-16", "type": "shotgun", "damage": 80, "weight": 1.9, "ammo_class": "ammo_12x70", "cost": 2000}
]

armors = [
    {"id": "arm_leather", "name": "Leather Jacket", "type": "light", "armor": 5, "anomaly_prot": 2, "weight": 3.0, "cost": 1000},
    {"id": "arm_bandit", "name": "Bandit Trenchcoat", "type": "light", "armor": 8, "anomaly_prot": 3, "weight": 3.5, "cost": 2500},
    {"id": "arm_merc", "name": "Mercenary Suit", "type": "medium", "armor": 25, "anomaly_prot": 10, "weight": 5.0, "cost": 12000},
    {"id": "arm_seva", "name": "SEVA Suit", "type": "medium", "armor": 20, "anomaly_prot": 45, "weight": 9.0, "cost": 45000},
    {"id": "arm_exo", "name": "Exoskeleton", "type": "heavy", "armor": 65, "anomaly_prot": 15, "weight": 25.0, "cost": 85000}
]

artifacts_and_detectors = [
    {"id": "art_medusa", "name": "Medusa", "type": "artifact", "radiation": 2, "effects": {"bullet_prot": 2}, "cost": 3000},
    {"id": "art_moonlight", "name": "Moonlight", "type": "artifact", "radiation": 3, "effects": {"stamina_regen": 10}, "cost": 10000},
    {"id": "art_soul", "name": "Soul", "type": "artifact", "radiation": 2, "effects": {"health_regen": 5}, "cost": 15000},
    {"id": "det_echo", "name": "Echo Detector", "type": "detector", "range": 10, "cost": 500},
    {"id": "det_bear", "name": "Bear Detector", "type": "detector", "range": 25, "cost": 2500},
    {"id": "det_veles", "name": "Veles Detector", "type": "detector", "range": 50, "cost": 10000}
]

mutant_parts = [
    {"id": "part_boar_leg", "name": "Boar Leg", "weight": 1.0, "cost": 200},
    {"id": "part_flesh_eye", "name": "Flesh Eye", "weight": 0.2, "cost": 150},
    {"id": "part_dog_tail", "name": "Blind Dog Tail", "weight": 0.3, "cost": 100},
    {"id": "part_bloodsucker_jaw", "name": "Bloodsucker Jaw", "weight": 0.5, "cost": 2500},
    {"id": "part_snork_foot", "name": "Snork Foot", "weight": 0.8, "cost": 800}
]

consumables = [
    {"id": "cons_medkit", "name": "Medkit", "type": "medical", "effects": {"health": 50}, "cost": 300},
    {"id": "cons_bandage", "name": "Bandage", "type": "medical", "effects": {"bleeding": -50}, "cost": 100},
    {"id": "cons_antirad", "name": "Anti-rad", "type": "medical", "effects": {"radiation": -50}, "cost": 400},
    {"id": "cons_bread", "name": "Bread", "type": "food", "effects": {"hunger": -20}, "cost": 20},
    {"id": "cons_diet_sausage", "name": "Diet Sausage", "type": "food", "effects": {"hunger": -35}, "cost": 50},
    {"id": "cons_vodka", "name": "Cossacks Vodka", "type": "drink", "effects": {"radiation": -15, "hunger": -5}, "cost": 100},
    {"id": "cons_energy_drink", "name": "Energy Drink", "type": "drink", "effects": {"fatigue": -40}, "cost": 80}
]

ammo = [
    {"id": "ammo_9x18", "name": "9x18 mm rounds", "weight": 0.2, "cost": 50},
    {"id": "ammo_5.45x39", "name": "5.45x39 mm rounds", "weight": 0.3, "cost": 120},
    {"id": "ammo_5.56x45", "name": "5.56x45 mm rounds", "weight": 0.3, "cost": 150},
    {"id": "ammo_9x39", "name": "9x39 mm rounds", "weight": 0.4, "cost": 300},
    {"id": "ammo_12x70", "name": "12x70 mm buckshot", "weight": 0.5, "cost": 80}
]

belt_plates = [
    {"id": "plate_kevlar", "name": "Kevlar Plate", "effects": {"armor": 10}, "weight": 1.5, "cost": 1500},
    {"id": "plate_ceramic", "name": "Ceramic Plate", "effects": {"armor": 25}, "weight": 2.5, "cost": 4000},
    {"id": "plate_steel", "name": "Steel Plate", "effects": {"armor": 40}, "weight": 5.0, "cost": 8000}
]

scrap = [
    {"id": "scrap_metal", "name": "Scrap Metal", "weight": 1.0, "cost": 50},
    {"id": "scrap_electronics", "name": "Electronic Components", "weight": 0.5, "cost": 250},
    {"id": "scrap_fabric", "name": "Torn Fabric", "weight": 0.2, "cost": 10},
    {"id": "scrap_tools", "name": "Basic Tools", "weight": 3.0, "cost": 1500}
]

chatter = {
    "DeathReport": [
        "Found {victimName} dead near {locationName}. Looked like a {mutantType} got him.",
        "Stumbled on a corpse. Tag says {victimName}. Shot to hell by a {weaponName}."
    ],
    "MutantWarning": [
        "Careful out there, spotted a {mutantType} pack roaming {locationName}.",
        "Don't go near {locationName}, {mutantType} activity is high right now."
    ],
    "General": [
        "Anyone in {locationName} want to trade? I'm looking for {weaponName}.",
        "This is {senderName}, just made it to the base. Safe for now."
    ]
}

if __name__ == "__main__":
    write_db("weapons", weapons)
    write_db("armors", armors)
    write_db("artifacts_and_detectors", artifacts_and_detectors)
    write_db("mutant_parts", mutant_parts)
    write_db("consumables", consumables)
    write_db("ammo", ammo)
    write_db("belt_plates", belt_plates)
    write_db("scrap", scrap)
    write_db("pda_chatter_templates", chatter)
    print("Successfully generated all item and chatter JSON databases in data/ directory.")
