﻿using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

using Microsoft.Win32.SafeHandles;
using Microsoft.Xna.Framework;

using System.Linq;
using SharpCraft.Utility;

namespace SharpCraft.World
{
    public class Chunk : IDisposable
    {
        public Vector3I Index { get; }
        public Vector3 Position { get; }
        public bool IsReady { get; set; }

        readonly HashSet<Vector3I> activeBlockIndexes = [];

        readonly Block[,,] blocks;

        public void Dispose() => Dispose(true);
        readonly SafeHandle safeHandle = new SafeFileHandle(IntPtr.Zero, true);
        bool disposed = false;

        public const int Size = 16;
        public const int Last = Size - 1;

        readonly BlockMetadataProvider blockMetadata;

        public bool RecalculateMesh { get; set; }

        readonly byte[,,] lightMap;
        readonly HashSet<Vector3I> lightSourceIndexes = [];
        readonly Queue<LightNode> lightQueue = new();

        readonly HashSet<Chunk> chunksToUpdate = [];

        public static int CalculateChunkIndex(float val)
        {
            if (val < 0)
            {
                return (int)(val / Size) - 1;
            }

            return (int)(val / Size);
        }

        public Chunk(Vector3I position, BlockMetadataProvider blockMetadata)
        {
            Index = position;
            Position = Size * new Vector3(position.X, position.Y, position.Z);

            this.blockMetadata = blockMetadata;

            blocks = new Block[Size, Size, Size];
            lightMap = new byte[Size, Size, Size];
        }

        public Block this[int x, int y, int z]
        {
            get => blocks[x, y, z];
            set => blocks[x, y, z] = value;
        }       

        public bool AddIndex(Vector3I index)
        {
            return activeBlockIndexes.Add(index);
        }

        public bool RemoveIndex(Vector3I index)
        {
            return activeBlockIndexes.Remove(index);
        }

        public Vector3I GetIndex(int i)
        {
            return activeBlockIndexes.Where((index, id) => id == i).Single();
        }

        public int ActiveBlocksCount => activeBlockIndexes.Count;

        public IEnumerable<Vector3I> GetActiveIndexes()
        {
            foreach (Vector3I index in activeBlockIndexes)
            {
                yield return index;
            }
        }

        public override int GetHashCode()
        {
            return Index.GetHashCode();
        }

        void Dispose(bool disposing)
        {
            if (disposed)
            {
                return;
            }

            if (disposing)
            {
                safeHandle?.Dispose();
            }

            disposed = true;
        }

