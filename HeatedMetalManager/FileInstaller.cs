﻿using System.Diagnostics;
using System.Diagnostics.Eventing.Reader;
using System.Reflection;

namespace HeatedMetalManager
{
    public class FileInstaller
    {
        private readonly string gameDirectory;
        private readonly string assemblyDirectory;
        private readonly Assembly currentAssembly;
        private readonly IProgress<int>? progress;
        private const string PlazaResourcePrefix = "HeatedMetalManager.Plazas.";
        private const string ShadowLegacyDLLPrefix = "HeatedMetalManager.ShadowLegacy.";

        private static readonly string[] LumaPlayFiles = new[]
        {
            "cream_api.ini",
            "HOWTOUSE.txt",
            "LumaPlay_x64.exe",
            "steam_api64.dll",
            "steam_api64_o.dll"
        };

        public FileInstaller(string gameDirectory, IProgress<int>? progress = null)
        {
            this.gameDirectory = gameDirectory;
            this.progress = progress;
            this.currentAssembly = Assembly.GetExecutingAssembly();
            this.assemblyDirectory = System.AppContext.BaseDirectory;
        }

        public bool HasLumaPlayFiles()
        {
            string lumaPlayDir = Path.Combine(gameDirectory, "LumaPlayFiles");

            // Check if any of the LumaPlay files exist in the game directory
            if (Directory.Exists(lumaPlayDir))
            {
                return true;
            }
            else return false;
        }

        public bool HasHeatedMetalInstalled()
        {
            string heatedMetalDir = Path.Combine(gameDirectory, "HeatedMetal");

            if (Directory.Exists(heatedMetalDir))
            {
                return true;
            }
            else return false;
        }

        public void RemoveLumaPlayFiles()
        {
            string lumaPlayDir = Path.Combine(gameDirectory, "LumaPlayFiles");

            if (Directory.Exists(lumaPlayDir))
            {
                Directory.Delete(lumaPlayDir, recursive: true);
            }

            foreach (var file in LumaPlayFiles)
            {
                string filePath = Path.Combine(gameDirectory, file);

                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
            }
        }

        public void InstallPlazaFiles()
        {
            var plazaResources = GetEmbeddedResourceNames(PlazaResourcePrefix).ToList();
            if (plazaResources.Count == 0)
            {
                Debug.WriteLine("No Plaza resources found!");
                return;
            }

            int totalFiles = plazaResources.Count;
            int completedFiles = 0;

            foreach (var resourceName in plazaResources)
            {
                try
                {
                    var fileName = Path.GetFileName(resourceName.Substring(PlazaResourcePrefix.Length));
                    var targetPath = Path.Combine(gameDirectory, fileName);
                    Debug.WriteLine($"Installing: {fileName} to {targetPath}");

                    if (File.Exists(targetPath))
                    {
                        var backupPath = targetPath + ".backup";
                        Debug.WriteLine($"Creating backup at: {backupPath}");
                        File.Copy(targetPath, backupPath, overwrite: true);
                    }

                    using var resourceStream = currentAssembly.GetManifestResourceStream(resourceName);
                    if (resourceStream == null)
                    {
                        Debug.WriteLine($"Failed to get resource stream for: {resourceName}");
                        continue;
                    }

                    using var fileStream = File.Create(targetPath);
                    resourceStream.CopyTo(fileStream);

                    completedFiles++;
                    int progressPercentage = (completedFiles * 100) / totalFiles;
                    progress?.Report(progressPercentage);
                    Debug.WriteLine($"Successfully installed: {fileName}");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error installing {resourceName}: {ex.Message}");
                    throw;
                }
            }
        }

        public async Task SwapDefaultArgs(bool isVanilla)
        {
            await Task.Run(() =>
            {
                try
                {
                    progress?.Report(0);
                    string backupDir = Path.Combine(assemblyDirectory, "Backups");
                    Directory.CreateDirectory(backupDir);

                    if (!isVanilla)
                    {
                        progress?.Report(25);
                        string sourceFile = Path.Combine(gameDirectory, "DefaultArgs.dll");
                        string destFile = Path.Combine(backupDir, "DefaultArgs.dll");

                        if (!File.Exists(sourceFile))
                        {
                            throw new FileNotFoundException("DefaultArgs.dll not found in game directory.");
                        }

                        if (File.Exists(destFile))
                        {
                            File.Delete(destFile);
                        }

                        File.Move(sourceFile, destFile);
                        progress?.Report(50);

                        var vanillaResources = GetEmbeddedResourceNames(ShadowLegacyDLLPrefix).ToList();
                        foreach (var resourceName in vanillaResources)
                        {
                            progress?.Report(75);
                            var fileName = Path.GetFileName(resourceName.Substring(ShadowLegacyDLLPrefix.Length));
                            var targetPath = Path.Combine(gameDirectory, fileName);

                            using var resourceStream = currentAssembly.GetManifestResourceStream(resourceName);
                            if (resourceStream == null)
                            {
                                throw new InvalidOperationException($"Failed to get resource stream for: {resourceName}");
                            }

                            using var fileStream = File.Create(targetPath);
                            resourceStream.CopyTo(fileStream);
                        }
                    }
                    else
                    {
                        progress?.Report(25);
                        string sourceFile = Path.Combine(backupDir, "DefaultArgs.dll");
                        string destFile = Path.Combine(gameDirectory, "DefaultArgs.dll");

                        if (!File.Exists(sourceFile))
                        {
                            throw new FileNotFoundException("Backup DefaultArgs.dll not found.");
                        }

                        if (File.Exists(Path.Combine(gameDirectory, "defaultargs.dll")))
                        {
                            File.Delete(Path.Combine(gameDirectory, "defaultargs.dll"));
                        }

                        if (File.Exists(destFile))
                        {
                            File.Delete(destFile);
                        }

                        progress?.Report(50);
                        File.Move(sourceFile, destFile);
                        progress?.Report(75);
                    }

                    progress?.Report(100);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error in SwapDefaultArgs: {ex.Message}");
                    throw;
                }
            });
        }

        public int GetPlazaResourceCount()
        {
            var resources = GetEmbeddedResourceNames(PlazaResourcePrefix).ToList();
            Debug.WriteLine($"Found {resources.Count} Plaza resources:");
            foreach (var resource in resources)
            {
                Debug.WriteLine($"Resource: {resource}");
            }
            return resources.Count;
        }

        private IEnumerable<string> GetEmbeddedResourceNames(string prefix)
        {
            return currentAssembly.GetManifestResourceNames()
                .Where(name => name.StartsWith(prefix));
        }

        public bool CheckDefaultArgsDLL()
        {

            long vanillaDLLSize = 5551816;
            long HMDLLSize = 18432;

            if (!string.IsNullOrEmpty(gameDirectory))
            {
                string[] files = Directory.GetFiles(gameDirectory, "*.*", SearchOption.AllDirectories);

                foreach (string file in files)
                {
                    FileInfo fileInfo = new FileInfo(file);
                    if (fileInfo.Length == vanillaDLLSize)
                    {
                        Debug.WriteLine("FOUND VANILLA DLL");
                        return false;
                    } else if (fileInfo.Length == HMDLLSize)
                    {
                        Debug.WriteLine("FOUND HEATED METAL DLL");
                        return true;
                    }
                }
            }



            Debug.WriteLine("NEITHER DLLs FOUND!!!");
            return false;
        }
    }
}
