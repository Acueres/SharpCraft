using SharpCraft.Graphics.Resources;

namespace SharpCraft.Assets;

internal class GraphicsShader(Shader vertex, Shader fragment) : IDisposable
{
    public Shader Vertex { get; } = vertex;
    public Shader Fragment { get; } = fragment;

    bool disposed;
    public void Dispose()
    {
        if (disposed) return;

        Vertex.Dispose();
        Fragment.Dispose();

        disposed = true;
    }
}
