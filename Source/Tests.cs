using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;

using SharpCraft.Models;
using SharpCraft.World;

namespace SharpCraft
{
    static class Tests
    {
        public static void Run()
        {
            VoxelRLETest();
        }

        static void VoxelRLETest()
        {
            var col = new VoxelRLE<ushort?>(32);
            for (int i = 0; i < Chunk.HEIGHT; i++)
            {
                col[0, i, 0] = 1;
            }

            //Test correct same-type filling
            Debug.Assert(col[0, 0, 0] == 1);
            Debug.Assert(col[0, 1, 0] == 1);
            Debug.Assert(col[0, 127, 0] == 1);
            Debug.Assert(col.Count(0, 0) == 1);

            //Test correct different type insertion
            col[0, 125, 0] = 2;

            Debug.Assert(col[0, 125, 0] == 2);
            Debug.Assert(col.Count(0, 0) == 3);

            //Test correct length-1 reassignment
            col[0, 125, 0] = 5;

            Debug.Assert(col[0, 125, 0] == 5);
            Debug.Assert(col.Count(0, 0) == 3);

            //Test data merge
            col[0, 125, 0] = 1;

            Debug.Assert(col[0, 125, 0] == 1);
            Debug.Assert(col.Count(0, 0) == 1);

            col[0, 1, 0] = 2;

            Debug.Assert(col[0, 1, 0] == 2);
            Debug.Assert(col.Count(0, 0) == 3);

            col[0, 1, 0] = 1;

            Debug.Assert(col[0, 1, 0] == 1);
            Debug.Assert(col.Count(0, 0) == 1);

            //Test different type insertion at 0
            col[0, 0, 0] = 2;

            Debug.Assert(col[0, 0, 0] == 2);
            Debug.Assert(col.Count(0, 0) == 2);

            col[0, 0, 0] = 1;

            Debug.Assert(col[0, 0, 0] == 1);
            Debug.Assert(col.Count(0, 0) == 1);
        }
    }
}
