using System;
using System.Globalization;
using System.Numerics;

namespace devLibra;

internal static class FieldMarkerRules
{
    internal static bool TryParse(string x, string y, string z, out Vector3 position)
    {
        position = default;
        if (!Coordinate(x, out var px) || !Coordinate(y, out var py) || !Coordinate(z, out var pz)) return false;
        position = new Vector3(px, py, pz);
        return true;
    }
    private static bool Coordinate(string text, out float value) =>
        float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value) &&
        float.IsFinite(value) && Math.Abs((double)value * 1000) < int.MaxValue;
    internal static string Format(float value) => value.ToString("0.###", CultureInfo.InvariantCulture);
    internal static bool Matches(Vector3 actual, Vector3 expected) => Vector3.Distance(actual, expected) <= 0.01f;
}
