using SharpCraft.Utility;
using System.Collections.Generic;

namespace SharpCraft.World
{
    public sealed partial class Chunk
    {
        readonly byte[][][] lightMap;
        readonly HashSet<Vector3I> lightSourceIndexes;

        readonly Queue<LightNode> lightQueue;
        readonly List<LightNode> lightList;

        readonly HashSet<Chunk> chunksToUpdate;

        public void InitializeLight(ChunkNeighbors neighbors)
        {
            bool skylight = true;
            bool blockLight = false;

            //Set the topmost layer to maximum light value
            for (int x = 0; x < SIZE; x++)
            {
                for (int z = 0; z < SIZE; z++)
                {
                    SetLight(HEIGHT - 1, x, z, 15, skylight);
                    lightQueue.Enqueue(new LightNode(this, x, HEIGHT - 1, z));
                }
            }

            FloodFill(skylight, neighbors);

            foreach (Vector3I lightSourceIndex in lightSourceIndexes)
            {
                int y = lightSourceIndex.Y;
                int x = lightSourceIndex.X;
                int z = lightSourceIndex.Z;

                SetLight(y, x, z, blockMetadata.GetLightSourceValue(this[x, y, z].Value), blockLight);
                lightQueue.Enqueue(new LightNode(this, x, y, z));
            }

            FloodFill(blockLight, neighbors);
        }

        public void UpdateLight(int y, int x, int z, ushort texture, ChunkNeighbors neighbors, bool sourceRemoved = false)
        {
            bool skylight = true;
            bool blockLight = false;

            //Propagate light to an empty cell
            if (texture == Block.EmptyValue)
            {
                var (nodes, lightValues) = GetNeighborLightValues(y, x, z, skylight, neighbors);
                lightQueue.Enqueue(nodes[Util.ArgMax(lightValues)]);
                Repropagate(skylight, neighbors);

                (nodes, lightValues) = GetNeighborLightValues(y, x, z, blockLight, neighbors);
                lightQueue.Enqueue(nodes[Util.ArgMax(lightValues)]);
                Repropagate(blockLight, neighbors);

                if (sourceRemoved)
                {
                    lightQueue.Enqueue(new LightNode(this, x, y, z));
                    RemoveSource(neighbors);
                }
            }

            //Recalculate light after block placement
            else
            {
                bool sourceAdded = false;
                if (blockMetadata.IsLightSource(this[x, y, z].Value))
                {
                    SetLight(y, x, z, blockMetadata.GetLightSourceValue(this[x, y, z].Value), blockLight);
                    sourceAdded = true;
                }

                lightQueue.Enqueue(new LightNode(this, x, y, z));
                FloodRemove(skylight, neighbors);

                lightQueue.Enqueue(new LightNode(this, x, y, z));
                if (sourceAdded)
                {
                    Repropagate(blockLight, neighbors);
                }
                else
                {
                    FloodRemove(blockLight, neighbors);
                }
            }
        }

        void RemoveSource(ChunkNeighbors neighbors)
        {
            bool blockLight = false;

            while (lightQueue.Count > 0)
            {
                LightNode node = lightQueue.Dequeue();

                byte light = node.GetLight(blockLight);

                chunksToUpdate.Add(node.Chunk);

                node.SetLight(0, blockLight);

                var (nodes, lightValues) = GetNeighborLightValues(node.Y, node.X, node.Z, blockLight, neighbors);

                for (int i = 0; i < 6; i++)
                {
                    if (lightValues[i] == (byte)(light - 1))
                    {
                        lightQueue.Enqueue(nodes[i]);
                    }
                }
            }

            foreach (Chunk chunk in chunksToUpdate)
            {
                chunk.UpdateMesh = true;
            }

            chunksToUpdate.Clear();
        }

        void FloodRemove(bool channel, ChunkNeighbors neighbors)
        {
            while (lightQueue.Count > 0)
            {
                LightNode node = lightQueue.Dequeue();

                chunksToUpdate.Add(node.Chunk);

                byte light = node.GetLight(channel);

                if (TransparentSolid(node.GetBlock()))
                {
                    node.SetLight(light, channel);
                }
                else
                {
                    node.SetLight(0, channel);
                }

                var (nodes, lightValues) = GetNeighborLightValues(node.Y, node.X, node.Z, channel, neighbors);
                int max = Util.ArgMax(lightValues);

                lightList.Add(nodes[max]);

                for (int i = 0; i < 6; i++)
                {
                    if (lightValues[i] > 0 && (i == 1 || lightValues[i] < light) &&
                       IsBlockTransparent(nodes[i].GetBlock()))
                    {
                        lightQueue.Enqueue(nodes[i]);
                    }
                }
            }

            for (int i = 0; i < lightList.Count; i++)
            {
                if (lightList[i].GetLight(channel) > 1)
                {
                    lightQueue.Enqueue(lightList[i]);
                }
            }

            lightList.Clear();

            Repropagate(channel, neighbors);
        }

