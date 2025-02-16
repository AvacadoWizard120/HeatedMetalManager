namespace HeatedMetalManager
{
    public class FileInstaller
    {
        private readonly string gameDirectory;
        private readonly string executableDirectory;

        public FileInstaller(string gameDirectory)
        {
            this.gameDirectory = gameDirectory;
            this.executableDirectory = AppDomain.CurrentDomain.BaseDirectory;
        }

        public bool HasLumaPlayFiles()
        {
            var lumaPlayDirectory = Path.Combine(executableDirectory, "LumaPlayFiles");
            var lumaFiles = Directory.GetFiles(lumaPlayDirectory, "*.*", SearchOption.AllDirectories);

            return lumaFiles.Any(file =>
                File.Exists(Path.Combine(gameDirectory, Path.GetFileName(file))));
        }

        public void RemoveLumaPlayFiles()
        {
            var lumaPlayDirectory = Path.Combine(executableDirectory, "LumaPlayFiles");
            var lumaFiles = Directory.GetFiles(lumaPlayDirectory, "*.*", SearchOption.AllDirectories);

            foreach (var file in lumaFiles)
            {
                var targetFile = Path.Combine(gameDirectory, Path.GetFileName(file));
                if (File.Exists(targetFile))
                {
                    File.Delete(targetFile);
                }
            }
        }

        public void InstallPlazaFiles()
        {
            var plazaDirectory = Path.Combine(executableDirectory, "Plazas");
            var plazaFiles = Directory.GetFiles(plazaDirectory, "*.*", SearchOption.AllDirectories);

            foreach (var file in plazaFiles)
            {
                var targetFile = Path.Combine(gameDirectory, Path.GetFileName(file));
                File.Copy(file, targetFile, true);
            }
        }
    }
}
