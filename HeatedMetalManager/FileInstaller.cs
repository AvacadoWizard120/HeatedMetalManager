using System.Reflection;

namespace HeatedMetalManager
{
    public class FileInstaller
    {
        private readonly string gameDirectory;
        private readonly Assembly currentAssembly;
        private const string LumaPlayResourcePrefix = "HeatedMetalManager.LumaPlayFiles.";
        private const string PlazaResourcePrefix = "HeatedMetalManager.Plazas.";

        public FileInstaller(string gameDirectory)
        {
            this.gameDirectory = gameDirectory;
            this.currentAssembly = Assembly.GetExecutingAssembly();
        }

        public bool HasLumaPlayFiles()
        {
            var lumaPlayResources = GetEmbeddedResourceNames(LumaPlayResourcePrefix);

            // Check if any of the LumaPlay files exist in the game directory
            return lumaPlayResources.Any(resourceName =>
            {
                var fileName = Path.GetFileName(resourceName.Substring(LumaPlayResourcePrefix.Length));
                var targetPath = Path.Combine(gameDirectory, fileName);

                if (!File.Exists(targetPath))
                    return false;

                // Compare file content to ensure it's actually a LumaPlay file
                using var resourceStream = currentAssembly.GetManifestResourceStream(resourceName);
                using var fileStream = File.OpenRead(targetPath);
                return StreamsAreEqual(resourceStream, fileStream);
            });
        }

        public void RemoveLumaPlayFiles()
        {
            var lumaPlayResources = GetEmbeddedResourceNames(LumaPlayResourcePrefix);

            foreach (var resourceName in lumaPlayResources)
            {
                var fileName = Path.GetFileName(resourceName.Substring(LumaPlayResourcePrefix.Length));
                var targetPath = Path.Combine(gameDirectory, fileName);

                if (File.Exists(targetPath))
                {
                    // Verify it's a LumaPlay file before deleting
                    using var resourceStream = currentAssembly.GetManifestResourceStream(resourceName);
                    using var fileStream = File.OpenRead(targetPath);

                    if (StreamsAreEqual(resourceStream, fileStream))
                    {
                        File.Delete(targetPath);
                    }
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

        private bool StreamsAreEqual(Stream stream1, Stream stream2)
        {
            const int bufferSize = 8192;
            var buffer1 = new byte[bufferSize];
            var buffer2 = new byte[bufferSize];

            while (true)
            {
                int count1 = stream1.Read(buffer1, 0, bufferSize);
                int count2 = stream2.Read(buffer2, 0, bufferSize);

                if (count1 != count2)
                    return false;

                if (count1 == 0)
                    return true;

                for (int i = 0; i < count1; i++)
                {
                    if (buffer1[i] != buffer2[i])
                        return false;
                }
            }
        }
    }
}
