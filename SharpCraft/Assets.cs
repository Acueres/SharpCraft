using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Collections.ObjectModel;

using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Newtonsoft.Json;

using SharpCraft.Utility;


namespace SharpCraft
{
    static class Assets
    {
        public static ReadOnlyDictionary<string, Texture2D> MenuTextures { get; private set; }
        public static ReadOnlyDictionary<ushort, string> BlockNames { get; private set; }
        public static ReadOnlyDictionary<string, ushort> BlockIndices { get; private set; }
        public static ReadOnlyDictionary<ushort, ushort[]> MultifaceBlocks { get; private set; }
        public static bool[] Multiface { get; private set; }
        public static ReadOnlyDictionary<ushort, byte> LightValues { get; private set; }

        public static IList<bool> TransparentBlocks { get; private set; }
        public static IList<bool> LightSources { get; private set; }

        public static IList<Texture2D> BlockTextures { get; private set; }
        public static IList<SpriteFont> Fonts { get; private set; }

        public static Effect Effect { get; private set; }

        class MultifaceData
        {
            public string type, front, back, top, bottom, right, left;
        }

        class BlockData
        {
            public string name, type;
            public bool transparent;
            public int lightLevel;
        }


        public static void Load(ContentManager content)
        {
            var menuTextures = new Dictionary<string, Texture2D>(100);

            LoadBlocks(content);

            //Load menu textures
            var textureNames = Directory.GetFiles("Content/Textures/Menu", ".").ToArray();

            for (int i = 0; i < textureNames.Length; i++)
            {
                var t = textureNames[i].Split('\\')[1].Split('.')[0];
                menuTextures.Add(t, content.Load<Texture2D>("Textures/Menu/" + t));
            }

            MenuTextures = new ReadOnlyDictionary<string, Texture2D>(menuTextures);

            var fonts = new SpriteFont[2];

            fonts[0] = content.Load<SpriteFont>("Fonts/font14");
            fonts[1] = content.Load<SpriteFont>("Fonts/font24");

            Fonts = Array.AsReadOnly(fonts);

            Effect = content.Load<Effect>("Effects/BlockEffect");
        }

        static void LoadBlocks(ContentManager content)
        {
            var blockNames = new Dictionary<ushort, string>(ushort.MaxValue);
            var blockIndices = new Dictionary<string, ushort>(ushort.MaxValue);
            var multifaceBlocks = new Dictionary<ushort, ushort[]>(100);
            var lightValues = new Dictionary<ushort, byte>(100);

            List<MultifaceData> blockFacesData;
            using (StreamReader r = new("Content/multiface_blocks.json"))
            {
                string json = r.ReadToEnd();
                blockFacesData = JsonConvert.DeserializeObject<List<MultifaceData>>(json);
            }

            List<BlockData> blockData;
            using (StreamReader r = new("Content/blocks.json"))
            {
                string json = r.ReadToEnd();
                blockData = JsonConvert.DeserializeObject<List<BlockData>>(json);
            }

            var blockTextureNames = Directory.GetFiles("Content/Textures/Blocks", ".");
            var blockTextures = new Texture2D[blockTextureNames.Length];

            for (int i = 0; i < blockTextureNames.Length; i++)
            {
                var t = blockTextureNames[i].Split('\\')[1].Split('.')[0];
                blockTextures[i] = content.Load<Texture2D>("Textures/Blocks/" + t);
                blockIndices.Add(t, (ushort)i);
            }

            string[] sides = ["front", "back", "top", "bottom", "right", "left"];

            foreach (var entry in blockFacesData)
            {
                var faceData = entry.GetType().GetFields().
                    ToDictionary(x => x.Name, x => x.GetValue(entry));

                ushort[] faceTextures = new ushort[6];

                for (int i = 0; i < sides.Length; i++)
                {
                    if (faceData[sides[i]] is null)
                    {
                        faceTextures[i] = blockIndices[faceData["type"].ToString()];
                    }
                    else
                    {
                        faceTextures[i] = blockIndices[faceData[sides[i]].ToString()];
                    }
                }

                multifaceBlocks.Add(blockIndices[entry.type], faceTextures);
            }

            var transparentBlocks = new bool[blockTextures.Length];
            var lightSources = new bool[blockTextures.Length];

            foreach (var entry in blockData)
            {
                string name = entry.name is null ? Util.Title(entry.type) : entry.name;
                blockNames.Add(blockIndices[entry.type], name);

                transparentBlocks[blockIndices[entry.type]] = entry.transparent;
                lightSources[blockIndices[entry.type]] = entry.lightLevel > 0;

                if (entry.lightLevel > 0)
                {
                    lightValues.Add(blockIndices[entry.type], (byte)entry.lightLevel);
                }
            }

            blockIndices = blockNames.ToDictionary(x => x.Value, x => x.Key);

            BlockNames = new ReadOnlyDictionary<ushort, string>(blockNames);
            BlockIndices = new ReadOnlyDictionary<string, ushort>(blockIndices);
            MultifaceBlocks = new ReadOnlyDictionary<ushort, ushort[]>(multifaceBlocks);
            LightValues = new ReadOnlyDictionary<ushort, byte>(lightValues);

            TransparentBlocks = Array.AsReadOnly(transparentBlocks);
            LightSources = Array.AsReadOnly(lightSources);

            BlockTextures = Array.AsReadOnly(blockTextures);

            Multiface = new bool[BlockTextures.Count];
            for (ushort i = 0; i < Multiface.Length; i++)
            {
                if (multifaceBlocks.ContainsKey(i))
                {
                    Multiface[i] = true;
                }
            }
        }
    }
}
