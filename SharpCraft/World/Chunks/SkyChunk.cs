using System.Collections.Generic;
using Microsoft.Xna.Framework;
using SharpCraft.MathUtilities;
using SharpCraft.Utilities;
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
        public void ActivateBlock(Vector3I index) { }

        public IEnumerable<Vector3I> GetActiveIndexes() => [];

        public IEnumerable<Vector3I> GetLightSources() => [];

        public void CalculateActiveBlocks(ChunkAdjacency adjacency) { }

        public FacesState GetVisibleFaces(Vector3I index, ChunkAdjacency adjacency,
                                    bool calculateOpacity = true) => new(false);

        public void Dispose() { }

        public override int GetHashCode()
        {
            return Index.GetHashCode();
        }
    }
}
