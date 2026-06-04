using SharpCraft.Platform;

using System.Text;
using SDL;

using static SDL.SDL3;
using static SDL.SDL3_ttf;

namespace SharpCraft.Rendering.Text;

internal static unsafe class TextRasterizer
{
    public static TextBitmap Render(Font font, string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return new TextBitmap(1, 1, "\0\0\0\0"u8.ToArray());
        }

        byte[] textBytes = Encoding.UTF8.GetBytes(text);

        SDL_Color white = new()
        {
            r = 255,
            g = 255,
            b = 255,
            a = 255
        };

        SDL_Surface* rawSurface;

        fixed (byte* pText = textBytes)
        {
            rawSurface = TTF_RenderText_Blended(
                font.Handle,
                pText,
                (nuint)textBytes.Length,
                white
            );
        }

        if (rawSurface == null)
        {
            SdlRuntime.Throw($"Failed to render text: {text}");
        }

        SDL_Surface* rgbaSurface = null;

        try
        {
            rgbaSurface = SDL_ConvertSurface(
                rawSurface,
                SDL_PixelFormat.SDL_PIXELFORMAT_ABGR8888
            );

            if (rgbaSurface == null)
            {
                SdlRuntime.Throw("Failed to convert text surface to RGBA32");
            }

            if (!SDL_LockSurface(rgbaSurface))
            {
                SdlRuntime.Throw("Failed to lock text surface");
            }

            try
            {
                int width = rgbaSurface->w;
                int height = rgbaSurface->h;
                int pitch = rgbaSurface->pitch;

                byte[] pixels = new byte[width * height * 4];

                byte* source = (byte*)rgbaSurface->pixels;

                fixed (byte* destination = pixels)
                {
                    for (int y = 0; y < height; y++)
                    {
                        Buffer.MemoryCopy(
                            source + y * pitch,
                            destination + y * width * 4,
                            width * 4,
                            width * 4
                        );
                    }
                }

                return new TextBitmap(
                    (uint)width,
                    (uint)height,
                    pixels
                );
            }
            finally
            {
                SDL_UnlockSurface(rgbaSurface);
            }
        }
        finally
        {
            SDL_DestroySurface(rawSurface);
            SDL_DestroySurface(rgbaSurface);
        }
    }
}