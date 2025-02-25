﻿using HeatedMetalManager;
using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text.Json;

public partial class OuterForm : Form
{
    private const string RepoOwner = "DataCluster0";
    private const string RepoName = "HeatedMetal";
    private const string ReleaseFile = "HeatedMetal.7z";
    private const string GameExe = "RainbowSix.exe";

    private readonly HttpClient httpClient = new();
    private readonly SettingsManager settingsManager;
    private FileInstaller? fileInstaller;
    private string gameDirectory = string.Empty;

    private TextBox dirTextBox;
    private Button browseButton;
    private Button updateButton;
    private Button changeVersionsButton;
    private ProgressBar progressBar;
    private Label statusLabel;
    private Label isVanillaLabel;
    private Label releaseVersionLabel;
    private Label VoHM;

    // For the profile manager tab
    private ComboBox profileComboBox;
    private Button usernameButton;
    private TextBox usernameTextBox;
    private Button savePathButton;
    private TextBox savePathTextBox;
    private Button openExplorerButton;

    public OuterForm()
    {
        this.AutoScaleMode = AutoScaleMode.None;
        InitializeComponent();
        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        httpClient.DefaultRequestHeaders.Add("User-Agent", "HeatedMetalManager");
        settingsManager = new SettingsManager();
        DisableAllControls();
        CheckForManagerUpdateAsync();
        LoadSavedDirectoryAsync();
    }

    private void InitializeComponent()
    {
        this.SuspendLayout();

        Text = "Heated Metal Manager";
        Size = new Size(600, 600);
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;

        var dirLabel = new Label
        {
            Text = "Game Directory:",
            Location = new Point(10, 15),
            AutoSize = true
        };

        dirTextBox = new TextBox
        {
            Location = new Point(10, 40),
            Width = 450,
            ReadOnly = true
        };

        browseButton = new Button
        {
            Text = "Browse",
            Location = new Point(470, 38),
            Width = 100
        };

        changeVersionsButton = new Button
        {
            Text = "Swap Versions",
            Location = new Point(200, 200),
            Width = 100,
            Enabled = false
        };

        progressBar = new ProgressBar
        {
            Location = new Point(10, 80),
            Width = 560,
            Height = 20
        };

        statusLabel = new Label
        {
            Location = new Point(10, 110),
            Width = 560,
            AutoSize = true
        };

        updateButton = new Button
        {
            Text = "Check for Updates/Install",
            Location = new Point(10, 130),
            Width = 560,
            Height = 30,
            Enabled = false
        };

        isVanillaLabel = new Label
        {
            Text = "Version: N/A",
            Location = new Point(10, 160),
            Width = 100,
            AutoSize = true
        };

        releaseVersionLabel = new Label
        {
            Text = "Latest: Fetching...",
            Location = new Point(10, 180),
            Width = 100,
            AutoSize = true
        };

        VoHM = new Label
        {
            Location = new Point(10, 200),
            Width = 100,
            AutoSize = true
        };

        TabControl tabControl = new TabControl
        {
            Location = new Point(5, 1),
            Size = new Size(580, 580),
            Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
        };

        TabPage mainTab = new TabPage("Main");
        TabPage profileTab = new TabPage("Profile Manager");

        mainTab.Controls.AddRange(new Control[] {
                dirLabel, dirTextBox, browseButton,
                progressBar, statusLabel, updateButton,
                isVanillaLabel, releaseVersionLabel, VoHM,
                changeVersionsButton
            });

        InitializeProfileTab(profileTab);

        tabControl.TabPages.Add(mainTab);

        tabControl.TabPages.Add(profileTab);

        browseButton.Click += BrowseButton_Click;
        updateButton.Click += UpdateButton_Click;
        changeVersionsButton.Click += ChangeVersionsButton_CLick;

        var progress = new Progress<int>(value => progressBar.Value = value);

        fileInstaller = new FileInstaller(gameDirectory, httpClient, progress);

        this.Controls.Add(tabControl);
        this.ResumeLayout(false);
    }

    private void DisableAllControls()
    {
        updateButton.Enabled = false;
        browseButton.Enabled = false;
        changeVersionsButton.Enabled = false;
        progressBar.Value = 0;
        statusLabel.Text = "Initializing...";
    }

