using SharpCraft.Graphics;
using SharpCraft.Graphics.Resources;

using SDL;
using StbiSharp;

namespace SharpCraft.AssetProcessing;

internal class AssetServer(GpuDevice device) : IDisposable
{
    public Texture CrosshairTexture => crosshairTexture;

    private const int TextureSize = 64;
    
    private TextureArray textureArray;
    private readonly List<Texture> blockTextures = [];
    private readonly Dictionary<string, GraphicsShader> shaders = [];

    private Texture crosshairTexture;

    public void Load()
    {
        LoadBlocks();
        CreateTextureArray();

        CreateCrosshairTexture();

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
        
        var emptyTexture = new Texture(device, TextureSize, TextureSize, Colors.Transparent);
        blockTextures.Add(emptyTexture);
        
        foreach (string texturePath in texturePaths)
        {
            Texture blockTexture = LoadTexture(texturePath);
            blockTextures.Add(blockTexture);
        }
    }

    private void CreateTextureArray()
    {
        const int bytesPerPixel = 4;
        int textureCount = blockTextures.Count;
        
        byte[] bytes = new byte[TextureSize * TextureSize * bytesPerPixel * textureCount];
        int index = 0;
        
        foreach (var texture in blockTextures)
        {
            for (int i = 0; i < texture.Data.Length; i++)
            {
                bytes[index++] = texture.Data[i];
            }
        }
        
        byte[,] textureArrayData = new byte[textureCount, TextureSize * TextureSize * bytesPerPixel];
        
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
        LoadShader("cube");
        LoadShader("sprite");
    }

    private void LoadShader(string name)
    {
        var vertexShader = LoadShaderPart(Path.Combine("Shaders", $"{name}.vert.spv"), ShaderType.Vertex);
        var fragmentShader = LoadShaderPart(Path.Combine("Shaders", $"{name}.frag.spv"), ShaderType.Fragment);
        var shader = new GraphicsShader(vertexShader, fragmentShader);

        shaders.Add(name, shader);
    }

    private Shader LoadShaderPart(string path, ShaderType shaderType)
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

    private void CreateCrosshairTexture()
    {
        const int CrosshairTextureSize = 32;
        const int CrosshairThickness = 2;

        byte[] data = CreateCrosshairTextureData(
            CrosshairTextureSize,
            CrosshairThickness
        );

        crosshairTexture = new Texture(
            device,
            CrosshairTextureSize,
            CrosshairTextureSize,
            data
        );
    }

    private static byte[] CreateCrosshairTextureData(int size, int thickness)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(size);

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(thickness);

        byte[] data = new byte[size * size * 4];

        int center = size / 2;
        int halfThickness = thickness / 2;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                bool vertical =
                    Math.Abs(x - center) <= halfThickness;

                bool horizontal =
                    Math.Abs(y - center) <= halfThickness;

                bool isCrosshair = vertical || horizontal;

                int index = (y * size + x) * 4;

                if (isCrosshair)
                {
                    data[index + 0] = 255; // R
                    data[index + 1] = 255; // G
                    data[index + 2] = 255; // B
                    data[index + 3] = 255; // A
                }
                else
                {
                    data[index + 0] = 0;
                    data[index + 1] = 0;
                    data[index + 2] = 0;
                    data[index + 3] = 0;
                }
            }
        }

        return data;
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
        
        crosshairTexture.Dispose();
        textureArray.Dispose();

        disposed = true;
    }
}
