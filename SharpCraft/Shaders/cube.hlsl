#pragma pack_matrix(row_major)

struct Camera
{
    float4x4 Mvp;
};

[[vk::binding(0, 1)]]
ConstantBuffer<Camera> camera : register(b0, space1);

Texture2D cubeTexture : register(t0, space2);
SamplerState cubeSampler : register(s0, space2);

struct VSInput
{
    [[vk::location(0)]]
    float3 Position : POSITION;

    [[vk::location(1)]]
    float2 TexCoord : TEXCOORD0;
};

struct VSOutput
{
    float4 Position : SV_Position;
    float2 TexCoord : TEXCOORD0;
};

VSOutput MainVS(VSInput input)
{
    VSOutput output;

    output.Position = mul(float4(input.Position, 1.0), camera.Mvp);
    output.TexCoord = input.TexCoord;

    return output;
}

float4 MainFS(VSOutput input) : SV_Target0
{
    return cubeTexture.Sample(cubeSampler, input.TexCoord);
}