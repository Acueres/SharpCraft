using SharpCraft.MathUtilities;
using SharpCraft.Utilities;
using SharpCraft.World.Blocks;
using SharpCraft.World.Chunks;

using System.Collections.Concurrent;
using System.Collections.Generic;

namespace SharpCraft.World.Lighting;

public class LightSystem
{
    readonly ConcurrentQueue<LightNode> lightQueue = [];

    public void InitializeSkylight(Chunk chunk)
    {
        //Propagate light downward from a sky chunk
        if (!chunk.YNeg.IsEmpty)
        {
            for (int x = 0; x < Chunk.Size; x++)
            {
                for (int z = 0; z < Chunk.Size; z++)
                {
                    if (!chunk.YNeg[x, Chunk.Last, z].IsEmpty) continue;

                    chunk.YNeg.SetLight(x, Chunk.Last, z, LightValue.Sunlight);
                    lightQueue.Enqueue(new LightNode(chunk.YNeg, x, Chunk.Last, z));
                }
            }
        }
    }

    public void InitializeLight(Chunk chunk)
    {
        //take light from upper chunk
        for (int x = 0; x < Chunk.Size; x++)
        {
            for (int z = 0; z < Chunk.Size; z++)
            {
                if (!chunk.YPos[x, 0, z].IsEmpty) continue;

                lightQueue.Enqueue(new LightNode(chunk.YPos, x, 0, z));
            }
        }

        foreach ((Vec3<byte> lightSourceIndex, Block block) in chunk.GetLightSources())
        {
            int x = lightSourceIndex.X;
            int y = lightSourceIndex.Y;
            int z = lightSourceIndex.Z;

            LightValue light = chunk.GetLight(x, y, z);
            LightValue sourceLight = new(light.SkyValue, chunk.GetLightSourceValue(lightSourceIndex.Into<int>()));

            chunk.SetLight(x, y, z, sourceLight);
            lightQueue.Enqueue(new LightNode(chunk, x, y, z));
        }
    }

    public void Execute()
    {
        while (!lightQueue.IsEmpty)
        {
            lightQueue.TryDequeue(out LightNode node);

            node.Chunk.RecalculateMesh = true;

            ProcessLightNode(node);
        }
    }

    void ProcessLightNode(LightNode node)
    {
        node.Chunk.RecalculateMesh = true;

        if (node.Chunk.AllAdjacent)
        {
            Propagate(node.Chunk, node.X, node.Y, node.Z);
            return;
        }
    }

    public void UpdateLight(int x, int y, int z, ushort texture, Chunk chunk, bool sourceRemoved = false)
    {
        //Propagate light to an empty cell
        if (texture == Block.EmptyValue)
        {
            var (nodes, _) = GetNeighborLightValues(x, y, z, chunk);

            foreach (var node in nodes.GetValues())
            {
                lightQueue.Enqueue(node);
            }

            FloodFill();

            if (sourceRemoved)
            {
                lightQueue.Enqueue(new LightNode(chunk, x, y, z));
                RemoveSource();
            }
        }
        //Recalculate light after block placement
        else
        {
            bool sourceAdded = false;
            if (chunk.IsBlockLightSource(new Vec3<int>(x, y, z)))
            {
                LightValue light = chunk.GetLight(x, y, z);
                LightValue sourceLight = new(light.SkyValue, chunk.GetLightSourceValue(new Vec3<int>(x, y, z)));
                chunk.SetLight(x, y, z, sourceLight);
                sourceAdded = true;
            }

            lightQueue.Enqueue(new LightNode(chunk, x, y, z));

            if (sourceAdded)
            {
                FloodFill();
            }
            else
            {
                FloodRemove();
            }
        }
    }

    void RemoveSource()
    {
        while (!lightQueue.IsEmpty)
        {
            lightQueue.TryDequeue(out LightNode node);

            LightValue light = node.GetLight();

            node.Chunk.RecalculateMesh = true;

            node.SetLight(new(light.SkyValue, 0));

            var (nodes, lightValues) = GetNeighborLightValues(node.X, node.Y, node.Z, node.Chunk);

            for (int i = 0; i < 6; i++)
            {
                Faces face = (Faces)i;
                if (lightValues.GetValue(face).BlockValue == (byte)(light.BlockValue - 1))
                {
                    lightQueue.Enqueue(nodes.GetValue(face));
                }
            }
        }
    }

