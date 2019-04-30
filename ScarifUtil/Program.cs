using System;
using System.Collections.Generic;
using System.Data.Common;
using System.IO;
using System.Linq;
using CommandLine;
using ScarifLib;
using Substrate;
using Substrate.Nbt;

namespace ScarifUtil
{
    [Verb("generate", HelpText = "Generate a SCRF from a world file")]
    internal class GenerateOptions
    {
        [Value(0, MetaName = "original", HelpText = "Unmodified world for comparison")]
        public string OriginalPath { get; set; }

        [Value(1, MetaName = "world", HelpText = "World to diff")]
        public string WorldPath { get; set; }

        [Value(2, MetaName = "world", HelpText = "Dimension to diff")]
        public int WorldDim { get; set; }

        [Value(3, MetaName = "output", HelpText = "Output diff file")]
        public string DiffOutput { get; set; }

        [Option('b', "bounds", HelpText = "Coordinate boundaries (format: \"minX:minY:minZ:maxX:maxY:maxZ\")")]
        public string ChunkBounds { get; set; }
    }

    [Verb("convert", HelpText = "Convert a SCRF file with a transformer")]
    internal class ConvertOptions
    {
        [Value(0, MetaName = "input", HelpText = "Input diff file")]
        public string Input { get; set; }

        [Value(1, MetaName = "transformer", HelpText = "Lookup table used to transform block IDs")]
        public string Transformer { get; set; }

        [Value(2, MetaName = "output", HelpText = "Output diff file")]
        public string Output { get; set; }
    }

    internal class Program
    {
        private static int Main(string[] args)
        {
            return Parser.Default.ParseArguments<GenerateOptions, ConvertOptions>(args)
                    .MapResult(
                        (GenerateOptions opts) =>
                        {
                            GenerateScrf(opts);
                            return 0;
                        },
                        (ConvertOptions opts) =>
                        {
                            ConvertScrf(opts);
                            return 0;
                        },
                        errs => 1);
        }

        private static void ConvertScrf(ConvertOptions opts)
        {
            var scrf = ScarifStructure.Load(opts.Input);
            var transformer = LoadTransformer(opts.Transformer);

            var newMapping = scrf.BlockTranslationMap.Clone();
            foreach (var pair in scrf.BlockTranslationMap)
            {
                if (transformer.ContainsKey(pair.Value))
                    newMapping[pair.Key] = transformer[pair.Value];
            }

            scrf.BlockTranslationMap = newMapping;

            scrf.Save(opts.Output);
        }

        private static Dictionary<string, string> LoadTransformer(string filename)
        {
            var dict = new Dictionary<string, string>();
            using(var reader = new StreamReader(filename))
            {
                while (!reader.EndOfStream)
                {
                    var line = reader.ReadLine();
                    if (line == null) continue;

                    var values = line.Split(',');
                    if (values.Length != 2) continue;

                    dict.Add(values[0], values[1]);
                }
            }

            return dict;
        }

