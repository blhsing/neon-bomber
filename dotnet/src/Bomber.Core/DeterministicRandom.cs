namespace Bomber.Core;

/// <summary>Small SplitMix64 generator whose sequence is stable across .NET versions.</summary>
internal sealed class DeterministicRandom
{
    private ulong _state;

    public DeterministicRandom(int seed)
    {
        _state = unchecked((ulong)(long)seed) ^ 0xD1B54A32D192ED03UL;
    }

    public ulong NextUInt64()
    {
        _state += 0x9E3779B97F4A7C15UL;
        var value = _state;
        value = (value ^ (value >> 30)) * 0xBF58476D1CE4E5B9UL;
        value = (value ^ (value >> 27)) * 0x94D049BB133111EBUL;
        return value ^ (value >> 31);
    }

    public int NextInt(int exclusiveMaximum)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(exclusiveMaximum);
        var bound = (ulong)exclusiveMaximum;
        var threshold = unchecked(0UL - bound) % bound;
        ulong value;
        do
        {
            value = NextUInt64();
        }
        while (value < threshold);

        return (int)(value % bound);
    }

    public double NextDouble() => (NextUInt64() >> 11) * (1.0 / (1UL << 53));

    public bool Chance(double probability) => probability >= 1 || (probability > 0 && NextDouble() < probability);
}
