using System.IO;
using Newtonsoft.Json;
using SharpCraft.DataModel;

namespace SharpCraft.Persistence
{
    static class Settings
    {
        public static int RenderDistance { get; set; } = 8;

        public static void Load()
        {
            if (File.Exists(@"settings.json"))
            {
                SettingsData data;
                using StreamReader r = new("settings.json");

                string json = r.ReadToEnd();
                data = JsonConvert.DeserializeObject<SettingsData>(json);

                RenderDistance = data.RenderDistance;
            }
        }

        public static void Save()
        {
            SettingsData data = new(RenderDistance);

            string json = JsonConvert.SerializeObject(data, Formatting.Indented);
            string path = "settings.json";

            File.WriteAllText(path, json);
        }
    }
}
