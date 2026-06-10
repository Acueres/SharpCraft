using SharpCraft.Graphics;
using SharpCraft.Graphics.Resources;
using SharpCraft.Rendering.Text;
using SharpCraft.SharpMath;
using SharpCraft.Time;

using System.Diagnostics;
using System.Numerics;

namespace SharpCraft.Rendering;

internal class DebugOverlay : IDisposable
{
    private Font font;

    private static readonly Vector4 NormalColor = Colors.WhiteSmoke.ToVector4();
    private static readonly Vector4 GoodColor = Colors.LightGreen.ToVector4();
    private static readonly Vector4 BadColor = Colors.Red.ToVector4();

    private readonly TextTextureManager textTextureManager;
    private readonly DynamicTextSlot fpsText;
    private readonly DynamicTextSlot managedMemoryText;
    private readonly DynamicTextSlot nativeMemoryText;

    private readonly DynamicTextSlot[] dynamicTexts;

    private float secondsElapsedSinceMemoryMeasurement;
    private double managedMemoryMb;
    private double nativeMemoryMb;

    public DebugOverlay(TextTextureManager textTextureManager, Font font)
    {
        this.font = font;

        this.textTextureManager = textTextureManager;
        fpsText = textTextureManager.CreateDynamic(font, "0");
        managedMemoryText = textTextureManager.CreateDynamic(font, "0.0");
        nativeMemoryText = textTextureManager.CreateDynamic(font, "0.0");

        dynamicTexts = [fpsText, managedMemoryText, nativeMemoryText];
    }

    public void Update(in FrameTime time)
    {
        secondsElapsedSinceMemoryMeasurement += time.DeltaSeconds;

        if (secondsElapsedSinceMemoryMeasurement >= 1)
        {
            managedMemoryMb = GetManagedMemoryMb();
            textTextureManager.UpdateDynamic(font, $"{managedMemoryMb:0.0} MB", managedMemoryText);

            nativeMemoryMb = GetNativeMemoryMb(managedMemoryMb);
            textTextureManager.UpdateDynamic(font, $"{nativeMemoryMb:0.0} MB", nativeMemoryText);

            secondsElapsedSinceMemoryMeasurement = 0;
        }

        textTextureManager.UpdateDynamic(font, time.Fps.ToString(), fpsText);
    }

    public void Draw(
        SpriteRenderer spriteRenderer,
        FrameTime time,
        GpuDevice device,
        uint screenWidth,
        uint screenHeight,
        float padding,
        float lineHeight)
    {
        DrawCpuColumn(
            spriteRenderer,
            time,
            padding,
            lineHeight
        );

        DrawGpuColumn(
            spriteRenderer,
            device,
            screenWidth,
            screenHeight,
            padding,
            lineHeight
        );
    }

    private void DrawCpuColumn(
        SpriteRenderer spriteRenderer,
        FrameTime time,
        float padding,
        float lineHeight)
    {
        float x = padding;
        float y = padding;

        DrawLeft(
            spriteRenderer,
            "Debug",
            null,
            x,
            ref y,
            lineHeight,
            NormalColor
        );

        DrawLeft(
            spriteRenderer,
            "FPS: ",
            fpsText,
            x,
            ref y,
            lineHeight,
            GetFpsColor(time.Fps)
        );

        DrawLeft(
            spriteRenderer,
            "Managed memory: ",
            managedMemoryText,
            x,
            ref y,
            lineHeight,
            NormalColor
        );

        DrawLeft(
            spriteRenderer,
            "Native memory: ",
            nativeMemoryText,
            x,
            ref y,
            lineHeight,
            NormalColor
        );
    }

    public void Rescale(Font font)
    {
        this.font = font;

        foreach (var dynamicText in dynamicTexts)
        {
            textTextureManager.UpdateDynamic(font, dynamicText.Text, dynamicText);
        }
    }

    private void DrawGpuColumn(
        SpriteRenderer spriteRenderer,
        GpuDevice device,
        uint screenWidth,
        uint screenHeight,
        float padding,
        float lineHeight)
    {
        float y = padding;

        DrawRight(
            spriteRenderer,
            "GPU",
            screenWidth,
            padding,
            ref y,
            lineHeight,
            NormalColor
        );

        DrawRight(
            spriteRenderer,
            $"Driver: {device.DriverName}",
            screenWidth,
            padding,
            ref y,
            lineHeight,
            NormalColor
        );

        DrawRight(
            spriteRenderer,
            $"Device: {device.DeviceName}",
            screenWidth,
            padding,
            ref y,
            lineHeight,
            NormalColor
        );

        DrawRight(
            spriteRenderer,
            $"Swapchain: {device.SwapchainFormat}",
            screenWidth,
            padding,
            ref y,
            lineHeight,
            NormalColor
        );

        DrawRight(
            spriteRenderer,
            $"Frame: {screenWidth}x{screenHeight}",
            screenWidth,
            padding,
            ref y,
            lineHeight,
            NormalColor
        );
    }

    private void DrawLeft(
        SpriteRenderer spriteRenderer,
        string staticText,
        DynamicTextSlot dynamicText,
        float x,
        ref float y,
        float lineHeight,
        Vector4 color)
    {
        Texture staticTexture = textTextureManager.GetOrCreateStatic(font, staticText);

        Rect staticDestination = new(
            x,
            y,
            staticTexture.Width,
            staticTexture.Height
        );

        spriteRenderer.DrawText(
            staticTexture,
            staticDestination,
            color
        );

        if (dynamicText != null)
        {
            Rect dynamicDestination = new(
                x + staticTexture.Width,
                y,
                dynamicText.Texture.Width,
                dynamicText.Texture.Height
            );

            spriteRenderer.DrawText(
                dynamicText.Texture,
                dynamicDestination,
                color
            );
        }

        y += lineHeight;
    }

    private void DrawRight(
        SpriteRenderer spriteRenderer,
        string text,
        uint screenWidth,
        float padding,
        ref float y,
        float lineHeight,
        Vector4 color)
    {
        Texture texture = textTextureManager.GetOrCreateStatic(font, text);

        float x = screenWidth - padding - texture.Width;

        Rect destination = new(
            x,
            y,
            texture.Width,
            texture.Height
        );

        spriteRenderer.DrawText(
            texture,
            destination,
            color
        );

        y += lineHeight;
    }

    private static Vector4 GetFpsColor(int fps)
    {
        if (fps >= 120)
        {
            return GoodColor;
        }

        if (fps >= 55)
        {
            return NormalColor;
        }

        return BadColor;
    }

    private static double GetManagedMemoryMb()
    {
        return GC.GetTotalMemory(forceFullCollection: false) / 1024.0 / 1024.0;
    }

    private static double GetNativeMemoryMb(double managedMemory)
    {
        using var process = Process.GetCurrentProcess();
        double processMemory = process.PrivateMemorySize64 / 1024.0 / 1024.0;
        return processMemory - managedMemory;
    }

    private bool disposed;
    public void Dispose()
    {
        if (disposed) return;

        foreach (var dynamicText in dynamicTexts)
        {
            dynamicText.Texture.Dispose();
        }

        disposed = true;
    }
}
