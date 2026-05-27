using SharpCraft.Platform;
using SharpCraft.Rendering;

using SDL;
using System.Numerics;

using static SDL.SDL3;

namespace SharpCraft.Graphics.Resources;

internal unsafe sealed class SpritePipeline : IDisposable
{
    public SDL_GPUGraphicsPipeline* Handle => pipeline;

    private readonly GpuDevice device;
    private readonly SDL_GPUGraphicsPipeline* pipeline;

    public SpritePipeline(
        GpuDevice device,
        Shader vertexShader,
        Shader fragmentShader)
    {
        this.device = device;

        SDL_GPUVertexBufferDescription spriteBufferDescription = new()
        {
            slot = 0,
            pitch = (uint)sizeof(SpriteVertex),
            input_rate = SDL_GPUVertexInputRate.SDL_GPU_VERTEXINPUTRATE_VERTEX,
            instance_step_rate = 0
        };

        SDL_GPUVertexAttribute positionAttribute = new()
        {
            location = 0,
            buffer_slot = 0,
            format = SDL_GPUVertexElementFormat.SDL_GPU_VERTEXELEMENTFORMAT_FLOAT2,
            offset = 0
        };

        SDL_GPUVertexAttribute texCoordAttribute = new()
        {
            location = 1,
            buffer_slot = 0,
            format = SDL_GPUVertexElementFormat.SDL_GPU_VERTEXELEMENTFORMAT_FLOAT2,
            offset = (uint)sizeof(Vector2)
        };

        SDL_GPUVertexAttribute colorAttribute = new()
        {
            location = 2,
            buffer_slot = 0,
            format = SDL_GPUVertexElementFormat.SDL_GPU_VERTEXELEMENTFORMAT_FLOAT4,
            offset = (uint)(sizeof(Vector2) + sizeof(Vector2))
        };

        SDL_GPUVertexAttribute* spriteAttributes =
            stackalloc SDL_GPUVertexAttribute[3];

        spriteAttributes[0] = positionAttribute;
        spriteAttributes[1] = texCoordAttribute;
        spriteAttributes[2] = colorAttribute;

        SDL_GPUColorTargetBlendState blendState = new()
        {
            enable_blend = true,

            src_color_blendfactor = SDL_GPUBlendFactor.SDL_GPU_BLENDFACTOR_SRC_ALPHA,
            dst_color_blendfactor = SDL_GPUBlendFactor.SDL_GPU_BLENDFACTOR_ONE_MINUS_SRC_ALPHA,
            color_blend_op = SDL_GPUBlendOp.SDL_GPU_BLENDOP_ADD,

            src_alpha_blendfactor = SDL_GPUBlendFactor.SDL_GPU_BLENDFACTOR_ONE,
            dst_alpha_blendfactor = SDL_GPUBlendFactor.SDL_GPU_BLENDFACTOR_ONE_MINUS_SRC_ALPHA,
            alpha_blend_op = SDL_GPUBlendOp.SDL_GPU_BLENDOP_ADD,

            enable_color_write_mask = true,
            color_write_mask =
                SDL_GPUColorComponentFlags.SDL_GPU_COLORCOMPONENT_R |
                SDL_GPUColorComponentFlags.SDL_GPU_COLORCOMPONENT_G |
                SDL_GPUColorComponentFlags.SDL_GPU_COLORCOMPONENT_B |
                SDL_GPUColorComponentFlags.SDL_GPU_COLORCOMPONENT_A
        };

        SDL_GPUColorTargetDescription colorTargetDescription = new()
        {
            format = device.SwapchainFormat,
            blend_state = blendState
        };

        SDL_GPUGraphicsPipelineCreateInfo pipelineInfo = new()
        {
            vertex_shader = vertexShader.Handle,
            fragment_shader = fragmentShader.Handle,

            primitive_type = SDL_GPUPrimitiveType.SDL_GPU_PRIMITIVETYPE_TRIANGLELIST,

            vertex_input_state = new SDL_GPUVertexInputState
            {
                vertex_buffer_descriptions = &spriteBufferDescription,
                num_vertex_buffers = 1,

                vertex_attributes = spriteAttributes,
                num_vertex_attributes = 3
            },

            rasterizer_state = new SDL_GPURasterizerState
            {
                fill_mode = SDL_GPUFillMode.SDL_GPU_FILLMODE_FILL,
                cull_mode = SDL_GPUCullMode.SDL_GPU_CULLMODE_NONE,
                front_face = SDL_GPUFrontFace.SDL_GPU_FRONTFACE_COUNTER_CLOCKWISE
            },

            multisample_state = new SDL_GPUMultisampleState
            {
                sample_count = SDL_GPUSampleCount.SDL_GPU_SAMPLECOUNT_1
            },

            depth_stencil_state = new SDL_GPUDepthStencilState
            {
                enable_depth_test = false,
                enable_depth_write = false,
                compare_op = SDL_GPUCompareOp.SDL_GPU_COMPAREOP_ALWAYS
            },

            target_info = new SDL_GPUGraphicsPipelineTargetInfo
            {
                color_target_descriptions = &colorTargetDescription,
                num_color_targets = 1,

                // Draw sprites in the same render pass
                // as the 3D world, where a depth target is already attached
                has_depth_stencil_target = true,
                depth_stencil_format = SDL_GPUTextureFormat.SDL_GPU_TEXTUREFORMAT_D24_UNORM
            }
        };

        pipeline = SDL_CreateGPUGraphicsPipeline(device.Handle, &pipelineInfo);
        if (pipeline == null)
        {
            SdlRuntime.Throw("Failed to create sprite pipeline");
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