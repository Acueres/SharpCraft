using System.Collections.Generic;

namespace SharpCraft.Utility
{
    public enum Faces : byte
    {
        ZPos,
        ZNeg,
        YPos,
        YNeg,
        XPos,
        XNeg
    }

    public struct FacesState
    {
        byte data;

        public FacesState()
        {
            data = 0;
        }

        public FacesState(bool value)
        {
            XPos = value;
            XNeg = value;
            YPos = value;
            YNeg = value;
            ZPos = value;
            ZNeg = value;
        }

        public readonly bool Any()
        {
            return YPos || XPos || XNeg || ZPos || ZNeg || YNeg;
        }

        public readonly IEnumerable<Faces> GetFaces()
        {
            if (ZPos) yield return Faces.ZPos;
            if (ZNeg) yield return Faces.ZNeg;
            if (YPos) yield return Faces.YPos;
            if (YNeg) yield return Faces.YNeg;
            if (XPos) yield return Faces.XPos;
            if (XNeg) yield return Faces.XNeg;
        }

        public readonly bool GetFaceValue(Faces face)
        {
            return face switch
            {
                Faces.XPos => XPos,
                Faces.XNeg => XNeg,
                Faces.YPos => YPos,
                Faces.YNeg => YNeg,
                Faces.ZPos => ZPos,
                Faces.ZNeg => ZNeg,
                _ => false,
            };
        }

        public bool XPos
        {
            readonly get
            {
                return ((data >> 0) & 1) == 1;
            }
            set
            {
                data = value ? (byte)(data | (1 << 0)) : (byte)(data & ~(1 << 0));
            }
        }

        public bool XNeg
        {
            readonly get
            {
                return ((data >> 1) & 1) == 1;
            }
            set
            {
                data = value ? (byte)(data | (1 << 1)) : (byte)(data & ~(1 << 1));
            }
        }

        public bool YPos
        {
            readonly get
            {
                return ((data >> 2) & 1) == 1;
            }
            set
            {
                data = value ? (byte)(data | (1 << 2)) : (byte)(data & ~(1 << 2));
            }
        }

        public bool YNeg
        {
            readonly get
            {
                return ((data >> 3) & 1) == 1;
            }
            set
            {
                data = value ? (byte)(data | (1 << 3)) : (byte)(data & ~(1 << 3));
            }
        }

        public bool ZPos
        {
            readonly get
            {
                return ((data >> 4) & 1) == 1;
            }
            set
            {
                data = value ? (byte)(data | (1 << 4)) : (byte)(data & ~(1 << 4));
            }
        }

        public bool ZNeg
        {
            readonly get
            {
                return ((data >> 5) & 1) == 1;
            }
            set
            {
                data = value ? (byte)(data | (1 << 5)) : (byte)(data & ~(1 << 5));
            }
        }
    }
}