    void FloodRemove()
    {
        List<LightNode> lightList = [];

        while (!lightQueue.IsEmpty)
        {
            lightQueue.TryDequeue(out LightNode node);

            node.Chunk.RecalculateMesh = true;

            LightValue light = node.GetLight();

            if (IsBlockTransparentSolid(node.Chunk, node.X, node.Y, node.Z))
            {
                node.SetLight(light);
            }
            else
            {
                node.SetLight(LightValue.Null);
            }

            var (nodes, lightValues) = GetNeighborLightValues(node.X, node.Y, node.Z, node.Chunk);

            Faces maxFace = Util.MaxFace(lightValues.GetValues());

            lightList.Add(nodes.GetValue(maxFace));

            for (int i = 0; i < 6; i++)
            {
                Faces face = (Faces)i;
                var n = nodes.GetValue(face);

                if (lightValues.GetValue(face) > LightValue.Null && (face == Faces.YNeg || n.GetLight() < light) &&
                   IsBlockTransparent(n.Chunk, n.X, n.Y, n.Z))
                {
                    lightQueue.Enqueue(nodes.GetValue(face));
                }
            }
        }

        for (int i = 0; i < lightList.Count; i++)
        {
            if (lightList[i].GetLight() > new LightValue(1, 1))
            {
                lightQueue.Enqueue(lightList[i]);
            }
        }

        FloodFill();
    }

    static (FacesData<LightNode> nodes, FacesData<LightValue> lightValues) GetNeighborLightValues(int x, int y, int z, Chunk chunk)
    {
        FacesData<LightNode> nodes = new();
        FacesData<LightValue> lightValues = new();

        if (y == Chunk.Last)
        {
            nodes.YPos = new LightNode(chunk.YPos, x, 0, z);
            LightValue light = chunk.GetLight(x, 0, z);
            lightValues.YPos = light;
        }
        else
        {
            nodes.YPos = new LightNode(chunk, x, y + 1, z);
            LightValue light = chunk.GetLight(x, y + 1, z);
            lightValues.YPos = light;
        }

        if (y == 0)
        {
            nodes.YNeg = new LightNode(chunk.YNeg, x, Chunk.Last, z);
            LightValue light = chunk.GetLight(x, Chunk.Last, z);
            lightValues.YNeg = light;
        }
        else
        {
            nodes.YNeg = new LightNode(chunk, x, y - 1, z);
            LightValue light = chunk.GetLight(x, y - 1, z);
            lightValues.YNeg = light;
        }


        if (x == Chunk.Last)
        {
            nodes.XPos = new LightNode(chunk.XPos, 0, y, z);
            LightValue light = chunk.GetLight(0, y, z);
            lightValues.XPos = light;
        }
        else
        {
            nodes.XPos = new LightNode(chunk, x + 1, y, z);
            LightValue light = chunk.GetLight(x + 1, y, z);
            lightValues.XPos = light;
        }

        if (x == 0)
        {
            nodes.XNeg = new LightNode(chunk.XNeg, Chunk.Last, y, z);
            LightValue light = chunk.GetLight(Chunk.Last, y, z);
            lightValues.XNeg = light;
        }
        else
        {
            nodes.XNeg = new LightNode(chunk, x - 1, y, z);
            LightValue light = chunk.GetLight(x - 1, y, z);
            lightValues.XNeg = light;
        }


        if (z == Chunk.Last)
        {
            nodes.ZPos = new LightNode(chunk.ZPos, x, y, 0);
            LightValue light = chunk.GetLight(x, y, 0);
            lightValues.ZPos = light;
        }
        else
        {
            nodes.ZPos = new LightNode(chunk, x, y, z + 1);
            LightValue light = chunk.GetLight(x, y, z + 1);
            lightValues.ZPos = light;
        }

        if (z == 0)
        {
            nodes.ZNeg = new LightNode(chunk.ZNeg, x, y, Chunk.Last);
            LightValue light = chunk.GetLight(x, y, Chunk.Last);
            lightValues.ZNeg = light;
        }
        else
        {
            nodes.ZNeg = new LightNode(chunk, x, y, z - 1);
            LightValue light = chunk.GetLight(x, y, z - 1);
            lightValues.ZNeg = light;
        }

        return (nodes, lightValues);
    }

