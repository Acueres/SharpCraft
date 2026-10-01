namespace SharpCraft;

using Diagnostics;

internal static class Program
{
    internal static int Main(string[] args)
    {
        if (args.Contains("--help"))
        {
            Console.WriteLine(BenchmarkOptions.Usage);
            return 0;
        }

        BenchmarkOptions? options;
        try
        {
            options = args.Length == 0 ? null : BenchmarkOptions.Parse(args);
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException or OverflowException)
        {
            Console.Error.WriteLine(ex.Message);
            Console.Error.WriteLine(BenchmarkOptions.Usage);
            return 1;
        }

        using var app = new App(options);
        app.Run();
        return 0;
    }
}
