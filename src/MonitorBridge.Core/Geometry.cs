namespace MonitorBridge.Core;

/// <summary>Win32 가상 화면 물리 픽셀 좌표(정수). 음수 가능.</summary>
public readonly record struct PxPoint(int X, int Y);

/// <summary>물리 픽셀 사각형. Right/Bottom은 포함하지 않는다(반열린 구간).</summary>
public readonly record struct PxRect(int Left, int Top, int Right, int Bottom)
{
    public int Width => Right - Left;
    public int Height => Bottom - Top;
    public bool Contains(PxPoint p) => p.X >= Left && p.X < Right && p.Y >= Top && p.Y < Bottom;

    public PxPoint Clamp(PxPoint p) => new(
        Math.Min(Math.Max(p.X, Left), Right - 1),
        Math.Min(Math.Max(p.Y, Top), Bottom - 1));

    public static PxRect FromSize(int left, int top, int width, int height) => new(left, top, left + width, top + height);
}

public enum Rotation { Deg0 = 0, Deg90 = 1, Deg180 = 2, Deg270 = 3 }

public static class RotationExtensions
{
    public static bool IsPortrait(this Rotation r) => r == Rotation.Deg90 || r == Rotation.Deg270;
    public static int ToDegrees(this Rotation r) => (int)r * 90;
}

/// <summary>책상 위 실제 위치/크기(mm). 좌표는 사용자가 보는 방향(회전 적용 후) 기준, y는 아래쪽이 +.</summary>
public readonly record struct MmRect(double X, double Y, double Width, double Height)
{
    public double Right => X + Width;
    public double Bottom => Y + Height;
}
