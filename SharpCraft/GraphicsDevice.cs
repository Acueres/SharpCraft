using System.Numerics;
using SDL;

using static SDL.SDL3;

namespace SharpCraft;

internal unsafe class GraphicsDevice : IDisposable
{
    private readonly Shader vertexShader;
    private readonly Shader fragmentShader;

    private readonly GpuDevice device;
    private readonly Window window;

    private readonly VertexBuffer vertexBuffer;
    private readonly IndexBuffer indexBuffer;

    private readonly SdlRuntime runtime;
    private readonly GpuUploader uploader;
    private readonly GraphicsPipeline pipeline;
    private readonly DepthBuffer depthBuffer;

    public GraphicsDevice(uint width, uint height, string title)
    {
        runtime = new SdlRuntime();

        window = new Window(title, (int)width, (int)height);
        device = new GpuDevice("vulkan", window, debugInfo: true);

        vertexShader = new Shader(device, Path.Combine("Shaders", "cube.vert.spv"),
            SDL_GPUShaderStage.SDL_GPU_SHADERSTAGE_VERTEX, uniformBuffers: 1, "MainVS");
        fragmentShader = new Shader(device, Path.Combine("Shaders", "cube.frag.spv"),
            SDL_GPUShaderStage.SDL_GPU_SHADERSTAGE_FRAGMENT, uniformBuffers: 0, "MainFS");

        uploader = new GpuUploader(device);
        pipeline = new GraphicsPipeline(device, vertexShader, fragmentShader);

        vertexBuffer = new VertexBuffer(device);
        indexBuffer = new IndexBuffer(device);

        depthBuffer = new DepthBuffer(device, width, height);
    }

    public void Dispose()
    {
        device.WaitIdle();

        pipeline.Dispose();

        vertexShader.Dispose();
        fragmentShader.Dispose();

        vertexBuffer.Dispose();
        indexBuffer.Dispose();

        depthBuffer.Dispose();

        device.Dispose();
        window.Dispose();
        runtime.Dispose();
    }

    public void UploadMesh()
    {
        uploader.Upload(vertexBuffer, indexBuffer);
    }

    public void DrawFrame()
    {
        if (!TryCreateFrameContext(out var fCtx))
        {
            return;
        }

        depthBuffer.EnsureSize(fCtx.Width, fCtx.Height);

        Matrix4x4 mvp = BuildMvp(fCtx.Width, fCtx.Height);

        SDL_PushGPUVertexUniformData(
            fCtx.CommandBuffer,
            0,
            (nint)(&mvp),
            (uint)sizeof(Matrix4x4)
        );

        SDL_GPUColorTargetInfo colorTarget = new()
        {
            texture = fCtx.SwapchainTexture,

            clear_color = new SDL_FColor
            {
                r = 0.08f,
                g = 0.12f,
                b = 0.22f,
                a = 1.0f
            },

            load_op = SDL_GPULoadOp.SDL_GPU_LOADOP_CLEAR,
            store_op = SDL_GPUStoreOp.SDL_GPU_STOREOP_STORE,
            cycle = false
        };

        SDL_GPUDepthStencilTargetInfo depthTarget = new()
        {
            texture = depthBuffer.Handle,

            clear_depth = 1.0f,
            load_op = SDL_GPULoadOp.SDL_GPU_LOADOP_CLEAR,
            store_op = SDL_GPUStoreOp.SDL_GPU_STOREOP_DONT_CARE,

            stencil_load_op = SDL_GPULoadOp.SDL_GPU_LOADOP_DONT_CARE,
            stencil_store_op = SDL_GPUStoreOp.SDL_GPU_STOREOP_DONT_CARE,

            cycle = false
        };

        SDL_GPURenderPass* renderPass = SDL_BeginGPURenderPass(
            fCtx.CommandBuffer,
            &colorTarget,
            1,
            &depthTarget
        );

        SDL_BindGPUGraphicsPipeline(renderPass, pipeline.Handle);

        SDL_GPUBufferBinding vertexBinding = new()
        {
            buffer = vertexBuffer.Handle,
            offset = 0
        };

        SDL_BindGPUVertexBuffers(renderPass, 0, &vertexBinding, 1);

        SDL_GPUBufferBinding indexBinding = new()
        {
            buffer = indexBuffer.Handle,
            offset = 0
        };

        SDL_BindGPUIndexBuffer(
            renderPass,
            &indexBinding,
            SDL_GPUIndexElementSize.SDL_GPU_INDEXELEMENTSIZE_16BIT
        );

        SDL_DrawGPUIndexedPrimitives(
            renderPass,
            num_indices: (uint)Cube.Indices.Length,
            num_instances: 1,
            first_index: 0,
            vertex_offset: 0,
            first_instance: 0
        );

        SDL_EndGPURenderPass(renderPass);

        if (!SDL_SubmitGPUCommandBuffer(fCtx.CommandBuffer))
        {
            SdlRuntime.Throw("Failed to submit frame command buffer");
        }
    }

    private bool TryCreateFrameContext(out FrameContext context)
    {
        context = default;

        SDL_GPUCommandBuffer* cmd = SDL_AcquireGPUCommandBuffer(device.Handle);
        if (cmd == null)
        {
            SdlRuntime.Throw("Failed to acquire command buffer");
        }

        SDL_GPUTexture* swapchainTexture = null;
        uint swapchainWidth = 0;
        uint swapchainHeight = 0;

        if (!SDL_AcquireGPUSwapchainTexture(
                cmd,
                window.Handle,
                &swapchainTexture,
                &swapchainWidth,
                &swapchainHeight))
        {
            SDL_SubmitGPUCommandBuffer(cmd);
            return false;
        }

        if (swapchainTexture == null)
        {
            SDL_SubmitGPUCommandBuffer(cmd);
            return false;
        }

        context = new FrameContext(cmd, swapchainTexture, swapchainWidth, swapchainHeight);

        return true;
    }

    private static Matrix4x4 BuildMvp(uint width, uint height)
    {
        float aspect = width / MathF.Max(1.0f, height);

        Matrix4x4 world =
            Matrix4x4.CreateRotationY(SDL_GetTicks() / 1000.0f * 0.7f) *
            Matrix4x4.CreateRotationX(-0.45f);

        Matrix4x4 view = Matrix4x4.CreateLookAt(
            new Vector3(0, 1.2f, 4.0f),
            Vector3.Zero,
            Vector3.UnitY
        );

        Matrix4x4 projection = Matrix4x4.CreatePerspectiveFieldOfView(
            MathF.PI / 4.0f,
            aspect,
            0.1f,
            100.0f
        );

        // Vulkan-style framebuffer orientation correction
        projection.M22 *= -1.0f;

        return world * view * projection;
    }
}
