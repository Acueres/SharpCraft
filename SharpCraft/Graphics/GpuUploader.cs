using SharpCraft.Graphics.Resources;
using SharpCraft.Platform;
using SharpCraft.Rendering;
using SharpCraft.Diagnostics;

using SDL;
using System.Diagnostics;

using static SDL.SDL3;

namespace SharpCraft.Graphics;

internal unsafe class GpuUploader(GpuDevice device, SubmissionTracker submissionTracker, FrameProfiler profiler)
{
    public void Upload(BlockFaceBuffer blockFaceBuffer, ReadOnlySpan<VoxelFace> data, uint first)
    {
        SDL_GPUTransferBuffer* vertexTransfer = null;
        
        try
        {
            long stagingStarted = Stopwatch.GetTimestamp();
            uint faceCount = (uint)data.Length;
            
            if (faceCount == 0)
            {
                return;
            }
            
            uint bytes = (uint)sizeof(VoxelFace);
            uint facesBytes = checked((uint)data.Length * bytes);

            SDL_GPUTransferBufferCreateInfo vertexTransferInfo = new()
            {
                usage = SDL_GPUTransferBufferUsage.SDL_GPU_TRANSFERBUFFERUSAGE_UPLOAD,
                size = facesBytes
            };

            vertexTransfer = SDL_CreateGPUTransferBuffer(device.Handle, &vertexTransferInfo);
            
            if (vertexTransfer == null)
            {
                SdlRuntime.Throw("Failed to create face transfer buffer");
            }

            nint vertexDst = SDL_MapGPUTransferBuffer(device.Handle, vertexTransfer, false);
            if (vertexDst == IntPtr.Zero)
            {
                SdlRuntime.Throw("Failed to map transfer vertex buffer");
            }

            fixed (VoxelFace* src = data)
            {
                Buffer.MemoryCopy(src, (void*)vertexDst, facesBytes, facesBytes);
            }

            SDL_UnmapGPUTransferBuffer(device.Handle, vertexTransfer);
            profiler.Timings.UploadStagingMilliseconds += Stopwatch.GetElapsedTime(stagingStarted).TotalMilliseconds;
            long recordingStarted = Stopwatch.GetTimestamp();

            SDL_GPUCommandBuffer* commandBuffer = SDL_AcquireGPUCommandBuffer(device.Handle);
            if (commandBuffer == null)
            {
                SdlRuntime.Throw("Failed to acquire GPU command buffer");
            }

            SDL_GPUCopyPass* copyPass = SDL_BeginGPUCopyPass(commandBuffer);

            SDL_GPUTransferBufferLocation vertexSource = new()
            {
                transfer_buffer = vertexTransfer,
                offset = 0
            };

            SDL_GPUBufferRegion vertexDestination = new()
            {
                buffer = blockFaceBuffer.Handle,
                offset = first * bytes,
                size = facesBytes
            };

            SDL_UploadToGPUBuffer(copyPass, &vertexSource, &vertexDestination, false);

            SDL_EndGPUCopyPass(copyPass);
            profiler.Timings.UploadCommandsMilliseconds += Stopwatch.GetElapsedTime(recordingStarted).TotalMilliseconds;
            long submitStarted = Stopwatch.GetTimestamp();

            submissionTracker.Submit(commandBuffer);
            profiler.Timings.SubmitMilliseconds += Stopwatch.GetElapsedTime(submitStarted).TotalMilliseconds;
            profiler.Rendering.UploadJobs++;
            
            profiler.Rendering.TerrainUploadBytes += facesBytes;
        }
        finally
        {
            if (vertexTransfer != null)
            {
                SDL_ReleaseGPUTransferBuffer(device.Handle, vertexTransfer);
            }
        }
    }
    