        private static void GenerateScrf(GenerateOptions opts)
        {
            var world = AnvilWorld.Open(opts.WorldPath);
            var donorWorld = AnvilWorld.Open(opts.OriginalPath);

            var dim = opts.WorldDim;
            var manager = world.GetChunkManager(dim).ToList();
            var donorManager = donorWorld.GetChunkManager(dim);

            var inputMap = NbtMap.Load(Path.Combine(opts.OriginalPath, "cdfidmap.nbt"));
            var outputMap = NbtMap.Load(Path.Combine(opts.WorldPath, "cdfidmap.nbt"));

            var chunkBounds = new ChunkBounds(opts.ChunkBounds);

            if (!chunkBounds.BoundsExist)
            {
                Console.WriteLine("Input bounds not in the correct format");
                return;
            }

            var diff = new ScarifStructure(outputMap);

            Console.Clear();
            var cgui = new ConsoleGui();

            var pbChunks = new ConsoleGuiProgressBar(0, 0, Console.WindowWidth, 0, 1)
            {
                ForegroundColor = ConsoleColor.Green,
                BackgroundColor = ConsoleColor.DarkGray
            };

            var lChunksTotal = new ConsoleGuiLabel(0, 1, "Total Chunks    : {0}");
            var lChunksRemaining = new ConsoleGuiLabel(0, 2, "Remaining Chunks: {0}");
            var lStatus = new ConsoleGuiLabel(0, 3, "Status          : {0}");

            var lChunksProcessed = new ConsoleGuiLabel(Console.WindowWidth / 2, 1, "Processed Chunks : {0}");
            var lChunksSkipped = new ConsoleGuiLabel(Console.WindowWidth / 2, 2, "Skipped Chunks   : {0}");
            var lChunksDiffed = new ConsoleGuiLabel(Console.WindowWidth / 2, 3, "Diffed Chunks    : {0}");
            var lBlocksDiffed = new ConsoleGuiLabel(Console.WindowWidth / 2, 4, "Diffed Blocks    : {0}");
            var lTilesDiffed = new ConsoleGuiLabel(Console.WindowWidth / 2, 5, "Diffed TEs       : {0}");

            cgui.Add(pbChunks);

            cgui.Add(lChunksTotal);
            cgui.Add(lChunksRemaining);
            cgui.Add(lStatus);

            cgui.Add(lChunksProcessed);
            cgui.Add(lChunksSkipped);
            cgui.Add(lChunksDiffed);
            cgui.Add(lBlocksDiffed);
            cgui.Add(lTilesDiffed);

            var processedChunks = 0;
            var diffedChunks = 0;
            var diffedBlocks = 0;
            var diffedTiles = 0;
            var skipped = 0;

            lStatus.Value = "Processing...";

            for (var i = 1; i <= manager.Count; i++)
            {
                var chunk = manager[i - 1];

                if (i > 1)
                    manager[i - 2] = null;

                pbChunks.Value = (float)i / manager.Count;

                if (!donorManager.ChunkExists(chunk.X, chunk.Z) || !chunkBounds.CoarseContains(chunk))
                {
                    skipped++;
                    continue;
                }

                processedChunks++;

                var pos = new ChunkPosition(chunk.X, chunk.Z);
                var otherChunk = donorManager.GetChunk(chunk.X, chunk.Z);

                var numBlocksBefore = diffedBlocks;
                for (var y = 0; y < 256; y++)
                {
                    for (var x = 0; x < 16; x++)
                    {
                        for (var z = 0; z < 16; z++)
                        {
                            if (!chunkBounds.Contains(chunk.X * 16 + x, y, chunk.Z * 16 + z))
                                continue;

                            var blockId = (short)chunk.Blocks.GetID(x, y, z);
                            var blockData = chunk.Blocks.GetData(x, y, z);
                            NbtTree nbt = null;
                            var te = chunk.Blocks.GetTileEntity(x, y, z);
                            if (te != null)
                                nbt = new NbtTree(te.Source, "tile");

                            var blockIdOriginal = (short)otherChunk.Blocks.GetID(x, y, z);
                            var blockDataOriginal = otherChunk.Blocks.GetData(x, y, z);
                            NbtTree nbtOriginal = null;
                            var teOriginal = otherChunk.Blocks.GetTileEntity(x, y, z);
                            if (teOriginal != null)
                                nbtOriginal = new NbtTree(teOriginal.Source, "tile");

                            if (!inputMap.ContainsKey(blockIdOriginal) || !outputMap.ContainsKey(blockId))
                                continue;

                            if (inputMap[blockIdOriginal] == outputMap[blockId] && blockDataOriginal == blockData && nbtOriginal == nbt)
                                continue;

                            if (nbtOriginal != nbt)
                                diffedTiles++;

                            diffedBlocks++;
                            diff.Add(pos, new BlockPosition(x, y, z), new ScarifBlock(blockId, blockData, nbt));
                        }
                    }
                }

                if (diffedBlocks != numBlocksBefore)
                    diffedChunks++;

                lChunksTotal.Value = i.ToString("N0");
                lChunksRemaining.Value = (manager.Count - i).ToString("N0");

                lChunksProcessed.Value = processedChunks.ToString("N0");
                lBlocksDiffed.Value = diffedBlocks.ToString("N0");
                lTilesDiffed.Value = diffedTiles.ToString("N0");
                lChunksDiffed.Value = diffedChunks.ToString("N0");
                lChunksSkipped.Value = skipped.ToString("N0");

                cgui.Render();
            }

            lStatus.Value = "Saving...";
            cgui.Render();

            diff.Save(opts.DiffOutput);

            lStatus.Value = "Done. Press Enter.";
            cgui.Render();

            Console.ReadKey();
        }
    }
}
