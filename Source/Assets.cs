using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;

using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Newtonsoft.Json;


namespace SharpCraft
{
    static class Assets
    {
        public static Dictionary<string, Texture2D> MenuTextures { get; private set; }
        public static Dictionary<ushort, string> BlockNames { get; private set; }
        public static Dictionary<string, ushort> BlockIndices { get; private set; }
        public static Dictionary<ushort, ushort[]> MultifaceBlocks { get; private set; }
        public static bool[] TransparentBlocks { get; private set; }
        public static Texture2D[] BlockTextures { get; private set; }
        public static SpriteFont[] Fonts { get; private set; }
        public static Effect Effect { get; private set; }

        class MultifaceData
        {
            public string type, front, back, top, bottom, right, left;
        }

        class BlockData
        {
            public string name, type;
        }


        public static void Load(ContentManager content)
        {
            MenuTextures = new Dictionary<string, Texture2D>(100);
            BlockNames = new Dictionary<ushort, string>(ushort.MaxValue);
            BlockIndices = new Dictionary<string, ushort>(ushort.MaxValue);
            MultifaceBlocks = new Dictionary<ushort, ushort[]>(100);

            LoadBlocks(content);

            //Load menu textures
            var textureNames = Directory.GetFiles("Assets/Textures/Menu", ".").ToArray();

            for (int i = 0; i < textureNames.Length; i++)
            {
                var t = textureNames[i].Split('\\')[1].Split('.')[0];
                MenuTextures.Add(t, content.Load<Texture2D>("Textures/Menu/" + t));
            }

            Fonts = new SpriteFont[2];

            Fonts[0] = content.Load<SpriteFont>("Fonts/font14");
            Fonts[1] = content.Load<SpriteFont>("Fonts/font24");

            Effect = content.Load<Effect>("Shaders/BlockEffect");
        }

        static void LoadBlocks(ContentManager content)
        {
            List<MultifaceData> blockData;
            using (StreamReader r = new StreamReader("Assets/multiface_blocks.json"))
            {
                string json = r.ReadToEnd();
                blockData = JsonConvert.DeserializeObject<List<MultifaceData>>(json);
            }

            List<BlockData> blockNameData;
            using (StreamReader r = new StreamReader("Assets/blocks.json"))
            {
                string json = r.ReadToEnd();
                blockNameData = JsonConvert.DeserializeObject<List<BlockData>>(json);
            }

            var blockTextureNames = Directory.GetFiles("Assets/Textures/Blocks", ".");
            BlockTextures = new Texture2D[blockTextureNames.Length];

            for (ushort i = 0; i < blockTextureNames.Length; i++)
            {
                var t = blockTextureNames[i].Split('\\')[1].Split('.')[0];
                BlockTextures[i] = content.Load<Texture2D>("Textures/Blocks/" + t);
                BlockIndices.Add(t, i);
            }

            string[] sides = { "front", "back", "top", "bottom", "right", "left" };

            foreach (var entry in blockData)
            {
                if (entry.type is null)
                    continue;

                var sideData = entry.GetType().GetFields().
                    ToDictionary(x => x.Name, x => x.GetValue(entry));

                ushort[] arr = new ushort[6];

                for (int i = 0; i < sides.Length; i++)
                {
                    if (sideData[sides[i]] is null)
                    {
                        arr[i] = BlockIndices[sideData["type"].ToString()];
                    }
                    else
                    {
                        arr[i] = BlockIndices[sideData[sides[i]].ToString()];
                    }
                }

                MultifaceBlocks.Add(BlockIndices[entry.type], arr);
            }

            foreach (var entry in blockNameData)
            {
                string name = entry.name is null ? Util.Title(entry.type) : entry.name;

                BlockNames.Add(BlockIndices[entry.type], name);
            }

            BlockIndices = BlockNames.ToDictionary(x => x.Value, x => x.Key);

            TransparentBlocks = new bool[BlockTextures.Length];

            for (int i = 0; i < TransparentBlocks.Length; i++)
            {
                if (BlockIndices["Glass"] == i)
                    TransparentBlocks[i] = true;
                else if (BlockIndices["Water"] == i)
                    TransparentBlocks[i] = true;
            }
        }
    }
}
