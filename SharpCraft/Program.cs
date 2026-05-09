using System.Numerics;
using System.Runtime.InteropServices;
using SDL;

using static SDL.SDL3;

namespace SharpCraft;

internal class Program
{
    const int width = 1280;
    const int height = 720;

    internal static void Main()
    {
        unsafe
        {
            if (!SDL_Init(SDL_InitFlags.SDL_INIT_VIDEO))
                Die("SDL_Init failed");

            SDL_Window* window;
            fixed (byte* title = "SDL3 GPU Cube"u8)
            {
                window = SDL_CreateWindow(
                    title,
                    width,
                    height,
                    SDL_WindowFlags.SDL_WINDOW_RESIZABLE
                );
            }

            if (window == null)
                Die("SDL_CreateWindow failed");

            SDL_GPUDevice* device;

            // Force SPIR-V path. Passing "vulkan" asks SDL_gpu for the Vulkan backend.
            // You can pass null instead if you want SDL to pick.
            fixed (byte* driverName = "vulkan"u8)
            {
                device = SDL_CreateGPUDevice(
                    SDL_GPUShaderFormat.SDL_GPU_SHADERFORMAT_SPIRV,
                    false,
                    driverName
                );
            }

            if (device == null)
                Die("SDL_CreateGPUDevice failed");

            if (!SDL_ClaimWindowForGPUDevice(device, window))
                Die("SDL_ClaimWindowForGPUDevice failed");

            Console.WriteLine($"SDL_gpu driver: {SDL_GetGPUDeviceDriver(device)}");

            SDL_GPUTextureFormat swapchainFormat = SDL_GetGPUSwapchainTextureFormat(device, window);

            CubeResources resources = CreateCubeResources(device, swapchainFormat);

            UploadCubeMesh(device, resources);

            bool running = true;

            while (running)
            {
                SDL_Event e;

                while (SDL_PollEvent(&e))
                {
                    if (e.type == (uint)SDL_EventType.SDL_EVENT_QUIT)
                        running = false;

                    if (e.type == (uint)SDL_EventType.SDL_EVENT_KEY_DOWN &&
                        e.key.key == SDL_Keycode.SDLK_ESCAPE)
                    {
                        running = false;
                    }
                }

                DrawFrame(device, window, resources);
                SDL_Delay(1);
            }

            SDL_WaitForGPUIdle(device);

            SDL_ReleaseGPUGraphicsPipeline(device, resources.Pipeline);
            SDL_ReleaseGPUShader(device, resources.VertexShader);
            SDL_ReleaseGPUShader(device, resources.FragmentShader);
            SDL_ReleaseGPUBuffer(device, resources.VertexBuffer);
            SDL_ReleaseGPUBuffer(device, resources.IndexBuffer);

            SDL_ReleaseWindowFromGPUDevice(device, window);
            SDL_DestroyGPUDevice(device);
            SDL_DestroyWindow(window);
            SDL_Quit();
        }
    }

    static unsafe CubeResources CreateCubeResources(SDL_GPUDevice* device, SDL_GPUTextureFormat swapchainFormat)
    {
        byte[] vertBytes = File.ReadAllBytes(Path.Combine("Shaders", "cube.vert.spv"));
        byte[] fragBytes = File.ReadAllBytes(Path.Combine("Shaders", "cube.frag.spv"));

        SDL_GPUShader* vertexShader = CreateShader(
            device,
            vertBytes,
            SDL_GPUShaderStage.SDL_GPU_SHADERSTAGE_VERTEX,
            uniformBuffers: 1,
            "MainVS"
        );

        SDL_GPUShader* fragmentShader = CreateShader(
            device,
            fragBytes,
            SDL_GPUShaderStage.SDL_GPU_SHADERSTAGE_FRAGMENT,
            uniformBuffers: 0,
            "MainFS"
        );

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
            format = swapchainFormat,
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
            vertex_shader = vertexShader,
            fragment_shader = fragmentShader,

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
                enable_depth_test = false,
                enable_depth_write = false
            },

