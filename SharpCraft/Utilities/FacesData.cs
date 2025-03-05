using System.Collections.Generic;

namespace SharpCraft.Utilities
{
    public struct FacesData<T>
    {
        public T XPos {  get; set; }
        public T XNeg { get; set; }
        public T YPos { get; set; }
        public T YNeg { get; set; }
        public T ZPos { get; set; }
        public T ZNeg { get; set; }

        public readonly T GetValue(Faces face)
        {
            return face switch
            {
                Faces.XPos => XPos,
                Faces.XNeg => XNeg,
                Faces.YPos => YPos,
                Faces.YNeg => YNeg,
                Faces.ZPos => ZPos,
                Faces.ZNeg => ZNeg,
                _ => default
            };
        }

        public readonly IEnumerable<T> GetValues()
        {
            yield return ZPos;
            yield return ZNeg;
            yield return YPos;
            yield return YNeg;
            yield return XPos;
            yield return XNeg;
        }
    }
}
