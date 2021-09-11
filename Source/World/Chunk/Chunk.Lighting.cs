using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.ObjectModel;

using SharpCraft.Utility;
using SharpCraft.Models;

namespace SharpCraft.World
{
    public sealed partial class Chunk
    {
        byte[][][] lightMap;
        List<BlockIndex> lightSources;

        Queue<LightNode> lightQueue;
        List<LightNode> lightList;

        LightNode[] nodes;
        byte[] lightValues;

        ReadOnlyDictionary<ushort, byte> lightSourceValues;

        IList<bool> isTransparent;
        IList<bool> isLightSource;

        HashSet<Chunk> chunksToUpdate;

        void InitializeLight()
        {
            bool skylight = true;
            bool blockLight = false;

            //Set the topmost layer to maximum light value
            for (int x = 0; x < size; x++)
            {
                for (int z = 0; z < size; z++)
                {
                    SetLight(height - 1, x, z, 15, skylight);
                    lightQueue.Enqueue(new LightNode(this, x, height - 1, z));
                }
            }

            FloodFill(skylight);

            for (int i = 0; i < lightSources.Count; i++)
            {
                int y = lightSources[i].Y;
                int x = lightSources[i].X;
                int z = lightSources[i].Z;

                SetLight(y, x, z, lightSourceValues[(ushort)blocks[y][x][z]], blockLight);
                lightQueue.Enqueue(new LightNode(this, x, y, z));
            }

            FloodFill(blockLight);
        }

        public void UpdateLight(int y, int x, int z, ushort? texture, bool sourceRemoved = false)
        {
            bool skylight = true;
            bool blockLight = false;

            //Propagate light to an empty cell
            if (texture is null)
            {
                GetNeighborValues(y, x, z, skylight);
                lightQueue.Enqueue(nodes[Util.ArgMax(lightValues)]);
                Repropagate(skylight);

                GetNeighborValues(y, x, z, blockLight);
                lightQueue.Enqueue(nodes[Util.ArgMax(lightValues)]);
                Repropagate(blockLight);

                if (sourceRemoved)
                {
                    lightQueue.Enqueue(new LightNode(this, x, y, z));
                    RemoveSource();
                }
            }

            //Recalculate light after block placement
            else
            {
                bool sourceAdded = false;
                if (isLightSource[(int)blocks[y][x][z]])
                {
                    SetLight(y, x, z, lightSourceValues[(ushort)blocks[y][x][z]], blockLight);
                    sourceAdded = true;
                }

                lightQueue.Enqueue(new LightNode(this, x, y, z));
                FloodRemove(skylight);

                lightQueue.Enqueue(new LightNode(this, x, y, z));
                if (sourceAdded)
                {
                    Repropagate(blockLight);
                }
                else
                {
                    FloodRemove(blockLight);
                }
            }
        }