    public void Upload(SlotRecordBuffer slotRecordBuffer, SlotRecord slotRecord, uint slotId)
    {
        SDL_GPUTransferBuffer* transfer = null;
        
        try
        {
            long stagingStarted = Stopwatch.GetTimestamp();
            
            uint bytes = (uint)sizeof(SlotRecord);

            SDL_GPUTransferBufferCreateInfo transferInfo = new()
            {
                usage = SDL_GPUTransferBufferUsage.SDL_GPU_TRANSFERBUFFERUSAGE_UPLOAD,
                size = bytes
            };

            transfer = SDL_CreateGPUTransferBuffer(device.Handle, &transferInfo);
            
            if (transfer == null)
            {
                SdlRuntime.Throw("Failed to create slot transfer buffer");
            }

            nint vertexDst = SDL_MapGPUTransferBuffer(device.Handle, transfer, false);
            if (vertexDst == IntPtr.Zero)
            {
                SdlRuntime.Throw("Failed to map slot transfer buffer");
            }
            
            Buffer.MemoryCopy(&slotRecord, (void*)vertexDst, bytes, bytes);

            SDL_UnmapGPUTransferBuffer(device.Handle, transfer);
            profiler.Timings.UploadStagingMilliseconds += Stopwatch.GetElapsedTime(stagingStarted).TotalMilliseconds;
            long recordingStarted = Stopwatch.GetTimestamp();

            SDL_GPUCommandBuffer* cmd = SDL_AcquireGPUCommandBuffer(device.Handle);
            if (cmd == null)
            {
                SdlRuntime.Throw("Failed to acquire GPU command buffer");
            }

            SDL_GPUCopyPass* copyPass = SDL_BeginGPUCopyPass(cmd);

            SDL_GPUTransferBufferLocation vertexSource = new()
            {
                transfer_buffer = transfer,
                offset = 0
            };

            SDL_GPUBufferRegion vertexDestination = new()
            {
                buffer = slotRecordBuffer.Handle,
                offset = slotId * bytes,
                size = bytes
            };

            SDL_UploadToGPUBuffer(copyPass, &vertexSource, &vertexDestination, false);

            SDL_EndGPUCopyPass(copyPass);
            profiler.Timings.UploadCommandsMilliseconds += Stopwatch.GetElapsedTime(recordingStarted).TotalMilliseconds;
            long submitStarted = Stopwatch.GetTimestamp();

            submissionTracker.Submit(cmd);
            profiler.Timings.SubmitMilliseconds += Stopwatch.GetElapsedTime(submitStarted).TotalMilliseconds;
            profiler.Rendering.UploadJobs++;
            
            profiler.Rendering.TerrainUploadBytes += bytes;
        }
        finally
        {
            if (transfer != null)
            {
                SDL_ReleaseGPUTransferBuffer(device.Handle, transfer);
            }
        }
    }

