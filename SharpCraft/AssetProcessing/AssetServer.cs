using SharpCraft.Graphics;
using SharpCraft.Graphics.Resources;

using SDL;
using StbiSharp;

namespace SharpCraft.AssetProcessing;

internal class AssetServer(GpuDevice device) : IDisposable
{
    private const int TextureSize = 64;
    
    private TextureArray textureArray;
    private readonly List<Texture> blockTextures = [];
    private readonly Dictionary<string, GraphicsShader> shaders = [];

    public void Load()
    {
        LoadBlocks();
        CreateTextureArray();
        LoadShaders();
    }

    public Texture GetBlockTexture(ushort index) => blockTextures[index];
    public TextureArray TextureArray => textureArray;

    public GraphicsShader GetShader(string name)
    {
        if (shaders.TryGetValue(name, out var shader)) return shader;

        throw new Exception($"Shader {name} not loaded.");
    }

    private void LoadBlocks()
    {
        string blocksPath = Path.Combine(AppContext.BaseDirectory, "Assets", "Textures", "Blocks");
        string[] texturePaths = Directory.GetFiles(blocksPath)
            .Where(f => f.EndsWith(".png", StringComparison.OrdinalIgnoreCase) 
                        || f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) 
                        || f.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase))
            .Order()
            .ToArray();
        
        foreach (string texturePath in texturePaths)
        {
            Texture blockTexture = LoadTexture(texturePath);
            blockTextures.Add(blockTexture);
        }
    }

    private void CreateTextureArray()
    {
        const int bytesPerPixel = 4;
        
        byte[] bytes = new byte[TextureSize * TextureSize * bytesPerPixel * blockTextures.Count];
        int index = 0;
        foreach (var texture in blockTextures)
        {
            for (int i = 0; i < texture.Data.Length; i++)
            {
                bytes[index++] = texture.Data[i];
            }
        }
        
        byte[,] textureArrayData = new byte[blockTextures.Count, TextureSize * TextureSize * bytesPerPixel];
        
        Buffer.BlockCopy(bytes, 0, textureArrayData, 0, bytes.Length * sizeof(byte));
        
        textureArray = new TextureArray(device, TextureSize, TextureSize, textureArrayData);
    }

    private Texture LoadTexture(string path)
    {
        byte[] bytes = File.ReadAllBytes(path);
        StbiImage image = Stbi.LoadFromMemory(bytes, 4);

        Texture texture = new(
            device,
            (uint)image.Width,
            (uint)image.Height,
            image.Data.ToArray()
        );

        return texture;
    }

    private void LoadShaders()
    {
        var vertexShader = LoadShader(Path.Combine("Shaders", "cube.vert.spv"), ShaderType.Vertex);
        var fragmentShader = LoadShader(Path.Combine("Shaders", "cube.frag.spv"), ShaderType.Fragment);

        var cubeShader = new GraphicsShader(vertexShader, fragmentShader);
        shaders.Add("cube", cubeShader);
    }

    private Shader LoadShader(string path, ShaderType shaderType)
    {
        byte[] code = File.ReadAllBytes(path);
        Shader shader;

        if (shaderType == ShaderType.Vertex)
        {
            shader = new(device, code, SDL_GPUShaderStage.SDL_GPU_SHADERSTAGE_VERTEX, uniformBuffers: 1, samplers: 0, "MainVS");
        }
        else
        {
            shader = new(device, code, SDL_GPUShaderStage.SDL_GPU_SHADERSTAGE_FRAGMENT, uniformBuffers: 0, samplers: 1, "MainFS");
        }

        return shader;
    }

    bool disposed;
    public void Dispose()
    {
        if (disposed) return;

        foreach (var shader in shaders.Values)
        {
            shader.Dispose();
        }

        foreach (var texture in blockTextures)
        {
            texture.Dispose();
        }
        
        textureArray.Dispose();

        disposed = true;
    }
}
