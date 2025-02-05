using System.Collections.Generic;
using Microsoft.Xna.Framework;
using SharpCraft.Utility;
using SharpCraft.World.Blocks;
using SharpCraft.World.Light;

namespace SharpCraft.World.Chunks
{
    public class SkyChunk(Vector3I index) : IChunk
    {
        public Vector3I Index { get; } = index;
        public Vector3 Position { get; } = Chunk.Size * new Vector3(index.X, index.Y, index.Z);
        public bool IsReady { get; set; }

        public bool RecalculateMesh { get; set; }

        public Block this[int x, int y, int z]
        {
            get => Block.Empty;
            set { }
        }

        public LightValue GetLight(int x, int y, int z)
        {
            return LightValue.Sunlight;
        }

        public void SetLight(int x, int y, int z, LightValue value) { }

        public bool AddIndex(Vector3I index) => true;

        public bool RemoveIndex(Vector3I index) => true;

        public IEnumerable<Vector3I> GetActiveIndexes() => [];

        public IEnumerable<Vector3I> GetLightSources() => [];

        public void CalculateActiveBlocks(ChunkAdjacency adjacency) { }

        public FacesState GetVisibleFaces(int y, int x, int z, ChunkAdjacency adjacency,
                                    bool calculateOpacity = true) => new(false);

        public void Dispose() { }

        public override int GetHashCode()
        {
            return Index.GetHashCode();
        }
    }
}
