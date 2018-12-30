using System;
using Substrate.Nbt;

namespace ScarifLib
{
    public class BlockDiff
    {
        public int Id { get; }
        public BlockFlags Flags { get; }
        public int Metadata { get; }
        public NbtTree TileData { get; }

        [Flags]
        public enum BlockFlags
        {
            None = 0,
            HasMetadata = 0b1,
            HasTileNbt = 0b10
        }

        public BlockDiff(int id, int metdata = 0, NbtTree tileData = null)
        {
            Id = id;
            Metadata = metdata;
            TileData = tileData;
            Flags = (Metadata == 0 ? BlockFlags.None : BlockFlags.HasMetadata) |
                    (TileData == null ? BlockFlags.None : BlockFlags.HasTileNbt);
        }
    }
}