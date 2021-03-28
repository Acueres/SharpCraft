using System.IO;
using System.Collections.Generic;

using Newtonsoft.Json;


namespace SharpCraft.Utility
{
    static class Settings
    {
        public static int
        ChunkSize = 16,
        RenderDistance = 8;

        class SettingsData
        {
            public int
            ChunkSize,
            RenderDistance;
        }

        public static void Load()
        {
            if (File.Exists(@"settings.json"))
            {
                SettingsData data;
                using (StreamReader r = new StreamReader("settings.json"))
                {
                    string json = r.ReadToEnd();
                    data = JsonConvert.DeserializeObject<List<SettingsData>>(json)[0];
                }

                RenderDistance = data.RenderDistance;
            }
        }

        public static void Save()
        {
            List<SettingsData> data = new List<SettingsData>(1)
            {
                new SettingsData()
                {
                    RenderDistance = RenderDistance,
                }
            };

            string json = JsonConvert.SerializeObject(data, Formatting.Indented);
            string path = "settings.json";

            File.WriteAllText(path, json);
        }
    }
}
