using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Newtonsoft.Json;
using SharpCraft.DataModel;
using SharpCraft.Utility;

namespace SharpCraft.World.Blocks
{
    public class BlockMetadataProvider(string rootDirectory)
    {
        readonly string rootDirectory = rootDirectory;

        Dictionary<ushort, string> blockIdToName = [];
        Dictionary<string, ushort> blockNameToId = [];
        Dictionary<ushort, ushort[]> multifaceBlocks = [];
        HashSet<ushort> transparentBlocks = [];
        HashSet<ushort> lightSources = [];
        Dictionary<ushort, byte> lightValues = [];

        public int BlockCount => blockNameToId.Count;
        public ushort[] GetBlockIds => [.. blockIdToName.Keys];
        public ushort GetBlockIndex(string name) => blockNameToId[name];
        public string GetBlockName(ushort index) => blockIdToName[index];
        public bool IsBlockTransparent(ushort index) => transparentBlocks.Contains(index);
        public bool IsBlockMultiface(ushort index) => multifaceBlocks.ContainsKey(index);
        public ushort GetMultifaceBlockFace(ushort blockIndex, Faces face) => multifaceBlocks[blockIndex][(byte)face];
        public byte GetLightSourceValue(ushort index) => lightValues[index];
        public bool IsLightSource(ushort index) => lightSources.Contains(index);

        public void Load()
        {
            var blockFaceData = GetBlockFaceData();
            var blockData = GetBlockData();
            blockNameToId = GetBlockNameToId();
            multifaceBlocks = GetMultifaceBlocks(blockFaceData, blockNameToId);
            blockIdToName = GetBlockIdToName(blockData, blockNameToId);
            transparentBlocks = GetTransparentBlocks(blockData, blockNameToId);
            (lightSources, lightValues) = GetLightSources(blockData, blockNameToId);
        }

        List<BlockData> GetBlockData()
        {
            List<BlockData> blockData;
            using StreamReader r = new($"{rootDirectory}/blocks.json");

            string json = r.ReadToEnd();
            blockData = JsonConvert.DeserializeObject<List<BlockData>>(json);
            return blockData;
        }

        List<BlockFaceData> GetBlockFaceData()
        {
            List<BlockFaceData> blockFacesData;
            using StreamReader r = new($"{rootDirectory}/multiface_blocks.json");
            string json = r.ReadToEnd();
            blockFacesData = JsonConvert.DeserializeObject<List<BlockFaceData>>(json);
            return blockFacesData;
        }

        Dictionary<string, ushort> GetBlockNameToId()
        {
            string[] blockTextureNames = Directory.GetFiles($"{rootDirectory}/Textures/Blocks", ".");
            Dictionary<string, ushort> blockNameToId = new(blockTextureNames.Length);

            for (int i = 0; i < blockTextureNames.Length; i++)
            {
                string textureName = blockTextureNames[i].Split('\\')[1].Split('.')[0];
                blockNameToId.Add(textureName, (ushort)(i + 1));
            }

            return blockNameToId;
        }

        static Dictionary<ushort, ushort[]> GetMultifaceBlocks(List<BlockFaceData> blockFaceData, Dictionary<string, ushort> blockFaceToId)
        {
            Span<string> sides = ["Front", "Back", "Top", "Bottom", "Right", "Left"];
            Dictionary<ushort, ushort[]> multifaceBlocks = [];

            foreach (BlockFaceData faceData in blockFaceData)
            {
                var faceMap = faceData.GetType().GetProperties().
                    ToDictionary(x => x.Name, x => x.GetValue(faceData));

                ushort[] faceTextureTypes = new ushort[6];

                for (int i = 0; i < sides.Length; i++)
                {
                    if (faceMap[sides[i]] is null)
                    {
                        faceTextureTypes[i] = blockFaceToId[faceMap["Type"].ToString()];
                    }
                    else
                    {
                        faceTextureTypes[i] = blockFaceToId[faceMap[sides[i]].ToString()];
                    }
                }

                multifaceBlocks.Add(blockFaceToId[faceData.Type], faceTextureTypes);
            }

            return multifaceBlocks;
        }

        static Dictionary<ushort, string> GetBlockIdToName(List<BlockData> blockData, Dictionary<string, ushort> blockNameToId)
        {
            Dictionary<ushort, string> blockIdToName = [];

            foreach (BlockData data in blockData)
            {
                string name = data.Name is null ? Util.Title(data.Type) : data.Name;
                blockIdToName.Add(blockNameToId[data.Type], name);
            }

            return blockIdToName;
        }

        static HashSet<ushort> GetTransparentBlocks(List<BlockData> blockData, Dictionary<string, ushort> blockNameToId)
        {
            HashSet<ushort> transparentBlocks = [];

            foreach (BlockData data in blockData)
            {
                if (data.Transparent)
                {
                    transparentBlocks.Add(blockNameToId[data.Type]);
                }
            }

            return transparentBlocks;
        }

        static (HashSet<ushort>, Dictionary<ushort, byte>) GetLightSources(List<BlockData> blockData, Dictionary<string, ushort> blockNameToId)
        {
            HashSet<ushort> lightSources = [];
            Dictionary<ushort, byte> lightValues = [];

            foreach (BlockData data in blockData)
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
}
