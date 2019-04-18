namespace ScarifLib
{
    public struct BlockDiffEntry
    {
        public BlockPosition Position;
        public ScarifBlock Diff;

        public BlockDiffEntry(BlockPosition pos, ScarifBlock diff)
        {
            Position = pos;
            Diff = diff;
        }
    }
}