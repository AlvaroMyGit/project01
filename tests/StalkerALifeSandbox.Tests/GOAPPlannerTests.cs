using System.Collections.Generic;
using StalkerALifeSandbox.AI.Blackboards;
using StalkerALifeSandbox.AI.GOAP;
using StalkerALifeSandbox.Entities.Needs;

namespace StalkerALifeSandbox.Tests;

public class GOAPPlannerTests
{
    // ── Test doubles ─────────────────────────────────────────────────────
    private sealed class TestAction : GOAPAction
    {
        private readonly Dictionary<string, bool> _pre;
        private readonly Dictionary<string, bool> _eff;
        private readonly float _cost;

        public TestAction(string name, Dictionary<string, bool>? pre, Dictionary<string, bool> eff, float cost = 1f)
        {
            Name = name;
            _pre = pre ?? new();
            _eff = eff;
            _cost = cost;
        }

        public override string Name { get; }
        public override Dictionary<string, bool> GetPreconditions() => _pre;
        public override Dictionary<string, bool> GetEffects() => _eff;
        public override float EvaluateCost(NPCBlackboard bb) => _cost;
        public override bool Execute(NPCBlackboard bb, float delta) => true;
    }

    private sealed class TestGoal : GOAPGoal
    {
        private readonly float _utility;
        private readonly Dictionary<string, bool> _target;
        private readonly bool _relevant;

        public TestGoal(string name, float utility, Dictionary<string, bool> target, bool relevant = true)
        {
            Name = name;
            _utility = utility;
            _target = target;
            _relevant = relevant;
        }

        public override string Name { get; }
        public override float EvaluateUtility(NPCBlackboard bb, SurvivalNeeds needs) => _utility;
        public override Dictionary<string, bool> GetTargetState() => _target;
        public override bool IsRelevant(NPCBlackboard bb) => _relevant;
    }

    private static (NPCBlackboard, SurvivalNeeds) Actor() => (new NPCBlackboard("npc"), new SurvivalNeeds());

    // ── Tests ────────────────────────────────────────────────────────────
    [Fact]
    public void Plan_SelectsHighestUtilityGoal()
    {
        var planner = new GOAPPlanner();
        planner.RegisterGoal(new TestGoal("Low", 10f, new() { ["A"] = true }));
        planner.RegisterGoal(new TestGoal("High", 90f, new() { ["B"] = true }));
        planner.RegisterAction(new TestAction("MakeA", null, new() { ["A"] = true }));
        planner.RegisterAction(new TestAction("MakeB", null, new() { ["B"] = true }));

        var (bb, needs) = Actor();
        var result = planner.Plan(bb, needs);

        Assert.NotNull(result);
        Assert.Equal("High", result!.ChosenGoal.Name);
        Assert.Single(result.Actions);
        Assert.Equal("MakeB", result.Actions[0].Name);
    }

    [Fact]
    public void Plan_ChainsActionsInExecutionOrder()
    {
        var planner = new GOAPPlanner();
        planner.RegisterGoal(new TestGoal("Upgrade", 50f, new() { ["HasUpgrade"] = true }));
        planner.RegisterAction(new TestAction("Craft", new() { ["HasParts"] = true }, new() { ["HasUpgrade"] = true }));
        planner.RegisterAction(new TestAction("Gather", null, new() { ["HasParts"] = true }));

        var (bb, needs) = Actor();
        var result = planner.Plan(bb, needs);

        Assert.NotNull(result);
        Assert.Equal(2, result!.Actions.Count);
        Assert.Equal("Gather", result.Actions[0].Name); // precondition first
        Assert.Equal("Craft", result.Actions[1].Name);
    }

    [Fact]
    public void Plan_PrefersCheaperPath()
    {
        var planner = new GOAPPlanner();
        planner.RegisterGoal(new TestGoal("Done", 50f, new() { ["Done"] = true }));
        planner.RegisterAction(new TestAction("Cheap", null, new() { ["Done"] = true }, cost: 1f));
        planner.RegisterAction(new TestAction("Expensive", null, new() { ["Done"] = true }, cost: 10f));

        var (bb, needs) = Actor();
        var result = planner.Plan(bb, needs);

        Assert.NotNull(result);
        Assert.Single(result!.Actions);
        Assert.Equal("Cheap", result.Actions[0].Name);
    }

    [Fact]
    public void Plan_ReturnsEmptyPlan_WhenGoalAlreadySatisfied()
    {
        var planner = new GOAPPlanner();
        planner.RegisterGoal(new TestGoal("BeSafe", 50f, new() { ["Safe"] = true }));
        planner.RegisterAction(new TestAction("FindCover", null, new() { ["Safe"] = true }));

        var (bb, needs) = Actor();
        bb.WorldStateBools["Safe"] = true; // already true

        var result = planner.Plan(bb, needs);

        Assert.NotNull(result);
        Assert.Empty(result!.Actions);
    }

    [Fact]
    public void Plan_ReturnsNull_WhenNoGoalIsRelevant()
    {
        var planner = new GOAPPlanner();
        planner.RegisterGoal(new TestGoal("Irrelevant", 99f, new() { ["X"] = true }, relevant: false));
        planner.RegisterAction(new TestAction("MakeX", null, new() { ["X"] = true }));

        var (bb, needs) = Actor();
        Assert.Null(planner.Plan(bb, needs));
    }

    [Fact]
    public void Plan_ReturnsNull_WhenGoalIsUnreachable()
    {
        var planner = new GOAPPlanner();
        planner.RegisterGoal(new TestGoal("Impossible", 50f, new() { ["Unreachable"] = true }));
        planner.RegisterAction(new TestAction("Unrelated", null, new() { ["Something"] = true }));

        var (bb, needs) = Actor();
        Assert.Null(planner.Plan(bb, needs));
    }
}
