using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

using SharpCraft.Utilities;
using SharpCraft.World.Lighting;
using SharpCraft.Persistence;
using SharpCraft.MathUtilities;
using SharpCraft.World.Blocks;

namespace SharpCraft.World.Chunks;

public enum BlockInteractionMode : byte
{
    Add,
    Remove,
    Replace
}

public class ChunkModificationData(ChunkAdjacency adjacency, Block newBlock, Vec3<byte> blockIndex, Vector3 rayDirection, BlockInteractionMode interactionMode)
{
    public ChunkAdjacency Adjacency { get; } = adjacency;
    public Block NewBlock { get; } = newBlock;
    public Vec3<byte> BlockIndex { get; } = blockIndex;
    public Vector3 RayDirection { get; } = rayDirection;
    public BlockInteractionMode InteractionMode { get; } = interactionMode;
}

class ChunkModificationSystem(DatabaseService db, BlockMetadataProvider blockMetadata, LightSystem lightSystem)
{
    readonly DatabaseService db = db;
    readonly BlockMetadataProvider blockMetadata = blockMetadata;
    readonly LightSystem lightSystem = lightSystem;

    readonly Queue<ChunkModificationData> queue = [];

    public void Add(Vec3<byte> blockIndex, ChunkAdjacency adjacency, BlockInteractionMode interactionMode)
    {
        queue.Enqueue(new ChunkModificationData(adjacency, Block.Empty, blockIndex, Vector3.Zero, interactionMode));
    }

    public void Add(Block newBlock, Vec3<byte> blockIndex, Vector3 rayDirection, ChunkAdjacency adjacency, BlockInteractionMode interactionMode)
    {
        queue.Enqueue(new ChunkModificationData(adjacency, newBlock, blockIndex, rayDirection, interactionMode));
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

    void RemoveBlock(ChunkAdjacency adjacency, Vec3<byte> blockIndex)
    {
        Chunk chunk = adjacency.Root;

        Block block = chunk[blockIndex.X, blockIndex.Y, blockIndex.Z];
        chunk[blockIndex.X, blockIndex.Y, blockIndex.Z] = Block.Empty;

        bool lightSource = !block.IsEmpty && blockMetadata.IsLightSource(block);

        lightSystem.UpdateLight(blockIndex.X, blockIndex.Y, blockIndex.Z, Block.EmptyValue, adjacency, sourceRemoved: lightSource);

        db.AddDelta(adjacency.Root.Index, blockIndex, Block.Empty);
    }

    void AddBlock(Block block, Vec3<byte> blockIndex, ChunkAdjacency adjacency, Vector3 rayDirection)
    {
        if (block.IsEmpty) return;

        AxisDirection dominantAxis = Util.GetDominantAxis(rayDirection);
        Vec3<int> dominantOffset = GetDominantAxisOffset(rayDirection, dominantAxis);

        (Vec3<byte> newBlockIndex, ChunkAdjacency newAdjacency) = GetAdjacentIndex(dominantAxis, blockIndex, dominantOffset, adjacency);
        Chunk chunk = newAdjacency.Root;

        if (!chunk[newBlockIndex.X, newBlockIndex.Y, newBlockIndex.Z].IsEmpty) return;

        if (chunk.IsEmpty)
        {
            chunk.Init();
            lightSystem.InitializeLight(newAdjacency);
            lightSystem.FloodFill();
        }

        chunk[newBlockIndex.X, newBlockIndex.Y, newBlockIndex.Z] = block;

        lightSystem.UpdateLight(newBlockIndex.X, newBlockIndex.Y, newBlockIndex.Z, block.Value, newAdjacency);

        db.AddDelta(chunk.Index, newBlockIndex, block);
    }

    static (Vec3<byte> newIndex, ChunkAdjacency newAdjacency) GetAdjacentIndex(AxisDirection dominantAxis, Vec3<byte> index, Vec3<int> offset, ChunkAdjacency adjacency)
    {
        ChunkAdjacency newAdjacency = null;
        Vec3<int> newIndex = new Vec3<int>(index.X, index.Y, index.Z) + offset;

        byte x = (byte)newIndex.X;
		byte y = (byte)newIndex.Y;
		byte z = (byte)newIndex.Z;

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

        return (new Vec3<byte>(x, y, z), newAdjacency ?? adjacency);
    }

    static Vec3<int> GetDominantAxisOffset(Vector3 rayDirection, AxisDirection dominantDirection)
    {
        return dominantDirection switch
        {
            AxisDirection.X => new Vec3<int>(-Math.Sign(rayDirection.X), 0, 0),
            AxisDirection.Y => new Vec3<int>(0, -Math.Sign(rayDirection.Y), 0),
            _ => new Vec3<int>(0, 0, -Math.Sign(rayDirection.Z)),
        };
    }
}

