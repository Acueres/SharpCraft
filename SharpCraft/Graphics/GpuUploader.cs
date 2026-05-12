using SDL;

using SharpCraft.Graphics.Resources;
using SharpCraft.Platform;
using SharpCraft.Rendering;

using static SDL.SDL3;

namespace SharpCraft.Graphics;

internal unsafe class GpuUploader(GpuDevice device)
{
    public void Upload(VertexBuffer vertexBuffer, IndexBuffer indexBuffer)
    {
        SDL_GPUTransferBuffer* vertexTransfer = null;
        SDL_GPUTransferBuffer* indexTransfer = null;

        try
        {
            uint vertexBytes = (uint)(Cube.Vertices.Length * sizeof(Vertex));
            uint indexBytes = (uint)(Cube.Indices.Length * sizeof(ushort));

            SDL_GPUTransferBufferCreateInfo vertexTransferInfo = new()
            {
                usage = SDL_GPUTransferBufferUsage.SDL_GPU_TRANSFERBUFFERUSAGE_UPLOAD,
                size = vertexBytes
            };

            SDL_GPUTransferBufferCreateInfo indexTransferInfo = new()
            {
                usage = SDL_GPUTransferBufferUsage.SDL_GPU_TRANSFERBUFFERUSAGE_UPLOAD,
                size = indexBytes
            };

            vertexTransfer = SDL_CreateGPUTransferBuffer(device.Handle, &vertexTransferInfo);
            indexTransfer = SDL_CreateGPUTransferBuffer(device.Handle, &indexTransferInfo);

            if (vertexTransfer == null || indexTransfer == null)
            {
                SdlRuntime.Throw("Failed to create transfer buffer");
            }

            nint vertexDst = SDL_MapGPUTransferBuffer(device.Handle, vertexTransfer, false);
            if (vertexDst == IntPtr.Zero)
            {
                SdlRuntime.Throw("Failed to map transfer vertex buffer");
            }

            fixed (Vertex* src = Cube.Vertices)
            {
                Buffer.MemoryCopy(src, (void*)vertexDst, vertexBytes, vertexBytes);
            }

            SDL_UnmapGPUTransferBuffer(device.Handle, vertexTransfer);

            nint indexDst = SDL_MapGPUTransferBuffer(device.Handle, indexTransfer, false);
            if (indexDst == IntPtr.Zero)
            {
                SdlRuntime.Throw("Failed to map transfer index buffer");
            }

            fixed (ushort* src = Cube.Indices)
                Buffer.MemoryCopy(src, (void*)indexDst, indexBytes, indexBytes);

            SDL_UnmapGPUTransferBuffer(device.Handle, indexTransfer);

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
                buffer = vertexBuffer.Handle,
                offset = 0,
                size = vertexBytes
            };

            SDL_UploadToGPUBuffer(copyPass, &vertexSource, &vertexDestination, false);

            SDL_GPUTransferBufferLocation indexSource = new()
            {
                transfer_buffer = indexTransfer,
                offset = 0
            };

            SDL_GPUBufferRegion indexDestination = new()
            {
                buffer = indexBuffer.Handle,
                offset = 0,
                size = indexBytes
            };

            SDL_UploadToGPUBuffer(copyPass, &indexSource, &indexDestination, false);

            SDL_EndGPUCopyPass(copyPass);

            if (!SDL_SubmitGPUCommandBuffer(cmd))
            {
                SdlRuntime.Throw("Failed to upload GPU command buffer");
            }

            device.WaitIdle();
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
}
