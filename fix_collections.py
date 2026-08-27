import glob
import re

for path in glob.glob("src/Core/Systems/*.cs"):
    with open(path, "r") as f:
        content = f.read()
    
    # Restore ctx.Stalkers and ctx.Mutants everywhere
    content = content.replace("stalkers.Where", "ctx.Stalkers.Where")
    content = content.replace("stalkers.FirstOrDefault", "ctx.Stalkers.FirstOrDefault")
    content = content.replace("stalkers.Count", "ctx.Stalkers.Count")
    content = content.replace("stalkers.Any", "ctx.Stalkers.Any")
    content = content.replace("stalkers.Select", "ctx.Stalkers.Select")
    content = content.replace("ctx.Stalkers.Add", "ctx.Stalkers.Add") # Was stalkers.Add ?
    content = content.replace("stalkers.Add", "ctx.Stalkers.Add")
    content = content.replace("stalkers, ", "ctx.Stalkers, ")
    content = content.replace("mutants.Where", "ctx.Mutants.Where")
    content = content.replace("mutants.FirstOrDefault", "ctx.Mutants.FirstOrDefault")
    content = content.replace("mutants.Count", "ctx.Mutants.Count")
    content = content.replace("mutants.Any", "ctx.Mutants.Any")
    content = content.replace("mutants.Select", "ctx.Mutants.Select")
    content = content.replace("mutants.Add", "ctx.Mutants.Add")

    # Fix the snapshot variables in Tick
    # "foreach (var s in ctx.Stalkers" -> we want to keep it as "foreach (var s in stalkers)" if we created the snapshot.
    # Wait, the python script did: "foreach (var s in stalkers"
    
    # Let's clean up the Tick methods.
    # Remove the snapshot declarations
    content = re.sub(r'Stalker\[\] stalkers;\s*lock \(ctx\.EntityLock\) \{ stalkers = ctx\.Stalkers\.ToArray\(\); \}\s*foreach \(var s in ctx\.Stalkers', r'lock (ctx.EntityLock) {\n            foreach (var s in ctx.Stalkers.ToList()', content)
    
    content = re.sub(r'Mutant\[\] mutants;\s*lock \(ctx\.EntityLock\) \{ mutants = ctx\.Mutants\.ToArray\(\); \}\s*foreach \(var m in ctx\.Mutants', r'lock (ctx.EntityLock) {\n            foreach (var m in ctx.Mutants.ToList()', content)

    # In TelemetrySystem
    if "TelemetrySystem.cs" in path:
        content = content.replace("LeaderboardSerializer.SaveLeaderboard(ctx.Stalkers", "LeaderboardSerializer.SaveLeaderboard(ctx.Stalkers.ToList()")
        content = content.replace("BroadcastTelemetry(ctx, ctx.Stalkers, ctx.Mutants)", "BroadcastTelemetry(ctx)")

    with open(path, "w") as f:
        f.write(content)

