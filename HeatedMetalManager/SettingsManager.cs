using System.Text.Json;

namespace HeatedMetalManager
{

    public class UserSettings
    {
        public string? GameDirectory { get; set; }

        public bool IsVanilla { get; set; }
        public bool UsingVanilla { get; set; }
        public bool UseVanillaProfile { get; set; }
        public string? VanillaProfile { get; set; }
    }

    public class SettingsManager
    {
        private const string SettingsFileName = "settings.json";
        private readonly string settingsFilePath;
        private UserSettings currentSettings;

        public bool VanillaProfileEnabled => currentSettings.UseVanillaProfile;
        public string? VanillaProfile => currentSettings.VanillaProfile;


        public SettingsManager()
        {
            string exeDirectory = AppDomain.CurrentDomain.BaseDirectory;
            settingsFilePath = Path.Combine(exeDirectory, SettingsFileName);
            currentSettings = LoadSettings();
        }

        private UserSettings LoadSettings()
        {
            if (!File.Exists(settingsFilePath))
            {
                return new UserSettings();
            }

            try
            {
                string jsonContent = File.ReadAllText(settingsFilePath);
                return JsonSerializer.Deserialize<UserSettings>(jsonContent) ?? new UserSettings();
            }
            catch (Exception)
            {
                return new UserSettings();
            }
        }

        public void SaveSettings()
        {
            try
            {
                string jsonContent = JsonSerializer.Serialize(currentSettings, new JsonSerializerOptions
                {
                    WriteIndented = true
                });
                File.WriteAllText(settingsFilePath, jsonContent);
            }
            catch (Exception ex)
            {
                throw new Exception("Failed to save settings", ex);
            }
        }

        public string? GetGameDirectory() => currentSettings.GameDirectory;

        public void SetGameDirectory(string directory)
        {
            currentSettings.GameDirectory = directory;
            SaveSettings();
        }

        public bool IsVanilla() => currentSettings.IsVanilla;

        public void ChangeVersions(bool isVanilla)
        {
            currentSettings.IsVanilla = isVanilla;
            SaveSettings();
        }


        public bool UsingVanilla => currentSettings.UsingVanilla;

        public void SetUsingVanilla(bool usingVanilla)
        {
            currentSettings.UsingVanilla = usingVanilla;
            SaveSettings();
        }
        public void SetUseVanillaProfile(bool useVanillaProfile)
        {
            currentSettings.UseVanillaProfile = useVanillaProfile;
            SaveSettings();
        }

        public void SetVanillaProfile(string profile)
        {
            currentSettings.VanillaProfile = profile;
            SaveSettings();
        }
    }
}