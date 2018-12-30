using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommandLine;
using ScarifLib;
using Substrate.Core;
using Substrate.ImportExport;
using Substrate.Nbt;

namespace TranslateSchematic
{
    internal class CliOptions
    {
        [Value(0, MetaName = "input", HelpText = "Input schematic")]
        public string InputSchematic { get; set; }

        [Value(1, MetaName = "inputMap", HelpText = "Input cdfidmap")]
        public string InputMap { get; set; }

        [Value(2, MetaName = "outputMap", HelpText = "Output reference cdfidmap")]
        public string OutputMap { get; set; }

        [Value(3, MetaName = "output", HelpText = "Output schematic")]
        public string OutputSchematic { get; set; }
    }

    class Program
    {
        private static void Main(string[] args)
        {
            Parser.Default.ParseArguments<CliOptions>(args)
                .WithParsed(RunOptionsAndReturnExitCode)
                .WithNotParsed(HandleParseError);
        }

        private static void HandleParseError(IEnumerable<Error> errs)
        {
            foreach (var error in errs)
                Console.WriteLine($"Error: {error.Tag}");
        }

        private static void RunOptionsAndReturnExitCode(CliOptions opts)
        {
            Console.Clear();
            var cgui = new ConsoleGui();

            var pbBlocks = new ConsoleGuiProgressBar(0, 0, Console.WindowWidth, 0, 1)
            {
                ForegroundColor = ConsoleColor.Green,
                BackgroundColor = ConsoleColor.DarkGray
            };

            var lBlocksTotal = new ConsoleGuiLabel(0, 1, "Total Blocks    : {0}");
            var lBlocksRemaining = new ConsoleGuiLabel(0, 2, "Remaining Blocks: {0}");
            var lBlocksFailed = new ConsoleGuiLabel(0, 3, "Failed Blocks   : {0}");
            var lStatus = new ConsoleGuiLabel(0, 4, "Status          : {0}");

            cgui.Add(pbBlocks);
            cgui.Add(lBlocksTotal);
            cgui.Add(lBlocksRemaining);
            cgui.Add(lBlocksFailed);
            cgui.Add(lStatus);

            var failed = new List<string>();

            var inputMap = NbtMap.Load(opts.InputMap);
            var outputMap = NbtMap.Load(opts.OutputMap);

            var inputSchematic = new NBTFile(opts.InputSchematic);
            var tag = new NbtTree(inputSchematic.GetDataInputStream()).Root;

            var bLower = tag["Blocks"].ToTagByteArray().Data;
            var addBlocks = new byte[(bLower.Length >> 1) + 1];

            if (tag.ContainsKey("AddBlocks"))
                addBlocks = tag["AddBlocks"].ToTagByteArray().Data;

            lStatus.Value = "Processing...";

            for (var index = 0; index < bLower.Length; index++)
            {
                short oldId;
                if ((index & 1) == 1)
                    oldId = (short) (((addBlocks[index >> 1] & 0x0F) << 8) + (bLower[index] & 0xFF));
                else
                    oldId = (short) (((addBlocks[index >> 1] & 0xF0) << 4) + (bLower[index] & 0xFF));

                if (!TranslateId(oldId, inputMap, outputMap, out var newId))
                {
                    var position = GetPosition(index, tag["Length"].ToTagShort().Data, tag["Width"].ToTagShort().Data);
                    failed.Add($"#{oldId} at {position.Item1},{position.Item2},{position.Item3}");
                }

                bLower[index] = (byte)(newId & 0xFF);
                addBlocks[index >> 1] = (byte)(((index & 1) == 1) ?
                    addBlocks[index >> 1] & 0xF0 | (newId >> 8) & 0xF
                    : addBlocks[index >> 1] & 0xF | ((newId >> 8) & 0xF) << 4);

                // Gui

                lBlocksTotal.Value = index + 1;
                lBlocksRemaining.Value = bLower.Length - index - 1;
                lBlocksFailed.Value = failed.Count;
                pbBlocks.Value = (float) index / bLower.Length;

                if (index % 10000 == 0)
                    cgui.Render();
            }

            cgui.Render();

            tag["Blocks"] = new TagNodeByteArray(bLower);

            if (!tag.ContainsKey("AddBlocks"))
                tag.Add("AddBlocks", new TagNodeByteArray(addBlocks));
            else
                tag["AddBlocks"] = new TagNodeByteArray(addBlocks);

            lStatus.Value = "Saving...";
            cgui.Render();

            var schematicFile = new NBTFile(opts.OutputSchematic);
            using (var nbtStream = schematicFile.GetDataOutputStream())
            {
                if (nbtStream == null)
                    return;

                var tree = new NbtTree(tag, "Schematic");
                tree.WriteTo(nbtStream);
            }

            lStatus.Value = "Done. Press Enter.";
            cgui.Render();

            Console.WriteLine();
            foreach (var i in failed)
                Console.WriteLine($"Failed ID: {i}");

            Console.ReadKey();
        }

        private static bool TranslateId(short oldId, NbtMap inputMap, NbtMap outputMap, out short result)
        {
            result = 0;
            if (!inputMap.ContainsKey(oldId))
                return false;
            var inputName = inputMap[oldId];
            result = outputMap.First(pair => pair.Value == inputName).Key;
            return true;
        }

        private static (int, int, int) GetPosition(int index, int length, int width)
        {
            var rX = index % length;
            var rZ = ((index - rX) / width) % length;
            var rY = (((index - rX) / width) - rZ) / length;
            return (rX, rY, rZ);
        }
    }
}
