using System;
using System.Collections.Generic;
using System.IO;

using Microsoft.Xna.Framework;
using Newtonsoft.Json;


namespace SharpCraft.Utility
{
    public class Parameters
    {
        public bool Flying = false;

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

        public ushort?[] Inventory = new ushort?[9];

        public DateTime Date = DateTime.Now;

        class SaveParameters
        {
            public int Seed;
            public bool Flying;
            public float X, Y, Z;
            public float DirX, DirY, DirZ;
            public ushort?[] Inventory;
            public string WorldType;
            public int Day, Hour, Minute;
            public DateTime Date;
        }


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

            SaveParameters data;
            using (StreamReader r = new($@"Saves\{saveName}\parameters.json"))
            {
                string json = r.ReadToEnd();
                data = JsonConvert.DeserializeObject<List<SaveParameters>>(json)[0];
            }

            Seed = data.Seed;
            Flying = data.Flying;
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

            List<SaveParameters> data =
                [
                    new SaveParameters()
                    {
                        Seed = Seed,
                        Flying = Flying,
                        X = Position.X,
                        Y = Position.Y,
                        Z = Position.Z,
                        DirX = Direction.X,
                        DirY = Direction.Y,
                        DirZ = Direction.Z,
                        Inventory = Inventory,
                        WorldType = WorldType,
                        Day = Day,
                        Hour = Hour,
                        Minute = Minute,
                        Date = Date
                    }
                ];

            string json = JsonConvert.SerializeObject(data, Formatting.Indented);
            string path = Directory.GetCurrentDirectory() + $@"\Saves\{SaveName}\parameters.json";

            File.WriteAllText(path, json);
        }
    }
}
