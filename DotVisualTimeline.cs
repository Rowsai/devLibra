using System;

namespace devLibra;

/// <summary>Uses the first observed duration, and restarts when a refresh extends it.</summary>
internal sealed class DotVisualTimeline
{
    private float duration;
    private float lastRemaining;
    private double lastSeen;
    internal double ExpiresAt => lastSeen + lastRemaining + 2;

    internal float Observe(float remaining, double now)
    {
        var predicted = Math.Max(0, lastRemaining - Math.Max(0, now - lastSeen));
        if (duration <= 0 || remaining > predicted + 0.75)
            duration = remaining;
        lastRemaining = remaining;
        lastSeen = now;
        return Math.Clamp(1 - remaining / Math.Max(duration, 0.001f), 0, 1);
    }

    internal static float Brightness(float remaining, double now)
        => remaining > 0 && remaining <= 3
            ? 0.10f + 0.90f * (float)((Math.Cos(now * Math.PI * 2) + 1) / 2)
            : 1f;
}
