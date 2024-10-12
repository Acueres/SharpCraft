using System;

namespace SharpCraft.DataModel
{
    public record ParametersData(int Seed, bool IsFlying,
        float X, float Y, float Z, float DirX, float DirY, float DirZ,
        ushort[] Inventory, string WorldType, int Day, int Hour, int Minute, DateTime Date);
}
