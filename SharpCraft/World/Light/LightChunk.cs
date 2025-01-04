using SharpCraft.Utility;
using System.Collections.Generic;

namespace SharpCraft.World.Light
{
    public class LightChunk
    {
        readonly LightValue[,,] lightMap;
        readonly HashSet<Vector3I> lightSourceIndexes = [];

        public LightChunk()
        {
            lightMap = new LightValue[FullChunk.Size, FullChunk.Size, FullChunk.Size];
        }

        public LightValue this[int x, int y, int z]
        {
            get => lightMap[x, y, z];
            set => lightMap[x, y, z] = value;
        }

        public void AddLightSource(int x, int y, int z)
        {
            lightSourceIndexes.Add(new Vector3I(x, y, z));
        }

        public IEnumerable<Vector3I> GetLightSources()
        {
            foreach (Vector3I index in lightSourceIndexes) yield return index;
        }
    }
}
