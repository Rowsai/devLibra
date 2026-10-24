using System;
using System.Numerics;
namespace devLibra;
internal static class FieldMarkerPreset
{
    // Native order: A B C D 1 2 3 4; clockwise order: A 2 B 3 C 4 D 1.
    internal static Vector3[] Generate(Vector3 center, int meters, bool square)
    {
        if (meters < 1 || meters > 20 || !float.IsFinite(center.X) || !float.IsFinite(center.Y) || !float.IsFinite(center.Z))
            throw new ArgumentOutOfRangeException(nameof(meters));
        int[] order = [0, 5, 1, 6, 2, 7, 3, 4];
        var result = new Vector3[8];
        for (var i = 0; i < 8; i++)
        {
            var angle = i * MathF.PI / 4;
            var x = MathF.Sin(angle); var z = -MathF.Cos(angle);
            var radius = square ? meters / Math.Max(Math.Abs(x), Math.Abs(z)) : meters;
            result[order[i]] = center + new Vector3(x * radius, 0, z * radius);
        }
        return result;
    }
}
