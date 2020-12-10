using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Collections.ObjectModel;

using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Newtonsoft.Json;


namespace SharpCraft
{
    static class Assets
    {
        public static ReadOnlyDictionary<string, Texture2D> MenuTextures { get; private set; }
        public static ReadOnlyDictionary<ushort, string> BlockNames { get; private set; }
        public static ReadOnlyDictionary<string, ushort> BlockIndices { get; private set; }
        public static ReadOnlyDictionary<ushort, ushort[]> MultifaceBlocks { get; private set; }
        public static IList<bool> TransparentBlocks { get; private set; }
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
        }


        public static void Load(ContentManager content)
        {
            var menuTextures = new Dictionary<string, Texture2D>(100);

            LoadBlocks(content);

            //Load menu textures
            var textureNames = Directory.GetFiles("Assets/Textures/Menu", ".").ToArray();

            for (int i = 0; i < textureNames.Length; i++)
            {
                var t = textureNames[i].Split('\\')[1].Split('.')[0];
                menuTextures.Add(t, content.Load<Texture2D>("Textures/Menu/" + t));
            }

            var fonts = new SpriteFont[2];

            fonts[0] = content.Load<SpriteFont>("Fonts/font14");
            fonts[1] = content.Load<SpriteFont>("Fonts/font24");

            Fonts = Array.AsReadOnly(fonts);

            Effect = content.Load<Effect>("Shaders/BlockEffect");

            MenuTextures = new ReadOnlyDictionary<string, Texture2D>(menuTextures);
        }

        static void LoadBlocks(ContentManager content)
        {
            var blockNames = new Dictionary<ushort, string>(ushort.MaxValue);
            var blockIndices = new Dictionary<string, ushort>(ushort.MaxValue);
            var multifaceBlocks = new Dictionary<ushort, ushort[]>(100);

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
            var blockTextures = new Texture2D[blockTextureNames.Length];

            for (ushort i = 0; i < blockTextureNames.Length; i++)
            {
                var t = blockTextureNames[i].Split('\\')[1].Split('.')[0];
                blockTextures[i] = content.Load<Texture2D>("Textures/Blocks/" + t);
                blockIndices.Add(t, i);
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
                        arr[i] = blockIndices[sideData["type"].ToString()];
                    }
                    else
                    {
                        arr[i] = blockIndices[sideData[sides[i]].ToString()];
                    }
                }

                multifaceBlocks.Add(blockIndices[entry.type], arr);
            }

            foreach (var entry in blockNameData)
            {
                string name = entry.name is null ? Util.Title(entry.type) : entry.name;

                blockNames.Add(blockIndices[entry.type], name);
            }

            blockIndices = blockNames.ToDictionary(x => x.Value, x => x.Key);

            var transparentBlocks = new bool[blockTextures.Length];

            for (int i = 0; i < transparentBlocks.Length; i++)
            {
                if (blockIndices["Glass"] == i)
                    transparentBlocks[i] = true;
                else if (blockIndices["Water"] == i)
                    transparentBlocks[i] = true;
            }

            BlockNames = new ReadOnlyDictionary<ushort, string>(blockNames);
            BlockIndices = new ReadOnlyDictionary<string, ushort>(blockIndices);
            MultifaceBlocks = new ReadOnlyDictionary<ushort, ushort[]>(multifaceBlocks);
            TransparentBlocks = Array.AsReadOnly(transparentBlocks);
            BlockTextures = Array.AsReadOnly(blockTextures);
        }
    }
}
