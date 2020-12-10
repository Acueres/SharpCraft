using System;
using System.Collections.Generic;
using System.IO;
using System.Data.SQLite;
using System.Threading.Tasks;

using Microsoft.Xna.Framework;


namespace SharpCraft
{
    class DatabaseHandler
    {
        SQLiteCommand cmd;
        SQLiteConnection connection;

        string insertCommand = @"INSERT OR REPLACE INTO chunks(chunkX, chunkZ, x, y, z, texture)
                                VALUES(@chunkX, @chunkZ, @x, @y, @z, @texture)";

        string selectCommand = @"SELECT x, y, z, texture FROM chunks
                                WHERE [chunkX] = @x AND [chunkZ] = @z";

        Queue<SaveData> dataQueue;

        Task saveTask;

        struct SaveData
        {
            public Vector3 Position;
            public int X;
            public int Y;
            public int Z;
            public ushort? Texture;

            public SaveData(Vector3 position, int x, int y, int z, ushort? texture)
            {
                Position = position;
                X = x;
                Y = y;
                Z = z;
                Texture = texture;
            }
        }


        public DatabaseHandler(MainGame game, string saveName)
        {
            dataQueue = new Queue<SaveData>(10);
            
            string path = @"URI=file:" + Directory.GetCurrentDirectory() + $@"\Saves\{saveName}\data.db";
            connection = new SQLiteConnection(path);
            connection.Open();

            cmd = new SQLiteCommand(connection)
            {
                CommandText = @"CREATE TABLE IF NOT EXISTS chunks(
                                chunkX INTEGER NOT NULL,
                                chunkZ INTEGER NOT NULL,
                                x INTEGER NOT NULL,
                                y INTEGER NOT NULL,
                                z INTEGER NOT NULL,
                                texture INTEGER NOT NULL,
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

        public void AddDelta(Vector3 position, int y, int x, int z, ushort? texture)
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
                ushort? texture;
                int value = reader.GetInt32(3);

                if (value == -1)
                {
                    texture = null;
                }
                else
                {
                    texture = (ushort)value;
                }

                chunk.Blocks[reader.GetInt32(1)][reader.GetInt32(0)][reader.GetInt32(2)] = texture;
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

                if (data.Texture is null)
                {
                    cmd.Parameters.AddWithValue("@texture", -1);
                }
                else
                {
                    cmd.Parameters.AddWithValue("@texture", data.Texture);
                }

                cmd.ExecuteNonQuery();
            }
        }
    }
}
