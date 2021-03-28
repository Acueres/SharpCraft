using System;
using System.Data.SQLite;
using System.Collections.Generic;
using System.IO;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;


namespace SharpCraft.Utility
{
    class Save
    {
        public string Name;
        public Parameters Parameters;
        public Texture2D Icon;


        public Save(Texture2D icon, string name, Parameters parameters)
        {
            Icon = icon;
            Name = name;
            Parameters = parameters;

            Parameters.SaveName = Name;
        }

        public Save(string name, Parameters parameters)
        {
            Name = name;
            Parameters = parameters;

            Parameters.SaveName = Name;
        }

        public void Clear()
        {
            if (File.Exists(@$"Saves\{Name}\data.db"))
            {
                string path = @"URI=file:" + Directory.GetCurrentDirectory() + @$"\Saves\{Name}\data.db";

                var connection = new SQLiteConnection(path);
                connection.Open();

                var cmd = new SQLiteCommand(connection)
                {
                    CommandText = "DROP TABLE IF EXISTS chunks"
                };
                cmd.ExecuteNonQuery();

                connection.Close();
            }

            string saveName = Parameters.SaveName;
            string worldType = Parameters.WorldType;
            int seed = Parameters.Seed;
            DateTime date = Parameters.Date;

            Parameters = new Parameters
            {
                SaveName = saveName,
                WorldType = worldType,
                Seed = seed,
                Date = date
            };

            Parameters.Save();
        }

        public static List<Save> LoadAll(GraphicsDevice graphics)
        {
            List<Save> saves = new List<Save>();
            string[] saveNames = Directory.GetDirectories("Saves");

            for (int i = 0; i < saveNames.Length; i++)
            {
                if (!File.Exists($@"{saveNames[i]}\parameters.json"))
                {
                    continue;
                }

                string saveName = saveNames[i].Split('\\')[1];
                saves.Add(Load(graphics, saveName));
            }

            return saves;
        }

        public static Save Load(GraphicsDevice graphics, string name)
        {
            using FileStream fileStream = new FileStream(@$"Saves\{name}\save_icon.png", FileMode.Open);
            Texture2D icon = Texture2D.FromStream(graphics, fileStream);

            return new Save(icon, name, new Parameters(name));
        }
    }
}