    private async Task<(string TagName, string DownloadUrl)> GetLatestManagerVersion()
    {
        var response = await httpClient.GetStringAsync($"https://api.github.com/repos/AvacadoWizard120/HeatedMetalManager/releases/latest");

        using var doc = JsonDocument.Parse(response);
        var root = doc.RootElement;

        var assets = root.GetProperty("assets");
        var managerAsset = assets.EnumerateArray()
            .FirstOrDefault(asset => asset.GetProperty("name").GetString() == "HeatedMetalManager.exe");

        if (managerAsset.ValueKind == JsonValueKind.Undefined)
        {
            throw new Exception("Manager update file not found in release");
        }

        return (
            root.GetProperty("tag_name").GetString()!,
            managerAsset.GetProperty("browser_download_url").GetString()!
        );
    }

    private bool IsNewerVersion(string latestVersion, string currentVersion)
    {
        latestVersion = latestVersion.TrimStart('v');
        currentVersion = currentVersion.TrimStart('v');

        Version latest = Version.Parse(latestVersion);
        Version current = Version.Parse(currentVersion);

        return latest > current;
    }

    private async Task UpdateManager(string downloadUrl)
    {
        try
        {
            statusLabel.Text = "Downloading update...";
            progressBar.Value = 0;

            // Download to a temporary file
            var tempFile = Path.Combine(Path.GetTempPath(), "HeatedMetalManager.exe");
            await DownloadFileWithProgress(downloadUrl, tempFile, progressBar);

            // Create batch file for updating
            string batchPath = Path.Combine(Path.GetTempPath(), "update.bat");
            string exePath = Application.ExecutablePath;
            string updateScript = $@"
@echo off
timeout /t 1 /nobreak >nul
move /y ""{tempFile}"" ""{exePath}""
start """" ""{exePath}""
del ""%~f0""
";
            File.WriteAllText(batchPath, updateScript);

            Process.Start(new ProcessStartInfo
            {
                FileName = batchPath,
                CreateNoWindow = true,
                UseShellExecute = false
            });

            Application.Exit();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error during update: {ex.Message}",
                "Update Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async Task CheckForManagerUpdateAsync()
    {
        try
        {
            var (latestTag, downloadUrl) = await GetLatestManagerVersion();
            if (IsNewerVersion(latestTag, "0.6"))
            {
                var result = MessageBox.Show(
                    $"A new version of Heated Metal Manager ({latestTag}) is available. Would you like to update?",
                    "Update Available",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Information
                );

                if (result == DialogResult.Yes)
                {
                    await UpdateManager(downloadUrl);
                }
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error checking for manager updates: {ex.Message}",
                "Update Check Failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private async void LoadSavedDirectoryAsync()
    {
        try
        {
            var savedDirectory = settingsManager.GetGameDirectory();
            if (!string.IsNullOrEmpty(savedDirectory) && Directory.Exists(savedDirectory))
            {
                gameDirectory = savedDirectory;
                dirTextBox.Text = gameDirectory;

                

                var progress = new Progress<int>(value => progressBar.Value = value);
                fileInstaller = new FileInstaller(gameDirectory, httpClient, progress);

                await InitializeGameDirectory();
                LoadProfileConfig();
                UpdateProfileUI();
            }
            else
            {
                browseButton.Enabled = true;
                statusLabel.Text = "Please select a valid game directory.";
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error loading saved directory: {ex.Message}",
                "Initialization Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            browseButton.Enabled = true;
            statusLabel.Text = "Please select a valid game directory.";
        }
    }


    private async Task InitializeGameDirectory()
    {
        try
        {
            DisableAllControls();

            // Check for LumaPlay installation
            if (fileInstaller?.HasLumaPlayFiles() == true)
            {
                var result = MessageBox.Show(
                    "LumaPlay is installed in your game directory. Would you like to replace it with the new files?",
                    "LumaPlay Detected",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning
                );

                if (result == DialogResult.Yes)
                {
                    await HandleLumaPlayReplacement();
                }
                else
                {
                    statusLabel.Text = "Please accept LumaPlay replacement to continue.";
                    browseButton.Enabled = true;
                    return;
                }
            }

            // Get latest release info
            var (currentTag, downloadUrl) = await GetLatestReleaseInfo();
            var localVersion = fileInstaller?.GetLocalVersion();
            var isHeatedMetalInstalled = fileInstaller?.HasHeatedMetalInstalled() ?? false;

            UpdateAllStatus();

            if (isHeatedMetalInstalled)
            {
                isVanillaLabel.Text = "Currently Installed: " + localVersion;
                releaseVersionLabel.Text = "Latest Release: " + currentTag;

                // Enable or disable update button based on version comparison
                updateButton.Enabled = localVersion != currentTag;
                changeVersionsButton.Enabled = true;
                statusLabel.Text = localVersion == currentTag ?
                    "Already up to date!" :
                    "Updates available. Click 'Check for Updates/Install' to update.";
            }
            else
            {
                isVanillaLabel.Text = "Vanilla";
                releaseVersionLabel.Text = "Latest HM Release: " + currentTag;
                updateButton.Enabled = true;
                changeVersionsButton.Enabled = false;
                statusLabel.Text = "Ready to install Heated Metal.";
            }

            browseButton.Enabled = true;
        }
        catch (Exception ex)
        {
            string message = ex.Message;

            string rateMessage = message.Contains("rate limit")
            ? $"{message}\n\nTo avoid this, create a GitHub Personal Access Token (PAT) and set it as an environment variable named 'GITHUB_TOKEN'."
            : $"Error initializing game directory: {message}";


            MessageBox.Show(message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            browseButton.Enabled = true;
            statusLabel.Text = "Error during initialization. Please try again.";
        }
    }

    // private void UpdateUIVersion()
    // {
    //     if (fileInstaller?.IsUsingHeatedMetal() == true)
    //     {
    //         VoHM.Text = "Using Heated Metal";
    //         settingsManager.SetUsingVanilla(false);
    //     }
    //     else
    //     {
    //         VoHM.Text = "Using Vanilla";
    //         settingsManager.SetUsingVanilla(true);
    //     }
    // }

    private void UpdateAllStatus()
    {
        if (string.IsNullOrEmpty(gameDirectory) || !Directory.Exists(gameDirectory))
        {
            statusLabel.Text = "Please select a valid game directory.";
            isVanillaLabel.Text = "Not installed";
            releaseVersionLabel.Text = "No version information";
            VoHM.Text = "No version detected";
            updateButton.Enabled = false;
            changeVersionsButton.Enabled = false;
            return;
        }

        bool isHeatedMetalMode = fileInstaller?.IsUsingHeatedMetal() ?? false;
        settingsManager.SetUsingVanilla(!isHeatedMetalMode);

        VoHM.Text = isHeatedMetalMode ? "Using Heated Metal" : "Using Vanilla";

        if (fileInstaller?.HasHeatedMetalInstalled() == true)
        {
            isVanillaLabel.Text = "Currently Installed: " + fileInstaller?.GetLocalVersion();
            updateButton.Enabled = true;
            changeVersionsButton.Enabled = true;
            statusLabel.Text = "Ready to check for updates.";
        }
        else
        {
            isVanillaLabel.Text = "Heated Metal not installed";
            updateButton.Enabled = true;
            changeVersionsButton.Enabled = false;
            statusLabel.Text = "Ready to install Heated Metal.";
        }

        vanillaProfileCheckbox.Checked = settingsManager.VanillaProfileEnabled;
        vanillaProfileComboBox.Enabled = settingsManager.VanillaProfileEnabled;
        if (!string.IsNullOrEmpty(settingsManager.VanillaProfile))
        {
            vanillaProfileComboBox.SelectedItem = settingsManager.VanillaProfile;
        }

        if (!fileInstaller.IsUsingHeatedMetal() && settingsManager.VanillaProfileEnabled)
        {
            SyncWithHeliosLoader();
        }

        UpdateReleaseVersionLabel();
    }


    private async void UpdateReleaseVersionLabel()
    {
        try
        {
            var (currentTag, _) = await GetLatestReleaseInfo();
            releaseVersionLabel.Text = "Latest Release: " + currentTag;

            if (currentTag == releaseVersionLabel.Text)
            {
                updateButton.Enabled = false;
            }
        }
        catch (Exception ex)
        {
            releaseVersionLabel.Text = "Unable to fetch latest version";
            Debug.WriteLine($"Error fetching release info: {ex.Message}");
        }
    }

    private async void BrowseButton_Click(object? sender, EventArgs e)
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "Select Game Directory"
        };

        if (dialog.ShowDialog() == DialogResult.OK)
        {
            var exePath = Path.Combine(dialog.SelectedPath, GameExe);
            if (File.Exists(exePath))
            {
                gameDirectory = dialog.SelectedPath;
                dirTextBox.Text = gameDirectory;
                settingsManager.SetGameDirectory(gameDirectory);

                var progress = new Progress<int>(value => progressBar.Value = value);
                fileInstaller = new FileInstaller(gameDirectory, httpClient, progress);

                await InitializeGameDirectory();
                LoadProfileConfig();
                UpdateProfileUI();
            }
            else
            {
                MessageBox.Show($"Selected folder must contain {GameExe}",
                    "Invalid Directory", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    private async void UpdateButton_Click(object? sender, EventArgs e)
    {
        try
        {
            updateButton.Enabled = false;
            browseButton.Enabled = false;
            changeVersionsButton.Enabled = false;
            progressBar.Value = 0;

            statusLabel.Text = "Checking dll...";
            progressBar.Value = 10;

            if (!fileInstaller.IsUsingHeatedMetal())
            {
                Debug.WriteLine("IsUsingHeatedMetal() ===== FALSE!");
                progressBar.Value = 50;
                await fileInstaller.SwapGameVersion();
                progressBar.Value = 100;
            }

            progressBar.Value = 0;

            statusLabel.Text = "Checking for updates...";
            var (currentTag, downloadUrl) = await GetLatestReleaseInfo();
            var localVersion = fileInstaller?.GetLocalVersion();
            var isHeatedMetalInstalled = fileInstaller?.HasHeatedMetalInstalled() ?? false;

            if (localVersion == currentTag && isHeatedMetalInstalled)
            {
                statusLabel.Text = "Already up to date!";
                return;
            }

            statusLabel.Text = "Downloading update...";
            var tempFile = Path.Combine(Path.GetTempPath(), ReleaseFile);
            await DownloadFileWithProgress(downloadUrl, tempFile, progressBar);

            statusLabel.Text = "Extracting update...";
            await ExtractUpdate(tempFile);

            File.Delete(tempFile);
            statusLabel.Text = "Installation completed successfully!";
            MessageBox.Show("Installation completed successfully!", "Success",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            statusLabel.Text = "Error occurred during installation.";
            MessageBox.Show($"Error: {ex.Message}", "Error",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            updateButton.Enabled = true;
            browseButton.Enabled = true;
            changeVersionsButton.Enabled = true;
            progressBar.Value = 0;
            UpdateAllStatus();
        }
    }

    private async void ChangeVersionsButton_CLick(object? sender, EventArgs e)
    {
        updateButton.Enabled = false;
        browseButton.Enabled = false;
        changeVersionsButton.Enabled = false;

        settingsManager.ChangeVersions(!fileInstaller.IsUsingHeatedMetal());
        UpdateAllStatus();

        try
        {
            await fileInstaller.SwapGameVersion();
            statusLabel.Text = "Game version changed successfully!";
            MessageBox.Show("Game version changed successfully!", "Success",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            statusLabel.Text = "Error changing game version.";
            MessageBox.Show($"Error: {ex.Message}", "Error",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            updateButton.Enabled = true;
            browseButton.Enabled = true;
            changeVersionsButton.Enabled = true;
            UpdateAllStatus();
            SyncWithHeliosLoader();
        }
    }

    private async Task HandleLumaPlayReplacement()
    {
        if (fileInstaller == null) return;

        try
        {
            statusLabel.Text = "Removing existing LumaPlay files...";
            fileInstaller.RemoveLumaPlayFiles();
            progressBar.Value = 75;

            MessageBox.Show(
                "LumaPlay files have been successfully removed from your game directory.",
                "LumaPlay Removal Success",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information
            );

            statusLabel.Text = "Installing new files...";

            await fileInstaller.InstallPlazaFiles();
            progressBar.Value = 100;
            statusLabel.Text = "Installation completed.";
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"An error occurred while handling LumaPlay files: {ex.Message}",
                "Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            throw;
        }

        UpdateAllStatus();
    }

    private async Task<(string TagName, string DownloadUrl)> GetLatestReleaseInfo()
    {
        string cachePath = Path.Combine(Path.GetTempPath(), "HeatedMetalReleaseCache.json");

        // Attempt to use cached data if available
        if (File.Exists(cachePath))
        {
            try
            {
                var cachedJson = File.ReadAllText(cachePath);
                using var cachedDoc = JsonDocument.Parse(cachedJson);
                var root = cachedDoc.RootElement;
                return (
                    root.GetProperty("tag_name").GetString()!,
                    root.GetProperty("assets")[0].GetProperty("browser_download_url").GetString()!
                );
            }
            catch { /* Ignore invalid cache */ }
        }

        // Fetch fresh data with authentication
        try
        {
            var request = new HttpRequestMessage(
                HttpMethod.Get,
                $"https://api.github.com/repos/{RepoOwner}/{RepoName}/releases/latest"
            );

            // Add GitHub token if available (optional for users)
            var ghToken = Environment.GetEnvironmentVariable("GITHUB_TOKEN");
            if (!string.IsNullOrEmpty(ghToken))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ghToken);
            }

            var response = await httpClient.SendAsync(request);

            // Handle rate limits
            if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
            {
                var rateLimitReset = response.Headers.GetValues("X-RateLimit-Reset").FirstOrDefault();
                var resetTime = DateTimeOffset.FromUnixTimeSeconds(long.Parse(rateLimitReset ?? "0"));
                throw new HttpRequestException($"GitHub API rate limit exceeded. Reset at: {resetTime:g}");
            }

            response.EnsureSuccessStatusCode();

            var responseBody = await response.Content.ReadAsStringAsync();
            File.WriteAllText(cachePath, responseBody); // Update cache

            using var doc = JsonDocument.Parse(responseBody);
            var root = doc.RootElement;
            return (
                root.GetProperty("tag_name").GetString()!,
                root.GetProperty("assets")[0].GetProperty("browser_download_url").GetString()!
            );
        }
        catch (HttpRequestException ex)
        {
            if (File.Exists(cachePath))
            {
                // Use stale cache if available
                var cachedJson = File.ReadAllText(cachePath);
                using var cachedDoc = JsonDocument.Parse(cachedJson);
                var root = cachedDoc.RootElement;
                return (
                    root.GetProperty("tag_name").GetString()!,
                    root.GetProperty("assets")[0].GetProperty("browser_download_url").GetString()!
                );
            }
            throw new Exception($"Failed to fetch release data. {ex.Message}");
        }
    }


    private async Task DownloadFileWithProgress(string url, string destination, ProgressBar progress)
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
                var progressPercent = (int)((totalBytesRead * 100) / totalBytes);
                progress.Value = progressPercent;
            }
        }

        UpdateAllStatus();
    }

    private async Task ExtractUpdate(string archivePath)
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
                await RunExtractionTool(path, "x", archivePath);
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
                await RunExtractionTool(path, "x", archivePath);
                return;
            }
        }

        throw new Exception("Neither 7-Zip nor WinRAR found. Please install one of them.");
    }

    private async Task RunExtractionTool(string toolPath, string command, string archivePath)
    {
        bool exclusionAdded = false;
        bool isExtracted = false;
        string directoryToExclude = Path.GetDirectoryName(archivePath);

        try
        {
            await RunExtractionInternal(toolPath, command, archivePath);
        }
        catch (Exception ex) when (IsAntivirusError(ex))
        {
            var result = MessageBox.Show(
                "Your antivirus fucked HeatedMetal.7z, would you like the Manager to add a temporary exclusion and continue?",
                "Antivirus shit",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question
            );

            if (result == DialogResult.Yes)
            {
                await AddAntivirusExclusionAsync(directoryToExclude);
                exclusionAdded = true;
                await RunExtractionInternal(toolPath, command, archivePath);
                isExtracted = true;
            }
            else
            {
                MessageBox.Show("Aborted... Closing manager.", "Exiting", MessageBoxButtons.OK, MessageBoxIcon.Information);
                Application.Exit();
            }


        }
        finally
        {
            if (exclusionAdded && isExtracted)
            {
                try
                {
                    await RemoveAntivirusExclusionAsync(directoryToExclude);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to remove antivirus exclusion: {ex.Message}");
                }
            }
        }
    }

    private async Task RunExtractionInternal(string toolPath, string command, string archivePath)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = toolPath,
            Arguments = $"{command} \"{archivePath}\" -y",
            WorkingDirectory = gameDirectory,
            UseShellExecute = false,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using var process = Process.Start(startInfo);
        if (process == null)
            throw new Exception("Failed to start extraction process");

        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
        {
            var error = await process.StandardError.ReadToEndAsync();
            throw new Exception($"Extraction failed: {error}");
        }
    }

    private bool IsAntivirusError(Exception ex)
    {
        string error = ex.Message.ToLower();

        return error.Contains("virus") || error.Contains("blocked") || error.Contains("access denied");
    }

    private async Task AddAntivirusExclusionAsync(string directoryPath)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = $"-Command Add-MpPreference -ExclusionPath \"{directoryPath}\"",
            Verb = "runas", // Requires admin privileges
            UseShellExecute = true,
            CreateNoWindow = true
        };

        using var process = Process.Start(startInfo);
        if (process == null)
            throw new Exception("Failed to start exclusion process");

        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
            throw new Exception($"Failed to add exclusion. Exit code: {process.ExitCode}");
    }

    private async Task RemoveAntivirusExclusionAsync(string directoryPath)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = $"-Command Remove-MpPreference -ExclusionPath \"{directoryPath}\"",
            Verb = "runas",
            UseShellExecute = true,
            CreateNoWindow = true
        };

        using var process = Process.Start(startInfo);
        if (process == null)
            throw new Exception("Failed to start exclusion removal process");

        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
            throw new Exception($"Failed to remove exclusion. Exit code: {process.ExitCode}");
    }


    // Profile Management for HeliosLoader

    private string HeliosLoaderPath => Path.Combine(gameDirectory, "HeliosLoader.json");

    private CheckBox vanillaProfileCheckbox;
    private ComboBox vanillaProfileComboBox;

    private GameProfileConfig profileConfig = new GameProfileConfig();
    private string ProfileConfigPath => Path.Combine(fileInstaller.GetAssemblyDirectory(), "ProfileConfig.json");

    private void InitializeProfileTab(TabPage profileTab)
    {
        Label profileLabel = new Label { Text = "Profile:", Location = new Point(10, 20), Width = 60 };
        profileComboBox = new ComboBox { Location = new Point(70, 17), Width = 200 };
        profileComboBox.SelectedIndexChanged += ProfileComboBox_SelectedIndexChanged;

        usernameButton = new Button { Text = "Username:", Location = new Point(10, 60), Width = 200 };
        usernameTextBox = new TextBox { Location = usernameButton.Location, Width = 200, Visible = false };
        usernameButton.Click += (s, e) => ShowEditableField(usernameButton, usernameTextBox);

        savePathButton = new Button { Text = "Save Path:", Location = new Point(10, 100), Width = 200 };
        savePathTextBox = new TextBox { Location = savePathButton.Location, Width = 200, Visible = false };
        savePathButton.Click += (s, e) => ShowEditableField(savePathButton, savePathTextBox);

        openExplorerButton = new Button { Text = "Open in Explorer", Location = new Point(10, 200), Width = 200 };
        openExplorerButton.Click += OpenExplorerButton_Click;

        vanillaProfileCheckbox = new CheckBox
        {
            Text = "Use specific profile for vanilla",
            Location = new Point(10, 140),
            Width = 200
        };

        vanillaProfileComboBox = new ComboBox
        {
            Location = new Point(10, 165),
            Width = 200,
            Enabled = false
        };

        vanillaProfileCheckbox.CheckedChanged += VanillaCheckbox_CheckedChanged;
        vanillaProfileComboBox.SelectedIndexChanged += VanillaCombo_SelectedIndexChanged;


        profileTab.Controls.AddRange(new Control[] {
        profileLabel, profileComboBox, usernameButton, usernameTextBox,
        savePathButton, savePathTextBox, openExplorerButton,
        vanillaProfileCheckbox, vanillaProfileComboBox
    });

        usernameTextBox.LostFocus += (s, e) => SaveField(usernameButton, usernameTextBox);
        savePathTextBox.LostFocus += (s, e) => SaveField(savePathButton, savePathTextBox);
        usernameTextBox.KeyPress += (s, e) => { if (e.KeyChar == (char)Keys.Enter) SaveField(usernameButton, usernameTextBox); };
        savePathTextBox.KeyPress += (s, e) => { if (e.KeyChar == (char)Keys.Enter) SaveField(savePathButton, savePathTextBox); };
    }

    private void UpdateVanillaProfileList()
    {
        vanillaProfileComboBox.Items.Clear();
        if (!Directory.Exists(Path.Combine(gameDirectory, profileConfig.SavePath))) return;

        foreach (var dir in Directory.GetDirectories(Path.Combine(gameDirectory, profileConfig.SavePath)))
        {
            vanillaProfileComboBox.Items.Add(Path.GetFileName(dir));
        }
    }


    private void ShowEditableField(Button button, TextBox textBox)
    {
        button.Visible = false;
        textBox.Text = button.Text.Split(':')[1].Trim();
        textBox.Visible = true;
        textBox.Focus();
    }

    private void SaveField(Button button, TextBox textBox)
    {
        button.Text = $"{button.Text.Split(':')[0]}: {textBox.Text}";
        textBox.Visible = false;
        button.Visible = true;

        if (button == usernameButton)
        {
            profileConfig.Username = textBox.Text;
        }
        else if (button == savePathButton)
        {
            profileConfig.SavePath = textBox.Text;
        }

        SaveProfileConfig();
    }


    private void LoadProfileConfig()
    {
        if (File.Exists(HeliosLoaderPath))
        {
            try
            {
                var heliosJson = File.ReadAllText(HeliosLoaderPath);
                var heliosConfig = JsonSerializer.Deserialize<HeliosConfig>(heliosJson);

                profileConfig.Username = heliosConfig.Username;
                profileConfig.ProfileID = heliosConfig.ProfileID;
                profileConfig.SavePath = heliosConfig.SavePath;
                profileConfig.ProductID = heliosConfig.ProductID;
            }
            catch { /* fuck off */ }
        }

        if (!File.Exists(HeliosLoaderPath))
        {
            SetProfileControlsEnabled(false);
        }
        else
        {
            SetProfileControlsEnabled(true);
        }

        if (!File.Exists(ProfileConfigPath))
        {
            SaveProfileConfig();
            return;
        }
        try
        {
            string json = File.ReadAllText(ProfileConfigPath);
#pragma warning disable CS8601 // Possible null reference assignment.
            profileConfig = JsonSerializer.Deserialize<GameProfileConfig>(json);
#pragma warning restore CS8601 // Possible null reference assignment.
        }
        catch (JsonException ex)
        {
            MessageBox.Show($"Error loading profile config: {ex.Message}. Creating new config.");
            profileConfig = new GameProfileConfig();
            SaveProfileConfig();
        }

        vanillaProfileCheckbox.Checked = settingsManager.VanillaProfileEnabled;
        vanillaProfileComboBox.Enabled = settingsManager.VanillaProfileEnabled;
        if (!string.IsNullOrEmpty(settingsManager.VanillaProfile))
        {
            vanillaProfileComboBox.SelectedItem = settingsManager.VanillaProfile;
        }

        UpdateProfileUI();
    }

    private void SaveProfileConfig()
    {
        vanillaProfileCheckbox.CheckedChanged -= VanillaCheckbox_CheckedChanged;
        vanillaProfileComboBox.SelectedIndexChanged -= VanillaProfileComboBox_SelectedIndexChanged;

        try
        {
            settingsManager.SetUseVanillaProfile(vanillaProfileCheckbox.Checked);
            if (vanillaProfileComboBox.SelectedItem != null)
            {
                settingsManager.SetVanillaProfile(vanillaProfileComboBox.SelectedItem.ToString());
            }

            string json = JsonSerializer.Serialize(profileConfig, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(ProfileConfigPath, json);

            SyncWithHeliosLoader();
        }
        finally
        {
            vanillaProfileCheckbox.CheckedChanged += VanillaCheckbox_CheckedChanged;
            vanillaProfileComboBox.SelectedIndexChanged += VanillaProfileComboBox_SelectedIndexChanged;
        }
    }

    private void VanillaCheckbox_CheckedChanged(object sender, EventArgs e)
    {
        settingsManager.SetUseVanillaProfile(vanillaProfileCheckbox.Checked);
        vanillaProfileComboBox.Enabled = vanillaProfileCheckbox.Checked;
        SaveProfileConfig();
        if (!fileInstaller.IsUsingHeatedMetal() && vanillaProfileCheckbox.Checked)
        {
            SyncWithHeliosLoader();
        }
    }

    private void VanillaCombo_SelectedIndexChanged(object sender, EventArgs e)
    {
        if (vanillaProfileComboBox.SelectedItem != null)
        {
            settingsManager.SetVanillaProfile(vanillaProfileComboBox.SelectedItem.ToString());
            SaveProfileConfig();
            if (!fileInstaller.IsUsingHeatedMetal() && settingsManager.VanillaProfileEnabled)
            {
                SyncWithHeliosLoader();
            }
        }
    }

    private void UpdateProfileUI()
    {
        vanillaProfileCheckbox.Checked = settingsManager.IsVanilla();
        usernameButton.Text = $"Username: {profileConfig.Username}";
        savePathButton.Text = $"Save Path: {profileConfig.SavePath}";
        LoadProfiles();
        UpdateVanillaProfileList();
    }

    private void LoadProfiles()
    {
        profileComboBox.SelectedIndexChanged -= ProfileComboBox_SelectedIndexChanged;
        vanillaProfileComboBox.SelectedIndexChanged -= VanillaCombo_SelectedIndexChanged;

        try
        {
            profileComboBox.Items.Clear();
            vanillaProfileComboBox.Items.Clear();

            string saveDir = Path.Combine(gameDirectory, profileConfig.SavePath);
            if (Directory.Exists(saveDir))
            {
                var dirs = Directory.GetDirectories(saveDir, "*", new EnumerationOptions
                {
                    MaxRecursionDepth = 1,
                    IgnoreInaccessible = true
                });

                foreach (var dir in dirs)
                {
                    string profileName = Path.GetFileName(dir);
                    if (!profileComboBox.Items.Contains(profileName))
                        profileComboBox.Items.Add(profileName);
                    if (!vanillaProfileComboBox.Items.Contains(profileName))
                        vanillaProfileComboBox.Items.Add(profileName);
                }
            }

            profileComboBox.SelectedItem = profileConfig.ProfileID;
            vanillaProfileComboBox.SelectedItem = settingsManager.VanillaProfile;
        }
        finally
        {
            profileComboBox.SelectedIndexChanged += ProfileComboBox_SelectedIndexChanged;
            vanillaProfileComboBox.SelectedIndexChanged += VanillaCombo_SelectedIndexChanged;
        }
    }

    private void VanillaProfileComboBox_SelectedIndexChanged(object sender, EventArgs e)
    {
        if (vanillaProfileComboBox.SelectedItem != null)
        {
            settingsManager.SetVanillaProfile(vanillaProfileComboBox.SelectedItem.ToString());
            SaveProfileConfig();
            if (!fileInstaller.IsUsingHeatedMetal() && settingsManager.VanillaProfileEnabled)
            {
                SyncWithHeliosLoader();
            }
        }
    }

    private void ProfileComboBox_SelectedIndexChanged(object sender, EventArgs e)
    {
        if (profileComboBox.SelectedItem != null)
        {
            profileConfig.ProfileID = profileComboBox.SelectedItem.ToString();
            SaveProfileConfig();
        }
    }

    private void OpenExplorerButton_Click(object sender, EventArgs e)
    {
        string path = Path.Combine(gameDirectory, profileConfig.SavePath, profileConfig.ProfileID, profileConfig.ProductID.ToString());
        if (Directory.Exists(path)) Process.Start("explorer.exe", path);
    }


    private void SyncWithHeliosLoader()
    {
        if (!File.Exists(HeliosLoaderPath))
        {
            var result = MessageBox.Show(
                "HeliosLoader not found. Get it from:\n" +
                "R6S: Operation Throwback 2.0 | Heated Metal > releases\n" +
                "https://discord.com/channels/1092820800203141130/1335739761670754395/1338604727188848710",
                "HeliosLoader Missing",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning
            );
            SetProfileControlsEnabled(false);
            return;
        }

        try
        {
            var json = File.ReadAllText(HeliosLoaderPath);
            var heliosConfig = JsonSerializer.Deserialize<HeliosConfig>(json)!;

            heliosConfig.Username = profileConfig.Username;
            heliosConfig.SavePath = profileConfig.SavePath;
            heliosConfig.ProductID = profileConfig.ProductID;
            heliosConfig.Email = $"{profileConfig.Username}@ubisoft.com";

            if (!fileInstaller.IsUsingHeatedMetal() && settingsManager.VanillaProfileEnabled)
            {
                heliosConfig.ProfileID = settingsManager.VanillaProfile;
            }
            else
            {
                heliosConfig.ProfileID = profileConfig.ProfileID;
            }

            File.WriteAllText(HeliosLoaderPath,
                JsonSerializer.Serialize(heliosConfig, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error syncing with HeliosLoader: {ex.Message}");
        }
    }


    private void SetProfileControlsEnabled(bool enabled)
    {
        profileComboBox.Enabled = enabled;
        usernameButton.Enabled = enabled;
        savePathButton.Enabled = enabled;
        openExplorerButton.Enabled = enabled;

        if (!enabled)
        {
            usernameButton.Text = "Profile viewing only available through HeliosLoader";
            savePathButton.Text = "Profile viewing only available through HeliosLoader";
        }
    }
}