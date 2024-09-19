﻿using Microsoft.VisualBasic.FileIO;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using SearchOption = System.IO.SearchOption;

namespace Unverum
{
    public static class ModLoader
    {
        // Restore all backups created from previous build
        public static bool Restart(string path, string movies, string splash, string sound)
        {
            try
            {
                // Delete everything in mods folder
                Directory.Delete(path, true);
                Directory.CreateDirectory(path);
                // Delete everything in patches folder for Switch games
                if (!String.IsNullOrEmpty(Global.config.Configs[Global.config.CurrentGame].PatchesFolder))
                {
                    if (Directory.Exists(Global.config.Configs[Global.config.CurrentGame].PatchesFolder))
                        Directory.Delete(Global.config.Configs[Global.config.CurrentGame].PatchesFolder, true);
                    Directory.CreateDirectory(Global.config.Configs[Global.config.CurrentGame].PatchesFolder);
                    // Delete sound folder in romfs if it exists
                    var SwitchSound = Global.config.Configs[Global.config.CurrentGame].PatchesFolder.Replace("exefs", $"romfs{Global.s}Project{Global.s}Content{Global.s}Sound");
                    if (Directory.Exists(SwitchSound))
                        Directory.Delete(SwitchSound, true);
                }
                // Reset movies and splash folder
                if (!String.IsNullOrEmpty(movies) && Directory.Exists(movies))
                    RestoreDirectory(movies);
                if (!String.IsNullOrEmpty(splash) && Directory.Exists(splash))
                    RestoreDirectory(splash);
                if (!String.IsNullOrEmpty(sound) && Directory.Exists(sound))
                    RestoreDirectory(sound);
                // Remove UE4SS Files
                var LogicModsFolder = $"{Path.GetDirectoryName(path)}{Global.s}LogicMods";
                var Win64Folder = $"{Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(path)))}{Global.s}Binaries{Global.s}Win64";
                var ue4ssModsFolder = $"{Win64Folder}{Global.s}Mods";
                List<string> ue4ssFiles = new() { "opengl32.dll", "patternsleuth_bind.dll", "ue4ss.dll", "UE4SS-settings.ini", "dwmapi.dll" };
                foreach (var ue4ssFile in ue4ssFiles) 
                {
                    var file = $"{Win64Folder}{Global.s}{ue4ssFile}";
                    if (File.Exists(file))
                        File.Delete(file);
                }
                if (Directory.Exists(ue4ssModsFolder))
                    Directory.Delete(ue4ssModsFolder, true);
                if (Directory.Exists(LogicModsFolder))
                    Directory.Delete(LogicModsFolder, true);
                Global.logger.WriteLine("Restored folders", LoggerType.Info);
            }
            catch (Exception e)
            {
                Global.logger.WriteLine(e.Message, LoggerType.Error);
                return false;
            }
            return true;
        }
        private static void RestoreDirectory(string path)
        {
            foreach (var file in Directory.GetFiles(path, "*", SearchOption.AllDirectories))
                if (File.Exists($"{file}.bak"))
                    File.Move($"{file}.bak", file, true);
        }
        private static int CopyFolder(Dictionary<string, bool> paks, string sourcePath, string targetPath, string defaultSig)
        {
            var counter = 0;
            //Copy all the files & Replaces any files with the same name
            foreach (var path in Directory.GetFiles(sourcePath, "*.*", SearchOption.AllDirectories))
            {
                if (Path.GetExtension(path).Equals(".pak", StringComparison.InvariantCultureIgnoreCase)
                    && paks.ContainsKey(path) && paks[path])
                {
                    var newPath = path.Replace(sourcePath, targetPath).Replace(".pak", "_9_P.pak");
                    Directory.CreateDirectory(Path.GetDirectoryName(newPath));
                    File.Copy(path, newPath, true);
                    if (defaultSig != null)
                    {
                        var sig = Path.ChangeExtension(path, ".sig");
                        var newPathSig = Path.ChangeExtension(newPath, ".sig");
                        // Check if mod folder has corresponding .sig
                        if (File.Exists(sig))
                            File.Copy(sig, newPathSig, true);
                        // Otherwise copy over original game's .sig
                        else if (File.Exists(defaultSig))
                            File.Copy(defaultSig, newPathSig, true);
                        else
                        {
                            Global.logger.WriteLine($"Couldn't find .sig file to go with {newPath}", LoggerType.Warning);
                            continue;
                        }
                    }
                    // Copy over utoc/ucas if they exist
                    if (File.Exists(Path.ChangeExtension(path, ".utoc")) && File.Exists(Path.ChangeExtension(path, ".ucas")))
                    {
                        var utoc = Path.ChangeExtension(path, ".utoc");
                        var ucas = Path.ChangeExtension(path, ".ucas");
                        File.Copy(utoc, utoc.Replace(sourcePath, targetPath).Replace(".utoc", "_9_P.utoc"), true);
                        File.Copy(ucas, ucas.Replace(sourcePath, targetPath).Replace(".ucas", "_9_P.ucas"), true);
                    }
                    counter++;
                }
            }

            // Copy over Sound folder to proper location for SMTV if it exists
            if (Global.config.CurrentGame.Equals("Shin Megami Tensei V", StringComparison.InvariantCultureIgnoreCase))
            foreach (var path in Directory.GetDirectories(sourcePath, "*.*", SearchOption.AllDirectories))
            {
                var SoundFolder = Global.config.Configs[Global.config.CurrentGame].PatchesFolder.Replace("exefs", $"romfs{Global.s}Project{Global.s}Content{Global.s}Sound");
                if (Path.GetFileName(path).Equals("Sound", StringComparison.InvariantCultureIgnoreCase))
                {
                    foreach(var file in Directory.GetFiles(path, "*", SearchOption.AllDirectories))
                    {
                        var newPath = file.Replace(path, SoundFolder);
                        Directory.CreateDirectory(Path.GetDirectoryName(newPath));
                        Global.logger.WriteLine($"Copying over {file} to {newPath}", LoggerType.Info);
                        File.Copy(file, newPath, true);
                    }
                    break;
                }
            }
            return counter;
        }

