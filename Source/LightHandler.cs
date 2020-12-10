using System;
using System.Collections.Generic;

using Microsoft.Xna.Framework;


namespace SharpCraft
{
    class LightHandler
    {
        Queue<LightNode> lightNodes;

        int size;
        int height;
        int last;

        IList<bool> transparent;

        HashSet<Chunk> chunksToUpdate;

        struct LightNode
        {
            public Chunk Chunk;
            public int X;
            public int Y;
            public int Z;

            public byte Light
            {
                get
                {
                    return Chunk.LightMap[Y][X][Z];
                }
            }

            public LightNode(Chunk chunk, int x, int y, int z)
            {
                Chunk = chunk;
                X = x;
                Y = y;
                Z = z;
            }
        }


        public LightHandler(int _size)
        {
            lightNodes = new Queue<LightNode>((int)1e3);

            size = _size;
            height = 128;
            last = size - 1;

            transparent = Assets.TransparentBlocks;

            chunksToUpdate = new HashSet<Chunk>(5);
        }

        public void Initialize(Chunk chunk)
        {
            for (int x = 0; x < size; x++)
            {
                for (int z = 0; z < size; z++)
                {
                    lightNodes.Enqueue(new LightNode(chunk, x, height - 1, z));
                }
            }

            FloodFill(sunlight: true);
        }

        public void Update(Chunk chunk, int y, int x, int z, ushort? texture)
        {
            LightNode[] nodes = new LightNode[6];
            byte[] lightValues = new byte[6];

            //Propagate light to an empty cell
            if (texture is null)
            {
                GetNeighbors(chunk, y, x, z, nodes, lightValues);

                lightNodes.Enqueue(nodes[Util.ArgMax(lightValues)]);

                Recalculate();
            }
            //Recalculate light after block placement
            else
            {
                lightNodes.Enqueue(new LightNode(chunk, x, y, z));

                LightNode nearestSource = new LightNode();
                int count = 0;

                //For all cells with light values less than the previous set light to 0
                while (lightNodes.Count > 0)
                {
                    LightNode node = lightNodes.Dequeue();

                    chunksToUpdate.Add(node.Chunk);

                    byte light = node.Chunk.LightMap[node.Y][node.X][node.Z];
                    node.Chunk.LightMap[node.Y][node.X][node.Z] = 0;

                    GetNeighbors(node.Chunk, node.Y, node.X, node.Z, nodes, lightValues);

                    //Find current cell's nearest light source, starting with the second cell of the queue
                    //If source for current cell doesn't exist, repeat the process for the next cell
                    if (count == 1)
                    {
                        int max = Util.ArgMax(lightValues);

                        nearestSource = NearestSource(nodes[max].Chunk,
                                        nodes[max].Y, nodes[max].X, nodes[max].Z);

                        if (nearestSource.Chunk is null)
                        {
                            count--;
                        }
                    }
                    count++;

                    for (int i = 0; i < 6; i++)
                    {
                        if ((i == 1 || lightValues[i] < light) && nodes[i].Chunk != null &&
                           IsTransparent(nodes[i].Chunk.Blocks[nodes[i].Y][nodes[i].X][nodes[i].Z]))
                        {
                            lightNodes.Enqueue(nodes[i]);
                        }
                    }
                }

                if (nearestSource.Chunk != null)
                {
                    chunksToUpdate.Add(nearestSource.Chunk);
                    lightNodes.Enqueue(nearestSource);
                    Recalculate();
                }
            }
        }

        void Recalculate()
        {
            FloodFill(sunlight: true, recalculate: true);

            foreach (Chunk chunk in chunksToUpdate)
            {
                chunk.GenerateMesh = true;
            }

            chunksToUpdate.Clear();
        }

        LightNode NearestSource(Chunk chunk, int y, int x, int z)
        {
            byte[] lightValues = new byte[6];
            LightNode[] nodes = new LightNode[6];

            GetNeighbors(chunk, y, x, z, nodes, lightValues);

            LightNode node = new LightNode(chunk, x, y, z);

            for (int i = 0; i < size; i++)
            {
                if (Sunlit(node.Chunk, node.Y, node.X, node.Z))
                {
                    break;
                }

                GetNeighbors(node.Chunk, node.Y, node.X, node.Z, nodes, lightValues);

                int max = Util.ArgMax(lightValues);
                node = nodes[max];
            }

            return node;
        }

        bool Sunlit(Chunk chunk, int y, int x, int z)
        {
            for (int i = y; i < height; i++)
            {
                if (chunk.Blocks[i][x][z] != null)
                {
                    return false;
                }
            }

            return true;
        }

        void GetNeighbors(Chunk chunk, int y, int x, int z, LightNode[] nodes, byte[] lightValues)
        {
            Array.Clear(nodes, 0, 6);
            Array.Clear(lightValues, 0, 6);

            if (y + 1 < height)
            {
                nodes[0] = new LightNode(chunk, x, y + 1, z);
                lightValues[0] = chunk.LightMap[y + 1][x][z];
            }

            if (y > 0)
            {
                nodes[1] = new LightNode(chunk, x, y - 1, z);
                lightValues[1] = chunk.LightMap[y - 1][x][z];
            }


            if (x == last)
            {
                nodes[2] = new LightNode(chunk.Neighbors.XNeg, 0, y, z);
                lightValues[2] = chunk.Neighbors.XNeg.LightMap[y][0][z];
            }
            else
            {
                nodes[2] = new LightNode(chunk, x + 1, y, z);
                lightValues[2] = chunk.LightMap[y][x + 1][z];
            }

            if (x == 0)
            {
                nodes[3] = new LightNode(chunk.Neighbors.XPos, last, y, z);
                lightValues[3] = chunk.Neighbors.XPos.LightMap[y][last][z];
            }
            else
            {
                nodes[3] = new LightNode(chunk, x - 1, y, z);
                lightValues[3] = chunk.LightMap[y][x - 1][z];
            }


            if (z == last)
            {
                nodes[4] = new LightNode(chunk.Neighbors.ZNeg, x, y, 0);
                lightValues[4] = chunk.Neighbors.ZNeg.LightMap[y][x][0];
            }
            else
            {
                nodes[4] = new LightNode(chunk, x, y, z + 1);
                lightValues[4] = chunk.LightMap[y][x][z + 1];
            }

            if (z == 0)
            {
                nodes[5] = new LightNode(chunk.Neighbors.ZPos, x, y, last);
                lightValues[5] = chunk.Neighbors.ZPos.LightMap[y][x][last];
            }
            else
            {
                nodes[5] = new LightNode(chunk, x, y, z - 1);
                lightValues[5] = chunk.LightMap[y][x][z - 1];
            }
        }

