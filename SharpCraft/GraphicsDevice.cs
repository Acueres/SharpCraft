using SDL;
using System.Numerics;

using static SDL.SDL3;

namespace SharpCraft;

internal unsafe class GraphicsDevice : IDisposable
{
    public Shader VertexShader {  get; private set; }
    public Shader FragmentShader { get; private set; }

    public GpuDevice Device { get; private set; }
    public Window Window { get; private set; }

    public SDL_GPUBuffer* VertexBuffer { get; private set; }
    public SDL_GPUBuffer* IndexBuffer { get; private set; }

    private readonly SdlRuntime runtime;
    private readonly SDL_GPUGraphicsPipeline* pipeline;

    private SDL_GPUTexture* depthTexture;

    private uint width;
    private uint height;

    public GraphicsDevice(uint width, uint height, string title)
    {
        runtime = new SdlRuntime();

        this.width = width;
        this.height = height;

        Window = new Window(title, (int)width, (int)height);
        Device = new GpuDevice("vulkan", Window, debugInfo: true);

        byte[] vertBytes = File.ReadAllBytes(Path.Combine("Shaders", "cube.vert.spv"));
        byte[] fragBytes = File.ReadAllBytes(Path.Combine("Shaders", "cube.frag.spv"));

        VertexShader = new Shader(Device.Handle, vertBytes,
            SDL_GPUShaderStage.SDL_GPU_SHADERSTAGE_VERTEX, uniformBuffers: 1, "MainVS");
        FragmentShader = new Shader(Device.Handle, fragBytes,
            SDL_GPUShaderStage.SDL_GPU_SHADERSTAGE_FRAGMENT, uniformBuffers: 0, "MainFS");

        SDL_GPUVertexBufferDescription vertexBufferDescription = new()
        {
            slot = 0,
            pitch = (uint)sizeof(Vertex),
            input_rate = SDL_GPUVertexInputRate.SDL_GPU_VERTEXINPUTRATE_VERTEX,
            instance_step_rate = 0
        };

        SDL_GPUVertexAttribute positionAttribute = new()
        {
            location = 0,
            buffer_slot = 0,
            format = SDL_GPUVertexElementFormat.SDL_GPU_VERTEXELEMENTFORMAT_FLOAT3,
            offset = 0
        };

        SDL_GPUColorTargetDescription colorTargetDescription = new()
        {
            format = Device.SwapchainFormat,
            blend_state = new SDL_GPUColorTargetBlendState
            {
                color_write_mask =
                    SDL_GPUColorComponentFlags.SDL_GPU_COLORCOMPONENT_R |
                    SDL_GPUColorComponentFlags.SDL_GPU_COLORCOMPONENT_G |
                    SDL_GPUColorComponentFlags.SDL_GPU_COLORCOMPONENT_B |
                    SDL_GPUColorComponentFlags.SDL_GPU_COLORCOMPONENT_A
            }
        };

        SDL_GPUGraphicsPipelineCreateInfo pipelineInfo = new()
        {
            vertex_shader = VertexShader.Handle,
            fragment_shader = FragmentShader.Handle,

            primitive_type = SDL_GPUPrimitiveType.SDL_GPU_PRIMITIVETYPE_TRIANGLELIST,

            vertex_input_state = new SDL_GPUVertexInputState
            {
                vertex_buffer_descriptions = &vertexBufferDescription,
                num_vertex_buffers = 1,
                vertex_attributes = &positionAttribute,
                num_vertex_attributes = 1
            },

            rasterizer_state = new SDL_GPURasterizerState
            {
                fill_mode = SDL_GPUFillMode.SDL_GPU_FILLMODE_FILL,
                cull_mode = SDL_GPUCullMode.SDL_GPU_CULLMODE_BACK,
                front_face = SDL_GPUFrontFace.SDL_GPU_FRONTFACE_COUNTER_CLOCKWISE
            },

            multisample_state = new SDL_GPUMultisampleState
            {
                sample_count = SDL_GPUSampleCount.SDL_GPU_SAMPLECOUNT_1
            },

            depth_stencil_state = new SDL_GPUDepthStencilState
            {
                enable_depth_test = true,
                enable_depth_write = true,
                compare_op = SDL_GPUCompareOp.SDL_GPU_COMPAREOP_LESS
            },

            target_info = new SDL_GPUGraphicsPipelineTargetInfo
            {
                color_target_descriptions = &colorTargetDescription,
                num_color_targets = 1,

                has_depth_stencil_target = true,
                depth_stencil_format = SDL_GPUTextureFormat.SDL_GPU_TEXTUREFORMAT_D24_UNORM
            }
        };

        pipeline = SDL_CreateGPUGraphicsPipeline(Device.Handle, &pipelineInfo);
        if (pipeline == null)
        {
            SdlRuntime.Throw("Failed to create graphics pipeline");
        }

        SDL_GPUBufferCreateInfo vertexBufferInfo = new()
        {
            usage = SDL_GPUBufferUsageFlags.SDL_GPU_BUFFERUSAGE_VERTEX,
            size = (uint)(Cube.Vertices.Length * sizeof(Vertex))
        };

        SDL_GPUBufferCreateInfo indexBufferInfo = new()
        {
            usage = SDL_GPUBufferUsageFlags.SDL_GPU_BUFFERUSAGE_INDEX,
            size = (uint)(Cube.Indices.Length * sizeof(ushort))
        };

        VertexBuffer = SDL_CreateGPUBuffer(Device.Handle, &vertexBufferInfo);
        if (VertexBuffer == null)
        {
            SdlRuntime.Throw("Failed to create vertex buffer");
        }

        IndexBuffer = SDL_CreateGPUBuffer(Device.Handle, &indexBufferInfo);
        if (IndexBuffer == null)
        {
            SdlRuntime.Throw("Failed to create index buffer");
        }

        depthTexture = CreateDepthTexture(
            width,
            height
        );
    }

