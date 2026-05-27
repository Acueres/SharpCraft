using SharpCraft.AssetProcessing;
using SharpCraft.Graphics;
using SharpCraft.Graphics.Resources;
using SharpCraft.SharpMath;

using SDL;
using System.Numerics;

using static SDL.SDL3;

namespace SharpCraft.Rendering;

internal unsafe sealed class SpriteRenderer : IDisposable
{
    private readonly GpuUploader uploader;
    private readonly SpritePipeline pipeline;
    private readonly SpriteBuffer buffer;
    private readonly Sampler sampler;
    private readonly Texture crosshairTexture;

    private SpriteVertex[] vertices = [];
    private uint[] indices = [];

    public SpriteRenderer(
        GpuDevice device,
        GpuUploader uploader,
        Texture crosshairTexture,
        GraphicsShader shader)
    {
        this.uploader = uploader;
        this.crosshairTexture = crosshairTexture;

        pipeline = new SpritePipeline(
            device,
            shader.Vertex,
            shader.Fragment
        );

        buffer = new SpriteBuffer(device);
        sampler = Sampler.CreateNearestClamp(device);
    }

    public void BuildCrosshair(uint screenWidth, uint screenHeight)
    {
        const float size = 32f;

        Rect destination = new(
            screenWidth * 0.5f - size * 0.5f,
            screenHeight * 0.5f - size * 0.5f,
            size,
            size
        );

        Vector4 color = Vector4.One;

        vertices =
        [
            new SpriteVertex(
                new Vector2(destination.Left, destination.Top),
                new Vector2(0f, 0f),
                color
            ),

            new SpriteVertex(
                new Vector2(destination.Right, destination.Top),
                new Vector2(1f, 0f),
                color
            ),

            new SpriteVertex(
                new Vector2(destination.Right, destination.Bottom),
                new Vector2(1f, 1f),
                color
            ),

            new SpriteVertex(
                new Vector2(destination.Left, destination.Bottom),
                new Vector2(0f, 1f),
                color
            )
        ];

        indices =
        [
            0, 1, 2,
            2, 3, 0
        ];

        uploader.Upload(buffer, vertices, indices);
    }

    public void Draw(
        SDL_GPUCommandBuffer* commandBuffer,
        SDL_GPURenderPass* renderPass,
        uint screenWidth,
        uint screenHeight)
    {
        if (buffer.IndexCount == 0)
        {
            return;
        }

        Vector4 screen = new(screenWidth, screenHeight, 0f, 0f);

        SDL_PushGPUVertexUniformData(
            commandBuffer,
            0,
            (nint)(&screen),
            (uint)sizeof(Vector4)
        );

        SDL_BindGPUGraphicsPipeline(renderPass, pipeline.Handle);

        SDL_GPUBufferBinding vertexBinding = new()
        {
            buffer = buffer.VertexHandle,
            offset = 0
        };

        SDL_BindGPUVertexBuffers(
            renderPass,
            0,
            &vertexBinding,
            1
        );

        SDL_GPUBufferBinding indexBinding = new()
        {
            buffer = buffer.IndexHandle,
            offset = 0
        };

        SDL_BindGPUIndexBuffer(
            renderPass,
            &indexBinding,
            SDL_GPUIndexElementSize.SDL_GPU_INDEXELEMENTSIZE_32BIT
        );

        SDL_GPUTextureSamplerBinding textureBinding = new()
        {
            texture = crosshairTexture.Handle,
            sampler = sampler.Handle
        };

        SDL_BindGPUFragmentSamplers(
            renderPass,
            0,
            &textureBinding,
            1
        );

        SDL_DrawGPUIndexedPrimitives(
            renderPass,
            num_indices: buffer.IndexCount,
            num_instances: 1,
            first_index: 0,
            vertex_offset: 0,
            first_instance: 0
        );
    }

    private bool disposed;

    public void Dispose()
    {
        if (disposed) return;

        buffer.Dispose();
        pipeline.Dispose();
        sampler.Dispose();

        disposed = true;
    }
}