        void Repropagate(bool channel, ChunkNeighbors neighbors)
        {
            FloodFill(channel, neighbors, repropagate: true);

            foreach (Chunk chunk in chunksToUpdate)
            {
                chunk.UpdateMesh = true;
            }

            chunksToUpdate.Clear();
        }

        (LightNode[] nodes, byte[] lightValues) GetNeighborLightValues(int y, int x, int z, bool channel, ChunkNeighbors neighbors)
        {
            LightNode[] nodes = new LightNode[6];
            byte[] lightValues = new byte[6];

            if (y + 1 < HEIGHT)
            {
                nodes[0] = new LightNode(this, x, y + 1, z);
                lightValues[0] = GetLight(y + 1, x, z, channel);
            }

            if (y > 0)
            {
                nodes[1] = new LightNode(this, x, y - 1, z);
                lightValues[1] = GetLight(y - 1, x, z, channel);
            }


            if (x == LAST)
            {
                nodes[2] = new LightNode(neighbors.XNeg, 0, y, z);
                lightValues[2] = neighbors.XNeg.GetLight(y, 0, z, channel);
            }
            else
            {
                nodes[2] = new LightNode(this, x + 1, y, z);
                lightValues[2] = GetLight(y, x + 1, z, channel);
            }

            if (x == 0)
            {
                nodes[3] = new LightNode(neighbors.XPos, LAST, y, z);
                lightValues[3] = neighbors.XPos.GetLight(y, LAST, z, channel);
            }
            else
            {
                nodes[3] = new LightNode(this, x - 1, y, z);
                lightValues[3] = GetLight(y, x - 1, z, channel);
            }


            if (z == LAST)
            {
                nodes[4] = new LightNode(neighbors.ZNeg, x, y, 0);
                lightValues[4] = neighbors.ZNeg.GetLight(y, x, 0, channel);
            }
            else
            {
                nodes[4] = new LightNode(this, x, y, z + 1);
                lightValues[4] = GetLight(y, x, z + 1, channel);
            }

            if (z == 0)
            {
                nodes[5] = new LightNode(neighbors.ZPos, x, y, LAST);
                lightValues[5] = neighbors.ZPos.GetLight(y, x, LAST, channel);
            }
            else
            {
                nodes[5] = new LightNode(this, x, y, z - 1);
                lightValues[5] = GetLight(y, x, z - 1, channel);
            }

            return (nodes, lightValues);
        }

        void FloodFill(bool channel, ChunkNeighbors neighbors, bool repropagate = false)
        {
            LightNode node;
            while (lightQueue.Count > 0)
            {
                node = lightQueue.Dequeue();

                if (repropagate)
                {
                    chunksToUpdate.Add(node.Chunk);
                }

                Propagate(neighbors, channel, node.Y, node.X, node.Z);
            }
        }

