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

        private readonly HttpClient httpClient;

        private readonly string PlazaDownloadUrl = "https://github.com/AvacadoWizard120/HeatedMetalManager/raw/master/Plazas.rar";
        private readonly string HeliosDownloadUrl = "https://github.com/AvacadoWizard120/HeatedMetalManager/raw/master/Helios.rar";

        private static readonly string[] LumaPlayFiles = new[]
        {
            "cream_api.ini",
            "HOWTOUSE.txt",
            "LumaPlay_x64.exe",
        };

        public FileInstaller(string gameDirectory, HttpClient httpClient, IProgress<int>? progress = null)
        {
            this.gameDirectory = gameDirectory;
            this.progress = progress;
            this.currentAssembly = Assembly.GetExecutingAssembly();
            this.assemblyDirectory = System.AppContext.BaseDirectory;
            this.httpClient = httpClient;
        }

        public string GetAssemblyDirectory()
        {
            return assemblyDirectory;
        }

        public string GetHeatedMetalDLLDir()
        {
            string usingVanillaDLL = Path.Combine(gameDirectory, "HeatedMetal", "ShadowLegacy.dll");
            string usingHMDLL = Path.Combine(gameDirectory, "HeatedMetal", "HeatedMetal.dll");

            if (gameDirectory == null || !Directory.Exists(Path.Combine(gameDirectory, "HeatedMetal")))
            {
                return usingHMDLL;
            }

            if (File.Exists(usingHMDLL))
            {
                return usingHMDLL;
            }

            if (File.Exists(usingVanillaDLL))
            {
                Debug.WriteLine("FOUND SHADOWLEGACY.DLL!!");
                return usingVanillaDLL;
            }

            return usingHMDLL;
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
                HeatedMetalInterop.Initialize(dllPath);

                // Call HMVersion() to get the string
                IntPtr versionPtr = HeatedMetalInterop.HMVersion();
                string version = Marshal.PtrToStringAnsi(versionPtr)!;

                // Call HMVersionInt() to get the integer
                uint versionInt = HeatedMetalInterop.HMVersionInt();

                Debug.WriteLine($"Version: {version}, VersionInt: 0x{versionInt:X}");
                
                return version.Trim();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to get version: {ex.Message}");
                return "0.0.0";
            }
            finally
            {
                HeatedMetalInterop.Unload();
            }
        }


        public bool HasLumaPlayFiles()
        {
            return Directory.Exists(Path.Combine(gameDirectory, "LumaPlayFiles"));
        }

        public bool HasHeatedMetalInstalled()
        {
            string dllPath = GetHeatedMetalDLLDir();

            if (File.Exists(Path.Combine(gameDirectory, "HeatedMetal", "ShadowLegacy.dll")))
            {
                return true;
            }

            return File.Exists(dllPath);
        }

        public bool IsUsingHeatedMetal()
        {
            string heatedMetalPath = GetHeatedMetalDLLDir();
            string shadowLegacyPath = Path.Combine(gameDirectory, "HeatedMetal", "ShadowLegacy.dll");

            if (heatedMetalPath == shadowLegacyPath)
            {
                
                return false;
            }
            return true;
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

        public async Task InstallPlazaFiles()
        {
            await DownloadAndInstallPackage(PlazaDownloadUrl, "");
            await InstallHeliosFiles();

        }

        public async Task InstallHeliosFiles()
        {
            await DownloadAndInstallPackage(HeliosDownloadUrl, "");
        }

        private async Task DownloadAndInstallPackage(string downloadUrl, string packageName)
        {
            try
            {
                var tempPath = Path.GetTempFileName();

                // Download the package
                progress?.Report(0);
                await DownloadFileWithProgress(downloadUrl, tempPath);

                // Extract the package
                progress?.Report(50);
                await ExtractPackage(tempPath, packageName);
                progress?.Report(100);

                File.Delete(tempPath);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error installing {packageName}: {ex.Message}");
                throw;
            }
        }

        private async Task DownloadFileWithProgress(string url, string destination)
        {
            using var response = await httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
            var totalBytes = response.Content.Headers.ContentLength ?? -1L;

            using var stream = await response.Content.ReadAsStreamAsync();
            using var fileStream = File.Create(destination);

            var buffer = new byte[8192];
            var totalBytesRead = 0L;
            int bytesRead;

            while ((bytesRead = await stream.ReadAsync(buffer)) > 0)
            {
                await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead));
                totalBytesRead += bytesRead;

                if (totalBytes > 0)
                {
                    var progressPercent = (int)((totalBytesRead * 50) / totalBytes);
                    progress?.Report(progressPercent);
                }
            }
        }

        private async Task ExtractPackage(string archivePath, string packageName)
        {
            var extractPath = Path.Combine(gameDirectory);
            Directory.CreateDirectory(extractPath);

            var sevenZipPaths = new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "7-Zip", "7z.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "7-Zip", "7z.exe")
            };

            foreach (var path in sevenZipPaths)
            {
                if (File.Exists(path))
                {
                    await RunExtractionTool(path, "x", archivePath, extractPath);
                    return;
                }
            }

            var winrarPaths = new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "WinRAR", "WinRAR.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "WinRAR", "WinRAR.exe")
            };

            foreach (var path in winrarPaths)
            {
                if (File.Exists(path))
                {
                    await RunExtractionTool(path, "x", archivePath, extractPath);
                    return;
                }
            }

            throw new Exception("Neither 7-Zip nor WinRAR found. Please install one of them.");
        }

        private async Task RunExtractionTool(string toolPath, string command, string archivePath, string outputPath)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = toolPath,
                Arguments = $"{command} \"{archivePath}\" -o\"{outputPath}\" -y",
                UseShellExecute = false,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process == null) throw new Exception("Failed to start extraction process");

            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
            {
                var error = await process.StandardError.ReadToEndAsync();
                throw new Exception($"Extraction failed: {error}");
            }
        }




        public async Task SwapGameVersion()
        {
            await Task.Run(() =>
            {
                try
                {
                    bool useHeatedMetal = IsUsingHeatedMetal();

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

                    if (File.Exists(heatedMetalDllDir))
                    {
                        Debug.WriteLine("FOUND HEATED METALL DLL!!");
                        heatedMetalDLL.MoveTo(shadowLegacyDllDir);
                        progress?.Report(50);
                    }
                    else if (File.Exists(shadowLegacyDllDir))
                    {
                        Debug.WriteLine("FOUND VANILLA DLL!!");
                        shadowLegacyDLL.MoveTo(heatedMetalDllDir);
                        progress?.Report(50);
                    }

                    progress?.Report(100);
                    Debug.WriteLine($"Successfully swapped game version. Using Vanilla: {useHeatedMetal}");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error in SwapGameVersion: {ex.Message}");
                    throw;
                }
            });
        }

        public async Task CreateZipArchive(List<string> sourceDirectories, string outputPath)
        {
            var sevenZipPaths = new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "7-Zip", "7z.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "7-Zip", "7z.exe")
            };

            foreach (var path in sevenZipPaths)
            {
                if (File.Exists(path))
                {
                    await RunCompressionTool(path, "a", $"-tzip \"{outputPath}\"", sourceDirectories);
                    return;
                }
            }

            var winrarPaths = new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "WinRAR", "WinRAR.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "WinRAR", "WinRAR.exe")
            };

            foreach (var path in winrarPaths)
            {
                if (File.Exists(path))
                {
                    await RunCompressionTool(path, "a", $"-afzip \"{outputPath}\"", sourceDirectories);
                    return;
                }
            }

            throw new Exception("Neither 7-Zip nor WinRAR found. Please install one of them.");
        }

        private async Task RunCompressionTool(string toolPath, string command, string arguments, List<string> sourceDirectories)
        {
            string sourceArgs = string.Join(" ", sourceDirectories.Select(dir => $"\"{dir}\""));
            string fullArgs = $"{command} {arguments} {sourceArgs}";

            var startInfo = new ProcessStartInfo
            {
                FileName = toolPath,
                Arguments = fullArgs,
                UseShellExecute = false,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process == null) throw new Exception("Failed to start compression process");

            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
            {
                var error = await process.StandardError.ReadToEndAsync();
                throw new Exception($"Compression failed: {error}");
            }
        }

    }
}