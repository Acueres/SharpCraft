using SharpCraft.AssetProcessing;
using SharpCraft.Graphics;
using SharpCraft.Diagnostics;
using SharpCraft.Graphics.Resources;
using SharpCraft.World.Chunks;
using SharpCraft.World.WorldStreaming;
using SharpCraft.SharpMath;
using SharpCraft.World.Meshing;
using SharpCraft.View;

using System.Numerics;
using System.Diagnostics;
using SDL;
using System.Runtime.InteropServices;
using static SDL.SDL3;

namespace SharpCraft.Rendering;

internal unsafe class VoxelFaceRenderer(GpuDevice device, GpuUploader uploader,
    TextureArray textureArray, GraphicsShader shader, ChunkMesher chunkMesher, FrameProfiler profiler) : IDisposable
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
        long started = Stopwatch.GetTimestamp();
        visibleMeshes.Clear();

        int opaqueCount = 0;
        int transparentCount = 0;
        int residentCount = 0;

        // Cull
        foreach (var chunk in volume.GetActiveChunks())
        {
            residentCount++;
            if (chunk.IsEmpty || !chunk.IsReady) continue;

            var relativeIndex = (chunk.Index - camera.Index) * Chunk.Size;
            var relativePosition = new Vector3(relativeIndex.X, relativeIndex.Y, relativeIndex.Z);
            relativePosition -= camera.LocalPosition;

            const float localCenter = Chunk.Last * 0.5f;
            
            Vector3 boundsCenter = relativePosition + new Vector3(localCenter);
            if (!camera.Frustum.Intersects(new CubeBound(boundsCenter, Chunk.HalfSize))) continue;
            
            var opaqueArr = chunkMesher.GetFaces(chunk.Index);
            opaqueCount += opaqueArr.Length;

            var transparentArr = chunkMesher.GetTransparentFaces(chunk.Index);
            transparentCount += transparentArr.Length;

            visibleMeshes.Add((opaqueArr, transparentArr));
        }

        profiler.Timings.CullingMilliseconds += Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        profiler.Rendering.ResidentChunks = residentCount;
        profiler.Rendering.VisibleChunks = visibleMeshes.Count;
        started = Stopwatch.GetTimestamp();

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

        profiler.Timings.AssemblyMilliseconds += Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        started = Stopwatch.GetTimestamp();
        
        Upload(faces, transparentFaces);
        
        profiler.Timings.TerrainUploadMilliseconds += Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        profiler.Rendering.OpaqueFaces = opaqueBuffer.Count;
        profiler.Rendering.TransparentFaces = transparentBuffer.Count;
        profiler.Rendering.TerrainUsedBytes = ((long)opaqueBuffer.Count + transparentBuffer.Count) * sizeof(VoxelFace);
        profiler.Rendering.TerrainBufferBytes = opaqueBuffer.CapacityBytes + transparentBuffer.CapacityBytes;
    }

    private void Upload(List<VoxelFace> data, List<VoxelFace> transparentData)
    {
        uploader.Upload(opaqueBuffer, CollectionsMarshal.AsSpan(data));
        uploader.Upload(transparentBuffer, CollectionsMarshal.AsSpan(transparentData));
    }

    public void Draw(
        SDL_GPUCommandBuffer* commandBuffer,
        SDL_GPURenderPass* renderPass,
        Matrix4x4 mvp, Vec3<int> cameraPositionIndex, Vector3 cameraLocalPosition)
    {
        // Upload camera buffer
        CameraUniformGpu uniform = new()
        {
            Mvp = mvp,
            ChunkX = cameraPositionIndex.X,
            ChunkY = cameraPositionIndex.Y,
            ChunkZ = cameraPositionIndex.Z,
            LocalPosition = cameraLocalPosition
        };

        SDL_PushGPUVertexUniformData(
            commandBuffer,
            0,
            (nint)(&uniform),
            (uint)sizeof(CameraUniformGpu)
        );
        
        GpuDevice.BeginGpuPass(commandBuffer, GpuPass.Opaque);
        DrawBuffer(opaqueBuffer, opaquePipeline, renderPass);
        GpuDevice.EndGpuPass(commandBuffer);
        profiler.Rendering.SubmittedOpaqueFaces = opaqueBuffer.Count;
        GpuDevice.BeginGpuPass(commandBuffer, GpuPass.Transparent);
        DrawBuffer(transparentBuffer, transparentPipeline, renderPass);
        GpuDevice.EndGpuPass(commandBuffer);
        profiler.Rendering.SubmittedTransparentFaces = transparentBuffer.Count;
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
        profiler.Rendering.TerrainDrawCalls++;
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
