using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

using SharpCraft.GUI.Menus;
using SharpCraft.MathUtilities;
using SharpCraft.Persistence;
using SharpCraft.Rendering.Meshers;
using SharpCraft.Utilities;
using SharpCraft.World.Blocks;
using SharpCraft.World.Chunks;
using SharpCraft.World.ChunkStreaming;
using SharpCraft.World.Generation;
using SharpCraft.World.Lighting;
using SharpCraft.World.Meshing;

namespace SharpCraft.World;

class WorldSystem : IDisposable
{
    readonly Region region;
    readonly ChunkModificationSystem chunkModSystem;

    readonly GameMenu gameMenu;
    readonly ChunkGenerator chunkGenerator;
    readonly BlockOutlineMesher blockOutlineMesher;

    readonly Player player;
    readonly Parameters parameters;
    readonly RegionStreaming regionStreaming;
    readonly SpawnResolver spawnResolver;

    public WorldSystem(Player player, Region region, GameMenu gameMenu, ChunkPersistenceService chunkPersistence,
        Parameters parameters, BlockMetadataProvider blockMetadata,
        ChunkMesher chunkMesher, BlockOutlineMesher blockOutlineMesher)
    {
        this.player = player;
        this.parameters = parameters;
        this.region = region;
        this.gameMenu = gameMenu;

        chunkGenerator = new ChunkGenerator(parameters, chunkPersistence, blockMetadata);
        this.blockOutlineMesher = blockOutlineMesher;

        LightSystem lightSystem = new();

        chunkModSystem = new ChunkModificationSystem(chunkPersistence, blockMetadata, lightSystem,
            chunk => regionStreaming.ScheduleForMeshing(chunk));

        regionStreaming = new RegionStreaming(region, chunkGenerator, lightSystem, chunkMesher);
        this.gameMenu.SetWorldGenerator(regionStreaming);

        spawnResolver = new SpawnResolver(player, parameters, regionStreaming, chunkGenerator, region);
    }

    public void Init()
    {
        player.Flying = parameters.IsFlying;

        var spawnPosition = spawnResolver.Resolve();

        player.Position = spawnPosition;
        player.Index = Chunk.WorldToChunkCoords(spawnPosition);

        regionStreaming.BulkGenerate(spawnPosition);
    }

    public void Update(GameTime gameTime, bool exitedMenu)
    {
        UpdateEntities(gameTime, exitedMenu);

        Vec3<int> currentPlayerIndex = Chunk.WorldToChunkCoords(player.Position);

        if (player.Index != currentPlayerIndex)
        {
            regionStreaming.Update(player.Position);
            player.Index = currentPlayerIndex;
        }

        regionStreaming.Update();
    }

    public void UpdateEntities(GameTime gameTime, bool exitedMenu)
    {
        player.Update(gameTime);

        UpdateEntitiesPhysics(exitedMenu);

        if (player.UpdateOccured)
        {
            ProcessPlayerActions(exitedMenu);
        }

        chunkModSystem.Update();
    }

    void ProcessPlayerActions(bool exitedMenu)
    {
        if (exitedMenu) return;

        Raycaster raycaster = new(player.Camera.Position,
            player.Camera.Direction, 0.1f);

        const float maxDistance = 4.5f;

        Vector3 blockPosition = player.Camera.Position;
        Vec3<byte> blockIndex = Chunk.WorldToBlockCoords(blockPosition);
        Block block = Block.Empty;
        Chunk chunk = null;

        while (raycaster.Length(blockPosition) < maxDistance)
        {
            blockPosition = raycaster.Step();
            var chunkIndex = Chunk.WorldToChunkCoords(blockPosition);
            blockIndex = Chunk.WorldToBlockCoords(blockPosition);

            chunk = region[chunkIndex];

            if (chunk is null) break;
            
            block = chunk[blockIndex.X, blockIndex.Y, blockIndex.Z];
            if (!block.IsEmpty) break;
        }

        if (chunk == null || block.IsEmpty)
        {
            blockOutlineMesher.Flush();
            return;
        }

        if (player.LeftClick)
        {
            chunkModSystem.Add(blockIndex, chunk, BlockInteractionMode.Remove);
        }
        else if (player.RightClick)
        {
            if ((blockPosition - player.Position).Length() > 1.1f)
            {
                chunkModSystem.Add(new Block(gameMenu.SelectedItem), blockIndex, player.Camera.Direction, chunk, BlockInteractionMode.Add);
            }
        }

        FacesState visibleFaces = chunk.GetVisibleFaces(blockIndex);
        blockOutlineMesher.GenerateMesh(visibleFaces, new Vector3(blockIndex.X, blockIndex.Y, blockIndex.Z) + chunk.Position, player.Camera.Direction);
    }

