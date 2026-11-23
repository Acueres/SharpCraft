using SharpCraft.AssetProcessing;
using SharpCraft.Graphics;
using SharpCraft.Diagnostics;
using SharpCraft.Graphics.Resources;
using SharpCraft.World.Chunks;
using SharpCraft.SharpMath;
using SharpCraft.View;
using SharpCraft.World.Meshing;

using System.Numerics;
using System.Diagnostics;
using SDL;
using static SDL.SDL3;

namespace SharpCraft.Rendering;

internal unsafe class VoxelFaceRenderer(GpuDevice device, GpuUploader uploader,
    TextureArray textureArray, GraphicsShader shader, MeshSink meshSink, SubmissionTracker submissionTracker, FrameProfiler profiler) : IDisposable
{
    private record Retirement(GpuAllocation Allocation, Arena Arena, ulong SubmissionId);
    
    private readonly Sampler sampler = Sampler.CreateNearestRepeat(device);
    
    private readonly BlockFacePipeline opaquePipeline = BlockFacePipeline.CreateOpaque(device, shader.Vertex, shader.Fragment);
    private readonly BlockFacePipeline transparentPipeline = BlockFacePipeline.CreateTransparent(device, shader.Vertex, shader.Fragment);

    private const float LocalCenter = Chunk.Last * 0.5f;
    private const float Radius = Chunk.HalfSize * MathUtilities.SquareRootOfThree;

    private const uint PageFaceCapacity = 1 << 23;
    private const uint PageSlotCapacity = 1 << 15;
    private const uint MaxUploadFacesPerFrame = 1 << 17;
    
    private readonly Arena opaqueArena = new(PageFaceCapacity, PageSlotCapacity, device);
    private readonly Arena transparentArena = new(PageFaceCapacity, PageSlotCapacity, device);
    
    private readonly Dictionary<Vec3<int>, MeshEntry> meshCache = [];
    private readonly Queue<Retirement> retirements = [];
    
    private readonly List<GpuAllocation> visibleOpaqueAllocations = [];
    private readonly List<GpuAllocation> visibleTransparentAllocations = [];
    private readonly List<(float Distance, MeshEntry Mesh)> visibleTransparentMeshes = [];
    
    public void Update(Camera camera)
    {
        long started = Stopwatch.GetTimestamp();

        visibleOpaqueAllocations.Clear();
        visibleTransparentAllocations.Clear();
        visibleTransparentMeshes.Clear();

        uint opaqueCount = 0;
        uint transparentCount = 0;
        int residentCount = 0;
        int visibleChunks = 0;

        // Cull
        foreach (var (index, mesh) in meshCache)
        {
            residentCount++;
            
            var relativeIndex = (index - camera.Index) * Chunk.Size;
            var relativePosition = new Vector3(relativeIndex.X, relativeIndex.Y, relativeIndex.Z);
            relativePosition -= camera.LocalPosition;
            
            Vector3 boundsCenter = relativePosition + new Vector3(LocalCenter);
            if (!camera.Frustum.Intersects(new CubeBound(boundsCenter, Chunk.HalfSize))) continue;
            
            opaqueCount += mesh.OpaqueCount;
            transparentCount += mesh.TransparentCount;

            if (mesh is { OpaqueCount: 0, TransparentCount: 0 }) continue;
            
            visibleChunks++;
            
            visibleOpaqueAllocations.AddRange(mesh.OpaqueAllocations);

            if (mesh.TransparentCount > 0)
            {
                float distanceToCenterSquared = boundsCenter.LengthSquared();
                visibleTransparentMeshes.Add((distanceToCenterSquared, mesh));
            }
        }

        profiler.Timings.CullingMilliseconds += Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        started = Stopwatch.GetTimestamp();

        var transparentOrdered = visibleTransparentMeshes
            .OrderByDescending(x => x.Distance);
        var transparentAllocations = transparentOrdered.SelectMany(x => x.Mesh.TransparentAllocations);
        visibleTransparentAllocations.AddRange(transparentAllocations);

        profiler.Timings.AssemblyMilliseconds += Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        profiler.Rendering.ResidentChunks = residentCount;
        profiler.Rendering.VisibleChunks = visibleChunks;

        profiler.Rendering.OpaqueFaces = opaqueCount;
        profiler.Rendering.TransparentFaces = transparentCount;
    }

    public bool ProcessMeshUpdates()
    {
        uint uploadedFaceCount = 0;
        bool cacheChanged = false;
        long started = Stopwatch.GetTimestamp();

        while (meshSink.TryPeekMesh(out var meshUpdate))
        {
            var updateFaceCount = meshUpdate.Opaque.Length + meshUpdate.Transparent.Length;

            if (updateFaceCount + uploadedFaceCount > MaxUploadFacesPerFrame)
                break;

            if (!meshSink.TryDequeueMesh()) continue;

            bool entryExists = meshCache.TryGetValue(meshUpdate.Index, out var meshEntry);

            if (meshUpdate.State == MeshUpdateState.Obsolete)
            {
                if (!entryExists) continue;

                meshCache.Remove(meshUpdate.Index);
                RetireMesh(meshEntry!);
                cacheChanged = true;
                continue;
            }
            
            if (updateFaceCount == 0)
            {
                if (entryExists)
                {
                    meshCache.Remove(meshUpdate.Index);
                    RetireMesh(meshEntry!);
                    cacheChanged = true;
                }
                continue;
            }

            var opaqueAllocations = new List<GpuAllocation>();
            var transparentAllocations = new List<GpuAllocation>();
            try
            {
                opaqueAllocations = opaqueArena.Allocate(meshUpdate.Opaque);
                transparentAllocations = transparentArena.Allocate(meshUpdate.Transparent);

                int opaqueCount = 0;
                foreach (var opaqueAllocation in opaqueAllocations)
                {
                    var slotRecord = new SlotRecord(meshUpdate.Index, new Vector3(LocalCenter), Radius);
                    Upload(opaqueAllocation,
                        meshUpdate.Opaque.AsSpan().Slice(opaqueCount, (int)opaqueAllocation.CpuAllocation.Count),
                        slotRecord);

                    opaqueCount += (int)opaqueAllocation.CpuAllocation.Count;
                }

                int transparentCount = 0;
                foreach (var transparentAllocation in transparentAllocations)
                {
                    var slotRecord = new SlotRecord(meshUpdate.Index, new Vector3(LocalCenter), Radius);
                    Upload(transparentAllocation,
                        meshUpdate.Transparent.AsSpan()
                            .Slice(transparentCount, (int)transparentAllocation.CpuAllocation.Count),
                        slotRecord);

                    transparentCount += (int)transparentAllocation.CpuAllocation.Count;
                }

                meshCache[meshUpdate.Index] = new MeshEntry(opaqueAllocations, transparentAllocations,
                    (uint)meshUpdate.Opaque.Length, (uint)meshUpdate.Transparent.Length);

                uploadedFaceCount += (uint)updateFaceCount;

                cacheChanged = true;

                // Retired payload remains counted until its allocation is released
                profiler.Rendering.TerrainUsedBytes +=
                    ((long)updateFaceCount) * sizeof(VoxelFace)
                    + ((long)opaqueAllocations.Count + transparentAllocations.Count) * sizeof(SlotRecord);
            }
            catch
            {
                ReleaseAllocations(opaqueAllocations, transparentAllocations);
                throw;
            }

            if (entryExists)
            {
                RetireMesh(meshEntry!);
            }
        }

        opaqueArena.ReclaimMemory();
        transparentArena.ReclaimMemory();

        profiler.Rendering.TerrainBufferBytes = opaqueArena.TotalCapacityBytes + transparentArena.TotalCapacityBytes;
        profiler.Timings.RendererMilliseconds += Stopwatch.GetElapsedTime(started).TotalMilliseconds;

        return cacheChanged;
    }

    public void ReleaseRetired()
    {
        long started = Stopwatch.GetTimestamp();
        int count = retirements.Count;
        
        for (int i = 0; i < count; i++)
        {
            var retirement = retirements.Peek();

            if (retirement.SubmissionId > submissionTracker.CompletedId)
            {
                break;
            }
            
            retirements.Dequeue();
            retirement.Arena.Release(retirement.Allocation);
            profiler.Rendering.TerrainUsedBytes -=
                retirement.Allocation.CpuAllocation.Count * sizeof(VoxelFace) + sizeof(SlotRecord);
        }

        profiler.Timings.RendererMilliseconds += Stopwatch.GetElapsedTime(started).TotalMilliseconds;
    }

    private void RetireMesh(MeshEntry meshEntry)
    {
        foreach (var opaqueAllocation in meshEntry.OpaqueAllocations)
        {
            var retirement = new Retirement(opaqueAllocation, opaqueArena, submissionTracker.LastSubmittedId);
            retirements.Enqueue(retirement);
        }

        foreach (var transparentAllocation in meshEntry.TransparentAllocations)
        {
            var retirement = new Retirement(transparentAllocation, transparentArena, submissionTracker.LastSubmittedId);
            retirements.Enqueue(retirement);
        }
    }

    private void ReleaseAllocations(List<GpuAllocation> opaqueAllocations, List<GpuAllocation> transparentAllocations)
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

    private void Upload(GpuAllocation allocation, ReadOnlySpan<VoxelFace> faces, SlotRecord slotRecord)
    {
        long started = Stopwatch.GetTimestamp();

        uploader.Upload(allocation.Page.FaceBuffer, faces, allocation.CpuAllocation.First,
            allocation.Page.SlotBuffer, slotRecord, allocation.SlotId);

        profiler.Timings.TerrainUploadMilliseconds += Stopwatch.GetElapsedTime(started).TotalMilliseconds;
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
        
        // Upload texture
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
        
        // Draw opaque
        GpuDevice.BeginGpuPass(commandBuffer, GpuPass.Opaque);
        SDL_BindGPUGraphicsPipeline(renderPass, opaquePipeline.Handle);

        uint submittedOpaqueFaces = 0;
        foreach (var opaqueAllocation in visibleOpaqueAllocations)
        {
            DrawAllocation(opaqueAllocation, renderPass);
            submittedOpaqueFaces += opaqueAllocation.CpuAllocation.Count;
        }

        GpuDevice.EndGpuPass(commandBuffer);

        profiler.Rendering.SubmittedOpaqueFaces = submittedOpaqueFaces;
        
        //Draw transparent
        GpuDevice.BeginGpuPass(commandBuffer, GpuPass.Transparent);
        SDL_BindGPUGraphicsPipeline(renderPass, transparentPipeline.Handle);
        
        uint submittedTransparentFaces = 0;
        foreach (var transparentAllocation in visibleTransparentAllocations)
        {
            DrawAllocation(transparentAllocation, renderPass);
            submittedTransparentFaces += transparentAllocation.CpuAllocation.Count;
        }

        GpuDevice.EndGpuPass(commandBuffer);

        profiler.Rendering.SubmittedTransparentFaces = submittedTransparentFaces;
    }
    
    private void DrawAllocation(
        GpuAllocation allocation,
        SDL_GPURenderPass* renderPass)
    {
        if (allocation.CpuAllocation.Count == 0)
        {
            return;
        }

        SDL_GPUBufferBinding vertexBinding = new()
        {
            buffer = allocation.Page.FaceBuffer.Handle,
            offset = 0
        };

        SDL_BindGPUVertexBuffers(renderPass, 0, &vertexBinding, 1);
        
        SDL_GPUBuffer** buffers = stackalloc SDL_GPUBuffer*[1];
        buffers[0] = allocation.Page.SlotBuffer.Handle;
        
        SDL_BindGPUVertexStorageBuffers(renderPass, 0, buffers, 1);

        SDL_DrawGPUPrimitives(
            renderPass,
            num_vertices: 6,
            num_instances: allocation.CpuAllocation.Count,
            first_vertex: 0,
            first_instance: allocation.CpuAllocation.First
        );
        
        profiler.Rendering.TerrainDrawCalls++;
    }

    private bool disposed;

    public void Dispose()
    {
        if (disposed) return;
        
        opaqueArena.Dispose();
        transparentArena.Dispose();
        
        opaquePipeline.Dispose();
        
        transparentPipeline.Dispose();
        
        sampler.Dispose();
        
        disposed = true;
    }
}
