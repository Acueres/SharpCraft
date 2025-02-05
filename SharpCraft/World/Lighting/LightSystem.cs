using System.Collections.Generic;
using SharpCraft.Utility;
using SharpCraft.World.Blocks;
using SharpCraft.World.Chunks;

namespace SharpCraft.World.Light
{

    public class LightSystem
    {
        readonly BlockMetadataProvider blockMetadata;

        readonly Queue<LightNode> lightQueue = [];
        readonly Dictionary<Vector3I, Queue<LightNode>> leftoverLight = [];

        public LightSystem(BlockMetadataProvider blockMetadata)
        {
            this.blockMetadata = blockMetadata;
        }

        public void InitializeSkylight(ChunkNeighbors neighbors)
        {
            //Propagate light downward from a sky chunk
            if (neighbors.YNeg is Chunk yNeg)
            {
                for (int x = 0; x < Chunk.Size; x++)
                {
                    for (int z = 0; z < Chunk.Size; z++)
                    {
                        if (!yNeg[x, Chunk.Last, z].IsEmpty) continue;

                        yNeg.SetLight(x, Chunk.Last, z, LightValue.Sunlight);
                        lightQueue.Enqueue(new LightNode(yNeg, x, Chunk.Last, z));
                    }
                }
            }
        }

