using devLibra;
using System.Text.Json;

internal static class PvpMacroRulesTests
{
    internal static void Run()
    {
        var count = 0;
        void Check(bool value) { count++; if (!value) throw new Exception($"PvP macro mapping test {count} failed."); }
        Check(PvpMacroRules.Normalize(null).SequenceEqual(new[] { -1, -1, -1, -1, -1 }));
        Check(PvpMacroRules.Normalize(new[] { 0, 0, 256, 999, -4, 1 }).SequenceEqual(new[] { 0, -1, 256, -1, -1 }));
        for (var id = -1; id < 513; id++)
            Check(PvpMacroRules.IsValid(id) == (id >= 0 && id <= 99 || id >= 256 && id <= 355));
        for (var row = 0; row < 5; row++)
            for (var set = 0; set < 2; set++)
                for (var index = 0; index < 100; index++)
                {
                    var id = (set << 8) | index;
                    var source = new[] { -1, -1, -1, -1, -1 };
                    var assigned = PvpMacroRules.Assign(source, row, id);
                    Check(assigned[row] == id && assigned.Count(x => x == id) == 1);
                    Check(source.All(x => x == -1));
                    var moved = PvpMacroRules.Assign(assigned, (row + 1) % 5, id);
                    Check(moved[row] == -1 && moved[(row + 1) % 5] == id);
                    var cleared = PvpMacroRules.Assign(moved, (row + 1) % 5, -1);
                    Check(cleared.All(x => x == -1));
                }
        var independent = new[] { 0, 1, 256, 257, 355 };
        Check(PvpMacroRules.Normalize(independent).SequenceEqual(independent));
        Check(PvpMacroRules.Assign(independent, 1, 99).SequenceEqual(new[] { 0, 99, 256, 257, 355 }));
        Check(PvpMacroRules.Normalize(JsonSerializer.Deserialize<int[]>(JsonSerializer.Serialize(independent))).SequenceEqual(independent));
        Console.WriteLine($"PASS: {count} PvP enemy-to-macro mapping assertions.");
    }
}
