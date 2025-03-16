using System.Collections.Generic;

using SharpCraft.MathUtilities;
using SharpCraft.Utilities;
using SharpCraft.World.Blocks;
using SharpCraft.World.Chunks;

namespace SharpCraft.World.Generation;

class BlockBuffer(Block[,,] blocks, BlockMetadataProvider blockMetadata)
{
    readonly Block[,,] buffer = blocks;

    public Block this[int x, int y, int z]
    {
        get => buffer[x, y, z];
        set => buffer[x, y, z] = value;
    }

    public IEnumerable<Vector3I> GetActiveBlocksIndexes()
    {
        for (int y = 0; y < Chunk.Size; y++)
        {
            for (int x = 0; x < Chunk.Size; x++)
            {
                for (int z = 0; z < Chunk.Size; z++)
                {
                    if (buffer[x, y, z].IsEmpty)
                    {
                        continue;
                    }

                    yield return new Vector3I(x, y, z);
                }
            }
        }
    }

    public FacesState GetVisibleFaces(Vector3I index, ChunkAdjacency adjacency)
    {
        FacesState visibleFaces = new();

        int x = index.X;
        int y = index.Y;
        int z = index.Z;

        Block block = buffer[x, y, z];

        Block adjacentBlock;

        bool isBlockOpaque = !(block.IsEmpty || blockMetadata.IsBlockTransparent(block.Value));

        if (z == Chunk.Last)
        {
            adjacentBlock = adjacency.ZPos.Root[x, y, 0];
        }
        else
        {
            adjacentBlock = buffer[x, y, z + 1];
        }
        visibleFaces.ZPos = adjacentBlock.IsEmpty || blockMetadata.IsBlockTransparent(adjacentBlock.Value) && isBlockOpaque;

        if (z == 0)
        {
            adjacentBlock = adjacency.ZNeg.Root[x, y, Chunk.Last];
        }
        else
        {
            adjacentBlock = buffer[x, y, z - 1];
        }
        visibleFaces.ZNeg = adjacentBlock.IsEmpty || blockMetadata.IsBlockTransparent(adjacentBlock.Value) && isBlockOpaque;

        if (y == Chunk.Last)
        {
            adjacentBlock = adjacency.YPos.Root[x, 0, z];
        }
        else
        {
            adjacentBlock = buffer[x, y + 1, z];
        }
        visibleFaces.YPos = adjacentBlock.IsEmpty || blockMetadata.IsBlockTransparent(adjacentBlock.Value) && isBlockOpaque;

        if (y == 0)
        {
            adjacentBlock = adjacency.YNeg.Root[x, Chunk.Last, z];
        }
        else
        {
            adjacentBlock = buffer[x, y - 1, z];
        }
        visibleFaces.YNeg = adjacentBlock.IsEmpty || blockMetadata.IsBlockTransparent(adjacentBlock.Value) && isBlockOpaque;


        if (x == Chunk.Last)
        {
            adjacentBlock = adjacency.XPos.Root[0, y, z];
        }
        else
        {
            adjacentBlock = buffer[x + 1, y, z];
        }
        visibleFaces.XPos = adjacentBlock.IsEmpty || blockMetadata.IsBlockTransparent(adjacentBlock.Value) && isBlockOpaque;

        if (x == 0)
        {
            adjacentBlock = adjacency.XNeg.Root[Chunk.Last, y, z];
        }
        else
        {
            adjacentBlock = buffer[x - 1, y, z];
        }
        visibleFaces.XNeg = adjacentBlock.IsEmpty || blockMetadata.IsBlockTransparent(adjacentBlock.Value) && isBlockOpaque;

        return visibleFaces;
    }
}
