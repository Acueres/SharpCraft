using SDL;

using SharpCraft.Platform;

using System.Text;

using static SDL.SDL3;

namespace SharpCraft.Graphics.Resources;

internal unsafe class Shader : IDisposable
{
    public SDL_GPUShader* Handle => shader;

    private readonly GpuDevice device;
    private readonly SDL_GPUShader* shader;

    public Shader(
        GpuDevice device,
        byte[] code,
        SDL_GPUShaderStage stage,
        uint uniformBuffers,
        string entrypoint)
    {
        this.device = device;

        byte[] entryBytes = Utf8Bytes(entrypoint);

        fixed (byte* codePtr = code)
        fixed (byte* entryPtr = entryBytes)
        {
            SDL_GPUShaderCreateInfo info = new()
            {
                code = codePtr,
                code_size = (nuint)code.Length,
                entrypoint = entryPtr,
                format = SDL_GPUShaderFormat.SDL_GPU_SHADERFORMAT_SPIRV,
                stage = stage,

                num_samplers = 0,
                num_storage_textures = 0,
                num_storage_buffers = 0,
                num_uniform_buffers = uniformBuffers
            };

            shader = SDL_CreateGPUShader(device.Handle, &info);
            if (shader == null)
            {
                SdlRuntime.Throw($"Failed to create shader: {entrypoint}");
            }
        }
    }

    private bool disposed;
    public void Dispose()
    {
        if (disposed) return;

        SDL_ReleaseGPUShader(device.Handle, shader);

        disposed = true;
    }

    private static byte[] Utf8Bytes(string value)
    {
        return Encoding.UTF8.GetBytes(value + '\0');
    }
}
