using SharpCraft.Platform;
using SharpCraft.Rendering;

using SDL;
using System.Numerics;

using static SDL.SDL3;

namespace SharpCraft.Graphics.Resources;

internal unsafe class BlockFacePipeline : IDisposable
{
    public SDL_GPUGraphicsPipeline* Handle => pipeline;

    private readonly GpuDevice device;
    private readonly SDL_GPUGraphicsPipeline* pipeline;

    public static BlockFacePipeline CreateOpaque(GpuDevice device, Shader vertexShader, Shader fragmentShader)
    {
        var blendState = new SDL_GPUColorTargetBlendState
        {
            enable_blend = false,

            enable_color_write_mask = true,
            color_write_mask =
                SDL_GPUColorComponentFlags.SDL_GPU_COLORCOMPONENT_R |
                SDL_GPUColorComponentFlags.SDL_GPU_COLORCOMPONENT_G |
                SDL_GPUColorComponentFlags.SDL_GPU_COLORCOMPONENT_B |
                SDL_GPUColorComponentFlags.SDL_GPU_COLORCOMPONENT_A
        };

        var depthStencilState = new SDL_GPUDepthStencilState
        {
            enable_depth_test = true,
            enable_depth_write = true,
            compare_op = SDL_GPUCompareOp.SDL_GPU_COMPAREOP_LESS
        };

        var pipeline = new BlockFacePipeline(device, vertexShader, fragmentShader, &blendState, &depthStencilState);
        return pipeline;
    }
    
    public static BlockFacePipeline CreateTransparent(GpuDevice device, Shader vertexShader, Shader fragmentShader)
    {
        var blendState = new SDL_GPUColorTargetBlendState
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

        var depthStencilState = new SDL_GPUDepthStencilState
        {
            enable_depth_test = true,
            enable_depth_write = false,
            compare_op = SDL_GPUCompareOp.SDL_GPU_COMPAREOP_LESS
        };

        var pipeline = new BlockFacePipeline(device, vertexShader, fragmentShader, &blendState, &depthStencilState);
        return pipeline;
    }
    
    private BlockFacePipeline(GpuDevice device, Shader vertexShader, Shader fragmentShader,
        SDL_GPUColorTargetBlendState* blendState, SDL_GPUDepthStencilState* depthStencilState)
    {
        this.device = device;

        SDL_GPUVertexBufferDescription faceBufferDescription = new()
        {
            slot = 0,
            pitch = (uint)sizeof(BlockFace),
            input_rate = SDL_GPUVertexInputRate.SDL_GPU_VERTEXINPUTRATE_INSTANCE,
            instance_step_rate = 0
        };

        SDL_GPUColorTargetDescription colorTargetDescription = new()
        {
            format = device.SwapchainFormat,
            blend_state = *blendState
        };
        
        SDL_GPUVertexAttribute centerAttribute = new()
        {
            location = 0,
            buffer_slot = 0,
            format = SDL_GPUVertexElementFormat.SDL_GPU_VERTEXELEMENTFORMAT_FLOAT3,
            offset = 0
        };

        SDL_GPUVertexAttribute directionAttribute = new()
        {
            location = 1,
            buffer_slot = 0,
            format = SDL_GPUVertexElementFormat.SDL_GPU_VERTEXELEMENTFORMAT_UINT,
            offset = (uint)sizeof(Vector3)
        };

        SDL_GPUVertexAttribute textureLayerAttribute = new()
        {
            location = 2,
            buffer_slot = 0,
            format = SDL_GPUVertexElementFormat.SDL_GPU_VERTEXELEMENTFORMAT_UINT,
            offset = (uint)(sizeof(Vector3) + sizeof(uint))
        };
        
        SDL_GPUVertexAttribute packedLightAttribute = new()
        {
            location = 3,
            buffer_slot = 0,
            format = SDL_GPUVertexElementFormat.SDL_GPU_VERTEXELEMENTFORMAT_UINT,
            offset = (uint)(
                sizeof(Vector3) +
                sizeof(uint) +
                sizeof(uint)
            )
        };
        
        SDL_GPUVertexAttribute* faceAttributes = stackalloc SDL_GPUVertexAttribute[4];
        faceAttributes[0] = centerAttribute;
        faceAttributes[1] = directionAttribute;
        faceAttributes[2] = textureLayerAttribute;
        faceAttributes[3] = packedLightAttribute;

        SDL_GPUGraphicsPipelineCreateInfo pipelineInfo = new()
        {
            vertex_shader = vertexShader.Handle,
            fragment_shader = fragmentShader.Handle,

            primitive_type = SDL_GPUPrimitiveType.SDL_GPU_PRIMITIVETYPE_TRIANGLELIST,

            vertex_input_state = new SDL_GPUVertexInputState
            {
                vertex_buffer_descriptions = &faceBufferDescription,
                num_vertex_buffers = 1,
                vertex_attributes = faceAttributes,
                num_vertex_attributes = 4
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

            depth_stencil_state = *depthStencilState,

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