        private static void ReplaceAsset(string file, string path)
        {
            var filesFound = 0;
            foreach (var oldFile in Directory.GetFiles(path, "*", SearchOption.AllDirectories)
                            .Where(a => Path.GetFileName(a).Equals(Path.GetFileName(file),
                            StringComparison.InvariantCultureIgnoreCase)))
            {
                if (!File.Exists($"{oldFile}.bak"))
                    File.Copy(oldFile, $"{oldFile}.bak", true);
                File.Copy(file, oldFile, true);
                Global.logger.WriteLine($"Replaced {oldFile} with {file}", LoggerType.Info);
                filesFound++;
            }
            if (filesFound == 0)
                Global.logger.WriteLine($"Couldn't find {file} within {path}", LoggerType.Warning);
        }
        private static void PakFiles(string path, string output, string sig)
        {
            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.CreateNoWindow = true;
            startInfo.UseShellExecute = false;
            startInfo.FileName = $"{Global.assemblyLocation}{Global.s}Dependencies{Global.s}u4pak{Global.s}u4pak.exe";
            startInfo.WindowStyle = ProcessWindowStyle.Hidden;
            startInfo.WorkingDirectory = $"{Global.assemblyLocation}{Global.s}Dependencies{Global.s}u4pak";
            startInfo.Arguments = $"pack \"{output}{Global.s}Unverum_9_P.pak\" {path}";
            using (Process process = new Process())
            {
                process.StartInfo = startInfo;
                process.Start();
                process.WaitForExit();
            }
            var pak = $"{output}{Global.s}Unverum_9_P.pak";
            if (File.Exists(pak))
            {
                if (sig != null)
                {
                    var newSig = Path.ChangeExtension(pak, ".sig");
                    // Copy over original game's .sig
                    if (File.Exists(sig))
                        File.Copy(sig, newSig, true);
                    else
                        Global.logger.WriteLine($"Couldn't find .sig file to go with {pak}", LoggerType.Warning);
                }
            }
            else
                Global.logger.WriteLine($"Failed to create pak!", LoggerType.Error);
        }
        // Copy over mod files in order of ModList
        public static void Build(string path, List<Mod> mods, bool? patched, string movies, string splash, string sound)
        {
            var missing = false;
            Dictionary<string, Entry> entries = null;
            HashSet<string> db = null;
            string prmFilePaths = String.Empty;
            string sig = null;
            var sigs = Directory.GetFiles(Path.GetDirectoryName(path), "*.sig", SearchOption.TopDirectoryOnly);
            if (sigs.Length > 0)
                sig = sigs[0];
            var folderLetter = 'a';
            var tildes = 0;
            // UnrealModLoader paths
            var LogicModsFolder = $"{Path.GetDirectoryName(path)}{Global.s}LogicMods";
            var Win64Folder = $"{Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(path)))}{Global.s}Binaries{Global.s}Win64";
            var ue4ssModsFolder = $"{Win64Folder}{Global.s}Mods";
            var ue4ss = false;
            foreach (var mod in mods)
            {
                var priorityName = String.Empty;
                foreach (var tilde in Enumerable.Range(0, tildes))
                    priorityName += "~";
                priorityName += folderLetter;
                var folder = $"{path}{Global.s}{priorityName}{Global.s}{mod.name}";
                var modPath = $@"{Global.assemblyLocation}{Global.s}Mods{Global.s}{Global.config.CurrentGame}{Global.s}{mod.name}";
                // Copy over .paks and .sigs to ~mods folder in order
                if (CopyFolder(mod.paks, modPath, folder, sig) > 0)
                {
                    Global.logger.WriteLine($"Copied paks and sigs from {mod.name} over to {folder}", LoggerType.Info);
                    folderLetter++;
                    if (folderLetter == '{')
                    {
                        folderLetter = 'a';
                        tildes++;
                    }
                }
                // Copy over UE4SS Mods
                foreach (var ue4ssModDirectory in Directory.GetDirectories(modPath, "*ue4ss", SearchOption.AllDirectories))
                    foreach (var ue4ssMod in Directory.GetFiles(ue4ssModDirectory, "*", SearchOption.AllDirectories))
                    {
                        // Don't copy over the mods.txt file if they have their own
                        if (Path.GetFileName(ue4ssMod).Equals("mods.txt", StringComparison.InvariantCultureIgnoreCase))
                            continue;
                        var newPath = ue4ssMod.Replace(ue4ssModDirectory, ue4ssModsFolder);
                        Directory.CreateDirectory(Path.GetDirectoryName(newPath));
                        File.Copy(ue4ssMod, newPath, true);
                        Global.logger.WriteLine($"Copying over {ue4ssMod} to {newPath}", LoggerType.Info);
                        ue4ss = true;
                    }
                foreach (var logicModDirectory in Directory.GetDirectories(modPath, "*LogicMods", SearchOption.AllDirectories))
                    foreach (var logicMod in Directory.GetFiles(logicModDirectory, "*", SearchOption.AllDirectories))
                    {
                        var newPath = logicMod.Replace(logicModDirectory, LogicModsFolder);
                        Directory.CreateDirectory(Path.GetDirectoryName(newPath));
                        File.Copy(logicMod, newPath, true);
                        Global.logger.WriteLine($"Copying over {logicMod} to {newPath}", LoggerType.Info);
                        ue4ss = true;
                    }
                foreach (var file in Directory.GetFiles(modPath, "*", SearchOption.AllDirectories))
                {
                    var ext = Path.GetExtension(file).ToLowerInvariant();
                    switch (ext)
                    {
                        case ".txt":
                            if (Path.GetFileName(file).Equals("dblist.txt", StringComparison.InvariantCultureIgnoreCase) &&
                                Global.config.CurrentGame.Equals("My Hero One's Justice 2", StringComparison.InvariantCultureIgnoreCase))
                            {
                                var dblistFile = $"{Global.assemblyLocation}{Global.s}Resources{Global.s}My Hero One's Justice 2{Global.s}HeroGame{Global.s}Content{Global.s}DB{Global.s}dblist.txt";
                                if (missing)
                                    continue;
                                if (db == null && TextPatcher.ExtractBaseFiles("HeroGame-WindowsNoEditor_0_P.pak", "*dblist.txt",
                                    $"HeroGame{Global.s}Content{Global.s}DB{Global.s}dblist.txt"))
                                    db = File.ReadAllLines(dblistFile).ToHashSet();
                                // Check if db is still null
                                if (db == null)
                                {
                                    missing = true;
                                    continue;
                                }
                                Global.logger.WriteLine($"Appending dblist.txt from {mod.name}...", LoggerType.Info);
                                db.UnionWith(File.ReadAllLines(file));
                            }
                            break;
                        case ".usm":
                        case ".uasset":
                        case ".mp4":
                            if (!String.IsNullOrEmpty(movies) && Directory.Exists(movies))
                                ReplaceAsset(file, movies);
                            break;
                        case ".bmp":
                            if (!String.IsNullOrEmpty(splash) && Directory.Exists(splash))
                                ReplaceAsset(file, splash);
                            break;
                        case ".awb":
                            if (!String.IsNullOrEmpty(sound) && Directory.Exists(sound))
                                ReplaceAsset(file, sound);
                            break;
                        case ".pchtxt":
                        case ".ips":
                            if (!String.IsNullOrEmpty(Global.config.Configs[Global.config.CurrentGame].PatchesFolder) && Directory.Exists(Global.config.Configs[Global.config.CurrentGame].PatchesFolder))
                                File.Copy(file, $"{Global.config.Configs[Global.config.CurrentGame].PatchesFolder}{Global.s}{Path.GetFileName(file)}", true);
                            break;
                        case ".json":
                            if (Path.GetFileName(file).Equals("text.json", StringComparison.InvariantCultureIgnoreCase) &&
                                    (Global.config.CurrentGame.Equals("Dragon Ball FighterZ", StringComparison.InvariantCultureIgnoreCase)
                                    || Global.config.CurrentGame.Equals("Guilty Gear -Strive-", StringComparison.InvariantCultureIgnoreCase)
                                    || Global.config.CurrentGame.Equals("Granblue Fantasy Versus", StringComparison.InvariantCultureIgnoreCase)
                                    || Global.config.CurrentGame.Equals("Granblue Fantasy Versus Rising", StringComparison.InvariantCultureIgnoreCase)
                                    || Global.config.CurrentGame.Equals("DNF Duel", StringComparison.InvariantCultureIgnoreCase)))
                            {
                                    if (missing)
                                        continue;
                                    var pakName = Global.config.CurrentGame.Equals("DNF Duel", StringComparison.InvariantCultureIgnoreCase) ? "RED-WindowsNoEditor.pak" : "pakchunk0-WindowsNoEditor.pak";
                                    if (entries == null && TextPatcher.ExtractBaseFiles(pakName, "RED/Content/Localization/INT/REDGame",
                                            $"RED{Global.s}Content{Global.s}Localization{Global.s}INT{Global.s}REDGame.uexp"))
                                            entries = TextPatcher.GetEntries();
                                    // Check if entries are still null
                                    if (entries == null)
                                    {
                                        missing = true;
                                        continue;
                                    }
                                    
                                    var text = File.ReadAllText(file);
                                    TextEntries replacements;
                                    try
                                    {
                                        replacements = JsonSerializer.Deserialize<TextEntries>(text);
                                    }
                                    catch (Exception e)
                                    {
                                        Global.logger.WriteLine(e.Message, LoggerType.Error);
                                        continue;
                                    }
                                    foreach (var replacement in replacements.Entries)
                                    {
                                        entries = TextPatcher.ReplaceEntry(replacement, entries);
                                    }
                            }
                            break;
                    }
                }
                // Check for prm_files folder for JUMP FORCE slots
                if (Global.config.CurrentGame.Equals("Jump Force", StringComparison.InvariantCultureIgnoreCase))
                    foreach (var prm in Directory.GetDirectories(modPath, "*prm_files", SearchOption.AllDirectories))
                        prmFilePaths += $@"""{prm}"" ";
            }
            // Check if UE4SS is installed if UE4SS mod is used
            if (ue4ss)
            {
                // Copy over UE4SS install files
                FileSystem.CopyDirectory($"{Global.assemblyLocation}{Global.s}Dependencies{Global.s}ue4ss", Win64Folder);
                // Edit mods.txt to enable added folders
                var modsFile = $"{Win64Folder}{Global.s}Mods{Global.s}mods.txt";
                var lines = File.ReadAllLines(modsFile).ToList();
                List<string> defaultMods = new()
                {
                    "CheatManagerEnablerMod",
                    "ActorDumperMod",
                    "ConsoleCommandsMod",
                    "ConsoleEnablerMod",
                    "SplitScreenMod",
                    "LineTraceMod",
                    "BPModLoaderMod",
                    "BPML_GenericFunctions",
                    "jsbLuaProfilerMod",
                    "shared",
                    "Keybinds"
                };
                foreach (var directory in Directory.GetDirectories(ue4ssModsFolder, "*", SearchOption.TopDirectoryOnly)) 
                {
                    var mod = Path.GetFileName(directory);
                    if (!defaultMods.Contains(mod))
                        lines.Insert(lines.Count - 1, $"{mod} : 1");
                }
                File.WriteAllLines(modsFile, lines);
            }
            // Create pak if text was patched
            if (entries != null)
            {
                // Write uasset/uexp
                TextPatcher.WriteToFile(entries);

                var priorityName = String.Empty;
                foreach (var tilde in Enumerable.Range(0, tildes))
                    priorityName += "~";
                priorityName += folderLetter;
                var folder = $"{path}{Global.s}{priorityName}";
                Directory.CreateDirectory(folder);

                PakFiles("RED", folder, sig);
                // Delete loose files
                Directory.Delete($"{Global.assemblyLocation}{Global.s}Dependencies{Global.s}u4pak{Global.s}RED", true);
            }
            // Create pak if dblist is found for MHOJ2
            if (db != null)
            {
                var dbOutput = $"{Global.assemblyLocation}{Global.s}Dependencies{Global.s}u4pak{Global.s}HeroGame{Global.s}Content{Global.s}DB{Global.s}dblist.txt";
                Directory.CreateDirectory(Path.GetDirectoryName(dbOutput));
                File.WriteAllLines(dbOutput, db);

                var priorityName = String.Empty;
                foreach (var tilde in Enumerable.Range(0, tildes))
                    priorityName += "~";
                priorityName += folderLetter;
                var folder = $"{path}{Global.s}{priorityName}";
                Directory.CreateDirectory(folder);
                PakFiles("HeroGame", folder, sig);
                Directory.Delete($"{Global.assemblyLocation}{Global.s}Dependencies{Global.s}u4pak{Global.s}HeroGame", true);
            }
            // Costume Patched placeholder files as lowest priority
            if (patched != null && (bool)patched && Global.config.CurrentGame != "Scarlet Nexus")
            {
                var baseFolder = $"{path}{Global.s}--Base--";
                using (var resource = Assembly.GetExecutingAssembly().GetManifestResourceStream($"Unverum.Resources.Patches.{Global.config.CurrentGame.Replace(" ", "_").Replace("-", "_")}.Placeholder.--PlaceholderCostumes.pak"))
                {
                    Directory.CreateDirectory(baseFolder);
                    using (var stream = new FileStream($"{baseFolder}{Global.s}--PlaceholderCostumes.pak", FileMode.Create, FileAccess.Write))
                    {
                        resource.CopyTo(stream);
                    }
                }
                if (sig != null)
                {
                    var newSig = $"{baseFolder}{Global.s}--PlaceholderCostumes.sig";
                    // Copy over original game's .sig
                    if (File.Exists(sig))
                        File.Copy(sig, newSig, true);
                    else
                        Global.logger.WriteLine($"Couldn't find .sig file to go with {baseFolder}{Global.s}--PlaceholderCostumes.pak", LoggerType.Warning);
                }
                Global.logger.WriteLine($"Copied over base costume patch files", LoggerType.Info);
            }
            if (!String.IsNullOrEmpty(prmFilePaths))
            {
                Global.logger.WriteLine($"Adding slots...", LoggerType.Info);
                ProcessStartInfo startInfo = new ProcessStartInfo();
                startInfo.CreateNoWindow = true;
                startInfo.UseShellExecute = false;
                startInfo.FileName = $"{Global.assemblyLocation}{Global.s}Dependencies{Global.s}jfaddslots{Global.s}JFAddSlots.exe";
                startInfo.WindowStyle = ProcessWindowStyle.Hidden;
                startInfo.WorkingDirectory = $"{Global.assemblyLocation}{Global.s}Dependencies{Global.s}jfaddslots";
                startInfo.Arguments = $@"""{Global.assemblyLocation}{Global.s}Dependencies{Global.s}u4pak"" {prmFilePaths}";
                using (Process process = new Process())
                {
                    process.StartInfo = startInfo;
                    process.Start();
                    process.WaitForExit();
                }
                var priorityName = String.Empty;
                foreach (var tilde in Enumerable.Range(0, tildes))
                    priorityName += "~";
                priorityName += folderLetter;
                var folder = $"{path}{Global.s}{priorityName}";
                Directory.CreateDirectory(folder);
                PakFiles("JUMP_FORCE", folder, sig);
                Directory.Delete($"{Global.assemblyLocation}{Global.s}Dependencies{Global.s}u4pak{Global.s}JUMP_FORCE", true);
                Global.logger.WriteLine($"Slots added!", LoggerType.Info);
            }
            Global.logger.WriteLine("Finished building!", LoggerType.Info);
        }
    }
}
