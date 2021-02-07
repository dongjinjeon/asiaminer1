﻿using NHM.Common;
using NHM.Common.Enums;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NhmPackager
{
    class Program
    {

        private static string GetRootPath(params string[] subPaths)
        {
            var rootPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            var paths = new List<string> { rootPath };
            foreach (var subPath in subPaths) paths.Add(subPath);
            var ret = Path.Combine(paths.ToArray());
            var retAbsolutePath = new Uri(ret).LocalPath;
            return retAbsolutePath;
        }

        private static bool ExecXCopy(string copyFrom, string copyTo)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "xcopy.exe",
                Arguments = $"/s /i {copyFrom} {copyTo}",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };
            using (var copyRelease = new Process { StartInfo = startInfo })
            {
                var ok = copyRelease.Start();
                while (!copyRelease.StandardOutput.EndOfStream)
                {
                    string line = copyRelease.StandardOutput.ReadLine();
                    Console.WriteLine(line);
                }
                return ok && copyRelease.ExitCode == 0;
            }
        }

        private static bool ExecPluginsPacker(string exePath, string exeCwd, string args)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = exePath,
                Arguments = args,
                WorkingDirectory = exeCwd,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                //RedirectStandardInput = true,
                CreateNoWindow = true,
            };
            using (var proc = new Process { StartInfo = startInfo })
            {
                var ok = proc.Start();
                while (!proc.StandardOutput.EndOfStream)
                {
                    string line = proc.StandardOutput.ReadLine();
                    Console.WriteLine(line);
                }
                return ok && proc.ExitCode == 0;
            }
        }

        private static bool ExecNhmpackerCreateInstallers(string exePath)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = exePath,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };
            using (var proc = new Process { StartInfo = startInfo })
            {
                var ok = proc.Start();
                while (!proc.StandardOutput.EndOfStream)
                {
                    string line = proc.StandardOutput.ReadLine();
                    Console.WriteLine(line);
                }
                return ok && proc.ExitCode == 0;
            }
        }

        private static void DeleteFileIfExists(string filePath)
        {
            if (File.Exists(filePath)) File.Delete(filePath);
        }

        private static void RecreateFolderIfExists(string dirPath)
        {
            if (Directory.Exists(dirPath))
            {
                Console.WriteLine($"Deleting '{dirPath}'");
                Directory.Delete(dirPath, true);
            }
            if (!Directory.Exists(dirPath))
            {
                Directory.CreateDirectory(dirPath);
            }
        }

        // #1 copy release folder
        // #2 delete all json settings from release folder
        // #3 get versions from launcher and app (they must be equal)
        // #4 append version to app folder
        // #5 copy assets (EULA,...)
        // #6 create NSIS template '_files_to_pack\nsis'
        static void Main(string[] args)
        {
            try
            {
                var randomPart = DateTime.UtcNow.Millisecond;
                var tmpWorkFolder = $"tmp_{randomPart}";

                // #1
                // assume we are in installer folder
                var nhmReleaseFolder = GetRootPath(@"..\", "Release");
                ExecXCopy(nhmReleaseFolder, GetRootPath(tmpWorkFolder, "Release"));
                // run the plugins packer in the installer
                Console.WriteLine("ExecPluginsPacker START");
                Console.WriteLine();
                Console.WriteLine();
                ExecPluginsPacker(GetRootPath("MinerPluginsPacker.exe"), GetRootPath(tmpWorkFolder, "Release"), GetRootPath(@"..\", "src", "Miners"));
                Console.WriteLine();
                Console.WriteLine();
                Console.WriteLine("ExecPluginsPacker Done. Press any key to continue");
                Console.ReadKey();

                // #2 
                DeleteFileIfExists(GetRootPath(tmpWorkFolder, "Release", "build_settings.json"));

                // #3 
                var launcherPath = GetRootPath(tmpWorkFolder, "Release", "NiceHashMiner.exe");
                var appPath = GetRootPath(tmpWorkFolder, "Release", "app", "app_nhm.exe");
                var (generatedTemplateLauncher, versionLauncher, buildTagLauncher) = VersionInfoHelpers.GenerateVariableTemplate(launcherPath);
                var (generatedTemplate, version, buildTag) = VersionInfoHelpers.GenerateVariableTemplate(appPath);
                if (generatedTemplateLauncher != generatedTemplate || versionLauncher != version || buildTagLauncher != buildTag)
                {
                    throw new Exception("Launcher and App TAG or Version missmatch!!!");
                }
                Console.WriteLine("ExecPluginsPacker resumming...");
                // #4 
                var appDirOld = GetRootPath(tmpWorkFolder, "Release", "app");
                var appDirNew = GetRootPath(tmpWorkFolder, "Release", $"app_{version}");
                Console.WriteLine($"moving '{appDirOld}' to '{appDirNew}'");
                Directory.Move(appDirOld, appDirNew);
                // #5
                ZipFile.ExtractToDirectory(GetRootPath("EULA.zip"), GetRootPath(tmpWorkFolder, "Release"));
                // #6 
                var filesToPackPath = GetRootPath(tmpWorkFolder, "_files_to_pack");
                RecreateFolderIfExists(filesToPackPath);
                // copy template and exe files
                ExecXCopy(GetRootPath(tmpWorkFolder, "Release"), Path.Combine(filesToPackPath, "bins"));
                ExecXCopy(GetRootPath("nsis_template"), Path.Combine(filesToPackPath, "nsis"));
                File.WriteAllText(Path.Combine(filesToPackPath, "nsis", "include_common", "packageDefsGenerated.nsh"), generatedTemplate, new UTF8Encoding(true));
                File.WriteAllText(Path.Combine(filesToPackPath, "version.txt"), version + buildTag);
                // delete previous _files_to_pack from nhmpacker
                var nhmPackerFilesToPack = GetRootPath("nhmpacker", "_files_to_pack"); 
                RecreateFolderIfExists(nhmPackerFilesToPack);
                ExecXCopy(filesToPackPath, nhmPackerFilesToPack);
                RecreateFolderIfExists(GetRootPath("nhmpacker", "_files_to_pack", "assets")); // just so the packer works
                ExecNhmpackerCreateInstallers(GetRootPath("nhmpacker", "nhmpacker.exe"));
                File.Move(GetRootPath("nhmpacker", $"nhm_windows_{version}.exe"), GetRootPath(tmpWorkFolder, $"nhm_windows_{version}.exe"));
                File.Move(GetRootPath("nhmpacker", $"nhm_windows_updater_{version}.exe"), GetRootPath(tmpWorkFolder, $"nhm_windows_updater_{version}.exe"));
                // move to the temp folder

                // nhm_windows_1.9.2.18_testnetdev.zip
                // nhm_windows_1.9.2.18_testnet.zip
                // TODO create these settings instead of copying them
                var buildSettings = new List<BuildTag> { BuildTag.PRODUCTION, BuildTag.TESTNET, BuildTag.TESTNETDEV };
                foreach (var build in buildSettings)
                {
                    DeleteFileIfExists(GetRootPath(tmpWorkFolder, "Release", "build_settings.json"));
                    var zipFileName = $"nhm_windows_{version}";
                    if (build != BuildTag.PRODUCTION)
                    {
                        zipFileName += $"_{build.ToString().ToLower()}";
                        File.Copy(GetRootPath("build_settings", $"build_settings_{build.ToString()}.json"), GetRootPath(tmpWorkFolder, "Release", "build_settings.json"), true);
                    }
                    Console.WriteLine($"Creating {zipFileName}.zip package");
                    ZipFile.CreateFromDirectory(GetRootPath(tmpWorkFolder, "Release"), GetRootPath(tmpWorkFolder, $"{zipFileName}.zip"));
                    Console.WriteLine($"FINISHED {zipFileName}.zip package");
                }

                Console.WriteLine("Clean up temp files...");
                //Console.ReadKey();
                Directory.Delete(GetRootPath(tmpWorkFolder, "_files_to_pack"), true);
                Directory.Delete(GetRootPath(tmpWorkFolder, "Release"), true);
                Console.WriteLine("Finishing...");
                Directory.Move(GetRootPath(tmpWorkFolder), GetRootPath($"nhm_windows_{version}_release_files"));
                Console.WriteLine("DONE!");
                Console.ReadKey();
                return;
            }
            catch (Exception e)
            {
                Console.WriteLine($"\t\t PROBLEM: {e.Message} {e.Message}");
                Console.WriteLine($"\t\t PROBLEM: {e.StackTrace}");
                Console.ReadKey();
            }
        }
    }
}