        void RemoveSource()
        {
            bool blockLight = false;

            while (lightQueue.Count > 0)
            {
                LightNode node = lightQueue.Dequeue();

                byte light = node.GetLight(blockLight);

                chunksToUpdate.Add(node.Chunk);

                node.SetLight(0, blockLight);

                GetNeighborValues(node.Y, node.X, node.Z, blockLight);

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

        void FloodRemove(bool channel)
        {
            while (lightQueue.Count > 0)
            {
                LightNode node = lightQueue.Dequeue();

                chunksToUpdate.Add(node.Chunk);

                byte light = node.GetLight(channel);

                if (TransparentSolid(node.GetTexture()))
                {
                    node.SetLight(light, channel);
                }
                else
                {
                    node.SetLight(0, channel);
                }

                GetNeighborValues(node.Y, node.X, node.Z, channel);
                int max = Util.ArgMax(lightValues);

                lightList.Add(nodes[max]);

                for (int i = 0; i < 6; i++)
                {
                    if (lightValues[i] > 0 && (i == 1 || lightValues[i] < light) &&
                       Transparent(nodes[i].GetTexture()))
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

            Repropagate(channel);
        }

        void Repropagate(bool channel)
        {
            FloodFill(channel, repropagate: true);

            foreach (Chunk chunk in chunksToUpdate)
            {
                chunk.UpdateMesh = true;
            }

            chunksToUpdate.Clear();
        }

        void GetNeighborValues(int y, int x, int z, bool channel)
        {
            Array.Clear(nodes, 0, 6);
            Array.Clear(lightValues, 0, 6);

            if (y + 1 < height)
            {
                nodes[0] = new LightNode(this, x, y + 1, z);
                lightValues[0] = GetLight(y + 1, x, z, channel);
            }

            if (y > 0)
            {
                nodes[1] = new LightNode(this, x, y - 1, z);
                lightValues[1] = GetLight(y - 1, x, z, channel);
            }


            if (x == last)
            {
                nodes[2] = new LightNode(Neighbors.XNeg, 0, y, z);
                lightValues[2] = Neighbors.XNeg.GetLight(y, 0, z, channel);
            }
            else
            {
                nodes[2] = new LightNode(this, x + 1, y, z);
                lightValues[2] = GetLight(y, x + 1, z, channel);
            }

            if (x == 0)
            {
                nodes[3] = new LightNode(Neighbors.XPos, last, y, z);
                lightValues[3] = Neighbors.XPos.GetLight(y, last, z, channel);
            }
            else
            {
                nodes[3] = new LightNode(this, x - 1, y, z);
                lightValues[3] = GetLight(y, x - 1, z, channel);
            }


            if (z == last)
            {
                nodes[4] = new LightNode(Neighbors.ZNeg, x, y, 0);
                lightValues[4] = Neighbors.ZNeg.GetLight(y, x, 0, channel);
            }
            else
            {
                nodes[4] = new LightNode(this, x, y, z + 1);
                lightValues[4] = GetLight(y, x, z + 1, channel);
            }

            if (z == 0)
            {
                nodes[5] = new LightNode(Neighbors.ZPos, x, y, last);
                lightValues[5] = Neighbors.ZPos.GetLight(y, x, last, channel);
            }
            else
            {
                nodes[5] = new LightNode(this, x, y, z - 1);
                lightValues[5] = GetLight(y, x, z - 1, channel);
            }
        }

        void FloodFill(bool channel, bool repropagate = false)
        {
            LightNode node;
            while (lightQueue.Count > 0)
            {
                node = lightQueue.Dequeue();

                if (repropagate)
                {
                    chunksToUpdate.Add(node.Chunk);
                }

                Propagate(node.Chunk, channel, node.Y, node.X, node.Z);
            }
        }

        void Propagate(Chunk chunk, bool channel, int y, int x, int z)
        {
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


            if (y + 1 < height &&
                Transparent(chunk.blocks[y + 1][x][z]) &&
                CompareValues(chunk.GetLight(y + 1, x, z, channel), light))
            {
                chunk.SetLight(y + 1, x, z, nextLight, channel);
                lightQueue.Enqueue(new LightNode(chunk, x, y + 1, z));
            }

            if (y > 0 &&
                Transparent(chunk.blocks[y - 1][x][z]) &&
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


            if (x == last)
            {
                if (chunk.Neighbors.XNeg != null &&
                    Transparent(chunk.Neighbors.XNeg.blocks[y][0][z]) &&
                    CompareValues(chunk.Neighbors.XNeg.GetLight(y, 0, z, channel), light))
                {
                    chunk.Neighbors.XNeg.SetLight(y, 0, z, nextLight, channel);
                    lightQueue.Enqueue(new LightNode(chunk.Neighbors.XNeg, 0, y, z));
                }
            }
            else if (Transparent(chunk.blocks[y][x + 1][z]) &&
                CompareValues(chunk.GetLight(y, x + 1, z, channel), light))
            {
                chunk.SetLight(y, x + 1, z, nextLight, channel);
                lightQueue.Enqueue(new LightNode(chunk, x + 1, y, z));
            }


            if (x == 0)
            {
                if (chunk.Neighbors.XPos != null &&
                    Transparent(chunk.Neighbors.XPos.blocks[y][last][z]) &&
                    CompareValues(chunk.Neighbors.XPos.GetLight(y, last, z, channel), light))
                {
                    chunk.Neighbors.XPos.SetLight(y, last, z, nextLight, channel);
                    lightQueue.Enqueue(new LightNode(chunk.Neighbors.XPos, last, y, z));
                }
            }
            else if (Transparent(chunk.blocks[y][x - 1][z]) &&
                CompareValues(chunk.GetLight(y, x - 1, z, channel), light))
            {
                chunk.SetLight(y, x - 1, z, nextLight, channel);
                lightQueue.Enqueue(new LightNode(chunk, x - 1, y, z));
            }


            if (z == last)
            {
                if (chunk.Neighbors.ZNeg != null &&
                    Transparent(chunk.Neighbors.ZNeg.blocks[y][x][0]) &&
                    CompareValues(chunk.Neighbors.ZNeg.GetLight(y, x, 0, channel), light))
                {
                    chunk.Neighbors.ZNeg.SetLight(y, x, 0, nextLight, channel);
                    lightQueue.Enqueue(new LightNode(chunk.Neighbors.ZNeg, x, y, 0));
                }
            }
            else if (Transparent(chunk.blocks[y][x][z + 1]) &&
                CompareValues(chunk.GetLight(y, x, z + 1, channel), light))
            {
                chunk.SetLight(y, x, z + 1, nextLight, channel);
                lightQueue.Enqueue(new LightNode(chunk, x, y, z + 1));
            }


            if (z == 0)
            {
                if (chunk.Neighbors.ZPos != null &&
                    Transparent(chunk.Neighbors.ZPos.blocks[y][x][last]) &&
                    CompareValues(chunk.Neighbors.ZPos.GetLight(y, x, last, channel), light))
                {
                    chunk.Neighbors.ZPos.SetLight(y, x, last, nextLight, channel);
                    lightQueue.Enqueue(new LightNode(chunk.Neighbors.ZPos, x, y, last));
                }
            }
            else if (Transparent(chunk.blocks[y][x][z - 1]) &&
                CompareValues(chunk.GetLight(y, x, z - 1, channel), light))
            {
                chunk.SetLight(y, x, z - 1, nextLight, channel);
                lightQueue.Enqueue(new LightNode(chunk, x, y, z - 1));
            }
        }

        byte[] GetFacesLight(byte[] lightValues, bool[] facesVisible, int y, int x, int z)
        {
            if (facesVisible[0])
            {
                if (z == last)
                {
                    if (Neighbors.ZNeg != null)
                        lightValues[0] = Neighbors.ZNeg.lightMap[y][x][0];
                }
                else
                {
                    lightValues[0] = lightMap[y][x][z + 1];
                }
            }

            if (facesVisible[1])
            {
                if (z == 0)
                {
                    if (Neighbors.ZPos != null)
                        lightValues[1] = Neighbors.ZPos.lightMap[y][x][last];
                }
                else
                {
                    lightValues[1] = lightMap[y][x][z - 1];
                }
            }

            if (facesVisible[2])
            {
                lightValues[2] = lightMap[y + 1][x][z];
            }

            if (facesVisible[3])
            {
                lightValues[3] = lightMap[y - 1][x][z];
            }


            if (facesVisible[4])
            {
                if (x == last)
                {
                    if (Neighbors.XNeg != null)
                        lightValues[4] = Neighbors.XNeg.lightMap[y][0][z];
                }
                else
                {
                    lightValues[4] = lightMap[y][x + 1][z];
                }
            }

            if (facesVisible[5])
            {
                if (x == 0)
                {
                    if (Neighbors.XPos != null)
                        lightValues[5] = Neighbors.XPos.lightMap[y][last][z];
                }
                else
                {
                    lightValues[5] = lightMap[y][x - 1][z];
                }
            }

            return lightValues;
        }

        public void AddLightSource(int y, int x, int z)
        {
            lightSources.Add(new BlockIndex(y, x, z));
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

        bool Transparent(ushort? texture)
        {
            return texture is null || isTransparent[(int)texture];
        }

        bool TransparentSolid(ushort? texture)
        {
            return texture != null && isTransparent[(int)texture];
        }
    }
}