        void FloodFill(bool sunlight = false, bool recalculate = false)
        {
            LightNode node;
            while (lightNodes.Count > 0)
            {
                node = lightNodes.Dequeue();

                if (recalculate)
                {
                    chunksToUpdate.Add(node.Chunk);
                }

                Propagate(node.Chunk, sunlight, node.Y, node.X, node.Z);
            }
        }

        void Propagate(Chunk chunk, bool sunlight, int y, int x, int z)
        {
            byte light = chunk.LightMap[y][x][z];

            byte nextLight;

            if (light > 1)
                nextLight = (byte)(light - 1);
            else
                nextLight = 1;


            if (y + 1 < height &&
                IsTransparent(chunk.Blocks[y + 1][x][z]) &&
                chunk.LightMap[y + 1][x][z] < light)
            {
                chunk.LightMap[y + 1][x][z] = nextLight;
                lightNodes.Enqueue(new LightNode(chunk, x, y + 1, z));
            }

            if (y > 0 &&
                IsTransparent(chunk.Blocks[y - 1][x][z]) &&
                chunk.LightMap[y - 1][x][z] < light)
            {
                if (sunlight)
                {
                    chunk.LightMap[y - 1][x][z] = light;
                    lightNodes.Enqueue(new LightNode(chunk, x, y - 1, z));
                }
                else
                {
                    chunk.LightMap[y - 1][x][z] = nextLight;
                    lightNodes.Enqueue(new LightNode(chunk, x, y - 1, z));
                }
            }


            if (x == last)
            {
                if (chunk.Neighbors.XNeg != null &&
                    IsTransparent(chunk.Neighbors.XNeg.Blocks[y][0][z]) &&
                    chunk.Neighbors.XNeg.LightMap[y][0][z] < light)
                {
                    chunk.Neighbors.XNeg.LightMap[y][0][z] = nextLight;
                    lightNodes.Enqueue(new LightNode(chunk.Neighbors.XNeg, 0, y, z));
                }
            }
            else if (IsTransparent(chunk.Blocks[y][x + 1][z]) &&
                chunk.LightMap[y][x + 1][z] < light)
            {
                chunk.LightMap[y][x + 1][z] = nextLight;
                lightNodes.Enqueue(new LightNode(chunk, x + 1, y, z));
            }


            if (x == 0)
            {
                if (chunk.Neighbors.XPos != null &&
                    IsTransparent(chunk.Neighbors.XPos.Blocks[y][last][z]) &&
                    chunk.Neighbors.XPos.LightMap[y][last][z] < light)
                {
                    chunk.Neighbors.XPos.LightMap[y][last][z] = nextLight;
                    lightNodes.Enqueue(new LightNode(chunk.Neighbors.XPos, last, y, z));
                }
            }
            else if (IsTransparent(chunk.Blocks[y][x - 1][z]) &&
                    chunk.LightMap[y][x - 1][z] < light)
            {
                chunk.LightMap[y][x - 1][z] = nextLight;
                lightNodes.Enqueue(new LightNode(chunk, x - 1, y, z));
            }


            if (z == last)
            {
                if (chunk.Neighbors.ZNeg != null &&
                    IsTransparent(chunk.Neighbors.ZNeg.Blocks[y][x][0]) &&
                    chunk.Neighbors.ZNeg.LightMap[y][x][0] < light)
                {
                    chunk.Neighbors.ZNeg.LightMap[y][x][0] = nextLight;
                    lightNodes.Enqueue(new LightNode(chunk.Neighbors.ZNeg, x, y, 0));
                }
            }
            else if (IsTransparent(chunk.Blocks[y][x][z + 1]) &&
                    chunk.LightMap[y][x][z + 1] < light)
            {
                chunk.LightMap[y][x][z + 1] = nextLight;
                lightNodes.Enqueue(new LightNode(chunk, x, y, z + 1));
            }


            if (z == 0)
            {
                if (chunk.Neighbors.ZPos != null &&
                    IsTransparent(chunk.Neighbors.ZPos.Blocks[y][x][last]) &&
                    chunk.Neighbors.ZPos.LightMap[y][x][last] < light)
                {
                    chunk.Neighbors.ZPos.LightMap[y][x][last] = nextLight;
                    lightNodes.Enqueue(new LightNode(chunk.Neighbors.ZPos, x, y, last));
                }
            }
            else if (IsTransparent(chunk.Blocks[y][x][z - 1]) &&
                    chunk.LightMap[y][x][z - 1] < light)
            {
                chunk.LightMap[y][x][z - 1] = nextLight;
                lightNodes.Enqueue(new LightNode(chunk, x, y, z - 1));
            }
        }

        bool IsTransparent(ushort? texture)
        {
            return texture is null || transparent[(int)texture];
        }
    }
}
