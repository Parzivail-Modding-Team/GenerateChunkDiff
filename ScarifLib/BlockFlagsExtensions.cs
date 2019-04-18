namespace ScarifLib
{
    public static class BlockFlagsExtensions
    {
        public static bool Has(this ScarifBlock.BlockFlags value, ScarifBlock.BlockFlags flag)
        {
            return (value & flag) != 0;
        }
    }
}