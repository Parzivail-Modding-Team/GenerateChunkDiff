﻿using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using Brotli;
using Substrate.Core;
using Substrate.Nbt;

namespace ScarifLib
{
    public class ScarifStructure : Dictionary<ChunkPosition, List<KeyValuePair<BlockPosition, BlockDiff>>>
    {
        public readonly int Version = 1;

        public void Save(string filename, NbtMap map)
        {
            using (var fs = File.OpenWrite(filename))
            using (var bs = new BrotliStream(fs, CompressionMode.Compress))
            using (var f = new BinaryWriter(bs))
            {
                var ident = "SCRF".ToCharArray();

                f.Write(ident);
                f.Write(Version);
                f.Write(Keys.Count); // Keys = Chunks
                f.Write((int) map.Keys.Count);

                foreach (var pair in map)
                {
                    f.Write((short) pair.Key);

                    var buffer = Encoding.UTF8.GetBytes((string) pair.Value);
                    f.Write(buffer);
                    f.Write((byte)0);
                }

                // For each chunk
                foreach (var pair in this)
                {
                    // Write out the chunk pos and how many blocks it has
                    f.Write(pair.Key.X);
                    f.Write(pair.Key.Z);
                    f.Write(pair.Value.Count);

                    // Write out each block's position and data
                    foreach (var block in pair.Value)
                    {
                        var x = (byte)(block.Key.X - pair.Key.X * 16) & 0x0F;
                        var z = (byte)(block.Key.Z - pair.Key.Z * 16) & 0x0F;
                        f.Write((byte)((x << 4) | z));
                        f.Write((byte)block.Key.Y);
                        f.Write((short)block.Value.Id);
                        f.Write((byte)block.Value.Flags);
                        if (block.Value.Flags.HasFlag(BlockDiff.BlockFlags.HasMetadata))
                            f.Write((byte)block.Value.Metadata);

                        if (!block.Value.Flags.HasFlag(BlockDiff.BlockFlags.HasTileNbt)) continue;

                        using (var memstream = new MemoryStream())
                        {
                            // Terrible hack to make the NBT in the format that MC likes
                            block.Value.TileData.WriteTo(memstream);
                            memstream.Seek(0, SeekOrigin.Begin);
                            var len = memstream.Length;
                            f.Write((int)len);
                            var b = new byte[(int)len];
                            memstream.Read(b, 0, (int)len);
                            f.Write(b);
                        }
                    }
                }
            }
        }

        public static ScarifStructure Load(string filename)
        {
            // TODO
            return new ScarifStructure();
        }

        public void Add(ChunkPosition chunk, BlockPosition pos, BlockDiff block)
        {
            var entry = new KeyValuePair<BlockPosition, BlockDiff>(pos, block);
            if (!ContainsKey(chunk))
                Add(chunk, new List<KeyValuePair<BlockPosition, BlockDiff>>());
            this[chunk].Add(entry);
        }
    }
}