    public void Upload(SpriteBuffer spriteBuffer, ReadOnlySpan<SpriteVertex> vertices, ReadOnlySpan<uint> indices)
    {
        if (vertices.Length % 4 != 0)
        {
            throw new ArgumentException(
                $"Sprite vertex count must be divisible by 4. Got {vertices.Length}.",
                nameof(vertices)
            );
        }

        uint spriteCount = (uint)(vertices.Length / 4);
        uint expectedIndexCount = spriteCount * 6;

        if ((uint)indices.Length != expectedIndexCount)
        {
            throw new ArgumentException(
                $"Sprite index count mismatch. Expected {expectedIndexCount}, got {indices.Length}.",
                nameof(indices)
            );
        }

        spriteBuffer.EnsureSize(spriteCount);

        if (spriteCount == 0)
        {
            return;
        }

        uint vertexBytes = spriteBuffer.VertexBytesCount;
        uint indexBytes = spriteBuffer.IndexBytesCount;

        SDL_GPUTransferBuffer* vertexTransfer = null;
        SDL_GPUTransferBuffer* indexTransfer = null;

        try
        {
            long stagingStarted = Stopwatch.GetTimestamp();
            SDL_GPUTransferBufferCreateInfo vertexTransferInfo = new()
            {
                usage = SDL_GPUTransferBufferUsage.SDL_GPU_TRANSFERBUFFERUSAGE_UPLOAD,
                size = vertexBytes
            };

            vertexTransfer = SDL_CreateGPUTransferBuffer(device.Handle, &vertexTransferInfo);
            if (vertexTransfer == null)
            {
                SdlRuntime.Throw("Failed to create sprite vertex transfer buffer");
            }

            nint vertexDestination = SDL_MapGPUTransferBuffer(device.Handle, vertexTransfer, false);
            if (vertexDestination == IntPtr.Zero)
            {
                SdlRuntime.Throw("Failed to map sprite vertex transfer buffer");
            }

            fixed (SpriteVertex* source = vertices)
            {
                Buffer.MemoryCopy(
                    source,
                    (void*)vertexDestination,
                    vertexBytes,
                    vertexBytes
                );
            }

            SDL_UnmapGPUTransferBuffer(device.Handle, vertexTransfer);

            SDL_GPUTransferBufferCreateInfo indexTransferInfo = new()
            {
                usage = SDL_GPUTransferBufferUsage.SDL_GPU_TRANSFERBUFFERUSAGE_UPLOAD,
                size = indexBytes
            };

            indexTransfer = SDL_CreateGPUTransferBuffer(device.Handle, &indexTransferInfo);
            if (indexTransfer == null)
            {
                SdlRuntime.Throw("Failed to create sprite index transfer buffer");
            }

            nint indexDestination = SDL_MapGPUTransferBuffer(device.Handle, indexTransfer, false);
            if (indexDestination == IntPtr.Zero)
            {
                SdlRuntime.Throw("Failed to map sprite index transfer buffer");
            }

            fixed (uint* source = indices)
            {
                Buffer.MemoryCopy(
                    source,
                    (void*)indexDestination,
                    indexBytes,
                    indexBytes
                );
            }

            SDL_UnmapGPUTransferBuffer(device.Handle, indexTransfer);
            profiler.Timings.UploadStagingMilliseconds += Stopwatch.GetElapsedTime(stagingStarted).TotalMilliseconds;
            long recordingStarted = Stopwatch.GetTimestamp();

            SDL_GPUCommandBuffer* commandBuffer =
                SDL_AcquireGPUCommandBuffer(device.Handle);

            if (commandBuffer == null)
            {
                SdlRuntime.Throw("Failed to acquire sprite upload command buffer");
            }

            SDL_GPUCopyPass* copyPass =
                SDL_BeginGPUCopyPass(commandBuffer);

            SDL_GPUTransferBufferLocation vertexSource = new()
            {
                transfer_buffer = vertexTransfer,
                offset = 0
            };

            SDL_GPUBufferRegion vertexBufferRegion = new()
            {
                buffer = spriteBuffer.VertexHandle,
                offset = 0,
                size = vertexBytes
            };

            SDL_UploadToGPUBuffer(
                copyPass,
                &vertexSource,
                &vertexBufferRegion,
                true
            );

            SDL_GPUTransferBufferLocation indexSource = new()
            {
                transfer_buffer = indexTransfer,
                offset = 0
            };

            SDL_GPUBufferRegion indexBufferRegion = new()
            {
                buffer = spriteBuffer.IndexHandle,
                offset = 0,
                size = indexBytes
            };

            SDL_UploadToGPUBuffer(
                copyPass,
                &indexSource,
                &indexBufferRegion,
                true
            );

            SDL_EndGPUCopyPass(copyPass);
            profiler.Timings.UploadCommandsMilliseconds += Stopwatch.GetElapsedTime(recordingStarted).TotalMilliseconds;
            long submitStarted = Stopwatch.GetTimestamp();

            submissionTracker.Submit(commandBuffer);
            profiler.Timings.SubmitMilliseconds += Stopwatch.GetElapsedTime(submitStarted).TotalMilliseconds;
            profiler.Rendering.UploadJobs++;
            
            profiler.Rendering.UiUploadBytes += (long)vertexBytes + indexBytes;
        }
        finally
        {
            if (vertexTransfer != null)
            {
                SDL_ReleaseGPUTransferBuffer(device.Handle, vertexTransfer);
            }

            if (indexTransfer != null)
            {
                SDL_ReleaseGPUTransferBuffer(device.Handle, indexTransfer);
            }
        }
    }

