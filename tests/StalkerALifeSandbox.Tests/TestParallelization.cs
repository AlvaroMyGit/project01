// The system under test carries process-global mutable state — the item database
// and other static singletons, static config on services like CorpseCleanupService,
// and the static EventBus. Running xUnit collections in parallel lets that shared
// state race between tests. Until the global state is removed (roadmap Phase 4),
// run collections serially so the suite stays reliable in CI. (Note: tests that
// assert an RNG-driven outcome must still be made deterministic individually — see
// RepairRestoresWeaponCondition.)
[assembly: Xunit.CollectionBehavior(DisableTestParallelization = true)]
