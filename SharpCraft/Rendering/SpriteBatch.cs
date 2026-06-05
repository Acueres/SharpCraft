using SharpCraft.Graphics.Resources;

namespace SharpCraft.Rendering;

internal readonly struct SpriteBatch
{
    public readonly Texture Texture;
    public readonly Sampler Sampler;
    public readonly uint FirstIndex;
    public readonly uint IndexCount;

    public SpriteBatch(Texture texture, Sampler sampler, uint firstIndex, uint indexCount)
    {
        Texture = texture;
        Sampler = sampler;
        FirstIndex = firstIndex;
        IndexCount = indexCount;
    }

    public SpriteBatch WithAdditionalIndices(uint count)
    {
        return new SpriteBatch(Texture, Sampler, FirstIndex, IndexCount + count);
    }
}