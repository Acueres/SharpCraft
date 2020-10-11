using System;
using System.IO;
using System.Data.SQLite;

using Microsoft.Xna.Framework;


namespace SharpCraft
{
    class SaveHandler
    {
        SQLiteCommand cmd;
        SQLiteConnection connection;

        string insertCommand = @"INSERT OR REPLACE INTO chunks(chunkX, chunkZ, x, y, z, texture)
                                VALUES(@chunkX, @chunkZ, @x, @y, @z, @texture)";

        string selectCommand = @"SELECT x, y, z, texture FROM chunks
                                WHERE [chunkX] = @x AND [chunkZ] = @z";

        public SaveHandler()
        {
            string saveName = @"URI=file:" + Directory.GetCurrentDirectory() + @"\Saves\Test\test.db";

            connection = new SQLiteConnection(saveName);
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
        }

        public void AddDelta(Vector3 position, int x, int y, int z, ushort? texture)
        {
            cmd.CommandText = insertCommand;

            cmd.Parameters.AddWithValue("@chunkX", position.X);
            cmd.Parameters.AddWithValue("@chunkZ", position.Z);
            cmd.Parameters.AddWithValue("@x", x);
            cmd.Parameters.AddWithValue("@y", y);
            cmd.Parameters.AddWithValue("@z", z);

            if (texture is null)
            {
                cmd.Parameters.AddWithValue("@texture", -1);
            }
            else
            {
                cmd.Parameters.AddWithValue("@texture", texture);
            }

            cmd.ExecuteNonQuery();
        }

        public void ApplyDelta(Chunk chunk)
        {
            var command = new SQLiteCommand(connection);
            command.CommandText = selectCommand;

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
    }
}
