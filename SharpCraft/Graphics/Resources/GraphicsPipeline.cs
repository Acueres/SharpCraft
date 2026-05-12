using SDL;

using SharpCraft.Platform;
using SharpCraft.Rendering;

using static SDL.SDL3;

namespace SharpCraft.Graphics.Resources;

internal unsafe class GraphicsPipeline : IDisposable
{
    public SDL_GPUGraphicsPipeline* Handle => pipeline;

    private readonly GpuDevice device;
    private readonly SDL_GPUGraphicsPipeline* pipeline;

    public GraphicsPipeline(GpuDevice device, Shader vertexShader, Shader fragmentShader)
    {
        this.device = device;

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
            format = device.SwapchainFormat,
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
            vertex_shader = vertexShader.Handle,
            fragment_shader = fragmentShader.Handle,

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

        pipeline = SDL_CreateGPUGraphicsPipeline(device.Handle, &pipelineInfo);
        if (pipeline == null)
        {
            SdlRuntime.Throw("Failed to create graphics pipeline");
        }
    }

    private bool disposed;
    public void Dispose()
    {
        if (disposed) return;

        SDL_ReleaseGPUGraphicsPipeline(device.Handle, pipeline);

        disposed = true;
    }
}
