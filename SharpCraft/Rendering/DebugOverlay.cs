using SharpCraft.Graphics;
using SharpCraft.Graphics.Resources;
using SharpCraft.Rendering.Text;
using SharpCraft.SharpMath;
using SharpCraft.Time;

using System.Diagnostics;
using System.Numerics;

namespace SharpCraft.Rendering;

internal class DebugOverlay(TextTextureCache textTextureCache, Font font)
{
    private static readonly Vector4 TitleColor = Colors.CornflowerBlue.ToVector4();
    private static readonly Vector4 NormalColor = Colors.WhiteSmoke.ToVector4();
    private static readonly Vector4 GoodColor = Colors.LightGreen.ToVector4();
    private static readonly Vector4 BadColor = Colors.Red.ToVector4();
    private static readonly Vector4 MutedColor = Colors.DarkGray.ToVector4();

    private readonly TextTextureCache textTextureCache = textTextureCache;

    public Font Font { get; set; } = font;

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
            x,
            ref y,
            lineHeight,
            TitleColor
        );

        DrawLeft(
            spriteRenderer,
            $"FPS: {time.Fps}",
            x,
            ref y,
            lineHeight,
            GetFpsColor(time.Fps)
        );

        DrawLeft(
            spriteRenderer,
            $"Frame: {time.DeltaSeconds * 1000f:0.00} ms",
            x,
            ref y,
            lineHeight,
            NormalColor
        );

        double managedMemory = GetManagedMemoryMb();
        DrawLeft(
            spriteRenderer,
            $"Managed memory: {managedMemory:0.0} MB",
            x,
            ref y,
            lineHeight,
            NormalColor
        );

        double nativememory = GetNativeMemoryMb(managedMemory);
        DrawLeft(
            spriteRenderer,
            $"Native memory: {nativememory:0.0}",
            x,
            ref y,
            lineHeight,
            MutedColor
        );
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
            TitleColor
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
            MutedColor
        );

        DrawRight(
            spriteRenderer,
            $"Frame: {screenWidth}x{screenHeight}",
            screenWidth,
            padding,
            ref y,
            lineHeight,
            MutedColor
        );
    }

    private void DrawLeft(
        SpriteRenderer spriteRenderer,
        string text,
        float x,
        ref float y,
        float lineHeight,
        Vector4 color)
    {
        Texture texture = textTextureCache.GetOrCreate(Font, text);

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

    private void DrawRight(
        SpriteRenderer spriteRenderer,
        string text,
        uint screenWidth,
        float padding,
        ref float y,
        float lineHeight,
        Vector4 color)
    {
        Texture texture = textTextureCache.GetOrCreate(Font, text);

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
        double processMemory = Process.GetCurrentProcess().PrivateMemorySize64 / 1024.0 / 1024.0;
        return processMemory - managedMemory;
    }
}
