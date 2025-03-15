using SharpCraft.MathUtilities;
using SharpCraft.Utilities;
using SharpCraft.World.Blocks;
using SharpCraft.World.Chunks;
using System.Collections.Generic;

namespace SharpCraft.World.Light
{

    public class LightSystem
    {
        readonly BlockMetadataProvider blockMetadata;
        readonly AdjacencyGraph adjacencyGraph;

        readonly Queue<LightNode> lightQueue = [];
        readonly Dictionary<Vector3I, Queue<LightNode>> leftoverLight = [];

        public LightSystem(BlockMetadataProvider blockMetadata, AdjacencyGraph adjacencyGraph)
        {
            this.blockMetadata = blockMetadata;
            this.adjacencyGraph = adjacencyGraph;
        }

        public void InitializeSkylight(ChunkAdjacency adjacency)
        {
            //Propagate light downward from a sky chunk
            if (!adjacency.YNeg.Root.IsEmpty)
            {
                for (int x = 0; x < Chunk.Size; x++)
                {
                    for (int z = 0; z < Chunk.Size; z++)
                    {
                        if (!adjacency.YNeg.Root[x, Chunk.Last, z].IsEmpty) continue;

                        adjacency.YNeg.Root.SetLight(x, Chunk.Last, z, LightValue.Sunlight);
                        lightQueue.Enqueue(new LightNode(adjacency.YNeg.Root, x, Chunk.Last, z));
                    }
                }
            }
        }

        public void InitializeLight(ChunkAdjacency adjacency)
        {
            Chunk chunk = adjacency.Root;

            //take light from upper chunk
            for (int x = 0; x < Chunk.Size; x++)
            {
                for (int z = 0; z < Chunk.Size; z++)
                {
                    if (!adjacency.YPos.Root[x, 0, z].IsEmpty) continue;

                    lightQueue.Enqueue(new LightNode(adjacency.YPos.Root, x, 0, z));
                }
            }

            foreach (Vector3I lightSourceIndex in chunk.GetLightSources())
            {
                int x = lightSourceIndex.X;
                int y = lightSourceIndex.Y;
                int z = lightSourceIndex.Z;

                LightValue light = chunk.GetLight(x, y, z);
                LightValue sourceLight = new(light.SkyValue, blockMetadata.GetLightSourceValue(chunk[lightSourceIndex.X, lightSourceIndex.Y, lightSourceIndex.Z].Value));

                chunk.SetLight(x, y, z, sourceLight);
                lightQueue.Enqueue(new LightNode(chunk, x, y, z));
            }
        }

        public void FloodFill()
        {
            List<Vector3I> deleted = [];
            Vector3I[] leftoverIndexes = [.. leftoverLight.Keys];

            foreach (Vector3I index in leftoverIndexes)
            {
                ChunkAdjacency adjacency = adjacencyGraph.GetAdjacency(index);
                if (adjacency != null)
                {
                    var queue = leftoverLight[index];

                    while (queue.Count > 0)
                    {
                        LightNode node = queue.Dequeue();

                        node.Chunk.RecalculateMesh = true;

                        ProcessLightNode(node);
                    }

                    deleted.Add(index);
                }
            }

            foreach (Vector3I index in deleted)
            {
                leftoverLight.Remove(index);
            }

            while (lightQueue.Count > 0)
            {
                LightNode node = lightQueue.Dequeue();

                node.Chunk.RecalculateMesh = true;

                ProcessLightNode(node);
            }
        }

        void ProcessLightNode(LightNode node)
        {
            node.Chunk.RecalculateMesh = true;

            ChunkAdjacency adjacency = adjacencyGraph.GetAdjacency(node.Chunk.Index);

            if (adjacency == null)
            {
                return;
            }

            if (adjacency.All())
            {
                Propagate(adjacency, node.X, node.Y, node.Z);
                return;
            }

            foreach (var nullChunkIndex in adjacency.GetNullChunksIndexes())
            {
                if (!leftoverLight.TryGetValue(nullChunkIndex, out var queue))
                {
                    queue = [];
                    queue.Enqueue(node);
                    leftoverLight.Add(nullChunkIndex, queue);
                }

                queue.Enqueue(node);
            }
        }

        public void UpdateLight(int x, int y, int z, ushort texture, ChunkAdjacency adjacency, bool sourceRemoved = false)
        {
            Chunk chunk = adjacency.Root;

            //Propagate light to an empty cell
            if (texture == Block.EmptyValue)
            {
                var (nodes, _) = GetNeighborLightValues(y, x, z, adjacency);

                foreach (var node in nodes.GetValues())
                {
                    lightQueue.Enqueue(node);
                }

                FloodFill(adjacency);

                if (sourceRemoved)
                {
                    lightQueue.Enqueue(new LightNode(chunk, x, y, z));
                    RemoveSource(adjacency);
                }
            }
            //Recalculate light after block placement
            else
            {
                bool sourceAdded = false;
                if (blockMetadata.IsLightSource(chunk[x, y, z].Value))
                {
                    LightValue light = chunk.GetLight(x, y, z);
                    LightValue sourceLight = new(light.SkyValue, blockMetadata.GetLightSourceValue(chunk[x, y, z].Value));
                    chunk.SetLight(x, y, z, sourceLight);
                    sourceAdded = true;
                }

                lightQueue.Enqueue(new LightNode(chunk, x, y, z));

                if (sourceAdded)
                {
                    FloodFill(adjacency);
                }
                else
                {
                    FloodRemove(adjacency);
                }
            }
        }

