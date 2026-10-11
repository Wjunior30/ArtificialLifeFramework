namespace SimulationLife.Core;

public sealed class DeterministicRandom
{
    public uint State { get; private set; }

    public DeterministicRandom(uint seed) => State = seed;

    public double NextUnit()
    {
        State = unchecked(State * 1_664_525u + 1_013_904_223u);
        return ((State >> 8) & 0x00FF_FFFFu) / 16_777_215.0;
    }

    internal void Restore(uint state) => State = state;
}
