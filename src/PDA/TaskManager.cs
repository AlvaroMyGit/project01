// TaskManager.cs — Emergent contracts & bounties
// Spec §3D: When an NPC has unfulfilled needs (e.g. irradiated
//           without vodka, or low ammo), they post an automated
//           PDA contract with gold rewards.
using StalkerALifeSandbox.Core;
using StalkerALifeSandbox.Entities.Characters;
using StalkerALifeSandbox.Entities.Needs;

namespace StalkerALifeSandbox.PDA;

/// <summary>Status of an emergent task.</summary>
public enum TaskStatus { Open, Accepted, Completed, Failed, Expired }

/// <summary>Category of emergent task for filtering.</summary>
public enum TaskCategory { SupplyRequest, Escort, Bounty, Rescue }

/// <summary>A dynamically generated contract / bounty.</summary>
public sealed class EmergentTask
{
    public string       Id          { get; init; } = "";
    public string       PosterId    { get; init; } = "";
    public string       Description { get; init; } = "";
    public TaskCategory Category    { get; init; }
    public string?      TargetId    { get; init; }
    public string?      ItemNeeded  { get; init; }
    public float        Reward      { get; init; }
    public TaskStatus   Status      { get; set; } = TaskStatus.Open;
    public float        ExpiresAt   { get; init; }
    public string?      AcceptedBy  { get; set; }
}

/// <summary>
/// Manages the pool of emergent NPC-posted contracts.
/// At 1 Hz, scans registered stalkers and auto-posts tasks
/// when critical needs are detected (spec §3D).
/// </summary>
public sealed class TaskManager
{
    private readonly List<EmergentTask> _tasks = new();
    public IReadOnlyList<EmergentTask> Tasks => _tasks;

    private int _taskCounter;

    /// <summary>Minimum game-time seconds between auto-posts per NPC.</summary>
    public float AutoPostCooldown { get; set; } = 300f;

    /// <summary>Tracks last auto-post time per NPC id.</summary>
    private readonly Dictionary<string, float> _lastAutoPost = new();

    // ── Manual Task Posting ─────────────────────────────────

    /// <summary>Post a new task and broadcast a bounty event.</summary>
    public void PostTask(EmergentTask task)
    {
        _tasks.Add(task);
        EventBus.Publish(new BountyEvent
        {
            TargetName  = task.TargetId ?? task.ItemNeeded ?? "General",
            PosterId    = task.PosterId,
            Reward      = task.Reward,
            Description = task.Description
        });
    }

    /// <summary>Accept a task by NPC id.</summary>
    public bool AcceptTask(string taskId, string npcId)
    {
        var t = _tasks.Find(t => t.Id == taskId && t.Status == TaskStatus.Open);
        if (t is null) return false;
        t.Status = TaskStatus.Accepted;
        t.AcceptedBy = npcId;
        return true;
    }

    /// <summary>Mark a task as completed and return the reward amount.</summary>
    public float CompleteTask(string taskId)
    {
        var t = _tasks.Find(t => t.Id == taskId && t.Status == TaskStatus.Accepted);
        if (t is null) return 0f;
        t.Status = TaskStatus.Completed;
        return t.Reward;
    }

    // ── Auto-Post (Spec §3D — Emergent Tasks) ──────────────

    /// <summary>
    /// Scan a stalker for critical needs and auto-post a PDA
    /// contract if they can't self-resolve.
    /// Called at 1 Hz per stalker.
    /// </summary>
    public void EvaluateAutoPost(Stalker stalker, float gameTime)
    {
        if (!stalker.IsAlive) return;

        // Cooldown check
        if (_lastAutoPost.TryGetValue(stalker.Id, out float last) &&
            gameTime - last < AutoPostCooldown)
            return;

        var needs = stalker.Needs;
        string? urgentNeed = needs.MostUrgentNeed();
        if (urgentNeed is null) return;

        // Build an emergent task based on the need type
        var task = urgentNeed switch
        {
            "Hunger" => MakeSupplyTask(stalker, gameTime,
                "food", "Requesting food supplies", 150f),

            "Radiation" => MakeSupplyTask(stalker, gameTime,
                "anti_rad", "Need anti-radiation meds urgently", 200f),

            "Fatigue" => null, // can self-resolve by resting

            "Ammo" => MakeSupplyTask(stalker, gameTime,
                "ammo_545", "Running low on ammunition", 250f),

            _ => null
        };

        if (task is not null)
        {
            PostTask(task);
            _lastAutoPost[stalker.Id] = gameTime;
        }
    }

    private EmergentTask MakeSupplyTask(
        Stalker poster, float gameTime,
        string itemNeeded, string description, float reward)
    {
        return new EmergentTask
        {
            Id          = $"auto_{_taskCounter++}",
            PosterId    = poster.Id,
            Description = $"[{poster.DisplayName}] {description}",
            Category    = TaskCategory.SupplyRequest,
            ItemNeeded  = itemNeeded,
            Reward      = reward,
            ExpiresAt   = gameTime + 600f // 10 min expiry
        };
    }

    // ── Tick (Macro Frequency) ──────────────────────────────

    /// <summary>Expire old tasks. Called at 0.1 Hz.</summary>
    public void Tick(float gameTime)
    {
        foreach (var t in _tasks)
        {
            if (t.Status == TaskStatus.Open && gameTime >= t.ExpiresAt)
                t.Status = TaskStatus.Expired;
        }
    }

    /// <summary>Count of currently open tasks.</summary>
    public int OpenTaskCount =>
        _tasks.Count(t => t.Status == TaskStatus.Open);
}
