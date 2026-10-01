namespace SharpCraft.Diagnostics;

// Bounded storage shared by CPU and worker measurements. Callers synchronize workers.
internal sealed class SampleWindow(int capacity = 256)
{
    private readonly double[] values = new double[capacity];
    private readonly double[] scratch = new double[capacity];
    private int cursor;
    public int Count { get; private set; }

    public void Add(double value)
    {
        values[cursor] = value;
        cursor = (cursor + 1) % values.Length;
        Count = Math.Min(Count + 1, values.Length);
    }

    public DurationStatistics Statistics()
    {
        if (Count == 0) return default;
        Array.Copy(values, scratch, Count);
        Array.Sort(scratch, 0, Count);
        double sum = 0;
        for (int i = 0; i < Count; i++) sum += scratch[i];
        return new(Count, sum / Count, scratch[0], scratch[(int)Math.Ceiling(Count * .95) - 1], scratch[Count - 1]);
    }
}
