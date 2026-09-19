using devLibra;

internal static class DotRulesTests
{
    internal static void Run()
    {
        var assertions = 0;
        void Check(bool condition, string name)
        {
            if (!condition) throw new Exception("DoT: " + name);
            assertions++;
        }
        // Description forms observed in the game's English Status sheet.
        Check(DotDisplayRules.IsSupportedStatus(2586, 2, 212926, "Damage taken from the caster is increased."), "Death's Design exception");
        Check(!DotDisplayRules.IsSupportedStatus(2587, 2, 212926, "Damage taken from the caster is increased."), "Other non-DoT debuffs excluded");
        Check(!DotDisplayRules.IsSupportedStatus(2586, 1, 212926, ""), "Exception still requires debuff");
        Check(!DotDisplayRules.IsSupportedStatus(2586, 2, 0, ""), "Exception still requires icon");
        Check(DotDisplayRules.IsSupportedStatus(1871, 2, 212635, "Sustaining damage over time."), "Existing DoTs remain supported");
        foreach (var description in new[]
        {
            "Sustaining damage over time.", // Biolysis, Dia, Combust III, etc.
            "Sustaining wind damage over time.", // Aero
            "Toxins are causing damage over time.", // Venomous / Caustic Bite
            "Lungs are failing, causing damage over time.", // Bio II
            "Proximity of a theoretical sun is causing damage over time.", // Combust
            "Suffering damage over time.",
            "Taking blunt damage over time.",
        }) Check(DotDisplayRules.IsDot(2, 123, description), "Recognize periodic damage");
        foreach (var description in new[]
        {
            "Damage dealt is reduced.", "Restoring HP over time.",
            "Damage over time potency is increased.", "Damage over time taken is increased.",
            "Unable to move.", "", "Causing direct damage. Damage over time is reduced.",
        }) Check(!DotDisplayRules.IsDot(2, 123, description), "Exclude non-DoT effects");
        Check(!DotDisplayRules.IsDot(1, 123, "Sustaining damage over time."), "Exclude buffs");
        Check(!DotDisplayRules.IsDot(2, 0, "Sustaining damage over time."), "Exclude missing icon");
        Check(DotDisplayRules.ShouldDisplay(100, 100, 30), "Local source");
        Check(!DotDisplayRules.ShouldDisplay(100, 101, 30), "Other player excluded");
        Check(!DotDisplayRules.ShouldDisplay(100, 0xE0000000, 30), "Invalid source excluded");
        Check(!DotDisplayRules.ShouldDisplay(0, 0, 30), "Logged out");
        Check(!DotDisplayRules.ShouldDisplay(0xE0000000, 0xE0000000, 30), "Invalid local player");
        foreach (var duration in new[] { 0f, -1f, float.NaN, float.PositiveInfinity, float.NegativeInfinity })
        {
            Check(!DotDisplayRules.ShouldDisplay(100, 100, duration), "Invalid or expired timer");
            Check(DotDisplayRules.TimeText(duration) == "", "Invalid timer text");
        }
        Check(DotDisplayRules.TimeText(29.2f) == "30", "Ceiling seconds");
        Check(DotDisplayRules.TimeText(0.01f) == "1", "Last fraction of second");
        Check(DotDisplayRules.TimeText(60) == "60", "Seconds, not minutes");
        Check(DotDisplayRules.TimeText(120) == "120", "Long timer");
        Check(DotDisplayRules.IsToggleCommand("show dot"), "Alias arguments");
        Check(DotDisplayRules.IsToggleCommand(" SHOW\tDoT "), "Case and whitespace");
        foreach (var invalid in new[] { "", "show", "dot", "show dot extra", "show hot" })
            Check(!DotDisplayRules.IsToggleCommand(invalid), "Reject malformed alias");
        Console.WriteLine($"PASS: DoT display rules, {assertions} assertions.");
    }
}
