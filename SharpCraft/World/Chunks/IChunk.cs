using System.Collections.Generic;
using Microsoft.Xna.Framework;
using SharpCraft.MathUtilities;
using SharpCraft.Utilities;
using SharpCraft.World.Blocks;
using SharpCraft.World.Light;

namespace SharpCraft.World.Chunks
{
    public interface IChunk
    {
        Vector3I Index { get; }
        Vector3 Position { get; }
        bool IsReady { get; set; }
        bool RecalculateMesh { get; set; }

        Block this[int x, int y, int z] { get; set; }
        LightValue GetLight(int x, int y, int z);
        void SetLight(int x, int y, int z, LightValue value);
        void CalculateActiveBlocks(ChunkAdjacency adjacency);
        IEnumerable<Vector3I> GetActiveIndexes();
        FacesState GetVisibleFaces(Vector3I index, ChunkAdjacency adjacency, bool calculateOpacity = true);
        bool AddIndex(Vector3I index);
        bool RemoveIndex(Vector3I index);
        IEnumerable<Vector3I> GetLightSources();
        void Dispose();
        int GetHashCode();
    }
}
