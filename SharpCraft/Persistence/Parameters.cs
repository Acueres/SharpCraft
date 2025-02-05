using System;
using System.IO;

using Microsoft.Xna.Framework;
using Newtonsoft.Json;
using SharpCraft.DataModel;

namespace SharpCraft.Persistence
{
    public class Parameters
    {
        public bool IsFlying = false;

        public int
        Seed = 0,
        Day = 1,
        Hour = 6,
        Minute = 0;

        public string WorldType = "Default";
        public string SaveName;

        public Vector3
        Position = Vector3.Zero,
        Direction = new(0, -0.5f, -1f);

        public ushort[] Inventory = new ushort[9];

        public DateTime Date = DateTime.Now;

        public Parameters(string saveName)
        {
            Load(saveName);
        }

        public Parameters()
        {
            var rnd = new Random();
            Seed = rnd.Next();
        }

        public void Load(string saveName)
        {
            SaveName = saveName;

            ParametersData data;
            using (StreamReader r = new($@"Saves\{saveName}\parameters.json"))
            {
                string json = r.ReadToEnd();
                data = JsonConvert.DeserializeObject<ParametersData>(json);
            }

            Seed = data.Seed;
            IsFlying = data.IsFlying;
            Position = new Vector3(data.X, data.Y, data.Z);
            Direction = new Vector3(data.DirX, data.DirY, data.DirZ);
            Inventory = data.Inventory;
            WorldType = data.WorldType;
            Day = data.Day;
            Hour = data.Hour;
            Minute = data.Minute;
            Date = data.Date;
        }

        public void Save()
        {
            Date = DateTime.Now;

            ParametersData data = new(Seed, IsFlying, Position.X, Position.Y, Position.Z,
                Direction.X, Direction.Y, Direction.Z, Inventory, WorldType, Day, Hour, Minute, Date);

            string json = JsonConvert.SerializeObject(data, Formatting.Indented);
            string path = Directory.GetCurrentDirectory() + $@"\Saves\{SaveName}\parameters.json";

            File.WriteAllText(path, json);
        }
    }
}
