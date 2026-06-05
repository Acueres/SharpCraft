using SharpCraft.AssetProcessing;
using SharpCraft.Graphics;
using SharpCraft.Graphics.Resources;
using SharpCraft.Platform;
using SharpCraft.Time;
using SharpCraft.SharpMath;

using System.Numerics;
using SDL;
using SharpCraft.Rendering.Text;
using static SDL.SDL3;

namespace SharpCraft.Rendering;

internal unsafe class Renderer : IDisposable
{
    private readonly GpuDevice device;
    private readonly BlockFaceRenderer blockFaceRenderer;
    private readonly SpriteRenderer spriteRenderer;

    private readonly MeshData mesh;
    private readonly TextureArray textureArray;
    private readonly Texture crosshairTexture;
    
    private readonly TextTextureCache textTextureCache;
    private readonly Font debugFont;

    private readonly GpuUploader uploader;

    private readonly FrameManager frameManager;
    private readonly DepthBuffer depthBuffer;

    public Renderer(uint width, uint height, Window window, GpuDevice device, AssetServer assetServer)
    {
        this.device = device;

        var cubeShader = assetServer.GetShader("cube");
        var spriteShader = assetServer.GetShader("sprite");
        textureArray = assetServer.TextureArray;
        crosshairTexture = assetServer.CrosshairTexture;
        debugFont = assetServer.GetDebugFont(16);
        
        uploader = new GpuUploader(this.device);
        
        textTextureCache = new TextTextureCache(device, uploader);
        
        mesh = new MeshData(64);

        frameManager = new FrameManager(this.device, window);
        depthBuffer = new DepthBuffer(this.device, width, height);

        blockFaceRenderer = new BlockFaceRenderer(device, uploader, textureArray, cubeShader);
        spriteRenderer = new SpriteRenderer(device, uploader, spriteShader);
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
        uploader.Upload(crosshairTexture);

        blockFaceRenderer.Upload(mesh.Faces, mesh.TransparentFaces);
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
        DrawScene(frame, camera);
        frameManager.SubmitFrame(frame);
    }

    private void DrawScene(FrameContext frame, Camera camera)
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
        
        spriteRenderer.Begin();
        
        const float size = 32f;

        Rect crosshairRect = new(
            frame.Width * 0.5f - size * 0.5f,
            frame.Height * 0.5f - size * 0.5f,
            size,
            size
        );
        
        spriteRenderer.Draw(crosshairTexture, crosshairRect, SamplerType.NearestClamp);
        
        Texture fpsTexture = textTextureCache.GetOrCreate(
            debugFont,
            "Debug menu"
        );
        
        spriteRenderer.Draw(
            fpsTexture,
            new Rect(12f, 12f, fpsTexture.Width, fpsTexture.Height),
            SamplerType.LinearClamp
        );
        
        spriteRenderer.Upload();
        
        spriteRenderer.Render(
            frame.CommandBuffer,
            renderPass,
            frame.Width,
            frame.Height
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
    
    private bool disposed;

    public void Dispose()
    {
        if (disposed) return;

        device.WaitIdle();
        
        textTextureCache.Dispose();
        blockFaceRenderer.Dispose();
        spriteRenderer.Dispose();
        depthBuffer.Dispose();

        disposed = true;
    }
}
