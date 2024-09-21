using System.Collections.Generic;
using System.IO;
using System.Data.SQLite;
using System.Threading.Tasks;

using SharpCraft.World;
using SharpCraft.Utility;


namespace SharpCraft.Handlers
{
    public class DatabaseHandler
    {
        readonly BlockMetadataProvider blockMetadata;
        SQLiteCommand cmd;
        SQLiteConnection connection;

        const string insertCommand = @"INSERT OR REPLACE INTO chunks(chunkX, chunkZ, x, y, z, texture)
                                VALUES(@chunkX, @chunkZ, @x, @y, @z, @texture)";

        const string selectCommand = @"SELECT x, y, z, texture FROM chunks
                                WHERE [chunkX] = @x AND [chunkZ] = @z";

        Queue<SaveData> dataQueue;

        Task saveTask;

        class SaveData
        {
            public Vector3I Position;
            public int X;
            public int Y;
            public int Z;
            public ushort Texture;

            public SaveData(Vector3I position, int x, int y, int z, ushort texture)
            {
                Position = position;
                X = x;
                Y = y;
                Z = z;
                Texture = texture;
            }
        }


        public DatabaseHandler(MainGame game, string saveName, BlockMetadataProvider blockMetadata)
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
                                chunkZ INTEGER,
                                x INTEGER,
                                y INTEGER,
                                z INTEGER,
                                texture INTEGER,
                                PRIMARY KEY(chunkX, chunkZ, x, y, z))"
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

        public void AddDelta(Vector3I position, int y, int x, int z, ushort texture)
        {
            dataQueue.Enqueue(new SaveData(position, x, y, z, texture));
        }

        public void ApplyDelta(Chunk chunk)
        {
            var command = new SQLiteCommand(connection)
            {
                CommandText = selectCommand
            };

            command.Parameters.AddWithValue("@x", chunk.Position.X);
            command.Parameters.AddWithValue("@z", chunk.Position.Z);

            var reader = command.ExecuteReader();

            while (reader.Read())
            {
                ushort texture;
                int value = reader.GetInt32(3);

                texture = (ushort)value;

                int x = reader.GetInt32(0);
                int y = reader.GetInt32(1);
                int z = reader.GetInt32(2);

                chunk[x, y, z] = new(texture);

                if (texture != Block.EmptyValue && blockMetadata.IsLightSource(texture))
                {
                    chunk.AddLightSource(y, x, z);
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

                cmd.Parameters.AddWithValue("@chunkX", data.Position.X);
                cmd.Parameters.AddWithValue("@chunkZ", data.Position.Z);
                cmd.Parameters.AddWithValue("@x", data.X);
                cmd.Parameters.AddWithValue("@y", data.Y);
                cmd.Parameters.AddWithValue("@z", data.Z);

                cmd.Parameters.AddWithValue("@texture", data.Texture);

                cmd.ExecuteNonQuery();
            }
        }
    }
}
