import os
import glob
import re

systems_dir = "src/Core/Systems/"

# 1. Update SimulationContext.cs
with open("src/Core/SimulationContext.cs", "r") as f:
    ctx_content = f.read()
ctx_content = ctx_content.replace("ConcurrentBag<Stalker>   Stalkers,", "List<Stalker>            Stalkers,")
ctx_content = ctx_content.replace("ConcurrentBag<Mutant>    Mutants,", "List<Mutant>             Mutants,\n    object                   EntityLock,")
with open("src/Core/SimulationContext.cs", "w") as f:
    f.write(ctx_content)

# 2. Update SimulationLoop.cs
with open("src/Core/SimulationLoop.cs", "r") as f:
    loop_content = f.read()

loop_content = loop_content.replace("ConcurrentBag<Stalker> stalkers,", "List<Stalker> stalkers,")
loop_content = loop_content.replace("ConcurrentBag<Mutant> mutants,", "List<Mutant> mutants,\n        object entityLock,")
loop_content = loop_content.replace("stalkers, mutants, corpses,", "stalkers, mutants, entityLock, corpses,")

# For TickLowFrequency
loop_content = loop_content.replace("foreach (var s in _ctx.Stalkers.Where(s => s.IsAlive))", "Stalker[] stalkers;\n        lock (_ctx.EntityLock) { stalkers = _ctx.Stalkers.ToArray(); }\n        foreach (var s in stalkers.Where(s => s.IsAlive))")
loop_content = loop_content.replace("foreach (var m in _ctx.Mutants.Where(m => m.IsAlive))", "Mutant[] mutants;\n        lock (_ctx.EntityLock) { mutants = _ctx.Mutants.ToArray(); }\n        foreach (var m in mutants.Where(m => m.IsAlive))")
loop_content = loop_content.replace("_ctx.Stalkers.Count(x => x.IsAlive)", "stalkers.Count(x => x.IsAlive)")

with open("src/Core/SimulationLoop.cs", "w") as f:
    f.write(loop_content)


# 3. Update Systems iterating over Stalkers or Mutants
def process_system(path):
    with open(path, "r") as f:
        content = f.read()

    # Find where it iterates or accesses ctx.Stalkers / ctx.Mutants
    # This is tricky with regex. Let's do simple replacements if it just iterates.
    if "foreach (var s in ctx.Stalkers" in content:
        content = content.replace("foreach (var s in ctx.Stalkers", "Stalker[] stalkers;\n        lock (ctx.EntityLock) { stalkers = ctx.Stalkers.ToArray(); }\n        foreach (var s in stalkers")
        content = content.replace("ctx.Stalkers", "stalkers")
    
    if "foreach (var m in ctx.Mutants" in content:
        content = content.replace("foreach (var m in ctx.Mutants", "Mutant[] mutants;\n        lock (ctx.EntityLock) { mutants = ctx.Mutants.ToArray(); }\n        foreach (var m in mutants")
        content = content.replace("ctx.Mutants", "mutants")

    # Fix ctx.Stalkers -> stalkers references in other places (e.g. SquadSuccession call)
    if "ctx.Stalkers" in content and "stalkers" in content:
        content = content.replace("ctx.Stalkers", "stalkers")

    with open(path, "w") as f:
        f.write(content)

for f in glob.glob(systems_dir + "*.cs"):
    if "SpawnOrchestrator" not in f and "TelemetrySystem" not in f and "CorpseCleanup" not in f:
        process_system(f)

# SpawnOrchestrator has custom logic, it already locks _entityLock. Let's fix it manually.
with open("src/Core/Systems/SpawnOrchestrator.cs", "r") as f:
    so_content = f.read()
so_content = so_content.replace("ctx.Stalkers.Add", "ctx.Stalkers.Add") # Nothing to change for Add, it is already under lock (_entityLock)
# However, we should change lock(_entityLock) to lock(ctx.EntityLock)
so_content = so_content.replace("lock (_entityLock)", "lock (ctx.EntityLock)")
with open("src/Core/Systems/SpawnOrchestrator.cs", "w") as f:
    f.write(so_content)

# TelemetrySystem has custom logic, building DTOs.
with open("src/Core/Systems/TelemetrySystem.cs", "r") as f:
    ts_content = f.read()
ts_content = ts_content.replace("ctx.Stalkers.Where", "stalkers.Where")
ts_content = ts_content.replace("ctx.Mutants.Where", "mutants.Where")
ts_content = ts_content.replace("LeaderboardSerializer.SaveLeaderboard(ctx.Stalkers", "LeaderboardSerializer.SaveLeaderboard(stalkers")
ts_content = ts_content.replace("BroadcastTelemetry(ctx);", "Stalker[] stalkers;\n        Mutant[] mutants;\n        lock (ctx.EntityLock) {\n            stalkers = ctx.Stalkers.ToArray();\n            mutants = ctx.Mutants.ToArray();\n        }\n\n        BroadcastTelemetry(ctx, stalkers, mutants);")
ts_content = ts_content.replace("private void BroadcastTelemetry(SimulationContext ctx)", "private void BroadcastTelemetry(SimulationContext ctx, Stalker[] stalkers, Mutant[] mutants)")
with open("src/Core/Systems/TelemetrySystem.cs", "w") as f:
    f.write(ts_content)

print("Done updating systems")
