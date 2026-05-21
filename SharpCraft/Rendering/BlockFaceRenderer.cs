using SharpCraft.AssetProcessing;
using SharpCraft.Graphics;
using SharpCraft.Graphics.Resources;

using System.Numerics;
using SDL;

using static SDL.SDL3;

namespace SharpCraft.Rendering;

internal unsafe class BlockFaceRenderer(GpuDevice device, GpuUploader uploader,
    TextureArray textureArray, GraphicsShader shader) : IDisposable
{
    private readonly Sampler sampler = new(device);
    
    private readonly BlockFacePipeline opaquePipeline = BlockFacePipeline.CreateOpaque(device, shader.Vertex, shader.Fragment);
    private readonly BlockFaceBuffer opaqueBuffer = new(device);
    
    private readonly BlockFacePipeline transparentPipeline = BlockFacePipeline.CreateTransparent(device, shader.Vertex, shader.Fragment);
    private readonly BlockFaceBuffer transparentBuffer = new(device);

    public void Upload(BlockFace[] data, BlockFace[] transparentData)
    {
        uploader.Upload(opaqueBuffer, data);
        uploader.Upload(transparentBuffer, transparentData);
    }

    public void Draw(
        SDL_GPUCommandBuffer* commandBuffer,
        SDL_GPURenderPass* renderPass,
        Matrix4x4 mvp)
    {
        SDL_PushGPUVertexUniformData(
            commandBuffer,
            0,
            (nint)(&mvp),
            (uint)sizeof(Matrix4x4)
        );
        
        DrawBuffer(opaqueBuffer, opaquePipeline, renderPass);
        DrawBuffer(transparentBuffer, transparentPipeline, renderPass);
    }
    
    private void DrawBuffer(
        BlockFaceBuffer buffer,
        BlockFacePipeline pipeline,
        SDL_GPURenderPass* renderPass)
    {
        if (buffer.Count == 0)
        {
            return;
        }

        SDL_BindGPUGraphicsPipeline(renderPass, pipeline.Handle);

        SDL_GPUBufferBinding vertexBinding = new()
        {
            buffer = buffer.Handle,
            offset = 0
        };

        SDL_BindGPUVertexBuffers(renderPass, 0, &vertexBinding, 1);

        SDL_GPUTextureSamplerBinding textureBinding = new()
        {
            texture = textureArray.Handle,
            sampler = sampler.Handle
        };

        SDL_BindGPUFragmentSamplers(
            renderPass,
            0,
            &textureBinding,
            1
        );

        SDL_DrawGPUPrimitives(
            renderPass,
            num_vertices: 6,
            num_instances: buffer.Count,
            first_vertex: 0,
            first_instance: 0
        );
    }

    private bool disposed;

    public void Dispose()
    {
        if (disposed) return;
        
        opaqueBuffer.Dispose();
        opaquePipeline.Dispose();
        
        transparentBuffer.Dispose();
        transparentPipeline.Dispose();
        
        sampler.Dispose();
        
        disposed = true;
    }
}