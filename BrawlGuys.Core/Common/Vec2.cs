namespace BrawlGuys.Core;

public readonly record struct Vec2(double X, double Y)
{
    public static Vec2 Zero => new(0, 0);

    public double Length => Math.Sqrt((X * X) + (Y * Y));

    public Vec2 Normalized()
    {
        var length = Length;
        return length <= 0.0001 ? Zero : new Vec2(X / length, Y / length);
    }

    public static double Dot(Vec2 a, Vec2 b) => (a.X * b.X) + (a.Y * b.Y);

    public static Vec2 operator +(Vec2 a, Vec2 b) => new(a.X + b.X, a.Y + b.Y);
    public static Vec2 operator -(Vec2 a, Vec2 b) => new(a.X - b.X, a.Y - b.Y);
    public static Vec2 operator *(Vec2 a, double b) => new(a.X * b, a.Y * b);
    public static Vec2 operator /(Vec2 a, double b) => new(a.X / b, a.Y / b);
}
