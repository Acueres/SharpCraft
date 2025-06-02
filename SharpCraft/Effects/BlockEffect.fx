matrix World;
matrix View;
matrix Projection;

float Alpha = 1;
float LightIntensity = 1;

texture Texture;

sampler2D textureSampler = sampler_state
{
    Texture = <Texture>;
    MipFilter = Point;
    MagFilter = Point;
    MinFilter = Point;
    AddressU = Clamp;
    AddressV = Clamp;
};

struct VertexShaderInput
{
    float4 Position : POSITION0;
    float2 TextureCoordinate : TEXCOORD0;
    float Light : TEXCOORD01;
};

struct VertexShaderOutput
{
    float4 Position : POSITION0;
    float2 TextureCoordinate : TEXCOORD0;
    float Light : TEXCOORD1;
};

struct PixelShaderOutput
{
    float4 Color : COLOR0;
};

VertexShaderOutput VertexShaderFunction(VertexShaderInput input)
{
    VertexShaderOutput output = (VertexShaderOutput)0;

    float4 worldPosition = mul(input.Position, World);
    float4 viewPosition = mul(worldPosition, View);
    output.Position = mul(viewPosition, Projection);

    output.TextureCoordinate = input.TextureCoordinate;

    output.Light = input.Light;

    return output;
}

PixelShaderOutput PixelShaderFunction(VertexShaderOutput input)
{
    PixelShaderOutput output = (PixelShaderOutput)0;
    output.Color = tex2D(textureSampler, input.TextureCoordinate);

    float skylight = pow(input.Light % 397 / 15.0f, 1.4f) * LightIntensity;
    float blockLight = pow(floor(input.Light / 397) / 15.0f, 1.4f);
    float4 colorValue = max(skylight, blockLight);

    output.Color.rgb *= colorValue;
    output.Color.a *= Alpha;

    return output;
}

technique BlockTechnique
{
    pass Pass0
    {
        VertexShader = compile vs_3_0 VertexShaderFunction();
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}