        void RemoveSource(ChunkAdjacency adjacency)
        {
            Chunk chunk = adjacency.Root;

            while (lightQueue.Count > 0)
            {
                LightNode node = lightQueue.Dequeue();

                LightValue light = node.GetLight();

                chunk.RecalculateMesh = true;

                node.SetLight(new(light.SkyValue, 0));

                var (nodes, lightValues) = GetNeighborLightValues(node.Y, node.X, node.Z, adjacency);

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

        void FloodRemove(ChunkAdjacency adjacency)
        {
            List<LightNode> lightList = [];

            while (lightQueue.Count > 0)
            {
                LightNode node = lightQueue.Dequeue();

                node.Chunk.RecalculateMesh = true;

                LightValue light = node.GetLight();

                if (TransparentSolid(node.GetBlock()))
                {
                    node.SetLight(light);
                }
                else
                {
                    node.SetLight(LightValue.Null);
                }

                var (nodes, lightValues) = GetNeighborLightValues(node.Y, node.X, node.Z, adjacency);

                Faces maxFace = Util.MaxFace(lightValues.GetValues());

                lightList.Add(nodes.GetValue(maxFace));

                for (int i = 0; i < 6; i++)
                {
                    Faces face = (Faces)i;
                    if (lightValues.GetValue(face) > LightValue.Null && (face == Faces.YNeg || nodes.GetValue(face).GetLight() < light) &&
                       IsBlockTransparent(nodes.GetValue(face).GetBlock()))
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

            FloodFill(adjacency);
        }

        (FacesData<LightNode> nodes, FacesData<LightValue> lightValues) GetNeighborLightValues(int y, int x, int z, ChunkAdjacency adjacency)
        {
            Chunk chunk = adjacency.Root;

            FacesData<LightNode> nodes = new();
            FacesData<LightValue> lightValues = new();
            
            if (y == Chunk.Last)
            {
                nodes.YPos = new LightNode(adjacency.YPos.Root, x, 0, z);
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
                nodes.YNeg = new LightNode(adjacency.YNeg.Root, x, Chunk.Last, z);
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
                nodes.XPos = new LightNode(adjacency.XPos.Root, 0, y, z);
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
                nodes.XNeg = new LightNode(adjacency.XNeg.Root, Chunk.Last, y, z);
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
                nodes.ZPos = new LightNode(adjacency.ZPos.Root, x, y, 0);
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
                nodes.ZNeg = new LightNode(adjacency.ZNeg.Root, x, y, Chunk.Last);
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

        void FloodFill(ChunkAdjacency adjacency)
        {
            while (lightQueue.Count > 0)
            {
                LightNode node = lightQueue.Dequeue();

                node.Chunk.RecalculateMesh = true;

                Propagate(adjacency, node.X, node.Y, node.Z);
            }
        }

        void Propagate(ChunkAdjacency adjacency, int x, int y, int z)
        {
            Chunk chunk = adjacency.Root;

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
                if (IsBlockTransparent(adjacency.YPos.Root[x, 0, z]) &&
                    adjacency.YPos.Root.GetLight(x, 0, z).Compare(nextLightValue, out value))
                {
                    adjacency.YPos.Root.SetLight(x, 0, z, value);
                    lightQueue.Enqueue(new LightNode(adjacency.YPos.Root, x, 0, z));
                }

                adjacency.YPos.Root.RecalculateMesh = true;
            }
            else if (IsBlockTransparent(chunk[x, y + 1, z]) &&
                chunk.GetLight(x, y + 1, z).Compare(nextLightValue, out value))
            {
                chunk.SetLight(x, y + 1, z, value);
                lightQueue.Enqueue(new LightNode(chunk, x, y + 1, z));
            }

            if (y == 0)
            {
                if (IsBlockTransparent(adjacency.YNeg.Root[x, Chunk.Last, z]) &&
                    adjacency.YNeg.Root.GetLight(x, Chunk.Last, z).Compare(lightValue.SubtractBlockValue(1), out value))
                {
                    adjacency.YNeg.Root.SetLight(x, Chunk.Last, z, value);
                    lightQueue.Enqueue(new LightNode(adjacency.YNeg.Root, x, Chunk.Last, z));
                }

                adjacency.YNeg.Root.RecalculateMesh = true;
            }
            else if (IsBlockTransparent(chunk[x, y - 1, z]) &&
                chunk.GetLight(x, y - 1, z).Compare(lightValue.SubtractBlockValue(1), out value))
            {
                chunk.SetLight(x, y - 1, z, value);
                lightQueue.Enqueue(new LightNode(chunk, x, y - 1, z));
            }


            if (x == Chunk.Last)
            {
                if (IsBlockTransparent(adjacency.XPos.Root[0, y, z]) &&
                    adjacency.XPos.Root.GetLight(0, y, z).Compare(nextLightValue, out value))
                {
                    adjacency.XPos.Root.SetLight(0, y, z, value);
                    lightQueue.Enqueue(new LightNode(adjacency.XPos.Root, 0, y, z));
                }

                adjacency.XPos.Root.RecalculateMesh = true;
            }
            else if (IsBlockTransparent(chunk[x + 1, y, z]) &&
                chunk.GetLight(x + 1, y, z).Compare(nextLightValue, out value))
            {
                chunk.SetLight(x + 1, y, z, value);
                lightQueue.Enqueue(new LightNode(chunk, x + 1, y, z));
            }


            if (x == 0)
            {
                if (IsBlockTransparent(adjacency.XNeg.Root[Chunk.Last, y, z]) &&
                    adjacency.XNeg.Root.GetLight(Chunk.Last, y, z).Compare(nextLightValue, out value))
                {
                    adjacency.XNeg.Root.SetLight(Chunk.Last, y, z, value);
                    lightQueue.Enqueue(new LightNode(adjacency.XNeg.Root, Chunk.Last, y, z));
                }

                adjacency.XNeg.Root.RecalculateMesh = true;
            }
            else if (IsBlockTransparent(chunk[x - 1, y, z]) &&
                chunk.GetLight(x - 1, y, z).Compare(nextLightValue, out value))
            {
                chunk.SetLight(x - 1, y, z, value);
                lightQueue.Enqueue(new LightNode(chunk, x - 1, y, z));
            }


            if (z == Chunk.Last)
            {
                if (IsBlockTransparent(adjacency.ZPos.Root[x, y, 0]) &&
                    adjacency.ZPos.Root.GetLight(x, y, 0).Compare(nextLightValue, out value))
                {
                    adjacency.ZPos.Root.SetLight(x, y, 0, value);
                    lightQueue.Enqueue(new LightNode(adjacency.ZPos.Root, x, y, 0));
                }

                adjacency.ZPos.Root.RecalculateMesh = true;
            }
            else if (IsBlockTransparent(chunk[x, y, z + 1]) &&
                chunk.GetLight(x, y, z + 1).Compare(nextLightValue, out value))
            {
                chunk.SetLight(x, y, z + 1, value);
                lightQueue.Enqueue(new LightNode(chunk, x, y, z + 1));
            }


            if (z == 0)
            {
                if (IsBlockTransparent(adjacency.ZNeg.Root[x, y, Chunk.Last]) &&
                    adjacency.ZNeg.Root.GetLight(x, y, Chunk.Last).Compare(nextLightValue, out value))
                {
                    adjacency.ZNeg.Root.SetLight(x, y, Chunk.Last, value);
                    lightQueue.Enqueue(new LightNode(adjacency.ZNeg.Root, x, y, Chunk.Last));
                }

                adjacency.ZNeg.Root.RecalculateMesh = true;
            }
            else if (IsBlockTransparent(chunk[x, y, z - 1]) &&
                chunk.GetLight(x, y, z - 1).Compare(nextLightValue, out value))
            {
                chunk.SetLight(x, y, z - 1, value);
                lightQueue.Enqueue(new LightNode(chunk, x, y, z - 1));
            }
        }

        public FacesData<LightValue> GetFacesLight(FacesState visibleFaces, int x, int y, int z, ChunkAdjacency adjacency)
        {
            FacesData<LightValue> lightValues = new();
            Chunk chunk = adjacency.Root;

            if (visibleFaces.ZPos)
            {
                if (z == Chunk.Last)
                {
                    lightValues.ZPos = adjacency.ZPos.Root.GetLight(x, y, 0);
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
                    lightValues.ZNeg = adjacency.ZNeg.Root.GetLight(x, y, Chunk.Last);
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
                    lightValues.YPos = adjacency.YPos.Root.GetLight(x, 0, z);
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
                    lightValues.YNeg = adjacency.YNeg.Root.GetLight(x, Chunk.Last, z);
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
                    lightValues.XPos = adjacency.XPos.Root.GetLight(0, y, z);
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
                    lightValues.XNeg = adjacency.XNeg.Root.GetLight(Chunk.Last, y, z);
                }
                else
                {
                    lightValues.XNeg = chunk.GetLight(x - 1, y, z);
                }
            }

            return lightValues;
        }

        bool IsBlockTransparent(Block block)
        {
            return block.IsEmpty || blockMetadata.IsBlockTransparent(block.Value);
        }

        bool TransparentSolid(Block block)
        {
            return !block.IsEmpty && blockMetadata.IsBlockTransparent(block.Value);
        }
    }
}
