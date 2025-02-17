using System.Diagnostics;
using System.Reflection;

namespace HeatedMetalManager
{
    public class FileInstaller
    {
        private readonly string gameDirectory;
        private readonly string assemblyDirectory;
        private readonly Assembly currentAssembly;
        private const string PlazaResourcePrefix = "HeatedMetalManager.Plazas.";

        private static readonly string[] LumaPlayFiles = new[]
        {
            "cream_api.ini",
            "HOWTOUSE.txt",
            "LumaPlay_x64.exe",
            "steam_api64.dll",
            "steam_api64_o.dll"
        };

        public FileInstaller(string gameDirectory)
        {
            this.gameDirectory = gameDirectory;
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
                    Debug.WriteLine($"Successfully installed: {fileName}");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error installing {resourceName}: {ex.Message}");
                    throw;
                }
            }
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
    }
}
