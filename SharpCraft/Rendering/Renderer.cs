using SharpCraft.AssetProcessing;
using SharpCraft.Graphics;
using SharpCraft.Graphics.Resources;
using SharpCraft.Platform;
using SharpCraft.Time;
using SharpCraft.SharpMath;
using SharpCraft.Rendering.Text;
using SharpCraft.World.Meshing;
using SharpCraft.World.WorldStreaming;

using System.Numerics;
using SDL;

using static SDL.SDL3;

namespace SharpCraft.Rendering;

internal unsafe class Renderer : IDisposable
{
    private readonly GpuDevice device;
    private readonly VoxelFaceRenderer voxelFaceRenderer;
    private readonly SpriteRenderer spriteRenderer;

    private readonly AssetServer assetServer;
    private readonly TextureArray textureArray;
    private readonly Texture crosshairTexture;

    private readonly DebugOverlay debugOverlay;
    private readonly TextTextureManager textTextureManager;

    private readonly GpuUploader uploader;

    private readonly FrameManager frameManager;
    private readonly DepthBuffer depthBuffer;

    private readonly Vector4 clearColor = Colors.CornflowerBlue.ToVector4();

    private float uiScale;
    private float fontSize;
    private float padding;
    private float lineHeight;
    private float crosshairSize;

    private uint currentWidth;
    private uint currentHeight;

    private readonly uint defaultWidth;
    private readonly uint defaultHeight;
    private const uint DefaultFontSize = 16;
    private const uint DefaultPadding = 8;
    private const uint DefaultLineHeight = 18;
    private const uint DefaultCrosshairSize = 32;

    public Renderer(uint width, uint height, Window window, GpuDevice device,
        AssetServer assetServer, ChunkMesher chunkMesher)
    {
        this.device = device;
        this.assetServer = assetServer;
        defaultWidth = width;
        defaultHeight = height;

        var cubeShader = assetServer.GetShader("cube");
        var spriteShader = assetServer.GetShader("sprite");
        textureArray = assetServer.TextureArray;
        crosshairTexture = assetServer.CrosshairTexture;
        var debugFont = assetServer.GetDebugFont(DefaultFontSize);
        
        uploader = new GpuUploader(this.device);
        
        textTextureManager = new TextTextureManager(device, uploader);

        debugOverlay = new DebugOverlay(textTextureManager, debugFont);

        frameManager = new FrameManager(this.device, window);
        depthBuffer = new DepthBuffer(this.device, width, height);

        voxelFaceRenderer = new VoxelFaceRenderer(device, uploader, textureArray, cubeShader, chunkMesher);
        spriteRenderer = new SpriteRenderer(device, uploader, spriteShader);

        RescaleUi(width, height);
    }
    
    public void UpdateWorld(Camera camera, ChunkVolume volume)
    {
        voxelFaceRenderer.Update(volume, camera);
    }


    public void UpdateUi(in FrameTime time)
    {
        debugOverlay.Update(time);
    }

    public void LoadGpuResources()
    {
        uploader.Upload(textureArray);
        uploader.Upload(crosshairTexture);
    }
    
    public void Render(in FrameTime time, Camera camera)
    {
        if (!TryBeginFrame(out var frame))
        {
            return;
        }

        camera.SetViewport(frame.Width, frame.Height);
        RescaleUi(frame.Width, frame.Height);
        Draw(frame, time, camera);

        textTextureManager.FlushDynamic();
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

    private void Draw(in FrameContext frame, in FrameTime time, Camera camera)
    {
        DrawScene(frame, time, camera);
        frameManager.SubmitFrame(frame);
    }

    private void DrawScene(in FrameContext frame, in FrameTime time, Camera camera)
    {
        Matrix4x4 mvp = BuildMvp(camera);

        SDL_GPUColorTargetInfo colorTarget = new()
        {
            texture = frame.SwapchainTexture,

            clear_color = new SDL_FColor
            {
                r = clearColor.X,
                g = clearColor.Y,
                b = clearColor.Z,
                a = clearColor.W
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

        voxelFaceRenderer.Draw(frame.CommandBuffer, renderPass, mvp);

        spriteRenderer.Begin();

        Rect crosshairRect = new(
            frame.Width * 0.5f - crosshairSize * 0.5f,
            frame.Height * 0.5f - crosshairSize * 0.5f,
            crosshairSize,
            crosshairSize
        );

        spriteRenderer.Draw(crosshairTexture, crosshairRect);

        debugOverlay.Draw(
            spriteRenderer,
            time,
            device,
            frame.Width,
            frame.Height,
            padding,
            lineHeight
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

    private void RescaleUi(uint newWidth, uint newHeight)
    {
        if (newWidth == 0 || newHeight == 0)
            return;

        if (newWidth == currentWidth && newHeight == currentHeight)
            return;

        float rawScale = Math.Min((float)newWidth / defaultWidth, (float)newHeight / defaultHeight);
        uiScale = Math.Max(0.75f, rawScale);

        fontSize = MathF.Round(DefaultFontSize * uiScale);
        var font = assetServer.GetDebugFont(fontSize);
        debugOverlay.Rescale(font);

        padding = MathF.Round(DefaultPadding * uiScale);
        lineHeight = MathF.Round(DefaultLineHeight * uiScale);
        crosshairSize = MathF.Round(DefaultCrosshairSize * uiScale);

        currentWidth = newWidth;
        currentHeight = newHeight;

        textTextureManager.ClearStatic();
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
        
        debugOverlay.Dispose();
        textTextureManager.Dispose();
        voxelFaceRenderer.Dispose();
        spriteRenderer.Dispose();
        depthBuffer.Dispose();

        disposed = true;
    }
}
