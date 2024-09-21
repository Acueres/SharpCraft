﻿using System;
using System.Collections.Generic;
using SharpCraft.Utility;

namespace SharpCraft.World
{
    public sealed partial class Chunk
    {
        byte[][][] lightMap;
        HashSet<Vector3I> lightSourceIndexes;

        Queue<LightNode> lightQueue;
        List<LightNode> lightList;

        LightNode[] nodes;
        byte[] lightValues;

        HashSet<Chunk> chunksToUpdate;

        public void InitializeLight()
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

            FloodFill(skylight);

            foreach (Vector3I lightSourceIndex in lightSourceIndexes)
            {
                int y = lightSourceIndex.Y;
                int x = lightSourceIndex.X;
                int z = lightSourceIndex.Z;

                SetLight(y, x, z, blockMetadata.GetLightSourceValue(this[x, y, z].Value), blockLight);
                lightQueue.Enqueue(new LightNode(this, x, y, z));
            }

            FloodFill(blockLight);
        }

        public void UpdateLight(int y, int x, int z, ushort texture, bool sourceRemoved = false)
        {
            bool skylight = true;
            bool blockLight = false;

            //Propagate light to an empty cell
            if (texture == Block.EmptyValue)
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
                if (blockMetadata.IsLightSource(this[x, y, z].Value))
                {
                    SetLight(y, x, z, blockMetadata.GetLightSourceValue(this[x, y, z].Value), blockLight);
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

                if (TransparentSolid(node.GetBlock()))
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
                nodes[3] = new LightNode(Neighbors.XPos, LAST, y, z);
                lightValues[3] = Neighbors.XPos.GetLight(y, LAST, z, channel);
            }
            else
            {
                nodes[3] = new LightNode(this, x - 1, y, z);
                lightValues[3] = GetLight(y, x - 1, z, channel);
            }


            if (z == LAST)
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
                nodes[5] = new LightNode(Neighbors.ZPos, x, y, LAST);
                lightValues[5] = Neighbors.ZPos.GetLight(y, x, LAST, channel);
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
                if (chunk.Neighbors.XNeg != null &&
                    IsBlockTransparent(chunk.Neighbors.XNeg[0, y, z]) &&
                    CompareValues(chunk.Neighbors.XNeg.GetLight(y, 0, z, channel), light))
                {
                    chunk.Neighbors.XNeg.SetLight(y, 0, z, nextLight, channel);
                    lightQueue.Enqueue(new LightNode(chunk.Neighbors.XNeg, 0, y, z));
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
                if (chunk.Neighbors.XPos != null &&
                    IsBlockTransparent(chunk.Neighbors.XPos[LAST, y, z]) &&
                    CompareValues(chunk.Neighbors.XPos.GetLight(y, LAST, z, channel), light))
                {
                    chunk.Neighbors.XPos.SetLight(y, LAST, z, nextLight, channel);
                    lightQueue.Enqueue(new LightNode(chunk.Neighbors.XPos, LAST, y, z));
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
                if (chunk.Neighbors.ZNeg != null &&
                    IsBlockTransparent(chunk.Neighbors.ZNeg[x, y, 0]) &&
                    CompareValues(chunk.Neighbors.ZNeg.GetLight(y, x, 0, channel), light))
                {
                    chunk.Neighbors.ZNeg.SetLight(y, x, 0, nextLight, channel);
                    lightQueue.Enqueue(new LightNode(chunk.Neighbors.ZNeg, x, y, 0));
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
                if (chunk.Neighbors.ZPos != null &&
                    IsBlockTransparent(chunk.Neighbors.ZPos[x, y, LAST]) &&
                    CompareValues(chunk.Neighbors.ZPos.GetLight(y, x, LAST, channel), light))
                {
                    chunk.Neighbors.ZPos.SetLight(y, x, LAST, nextLight, channel);
                    lightQueue.Enqueue(new LightNode(chunk.Neighbors.ZPos, x, y, LAST));
                }
            }
            else if (IsBlockTransparent(chunk[x, y, z - 1]) &&
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
                if (z == LAST)
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
                        lightValues[1] = Neighbors.ZPos.lightMap[y][x][LAST];
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
                if (x == LAST)
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
                        lightValues[5] = Neighbors.XPos.lightMap[y][LAST][z];
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
