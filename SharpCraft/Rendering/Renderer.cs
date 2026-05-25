using SharpCraft.AssetProcessing;
using SharpCraft.Graphics;
using SharpCraft.Graphics.Resources;
using SharpCraft.Platform;
using SharpCraft.Time;

using System.Numerics;
using SDL;

using static SDL.SDL3;

namespace SharpCraft.Rendering;

internal unsafe class Renderer : IDisposable
{
    private readonly GpuDevice device;
    private readonly BlockFaceRenderer blockFaceRenderer;

    private readonly MeshData mesh;
    private readonly TextureArray textureArray;

    private readonly GpuUploader uploader;

    private readonly FrameManager frameManager;
    private readonly DepthBuffer depthBuffer;

    public Renderer(uint width, uint height, Window window, GpuDevice device, AssetServer assetServer)
    {
        this.device = device;

        var shader = assetServer.GetShader("cube");
        textureArray = assetServer.TextureArray;
        
        uploader = new GpuUploader(this.device);
        
        mesh = new MeshData(64);

        frameManager = new FrameManager(this.device, window);
        depthBuffer = new DepthBuffer(this.device, width, height);

        blockFaceRenderer = new BlockFaceRenderer(device, uploader, textureArray, shader);
    }
    

    public void Update(FrameTime time, Camera camera)
    {
        if (mesh.Update(time, camera))
        {
            blockFaceRenderer.Upload(mesh.Faces, mesh.TransparentFaces);
        }
    }

    public void LoadGpuResources()
    {
        uploader.Upload(textureArray);
    }
    
    public void Render(Camera camera)
    {
        if (!TryBeginFrame(out var frame))
        {
            return;
        }

        camera.SetViewport(frame.Width, frame.Height);
        Draw(frame, camera);
    }

    private bool TryBeginFrame(out FrameContext frame)
    {
        if (!frameManager.TryBeginFrame(out frame))
        {
            return false;
        }

        depthBuffer.EnsureSize(frame.Width, frame.Height);

        return true;
    }

    private void Draw(FrameContext frame, Camera camera)
    {
        DrawBlockFaces(frame, camera);
        frameManager.SubmitFrame(frame);
    }

    private void DrawBlockFaces(FrameContext frame, Camera camera)
    {
        Matrix4x4 mvp = BuildMvp(camera);

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
        
        blockFaceRenderer.Draw(frame.CommandBuffer, renderPass, mvp);
        
        SDL_EndGPURenderPass(renderPass);
    }

    private static Matrix4x4 BuildMvp(Camera camera)
    {
        Matrix4x4 world = Matrix4x4.Identity;

        Matrix4x4 projection = camera.Projection;

        Matrix4x4 mvp = world * camera.View * projection;

        return mvp;
    }
    
    private bool disposed;

    public void Dispose()
    {
        if (disposed) return;

        device.WaitIdle();
        
        blockFaceRenderer.Dispose();
        depthBuffer.Dispose();

        disposed = true;
    }
}