            target_info = new SDL_GPUGraphicsPipelineTargetInfo
            {
                color_target_descriptions = &colorTargetDescription,
                num_color_targets = 1,
                has_depth_stencil_target = false
            }
        };

        SDL_GPUGraphicsPipeline* pipeline = SDL_CreateGPUGraphicsPipeline(device, &pipelineInfo);
        if (pipeline == null)
            Die("SDL_CreateGPUGraphicsPipeline failed");

        SDL_GPUBufferCreateInfo vertexBufferInfo = new()
        {
            usage = SDL_GPUBufferUsageFlags.SDL_GPU_BUFFERUSAGE_VERTEX,
            size = (uint)(CubeVertices.Length * sizeof(Vertex))
        };

        SDL_GPUBufferCreateInfo indexBufferInfo = new()
        {
            usage = SDL_GPUBufferUsageFlags.SDL_GPU_BUFFERUSAGE_INDEX,
            size = (uint)(CubeIndices.Length * sizeof(ushort))
        };

        SDL_GPUBuffer* vertexBuffer = SDL_CreateGPUBuffer(device, &vertexBufferInfo);
        if (vertexBuffer == null)
            Die("SDL_CreateGPUBuffer vertex failed");

        SDL_GPUBuffer* indexBuffer = SDL_CreateGPUBuffer(device, &indexBufferInfo);
        if (indexBuffer == null)
            Die("SDL_CreateGPUBuffer index failed");

        return new CubeResources
        {
            VertexShader = vertexShader,
            FragmentShader = fragmentShader,
            Pipeline = pipeline,
            VertexBuffer = vertexBuffer,
            IndexBuffer = indexBuffer
        };
    }

    static unsafe SDL_GPUShader* CreateShader(
    SDL_GPUDevice* device,
    byte[] code,
    SDL_GPUShaderStage stage,
    uint uniformBuffers,
    string entrypoint)
    {
        fixed (byte* codePtr = code)
        fixed (byte* entryPtr = Utf8Bytes(entrypoint))
        {
            SDL_GPUShaderCreateInfo info = new()
            {
                code = codePtr,
                code_size = (nuint)code.Length,
                entrypoint = entryPtr,
                format = SDL_GPUShaderFormat.SDL_GPU_SHADERFORMAT_SPIRV,
                stage = stage,

                num_samplers = 0,
                num_storage_textures = 0,
                num_storage_buffers = 0,
                num_uniform_buffers = uniformBuffers
            };

            SDL_GPUShader* shader = SDL_CreateGPUShader(device, &info);
            if (shader == null)
                Die($"SDL_CreateGPUShader failed: {entrypoint}");

            return shader;
        }
    }

    static unsafe void UploadCubeMesh(SDL_GPUDevice* device, CubeResources resources)
    {
        uint vertexBytes = (uint)(CubeVertices.Length * sizeof(Vertex));
        uint indexBytes = (uint)(CubeIndices.Length * sizeof(ushort));

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

        SDL_GPUTransferBuffer* vertexTransfer = SDL_CreateGPUTransferBuffer(device, &vertexTransferInfo);
        SDL_GPUTransferBuffer* indexTransfer = SDL_CreateGPUTransferBuffer(device, &indexTransferInfo);

        if (vertexTransfer == null || indexTransfer == null)
            Die("SDL_CreateGPUTransferBuffer failed");

        nint vertexDst = SDL_MapGPUTransferBuffer(device, vertexTransfer, false);
        if (vertexDst == IntPtr.Zero)
            Die("SDL_MapGPUTransferBuffer vertex failed");

        fixed (Vertex* src = CubeVertices)
            Buffer.MemoryCopy(src, (void*)vertexDst, vertexBytes, vertexBytes);

        SDL_UnmapGPUTransferBuffer(device, vertexTransfer);

        nint indexDst = SDL_MapGPUTransferBuffer(device, indexTransfer, false);
        if (indexDst == null)
            Die("SDL_MapGPUTransferBuffer index failed");

        fixed (ushort* src = CubeIndices)
            Buffer.MemoryCopy(src, (void*)indexDst, indexBytes, indexBytes);

        SDL_UnmapGPUTransferBuffer(device, indexTransfer);

        SDL_GPUCommandBuffer* cmd = SDL_AcquireGPUCommandBuffer(device);
        if (cmd == null)
            Die("SDL_AcquireGPUCommandBuffer failed");

        SDL_GPUCopyPass* copyPass = SDL_BeginGPUCopyPass(cmd);

        SDL_GPUTransferBufferLocation vertexSource = new()
        {
            transfer_buffer = vertexTransfer,
            offset = 0
        };

        SDL_GPUBufferRegion vertexDestination = new()
        {
            buffer = resources.VertexBuffer,
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
            buffer = resources.IndexBuffer,
            offset = 0,
            size = indexBytes
        };

        SDL_UploadToGPUBuffer(copyPass, &indexSource, &indexDestination, false);

        SDL_EndGPUCopyPass(copyPass);

        if (!SDL_SubmitGPUCommandBuffer(cmd))
            Die("SDL_SubmitGPUCommandBuffer upload failed");

        SDL_WaitForGPUIdle(device);

        SDL_ReleaseGPUTransferBuffer(device, vertexTransfer);
        SDL_ReleaseGPUTransferBuffer(device, indexTransfer);
    }

    static unsafe void DrawFrame(SDL_GPUDevice* device, SDL_Window* window, CubeResources resources)
    {
        SDL_GPUCommandBuffer* cmd = SDL_AcquireGPUCommandBuffer(device);
        if (cmd == null)
            Die("SDL_AcquireGPUCommandBuffer failed");

        SDL_GPUTexture* swapchainTexture = null;
        uint swapchainWidth = 0;
        uint swapchainHeight = 0;

        if (!SDL_AcquireGPUSwapchainTexture(
                cmd,
                window,
                &swapchainTexture,
                &swapchainWidth,
                &swapchainHeight))
        {
            SDL_SubmitGPUCommandBuffer(cmd);
            return;
        }

        if (swapchainTexture == null)
        {
            SDL_SubmitGPUCommandBuffer(cmd);
            return;
        }

        Matrix4x4 mvp = BuildMvp(swapchainWidth, swapchainHeight);

        SDL_PushGPUVertexUniformData(
            cmd,
            0,
            (nint)(&mvp),
            (uint)sizeof(Matrix4x4)
        );

        SDL_GPUColorTargetInfo colorTarget = new()
        {
            texture = swapchainTexture,

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

        SDL_GPURenderPass* renderPass = SDL_BeginGPURenderPass(
            cmd,
            &colorTarget,
            1,
            null
        );

        SDL_BindGPUGraphicsPipeline(renderPass, resources.Pipeline);

        SDL_GPUBufferBinding vertexBinding = new()
        {
            buffer = resources.VertexBuffer,
            offset = 0
        };

        SDL_BindGPUVertexBuffers(renderPass, 0, &vertexBinding, 1);

        SDL_GPUBufferBinding indexBinding = new()
        {
            buffer = resources.IndexBuffer,
            offset = 0
        };

        SDL_BindGPUIndexBuffer(
            renderPass,
            &indexBinding,
            SDL_GPUIndexElementSize.SDL_GPU_INDEXELEMENTSIZE_16BIT
        );

        SDL_DrawGPUIndexedPrimitives(
            renderPass,
            num_indices: (uint)CubeIndices.Length,
            num_instances: 1,
            first_index: 0,
            vertex_offset: 0,
            first_instance: 0
        );

        SDL_EndGPURenderPass(renderPass);

        if (!SDL_SubmitGPUCommandBuffer(cmd))
            Die("SDL_SubmitGPUCommandBuffer frame failed");
    }

    static Matrix4x4 BuildMvp(uint width, uint height)
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

        // Vulkan-style framebuffer orientation correction.
        // If you later target non-Vulkan backends, revisit this.
        projection.M22 *= -1.0f;

        return world * view * projection;
    }

    static unsafe string Utf8(byte* ptr)
    {
        return ptr == null ? "<null>" : SDL3.PtrToStringUTF8(ptr) ?? "<null>";
    }

    static byte[] Utf8Bytes(string value)
    {
        return System.Text.Encoding.UTF8.GetBytes(value + '\0');
    }

    [StructLayout(LayoutKind.Sequential)]
    struct Vertex
    {
        public Vector3 Position;

        public Vertex(float x, float y, float z)
        {
            Position = new Vector3(x, y, z);
        }
    }

    unsafe struct CubeResources
    {
        public SDL_GPUShader* VertexShader;
        public SDL_GPUShader* FragmentShader;
        public SDL_GPUGraphicsPipeline* Pipeline;
        public SDL_GPUBuffer* VertexBuffer;
        public SDL_GPUBuffer* IndexBuffer;
    }

    static void Die(string message)
    {
        unsafe
        {
            string error = SDL_GetError() ?? "unknown SDL error";
            throw new InvalidOperationException($"{message}: {error}");
        }
    }

    static readonly Vertex[] CubeVertices =
    [
        new(-1, -1, -1),
        new( 1, -1, -1),
        new( 1,  1, -1),
        new(-1,  1, -1),

        new(-1, -1,  1),
        new( 1, -1,  1),
        new( 1,  1,  1),
        new(-1,  1,  1),
    ];

    static readonly ushort[] CubeIndices =
    [
    // Front
    4, 5, 6,
    4, 6, 7,

    // Back
    1, 0, 3,
    1, 3, 2,

    // Left
    0, 4, 7,
    0, 7, 3,

    // Right
    5, 1, 2,
    5, 2, 6,

    // Top
    3, 7, 6,
    3, 6, 2,

    // Bottom
    0, 1, 5,
    0, 5, 4,
];
}
