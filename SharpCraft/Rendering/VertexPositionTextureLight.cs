using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace SharpCraft.Rendering;

public struct VertexPositionTextureLight(Vector3 position, Vector2 textureCoordinate, float light) : IVertexType
{
    public Vector3 Position { get; set; } = position;
    public Vector2 TextureCoordinate { get; set; } = textureCoordinate;
    public float Light { get; set; } = light;

    static readonly VertexDeclaration vertexDeclaration = new(
        new VertexElement(0, VertexElementFormat.Vector3, VertexElementUsage.Position, 0),
        new VertexElement(sizeof(float) * 3, VertexElementFormat.Vector2, VertexElementUsage.TextureCoordinate, 0),
        new VertexElement(sizeof(float) * 5, VertexElementFormat.Single, VertexElementUsage.TextureCoordinate, 1)
    );

    public readonly VertexDeclaration VertexDeclaration => vertexDeclaration;
}