    public void Upload(TextureArray textureArray)
    {
        uint bytesPerLayer = textureArray.Width * textureArray.Height * 4;
        uint expectedByteCount = textureArray.Width * textureArray.Height * 4 * textureArray.LayerCount;

        if ((uint)textureArray.Data.Length != expectedByteCount)
        {
            throw new ArgumentException(
                $"Texture upload byte count mismatch. " +
                $"Expected {expectedByteCount}, got {textureArray.Data.Length}."
            );
        }

        SDL_GPUTransferBuffer* transferBuffer = null;

        try
        {
            long stagingStarted = Stopwatch.GetTimestamp();
            SDL_GPUTransferBufferCreateInfo transferInfo = new()
            {
                usage = SDL_GPUTransferBufferUsage.SDL_GPU_TRANSFERBUFFERUSAGE_UPLOAD,
                size = expectedByteCount
            };

            transferBuffer = SDL_CreateGPUTransferBuffer(device.Handle, &transferInfo);
            if (transferBuffer == null)
            {
                SdlRuntime.Throw("Failed to create texture transfer buffer");
            }

            nint destination = SDL_MapGPUTransferBuffer(device.Handle, transferBuffer, false);
            if (destination == IntPtr.Zero)
            {
                SdlRuntime.Throw("Failed to map texture transfer buffer");
            }

            fixed (byte* source = textureArray.Data)
            {
                Buffer.MemoryCopy(
                    source,
                    (void*)destination,
                    expectedByteCount,
                    expectedByteCount
                );
            }

            SDL_UnmapGPUTransferBuffer(device.Handle, transferBuffer);
            profiler.Timings.UploadStagingMilliseconds += Stopwatch.GetElapsedTime(stagingStarted).TotalMilliseconds;
            long recordingStarted = Stopwatch.GetTimestamp();

            SDL_GPUCommandBuffer* commandBuffer = SDL_AcquireGPUCommandBuffer(device.Handle);
            if (commandBuffer == null)
            {
                SdlRuntime.Throw("Failed to acquire texture upload command buffer");
            }

            SDL_GPUCopyPass* copyPass = SDL_BeginGPUCopyPass(commandBuffer);

            for (uint layer = 0; layer < textureArray.LayerCount; layer++)
            {
                SDL_GPUTextureTransferInfo sourceInfo = new()
                {
                    transfer_buffer = transferBuffer,
                    offset = layer * bytesPerLayer,
                    pixels_per_row = textureArray.Width,
                    rows_per_layer = textureArray.Height
                };

                SDL_GPUTextureRegion destinationRegion = new()
                {
                    texture = textureArray.Handle,
                    mip_level = 0,
                    layer = layer,

                    x = 0,
                    y = 0,
                    z = 0,

                    w = textureArray.Width,
                    h = textureArray.Height,
                    d = 1
                };

                SDL_UploadToGPUTexture(
                    copyPass,
                    &sourceInfo,
                    &destinationRegion,
                    false
                );
            }

            SDL_EndGPUCopyPass(copyPass);
            profiler.Timings.UploadCommandsMilliseconds += Stopwatch.GetElapsedTime(recordingStarted).TotalMilliseconds;
            long submitStarted = Stopwatch.GetTimestamp();

            submissionTracker.Submit(commandBuffer);
            profiler.Timings.SubmitMilliseconds += Stopwatch.GetElapsedTime(submitStarted).TotalMilliseconds;
            profiler.Rendering.UploadJobs++;
            profiler.Rendering.UiUploadBytes += expectedByteCount;
        }
        finally
        {
            if (transferBuffer != null)
            {
                SDL_ReleaseGPUTransferBuffer(device.Handle, transferBuffer);
            }
        }
    }

