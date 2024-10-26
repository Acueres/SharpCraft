using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;


namespace SharpCraft.Utility
{
    public struct VertexPositionTextureLight : IVertexType
    {
        public Vector3 Position;
        public Vector2 TextureCoordinate;
        public float Light;

        static readonly VertexDeclaration vertexDeclaration = new(
            new VertexElement(0, VertexElementFormat.Vector3, VertexElementUsage.Position, 0),
            new VertexElement(sizeof(float) * 3, VertexElementFormat.Vector2, VertexElementUsage.TextureCoordinate, 0),
            new VertexElement(sizeof(float) * 5, VertexElementFormat.Single, VertexElementUsage.TextureCoordinate, 1)
        );

        public VertexDeclaration VertexDeclaration
        {
            get { return vertexDeclaration; }
        }

        public VertexPositionTextureLight(Vector3 position, Vector2 textureCoordinate, float light)
        {
            Position = position;
            TextureCoordinate = textureCoordinate;
            Light = light;
        }
    }
}
