using System;
using Substrate.Nbt;

namespace ScarifLib
{
    public struct ScarifBlock
    {
        public int Id { get; }
        public BlockFlags Flags { get; }
        public int Metadata { get; }
        public NbtTree TileData { get; }

        [Flags]
        public enum BlockFlags
        {
            None = 0,
            Metadata = 0b1,
            Nbt = 0b10
        }

        public ScarifBlock(int id, int metdata = 0, NbtTree tileData = null)
        {
            Id = id;
            Metadata = metdata;
            TileData = tileData;
            Flags = (Metadata == 0 ? BlockFlags.None : BlockFlags.Metadata) |
                    (TileData == null ? BlockFlags.None : BlockFlags.Nbt);
        }
    }
}