using SDL;

using SharpCraft.Graphics.Resources;
using SharpCraft.Platform;
using SharpCraft.Rendering;

using static SDL.SDL3;

namespace SharpCraft.Graphics;

internal unsafe class GpuUploader(GpuDevice device)
{
    public void Upload(BlockFaceBuffer blockFaceBuffer, BlockFace[] data)
    {
        SDL_GPUTransferBuffer* vertexTransfer = null;
        
        try
        {
            uint faceCount = (uint)data.Length;
            
            blockFaceBuffer.EnsureSize(faceCount);
            
            if (faceCount == 0)
            {
                return;
            }
            
            uint vertexBytes = blockFaceBuffer.BytesCount;

            SDL_GPUTransferBufferCreateInfo vertexTransferInfo = new()
            {
                usage = SDL_GPUTransferBufferUsage.SDL_GPU_TRANSFERBUFFERUSAGE_UPLOAD,
                size = vertexBytes
            };

            vertexTransfer = SDL_CreateGPUTransferBuffer(device.Handle, &vertexTransferInfo);
            
            if (vertexTransfer == null)
            {
                SdlRuntime.Throw("Failed to create transfer buffer");
            }

            nint vertexDst = SDL_MapGPUTransferBuffer(device.Handle, vertexTransfer, false);
            if (vertexDst == IntPtr.Zero)
            {
                SdlRuntime.Throw("Failed to map transfer vertex buffer");
            }

            fixed (BlockFace* src = data)
            {
                Buffer.MemoryCopy(src, (void*)vertexDst, vertexBytes, vertexBytes);
            }

            SDL_UnmapGPUTransferBuffer(device.Handle, vertexTransfer);

            SDL_GPUCommandBuffer* cmd = SDL_AcquireGPUCommandBuffer(device.Handle);
            if (cmd == null)
            {
                SdlRuntime.Throw("Failed to acquire GPU command buffer");
            }

            SDL_GPUCopyPass* copyPass = SDL_BeginGPUCopyPass(cmd);

            SDL_GPUTransferBufferLocation vertexSource = new()
            {
                transfer_buffer = vertexTransfer,
                offset = 0
            };

            SDL_GPUBufferRegion vertexDestination = new()
            {
                buffer = blockFaceBuffer.Handle,
                offset = 0,
                size = vertexBytes
            };

            SDL_UploadToGPUBuffer(copyPass, &vertexSource, &vertexDestination, true);

            SDL_EndGPUCopyPass(copyPass);

            if (!SDL_SubmitGPUCommandBuffer(cmd))
            {
                SdlRuntime.Throw("Failed to upload GPU command buffer");
            }
        }
        finally
        {
            if (vertexTransfer != null)
            {
                SDL_ReleaseGPUTransferBuffer(device.Handle, vertexTransfer);
            }
        }
    }

    public void Upload(SpriteBuffer spriteBuffer, SpriteVertex[] vertices, uint[] indices)
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

            if (!SDL_SubmitGPUCommandBuffer(commandBuffer))
            {
                SdlRuntime.Throw("Failed to submit sprite upload command buffer");
            }
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

            if (!SDL_SubmitGPUCommandBuffer(commandBuffer))
            {
                SdlRuntime.Throw("Failed to submit texture upload command buffer");
            }
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

            if (!SDL_SubmitGPUCommandBuffer(commandBuffer))
            {
                SdlRuntime.Throw("Failed to submit texture upload command buffer");
            }

            device.WaitIdle();
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
