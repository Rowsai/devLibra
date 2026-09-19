using devLibra;

var assertions = 0;
void Check(bool condition, string name)
{
    if (!condition) throw new Exception(name);
    assertions++;
}
void Equal(IEnumerable<int> actual, IEnumerable<int> expected, string name)
    => Check(actual.SequenceEqual(expected), name);

int[] order = [0, 1, 2, 3, 4, 14, 15, 16, 5, 6, 7, 8, 9];
Equal(TargetMarkerSortOrder.DefaultOrder(), order, "All 13 marker ranks, including attack 6–8");
Equal(TargetMarkerSortOrder.Normalize(null), order, "Missing saved config");
Equal(TargetMarkerSortOrder.Normalize([9, 9, 10, -1, 99, 0]), new[] { 9, 0 }.Concat(order.Where(x => x != 9 && x != 0)), "Repair duplicate/invalid saved ranks");
Check(TargetMarkerSortOrder.Label(14) == "攻撃6" && TargetMarkerSortOrder.Label(9) == "禁止2", "Labels");
Check(TargetMarkerSortOrder.Parse("tm asc") == PartySortMode.Ascending, "asc command");
Check(TargetMarkerSortOrder.Parse("TM  DESC ") == PartySortMode.Descending, "desc command");
Check(TargetMarkerSortOrder.Parse(" tm\toriginal") == PartySortMode.Original, "original command");
Check(TargetMarkerSortOrder.Parse("default") == PartySortMode.Default, "default command");
foreach (var invalid in new[] { "", "tm", "tm foo", "asc", "default extra", "tm asc extra" })
    Check(TargetMarkerSortOrder.Parse(invalid) == PartySortMode.None, "Invalid command: " + invalid);

// Self, ignore2, shape, attack6, unmarked, attack1, bind1, ignore1.
int[] mixed = [4, 9, 10, 14, -1, 0, 5, 8];
Equal(TargetMarkerSortOrder.BuildTarget(mixed, order, false), [0, 5, 2, 3, 4, 6, 7, 1], "Ascending, excluded positions fixed");
Equal(TargetMarkerSortOrder.BuildTarget(mixed, order, true), [0, 1, 2, 7, 4, 6, 3, 5], "Descending, excluded positions fixed");
int[] custom = [5, 9, 14, 0, 8];
Equal(TargetMarkerSortOrder.BuildTarget(mixed, custom, false), [0, 6, 2, 1, 4, 3, 5, 7], "Custom ascending");
Equal(TargetMarkerSortOrder.BuildTarget(mixed, custom, true), [0, 7, 2, 5, 4, 3, 1, 6], "Custom descending");
Equal(TargetMarkerSortOrder.BuildTarget([0, 10, 11, 12, 13, -1], order, false), [0, 1, 2, 3, 4, 5], "All excluded");
Equal(TargetMarkerSortOrder.BuildTarget([0, 2, 2, 1], order, false), [0, 3, 1, 2], "Stable ties");
Equal(TargetMarkerSortOrder.BuildTarget([], order, false), [], "Empty");
Equal(TargetMarkerSortOrder.BuildTarget([9], order, true), [0], "Solo");

void VerifySwaps(int[] target)
{
    var current = Enumerable.Range(0, target.Length).ToArray();
    foreach (var (first, second) in TargetMarkerSortOrder.BuildSwaps(target))
    {
        Check(first >= 1 && second == first + 1 && second < current.Length, "Safe native adjacent indices");
        (current[first], current[second]) = (current[second], current[first]);
    }
    Equal(current, target, "Native swap simulation reaches target");
}
var permutations = 0;
void Visit(int[] permutation, int at)
{
    if (at == permutation.Length)
    {
        VerifySwaps(permutation);
        permutations++;
        return;
    }
    for (var i = at; i < permutation.Length; i++)
    {
        (permutation[at], permutation[i]) = (permutation[i], permutation[at]);
        Visit(permutation, at + 1);
        (permutation[at], permutation[i]) = (permutation[i], permutation[at]);
    }
}
Visit([0, 1, 2, 3, 4, 5, 6, 7], 1);
Check(permutations == 5040, "All permutations of seven movable members");
var random = new Random(25);
for (var test = 0; test < 1000; test++)
{
    var markers = Enumerable.Range(0, 8).Select(_ => random.Next(-1, 17)).ToArray();
    var descending = test % 2 == 1;
    var target = TargetMarkerSortOrder.BuildTarget(markers, order, descending);
    Check(target[0] == 0, "Self stays first");
    var lastRank = -1;
    for (var row = 1; row < 8; row++)
    {
        if (!order.Contains(markers[row])) Check(target[row] == row, "Excluded member remains in place");
        else
        {
            var rank = Array.IndexOf(order, markers[target[row]]);
            if (descending) rank = 12 - rank;
            Check(rank >= lastRank, "Requested rank order");
            lastRank = rank;
        }
    }
    VerifySwaps(target);
}
foreach (var invalid in new[] { new[] { 1, 0 }, new[] { 0, 1, 1 }, new[] { 0, 3 } })
{
    var rejected = false;
    try { TargetMarkerSortOrder.BuildSwaps(invalid); }
    catch (ArgumentException) { rejected = true; }
    Check(rejected, "Reject invalid native plan");
}
Console.WriteLine($"PASS: {assertions} assertions; 5040 complete permutations; 1000 mixed-marker scenarios.");
DotRulesTests.Run();
