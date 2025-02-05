using System.Collections.Generic;
using System.IO;
using System.Data.SQLite;
using System.Threading.Tasks;
using SharpCraft.Utility;
using SharpCraft.World.Blocks;
using SharpCraft.World.Chunks;


namespace SharpCraft.Persistence
{
    public class DatabaseService
    {
        readonly BlockMetadataProvider blockMetadata;
        SQLiteCommand cmd;
        SQLiteConnection connection;

        const string insertCommand = @"INSERT OR REPLACE INTO chunks(chunkX, chunkY, chunkZ, x, y, z, block)
                                VALUES(@chunkX, @chunkY, @chunkZ, @x, @y, @z, @block)";

        const string selectCommand = @"SELECT x, y, z, block FROM chunks
                                WHERE [chunkX] = @x AND [chunkY] = @y AND [chunkZ] = @z";

        Queue<SaveData> dataQueue;

        Task saveTask;

        class SaveData
        {
            public Vector3I Index;
            public int X;
            public int Y;
            public int Z;
            public Block Block;

            public SaveData(Vector3I index, int x, int y, int z, Block block)
            {
                Index = index;
                X = x;
                Y = y;
                Z = z;
                Block = block;
            }
        }


        public DatabaseService(MainGame game, string saveName, BlockMetadataProvider blockMetadata)
        {
            dataQueue = new Queue<SaveData>(10);

            this.blockMetadata = blockMetadata;

            string path = @"URI=file:" + Directory.GetCurrentDirectory() + $@"\Saves\{saveName}\data.db";
            connection = new SQLiteConnection(path);
            connection.Open();

            cmd = new SQLiteCommand(connection)
            {
                CommandText = @"CREATE TABLE IF NOT EXISTS chunks(
                                chunkX INTEGER,
                                chunkY INTEGER,
                                chunkZ INTEGER,
                                x INTEGER,
                                y INTEGER,
                                z INTEGER,
                                block INTEGER,
                                PRIMARY KEY(chunkX, chunkY, chunkZ, x, y, z))"
            };
            cmd.ExecuteNonQuery();

            saveTask = Task.Run(async () =>
            {
                while (game.State == GameState.Started || dataQueue.Count > 0)
                {
                    WriteDelta();
                    await Task.Delay(2000);
                }
            });
        }

        public void Close()
        {
            saveTask.Wait();
            connection.Close();
        }

        public void AddDelta(Vector3I index, int y, int x, int z, Block block)
        {
            dataQueue.Enqueue(new SaveData(index, x, y, z, block));
        }

        public void ApplyDelta(IChunk chunk)
        {
            if (chunk is not Chunk fullChunk) return;

            var command = new SQLiteCommand(connection)
            {
                CommandText = selectCommand
            };

            command.Parameters.AddWithValue("@x", fullChunk.Index.X);
            command.Parameters.AddWithValue("@y", fullChunk.Index.Y);
            command.Parameters.AddWithValue("@z", fullChunk.Index.Z);

            var reader = command.ExecuteReader();

            while (reader.Read())
            {
                int x = reader.GetInt32(0);
                int y = reader.GetInt32(1);
                int z = reader.GetInt32(2);
                ushort block = (ushort)reader.GetInt32(3);

                fullChunk[x, y, z] = new(block);

                if (block != Block.EmptyValue && blockMetadata.IsLightSource(block))
                {
                    fullChunk.AddLightSource(x, y, z);
                }
            }
        }

        void WriteDelta()
        {
            SaveData data;

            while (dataQueue.Count > 0)
            {
                data = dataQueue.Dequeue();
                cmd.CommandText = insertCommand;

                cmd.Parameters.AddWithValue("@chunkX", data.Index.X);
                cmd.Parameters.AddWithValue("@chunkY", data.Index.Y);
                cmd.Parameters.AddWithValue("@chunkZ", data.Index.Z);
                cmd.Parameters.AddWithValue("@x", data.X);
                cmd.Parameters.AddWithValue("@y", data.Y);
                cmd.Parameters.AddWithValue("@z", data.Z);
                cmd.Parameters.AddWithValue("@block", data.Block.Value);

                cmd.ExecuteNonQuery();
            }
        }
    }
}