    public void Dispose()
    {
        Device.WaitIdle();

        SDL_ReleaseGPUGraphicsPipeline(Device.Handle, pipeline);

        VertexShader.Dispose();
        FragmentShader.Dispose();

        SDL_ReleaseGPUBuffer(Device.Handle, VertexBuffer);
        SDL_ReleaseGPUBuffer(Device.Handle, IndexBuffer);

        Device.ReleaseTexture(depthTexture);

        Device.Dispose();
        Window.Dispose();
        runtime.Dispose();
    }

    public void UploadMesh()
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

            vertexTransfer = SDL_CreateGPUTransferBuffer(Device.Handle, &vertexTransferInfo);
            indexTransfer = SDL_CreateGPUTransferBuffer(Device.Handle, &indexTransferInfo);

            if (vertexTransfer == null || indexTransfer == null)
            {
                SdlRuntime.Throw("Failed to create transfer buffer");
            }

            nint vertexDst = SDL_MapGPUTransferBuffer(Device.Handle, vertexTransfer, false);
            if (vertexDst == IntPtr.Zero)
            {
                SdlRuntime.Throw("Failed to map transfer vertex buffer");
            }

            fixed (Vertex* src = Cube.Vertices)
            {
                Buffer.MemoryCopy(src, (void*)vertexDst, vertexBytes, vertexBytes);
            }

            SDL_UnmapGPUTransferBuffer(Device.Handle, vertexTransfer);

            nint indexDst = SDL_MapGPUTransferBuffer(Device.Handle, indexTransfer, false);
            if (indexDst == IntPtr.Zero)
            {
                SdlRuntime.Throw("Failed to map transfer index buffer");
            }

            fixed (ushort* src = Cube.Indices)
                Buffer.MemoryCopy(src, (void*)indexDst, indexBytes, indexBytes);

            SDL_UnmapGPUTransferBuffer(Device.Handle, indexTransfer);

