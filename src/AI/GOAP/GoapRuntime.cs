namespace StalkerALifeSandbox.AI.GOAP;

/// <summary>Per-stalker GOAP plan state.</summary>
public sealed class GoapRuntime
{
    public string? ActiveGoalName { get; private set; }
    private readonly List<GOAPAction> _plan = new();

    public int ActionIndex { get; private set; }
    public GOAPAction? CurrentAction =>
        ActionIndex >= 0 && ActionIndex < _plan.Count ? _plan[ActionIndex] : null;

    public bool HasActivePlan => _plan.Count > 0 && ActionIndex < _plan.Count;
    public bool ActionEntered { get; private set; }

    public void SetPlan(GOAPGoal goal, IReadOnlyList<GOAPAction> actions)
    {
        ActiveGoalName = goal.Name;
        _plan.Clear();
        _plan.AddRange(actions);
        ActionIndex = 0;
        ActionEntered = false;
    }

    public void ClearPlan()
    {
        ActiveGoalName = null;
        _plan.Clear();
        ActionIndex = 0;
        ActionEntered = false;
    }

    public bool AdvanceAction()
    {
        ActionIndex++;
        ActionEntered = false;
        return ActionIndex < _plan.Count;
    }

    public void MarkEntered() => ActionEntered = true;
}
