namespace SharpCraft.Utility
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
    }
}
