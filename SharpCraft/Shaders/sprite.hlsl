#pragma pack_matrix(row_major)

struct Screen
{
    float2 Size;
    float2 Padding;
};

[[vk::binding(0, 1)]]
ConstantBuffer<Screen> screen : register(b0, space1);

Texture2D spriteTexture : register(t0, space2);
SamplerState spriteSampler : register(s0, space2);

struct VSInput
{
    [[vk::location(0)]]
    float2 Position : POSITION;

    [[vk::location(1)]]
    float2 TexCoord : TEXCOORD0;

    [[vk::location(2)]]
    float4 Color : COLOR0;
};

struct VSOutput
{
    float4 Position : SV_Position;
    float2 TexCoord : TEXCOORD0;
    float4 Color : COLOR0;
};

VSOutput MainVS(VSInput input)
{
    VSOutput output;

    float2 ndc;
    ndc.x = input.Position.x / screen.Size.x * 2.0f - 1.0f;
    ndc.y = 1.0f - input.Position.y / screen.Size.y * 2.0f;

    output.Position = float4(ndc, 0.0f, 1.0f);
    output.TexCoord = input.TexCoord;
    output.Color = input.Color;

    return output;
}

float4 MainFS(VSOutput input) : SV_Target0
{
    float4 sampled = spriteTexture.Sample(spriteSampler, input.TexCoord);
    return sampled * input.Color;
}