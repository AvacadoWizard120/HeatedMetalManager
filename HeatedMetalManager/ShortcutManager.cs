using IWshRuntimeLibrary;

namespace HeatedMetalManager
{
    public static class ShortcutManager
    {
        public static void CreateShortcut(string targetPath, string shortcutName)
        {
            var shell = new WshShell();
            var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            var shortcutPath = Path.Combine(desktop, shortcutName);

            if (System.IO.File.Exists(shortcutPath)) return;

            var shortcut = (IWshShortcut)shell.CreateShortcut(shortcutPath);
            shortcut.TargetPath = targetPath;
            shortcut.WorkingDirectory = Path.GetDirectoryName(targetPath);
            shortcut.Save();
        }

        public static bool ShortcutExists(string targetPath)
        {
            var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            foreach (var file in Directory.GetFiles(desktop, "*.lnk"))
            {
                var shell = new WshShell();
                var shortcut = (IWshShortcut)shell.CreateShortcut(file);
                if (shortcut.TargetPath.Equals(targetPath, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        public static void DeleteShortcut(string targetPath)
        {
            var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            foreach (var file in Directory.GetFiles(desktop, "*.lnk"))
            {
                try
                {
                    var shell = new WshShell();
                    var shortcut = (IWshShortcut)shell.CreateShortcut(file);
                    if (shortcut.TargetPath.Equals(targetPath, StringComparison.OrdinalIgnoreCase))
                    {
                        System.IO.File.Delete(file);
                    }
                }
                catch { /* Skip invalid shortcuts */ }
            }
        }

    }
}
