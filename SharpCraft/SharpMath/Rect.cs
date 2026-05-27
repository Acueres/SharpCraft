using System.Numerics;

namespace SharpCraft.SharpMath;

internal readonly struct Rect
{
    public float X { get; }
    public float Y { get; }
    public float Width { get; }
    public float Height { get; }

    public float Left => X;
    public float Top => Y;
    public float Right => X + Width;
    public float Bottom => Y + Height;

    public Vector2 Position => new(X, Y);
    public Vector2 Size => new(Width, Height);

    public Vector2 Center => new(
        X + Width * 0.5f,
        Y + Height * 0.5f
    );

    public bool IsEmpty => Width <= 0 || Height <= 0;

    public Rect(float x, float y, float width, float height)
    {
        if (width < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(width),
                "Width cannot be negative."
            );
        }

        if (height < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(height),
                "Height cannot be negative."
            );
        }

        X = x;
        Y = y;
        Width = width;
        Height = height;
    }

    public Rect(Vector2 position, Vector2 size)
        : this(position.X, position.Y, size.X, size.Y)
    {
    }

    public bool Contains(Vector2 point)
    {
        return
            point.X >= Left &&
            point.X < Right &&
            point.Y >= Top &&
            point.Y < Bottom;
    }

    public bool Intersects(Rect other)
    {
        return
            Left < other.Right &&
            Right > other.Left &&
            Top < other.Bottom &&
            Bottom > other.Top;
    }

    public Rect Offset(Vector2 offset)
    {
        return new Rect(
            X + offset.X,
            Y + offset.Y,
            Width,
            Height
        );
    }

    public Rect Inflate(float amount)
    {
        return Inflate(amount, amount);
    }

    public Rect Inflate(float horizontal, float vertical)
    {
        return new Rect(
            X - horizontal,
            Y - vertical,
            Width + horizontal * 2,
            Height + vertical * 2
        );
    }

    public override string ToString()
    {
        return $"Rect {{ X = {X}, Y = {Y}, Width = {Width}, Height = {Height} }}";
    }
}