    void FloodFill()
    {
        while (!lightQueue.IsEmpty)
        {
            lightQueue.TryDequeue(out LightNode node);

            node.Chunk.RecalculateMesh = true;

            Propagate(node.Chunk, node.X, node.Y, node.Z);
        }
    }

    void Propagate(Chunk chunk, byte x, byte y, byte z)
    {
        LightValue lightValue = chunk.GetLight(x, y, z);

        if (lightValue == LightValue.Null)
        {
            return;
        }

        LightValue nextLightValue = lightValue;
        if (lightValue.SkyValue > 1)
        {
            nextLightValue = lightValue.SubtractSkyValue(1);
        }

        if (lightValue.BlockValue > 0)
        {
            nextLightValue = nextLightValue.SubtractBlockValue(1);
        }

        LightValue value;

        if (y == Chunk.Last)
        {
            if (IsBlockTransparent(chunk.YPos, x, 0, z) &&
                chunk.YPos.GetLight(x, 0, z).Compare(nextLightValue, out value))
            {
                chunk.YPos.SetLight(x, 0, z, value);
                lightQueue.Enqueue(new LightNode(chunk.YPos, x, 0, z));
            }

            chunk.YPos.RecalculateMesh = true;
        }
        else if (IsBlockTransparent(chunk, x, y + 1, z) &&
            chunk.GetLight(x, y + 1, z).Compare(nextLightValue, out value))
        {
            chunk.SetLight(x, y + 1, z, value);
            lightQueue.Enqueue(new LightNode(chunk, x, y + 1, z));
        }

        if (y == 0)
        {
            if (IsBlockTransparent(chunk.YNeg, x, Chunk.Last, z) &&
                chunk.YNeg.GetLight(x, Chunk.Last, z).Compare(lightValue.SubtractBlockValue(1), out value))
            {
                chunk.YNeg.SetLight(x, Chunk.Last, z, value);
                lightQueue.Enqueue(new LightNode(chunk.YNeg, x, Chunk.Last, z));
            }

            chunk.YNeg.RecalculateMesh = true;
        }
        else if (IsBlockTransparent(chunk, x, y - 1, z) &&
            chunk.GetLight(x, y - 1, z).Compare(lightValue.SubtractBlockValue(1), out value))
        {
            chunk.SetLight(x, y - 1, z, value);
            lightQueue.Enqueue(new LightNode(chunk, x, y - 1, z));
        }


        if (x == Chunk.Last)
        {
            if (IsBlockTransparent(chunk.XPos, 0, y, z) &&
                chunk.XPos.GetLight(0, y, z).Compare(nextLightValue, out value))
            {
                chunk.XPos.SetLight(0, y, z, value);
                lightQueue.Enqueue(new LightNode(chunk.XPos, 0, y, z));
            }

            chunk.XPos.RecalculateMesh = true;
        }
        else if (IsBlockTransparent(chunk, x + 1, y, z) &&
            chunk.GetLight(x + 1, y, z).Compare(nextLightValue, out value))
        {
            chunk.SetLight(x + 1, y, z, value);
            lightQueue.Enqueue(new LightNode(chunk, x + 1, y, z));
        }


        if (x == 0)
        {
            if (IsBlockTransparent(chunk.XNeg, Chunk.Last, y, z) &&
                chunk.XNeg.GetLight(Chunk.Last, y, z).Compare(nextLightValue, out value))
            {
                chunk.XNeg.SetLight(Chunk.Last, y, z, value);
                lightQueue.Enqueue(new LightNode(chunk.XNeg, Chunk.Last, y, z));
            }

            chunk.XNeg.RecalculateMesh = true;
        }
        else if (IsBlockTransparent(chunk, x - 1, y, z) &&
            chunk.GetLight(x - 1, y, z).Compare(nextLightValue, out value))
        {
            chunk.SetLight(x - 1, y, z, value);
            lightQueue.Enqueue(new LightNode(chunk, x - 1, y, z));
        }


        if (z == Chunk.Last)
        {
            if (IsBlockTransparent(chunk.ZPos, x, y, 0) &&
                chunk.ZPos.GetLight(x, y, 0).Compare(nextLightValue, out value))
            {
                chunk.ZPos.SetLight(x, y, 0, value);
                lightQueue.Enqueue(new LightNode(chunk.ZPos, x, y, 0));
            }

            chunk.ZPos.RecalculateMesh = true;
        }
        else if (IsBlockTransparent(chunk, x, y, z + 1) &&
            chunk.GetLight(x, y, z + 1).Compare(nextLightValue, out value))
        {
            chunk.SetLight(x, y, z + 1, value);
            lightQueue.Enqueue(new LightNode(chunk, x, y, z + 1));
        }


        if (z == 0)
        {
            if (IsBlockTransparent(chunk.ZNeg, x, y, Chunk.Last) &&
                chunk.ZNeg.GetLight(x, y, Chunk.Last).Compare(nextLightValue, out value))
            {
                chunk.ZNeg.SetLight(x, y, Chunk.Last, value);
                lightQueue.Enqueue(new LightNode(chunk.ZNeg, x, y, Chunk.Last));
            }

            chunk.ZNeg.RecalculateMesh = true;
        }
        else if (IsBlockTransparent(chunk, x, y, z - 1) &&
            chunk.GetLight(x, y, z - 1).Compare(nextLightValue, out value))
        {
            chunk.SetLight(x, y, z - 1, value);
            lightQueue.Enqueue(new LightNode(chunk, x, y, z - 1));
        }
    }

