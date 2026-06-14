namespace SharpCraft.World.Blocks;

internal record struct FacesState
    {
        private byte data;

        public FacesState() { }

        public FacesState(bool value) => data = value ? (byte)0x3F : (byte)0;

        public readonly bool Any() => data != 0;
        
        public readonly FaceEnumerator GetFaces() => new(data);

        public readonly bool GetFaceValue(FaceDirection face)
        {
            return face switch
            {
                FaceDirection.ZPos => ZPos,
                FaceDirection.ZNeg => ZNeg,
                FaceDirection.XPos => XPos,
                FaceDirection.XNeg => XNeg,
                FaceDirection.YPos => YPos,
                FaceDirection.YNeg => YNeg,
                _ => false,
            };
        }

        private readonly bool Get(int bit) => (data & (1 << bit)) != 0;
        private void Set(int bit, bool value)
            => data = value ? (byte)(data | (1 << bit)) : (byte)(data & ~(1 << bit));
        
        public bool ZPos { readonly get => Get(0); set => Set(0, value); }
        public bool ZNeg { readonly get => Get(1); set => Set(1, value); }
        public bool XPos { readonly get => Get(2); set => Set(2, value); }
        public bool XNeg { readonly get => Get(3); set => Set(3, value); }
        public bool YPos { readonly get => Get(4); set => Set(4, value); }
        public bool YNeg { readonly get => Get(5); set => Set(5, value); }
        
        public struct FaceEnumerator(byte data)
        {
            private int i = -1;
            public FaceDirection Current { get; private set; }

            public bool MoveNext()
            {
                while (++i < 6)
                {
                    if (((data >> i) & 1) == 1)
                    {
                        Current = (FaceDirection)i;
                        return true;
                    }
                }
                return false;
            }

            public FaceEnumerator GetEnumerator() => this;
        }
    }