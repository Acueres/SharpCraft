using System;
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
        Chunk chunk = adjacency.Root;

        Block block = chunk[blockIndex.X, blockIndex.Y, blockIndex.Z];
        chunk[blockIndex.X, blockIndex.Y, blockIndex.Z] = Block.Empty;

        bool lightSource = !block.IsEmpty && blockMetadata.IsLightSource(block.Value);

        chunk.RemoveIndex(blockIndex);

        lightSystem.UpdateLight(blockIndex.X, blockIndex.Y, blockIndex.Z, Block.EmptyValue, adjacency, sourceRemoved: lightSource);

        db.AddDelta(adjacency.Root.Index, blockIndex, Block.Empty);

        UpdateAdjacentBlocks(adjacency, blockIndex);
    }

    void AddBlock(Block block, Vector3I blockIndex, ChunkAdjacency adjacency, Vector3 rayDirection)
    {
        if (block.IsEmpty) return;

        AxisDirection dominantAxis = Util.GetDominantAxis(rayDirection);
        Vector3I dominantOffset = GetDominantAxisOffset(rayDirection, dominantAxis);

        (Vector3I newBlockIndex, ChunkAdjacency newAdjacency) = GetAdjacentIndex(dominantAxis, blockIndex, dominantOffset, adjacency);
         Chunk chunk = newAdjacency.Root;

        if (!chunk[newBlockIndex.X, newBlockIndex.Y, newBlockIndex.Z].IsEmpty) return;

        chunk[newBlockIndex.X, newBlockIndex.Y, newBlockIndex.Z] = block;

        lightSystem.UpdateLight(newBlockIndex.X, newBlockIndex.Y, newBlockIndex.Z, block.Value, newAdjacency);

        db.AddDelta(chunk.Index, newBlockIndex, block);

        UpdateAdjacentBlocks(newAdjacency, newBlockIndex);
    }

    static void UpdateAdjacentBlocks(ChunkAdjacency adjacency, Vector3I index)
    {
        ReadOnlySpan<Vector3I> offsets = [
            new Vector3I(0, 0, 0),
            new Vector3I(1, 0, 0),
            new Vector3I(-1, 0, 0),
            new Vector3I(0, 1, 0),
            new Vector3I(0, -1, 0),
            new Vector3I(0, 0, 1),
            new Vector3I(0, 0, -1)
        ];

        foreach (var offset in offsets)
        {
            Vector3I neighborIndex = index + offset;

            Chunk targetChunk = adjacency.Root;

            // Check X boundaries.
            if (neighborIndex.X < 0)
            {
                targetChunk = adjacency.XNeg.Root;
                neighborIndex = new Vector3I(Chunk.Last, neighborIndex.Y, neighborIndex.Z);
            }
            else if (neighborIndex.X > Chunk.Last)
            {
                targetChunk = adjacency.XPos.Root;
                neighborIndex = new Vector3I(0, neighborIndex.Y, neighborIndex.Z);
            }

            // Check Y boundaries.
            if (neighborIndex.Y < 0)
            {
                targetChunk = adjacency.YNeg.Root;
                neighborIndex = new Vector3I(neighborIndex.X, Chunk.Last, neighborIndex.Z);
            }
            else if (neighborIndex.Y > Chunk.Last)
            {
                targetChunk = adjacency.YPos.Root;
                neighborIndex = new Vector3I(neighborIndex.X, 0, neighborIndex.Z);
            }

            // Check Z boundaries.
            if (neighborIndex.Z < 0)
            {
                targetChunk = adjacency.ZNeg.Root;
                neighborIndex = new Vector3I(neighborIndex.X, neighborIndex.Y, Chunk.Last);
            }
            else if (neighborIndex.Z > Chunk.Last)
            {
                targetChunk = adjacency.ZPos.Root;
                neighborIndex = new Vector3I(neighborIndex.X, neighborIndex.Y, 0);
            }

            targetChunk.ActivateBlock(neighborIndex);
        }
    }

    static (Vector3I newIndex, ChunkAdjacency newAdjacency) GetAdjacentIndex(AxisDirection dominantAxis, Vector3I index, Vector3I offset, ChunkAdjacency adjacency)
    {
        ChunkAdjacency newAdjacency = null;
        Vector3I newIndex = index + offset;

        int x = newIndex.X;
        int y = newIndex.Y;
        int z = newIndex.Z;

        if (dominantAxis == AxisDirection.X)
        {
            if (newIndex.X > Chunk.Last)
            {
                newAdjacency = adjacency.XPos;
                x = 0;
            }
            else if (newIndex.X < 0)
            {
                newAdjacency = adjacency.XNeg;
                x = Chunk.Last;
            }
        }
        else if (dominantAxis == AxisDirection.Y)
        {
            if (newIndex.Y > Chunk.Last)
            {
                newAdjacency = adjacency.YPos;
                y = 0;
            }
            else if (newIndex.Y < 0)
            {
                newAdjacency = adjacency.YNeg;
                y = Chunk.Last;
            }
        }
        else
        {
            if (newIndex.Z > Chunk.Last)
            {
                newAdjacency = adjacency.ZPos;
                z = 0;
            }
            else if (newIndex.Z < 0)
            {
                newAdjacency = adjacency.ZNeg;
                z = Chunk.Last;
            }
        }

        return (new Vector3I(x, y, z), newAdjacency ?? adjacency);
    }

    static Vector3I GetDominantAxisOffset(Vector3 rayDirection, AxisDirection dominantDirection)
    {
        return dominantDirection switch
        {
            AxisDirection.X => new Vector3I(-Math.Sign(rayDirection.X), 0, 0),
            AxisDirection.Y => new Vector3I(0, -Math.Sign(rayDirection.Y), 0),
            _ => new Vector3I(0, 0, -Math.Sign(rayDirection.Z)),
        };
    }
}

