using SharpCraft.AssetProcessing;
using SharpCraft.Graphics;
using SharpCraft.Diagnostics;
using SharpCraft.Graphics.Resources;
using SharpCraft.Platform;
using SharpCraft.SharpMath;
using SharpCraft.Rendering.Text;
using SharpCraft.World.Meshing;
using SharpCraft.World.WorldStreaming;
using SharpCraft.View;
using SharpCraft.Input;

using System.Numerics;
using System.Diagnostics;
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
    private readonly DetailedReport detailedReport;
    private readonly DebugPanelState debugState = new();
    private readonly GpuTextEngine textEngine;

    private readonly GpuUploader uploader;
    private readonly FrameProfiler profiler;

    private readonly FrameManager frameManager;
    private readonly DepthBuffer depthBuffer;

    private readonly Vector4 clearColor = Colors.CornflowerBlue.ToVector4();

    private float uiScale;
    private float fontSize;
    private float padding;
    private float crosshairSize;

    private uint currentWidth;
    private uint currentHeight;

    private readonly uint defaultWidth;
    private readonly uint defaultHeight;
    private const uint DefaultFontSize = 16;
    private const uint DefaultPadding = 8;
    private const uint DefaultCrosshairSize = 32;

    public Renderer(uint width, uint height, Window window, GpuDevice device,
        AssetServer assetServer, ChunkMesher chunkMesher, FrameProfiler profiler, BenchmarkOptions options, BenchmarkRun benchmark)
    {
        this.device = device;
        this.assetServer = assetServer;
        this.profiler = profiler;
        defaultWidth = width;
        defaultHeight = height;

        var cubeShader = assetServer.GetShader("cube");
        var spriteShader = assetServer.GetShader("sprite");
        textureArray = assetServer.TextureArray;
        crosshairTexture = assetServer.CrosshairTexture;
        var debugFont = assetServer.GetDebugFont(DefaultFontSize);
        
        uploader = new GpuUploader(this.device, profiler);
        
        textEngine = new GpuTextEngine(device);

        debugOverlay = new DebugOverlay(textEngine, debugFont, device);
        detailedReport = new DetailedReport(textEngine, debugFont, device, uploader, profiler, debugState, options, benchmark);
        if (options.Scene.HasValue) debugState.OpenBenchmarks();

        frameManager = new FrameManager(this.device, window, profiler);
        depthBuffer = new DepthBuffer(this.device, width, height);

        voxelFaceRenderer = new VoxelFaceRenderer(device, uploader, textureArray, cubeShader, chunkMesher, profiler);
        spriteRenderer = new SpriteRenderer(device, uploader, spriteShader);

        RescaleUi(width, height);
    }
    
    public void UpdateWorld(Camera camera, ChunkVolume volume)
    {
        voxelFaceRenderer.Update(volume, camera);
    }


    public void UpdateUi(Camera camera)
    {
        if (debugState.Detailed) detailedReport.Update(camera.Position);
        else debugOverlay.Update(profiler.Snapshot, camera.Position);
    }

    public void HandleDebugInput(Keyboard keyboard)
    {
        debugState.Handle(keyboard);
        if (debugState.ToggleCompact) debugOverlay.Toggle();
    }

    public void LoadGpuResources()
    {
        uploader.Upload(textureArray);
        uploader.Upload(crosshairTexture);
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
        long started = Stopwatch.GetTimestamp();
        bool acquired = frameManager.TryBeginFrame(out frame);
        profiler.Timings.AcquireMilliseconds += Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        if (!acquired)
        {
            profiler.Rendering.SkippedFrames++;
            return false;
        }

        depthBuffer.EnsureSize(frame.Width, frame.Height);

        return true;
    }

    private void Draw(in FrameContext frame, Camera camera)
    {
        DrawScene(frame, camera);
        long started = Stopwatch.GetTimestamp();
        frameManager.SubmitFrame(frame);
        profiler.Timings.SubmitMilliseconds += Stopwatch.GetElapsedTime(started).TotalMilliseconds;
    }

    private void DrawScene(in FrameContext frame, Camera camera)
    {
        long uiStarted = Stopwatch.GetTimestamp();
        PrepareUi(frame);
        profiler.Timings.UiMilliseconds += Stopwatch.GetElapsedTime(uiStarted).TotalMilliseconds;
        long recordingStarted = Stopwatch.GetTimestamp();
        
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

        GpuDevice.BeginGpuPass(frame.CommandBuffer, GpuPass.Ui);
        spriteRenderer.Render(
            frame.CommandBuffer,
            renderPass,
            frame.Width,
            frame.Height
        );
        GpuDevice.EndGpuPass(frame.CommandBuffer);
        profiler.Rendering.UiDrawCalls = spriteRenderer.DrawCallCount;

        SDL_EndGPURenderPass(renderPass);
        profiler.Timings.CommandRecordingMilliseconds += Stopwatch.GetElapsedTime(recordingStarted).TotalMilliseconds;
    }

    private void PrepareUi(in FrameContext frame)
    {
        if (debugState.Detailed) detailedReport.Prepare(frame.Width, frame.Height, padding);
        else debugOverlay.Prepare();
        spriteRenderer.Begin();

        Rect crosshairRect = new(
            frame.Width * 0.5f - crosshairSize * 0.5f,
            frame.Height * 0.5f - crosshairSize * 0.5f,
            crosshairSize,
            crosshairSize
        );

        spriteRenderer.Draw(crosshairTexture, crosshairRect);

        if (debugState.Detailed) detailedReport.Draw(spriteRenderer);
        else debugOverlay.Draw(spriteRenderer, frame.Width, frame.Height, padding);

        spriteRenderer.Upload();
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
        detailedReport.Rescale(font);

        padding = MathF.Round(DefaultPadding * uiScale);
        crosshairSize = MathF.Round(DefaultCrosshairSize * uiScale);

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
        
        detailedReport.Dispose();
        debugOverlay.Dispose();
        textEngine.Dispose();
        voxelFaceRenderer.Dispose();
        spriteRenderer.Dispose();
        depthBuffer.Dispose();

        disposed = true;
    }
}