        public void CalculateActiveBlocks(ChunkNeighbors neighbors)
        {
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    for (int z = 0; z < Size; z++)
                    {
                        if (blocks[x, y, z].IsEmpty)
                        {
                            continue;
                        }

                        FacesState visibleFaces = GetVisibleFaces(y, x, z, neighbors, calculateOpacity: false);

                        if (visibleFaces.Any())
                        {
                            AddIndex(new(x, y, z));
                        }
                    }
                }
            }
        }

        public FacesState GetVisibleFaces(int y, int x, int z, ChunkNeighbors neighbors,
                                    bool calculateOpacity = true)
        {
            FacesState visibleFaces = new();

            Block block = blocks[x, y, z];

            Block adjacentBlock;

            bool blockOpaque = true;
            if (calculateOpacity)
            {
                blockOpaque = !(block.IsEmpty || blockMetadata.IsBlockTransparent(block.Value));
            }

            if (z == Last)
            {
                adjacentBlock = neighbors.ZPos[x, y, 0];
            }
            else
            {
                adjacentBlock = blocks[x, y, z + 1];
            }
            visibleFaces.ZPos = adjacentBlock.IsEmpty || (blockMetadata.IsBlockTransparent(adjacentBlock.Value) && blockOpaque);

            if (z == 0)
            {
                adjacentBlock = neighbors.ZNeg[x, y, Last];
            }
            else
            {
                adjacentBlock = blocks[x, y, z - 1];
            }
            visibleFaces.ZNeg = adjacentBlock.IsEmpty || (blockMetadata.IsBlockTransparent(adjacentBlock.Value) && blockOpaque);

            if (y == Last)
            {
                adjacentBlock = neighbors.YPos[x, 0, z];
            }
            else
            {
                adjacentBlock = blocks[x, y + 1, z];
            }
            visibleFaces.YPos = adjacentBlock.IsEmpty || (blockMetadata.IsBlockTransparent(adjacentBlock.Value) && blockOpaque);

            if (y == 0)
            {
                adjacentBlock = neighbors.YNeg[x, Last, z];
            }
            else
            {
                adjacentBlock = blocks[x, y - 1, z];
            }
            visibleFaces.YNeg = adjacentBlock.IsEmpty || (blockMetadata.IsBlockTransparent(adjacentBlock.Value) && blockOpaque);


            if (x == Last)
            {
                adjacentBlock = neighbors.XPos[0, y, z];
            }
            else
            {
                adjacentBlock = blocks[x + 1, y, z];
            }
            visibleFaces.XPos = adjacentBlock.IsEmpty || (blockMetadata.IsBlockTransparent(adjacentBlock.Value) && blockOpaque);

            if (x == 0)
            {
                adjacentBlock = neighbors.XNeg[Last, y, z];
            }
            else
            {
                adjacentBlock = blocks[x - 1, y, z];
            }
            visibleFaces.XNeg = adjacentBlock.IsEmpty || (blockMetadata.IsBlockTransparent(adjacentBlock.Value) && blockOpaque);

            return visibleFaces;
        }

        //TODO: refactor light section

        public void InitializeLight(ChunkNeighbors neighbors)
        {
            bool skylight = true;
            bool blockLight = false;

            //Set the topmost layer to maximum light value
            for (int x = 0; x < Size; x++)
            {
                for (int z = 0; z < Size; z++)
                {
                    SetLight(Last, x, z, 15, skylight);
                    lightQueue.Enqueue(new LightNode(this, x, Last, z));
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
                Faces maxFace = Util.MaxFace(lightValues.GetValues());
                lightQueue.Enqueue(nodes.GetValue(maxFace));
                Repropagate(skylight, neighbors);

                (nodes, lightValues) = GetNeighborLightValues(y, x, z, blockLight, neighbors);
                maxFace = Util.MaxFace(lightValues.GetValues());
                lightQueue.Enqueue(nodes.GetValue(maxFace));
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
                    Faces face = (Faces)i;
                    if (lightValues.GetValue(face) == (byte)(light - 1))
                    {
                        lightQueue.Enqueue(nodes.GetValue(face));
                    }
                }
            }

            foreach (Chunk chunk in chunksToUpdate)
            {
                chunk.RecalculateMesh = true;
            }

            chunksToUpdate.Clear();
        }

        void FloodRemove(bool channel, ChunkNeighbors neighbors)
        {
            List<LightNode> lightList = [];

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
                Faces maxFace = Util.MaxFace(lightValues.GetValues());

                lightList.Add(nodes.GetValue(maxFace));

                for (int i = 0; i < 6; i++)
                {
                    Faces face = (Faces)i;
                    if (lightValues.GetValue(face) > 0 && (face == Faces.YNeg || lightValues.GetValue(face) < light) &&
                       IsBlockTransparent(nodes.GetValue(face).GetBlock()))
                    {
                        lightQueue.Enqueue(nodes.GetValue(face));
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

            Repropagate(channel, neighbors);
        }

        void Repropagate(bool channel, ChunkNeighbors neighbors)
        {
            FloodFill(channel, neighbors, repropagate: true);

            foreach (Chunk chunk in chunksToUpdate)
            {
                chunk.RecalculateMesh = true;
            }

            chunksToUpdate.Clear();
        }

        (FacesData<LightNode> nodes, FacesData<byte> lightValues) GetNeighborLightValues(int y, int x, int z, bool channel, ChunkNeighbors neighbors)
        {
            FacesData<LightNode> nodes = new();
            FacesData<byte> lightValues = new();

            if (y + 1 < Size)
            {
                nodes.YPos = new LightNode(this, x, y + 1, z);
                lightValues.YPos = GetLight(y + 1, x, z, channel);
            }

            if (y > 0)
            {
                nodes.YNeg = new LightNode(this, x, y - 1, z);
                lightValues.YNeg = GetLight(y - 1, x, z, channel);
            }


            if (x == Last)
            {
                nodes.XPos = new LightNode(neighbors.XPos, 0, y, z);
                lightValues.XPos = neighbors.XPos.GetLight(y, 0, z, channel);
            }
            else
            {
                nodes.XPos = new LightNode(this, x + 1, y, z);
                lightValues.XPos = GetLight(y, x + 1, z, channel);
            }

            if (x == 0)
            {
                nodes.XNeg = new LightNode(neighbors.XNeg, Last, y, z);
                lightValues.XNeg = neighbors.XNeg.GetLight(y, Last, z, channel);
            }
            else
            {
                nodes.XNeg = new LightNode(this, x - 1, y, z);
                lightValues.XNeg = GetLight(y, x - 1, z, channel);
            }


            if (z == Last)
            {
                nodes.ZPos = new LightNode(neighbors.ZPos, x, y, 0);
                lightValues.ZPos = neighbors.ZPos.GetLight(y, x, 0, channel);
            }
            else
            {
                nodes.ZPos = new LightNode(this, x, y, z + 1);
                lightValues.ZPos = GetLight(y, x, z + 1, channel);
            }

            if (z == 0)
            {
                nodes.ZNeg = new LightNode(neighbors.ZNeg, x, y, Last);
                lightValues.ZNeg = neighbors.ZNeg.GetLight(y, x, Last, channel);
            }
            else
            {
                nodes.ZNeg = new LightNode(this, x, y, z - 1);
                lightValues.ZNeg = GetLight(y, x, z - 1, channel);
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


            if (y + 1 < Size &&
                IsBlockTransparent(chunk[x, y + 1, z]) &&
                CompareLightValues(chunk.GetLight(y + 1, x, z, channel), light))
            {
                chunk.SetLight(y + 1, x, z, nextLight, channel);
                lightQueue.Enqueue(new LightNode(chunk, x, y + 1, z));
            }

            if (y > 0 &&
                IsBlockTransparent(chunk[x, y - 1, z]) &&
                CompareLightValues(chunk.GetLight(y - 1, x, z, channel), light, amount: (byte)(channel ? 0 : 1)))
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


            if (x == Last)
            {
                if (IsBlockTransparent(neighbors.XPos[0, y, z]) &&
                    CompareLightValues(neighbors.XPos.GetLight(y, 0, z, channel), light))
                {
                    neighbors.XPos.SetLight(y, 0, z, nextLight, channel);
                    lightQueue.Enqueue(new LightNode(neighbors.XPos, 0, y, z));
                }

                neighbors.XPos.RecalculateMesh = true;
            }
            else if (IsBlockTransparent(chunk[x + 1, y, z]) &&
                CompareLightValues(chunk.GetLight(y, x + 1, z, channel), light))
            {
                chunk.SetLight(y, x + 1, z, nextLight, channel);
                lightQueue.Enqueue(new LightNode(chunk, x + 1, y, z));
            }


            if (x == 0)
            {
                if (IsBlockTransparent(neighbors.XNeg[Last, y, z]) &&
                    CompareLightValues(neighbors.XNeg.GetLight(y, Last, z, channel), light))
                {
                    neighbors.XNeg.SetLight(y, Last, z, nextLight, channel);
                    lightQueue.Enqueue(new LightNode(neighbors.XNeg, Last, y, z));
                }

                neighbors.XNeg.RecalculateMesh = true;
            }
            else if (IsBlockTransparent(chunk[x - 1, y, z]) &&
                CompareLightValues(chunk.GetLight(y, x - 1, z, channel), light))
            {
                chunk.SetLight(y, x - 1, z, nextLight, channel);
                lightQueue.Enqueue(new LightNode(chunk, x - 1, y, z));
            }


            if (z == Last)
            {
                if (IsBlockTransparent(neighbors.ZPos[x, y, 0]) &&
                    CompareLightValues(neighbors.ZPos.GetLight(y, x, 0, channel), light))
                {
                    neighbors.ZPos.SetLight(y, x, 0, nextLight, channel);
                    lightQueue.Enqueue(new LightNode(neighbors.ZPos, x, y, 0));
                }

                neighbors.ZPos.RecalculateMesh = true;
            }
            else if (IsBlockTransparent(chunk[x, y, z + 1]) &&
                CompareLightValues(chunk.GetLight(y, x, z + 1, channel), light))
            {
                chunk.SetLight(y, x, z + 1, nextLight, channel);
                lightQueue.Enqueue(new LightNode(chunk, x, y, z + 1));
            }


            if (z == 0)
            {
                if (IsBlockTransparent(neighbors.ZNeg[x, y, Last]) &&
                    CompareLightValues(neighbors.ZNeg.GetLight(y, x, Last, channel), light))
                {
                    neighbors.ZNeg.SetLight(y, x, Last, nextLight, channel);
                    lightQueue.Enqueue(new LightNode(neighbors.ZNeg, x, y, Last));
                }

                neighbors.ZNeg.RecalculateMesh = true;
            }
            else if (IsBlockTransparent(chunk[x, y, z - 1]) &&
                CompareLightValues(chunk.GetLight(y, x, z - 1, channel), light))
            {
                chunk.SetLight(y, x, z - 1, nextLight, channel);
                lightQueue.Enqueue(new LightNode(chunk, x, y, z - 1));
            }
        }

        public FacesData<byte> GetFacesLight(FacesState visibleFaces, int y, int x, int z, ChunkNeighbors neighbors)
        {
            FacesData<byte> lightValues = new();

            if (visibleFaces.ZPos)
            {
                if (z == Last)
                {
                    lightValues.ZPos = neighbors.ZPos.lightMap[y, x, 0];
                }
                else
                {
                    lightValues.ZPos = lightMap[y, x, z + 1];
                }
            }

            if (visibleFaces.ZNeg)
            {
                if (z == 0)
                {
                    lightValues.ZNeg = neighbors.ZNeg.lightMap[y, x, Last];
                }
                else
                {
                    lightValues.ZNeg = lightMap[y, x, z - 1];
                }
            }

            if (visibleFaces.YPos)
            {
                if (y == Last)
                {
                    lightValues.YPos = neighbors.YPos.lightMap[0, x, z];
                }
                else
                {
                    lightValues.YPos = lightMap[y + 1, x, z];
                }
            }

            if (visibleFaces.YNeg)
            {
                if (y == 0)
                {
                    lightValues.YNeg = neighbors.YNeg.lightMap[Last, x, z];
                }
                else
                {
                    lightValues.YNeg = lightMap[y - 1, x, z];
                }
            }


            if (visibleFaces.XPos)
            {
                if (x == Last)
                {
                    lightValues.XPos = neighbors.XPos.lightMap[y, 0, z];
                }
                else
                {
                    lightValues.XPos = lightMap[y, x + 1, z];
                }
            }

            if (visibleFaces.XNeg)
            {
                if (x == 0)
                {
                    lightValues.XNeg = neighbors.XNeg.lightMap[y, Last, z];
                }
                else
                {
                    lightValues.XNeg = lightMap[y, x - 1, z];
                }
            }

            return lightValues;
        }

        public void AddLightSource(int y, int x, int z)
        {
            lightSourceIndexes.Add(new Vector3I(x, y, z));
        }

        public void SetLight(int y, int x, int z, byte value, bool skylight)
        {
            if (skylight)
            {
                lightMap[y, x, z] = (byte)((lightMap[y, x, z] & 0xF) | (value << 4));
            }
            else
            {
                lightMap[y, x, z] = (byte)((lightMap[y, x, z] & 0xF0) | value);
            }
        }

        public byte GetLight(int y, int x, int z, bool skylight)
        {
            if (skylight)
            {
                return (byte)((lightMap[y, x, z] >> 4) & 0xF);
            }

            return (byte)(lightMap[y, x, z] & 0xF);
        }

        static bool CompareLightValues(byte val, byte light, byte amount = 1)
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
