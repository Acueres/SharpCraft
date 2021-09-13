using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using SharpCraft.World;

namespace SharpCraft.Models
{
    readonly struct VoxelData<T>
    {
        public readonly byte Length { get; }
        public readonly T Value { get; }

        public VoxelData(int count, T val)
        {
            Length = (byte)(count > 0 ? count : 1);
            Value = val;
        }
    }

    class VoxelRLE<T>
    {
        public int Count(int x, int z) => data[x, z].Count;

        readonly List<VoxelData<T>>[,] data;

        public VoxelRLE(int capacity = 32)
        {
            data = new List<VoxelData<T>>[Chunk.SIZE, Chunk.SIZE];
            for (int x = 0; x < Chunk.SIZE; x++)
            {
                for (int z = 0; z < Chunk.SIZE; z++)
                {
                    data[x, z] = new List<VoxelData<T>>(capacity);
                }
            }
        }

        public T this[int x, int y, int z]
        {
            get
            {
                if (y >= Chunk.HEIGHT || y < 0) throw new IndexOutOfRangeException();

                int index = 0;
                for (int i = 0; i < data[x, z].Count; i++)
                {
                    if (y >= index && y < index + data[x, z][i].Length)
                    {
                        if (data[x, z][i].Length == 1)
                        {
                            return data[x, z][i].Value;
                        }
                        else
                        {
                            return data[x, z][i].Value;
                        }
                    }

                    index += data[x, z][i].Length;
                }

                return default;
            }

            set
            {
                if (y >= Chunk.HEIGHT || y < 0) throw new IndexOutOfRangeException();

                int index = 0;
                for (int i = 0; i < data[x, z].Count; i++)
                {
                    if (y >= index && y < index + data[x, z][i].Length)
                    {
                        if (data[x, z][i].Length == 1)
                        {
                            data[x, z][i] = new(1, value);
                        }
                        else if (y == index)
                        {
                            data[x, z][i] = new(data[x, z][i].Length - 1, data[x, z][i].Value);
                            data[x, z].Insert(i, new(1, value));
                        }
                        else if (y == index + data[x, z][i].Length)
                        {
                            data[x, z][i] = new(data[x, z][i].Length - 1, data[x, z][i].Value);
                            data[x, z].Insert(i + 1, new(1, value));
                        }
                        else if (!EqualityComparer<T>.Default.Equals(data[x, z][i].Value, value))
                        {
                            int first = y;
                            int second = data[x, z][i].Length - first - 1;

                            data[x, z][i] = new(first, data[x, z][i].Value);
                            data[x, z].Insert(i + 1, new(second, data[x, z][i].Value));
                            data[x, z].Insert(i + 1, new(1, value));
                        }

                        CombineValues(x, z, i);
                        return;
                    }

                    index += data[x, z][i].Length;
                }

                data[x, z].Add(new(1, value));
                CombineValues(x, z, data[x, z].Count - 1);
            }
        }

        void CombineValues(int x, int z, int i)
        {
            if (data[x, z].Count == 1) return;

            if (i != 0 && EqualityComparer<T>.Default.Equals(data[x, z][i - 1].Value, data[x, z][i].Value))
            {
                data[x, z][i] = new(data[x, z][i].Length + data[x, z][i - 1].Length, data[x, z][i].Value);
                data[x, z].RemoveAt(i - 1);
                i--;
            }

            if (i < data[x, z].Count - 1 && EqualityComparer<T>.Default.Equals(data[x, z][i + 1].Value, data[x, z][i].Value))
            {
                data[x, z][i] = new(data[x, z][i].Length + data[x, z][i + 1].Length, data[x, z][i].Value);
                data[x, z].RemoveAt(i + 1);
            }
        }
    }
}
