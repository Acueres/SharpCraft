using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Microsoft.Xna.Framework;

namespace SharpCraft.World
{
    public sealed class Region
    {
        public IEnumerable<Chunk> GetChunks()
        {
            foreach (var chunk in map.Values)
            {
                yield return chunk;
            }
        }

        Dictionary<Vector3, Chunk> map = new();

        public void InitArea(Vector3 center, int distance)
        {
            foreach (var position in GetPositions(center, distance))
            {
                if (!map.ContainsKey(position))
                {
                    map.Add(position, null);
                }
            }
        }

        private static IEnumerable<Vector3> GetPositions(Vector3 center, int distance)
        {
            for (int x = -distance; x <= distance; x++)
            {
                for (int z = -distance; z <= distance; z++)
                {
                    yield return center + new Vector3(x, 0, z);
                }
            }
        }
    }
}
