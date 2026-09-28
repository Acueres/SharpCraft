using SharpCraft.Graphics.Resources;
using SDL;

namespace SharpCraft.Rendering;

internal readonly unsafe struct SpriteBatch
{
    public readonly SDL_GPUTexture* Texture;
    public readonly Sampler Sampler;
    public readonly uint FirstIndex;
    public readonly uint IndexCount;

    public SpriteBatch(SDL_GPUTexture* texture, Sampler sampler, uint firstIndex, uint indexCount)
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