    void UpdateEntitiesPhysics(bool exitedMenu)
    {
        if (exitedMenu) return;

        var playerBox = player.Bound;
        Vector3 playerCenter = (playerBox.Min + playerBox.Max) * 0.5f;

        ReadOnlySpan<Vector3> collisionPoints = [
                // The eight corners
                new Vector3(playerBox.Min.X, playerBox.Min.Y, playerBox.Min.Z),
                    new Vector3(playerBox.Min.X, playerBox.Min.Y, playerBox.Max.Z),
                    new Vector3(playerBox.Min.X, playerBox.Max.Y, playerBox.Min.Z),
                    new Vector3(playerBox.Min.X, playerBox.Max.Y, playerBox.Max.Z),
                    new Vector3(playerBox.Max.X, playerBox.Min.Y, playerBox.Min.Z),
                    new Vector3(playerBox.Max.X, playerBox.Min.Y, playerBox.Max.Z),
                    new Vector3(playerBox.Max.X, playerBox.Max.Y, playerBox.Min.Z),
                    new Vector3(playerBox.Max.X, playerBox.Max.Y, playerBox.Max.Z),

                    // Centers of the bottom and top faces
                    new Vector3(playerCenter.X, playerBox.Min.Y, playerCenter.Z),
                    new Vector3(playerCenter.X, playerBox.Max.Y, playerCenter.Z),

                    // Centers of the left and right faces (horizontal sides)
                    new Vector3(playerBox.Min.X, playerCenter.Y, playerCenter.Z),
                    new Vector3(playerBox.Max.X, playerCenter.Y, playerCenter.Z),

                    // Centers of the front and back faces
                    new Vector3(playerCenter.X, playerCenter.Y, playerBox.Min.Z),
                    new Vector3(playerCenter.X, playerCenter.Y, playerBox.Max.Z)
        ];

        // A hashset of chunk and block indices
        var collisionIndices = new HashSet<(Vec3<int>, Vec3<byte>)>();
        foreach (var point in collisionPoints)
        {
            Vec3<int> chunkIndex = Chunk.WorldToChunkCoords(point);
            Vec3<byte> blockIndex = Chunk.WorldToBlockCoords(point);
            collisionIndices.Add((chunkIndex, blockIndex));
        }

        List<BoundingBox> collidableBlockBounds = [];

        foreach (var (chunkIndex, blockIndex) in collisionIndices)
        {
            Chunk chunk = region[chunkIndex];
            
            if (chunk is null) continue;
            
            Block block = chunk[blockIndex.X, blockIndex.Y, blockIndex.Z];
            if (!block.IsEmpty)
            {
                Vector3 blockWorldPos = Chunk.BlockIndexToWorldPosition(chunk.Position, blockIndex);
                BoundingBox blockBounds = new(blockWorldPos, blockWorldPos + Vector3.One);
                collidableBlockBounds.Add(blockBounds);
            }
        }

        foreach (var bound in collidableBlockBounds)
        {
            player.Physics.ResolveCollision(bound);
        }
    }


    bool disposed;

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposed) return;
        disposed = true;

        if (disposing)
        {
            regionStreaming.Dispose();
        }
    }
}
