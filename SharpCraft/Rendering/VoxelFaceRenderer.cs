using SharpCraft.AssetProcessing;
using SharpCraft.Graphics;
using SharpCraft.Diagnostics;
using SharpCraft.Graphics.Resources;
using SharpCraft.World.Chunks;
using SharpCraft.World.WorldStreaming;
using SharpCraft.SharpMath;
using SharpCraft.View;
using SharpCraft.World.Meshing;

using System.Numerics;
using System.Diagnostics;
using SDL;
using System.Runtime.InteropServices;
using static SDL.SDL3;

namespace SharpCraft.Rendering;

internal unsafe class VoxelFaceRenderer(GpuDevice device, GpuUploader uploader,
    TextureArray textureArray, GraphicsShader shader, MeshSink meshSink, FrameProfiler profiler) : IDisposable
{
    private readonly Sampler sampler = Sampler.CreateNearestRepeat(device);
    
    private readonly BlockFacePipeline opaquePipeline = BlockFacePipeline.CreateOpaque(device, shader.Vertex, shader.Fragment);
    private readonly BlockFaceBuffer opaqueBuffer = new(device);
    
    private readonly BlockFacePipeline transparentPipeline = BlockFacePipeline.CreateTransparent(device, shader.Vertex, shader.Fragment);
    private readonly BlockFaceBuffer transparentBuffer = new(device);
    
    private readonly SlotRecordBuffer slotRecordBuffer = new(device);

    private const uint pageCapacity = 1 << 16;
    
    private readonly Arena opaqueArena = new(pageCapacity);
    private readonly Arena transparentArena = new(pageCapacity);
    
    private readonly Dictionary<Vec3<int>, MeshEntry> meshCache = [];
    
    private readonly List<VoxelFace> faces = [];
    private readonly List<VoxelFace> transparentFaces = [];
    private readonly List<(List<CpuAllocation> opaque, List<CpuAllocation> transparent, ushort slotId)> visibleMeshes = [];
    private readonly List<SlotRecord> slotRecords = [];

    public void Update(ChunkVolume volume, Camera camera)
    {
        long started = Stopwatch.GetTimestamp();

        visibleMeshes.Clear();
        slotRecords.Clear();

        uint opaqueCount = 0;
        uint transparentCount = 0;
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

            if (slotRecords.Count > ushort.MaxValue)
                throw new InvalidOperationException("Slot table capacity exceeded");

            const float radius = Chunk.HalfSize * MathUtilities.SquareRootOfThree;

            ushort slotId = (ushort)slotRecords.Count;
            slotRecords.Add(new SlotRecord(chunk.Index, new Vector3(localCenter), radius));

            var meshEntry = meshCache[chunk.Index];

            var opaqueAllocations = meshEntry.OpaqueAllocations;
            opaqueCount += meshEntry.OpaqueCount;

            var transparentAllocations = meshEntry.TransparentAllocations;
            transparentCount += meshEntry.TransparentCount;

            visibleMeshes.Add((opaqueAllocations, transparentAllocations, slotId));
        }

        profiler.Timings.CullingMilliseconds += Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        profiler.Rendering.ResidentChunks = residentCount;
        profiler.Rendering.VisibleChunks = visibleMeshes.Count;
        started = Stopwatch.GetTimestamp();

        // Ensure capacity
        faces.Clear();
        transparentFaces.Clear();
        if (faces.Capacity < opaqueCount) faces.Capacity = (int)opaqueCount;
        if (transparentFaces.Capacity < transparentCount) transparentFaces.Capacity = (int)transparentCount;

        // Fill
        foreach (var (opaqueAllocations, transparentAllocations, slotId) in visibleMeshes)
        {
            foreach (var opaqueAllocation in opaqueAllocations)
            {
                var pageOpaqueFaces = opaqueAllocation.Page.GetFaces(opaqueAllocation);

                foreach (var f in pageOpaqueFaces)
                {
                    var face = f;
                    face.SetSlotId(slotId);
                    faces.Add(face);
                }
            }

            foreach (var transparentAllocation in transparentAllocations)
            {
                var pageTransparentFaces = transparentAllocation.Page.GetFaces(transparentAllocation);
                
                foreach (var f in pageTransparentFaces)
                {
                    var face = f;
                    face.SetSlotId(slotId);
                    transparentFaces.Add(face);
                }
            }
        }

        profiler.Timings.AssemblyMilliseconds += Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        started = Stopwatch.GetTimestamp();

        Upload(faces, transparentFaces, slotRecords);

        profiler.Timings.TerrainUploadMilliseconds += Stopwatch.GetElapsedTime(started).TotalMilliseconds;

        profiler.Rendering.OpaqueFaces = opaqueBuffer.Count;
        profiler.Rendering.TransparentFaces = transparentBuffer.Count;

        profiler.Rendering.TerrainUsedBytes =
            ((long)opaqueBuffer.Count + transparentBuffer.Count) * sizeof(VoxelFace);
        profiler.Rendering.TerrainUsedBytes += slotRecordBuffer.Count * sizeof(SlotRecord);

        profiler.Rendering.TerrainBufferBytes = opaqueBuffer.CapacityBytes + transparentBuffer.CapacityBytes +
                                                slotRecordBuffer.CapacityBytes;
    }

    public void ProcessMeshUpdates()
    {
        while (meshSink.ReadMesh(out var meshUpdate))
        {
            bool entryExists = meshCache.TryGetValue(meshUpdate.Index, out var entry);
            
            if (meshUpdate.State == MeshUpdateState.Obsolete)
            {
                if (!entryExists) continue;
                
                meshCache.Remove(meshUpdate.Index);
                ReleaseAllocations(entry!.OpaqueAllocations, entry.TransparentAllocations);
                continue;
            }

            var opaqueAllocations = new List<CpuAllocation>();
            var transparentAllocations = new List<CpuAllocation>();
            try
            {
                opaqueAllocations = opaqueArena.Allocate(meshUpdate.Opaque);
                transparentAllocations = transparentArena.Allocate(meshUpdate.Transparent);
                
                meshCache[meshUpdate.Index] = new MeshEntry(opaqueAllocations, transparentAllocations,
                    (uint)meshUpdate.Opaque.Length, (uint)meshUpdate.Transparent.Length);
            }
            catch
            {
                ReleaseAllocations(opaqueAllocations, transparentAllocations);
                throw;
            }
            
            if (entryExists)
            {
                ReleaseAllocations(entry!.OpaqueAllocations, entry.TransparentAllocations);
            }
        }
        
        opaqueArena.ReclaimMemory();
        transparentArena.ReclaimMemory();
    }

    private void ReleaseAllocations(List<CpuAllocation> opaqueAllocations, List<CpuAllocation> transparentAllocations)
    {
        foreach (var opaqueAllocation in opaqueAllocations)
        {
            opaqueArena.Release(opaqueAllocation);
        }

        foreach (var transparentAllocation in transparentAllocations)
        {
            transparentArena.Release(transparentAllocation);
        }
    }

    private void Upload(List<VoxelFace> data, List<VoxelFace> transparentData, List<SlotRecord> slots)
    {
        uploader.Upload(slotRecordBuffer, CollectionsMarshal.AsSpan(slots));
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
        
        SDL_GPUBuffer** buffers =  stackalloc SDL_GPUBuffer*[1];
        buffers[0] = slotRecordBuffer.Handle;
        
        SDL_BindGPUVertexStorageBuffers(renderPass, 0, buffers, 1);

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
        
        slotRecordBuffer.Dispose();
        
        sampler.Dispose();
        
        disposed = true;
    }
}
