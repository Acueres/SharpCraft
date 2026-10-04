#pragma pack_matrix(row_major)

struct SlotRecordGpu
{
    // Bytes 0–15
    int ChunkX;
    int ChunkY;
    int ChunkZ;
    uint Flags;

    // Bytes 16–31
    uint Aux0;
    uint Aux1;
    uint Aux2;
    uint Aux3;

    // Bytes 32–47
    float BoundsX;
    float BoundsY;
    float BoundsZ;
    float BoundsRadius;
};

[[vk::binding(0, 0)]]
StructuredBuffer<SlotRecordGpu> slots : register(t0, space0);

struct Camera
{
    float4x4 Mvp;
    int3 PositionIndex;
    float3 LocalPosition;
};

[[vk::binding(0, 1)]]
ConstantBuffer<Camera> camera : register(b0, space1);

Texture2DArray cubeTextures : register(t0, space2);
SamplerState cubeSampler : register(s0, space2);

static const int ChunkSize = 16;

static const uint QuadCornerIndices[6] =
{
    0, 1, 2,
    2, 3, 0
};

static const float2 FaceUvs[4] =
{
    float2(0, 1),
    float2(1, 1),
    float2(1, 0),
    float2(0, 0)
};

struct VSInput
{
    [[vk::location(0)]]
    uint Uint0 : TEXCOORD0;
    
    [[vk::location(1)]]
    uint Uint1 : TEXCOORD1;

    [[vk::location(2)]]
    uint Uint2 : TEXCOORD2;
    
    [[vk::location(3)]]
    uint Uint3 : TEXCOORD3;
    
    uint VertexId : SV_VertexID;
};

struct VSOutput
{
    float4 Position : SV_Position;
    float2 TexCoord : TEXCOORD0;
    nointerpolation uint TextureLayer : TEXCOORD1;
    float Light : TEXCOORD2;
};

static const float3 FaceCorners[24] =
{
    // ZPos
    float3(-0.5, -0.5,  0.5),
    float3( 0.5, -0.5,  0.5),
    float3( 0.5,  0.5,  0.5),
    float3(-0.5,  0.5,  0.5),

    // ZNeg
    float3( 0.5, -0.5, -0.5),
    float3(-0.5, -0.5, -0.5),
    float3(-0.5,  0.5, -0.5),
    float3( 0.5,  0.5, -0.5),

    // XPos
    float3( 0.5, -0.5,  0.5),
    float3( 0.5, -0.5, -0.5),
    float3( 0.5,  0.5, -0.5),
    float3( 0.5,  0.5,  0.5),

    // XNeg
    float3(-0.5, -0.5, -0.5),
    float3(-0.5, -0.5,  0.5),
    float3(-0.5,  0.5,  0.5),
    float3(-0.5,  0.5, -0.5),

    // YPos
    float3(-0.5,  0.5,  0.5),
    float3( 0.5,  0.5,  0.5),
    float3( 0.5,  0.5, -0.5),
    float3(-0.5,  0.5, -0.5),

    // YNeg
    float3(-0.5, -0.5, -0.5),
    float3( 0.5, -0.5, -0.5),
    float3( 0.5, -0.5,  0.5),
    float3(-0.5, -0.5,  0.5)
};

float3 GetFaceCorner(uint direction, uint corner)
{
    if (direction > 5)
    {
        return float3(0, 0, 0);
    }

    return FaceCorners[direction * 4 + corner];
}

float ComputeLight(uint packedLight)
{
    uint blockLightLevel = packedLight & 0xF;
    uint skylightLevel = (packedLight >> 4) & 0xF;

    float skylight =
        pow((float)skylightLevel / 15.0f, 1.4f);

    float blockLight =
        pow((float)blockLightLevel / 15.0f, 1.4f);

    return max(skylight, blockLight);
}

VSOutput MainVS(VSInput input)
{
    VSOutput output;
    
    uint corner = QuadCornerIndices[input.VertexId];
    
    uint slotId = input.Uint2 >> 16;
    SlotRecordGpu slot = slots[slotId];
    
    int3 chunkIndex = int3(slot.ChunkX, slot.ChunkY, slot.ChunkZ);
    int3 relativeOffset = chunkIndex - camera.PositionIndex;
    relativeOffset = mul(relativeOffset, ChunkSize);
    
    float3 cameraRelativePosition = float3(relativeOffset);
    cameraRelativePosition = cameraRelativePosition - camera.LocalPosition;
    
    uint localX = input.Uint0          & 0xFFu;
    uint localY = (input.Uint0 >> 8u)  & 0xFFu;
    uint localZ = (input.Uint0 >> 16u) & 0xFFu;
    
    uint direction = (input.Uint0 >> 24u) & 0x7u;
    uint textureId = input.Uint2 & 0xFFFFu;
    uint packedLight = input.Uint3 & 0xFFu;
    
    cameraRelativePosition = cameraRelativePosition + float3(localX, localY, localZ);
    cameraRelativePosition = cameraRelativePosition + GetFaceCorner(direction, corner);

    output.Position = mul(float4(cameraRelativePosition, 1.0), camera.Mvp);
    output.TexCoord = FaceUvs[corner];
    output.TextureLayer = textureId;
    output.Light = ComputeLight(packedLight);

    return output;
}

float4 MainFS(VSOutput input) : SV_Target0
{
    float4 color = cubeTextures.Sample(
        cubeSampler,
        float3(input.TexCoord, (float)input.TextureLayer)
    );

    color.rgb *= input.Light;

    return color;
}