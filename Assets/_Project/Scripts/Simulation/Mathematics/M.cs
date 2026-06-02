using System;

namespace FoundersLands.Simulation.Mathematics
{
    /// <summary>
    /// Minimal math helpers used by the simulation. Deliberately does not depend on
    /// <c>UnityEngine.Mathf</c> so the whole simulation assembly stays engine-free and
    /// headless-testable (GDD §4 / §20 Simulation–Presentation split).
    /// </summary>
    public static class M
    {
        public static float Clamp01(float v) { return v < 0f ? 0f : (v > 1f ? 1f : v); }

        public static float Clamp(float v, float min, float max) { return v < min ? min : (v > max ? max : v); }

        public static int Clamp(int v, int min, int max) { return v < min ? min : (v > max ? max : v); }

        public static float Lerp(float a, float b, float t) { return a + (b - a) * Clamp01(t); }

        public static float LerpUnclamped(float a, float b, float t) { return a + (b - a) * t; }

        public static float SmoothStep(float t)
        {
            t = Clamp01(t);
            return t * t * (3f - 2f * t);
        }

        public static float InverseLerp(float a, float b, float v)
        {
            if (Math.Abs(b - a) < 1e-8f) return 0f;
            return Clamp01((v - a) / (b - a));
        }

        public static float Abs(float v) { return v < 0f ? -v : v; }
    }
}
