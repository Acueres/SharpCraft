using SharpCraft.Graphics.Resources;

namespace SharpCraft.Rendering;

internal readonly struct SpriteBatch
{
    public readonly Texture Texture;
    public readonly uint FirstIndex;
    public readonly uint IndexCount;

    public SpriteBatch(Texture texture, uint firstIndex, uint indexCount)
    {
        Texture = texture;
        FirstIndex = firstIndex;
        IndexCount = indexCount;
    }

    public SpriteBatch WithAdditionalIndices(uint count)
    {
        return new SpriteBatch(Texture, FirstIndex, IndexCount + count);
    }
}