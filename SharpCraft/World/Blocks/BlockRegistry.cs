using SharpCraft.World.Blocks.Serialization;
using SharpCraft.Extensions;

using System.Text.Json;
using SharpCraft.AssetProcessing;

namespace SharpCraft.World.Blocks;

internal class BlockRegistry
    {
        private readonly Dictionary<uint, string> textureIdToName;
        private readonly Dictionary<string, uint> textureNameToId;
        private readonly Dictionary<uint, uint[]> multifaceTextures;
        private readonly HashSet<uint> transparentTextures;
        private readonly HashSet<uint> lightSources;
        private readonly Dictionary<uint, byte> lightValues;

        public int BlockCount => textureNameToId.Count;
        public uint[] GetBlockIds => [.. textureIdToName.Keys];
        public uint GetBlockId(string name) => textureNameToId[name];
        public string GetBlockName(uint index) => textureIdToName[index];
        public bool IsBlockTransparent(Block block) => transparentTextures.Contains(block.Value);
        public bool IsBlockMultiface(Block block) => multifaceTextures.ContainsKey(block.Value);
        public uint GetMultifaceBlockFace(Block block, FaceDirection faceDirection) => multifaceTextures[block.Value][(byte)faceDirection];
        public byte GetLightSourceValue(Block block) => lightValues[block.Value];
        public bool IsLightSource(Block block) => lightSources.Contains(block.Value);
        
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        public BlockRegistry()
        {
            var blockFaceData = GetBlockFaceData();
            var blockData = GetBlockData();
            textureNameToId = GetBlockNameToId();
            multifaceTextures = GetMultifaceBlocks(blockFaceData, textureNameToId);
            textureIdToName = GetBlockIdToName(blockData, textureNameToId);
            transparentTextures = GetTransparentBlocks(blockData, textureNameToId);
            (lightSources, lightValues) = GetLightSources(blockData, textureNameToId);
        }

        private List<BlockDto> GetBlockData()
        {
            return LoadJson<List<BlockDto>>("blocks.json");
        }

        private List<BlockFaceDto> GetBlockFaceData()
        {
            return LoadJson<List<BlockFaceDto>>("multiface_blocks.json");
        }

        private T LoadJson<T>(string fileName)
        {
            string path = AssetServer.GetAssetPath(fileName);
            string json = File.ReadAllText(path);

            return JsonSerializer.Deserialize<T>(json, JsonOptions)
                   ?? throw new InvalidDataException($"Failed to deserialize {fileName}.");
        }

        private Dictionary<string, uint> GetBlockNameToId()
        {
            string blocksPath = AssetServer.GetAssetPath("Textures", "Blocks");
            string[] texturePaths = Directory.GetFiles(blocksPath)
                .Where(f => f.EndsWith(".png", StringComparison.OrdinalIgnoreCase) 
                            || f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) 
                            || f.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase))
                .Order()
                .ToArray();
            Dictionary<string, uint> result = new(texturePaths.Length);

            for (uint i = 0; i < texturePaths.Length; i++)
            {
                string textureName = Path.GetFileNameWithoutExtension(texturePaths[i]);
                result.Add(textureName, i + 1);
            }

            return result;
        }

        private static Dictionary<uint, uint[]> GetMultifaceBlocks(
            List<BlockFaceDto> blockFaceData,
            Dictionary<string, uint> textureNameToId)
        {
            Dictionary<uint, uint[]> result = [];

            foreach (BlockFaceDto data in blockFaceData)
            {
                uint baseTexture = GetTextureId(textureNameToId, data.Type);

                uint[] faceTextures = new uint[6];

                faceTextures[(int)FaceDirection.ZPos] = GetTextureId(textureNameToId, data.Front ?? data.Type);
                faceTextures[(int)FaceDirection.ZNeg] = GetTextureId(textureNameToId, data.Back ?? data.Type);
                faceTextures[(int)FaceDirection.XPos] = GetTextureId(textureNameToId, data.Right ?? data.Type);
                faceTextures[(int)FaceDirection.XNeg] = GetTextureId(textureNameToId, data.Left ?? data.Type);
                faceTextures[(int)FaceDirection.YPos] = GetTextureId(textureNameToId, data.Top ?? data.Type);
                faceTextures[(int)FaceDirection.YNeg] = GetTextureId(textureNameToId, data.Bottom ?? data.Type);

                result.Add(baseTexture, faceTextures);
            }

            return result;
        }
        
        
        private static uint GetTextureId(Dictionary<string, uint> textureNameToId, string name)
        {
            if (!textureNameToId.TryGetValue(name, out uint id))
            {
                throw new InvalidDataException($"Unknown block texture/type '{name}'.");
            }

            return id;
        }

        private static Dictionary<uint, string> GetBlockIdToName(List<BlockDto> blockData,
            Dictionary<string, uint> blockNameToId)
        {
            Dictionary<uint, string> blockIdToName = [];

            foreach (BlockDto data in blockData)
            {
                string name = data.Name ?? data.Type.CapitalizeFirst();
                blockIdToName.Add(blockNameToId[data.Type], name);
            }

            return blockIdToName;
        }

        private static HashSet<uint> GetTransparentBlocks(List<BlockDto> blockData,
            Dictionary<string, uint> blockNameToId)
        {
            HashSet<uint> transparentBlocks = [];

            foreach (BlockDto data in blockData)
            {
                if (data.Transparent)
                {
                    transparentBlocks.Add(blockNameToId[data.Type]);
                }
            }

            return transparentBlocks;
        }

        private static (HashSet<uint>, Dictionary<uint, byte>) GetLightSources(List<BlockDto> blockData,
            Dictionary<string, uint> blockNameToId)
        {
            HashSet<uint> lightSources = [];
            Dictionary<uint, byte> lightValues = [];

            foreach (BlockDto data in blockData)
            {
                if (data.LightLevel > 0)
                {
                    lightSources.Add(blockNameToId[data.Type]);
                    lightValues.Add(blockNameToId[data.Type], (byte)data.LightLevel);
                }
            }

            return (lightSources, lightValues);
        }
    }