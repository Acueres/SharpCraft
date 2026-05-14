namespace SharpCraft.Time;

internal readonly record struct FrameTime(
    double TotalSeconds,
    float DeltaSeconds
);
