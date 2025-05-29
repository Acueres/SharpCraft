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

public class ChunkModificationData(Chunk chunk, Block newBlock, Vec3<byte> blockIndex, Vector3 rayDirection, BlockInteractionMode interactionMode)
{
    public Chunk Chunk { get; } = chunk;
    public Block NewBlock { get; } = newBlock;
    public Vec3<byte> BlockIndex { get; } = blockIndex;
    public Vector3 RayDirection { get; } = rayDirection;
    public BlockInteractionMode InteractionMode { get; } = interactionMode;
}

class ChunkModificationSystem(DatabaseService db,
    BlockMetadataProvider blockMetadata, LightSystem lightSystem, Action<Chunk> PostChunkForRemeshing)
{
    readonly DatabaseService db = db;
    readonly BlockMetadataProvider blockMetadata = blockMetadata;
    readonly LightSystem lightSystem = lightSystem;
    readonly Action<Chunk> PostChunkForRemeshing = PostChunkForRemeshing;

    readonly Queue<ChunkModificationData> queue = [];

    public void Add(Vec3<byte> blockIndex, Chunk chunk, BlockInteractionMode interactionMode)
    {
        queue.Enqueue(new ChunkModificationData(chunk, Block.Empty, blockIndex, Vector3.Zero, interactionMode));
    }

    public void Add(Block newBlock, Vec3<byte> blockIndex, Vector3 rayDirection, Chunk chunk, BlockInteractionMode interactionMode)
    {
        queue.Enqueue(new ChunkModificationData(chunk, newBlock, blockIndex, rayDirection, interactionMode));
    }

    public void Update()
    {
        while (queue.Count > 0)
        {
            var mod = queue.Dequeue();

            if (mod.InteractionMode == BlockInteractionMode.Add)
            {
                AddBlock(mod.Chunk, mod.NewBlock, mod.BlockIndex, mod.RayDirection);
            }
            else if (mod.InteractionMode == BlockInteractionMode.Remove)
            {
                RemoveBlock(mod.Chunk, mod.BlockIndex);
            }
        }
    }

    void RemoveBlock(Chunk chunk, Vec3<byte> blockIndex)
    {
        Block block = chunk[blockIndex.X, blockIndex.Y, blockIndex.Z];
        chunk[blockIndex.X, blockIndex.Y, blockIndex.Z] = Block.Empty;

        bool isLightSource = !block.IsEmpty && blockMetadata.IsLightSource(block);

        HashSet<Chunk> visited;
        if (isLightSource)
        {
            chunk.RemoveLightSource(blockIndex.X, blockIndex.Y, blockIndex.Z, block);
            visited = lightSystem.RecalculateLightOnBlockUpdate(chunk, blockIndex);
        }
        else
        {
            visited = lightSystem.RecalculateLightOnBlockRemoval(chunk, blockIndex);
        }

        foreach (var ch in visited)
            PostChunkForRemeshing(ch);

        db.AddDelta(chunk.Index, blockIndex, Block.Empty);
    }

    void AddBlock(Chunk chunk, Block block, Vec3<byte> blockIndex, Vector3 rayDirection)
    {
        if (block.IsEmpty) return;

        AxisDirection dominantAxis = Util.GetDominantAxis(rayDirection);
        Vec3<int> dominantOffset = GetDominantAxisOffset(rayDirection, dominantAxis);

        (Vec3<byte> newBlockIndex, Vec3<sbyte> chunkOffset) = GetAdjacentIndex(dominantAxis, blockIndex, dominantOffset);

        chunk = chunk.GetNeighborFromOffset(chunkOffset);

        if (!chunk[newBlockIndex.X, newBlockIndex.Y, newBlockIndex.Z].IsEmpty) return;

        if (chunk.IsEmpty)
        {
            chunk.Init();
            lightSystem.InitializeLight(chunk);
            lightSystem.RunBFS();
        }

        chunk[newBlockIndex.X, newBlockIndex.Y, newBlockIndex.Z] = block;

        bool isLightSource = !block.IsEmpty && blockMetadata.IsLightSource(block);

        HashSet<Chunk> visited;
        if (isLightSource)
        {
            chunk.AddLightSource(newBlockIndex.X, newBlockIndex.Y, newBlockIndex.Z, block);
        }

        visited = lightSystem.RecalculateLightOnBlockUpdate(chunk, newBlockIndex);

        foreach (var ch in visited)
            PostChunkForRemeshing(ch);

        db.AddDelta(chunk.Index, newBlockIndex, block);
    }

    static (Vec3<byte> blockIndex, Vec3<sbyte> chunkOffset) GetAdjacentIndex(AxisDirection dominantAxis, Vec3<byte> index, Vec3<int> offset)
    {
        var chunkOffset = Vec3<sbyte>.Zero;
        Vec3<int> newIndex = index.Into<int>() + offset;

        byte x = (byte)newIndex.X;
		byte y = (byte)newIndex.Y;
		byte z = (byte)newIndex.Z;

        if (dominantAxis == AxisDirection.X)
        {
            if (newIndex.X > Chunk.Last)
            {
                chunkOffset = new Vec3<sbyte>(1, 0, 0);
                x = 0;
            }
            else if (newIndex.X < 0)
            {
                chunkOffset = new Vec3<sbyte>(-1, 0, 0);
                x = Chunk.Last;
            }
        }
        else if (dominantAxis == AxisDirection.Y)
        {
            if (newIndex.Y > Chunk.Last)
            {
                chunkOffset = new Vec3<sbyte>(0, 1, 0);
                y = 0;
            }
            else if (newIndex.Y < 0)
            {
                chunkOffset = new Vec3<sbyte>(0, -1, 0);
                y = Chunk.Last;
            }
        }
        else
        {
            if (newIndex.Z > Chunk.Last)
            {
                chunkOffset = new Vec3<sbyte>(0, 0, 1);
                z = 0;
            }
            else if (newIndex.Z < 0)
            {
                chunkOffset = new Vec3<sbyte>(0, 0, -1);
                z = Chunk.Last;
            }
        }

        return (new Vec3<byte>(x, y, z), chunkOffset);
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

