// ──────────────────────────────────────────────────────────────
//  SurvivalNeeds.cs — Hunger, Radiation, Fatigue, Ammo, Thirst
//  Attached to every Stalker / human NPC.  Ticked at 1 Hz.
// ──────────────────────────────────────────────────────────────

namespace StalkerALifeSandbox.Entities.Needs;

using StalkerALifeSandbox.Economy;

/// <summary>
/// Holds the survival variables that drive NPC decision-making through the GOAP planner.
/// All values are normalised to 0 – 100 unless stated otherwise.
/// </summary>
public sealed class SurvivalNeeds
{
    // ── Constants ────────────────────────────────────────────
    public const float MaxValue = 100f;
    public const float CriticalThreshold = 80f;
    public const float UrgentThreshold = 60f;

    // ── Per-game-second drain rates ─────────────────────────────────────
    // Tuned so needs reach Critical (~80) within roughly 10-14 game-hours
    // (≈ 6-8 real-time minutes at TimeFactor 6×, 10 ticks/s).
    public float HungerDrainRate   { get; set; } = 8.3f / 3600f;   // ~8.3 per game hour → critical in ~10h
    public float ThirstDrainRate   { get; set; } = 12.0f / 3600f;  // ~12 per game hour → critical in ~7h
    public float FatigueDrainRate  { get; set; } = 5.5f / 3600f;   // ~5.5 per game hour → critical in ~15h
    public float RadiationFlushRate{ get; set; } = 2.0f / 3600f;   // passive flush ~2/h
    public float RadiationGainRate { get; set; } = 0f;

    // ── Need Values ─────────────────────────────────────────
    public float Hunger    { get; private set; }
    public float Thirst    { get; private set; }
    public float Radiation { get; private set; }
    public float Fatigue   { get; private set; }
    public float Morale    { get; private set; } = 70f;
    public int AmmoCount   { get; private set; } = 90;
    public float GoldAmount { get; set; } = TraderEconomyConfig.StartingGold;

    // ── Tick (called at 1 Hz by ZoneDirector) ───────────────
    public void Tick(float deltaGameSec)
    {
        Hunger    = Math.Clamp(Hunger    + HungerDrainRate   * deltaGameSec, 0f, MaxValue);
        Thirst    = Math.Clamp(Thirst    + ThirstDrainRate   * deltaGameSec, 0f, MaxValue);
        Fatigue   = Math.Clamp(Fatigue   + FatigueDrainRate  * deltaGameSec, 0f, MaxValue);
        
        float radChange = (RadiationGainRate - RadiationFlushRate) * deltaGameSec;
        Radiation = Math.Clamp(Radiation + radChange, 0f, MaxValue);

        if (Hunger > UrgentThreshold || Thirst > UrgentThreshold || Fatigue > UrgentThreshold)
        {
            Morale = Math.Clamp(Morale - 0.02f * (deltaGameSec / 6.0f), 0f, MaxValue);
        }
    }

    // ── Mutators ────────────────────────────────────────────
    public void Feed(float amount) => Hunger = Math.Clamp(Hunger - amount, 0f, MaxValue);
    public void Drink(float amount) => Thirst = Math.Clamp(Thirst - amount, 0f, MaxValue);
    public void Rest(float amount) => Fatigue = Math.Clamp(Fatigue - amount, 0f, MaxValue);
    public void Exhaust(float amount) => Fatigue = Math.Clamp(Fatigue + amount, 0f, MaxValue);
    public void TakeAntiRad(float amount) => Radiation = Math.Clamp(Radiation - amount, 0f, MaxValue);
    public void AdjustMorale(float delta) => Morale = Math.Clamp(Morale + delta, 0f, MaxValue);
    public void AddAmmo(int rounds) => AmmoCount = Math.Max(0, AmmoCount + rounds);
    public bool ConsumeAmmo(int rounds)
    {
        if (AmmoCount < rounds) return false;
        AmmoCount -= rounds;
        return true;
    }

    // ── Queries ──────────────────────────────────────────────
    public bool IsInCriticalState => Hunger >= CriticalThreshold || Thirst >= CriticalThreshold || Radiation >= CriticalThreshold || Fatigue >= CriticalThreshold;
    public bool IsDesperate => Hunger > 75f && GoldAmount < 500f;
    public bool IsOutOfAmmo => AmmoCount <= 0;

    public string? MostUrgentNeed()
    {
        float worst = UrgentThreshold;
        string? tag = null;

        if (Hunger    > worst) { worst = Hunger;    tag = "Hunger"; }
        if (Thirst    > worst) { worst = Thirst;    tag = "Thirst"; }
        if (Radiation > worst) { worst = Radiation; tag = "Radiation"; }
        if (Fatigue   > worst) { worst = Fatigue;   tag = "Fatigue"; }

        if (AmmoCount <= 5 && tag is null) tag = "Ammo";

        return tag;
    }

    // ── Diagnostics ─────────────────────────────────────────
    public override string ToString() =>
        $"[Needs] H={Hunger:F1} T={Thirst:F1} R={Radiation:F1} F={Fatigue:F1} " +
        $"M={Morale:F0} A={AmmoCount} Desp={IsDesperate}";
}
