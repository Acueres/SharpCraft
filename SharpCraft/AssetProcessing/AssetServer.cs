using SharpCraft.Graphics;
using SharpCraft.Graphics.Resources;
using SharpCraft.Rendering.Text;

using SDL;
using StbiSharp;

namespace SharpCraft.AssetProcessing;

internal class AssetServer : IDisposable
{
    public Texture CrosshairTexture => crosshairTexture;

    private const int TextureSize = 64;

    private readonly GpuDevice device;
    private readonly TextureArray textureArray;
    private readonly List<Texture> blockTextures = [];
    private readonly Dictionary<string, uint> textureLayers = [];
    private readonly Dictionary<string, GraphicsShader> shaders = [];

    private readonly Texture crosshairTexture;

    private readonly FontLibrary fonts = new();

    public AssetServer(GpuDevice device)
    {
        this.device = device;

        LoadBlocks();
        textureArray = CreateTextureArray(device, blockTextures);

        crosshairTexture = CreateCrosshairTexture(device);

        LoadShaders();
    }

    public Texture GetBlockTexture(ushort index) => blockTextures[index];
    public uint GetTextureLayer(string name) => textureLayers[name];
    public TextureArray TextureArray => textureArray;

    public GraphicsShader GetShader(string name)
    {
        if (shaders.TryGetValue(name, out var shader)) return shader;

        throw new Exception($"Shader {name} not loaded.");
    }

    public Font GetDebugFont(float size)
    {
        string path = GetAssetPath(
            "Fonts",
            "JetBrainsMono",
            "JetBrainsMono-Regular.ttf"
        );

        return fonts.Get(path, size);
    }

    private void LoadBlocks()
    {
        string blocksPath = GetAssetPath("Textures", "Blocks");
        string[] texturePaths = Directory.GetFiles(blocksPath)
            .Where(f => f.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
                        || f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)
                        || f.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        var emptyTexture = new Texture(device, TextureSize, TextureSize, Colors.Transparent);
        blockTextures.Add(emptyTexture);

        for (uint i = 0; i < texturePaths.Length; i++)
        {
            string texturePath = texturePaths[i];

            Texture blockTexture = LoadTexture(texturePath);
            blockTextures.Add(blockTexture);

            string textureName = Path.GetFileNameWithoutExtension(texturePath);
            textureLayers[textureName] = i;
        }
    }

private static TextureArray CreateTextureArray(GpuDevice device, IReadOnlyList<Texture> textures)
    {
        const int bytesPerPixel = 4;

        int textureCount = textures.Count;
        int layerSize = TextureSize * TextureSize * bytesPerPixel;

        byte[,] data = new byte[textureCount, layerSize];

        for (int layer = 0; layer < textureCount; layer++)
        {
            Texture texture = textures[layer];

            if (texture.Width != TextureSize || texture.Height != TextureSize)
            {
                throw new InvalidOperationException(
                    $"Block texture at index {layer} has size {texture.Width}x{texture.Height}, expected {TextureSize}x{TextureSize}."
                );
            }

            Buffer.BlockCopy(
                texture.Data,
                0,
                data,
                layer * layerSize,
                layerSize
            );
        }

        return new TextureArray(
            device,
            TextureSize,
            TextureSize,
            data
        );
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
        var vertexShader = LoadShaderPart(GetShaderPath($"{name}.vert.spv"), ShaderType.Vertex);
        var fragmentShader = LoadShaderPart(GetShaderPath($"{name}.frag.spv"), ShaderType.Fragment);
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

    private static Texture CreateCrosshairTexture(GpuDevice device)
    {
        const int crosshairTextureSize = 32;
        const int crosshairThickness = 2;

        byte[] data = CreateCrosshairTextureData(
            crosshairTextureSize,
            crosshairThickness
        );

        var crosshairTexture = new Texture(
            device,
            crosshairTextureSize,
            crosshairTextureSize,
            data
        );
        
        return crosshairTexture;
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
    
    public static string GetAssetPath(params string[] segments)
    {
        return Path.Combine([AppContext.BaseDirectory, "Assets", ..segments]);
    }
    
    private static string GetShaderPath(params string[] segments)
    {
        return Path.Combine([AppContext.BaseDirectory, "Shaders", ..segments]);
    }

    bool disposed;
    public void Dispose()
    {
        if (disposed) return;

        foreach (var shader in shaders.Values)
        {
            shader.Dispose();
        }
        shaders.Clear();

        foreach (var texture in blockTextures)
        {
            texture.Dispose();
        }
        blockTextures.Clear();
        
        crosshairTexture.Dispose();
        textureArray.Dispose();
        
        fonts.Dispose();

        disposed = true;
    }
}