        void Propagate(ChunkNeighbors neighbors, bool channel, int y, int x, int z)
        {
            Chunk chunk = neighbors.Chunk;

            byte light = chunk.GetLight(y, x, z, channel);
            byte nextLight;

            if (light == 1)
            {
                return;
            }
            else
            {
                nextLight = (byte)(light - 1);
            }


            if (y + 1 < HEIGHT &&
                IsBlockTransparent(chunk[x, y + 1, z]) &&
                CompareValues(chunk.GetLight(y + 1, x, z, channel), light))
            {
                chunk.SetLight(y + 1, x, z, nextLight, channel);
                lightQueue.Enqueue(new LightNode(chunk, x, y + 1, z));
            }

            if (y > 0 &&
                IsBlockTransparent(chunk[x, y - 1, z]) &&
                CompareValues(chunk.GetLight(y - 1, x, z, channel), light, amount: (byte)(channel ? 0 : 1)))
            {
                if (channel)
                {
                    chunk.SetLight(y - 1, x, z, light, channel);
                    lightQueue.Enqueue(new LightNode(chunk, x, y - 1, z));
                }
                else
                {
                    chunk.SetLight(y - 1, x, z, nextLight, channel);
                    lightQueue.Enqueue(new LightNode(chunk, x, y - 1, z));
                }
            }


            if (x == LAST)
            {
                if (neighbors.XNeg != null &&
                    IsBlockTransparent(neighbors.XNeg[0, y, z]) &&
                    CompareValues(neighbors.XNeg.GetLight(y, 0, z, channel), light))
                {
                    neighbors.XNeg.SetLight(y, 0, z, nextLight, channel);
                    lightQueue.Enqueue(new LightNode(neighbors.XNeg, 0, y, z));
                }
            }
            else if (IsBlockTransparent(chunk[x + 1, y, z]) &&
                CompareValues(chunk.GetLight(y, x + 1, z, channel), light))
            {
                chunk.SetLight(y, x + 1, z, nextLight, channel);
                lightQueue.Enqueue(new LightNode(chunk, x + 1, y, z));
            }


            if (x == 0)
            {
                if (neighbors.XPos != null &&
                    IsBlockTransparent(neighbors.XPos[LAST, y, z]) &&
                    CompareValues(neighbors.XPos.GetLight(y, LAST, z, channel), light))
                {
                    neighbors.XPos.SetLight(y, LAST, z, nextLight, channel);
                    lightQueue.Enqueue(new LightNode(neighbors.XPos, LAST, y, z));
                }
            }
            else if (IsBlockTransparent(chunk[x - 1, y, z]) &&
                CompareValues(chunk.GetLight(y, x - 1, z, channel), light))
            {
                chunk.SetLight(y, x - 1, z, nextLight, channel);
                lightQueue.Enqueue(new LightNode(chunk, x - 1, y, z));
            }


            if (z == LAST)
            {
                if (neighbors.ZNeg != null &&
                    IsBlockTransparent(neighbors.ZNeg[x, y, 0]) &&
                    CompareValues(neighbors.ZNeg.GetLight(y, x, 0, channel), light))
                {
                    neighbors.ZNeg.SetLight(y, x, 0, nextLight, channel);
                    lightQueue.Enqueue(new LightNode(neighbors.ZNeg, x, y, 0));
                }
            }
            else if (IsBlockTransparent(chunk[x, y, z + 1]) &&
                CompareValues(chunk.GetLight(y, x, z + 1, channel), light))
            {
                chunk.SetLight(y, x, z + 1, nextLight, channel);
                lightQueue.Enqueue(new LightNode(chunk, x, y, z + 1));
            }


            if (z == 0)
            {
                if (neighbors.ZPos != null &&
                    IsBlockTransparent(neighbors.ZPos[x, y, LAST]) &&
                    CompareValues(neighbors.ZPos.GetLight(y, x, LAST, channel), light))
                {
                    neighbors.ZPos.SetLight(y, x, LAST, nextLight, channel);
                    lightQueue.Enqueue(new LightNode(neighbors.ZPos, x, y, LAST));
                }
            }
            else if (IsBlockTransparent(chunk[x, y, z - 1]) &&
                CompareValues(chunk.GetLight(y, x, z - 1, channel), light))
            {
                chunk.SetLight(y, x, z - 1, nextLight, channel);
                lightQueue.Enqueue(new LightNode(chunk, x, y, z - 1));
            }
        }

        FacesData<byte> GetFacesLight(FacesState visibleFaces, int y, int x, int z, ChunkNeighbors neighbors)
        {
            FacesData<byte> lightValues = new();

            if (visibleFaces.ZPos)
            {
                if (z == LAST)
                {
                    if (neighbors.ZNeg != null)
                        lightValues.ZPos = neighbors.ZNeg.lightMap[y][x][0];
                }
                else
                {
                    lightValues.ZPos = lightMap[y][x][z + 1];
                }
            }

            if (visibleFaces.ZNeg)
            {
                if (z == 0)
                {
                    if (neighbors.ZPos != null)
                        lightValues.ZNeg = neighbors.ZPos.lightMap[y][x][LAST];
                }
                else
                {
                    lightValues.ZNeg = lightMap[y][x][z - 1];
                }
            }

            if (visibleFaces.YPos)
            {
                lightValues.YPos = lightMap[y + 1][x][z];
            }

            if (visibleFaces.YNeg)
            {
                lightValues.YNeg = lightMap[y - 1][x][z];
            }


            if (visibleFaces.XPos)
            {
                if (x == LAST)
                {
                    if (neighbors.XNeg != null)
                        lightValues.XPos = neighbors.XNeg.lightMap[y][0][z];
                }
                else
                {
                    lightValues.XPos = lightMap[y][x + 1][z];
                }
            }

            if (visibleFaces.XNeg)
            {
                if (x == 0)
                {
                    if (neighbors.XPos != null)
                        lightValues.XNeg = neighbors.XPos.lightMap[y][LAST][z];
                }
                else
                {
                    lightValues.XNeg = lightMap[y][x - 1][z];
                }
            }

            return lightValues;
        }

        public void AddLightSource(int y, int x, int z)
        {
            lightSourceIndexes.Add(new Vector3I(y, x, z));
        }

        public void SetLight(int y, int x, int z, byte value, bool skylight)
        {
            if (skylight)
            {
                lightMap[y][x][z] = (byte)((lightMap[y][x][z] & 0xF) | (value << 4));
            }
            else
            {
                lightMap[y][x][z] = (byte)((lightMap[y][x][z] & 0xF0) | value);
            }
        }

        public byte GetLight(int y, int x, int z, bool skylight)
        {
            if (skylight)
            {
                return (byte)((lightMap[y][x][z] >> 4) & 0xF);
            }

            return (byte)(lightMap[y][x][z] & 0xF);
        }

        bool CompareValues(byte val, byte light, byte amount = 1)
        {
            return val + amount < light;
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