        public void InitializeLight(ChunkNeighbors neighbors)
        {
            IChunk chunk = neighbors.Chunk;

            //take light from upper chunk
            for (int x = 0; x < Chunk.Size; x++)
            {
                for (int z = 0; z < Chunk.Size; z++)
                {
                    if (!neighbors.YPos[x, 0, z].IsEmpty) continue;

                    lightQueue.Enqueue(new LightNode(neighbors.YPos, x, 0, z));
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

        public void FloodFill(Dictionary<Vector3I, ChunkNeighbors> neighborsMap)
        {
            List<Vector3I> deleted = [];
            Vector3I[] leftoverIndexes = [.. leftoverLight.Keys];

            foreach (Vector3I index in leftoverIndexes)
            {
                if (neighborsMap.ContainsKey(index))
                {
                    var queue = leftoverLight[index];

                    while (queue.Count > 0)
                    {
                        LightNode node = queue.Dequeue();

                        node.Chunk.RecalculateMesh = true;

                        ProcessLightNode(node, neighborsMap);
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

                ProcessLightNode(node, neighborsMap);
            }
        }

        void ProcessLightNode(LightNode node, Dictionary<Vector3I, ChunkNeighbors> neighborsMap)
        {
            node.Chunk.RecalculateMesh = true;

            if (!neighborsMap.TryGetValue(node.Chunk.Index, out ChunkNeighbors neighbors))
            {
                return;
            }

            if (neighbors.All())
            {
                Propagate(neighbors, node.X, node.Y, node.Z);
                return;
            }

            foreach (var nullChunkIndex in neighbors.GetNullChunksIndexes())
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

        public void UpdateLight(int x, int y, int z, ushort texture, ChunkNeighbors neighbors, bool sourceRemoved = false)
        {
            IChunk chunk = neighbors.Chunk;

            //Propagate light to an empty cell
            if (texture == Block.EmptyValue)
            {
                var (nodes, _) = GetNeighborLightValues(y, x, z, neighbors);

                foreach (var node in nodes.GetValues())
                {
                    lightQueue.Enqueue(node);
                }

                FloodFill(neighbors);

                if (sourceRemoved)
                {
                    lightQueue.Enqueue(new LightNode(chunk, x, y, z));
                    RemoveSource(neighbors);
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
                    FloodFill(neighbors);
                }
                else
                {
                    FloodRemove(neighbors);
                }
            }
        }

        void RemoveSource(ChunkNeighbors neighbors)
        {
            IChunk chunk = neighbors.Chunk;

            while (lightQueue.Count > 0)
            {
                LightNode node = lightQueue.Dequeue();

                LightValue light = node.GetLight();

                chunk.RecalculateMesh = true;

                node.SetLight(new(light.SkyValue, 0));

                var (nodes, lightValues) = GetNeighborLightValues(node.Y, node.X, node.Z, neighbors);

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

        void FloodRemove(ChunkNeighbors neighbors)
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

                var (nodes, lightValues) = GetNeighborLightValues(node.Y, node.X, node.Z, neighbors);

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

            FloodFill(neighbors);
        }

        (FacesData<LightNode> nodes, FacesData<LightValue> lightValues) GetNeighborLightValues(int y, int x, int z, ChunkNeighbors neighbors)
        {
            IChunk chunk = neighbors.Chunk;

            FacesData<LightNode> nodes = new();
            FacesData<LightValue> lightValues = new();
            
            if (y == Chunk.Last)
            {
                nodes.YPos = new LightNode(neighbors.YPos, x, 0, z);
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
                nodes.YNeg = new LightNode(neighbors.YNeg, x, Chunk.Last, z);
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
                nodes.XPos = new LightNode(neighbors.XPos, 0, y, z);
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
                nodes.XNeg = new LightNode(neighbors.XNeg, Chunk.Last, y, z);
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
                nodes.ZPos = new LightNode(neighbors.ZPos, x, y, 0);
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
                nodes.ZNeg = new LightNode(neighbors.ZNeg, x, y, Chunk.Last);
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

        void FloodFill(ChunkNeighbors neighbors)
        {
            while (lightQueue.Count > 0)
            {
                LightNode node = lightQueue.Dequeue();

                node.Chunk.RecalculateMesh = true;

                Propagate(neighbors, node.X, node.Y, node.Z);
            }
        }

        void Propagate(ChunkNeighbors neighbors, int x, int y, int z)
        {
            IChunk chunk = neighbors.Chunk;

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
                if (IsBlockTransparent(neighbors.YPos[x, 0, z]) &&
                    neighbors.YPos.GetLight(x, 0, z).Compare(nextLightValue, out value))
                {
                    neighbors.YPos.SetLight(x, 0, z, value);
                    lightQueue.Enqueue(new LightNode(neighbors.YPos, x, 0, z));
                }

                neighbors.YPos.RecalculateMesh = true;
            }
            else if (IsBlockTransparent(chunk[x, y + 1, z]) &&
                chunk.GetLight(x, y + 1, z).Compare(nextLightValue, out value))
            {
                chunk.SetLight(x, y + 1, z, value);
                lightQueue.Enqueue(new LightNode(chunk, x, y + 1, z));
            }

            if (y == 0)
            {
                if (IsBlockTransparent(neighbors.YNeg[x, Chunk.Last, z]) &&
                    neighbors.YNeg.GetLight(x, Chunk.Last, z).Compare(lightValue.SubtractBlockValue(1), out value))
                {
                    neighbors.YNeg.SetLight(x, Chunk.Last, z, value);
                    lightQueue.Enqueue(new LightNode(neighbors.YNeg, x, Chunk.Last, z));
                }

                neighbors.YNeg.RecalculateMesh = true;
            }
            else if (IsBlockTransparent(chunk[x, y - 1, z]) &&
                chunk.GetLight(x, y - 1, z).Compare(lightValue.SubtractBlockValue(1), out value))
            {
                chunk.SetLight(x, y - 1, z, value);
                lightQueue.Enqueue(new LightNode(chunk, x, y - 1, z));
            }


            if (x == Chunk.Last)
            {
                if (IsBlockTransparent(neighbors.XPos[0, y, z]) &&
                    neighbors.XPos.GetLight(0, y, z).Compare(nextLightValue, out value))
                {
                    neighbors.XPos.SetLight(0, y, z, value);
                    lightQueue.Enqueue(new LightNode(neighbors.XPos, 0, y, z));
                }

                neighbors.XPos.RecalculateMesh = true;
            }
            else if (IsBlockTransparent(chunk[x + 1, y, z]) &&
                chunk.GetLight(x + 1, y, z).Compare(nextLightValue, out value))
            {
                chunk.SetLight(x + 1, y, z, value);
                lightQueue.Enqueue(new LightNode(chunk, x + 1, y, z));
            }


            if (x == 0)
            {
                if (IsBlockTransparent(neighbors.XNeg[Chunk.Last, y, z]) &&
                    neighbors.XNeg.GetLight(Chunk.Last, y, z).Compare(nextLightValue, out value))
                {
                    neighbors.XNeg.SetLight(Chunk.Last, y, z, value);
                    lightQueue.Enqueue(new LightNode(neighbors.XNeg, Chunk.Last, y, z));
                }

                neighbors.XNeg.RecalculateMesh = true;
            }
            else if (IsBlockTransparent(chunk[x - 1, y, z]) &&
                chunk.GetLight(x - 1, y, z).Compare(nextLightValue, out value))
            {
                chunk.SetLight(x - 1, y, z, value);
                lightQueue.Enqueue(new LightNode(chunk, x - 1, y, z));
            }


            if (z == Chunk.Last)
            {
                if (IsBlockTransparent(neighbors.ZPos[x, y, 0]) &&
                    neighbors.ZPos.GetLight(x, y, 0).Compare(nextLightValue, out value))
                {
                    neighbors.ZPos.SetLight(x, y, 0, value);
                    lightQueue.Enqueue(new LightNode(neighbors.ZPos, x, y, 0));
                }

                neighbors.ZPos.RecalculateMesh = true;
            }
            else if (IsBlockTransparent(chunk[x, y, z + 1]) &&
                chunk.GetLight(x, y, z + 1).Compare(nextLightValue, out value))
            {
                chunk.SetLight(x, y, z + 1, value);
                lightQueue.Enqueue(new LightNode(chunk, x, y, z + 1));
            }


            if (z == 0)
            {
                if (IsBlockTransparent(neighbors.ZNeg[x, y, Chunk.Last]) &&
                    neighbors.ZNeg.GetLight(x, y, Chunk.Last).Compare(nextLightValue, out value))
                {
                    neighbors.ZNeg.SetLight(x, y, Chunk.Last, value);
                    lightQueue.Enqueue(new LightNode(neighbors.ZNeg, x, y, Chunk.Last));
                }

                neighbors.ZNeg.RecalculateMesh = true;
            }
            else if (IsBlockTransparent(chunk[x, y, z - 1]) &&
                chunk.GetLight(x, y, z - 1).Compare(nextLightValue, out value))
            {
                chunk.SetLight(x, y, z - 1, value);
                lightQueue.Enqueue(new LightNode(chunk, x, y, z - 1));
            }
        }

        public FacesData<LightValue> GetFacesLight(FacesState visibleFaces, int y, int x, int z, ChunkNeighbors neighbors)
        {
            FacesData<LightValue> lightValues = new();
            IChunk chunk = neighbors.Chunk;

            if (visibleFaces.ZPos)
            {
                if (z == Chunk.Last)
                {
                    lightValues.ZPos = neighbors.ZPos.GetLight(x, y, 0);
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
                    lightValues.ZNeg = neighbors.ZNeg.GetLight(x, y, Chunk.Last);
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
                    lightValues.YPos = neighbors.YPos.GetLight(x, 0, z);
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
                    lightValues.YNeg = neighbors.YNeg.GetLight(x, Chunk.Last, z);
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
                    lightValues.XPos = neighbors.XPos.GetLight(0, y, z);
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
                    lightValues.XNeg = neighbors.XNeg.GetLight(Chunk.Last, y, z);
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
