using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using Brotli;
using Substrate.Core;
using Substrate.Nbt;

namespace ScarifLib
{
    public class ScarifStructure
    {
        private const string Magic = "SCRF";

        public readonly int Version = 1;
        public NbtMap BlockTranslationMap;
        public DiffMap BlockDiffMap;

        public ScarifStructure(NbtMap blockTranslationMap)
        {
            BlockDiffMap = new DiffMap();
            BlockTranslationMap = blockTranslationMap;
        }

        private ScarifStructure(int version, NbtMap idMap, DiffMap diffMap) : this(idMap)
        {
            Version = version;
            BlockDiffMap = diffMap;
        }

        private void TrimMappings()
        {
            var keysToRemove = new List<short>();
            foreach (var pair in BlockTranslationMap)
            {
                if (HasAnyBlocksWithId(pair.Key))
                    continue;

                keysToRemove.Add(pair.Key);
            }

            foreach (var key in keysToRemove) BlockTranslationMap.Remove(key);
        }

        private bool HasAnyBlocksWithId(short id)
        {
            return BlockDiffMap.Any(chunk => chunk.Value.Any(entry => id == entry.Diff.Id));
        }

        public void Save(string filename)
        {
            using (var fs = File.OpenWrite(filename))
            using (var bs = new BrotliStream(fs, CompressionMode.Compress))
            using (var f = new BinaryWriter(bs))
            {
                var ident = Magic.ToCharArray();

                f.Write(ident);
                f.Write(Version);
                f.Write(BlockDiffMap.Keys.Count); // Keys = Chunks
                f.Write((int)BlockTranslationMap.Keys.Count);
                
                TrimMappings();

                foreach (var pair in BlockTranslationMap)
                {
                    f.Write((short)pair.Key);

                    var buffer = Encoding.UTF8.GetBytes((string)pair.Value);
                    f.Write(buffer);
                    f.Write((byte)0);
                }

                // For each chunk
                foreach (var pair in BlockDiffMap)
                {
                    // Write out the chunk pos and how many blocks it has
                    f.Write(pair.Key.X);
                    f.Write(pair.Key.Z);
                    f.Write(pair.Value.Count);

                    // Write out each block's position and data
                    foreach (var block in pair.Value)
                    {
                        var x = (byte)(block.Position.X - pair.Key.X * 16) & 0x0F;
                        var z = (byte)(block.Position.Z - pair.Key.Z * 16) & 0x0F;
                        f.Write((byte)((x << 4) | z));
                        f.Write((byte)block.Position.Y);
                        f.Write((short)block.Diff.Id);
                        f.Write((byte)block.Diff.Flags);
                        if (block.Diff.Flags.HasFlag(ScarifBlock.BlockFlags.Metadata))
                            f.Write((byte)block.Diff.Metadata);

                        if (!block.Diff.Flags.HasFlag(ScarifBlock.BlockFlags.Nbt)) continue;

                        using (var memstream = new MemoryStream())
                        {
                            // Terrible hack to make the NBT in the format that MC likes
                            block.Diff.TileData.WriteTo(memstream);
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
            using (var fs = File.OpenRead(filename))
            using (var bs = new BrotliStream(fs, CompressionMode.Decompress))
            using (var s = new BinaryReader(bs))
            {
                var idMap = new NbtMap();
                var diffMap = new DiffMap();

                var identBytes = new byte[Magic.Length];
                var read = s.Read(identBytes, 0, identBytes.Length);
                var ident = Encoding.UTF8.GetString(identBytes);
                if (ident != Magic || read != identBytes.Length)
                    throw new IOException("Input file not SCARIF structure");

                var version = s.ReadInt32();
                var numChunks = s.ReadInt32();
                var numIdMapEntries = s.ReadInt32();

                for (var entryIdx = 0; entryIdx < numIdMapEntries; entryIdx++)
                {
                    var id = s.ReadInt16();
                    var name = ReadNullTerminatedString(s);
                    idMap.Add(id, name);
                }

                for (var chunkIdx = 0; chunkIdx < numChunks; chunkIdx++)
                {
                    var chunkX = s.ReadInt32();
                    var chunkZ = s.ReadInt32();
                    var numBlocks = s.ReadInt32();

                    var blocks = new BlockDiffEntry[numBlocks];

                    for (var blockIdx = 0; blockIdx < numBlocks; blockIdx++)
                    {
                        // Format:
                        // 0x 0000 1111
                        //    xxxx zzzz
                        var xz = s.ReadByte();

                        var x = (byte)((xz & 0xF0) >> 4);
                        var z = (byte)(xz & 0x0F);
                        var y = s.ReadByte();

                        var id = s.ReadInt16();
                        var flags = (ScarifBlock.BlockFlags)s.ReadByte();

                        byte metadata = 0;
                        NbtTree tileTag = null;

                        if (flags.Has(ScarifBlock.BlockFlags.Metadata))
                            metadata = s.ReadByte();
                        if (flags.Has(ScarifBlock.BlockFlags.Nbt))
                        {
                            var len = s.ReadInt32();
                            if (len <= 0)
                                throw new IOException("Zero-length NBT present");
                            var bytes = s.ReadBytes(len);
                            using (var ms = new MemoryStream(bytes))
                                tileTag = new NbtTree(ms);
                        }

                        if (idMap.ContainsKey(id))
                            blocks[blockIdx] = new BlockDiffEntry(new BlockPosition(x, y, z), new ScarifBlock(id, metadata, tileTag));
                        else
                            throw new IOException($"Unknown block ID found: {id}");
                    }

                    diffMap.Add(new ChunkPosition(chunkX, chunkZ), blocks);
                }

                return new ScarifStructure(version, idMap, diffMap);
            }
        }

        private static string ReadNullTerminatedString(BinaryReader s)
        {
            var str = new StringBuilder();
            while (true)
            {
                var b = s.ReadByte();
                if (b == 0)
                    return str.ToString();
                str.Append((char)b);
            }
        }

        public void Add(ChunkPosition chunk, BlockPosition pos, ScarifBlock block)
        {
            var entry = new BlockDiffEntry(pos, block);
            if (!BlockDiffMap.ContainsKey(chunk))
                BlockDiffMap.Add(chunk, new List<BlockDiffEntry>());
            BlockDiffMap[chunk].Add(entry);
        }
    }
}