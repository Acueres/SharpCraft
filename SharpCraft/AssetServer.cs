using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

using System.Collections.Generic;
using System.IO;
using System.Linq;

using Newtonsoft.Json;

using SharpCraft.Utility;

namespace SharpCraft
{
    public class AssetServer(ContentManager content)
    {
        readonly ContentManager content = content;

        readonly Dictionary<string, Texture2D> menuTextures = [];
        readonly Dictionary<ushort, string> blockNames = [];
        readonly Dictionary<string, ushort> blockNamesToIndices = [];
        readonly Dictionary<ushort, ushort[]> multifaceBlocks = [];
        readonly Dictionary<ushort, byte> lightSourceValues = [];
        readonly HashSet<ushort> transparentBlocks = [];
        readonly HashSet<ushort> lightSources = [];
        readonly List<Texture2D> blockTextures = [];
        readonly List<SpriteFont> fonts = [];

        Effect effect;

        public int GetBlocksCount => blockTextures.Count;
        public ushort[] GetInteractiveBlockIndices => [.. blockNames.Keys];
        public Effect GetEffect => effect.Clone();

        public ushort GetBlockIndex(string name) => blockNamesToIndices[name];
        public string GetBlockName(ushort index) => blockNames[index];
        public Texture2D GetBlockTexture(ushort index) => blockTextures[index];
        public bool IsBlockTransparent(ushort index) => transparentBlocks.Contains(index);
        public bool IsBlockMultiface(ushort index) => multifaceBlocks.ContainsKey(index);
        public ushort GetMultifaceBlockFace(ushort blockIndex, ushort faceIndex) => multifaceBlocks[blockIndex][faceIndex];
        public byte GetLightSourceValue(ushort index) => lightSourceValues[index];
        public bool IsLightSource(ushort index) => lightSources.Contains(index);
        public Texture2D GetMenuTexture(string name) => menuTextures[name];
        public SpriteFont GetFont(int index) => fonts[index];

        public void Load()
        {
            LoadBlocks();

            //Load menu textures
            string[] textureNames = [.. Directory.GetFiles("Content/Textures/Menu", ".")];

            for (int i = 0; i < textureNames.Length; i++)
            {
                string textureName = textureNames[i].Split('\\')[1].Split('.')[0];
                menuTextures.Add(textureName, content.Load<Texture2D>("Textures/Menu/" + textureName));
            }

            fonts.Add(content.Load<SpriteFont>("Fonts/font14"));
            fonts.Add(content.Load<SpriteFont>("Fonts/font24"));

            effect = content.Load<Effect>("Effects/BlockEffect");
        }

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

        void LoadBlocks()
        {
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

            string[] blockTextureNames = Directory.GetFiles("Content/Textures/Blocks", ".");

            for (int i = 0; i < blockTextureNames.Length; i++)
            {
                var t = blockTextureNames[i].Split('\\')[1].Split('.')[0];
                blockTextures.Add(content.Load<Texture2D>("Textures/Blocks/" + t));
                blockNamesToIndices.Add(t, (ushort)i);
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
                        faceTextures[i] = blockNamesToIndices[faceData["type"].ToString()];
                    }
                    else
                    {
                        faceTextures[i] = blockNamesToIndices[faceData[sides[i]].ToString()];
                    }
                }

                multifaceBlocks.Add(blockNamesToIndices[entry.type], faceTextures);
            }

            foreach (var entry in blockData)
            {
                string name = entry.name is null ? Util.Title(entry.type) : entry.name;
                blockNames.Add(blockNamesToIndices[entry.type], name);

                if (entry.transparent)
                {
                    transparentBlocks.Add(blockNamesToIndices[entry.type]);
                }

                if (entry.lightLevel > 0)
                {
                    lightSources.Add(blockNamesToIndices[entry.type]);
                }

                if (entry.lightLevel > 0)
                {
                    lightSourceValues.Add(blockNamesToIndices[entry.type], (byte)entry.lightLevel);
                }
            }
        }
    }
}
