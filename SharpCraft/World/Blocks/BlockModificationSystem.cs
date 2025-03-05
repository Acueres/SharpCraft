using System.Collections.Generic;
using Microsoft.Xna.Framework;

using SharpCraft.Utilities;
using SharpCraft.World.Light;
using SharpCraft.World.Chunks;
using SharpCraft.Persistence;
using SharpCraft.MathUtilities;

namespace SharpCraft.World.Blocks;

public enum BlockInteractionMode : byte
{
    Add,
    Remove,
    Replace
}

public class BlockModificationData(ChunkAdjacency adjacency, Block newBlock, Vector3I blockIndex, Vector3 rayDirection, BlockInteractionMode interactionMode)
{
    public ChunkAdjacency Adjacency { get; } = adjacency;
    public Block NewBlock { get; } = newBlock;
    public Vector3I BlockIndex { get; } = blockIndex;
    public Vector3 RayDirection { get; } = rayDirection;
    public BlockInteractionMode InteractionMode { get; } = interactionMode;
}

class BlockModificationSystem(DatabaseService db, BlockMetadataProvider blockMetadata, LightSystem lightSystem)
{
    readonly DatabaseService db = db;
    readonly BlockMetadataProvider blockMetadata = blockMetadata;
    readonly LightSystem lightSystem = lightSystem;

    readonly Queue<BlockModificationData> queue = [];

    public void Add(Vector3I blockIndex, ChunkAdjacency adjacency, BlockInteractionMode interactionMode)
    {
        queue.Enqueue(new BlockModificationData(adjacency, Block.Empty, blockIndex, Vector3.Zero, interactionMode));
    }

    public void Add(Block newBlock, Vector3I blockIndex, Vector3 rayDirection, ChunkAdjacency adjacency, BlockInteractionMode interactionMode)
    {
        queue.Enqueue(new BlockModificationData(adjacency, newBlock, blockIndex, rayDirection, interactionMode));
    }

    public void Update()
    {
        while (queue.Count > 0)
        {
            var mod = queue.Dequeue();

            if (mod.InteractionMode == BlockInteractionMode.Add)
            {
                AddBlock(mod.NewBlock, mod.BlockIndex, mod.Adjacency, mod.RayDirection);
            }
            else if (mod.InteractionMode == BlockInteractionMode.Remove)
            {
                RemoveBlock(mod.Adjacency, mod.BlockIndex);
            }
        }
    }

    void RemoveBlock(ChunkAdjacency adjacency, Vector3I blockIndex)
    {
        IChunk chunk = adjacency.Root;

        Block block = chunk[blockIndex.X, blockIndex.Y, blockIndex.Z];
        chunk[blockIndex.X, blockIndex.Y, blockIndex.Z] = Block.Empty;

        bool lightSource = !block.IsEmpty && blockMetadata.IsLightSource(block.Value);

        chunk.RemoveIndex(blockIndex);

        lightSystem.UpdateLight(blockIndex.X, blockIndex.Y, blockIndex.Z, Block.EmptyValue, adjacency, sourceRemoved: lightSource);

        db.AddDelta(adjacency.Root.Index, blockIndex, Block.Empty);

        UpdateAdjacentBlocks(adjacency, blockIndex.Y, blockIndex.X, blockIndex.Z);
    }

    void AddBlock(Block block, Vector3I blockIndex, ChunkAdjacency adjacency, Vector3 rayDirection)
    {
        if (block.IsEmpty) return;

        char side = Util.MaxVectorComponent(rayDirection);

        (ChunkAdjacency newAdjacency, Vector3I newBlockIndex) = GetAdjacentEmptyBlockData(side, blockIndex, rayDirection, adjacency);
        IChunk chunk = newAdjacency.Root;

        if (!chunk[newBlockIndex.X, newBlockIndex.Y, newBlockIndex.Z].IsEmpty) return;

        chunk[newBlockIndex.X, newBlockIndex.Y, newBlockIndex.Z] = block;

        lightSystem.UpdateLight(newBlockIndex.X, newBlockIndex.Y, newBlockIndex.Z, block.Value, adjacency);

        db.AddDelta(chunk.Index, newBlockIndex, block);

        UpdateAdjacentBlocks(newAdjacency, newBlockIndex.Y, newBlockIndex.X, newBlockIndex.Z);
    }

    static void UpdateAdjacentBlocks(ChunkAdjacency adjacency, int y, int x, int z)
    {
        IChunk chunk = adjacency.Root;
        chunk.RecalculateMesh = true;

        if (!chunk[x, y, z].IsEmpty)
        {
            ActivateBlock(chunk, y, x, z);
        }

        if (x < Chunk.Last)
            ActivateBlock(chunk, y, x + 1, z);
        else if (x == Chunk.Last)
            ActivateBlock(adjacency.XPos.Root, y, 0, z);

        if (x > 0)
            ActivateBlock(chunk, y, x - 1, z);
        else if (x == 0)
            ActivateBlock(adjacency.XNeg.Root, y, Chunk.Last, z);

        if (y < Chunk.Last)
            ActivateBlock(chunk, y + 1, x, z);
        else if (y == Chunk.Last)
            ActivateBlock(adjacency.YPos.Root, 0, x, z);

        if (y > 0)
            ActivateBlock(chunk, y - 1, x, z);
        else if (y == 0)
            ActivateBlock(adjacency.YNeg.Root, Chunk.Last, x, z);

        if (z < Chunk.Last)
            ActivateBlock(chunk, y, x, z + 1);
        else if (z == Chunk.Last)
            ActivateBlock(adjacency.ZPos.Root, y, x, 0);

        if (z > 0)
            ActivateBlock(chunk, y, x, z - 1);
        else if (z == 0)
            ActivateBlock(adjacency.ZNeg.Root, y, x, Chunk.Last);
    }

    static void ActivateBlock(IChunk chunk, int y, int x, int z)
    {
        if (!chunk[x, y, z].IsEmpty)
        {
            chunk.AddIndex(new(x, y, z));
            chunk.RecalculateMesh = true;
        }
    }

    static (ChunkAdjacency Adjacency, Vector3I BlockIndex) GetAdjacentEmptyBlockData(char face, Vector3I blockIndex, Vector3 rayDirection, ChunkAdjacency adjacency)
    {
        int x = blockIndex.X;
        int y = blockIndex.Y;
        int z = blockIndex.Z;

        ChunkAdjacency newAdjacency = null;

        switch (face)
        {
            case 'X':
                if (rayDirection.X > 0) x--;
                else x++;

                if (x > Chunk.Last)
                {
                    newAdjacency = adjacency.XPos;
                    x = 0;
                }

                else if (x < 0)
                {
                    newAdjacency = adjacency.XNeg;
                    x = Chunk.Last;
                }

                break;

            case 'Y':
                if (rayDirection.Y > 0) y--;
                else y++;

                if (y > Chunk.Last)
                {
                    newAdjacency = adjacency.YPos;
                    y = 0;
                }

                else if (y < 0)
                {
                    newAdjacency = adjacency.YNeg;
                    y = Chunk.Last;
                }

                break;

            case 'Z':
                if (rayDirection.Z > 0) z--;
                else z++;

                if (z > Chunk.Last)
                {
                    newAdjacency = adjacency.ZPos;
                    z = 0;
                }

                else if (z < 0)
                {
                    newAdjacency = adjacency.ZNeg;
                    z = Chunk.Last;
                }

                break;
        }

        return (newAdjacency ?? adjacency, new Vector3I(x, y, z));
    }
}