            SDL_GPUCommandBuffer* cmd = SDL_AcquireGPUCommandBuffer(Device.Handle);
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
                buffer = VertexBuffer,
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
                buffer = IndexBuffer,
                offset = 0,
                size = indexBytes
            };

            SDL_UploadToGPUBuffer(copyPass, &indexSource, &indexDestination, false);

            SDL_EndGPUCopyPass(copyPass);

            if (!SDL_SubmitGPUCommandBuffer(cmd))
            {
                SdlRuntime.Throw("Failed to upload GPU command buffer");
            }

            Device.WaitIdle();
        }
        finally
        {
            if (vertexTransfer != null)
            {
                SDL_ReleaseGPUTransferBuffer(Device.Handle, vertexTransfer);
            }

            if (indexTransfer != null)
            {
                SDL_ReleaseGPUTransferBuffer(Device.Handle, indexTransfer);
            }
        }
    }

    public void DrawFrame()
    {
        if (!TryCreateFrameContext(out var fCtx))
        {
            return;
        }

        EnsureDepthTextureSize(
            fCtx.Width,
            fCtx.Height
        );


        Matrix4x4 mvp = BuildMvp(fCtx.Width, fCtx.Height);

        SDL_PushGPUVertexUniformData(
            fCtx.CommandBuffer,
            0,
            (nint)(&mvp),
            (uint)sizeof(Matrix4x4)
        );

        SDL_GPUColorTargetInfo colorTarget = new()
        {
            texture = fCtx.SwapchainTexture,

            clear_color = new SDL_FColor
            {
                r = 0.08f,
                g = 0.12f,
                b = 0.22f,
                a = 1.0f
            },

            load_op = SDL_GPULoadOp.SDL_GPU_LOADOP_CLEAR,
            store_op = SDL_GPUStoreOp.SDL_GPU_STOREOP_STORE,
            cycle = false
        };

        SDL_GPUDepthStencilTargetInfo depthTarget = new()
        {
            texture = depthTexture,

            clear_depth = 1.0f,
            load_op = SDL_GPULoadOp.SDL_GPU_LOADOP_CLEAR,
            store_op = SDL_GPUStoreOp.SDL_GPU_STOREOP_DONT_CARE,

            stencil_load_op = SDL_GPULoadOp.SDL_GPU_LOADOP_DONT_CARE,
            stencil_store_op = SDL_GPUStoreOp.SDL_GPU_STOREOP_DONT_CARE,

            cycle = false
        };

        SDL_GPURenderPass* renderPass = SDL_BeginGPURenderPass(
            fCtx.CommandBuffer,
            &colorTarget,
            1,
            &depthTarget
        );

        SDL_BindGPUGraphicsPipeline(renderPass, pipeline);

        SDL_GPUBufferBinding vertexBinding = new()
        {
            buffer = VertexBuffer,
            offset = 0
        };

        SDL_BindGPUVertexBuffers(renderPass, 0, &vertexBinding, 1);

        SDL_GPUBufferBinding indexBinding = new()
        {
            buffer = IndexBuffer,
            offset = 0
        };

        SDL_BindGPUIndexBuffer(
            renderPass,
            &indexBinding,
            SDL_GPUIndexElementSize.SDL_GPU_INDEXELEMENTSIZE_16BIT
        );

        SDL_DrawGPUIndexedPrimitives(
            renderPass,
            num_indices: (uint)Cube.Indices.Length,
            num_instances: 1,
            first_index: 0,
            vertex_offset: 0,
            first_instance: 0
        );

        SDL_EndGPURenderPass(renderPass);

        if (!SDL_SubmitGPUCommandBuffer(fCtx.CommandBuffer))
        {
            SdlRuntime.Throw("Failed to submit frame command buffer");
        }
    }

    private bool TryCreateFrameContext(out FrameContext context)
    {
        context = default;

        SDL_GPUCommandBuffer* cmd = SDL_AcquireGPUCommandBuffer(Device.Handle);
        if (cmd == null)
        {
            SdlRuntime.Throw("Failed to acquire command buffer");
        }

        SDL_GPUTexture* swapchainTexture = null;
        uint swapchainWidth = 0;
        uint swapchainHeight = 0;

        if (!SDL_AcquireGPUSwapchainTexture(
                cmd,
                Window.Handle,
                &swapchainTexture,
                &swapchainWidth,
                &swapchainHeight))
        {
            SDL_SubmitGPUCommandBuffer(cmd);
            return false;
        }

        if (swapchainTexture == null)
        {
            SDL_SubmitGPUCommandBuffer(cmd);
            return false;
        }

        context = new FrameContext(cmd, swapchainTexture, swapchainWidth, swapchainHeight);

        return true;
    }

    private void EnsureDepthTextureSize(
        uint swapchainWidth,
        uint swapchainHeight)
    {
        if (swapchainWidth == 0 || swapchainHeight == 0)
            return;

        if (depthTexture != null &&
            width == swapchainWidth &&
            height == swapchainHeight)
        {
            return;
        }

        Device.WaitIdle();

        if (depthTexture != null)
        {
            Device.ReleaseTexture(depthTexture);
        }

        depthTexture = CreateDepthTexture(swapchainWidth, swapchainHeight);
        width = swapchainWidth;
        height = swapchainHeight;
    }

    private static Matrix4x4 BuildMvp(uint width, uint height)
    {
        float aspect = width / MathF.Max(1.0f, height);

        Matrix4x4 world =
            Matrix4x4.CreateRotationY(SDL_GetTicks() / 1000.0f * 0.7f) *
            Matrix4x4.CreateRotationX(-0.45f);

        Matrix4x4 view = Matrix4x4.CreateLookAt(
            new Vector3(0, 1.2f, 4.0f),
            Vector3.Zero,
            Vector3.UnitY
        );

        Matrix4x4 projection = Matrix4x4.CreatePerspectiveFieldOfView(
            MathF.PI / 4.0f,
            aspect,
            0.1f,
            100.0f
        );

        // Vulkan-style framebuffer orientation correction
        projection.M22 *= -1.0f;

        return world * view * projection;
    }

    private SDL_GPUTexture* CreateDepthTexture(
        uint width,
        uint height)
    {
        SDL_GPUTextureCreateInfo depthTextureInfo = new()
        {
            type = SDL_GPUTextureType.SDL_GPU_TEXTURETYPE_2D,
            format = SDL_GPUTextureFormat.SDL_GPU_TEXTUREFORMAT_D24_UNORM,
            usage = SDL_GPUTextureUsageFlags.SDL_GPU_TEXTUREUSAGE_DEPTH_STENCIL_TARGET,
            width = width,
            height = height,
            layer_count_or_depth = 1,
            num_levels = 1,
            sample_count = SDL_GPUSampleCount.SDL_GPU_SAMPLECOUNT_1
        };

        SDL_GPUTexture* depthTexture = SDL_CreateGPUTexture(Device.Handle, &depthTextureInfo);
        if (depthTexture == null)
        {
            SdlRuntime.Throw("Failed to create depth texture");
        }

        return depthTexture;
    }
}
