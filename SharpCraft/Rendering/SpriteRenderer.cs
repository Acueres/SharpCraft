using SharpCraft.AssetProcessing;
using SharpCraft.Graphics;
using SharpCraft.Graphics.Resources;
using SharpCraft.SharpMath;

using SDL;
using System.Numerics;
using System.Runtime.InteropServices;
using static SDL.SDL3;

namespace SharpCraft.Rendering;

internal enum SamplerType
{
    NearestClamp,
    LinearClamp
}

internal sealed unsafe class SpriteRenderer : IDisposable
{
    private readonly GpuUploader uploader;
    private readonly SpritePipeline pipeline;
    private readonly SpriteBuffer buffer;
    private readonly Sampler nearestSampler;
    private readonly Sampler linearSampler;

    private readonly List<SpriteVertex> vertices = [];
    private readonly List<uint> indices = [];
    private readonly List<SpriteBatch> batches = [];

    private bool disposed;

    public SpriteRenderer(
        GpuDevice device,
        GpuUploader uploader,
        GraphicsShader shader)
    {
        this.uploader = uploader;

        pipeline = new SpritePipeline(
            device,
            shader.Vertex,
            shader.Fragment
        );

        buffer = new SpriteBuffer(device);
        nearestSampler = Sampler.CreateNearestClamp(device);
        linearSampler = Sampler.CreateLinearClamp(device);
    }

    public void Begin()
    {
        vertices.Clear();
        indices.Clear();
        batches.Clear();
    }

    public void Draw(Texture texture, Rect destination, SamplerType samplerType)
    {
        Draw(
            texture,
            destination,
            source: new Rect(0f, 0f, texture.Width, texture.Height),
            color: Vector4.One,
            samplerType
        );
    }

    public void Draw(Texture texture, Rect destination, Vector4 color, SamplerType samplerType)
    {
        Draw(
            texture,
            destination,
            source: new Rect(0f, 0f, texture.Width, texture.Height),
            color,
            samplerType
        );
    }

    private void Draw(
        Texture texture,
        Rect destination,
        Rect source,
        Vector4 color,
        SamplerType samplerType)
    {
        AddBatch(texture, samplerType);

        uint baseVertex = (uint)vertices.Count;

        float invWidth = 1f / texture.Width;
        float invHeight = 1f / texture.Height;

        float u0 = source.Left * invWidth;
        float v0 = source.Top * invHeight;
        float u1 = source.Right * invWidth;
        float v1 = source.Bottom * invHeight;

        vertices.Add(new SpriteVertex(
            new Vector2(destination.Left, destination.Top),
            new Vector2(u0, v0),
            color
        ));

        vertices.Add(new SpriteVertex(
            new Vector2(destination.Right, destination.Top),
            new Vector2(u1, v0),
            color
        ));

        vertices.Add(new SpriteVertex(
            new Vector2(destination.Right, destination.Bottom),
            new Vector2(u1, v1),
            color
        ));

        vertices.Add(new SpriteVertex(
            new Vector2(destination.Left, destination.Bottom),
            new Vector2(u0, v1),
            color
        ));

        indices.Add(baseVertex + 0);
        indices.Add(baseVertex + 1);
        indices.Add(baseVertex + 2);

        indices.Add(baseVertex + 2);
        indices.Add(baseVertex + 3);
        indices.Add(baseVertex + 0);

        GrowCurrentBatch(6);
    }

    public void Upload()
    {
        uploader.Upload(
            buffer,
            CollectionsMarshal.AsSpan(vertices),
            CollectionsMarshal.AsSpan(indices)
        );
    }

    public void Render(
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

        foreach (SpriteBatch batch in batches)
        {
            SDL_GPUTextureSamplerBinding textureBinding = new()
            {
                texture = batch.Texture.Handle,
                sampler = batch.Sampler.Handle
            };

            SDL_BindGPUFragmentSamplers(
                renderPass,
                0,
                &textureBinding,
                1
            );

            SDL_DrawGPUIndexedPrimitives(
                renderPass,
                num_indices: batch.IndexCount,
                num_instances: 1,
                first_index: batch.FirstIndex,
                vertex_offset: 0,
                first_instance: 0
            );
        }
    }

    private void AddBatch(Texture texture, SamplerType samplerType)
    {
        if (batches.Count > 0)
        {
            SpriteBatch last = batches[^1];

            if (ReferenceEquals(last.Texture, texture))
            {
                return;
            }
        }

        Sampler sampler = samplerType switch
        {
            SamplerType.LinearClamp => linearSampler,
            SamplerType.NearestClamp => nearestSampler,
            _ => throw new Exception("Unknown samplerType: " + samplerType)
        };

        batches.Add(new SpriteBatch(
            texture,
            sampler,
            firstIndex: (uint)indices.Count,
            indexCount: 0
        ));
    }

    private void GrowCurrentBatch(uint indexCount)
    {
        int index = batches.Count - 1;

        SpriteBatch batch = batches[index];
        batches[index] = batch.WithAdditionalIndices(indexCount);
    }

    public void Dispose()
    {
        if (disposed) return;

        buffer.Dispose();
        pipeline.Dispose();
        nearestSampler.Dispose();
        linearSampler.Dispose();

        disposed = true;
    }
}