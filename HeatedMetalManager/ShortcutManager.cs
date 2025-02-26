namespace HeatedMetalManager
{
    public static class ShortcutManager
    {
        public static void CreateShortcut(string targetPath, string shortcutName)
        {
            try
            {
                var shellType = Type.GetTypeFromProgID("WScript.Shell");
                dynamic shell = Activator.CreateInstance(shellType);
                string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                string shortcutPath = Path.Combine(desktop, $"{shortcutName}.lnk");

                dynamic shortcut = shell.CreateShortcut(shortcutPath);
                shortcut.TargetPath = targetPath;
                shortcut.WorkingDirectory = Path.GetDirectoryName(targetPath);
                shortcut.Save();
            }
            catch
            {
            }
        }

        public static bool ShortcutExists(string targetPath)
        {
            try
            {
                string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                foreach (var file in Directory.GetFiles(desktop, "*.lnk"))
                {
                    var shellType = Type.GetTypeFromProgID("WScript.Shell");
                    dynamic shell = Activator.CreateInstance(shellType);
                    dynamic shortcut = shell.CreateShortcut(file);

                    if (shortcut.TargetPath.Equals(targetPath, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
                return false;
            }
            catch
            {
                return false;
            }
        }


        public static void DeleteShortcut(string targetPath)
        {
            try
            {
                string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                foreach (var file in Directory.GetFiles(desktop, "*.lnk"))
                {
                    var shellType = Type.GetTypeFromProgID("WScript.Shell");
                    dynamic shell = Activator.CreateInstance(shellType);
                    dynamic shortcut = shell.CreateShortcut(file);

                    if (shortcut.TargetPath.Equals(targetPath, StringComparison.OrdinalIgnoreCase))
                        File.Delete(file);
                }
            }
            catch
            {
            }
        }

    }
}
