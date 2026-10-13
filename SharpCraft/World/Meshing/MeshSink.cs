using SharpCraft.SharpMath;

using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

namespace SharpCraft.World.Meshing;

internal class MeshSink
{
    private readonly ConcurrentQueue<MeshUpdate> meshes = [];

    public void AddMesh(MeshUpdate meshEntry)
    {
        meshes.Enqueue(meshEntry);
    }

    public void RemoveMesh(Vec3<int> index)
    {
        var removeEntry = new MeshUpdate(index, MeshUpdateState.Obsolete, [], []);
        meshes.Enqueue(removeEntry);
    }
    
    public bool ReadMesh([MaybeNullWhen(false)] out MeshUpdate result)
    {
        result = null;
        
        if (meshes.TryDequeue(out result))
        {
            return true;
        }

        return false;
    }
}