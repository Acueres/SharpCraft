using SharpCraft.SharpMath;

using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

namespace SharpCraft.World.Meshing;

internal class MeshSink
{
    private readonly ConcurrentQueue<MeshUpdate> meshes = [];
    private long queuedFaces;

    public int QueuedUpdates => meshes.Count;
    public long QueuedFaces => Interlocked.Read(ref queuedFaces);

    public void AddMesh(MeshUpdate meshEntry)
    {
        // Counted before enqueueing so a concurrent dequeue never drives the total negative
        Interlocked.Add(ref queuedFaces, FaceCount(meshEntry));
        meshes.Enqueue(meshEntry);
    }

    public void RemoveMesh(Vec3<int> index)
    {
        var removeEntry = new MeshUpdate(index, MeshUpdateState.Obsolete, [], []);
        meshes.Enqueue(removeEntry);
    }

    public bool TryPeekMesh([MaybeNullWhen(false)] out MeshUpdate result)
    {
        result = null;

        if (meshes.TryPeek(out result))
        {
            return true;
        }

        return false;
    }

    public bool TryDequeueMesh()
    {
        if (!meshes.TryDequeue(out var mesh)) return false;

        Interlocked.Add(ref queuedFaces, -FaceCount(mesh));
        return true;
    }

    private static long FaceCount(MeshUpdate mesh) => mesh.Opaque.Length + mesh.Transparent.Length;
}
