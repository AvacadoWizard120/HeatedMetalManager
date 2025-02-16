using System.Reflection;

namespace HeatedMetalManager
{
    public class FileInstaller
    {
        private readonly string gameDirectory;
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
        }

        public bool HasLumaPlayFiles()
        {
            string lumaPlayDir = Path.Combine(gameDirectory, "LumaPlayFiles");

            // Check if any of the LumaPlay files exist in the game directory
            if (Directory.Exists(lumaPlayDir))
                return true;
                return LumaPlayFiles.Any(file =>
                    File.Exists(Path.Combine(gameDirectory, file)));
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
            var plazaResources = GetEmbeddedResourceNames(PlazaResourcePrefix);

            foreach (var resourceName in plazaResources)
            {
                var fileName = Path.GetFileName(resourceName.Substring(PlazaResourcePrefix.Length));
                var targetPath = Path.Combine(gameDirectory, fileName);

                if (File.Exists(targetPath))
                {
                    var backupPath = targetPath + ".backup";
                    File.Copy(targetPath, backupPath, overwrite: true);
                }

                using var resourceStream = currentAssembly.GetManifestResourceStream(resourceName);
                using var fileStream = File.Create(targetPath);
                resourceStream.CopyTo(fileStream);
            }
        }

        private IEnumerable<string> GetEmbeddedResourceNames(string prefix)
        {
            return currentAssembly.GetManifestResourceNames()
                .Where(name => name.StartsWith(prefix));
        }
    }
}
