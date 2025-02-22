using HeatedMetalManager;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
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

    public OuterForm()
    {
        InitializeComponent();
        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        httpClient.DefaultRequestHeaders.Add("User-Agent", "HeatedMetalManager");
        settingsManager = new SettingsManager();
        DisableAllControls();
        CheckForManagerUpdateAsync(); // idgaf
        LoadSavedDirectoryAsync();
    }

    private void InitializeComponent()
    {
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

        Controls.AddRange(new Control[] {
                dirLabel, dirTextBox, browseButton,
                progressBar, statusLabel, updateButton,
                isVanillaLabel, releaseVersionLabel, VoHM,
                changeVersionsButton
            });

        browseButton.Click += BrowseButton_Click;
        updateButton.Click += UpdateButton_Click;
        changeVersionsButton.Click += ChangeVersionsButton_CLick;

        var progress = new Progress<int>(value => progressBar.Value = value);

        fileInstaller = new FileInstaller(gameDirectory, httpClient, progress);
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
            if (IsNewerVersion(latestTag, "0.5"))
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

            UpdateUIVersion();

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
            MessageBox.Show($"Error initializing game directory: {ex.Message}",
                "Initialization Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            browseButton.Enabled = true;
            statusLabel.Text = "Error during initialization. Please try again.";
        }
    }

    private void UpdateUIVersion()
    {
        if (fileInstaller?.IsUsingHeatedMetal() == true)
        {
            VoHM.Text = "Using Heated Metal";
            settingsManager.SetUsingVanilla(false);
        }
        else
        {
            VoHM.Text = "Using Vanilla";
            settingsManager.SetUsingVanilla(true);
        }
    }

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
            isVanillaLabel.Text = "Currently Installed: " + fileInstaller?.GetLocalVersion(); ;
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

        // Update release version label asynchronously
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

        try
        {
            await fileInstaller.SwapGameVersion(settingsManager.UsingVanilla);
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

        UpdateUIVersion();
    }

    private async Task<(string TagName, string DownloadUrl)> GetLatestReleaseInfo()
    {
        var response = await httpClient.GetStringAsync(
            $"https://api.github.com/repos/{RepoOwner}/{RepoName}/releases/latest");

        using var doc = JsonDocument.Parse(response);
        var root = doc.RootElement;

        UpdateUIVersion();

        return (
            root.GetProperty("tag_name").GetString()!,
            root.GetProperty("assets")[0].GetProperty("browser_download_url").GetString()!
        );
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

        UpdateUIVersion();
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
}