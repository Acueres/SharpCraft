using SDL;

using SharpCraft.Graphics;
using SharpCraft.Graphics.Resources;

namespace SharpCraft.Assets;

internal class AssetServer(GpuDevice device) : IDisposable
{
    private readonly Dictionary<string, GraphicsShader> shaders = [];

    public void Load()
    {
        LoadShaders(device);
    }

    public GraphicsShader GetShader(string name)
    {
        if (shaders.TryGetValue(name, out var shader)) return shader;

        throw new Exception($"Shader {name} not loaded.");
    }

    private void LoadShaders(GpuDevice device)
    {
        var vertexShader = LoadShader(Path.Combine("Shaders", "cube.vert.spv"), "MainVS", SDL_GPUShaderStage.SDL_GPU_SHADERSTAGE_VERTEX, device);
        var fragmentShader = LoadShader(Path.Combine("Shaders", "cube.frag.spv"), "MainFS", SDL_GPUShaderStage.SDL_GPU_SHADERSTAGE_FRAGMENT, device);

        var cubeShader = new GraphicsShader(vertexShader, fragmentShader);
        shaders.Add("cube", cubeShader);
    }

    private static Shader LoadShader(string path, string entryPoint, SDL_GPUShaderStage stage, GpuDevice device)
    {
        byte[] code = File.ReadAllBytes(path);
        Shader shader = new(device, code, stage, uniformBuffers: 1, entryPoint);
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

        disposed = true;
    }
}
