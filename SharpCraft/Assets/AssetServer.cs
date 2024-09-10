using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Newtonsoft.Json;

using SharpCraft.Utility;
using SharpCraft.DataModel;

namespace SharpCraft.Assets
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

        public void Load(GraphicsDevice graphics)
        {
            LoadBlocks(graphics);

            //Load menu textures
            string[] textureNames = [.. Directory.GetFiles("Assets/Textures/Menu", ".")];

            for (int i = 0; i < textureNames.Length; i++)
            {
                string textureName = textureNames[i].Split('\\')[1].Split('.')[0];
                menuTextures.Add(textureName, content.Load<Texture2D>("Textures/Menu/" + textureName));
            }

            fonts.Add(content.Load<SpriteFont>("Fonts/font14"));
            fonts.Add(content.Load<SpriteFont>("Fonts/font24"));

            effect = content.Load<Effect>("Effects/BlockEffect");
        }

        void LoadBlocks(GraphicsDevice graphics)
        {
            List<BlockFaceData> blockFacesData;
            using (StreamReader r = new("Assets/multiface_blocks.json"))
            {
                string json = r.ReadToEnd();
                blockFacesData = JsonConvert.DeserializeObject<List<BlockFaceData>>(json);
            }

            List<BlockData> blockData;
            using (StreamReader r = new("Assets/blocks.json"))
            {
                string json = r.ReadToEnd();
                blockData = JsonConvert.DeserializeObject<List<BlockData>>(json);
            }

            string[] blockTextureNames = Directory.GetFiles("Assets/Textures/Blocks", ".");

            blockTextures.Add(Util.GetColoredTexture(graphics, 64, 64, Color.Transparent, 1));
            for (int i = 0; i < blockTextureNames.Length; i++)
            {
                string textureName = blockTextureNames[i].Split('\\')[1].Split('.')[0];
                blockTextures.Add(content.Load<Texture2D>("Textures/Blocks/" + textureName));
                blockNamesToIndices.Add(textureName, (ushort)(i + 1));
            }

            Span<string> sides = ["Front", "Back", "Top", "Bottom", "Right", "Left"];

            foreach (BlockFaceData faceData in blockFacesData)
            {
                var faceMap = faceData.GetType().GetProperties().
                    ToDictionary(x => x.Name, x => x.GetValue(faceData));

                ushort[] faceTextureTypes = new ushort[6];

                for (int i = 0; i < sides.Length; i++)
                {
                    if (faceMap[sides[i]] is null)
                    {
                        faceTextureTypes[i] = blockNamesToIndices[faceMap["Type"].ToString()];
                    }
                    else
                    {
                        faceTextureTypes[i] = blockNamesToIndices[faceMap[sides[i]].ToString()];
                    }
                }

                multifaceBlocks.Add(blockNamesToIndices[faceData.Type], faceTextureTypes);
            }

            foreach (BlockData data in blockData)
            {
                string name = data.Name is null ? Util.Title(data.Type) : data.Name;
                blockNames.Add(blockNamesToIndices[data.Type], name);

                if (data.Transparent)
                {
                    transparentBlocks.Add(blockNamesToIndices[data.Type]);
                }

                if (data.LightLevel > 0)
                {
                    lightSources.Add(blockNamesToIndices[data.Type]);
                }

                if (data.LightLevel > 0)
                {
                    lightSourceValues.Add(blockNamesToIndices[data.Type], (byte)data.LightLevel);
                }
            }
        }
    }
}
