namespace SharpCraft;

static class Program
{
    internal static void Main()
    {
        using var game = new MainGame();
        game.Run();
    }
}