    public void Upload(Texture texture)
    {
        uint expectedByteCount =
            texture.Width *
            texture.Height *
            4;

        if ((uint)texture.Data.Length != expectedByteCount)
        {
            throw new ArgumentException(
                $"Texture upload byte count mismatch. " +
                $"Expected {expectedByteCount}, got {texture.Data.Length}."
            );
        }

        SDL_GPUTransferBuffer* transferBuffer = null;

        try
        {
            long stagingStarted = Stopwatch.GetTimestamp();
            SDL_GPUTransferBufferCreateInfo transferInfo = new()
            {
                usage = SDL_GPUTransferBufferUsage.SDL_GPU_TRANSFERBUFFERUSAGE_UPLOAD,
                size = expectedByteCount
            };

            transferBuffer = SDL_CreateGPUTransferBuffer(device.Handle, &transferInfo);
            if (transferBuffer == null)
            {
                SdlRuntime.Throw("Failed to create texture transfer buffer");
            }

            nint destination = SDL_MapGPUTransferBuffer(
                device.Handle,
                transferBuffer,
                false
            );

            if (destination == IntPtr.Zero)
            {
                SdlRuntime.Throw("Failed to map texture transfer buffer");
            }

            fixed (byte* source = texture.Data)
            {
                Buffer.MemoryCopy(
                    source,
                    (void*)destination,
                    expectedByteCount,
                    expectedByteCount
                );
            }

            SDL_UnmapGPUTransferBuffer(device.Handle, transferBuffer);
            profiler.Timings.UploadStagingMilliseconds += Stopwatch.GetElapsedTime(stagingStarted).TotalMilliseconds;
            long recordingStarted = Stopwatch.GetTimestamp();

            SDL_GPUCommandBuffer* commandBuffer =
                SDL_AcquireGPUCommandBuffer(device.Handle);

            if (commandBuffer == null)
            {
                SdlRuntime.Throw("Failed to acquire texture upload command buffer");
            }

            SDL_GPUCopyPass* copyPass =
                SDL_BeginGPUCopyPass(commandBuffer);

            SDL_GPUTextureTransferInfo sourceInfo = new()
            {
                transfer_buffer = transferBuffer,
                offset = 0,
                pixels_per_row = texture.Width,
                rows_per_layer = texture.Height
            };

            SDL_GPUTextureRegion destinationRegion = new()
            {
                texture = texture.Handle,
                mip_level = 0,
                layer = 0,

                x = 0,
                y = 0,
                z = 0,

                w = texture.Width,
                h = texture.Height,
                d = 1
            };

            SDL_UploadToGPUTexture(
                copyPass,
                &sourceInfo,
                &destinationRegion,
                false
            );

            SDL_EndGPUCopyPass(copyPass);
            profiler.Timings.UploadCommandsMilliseconds += Stopwatch.GetElapsedTime(recordingStarted).TotalMilliseconds;
            long submitStarted = Stopwatch.GetTimestamp();

            submissionTracker.Submit(commandBuffer);
            profiler.Timings.SubmitMilliseconds += Stopwatch.GetElapsedTime(submitStarted).TotalMilliseconds;
            profiler.Rendering.UploadJobs++;

            device.WaitIdle();
            
            profiler.Rendering.UiUploadBytes += expectedByteCount;
        }
        finally
        {
            if (transferBuffer != null)
            {
                SDL_ReleaseGPUTransferBuffer(device.Handle, transferBuffer);
            }
        }
    }
}
