using SharpCraft.AssetProcessing;
using SharpCraft.Extensions;
using SharpCraft.World.Blocks.Serialization;

using System.Text.Json;

namespace SharpCraft.World.Blocks;

internal sealed class BlockRegistry
{
    private readonly string[] names;
    private readonly uint[] faceTextureLayers;
    private readonly bool[] transparent;
    private readonly byte[] lightLevel;

    private readonly Dictionary<string, uint> idToNumeric;

    public int BlockCount => names.Length;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public BlockRegistry(AssetServer assets)
    {
        List<BlockDto> defs = LoadJson<List<BlockDto>>("blocks.json");

        int count = defs.Count + 1;
        names = new string[count];
        faceTextureLayers = new uint[count * 6];
        transparent = new bool[count];
        lightLevel = new byte[count];
        idToNumeric = new Dictionary<string, uint>(count) { ["empty"] = 0 };
        
        names[0] = "Empty";
        transparent[0] = true;

        for (int i = 0; i < defs.Count; i++)
        {
            uint numericId = (uint)(i + 1);
            BlockDto def = defs[i];

            if (!idToNumeric.TryAdd(def.Id, numericId))
            {
                throw new InvalidDataException($"Duplicate block id '{def.Id}'.");
            }

            names[numericId] = def.Name ?? def.Id.CapitalizeFirst();
            AddFaces(def, assets, numericId, faceTextureLayers);
            
            transparent[numericId] = def.Transparent;
            lightLevel[numericId] = (byte)def.LightLevel;
        }
    }

    public uint GetNumericId(string id) => idToNumeric[id];
    public string GetBlockName(Block block) => names[block.Value];
    public bool IsTransparent(Block block) => transparent[block.Value];
    public bool IsLightSource(Block block) => lightLevel[block.Value] > 0;
    public byte GetLightLevel(Block block) => lightLevel[block.Value];
    public uint GetFaceTextureLayer(Block block, FaceDirection face)
        => faceTextureLayers[block.Value * 6 + (uint)face];

    private static void AddFaces(BlockDto def, AssetServer assets, uint numericId, uint[] faceTextureLayers)
    {
        // Priority per face: explicit face name -> side (horizontals) -> texture/all -> error
        string? all = def.Texture;
        var t = def.Textures;

        string Face(string? specific, bool horizontal)
        {
            string? name = specific
                           ?? (horizontal ? t?.Side : null)
                           ?? t?.All
                           ?? all;
            if (name is null)
            {
                throw new InvalidDataException($"Block '{def.Id}' has no texture for a face.");
            }
            return name;
        }

        var faces = new uint[6];
        faces[(int)FaceDirection.ZPos] = assets.GetTextureLayer(Face(t?.Front, true));
        faces[(int)FaceDirection.ZNeg] = assets.GetTextureLayer(Face(t?.Back,  true));
        faces[(int)FaceDirection.XPos] = assets.GetTextureLayer(Face(t?.Right, true));
        faces[(int)FaceDirection.XNeg] = assets.GetTextureLayer(Face(t?.Left,  true));
        faces[(int)FaceDirection.YPos] = assets.GetTextureLayer(Face(t?.Top,   false));
        faces[(int)FaceDirection.YNeg] = assets.GetTextureLayer(Face(t?.Bottom,false));
        
        Array.Copy(faces, 0, faceTextureLayers, (int)numericId * 6, 6);
    }

    private static T LoadJson<T>(string fileName)
    {
        string path = AssetServer.GetAssetPath(fileName);
        string json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<T>(json, JsonOptions)
               ?? throw new InvalidDataException($"Failed to deserialize {fileName}.");
    }
}