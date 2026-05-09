#pragma pack_matrix(row_major)

struct Camera
{
    float4x4 Mvp;
};

[[vk::binding(0, 1)]]
ConstantBuffer<Camera> camera : register(b0, space1);

struct VSInput
{
    [[vk::location(0)]]
    float3 Position : POSITION;
};

struct VSOutput
{
    float4 Position : SV_Position;
};

VSOutput MainVS(VSInput input)
{
    VSOutput output;
    output.Position = mul(float4(input.Position, 1.0), camera.Mvp);
    return output;
}

float4 MainFS() : SV_Target0
{
    return float4(0.0, 1.0, 0.15, 1.0);
}