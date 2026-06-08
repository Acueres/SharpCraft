using SharpCraft.AssetProcessing;
using SharpCraft.Graphics;
using SharpCraft.Graphics.Resources;
using SharpCraft.Platform;
using SharpCraft.Time;
using SharpCraft.SharpMath;
using SharpCraft.Rendering.Text;

using System.Numerics;
using SDL;
using static SDL.SDL3;

namespace SharpCraft.Rendering;

internal unsafe class Renderer : IDisposable
{
    private readonly GpuDevice device;
    private readonly BlockFaceRenderer blockFaceRenderer;
    private readonly SpriteRenderer spriteRenderer;

    private readonly AssetServer assetServer;
    private readonly MeshData mesh;
    private readonly TextureArray textureArray;
    private readonly Texture crosshairTexture;
    
    private readonly TextTextureCache textTextureCache;
    private Font debugFont;

    private readonly GpuUploader uploader;

    private readonly FrameManager frameManager;
    private readonly DepthBuffer depthBuffer;

    private float uiScale;
    private float fontSize;
    private float padding;
    private float crosshairSize;
    private float scaledUiWidth;
    private float scaledUiHeight;

    private uint currentWidth;
    private uint currentHeight;

    private readonly uint defaultWidth;
    private readonly uint defaultHeight;
    private const uint defaultFontSize = 16;
    private const uint defaultPadding = 8;
    private const uint defaultCrosshairSize = 32;

    public Renderer(uint width, uint height, Window window, GpuDevice device, AssetServer assetServer)
    {
        this.device = device;
        this.assetServer = assetServer;
        defaultWidth = width;
        defaultHeight = height;

        var cubeShader = assetServer.GetShader("cube");
        var spriteShader = assetServer.GetShader("sprite");
        textureArray = assetServer.TextureArray;
        crosshairTexture = assetServer.CrosshairTexture;
        debugFont = assetServer.GetDebugFont(defaultFontSize);
        
        uploader = new GpuUploader(this.device);
        
        textTextureCache = new TextTextureCache(device, uploader);
        
        mesh = new MeshData(64);

        frameManager = new FrameManager(this.device, window);
        depthBuffer = new DepthBuffer(this.device, width, height);

        blockFaceRenderer = new BlockFaceRenderer(device, uploader, textureArray, cubeShader);
        spriteRenderer = new SpriteRenderer(device, uploader, spriteShader);

        RescaleUi(width, height);
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
        RescaleUi(frame.Width, frame.Height);
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

        Rect crosshairRect = new(
            frame.Width * 0.5f - crosshairSize * 0.5f,
            frame.Height * 0.5f - crosshairSize * 0.5f,
            crosshairSize,
            crosshairSize
        );
        
        spriteRenderer.Draw(crosshairTexture, crosshairRect);
        
        Texture fpsTexture = textTextureCache.GetOrCreate(
            debugFont,
            "Debug menu"
        );
        
        spriteRenderer.DrawText(
            fpsTexture,
            new Rect(padding, padding, fpsTexture.Width, fpsTexture.Height),
            Colors.LimeGreen.ToVector4()
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
        scaledUiHeight = defaultHeight * uiScale;
        scaledUiWidth = defaultWidth * uiScale;

        fontSize = MathF.Round(defaultFontSize * uiScale);
        debugFont = assetServer.GetDebugFont(fontSize);

        padding = MathF.Round(defaultPadding * uiScale);
        crosshairSize = MathF.Round(defaultCrosshairSize * uiScale);

        currentWidth = newWidth;
        currentHeight = newHeight;
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
