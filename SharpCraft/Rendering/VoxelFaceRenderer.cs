using SharpCraft.AssetProcessing;
using SharpCraft.Graphics;
using SharpCraft.Graphics.Resources;
using SharpCraft.World.Chunks;
using SharpCraft.World.WorldStreaming;
using SharpCraft.SharpMath;
using SharpCraft.World.Meshing;

using System.Numerics;
using SDL;
using System.Runtime.InteropServices;
using static SDL.SDL3;

namespace SharpCraft.Rendering;

internal unsafe class VoxelFaceRenderer(GpuDevice device, GpuUploader uploader,
    TextureArray textureArray, GraphicsShader shader, ChunkMesher chunkMesher) : IDisposable
{
    private readonly Sampler sampler = Sampler.CreateNearestRepeat(device);
    
    private readonly BlockFacePipeline opaquePipeline = BlockFacePipeline.CreateOpaque(device, shader.Vertex, shader.Fragment);
    private readonly BlockFaceBuffer opaqueBuffer = new(device);
    
    private readonly BlockFacePipeline transparentPipeline = BlockFacePipeline.CreateTransparent(device, shader.Vertex, shader.Fragment);
    private readonly BlockFaceBuffer transparentBuffer = new(device);
    
    private readonly List<VoxelFace> faces = [];
    private readonly List<VoxelFace> transparentFaces = [];
    private readonly List<(VoxelFace[] opaque, VoxelFace[] transparent)> visibleMeshes = [];

    public void Update(ChunkVolume volume, Camera camera)
    {
        visibleMeshes.Clear();

        int opaqueCount = 0;
        int transparentCount = 0;

        // Cull
        foreach (var chunk in volume.GetActiveChunks())
        {
            if (chunk.IsEmpty || !chunk.IsReady) continue;

            Vector3 center = chunk.Position + new Vector3(Chunk.HalfSize);
            if (!camera.Frustum.Intersects(new CubeBound(center, Chunk.HalfSize))) continue;
            
            var opaqueArr = chunkMesher.GetFaces(chunk.Index);
            opaqueCount += opaqueArr.Length;

            var transparentArr = chunkMesher.GetTransparentFaces(chunk.Index);
            transparentCount += transparentArr.Length;

            visibleMeshes.Add((opaqueArr, transparentArr));
        }

        // Ensure capacity
        faces.Clear();
        transparentFaces.Clear();
        if (faces.Capacity < opaqueCount) faces.Capacity = opaqueCount;
        if (transparentFaces.Capacity < transparentCount) transparentFaces.Capacity = transparentCount;

        // Fill
        foreach (var (opaqueArr, transparentArr) in visibleMeshes)
        {
            faces.AddRange(opaqueArr);
            transparentFaces.AddRange(transparentArr);
        }

        Upload(faces, transparentFaces);
    }

    private void Upload(List<VoxelFace> data, List<VoxelFace> transparentData)
    {
        uploader.Upload(opaqueBuffer, CollectionsMarshal.AsSpan(data));
        uploader.Upload(transparentBuffer, CollectionsMarshal.AsSpan(transparentData));
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