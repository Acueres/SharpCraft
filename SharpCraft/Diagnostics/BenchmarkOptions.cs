using System.Globalization;

namespace SharpCraft.Diagnostics;

internal enum BenchmarkScene { Exposure, Merge, Terrain }
internal enum BenchmarkPath { Stationary, Rotate, Travel }

internal sealed record BenchmarkOptions(BenchmarkScene? Scene = null, int Seed = 1337,
    int Radius = 8, BenchmarkPath Path = BenchmarkPath.Rotate, double WarmupSeconds = 5,
    double MeasureSeconds = 20, uint FpsLimit = 60)
{
    public static BenchmarkOptions ForScene(BenchmarkScene scene) => new(Scene: scene, Radius: 4, FpsLimit: 0);

    public static BenchmarkOptions Parse(string[] args)
    {
        BenchmarkOptions options = new();
        bool radiusSet = false;
        bool fpsSet = false;
        
        for (int i = 0; i < args.Length; i += 2)
        {
            if (i + 1 == args.Length) throw new ArgumentException($"Missing value for {args[i]}");
            string value = args[i + 1];
            options = args[i] switch
            {
                "--benchmark" => options with { Scene = ParseEnum<BenchmarkScene>(value) },
                "--seed" => options with { Seed = int.Parse(value, CultureInfo.InvariantCulture) },
                "--radius" => options with { Radius = int.Parse(value, CultureInfo.InvariantCulture) },
                "--path" => options with { Path = ParseEnum<BenchmarkPath>(value) },
                "--warmup" => options with { WarmupSeconds = double.Parse(value, CultureInfo.InvariantCulture) },
                "--seconds" => options with { MeasureSeconds = double.Parse(value, CultureInfo.InvariantCulture) },
                "--fps" => options with { FpsLimit = uint.Parse(value, CultureInfo.InvariantCulture) },
                _ => throw new ArgumentException($"Unknown option: {args[i]}")
            };
            radiusSet |= args[i] == "--radius";
            fpsSet |= args[i] == "--fps";
        }
        if (options.Scene.HasValue)
        {
            var defaults = ForScene(options.Scene.Value);
            options = options with { Radius = radiusSet ? options.Radius : defaults.Radius, FpsLimit = fpsSet ? options.FpsLimit : defaults.FpsLimit };
        }
        options.Validate();
        if (!options.Scene.HasValue && args.Any(a => a is "--seed" or "--path" or "--warmup" or "--seconds"))
            throw new ArgumentException("Seed, path and measurement options require --benchmark");
        return options;
    }

    public void Validate()
    {
        if (Scene.HasValue && !Enum.IsDefined(Scene.Value)) throw new ArgumentException("Unknown benchmark scene");
        if (!Enum.IsDefined(Path)) throw new ArgumentException("Unknown benchmark camera path");
        if (Radius is < 1 or > 16) throw new ArgumentException("Radius must be 1..16 chunks");
        if (!double.IsFinite(WarmupSeconds) || WarmupSeconds < 0 || WarmupSeconds > 600 ||
            !double.IsFinite(MeasureSeconds) || MeasureSeconds <= 0 || MeasureSeconds > 600)
            throw new ArgumentException("Warmup must be 0..600 seconds; measurement must be >0..600 seconds");
        if (FpsLimit > 1000) throw new ArgumentException("FPS limit must be 0..1000 (0 = uncapped)");
    }

    private static T ParseEnum<T>(string value) where T : struct, Enum =>
        Enum.TryParse<T>(value, true, out var result) && Enum.IsDefined(result) && !int.TryParse(value, out _)
            ? result : throw new ArgumentException($"Invalid {typeof(T).Name}: {value}");

    public const string Usage = "SharpCraft [--benchmark exposure|merge|terrain] [--seed 1337] [--radius 4]\n" +
        "           [--path stationary|rotate|travel] [--warmup 5] [--seconds 20] [--fps 0]\n" +
        "F3: compact overlay. Shift+F3: detailed report. 1..4: report page. Up/Down: scroll.\n" +
        "Benchmarks start after loading and keep the completed summary visible. Escape exits.";
}
