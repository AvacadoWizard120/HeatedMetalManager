using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;

namespace HeatedMetalManager
{
    public class FileInstaller
    {
        private readonly string gameDirectory;
        private readonly string assemblyDirectory;
        private readonly IProgress<int>? progress;
        private readonly Assembly currentAssembly;
        private const string PlazaResourcePrefix = "HeatedMetalManager.Plazas.";
        private const string HeliosResourcePrefix = "HeatedMetalManager.Helios.";

        private static readonly string[] LumaPlayFiles = new[]
        {
            "cream_api.ini",
            "HOWTOUSE.txt",
            "LumaPlay_x64.exe",
        };

        public FileInstaller(string gameDirectory, IProgress<int>? progress = null)
        {
            this.gameDirectory = gameDirectory;
            this.progress = progress;
            this.currentAssembly = Assembly.GetExecutingAssembly();
            this.assemblyDirectory = System.AppContext.BaseDirectory;
        }

        public string GetHeatedMetalDLLDir()
        {
            return Path.Combine(gameDirectory, "HeatedMetal", "HeatedMetal.dll");
        }

        public string GetLocalVersion()
        {
            string dllPath = GetHeatedMetalDLLDir();

            if (!File.Exists(dllPath))
            {
                Debug.WriteLine("HeatedMetal.dll not found!");
                return "0.0.0";
            }

            try
            {
                // Initialize the interop with the dynamic path
                HeatedMetalInterop.Initialize(dllPath);

                // Call the functions
                IntPtr versionPtr = HeatedMetalInterop.HMVersion();
                string version = Marshal.PtrToStringAnsi(versionPtr)!;

                // Optional: Get integer version
                uint versionInt = HeatedMetalInterop.HMVersionInt();

                return version;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to get version: {ex.Message}");
                return "0.0.0";
            }
            finally
            {
                HeatedMetalInterop.Unload(); // Free the DLL
            }
        }


        public bool HasLumaPlayFiles()
        {
            return Directory.Exists(Path.Combine(gameDirectory, "LumaPlayFiles"));
        }

        public bool HasHeatedMetalInstalled()
        {
            return Directory.Exists(Path.Combine(gameDirectory, "HeatedMetal"));
        }

        public bool IsUsingHeatedMetal()
        {
            string heatedMetalPath = GetHeatedMetalDLLDir();
            string shadowLegacyPath = Path.Combine(gameDirectory, "HeatedMetal", "ShadowLegacy.dll");

            if (File.Exists(heatedMetalPath)) return true;
            if (File.Exists(shadowLegacyPath)) return false;

            return false;
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

            installHeliosFiles();
        }

        public void installHeliosFiles()
        {
            var heliosResources = GetEmbeddedResourceNames(HeliosResourcePrefix).ToList();
            if (heliosResources.Count == 0)
            {
                Debug.WriteLine("No Plaza resources found!");
                return;
            }

            int totalFiles = heliosResources.Count;
            int completedFiles = 0;

            foreach (var resourceName in heliosResources)
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

        public async Task SwapGameVersion(bool useVanilla)
        {
            Debug.WriteLine(useVanilla);

            await Task.Run(() =>
            {
                try
                {
                    progress?.Report(0);
                    string heatedMetalDir = Path.Combine(gameDirectory, "HeatedMetal");
                    string heatedMetalDllDir = Path.Combine(heatedMetalDir, "HeatedMetal.dll");
                    string shadowLegacyDllDir = Path.Combine(heatedMetalDir, "ShadowLegacy.dll");

                    FileInfo shadowLegacyDLL = new FileInfo(shadowLegacyDllDir);

                    FileInfo heatedMetalDLL = new FileInfo(heatedMetalDllDir);

                    if (!Directory.Exists(heatedMetalDir))
                    {
                        Debug.WriteLine("DID NOT FIND HEATED METAL DIRECTORY!");
                        throw new DirectoryNotFoundException("HeatedMetal directory not found.");
                    }

                    progress?.Report(25);

                    if (!useVanilla && File.Exists(heatedMetalDllDir))
                    {
                        Debug.WriteLine("FOUND HEATED METALL DLL!!");
                        heatedMetalDLL.MoveTo(shadowLegacyDllDir);
                        progress?.Report(50);
                    }
                    else if (useVanilla && File.Exists(shadowLegacyDllDir))
                    {
                        Debug.WriteLine("FOUND VANILLA DLL!!");
                        shadowLegacyDLL.MoveTo(heatedMetalDllDir);
                        progress?.Report(50);
                    }

                    progress?.Report(100);
                    Debug.WriteLine($"Successfully swapped game version. Using Vanilla: {useVanilla}");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error in SwapGameVersion: {ex.Message}");
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
                    }
                    else if (fileInfo.Length == HMDLLSize)
                    {
                        Debug.WriteLine("FOUND HEATED METAL DLL");
                        return true;
                    }
                }
            }



            Debug.WriteLine("NEITHER DLLs FOUND!!!");
            return false;
        }

        public void CheckHeatedMetalExports()
        {
            string heatedMetalPath = GetHeatedMetalDLLDir();

            if (!File.Exists(heatedMetalPath))
            {
                Debug.WriteLine("HeatedMetal.dll not found");
                return;
            } else
            {
                Debug.WriteLine("FOUND HEATED METAL DLL!!!!");
            }

            var exportReader = new DllExportReader(heatedMetalPath);
            exportReader.PrintExports();
        }
    }
}