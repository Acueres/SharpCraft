namespace SharpCraft
{
    class Program
    {
        public static void Main()
        {
            using (MainGame game = new MainGame())
            {
                game.Run();
            }
        }
    }
}
