using System;
using System.Collections.Generic;
using System.IO;
using System.Data.SQLite;
using System.Threading.Tasks;
using System.Collections.Concurrent;

using SharpCraft.World.Blocks;
using SharpCraft.World.Chunks;
using SharpCraft.MathUtilities;

namespace SharpCraft.Persistence;

internal class SaveData(Vec3<int> chunkIndex, Vec3<byte> blockIndex, Block block)
{
    public Vec3<int> ChunkIndex { get; } = chunkIndex;
    public Vec3<byte> BlockIndex { get; } = blockIndex;
    public Block Block { get; } = block;
}

public class DatabaseService
{
    readonly string connectionString;
    readonly BlockMetadataProvider blockMetadata;

    readonly Queue<SaveData> saveQueue = [];
    readonly ConcurrentDictionary<Vec3<int>, int> chunkDbIndexCache = [];
    readonly Task flushTask;
    readonly SQLiteConnection connection;

    const string initCommand = @"
                                CREATE TABLE IF NOT EXISTS block_delta(
                                    Id INTEGER PRIMARY KEY,
                                    ChunkId INTEGER,
                                    X INTEGER,
                                    Y INTEGER,
                                    Z INTEGER,
                                    Block INTEGER,
                                    UNIQUE(ChunkId, X, Y, Z),
                                    FOREIGN KEY(ChunkId) REFERENCES chunks(Id)
                                );
                                
                                CREATE TABLE IF NOT EXISTS chunks(
                                    Id INTEGER PRIMARY KEY,
                                    X INTEGER,
                                    Y INTEGER,
                                    Z INTEGER,
                                    UNIQUE(X, Y, Z)
                                );
                                ";

    const string chunkIdQuery = @"INSERT OR IGNORE INTO chunks(X, Y, Z) VALUES(@x, @y, @z);
                                  SELECT Id FROM chunks WHERE X = @x AND Y = @y AND Z = @z;";

    const string addBlockDeltaCommand = @"INSERT OR REPLACE INTO block_delta(ChunkId, X, Y, Z, Block)
                                          VALUES(@chunkId, @x, @y, @z, @block)";

    const string chunkDeltaQuery = @"SELECT x, y, z, block FROM block_delta
                                     WHERE ChunkId = @chunkId";

    public DatabaseService(MainGame game, string saveName, BlockMetadataProvider blockMetadata)
    {
        this.blockMetadata = blockMetadata;

        connectionString = "URI=file:" + Path.Combine(Directory.GetCurrentDirectory(), "Saves", saveName, "data.db");
        connection = new SQLiteConnection(connectionString);

        flushTask = Task.Run(async () =>
        {
            const int delay = 2;
            TimeSpan delaySpan = TimeSpan.FromSeconds(delay);

            while (game.State == GameState.Running || saveQueue.Count > 0)
            {
                await FlushDeltasAsync();
                await Task.Delay(delaySpan);
            }
        });
    }

    public void Close()
    {
        flushTask.Wait();
        connection.Close();
    }

    public void Initialize()
    {
        connection.Open();

        using var cmd = new SQLiteCommand(connection)
        {
            CommandText = initCommand
        };
        cmd.ExecuteNonQuery();
    }

    public void AddDelta(Vec3<int> chunkIndex, Vec3<byte> blockIndex, Block block)
    {
        saveQueue.Enqueue(new SaveData(chunkIndex, blockIndex, block));
    }

    public Block[,,] ApplyDelta(Chunk chunk, Block[,,] buffer)
    {
        int chunkId = GetChunkIdAsync(chunk.Index).Result;

        using var command = new SQLiteCommand(connection)
        {
            CommandText = chunkDeltaQuery
        };

        command.Parameters.AddWithValue("@chunkId", chunkId);

        using var reader = command.ExecuteReader();

        if (buffer is null && reader.HasRows)
        {
            buffer = Chunk.GetBlockArray();
        }

        if (!reader.HasRows) return buffer;

        while (reader.Read())
        {
            byte x = (byte)reader.GetInt32(0);
            byte y = (byte)reader.GetInt32(1);
            byte z = (byte)reader.GetInt32(2);
            var block = new Block((ushort)reader.GetInt32(3));

            buffer[x, y, z] = block;

            if (!block.IsEmpty && blockMetadata.IsLightSource(block))
            {
                chunk.AddLightSource(x, y, z, block);
            }
        }

        return buffer;
    }

    async Task FlushDeltasAsync()
    {
        using var command = new SQLiteCommand(connection)
        {
            CommandText = addBlockDeltaCommand
        };

        while (saveQueue.Count > 0)
        {
            SaveData data = saveQueue.Dequeue();

            int chunkId = await GetChunkIdAsync(data.ChunkIndex);

            command.Parameters.AddWithValue("@chunkId", chunkId);
            command.Parameters.AddWithValue("@x", data.BlockIndex.X);
            command.Parameters.AddWithValue("@y", data.BlockIndex.Y);
            command.Parameters.AddWithValue("@z", data.BlockIndex.Z);
            command.Parameters.AddWithValue("@block", (int)data.Block.Value);

            await command.ExecuteNonQueryAsync();
        }
    }

    async Task<int> GetChunkIdAsync(Vec3<int> chunkIndex)
    {
        if (chunkDbIndexCache.TryGetValue(chunkIndex, out int id))
        {
            return id;
        }

        using var command = new SQLiteCommand(connection)
        {
            CommandText = chunkIdQuery
        };

        command.Parameters.AddWithValue("@x", chunkIndex.X);
        command.Parameters.AddWithValue("@y", chunkIndex.Y);
        command.Parameters.AddWithValue("@z", chunkIndex.Z);

        using var reader = await command.ExecuteReaderAsync();

        if (reader.Read())
        {
            id = reader.GetInt32(0);
        }

        chunkDbIndexCache.TryAdd(chunkIndex, id);

        return id;
    }
}