    public static FacesData<LightValue> GetFacesLight(FacesState visibleFaces, int x, int y, int z, Chunk chunk)
    {
        FacesData<LightValue> lightValues = new();

        if (visibleFaces.ZPos)
        {
            if (z == Chunk.Last)
            {
                lightValues.ZPos = chunk.ZPos.GetLight(x, y, 0);
            }
            else
            {
                lightValues.ZPos = chunk.GetLight(x, y, z + 1);
            }
        }

        if (visibleFaces.ZNeg)
        {
            if (z == 0)
            {
                lightValues.ZNeg = chunk.ZNeg.GetLight(x, y, Chunk.Last);
            }
            else
            {
                lightValues.ZNeg = chunk.GetLight(x, y, z - 1);
            }
        }

        if (visibleFaces.YPos)
        {
            if (y == Chunk.Last)
            {
                lightValues.YPos = chunk.YPos.GetLight(x, 0, z);
            }
            else
            {
                lightValues.YPos = chunk.GetLight(x, y + 1, z);
            }
        }

        if (visibleFaces.YNeg)
        {
            if (y == 0)
            {
                lightValues.YNeg = chunk.YNeg.GetLight(x, Chunk.Last, z);
            }
            else
            {
                lightValues.YNeg = chunk.GetLight(x, y - 1, z);
            }
        }


        if (visibleFaces.XPos)
        {
            if (x == Chunk.Last)
            {
                lightValues.XPos = chunk.XPos.GetLight(0, y, z);
            }
            else
            {
                lightValues.XPos = chunk.GetLight(x + 1, y, z);
            }
        }

        if (visibleFaces.XNeg)
        {
            if (x == 0)
            {
                lightValues.XNeg = chunk.XNeg.GetLight(Chunk.Last, y, z);
            }
            else
            {
                lightValues.XNeg = chunk.GetLight(x - 1, y, z);
            }
        }

        return lightValues;
    }

    static bool IsBlockTransparent(Chunk chunk, int x, int y, int z)
    {
        return chunk.IsBlockTransparent(new Vec3<int>(x, y, z));
    }

    static bool IsBlockTransparentSolid(Chunk chunk, int x, int y, int z)
    {
        return chunk.IsBlockTransparentSolid(new Vec3<int>(x, y, z));
    }
}
