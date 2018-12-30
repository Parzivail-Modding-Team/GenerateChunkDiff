namespace ScarifLib
{
    public class BlockPosition
    {
        public int X { get; }
        public int Y { get; }
        public int Z { get; }

        public BlockPosition(int x, int y, int z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public override string ToString()
        {
            return $"({X},{Y},{Z})";
        }
    }
}