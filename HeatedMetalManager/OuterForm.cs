using HeatedMetalManager;
using System.Diagnostics;
using System.Text.Json;

public partial class OuterForm : Form
{
    private const string RepoOwner = "DataCluster0";
    private const string RepoName = "HeatedMetal";
    private const string ReleaseFile = "HeatedMetal.7z";
    private const string VersionFile = "version.txt";
    private const string GameExe = "RainbowSix.exe";

    private readonly HttpClient httpClient = new();
    private readonly SettingsManager settingsManager;
    private FileInstaller? fileInstaller;
    private string gameDirectory = string.Empty;

    private TextBox dirTextBox;
    private Button browseButton;
    private Button updateButton;
    private ProgressBar progressBar;
    private Label statusLabel;

    public OuterForm()
    {
        InitializeComponent();
        httpClient.DefaultRequestHeaders.Add("User-Agent", "HeatedMetalManager");
        settingsManager = new SettingsManager();
        LoadSavedDirectory();
    }

    private void InitializeComponent()
    {
        Text = "Heated Metal Manager";
        Size = new Size(600, 200);
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

        Controls.AddRange(new Control[] {
                dirLabel, dirTextBox, browseButton,
                progressBar, statusLabel, updateButton
            });

        browseButton.Click += BrowseButton_Click;
        updateButton.Click += UpdateButton_Click;
    }

    private void LoadSavedDirectory()
    {
        var savedDirectory = settingsManager.GetGameDirectory();
        if (!string.IsNullOrEmpty(savedDirectory) && Directory.Exists(savedDirectory))
        {
            gameDirectory = savedDirectory;
            dirTextBox.Text = gameDirectory;
            fileInstaller = new FileInstaller(gameDirectory);
            CheckInstallation();
        }
    }

    private async Task CheckInstallation()
    {
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

            updateButton.Enabled = result == DialogResult.Yes;
            if (!updateButton.Enabled)
            {
                statusLabel.Text = "Please accept LumaPlay replacement to continue.";
            }
        }
        else
        {
            updateButton.Enabled = true;
            statusLabel.Text = "Ready to check for updates.";
        }
    }

    private void BrowseButton_Click(object? sender, EventArgs e)
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
                fileInstaller = new FileInstaller(gameDirectory);
                CheckInstallation();
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
            progressBar.Value = 0;

            // Check versions
            statusLabel.Text = "Checking for updates...";
            var (currentTag, downloadUrl) = await GetLatestReleaseInfo();
            var localVersion = GetLocalVersion();

            if (localVersion == currentTag)
            {
                statusLabel.Text = "Already up to date!";
                return;
            }

            // Download update
            statusLabel.Text = "Downloading update...";
            var tempFile = Path.Combine(Path.GetTempPath(), ReleaseFile);
            await DownloadFileWithProgress(downloadUrl, tempFile, progressBar);

            // Extract update
            statusLabel.Text = "Extracting update...";
            await ExtractUpdate(tempFile);
            File.WriteAllText(Path.Combine(gameDirectory, VersionFile), currentTag);

            // Cleanup
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
            progressBar.Value = 0;
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
            var resourceCount = fileInstaller.GetPlazaResourceCount();
            if (resourceCount == 0)
            {
                MessageBox.Show(
                    "No Plaza files found in the application resources. Please ensure the Plaza files are properly embedded in the application.",
                    "Installation Warning",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
                return;
            }

            fileInstaller.InstallPlazaFiles();
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
    }

    private async Task<(string TagName, string DownloadUrl)> GetLatestReleaseInfo()
    {
        var response = await httpClient.GetStringAsync(
            $"https://api.github.com/repos/{RepoOwner}/{RepoName}/releases/latest");

        using var doc = JsonDocument.Parse(response);
        var root = doc.RootElement;

        return (
            root.GetProperty("tag_name").GetString()!,
            root.GetProperty("assets")[0].GetProperty("browser_download_url").GetString()!
        );
    }

    private string? GetLocalVersion()
    {
        var versionFile = Path.Combine(gameDirectory, VersionFile);
        return File.Exists(versionFile) ? File.ReadAllText(versionFile).Trim() : null;
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
}