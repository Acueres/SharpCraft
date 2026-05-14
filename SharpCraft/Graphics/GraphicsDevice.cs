using System.Numerics;
using SDL;

using SharpCraft.Assets;
using SharpCraft.Graphics.Resources;
using SharpCraft.Platform;
using SharpCraft.Rendering;

using static SDL.SDL3;

namespace SharpCraft.Graphics;

internal unsafe class GraphicsDevice : IDisposable
{
    public Window Window => window;

    private readonly GpuDevice device;
    private readonly Window window;

    private readonly VertexBuffer vertexBuffer;
    private readonly IndexBuffer indexBuffer;

    private readonly GpuUploader uploader;
    private readonly GraphicsPipeline pipeline;
    private readonly FrameManager frameManager;
    private readonly DepthBuffer depthBuffer;

    public GraphicsDevice(uint width, uint height, Window window, GpuDevice device, AssetServer assetServer)
    {
        this.window = window;
        this.device = device;

        var shader = assetServer.GetShader("cube");

        uploader = new GpuUploader(this.device);
        pipeline = new GraphicsPipeline(this.device, shader.Vertex, shader.Fragment);

        vertexBuffer = new VertexBuffer(this.device);
        indexBuffer = new IndexBuffer(this.device);
        frameManager = new FrameManager(this.device, window);
        depthBuffer = new DepthBuffer(this.device, width, height);
    }

    private bool disposed;
    public void Dispose()
    {
        if (disposed) return;

        device.WaitIdle();

        pipeline.Dispose();

        vertexBuffer.Dispose();
        indexBuffer.Dispose();

        depthBuffer.Dispose();

        disposed = true;
    }

    public void UploadMesh()
    {
        uploader.Upload(vertexBuffer, indexBuffer);
    }

    public bool TryBeginFrame(out FrameContext frame)
    {
        if (!frameManager.TryBeginFrame(out frame))
        {
            return false;
        }

        depthBuffer.EnsureSize(frame.Width, frame.Height);

        return true;
    }

    public void Draw(FrameContext frame, Camera camera)
    {
        DrawCube(frame, camera);
        frameManager.SubmitFrame(frame);
    }

    private void DrawCube(FrameContext frame, Camera camera)
    {
        Matrix4x4 mvp = BuildMvp(camera);

        SDL_PushGPUVertexUniformData(
            frame.CommandBuffer,
            0,
            (nint)(&mvp),
            (uint)sizeof(Matrix4x4)
        );

        SDL_GPUColorTargetInfo colorTarget = new()
        {
            texture = frame.SwapchainTexture,

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
            frame.CommandBuffer,
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
    }

    private static Matrix4x4 BuildMvp(Camera camera)
    {
        Matrix4x4 world = Matrix4x4.Identity;

        Matrix4x4 projection = camera.Projection;

        Matrix4x4 mvp = world * camera.View * projection;

        return mvp;
    }
}
