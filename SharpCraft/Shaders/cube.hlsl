#pragma pack_matrix(row_major)

struct Camera
{
    float4x4 Mvp;
};

[[vk::binding(0, 1)]]
ConstantBuffer<Camera> camera : register(b0, space1);

Texture2DArray cubeTextures : register(t0, space2);
SamplerState cubeSampler : register(s0, space2);

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
    float3 Center : TEXCOORD0;

    [[vk::location(1)]]
    uint Direction : TEXCOORD1;
    
    [[vk::location(2)]]
    uint TextureLayer : TEXCOORD2;
    
    [[vk::location(3)]]
    uint PackedLight : TEXCOORD3;
    
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
    float3(-1, -1,  1),
    float3( 1, -1,  1),
    float3( 1,  1,  1),
    float3(-1,  1,  1),

    // ZNeg
    float3( 1, -1, -1),
    float3(-1, -1, -1),
    float3(-1,  1, -1),
    float3( 1,  1, -1),

    // XPos
    float3( 1, -1,  1),
    float3( 1, -1, -1),
    float3( 1,  1, -1),
    float3( 1,  1,  1),

    // XNeg
    float3(-1, -1, -1),
    float3(-1, -1,  1),
    float3(-1,  1,  1),
    float3(-1,  1, -1),

    // YPos
    float3(-1,  1,  1),
    float3( 1,  1,  1),
    float3( 1,  1, -1),
    float3(-1,  1, -1),

    // YNeg
    float3(-1, -1, -1),
    float3( 1, -1, -1),
    float3( 1, -1,  1),
    float3(-1, -1,  1)
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
    uint skylightLevel = packedLight & 0xF;
    uint blockLightLevel = (packedLight >> 4) & 0xF;

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
    float3 worldPosition = input.Center + GetFaceCorner(input.Direction, corner);

    output.Position = mul(float4(worldPosition, 1.0), camera.Mvp);
    output.TexCoord = FaceUvs[corner];
    output.TextureLayer = input.TextureLayer;
    output.Light = ComputeLight(input.PackedLight);

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