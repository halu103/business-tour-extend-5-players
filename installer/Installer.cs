using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;

namespace BusinessTourFiveRealmsInstaller
{
    internal enum LoaderState
    {
        Absent,
        Exact
    }

    internal sealed class SetupOptions
    {
        internal bool ShowHelp { get; private set; }
        internal bool ShowVersion { get; private set; }
        internal bool VerifyOnly { get; private set; }
        internal bool Install { get; private set; }
        internal string GameRoot { get; private set; }
        internal string LogPath { get; private set; }

        internal static SetupOptions Parse(string[] args)
        {
            var result = new SetupOptions();
            for (int index = 0; index < args.Length; index++)
            {
                string value = args[index];
                if (String.Equals(value, "--help", StringComparison.OrdinalIgnoreCase) ||
                    String.Equals(value, "-h", StringComparison.OrdinalIgnoreCase) ||
                    String.Equals(value, "/?", StringComparison.OrdinalIgnoreCase))
                {
                    result.ShowHelp = true;
                }
                else if (String.Equals(value, "--version", StringComparison.OrdinalIgnoreCase))
                {
                    result.ShowVersion = true;
                }
                else if (String.Equals(value, "--verify-only", StringComparison.OrdinalIgnoreCase))
                {
                    result.VerifyOnly = true;
                }
                else if (String.Equals(value, "--install", StringComparison.OrdinalIgnoreCase))
                {
                    result.Install = true;
                }
                else if (String.Equals(value, "--game-root", StringComparison.OrdinalIgnoreCase))
                {
                    result.GameRoot = RequireValue(args, ref index, value);
                }
                else if (value.StartsWith("--game-root=", StringComparison.OrdinalIgnoreCase))
                {
                    result.GameRoot = value.Substring("--game-root=".Length);
                }
                else if (String.Equals(value, "--log", StringComparison.OrdinalIgnoreCase))
                {
                    result.LogPath = RequireValue(args, ref index, value);
                }
                else if (value.StartsWith("--log=", StringComparison.OrdinalIgnoreCase))
                {
                    result.LogPath = value.Substring("--log=".Length);
                }
                else
                {
                    throw new ArgumentException("Unknown command-line option: " + value);
                }
            }

            int modes = (result.VerifyOnly ? 1 : 0) + (result.Install ? 1 : 0);
            if (!result.ShowHelp && !result.ShowVersion && modes != 1)
            {
                throw new ArgumentException("Choose exactly one operation: --verify-only or --install.");
            }
            if ((result.ShowHelp || result.ShowVersion) && modes != 0)
            {
                throw new ArgumentException("--help and --version cannot be combined with an operation.");
            }
            return result;
        }

        private static string RequireValue(string[] args, ref int index, string option)
        {
            if (index + 1 >= args.Length || String.IsNullOrWhiteSpace(args[index + 1]))
            {
                throw new ArgumentException("Missing value for " + option + ".");
            }
            index++;
            return args[index];
        }
    }

    internal sealed class TextLog : IDisposable
    {
        private readonly object sync = new object();
        private readonly StreamWriter writer;

        internal TextLog(string path)
        {
            if (!String.IsNullOrWhiteSpace(path))
            {
                string full = Path.GetFullPath(path);
                string parent = Path.GetDirectoryName(full);
                if (!String.IsNullOrEmpty(parent))
                {
                    Directory.CreateDirectory(parent);
                }
                writer = new StreamWriter(new FileStream(full, FileMode.Append, FileAccess.Write, FileShare.Read),
                    new UTF8Encoding(false));
                writer.AutoFlush = true;
            }
        }

        internal void Write(string message)
        {
            string line = "[" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "] " + message;
            lock (sync)
            {
                Console.WriteLine(line);
                if (writer != null)
                {
                    writer.WriteLine(line);
                }
            }
        }

        public void Dispose()
        {
            if (writer != null)
            {
                writer.Dispose();
            }
        }
    }

    internal static class SetupEngine
    {
        internal const string PluginDirectory = @"BepInEx\plugins\BusinessTourFiveRealms";
        internal const string MarkerRelativePath = PluginDirectory + @"\install-marker.json";
        internal const string ConfigRelativePath = @"BepInEx\config\vn.businesstour.fiverealms.cfg";

        private static readonly string[] LoaderSentinels =
        {
            @".doorstop_version",
            @"doorstop_config.ini",
            @"winhttp.dll",
            @"BepInEx\core",
            @"BepInEx\core\BepInEx.Core.dll",
            @"dotnet",
            @"dotnet\.version"
        };

        private static readonly string[] ForeignInjectorFiles =
        {
            @"version.dll",
            @"winmm.dll",
            @"dinput8.dll",
            @"xinput1_3.dll",
            @"dxgi.dll"
        };

        internal static void Verify(string gameRoot, Action<string> log)
        {
            gameRoot = Path.GetFullPath(gameRoot);
            GameValidator.ValidateExact(gameRoot, log);
            using (ReleasePayload payload = ReleasePayload.ExtractAndValidate(log))
            {
                ValidatePayloadLayout(payload.Manifest);
                LoaderState state = InspectLoader(gameRoot, payload.Manifest, log);
                ReadExistingMarker(gameRoot, true, log);
                log(state == LoaderState.Exact
                    ? "Verification complete. A matching BepInEx installation will be preserved."
                    : "Verification complete. The pinned BepInEx build can be installed safely.");
            }
        }

        internal static void Install(string gameRoot, Action<string> log)
        {
            gameRoot = Path.GetFullPath(gameRoot);
            GameValidator.ValidateExact(gameRoot, log);
            using (IDisposable gameLock = GameValidator.AcquireGameLock(gameRoot))
            using (ReleasePayload payload = ReleasePayload.ExtractAndValidate(log))
            {
                ValidatePayloadLayout(payload.Manifest);
                LoaderState loaderState = InspectLoader(gameRoot, payload.Manifest, log);
                InstallMarker oldMarker = ReadExistingMarker(gameRoot, true, log);

                List<PayloadFile> modFiles = payload.Manifest.Files
                    .Where(file => String.Equals(file.Scope, "mod", StringComparison.Ordinal))
                    .ToList();
                List<PayloadFile> loaderFiles = payload.Manifest.Files
                    .Where(file => String.Equals(file.Scope, "loader", StringComparison.Ordinal))
                    .ToList();
                var newModPaths = new HashSet<string>(modFiles.Select(file => PathSecurity.NormalizeRelative(file.Path)),
                    StringComparer.OrdinalIgnoreCase);
                var obsoleteModPaths = new List<string>();
                if (oldMarker != null)
                {
                    foreach (PayloadFile file in oldMarker.Files)
                    {
                        string relative = PathSecurity.NormalizeRelative(file.Path);
                        if (String.Equals(file.Scope, "mod", StringComparison.Ordinal) &&
                            !newModPaths.Contains(relative) &&
                            !String.Equals(relative, ConfigRelativePath, StringComparison.OrdinalIgnoreCase))
                        {
                            obsoleteModPaths.Add(relative);
                        }
                    }
                }

                var transactionPaths = new List<string>();
                transactionPaths.AddRange(modFiles.Select(file => file.Path));
                if (loaderState == LoaderState.Absent)
                {
                    transactionPaths.AddRange(loaderFiles.Select(file => file.Path));
                }
                transactionPaths.AddRange(obsoleteModPaths);
                transactionPaths.Add(MarkerRelativePath);

                BackupSession backup = BackupSession.Create(gameRoot, transactionPaths, log);
                try
                {
                    if (loaderState == LoaderState.Absent)
                    {
                        foreach (PayloadFile file in loaderFiles)
                        {
                            CopyPayloadFile(gameRoot, payload.Root, file);
                        }
                        log("Pinned BepInEx build installed.");
                    }
                    else
                    {
                        log("Existing matching BepInEx files were left untouched.");
                    }

                    foreach (PayloadFile file in modFiles)
                    {
                        CopyPayloadFile(gameRoot, payload.Root, file);
                    }

                    foreach (string relative in obsoleteModPaths)
                    {
                        DeleteOwnedFile(gameRoot, relative);
                    }

                    var marker = new InstallMarker
                    {
                        ModId = BuildInfo.ModId,
                        ModVersion = BuildInfo.ModVersion,
                        InstalledUtc = DateTime.UtcNow.ToString("o"),
                        PayloadSha256 = BuildInfo.PayloadSha256,
                        LoaderInstalledByThisRun = loaderState == LoaderState.Absent,
                        Files = payload.Manifest.Files.Select(CloneFile).ToList()
                    };
                    WriteMarker(gameRoot, marker);
                    log("Business Tour Five Realms " + BuildInfo.ModVersion + " installed successfully.");
                    log("Backup retained at: " + backup.BackupRoot);
                }
                catch (Exception operationError)
                {
                    try
                    {
                        backup.Restore(log);
                    }
                    catch (Exception rollbackError)
                    {
                        throw new InvalidOperationException(
                            "Installation failed and rollback also failed. Backup: " + backup.BackupRoot +
                            Environment.NewLine + "Install error: " + operationError.Message +
                            Environment.NewLine + "Rollback error: " + rollbackError.Message,
                            operationError);
                    }
                    throw new InvalidOperationException("Installation failed; all managed files were rolled back. " +
                        operationError.Message, operationError);
                }
            }
        }

        private static void ValidatePayloadLayout(PayloadManifest manifest)
        {
            if (manifest.Files.Count == 0)
            {
                throw new InvalidDataException("The payload manifest is empty.");
            }
            if (!String.Equals(manifest.BepInExBuild, BuildInfo.BepInExBuild, StringComparison.Ordinal))
            {
                throw new InvalidDataException("The payload contains an unexpected BepInEx build.");
            }

            int modCount = 0;
            int loaderCount = 0;
            foreach (PayloadFile file in manifest.Files)
            {
                string relative = PathSecurity.NormalizeRelative(file.Path);
                RequireHash(file.Sha256, relative);
                if (String.Equals(file.Scope, "mod", StringComparison.Ordinal))
                {
                    if (!IsOwnedModPath(relative))
                    {
                        throw new InvalidDataException("A mod payload file is outside the owned mod directory: " + relative);
                    }
                    modCount++;
                }
                else if (String.Equals(file.Scope, "loader", StringComparison.Ordinal))
                {
                    if (!IsAllowedLoaderPath(relative))
                    {
                        throw new InvalidDataException("A loader payload file has an unexpected destination: " + relative);
                    }
                    loaderCount++;
                }
            }
            if (modCount == 0 || loaderCount == 0)
            {
                throw new InvalidDataException("The payload must contain both loader and mod files.");
            }
        }

        private static LoaderState InspectLoader(string gameRoot, PayloadManifest manifest, Action<string> log)
        {
            foreach (string relative in ForeignInjectorFiles)
            {
                string full = PathSecurity.CombineInside(gameRoot, relative);
                if (File.Exists(full))
                {
                    throw new InvalidDataException("A different root-level injector is present: " + relative +
                        ". Remove or isolate it before installing this mod.");
                }
            }

            List<PayloadFile> loaderFiles = manifest.Files
                .Where(file => String.Equals(file.Scope, "loader", StringComparison.Ordinal))
                .ToList();
            bool anySentinel = LoaderSentinels.Any(relative =>
                File.Exists(PathSecurity.CombineInside(gameRoot, relative)) ||
                Directory.Exists(PathSecurity.CombineInside(gameRoot, relative)));
            bool anyPayloadTarget = loaderFiles.Any(file =>
                File.Exists(PathSecurity.CombineInside(gameRoot, file.Path)));

            if (!anySentinel && !anyPayloadTarget)
            {
                log("No BepInEx loader installation was detected.");
                return LoaderState.Absent;
            }

            foreach (PayloadFile file in loaderFiles)
            {
                string relative = PathSecurity.NormalizeRelative(file.Path);
                string destination = PathSecurity.CombineInside(gameRoot, relative);
                PathSecurity.RejectReparsePoints(gameRoot, destination);
                if (!File.Exists(destination))
                {
                    throw new InvalidDataException("A partial or different BepInEx installation was detected. Missing: " + relative);
                }
                string actual = Hashing.Sha256(destination);
                if (!String.Equals(actual, file.Sha256, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException("A different BepInEx installation was detected. File differs: " + relative);
                }
            }
            log("The installed BepInEx files exactly match the pinned build.");
            return LoaderState.Exact;
        }

        private static InstallMarker ReadExistingMarker(string gameRoot, bool rejectUnownedDirectory, Action<string> log)
        {
            string pluginRoot = PathSecurity.CombineInside(gameRoot, PluginDirectory);
            string markerPath = PathSecurity.CombineInside(gameRoot, MarkerRelativePath);
            PathSecurity.RejectReparsePoints(gameRoot, markerPath);

            if (!File.Exists(markerPath))
            {
                if (rejectUnownedDirectory && Directory.Exists(pluginRoot) &&
                    Directory.EnumerateFileSystemEntries(pluginRoot).Any())
                {
                    throw new InvalidDataException("The Five Realms plugin folder exists without a valid install marker. " +
                        "Move or remove that folder manually before installing.");
                }
                return null;
            }

            InstallMarker marker;
            try
            {
                marker = Json.Read<InstallMarker>(markerPath);
            }
            catch (Exception error)
            {
                throw new InvalidDataException("The existing install marker cannot be read: " + error.Message, error);
            }
            ValidateMarker(marker);
            log("Existing managed installation detected: version " + marker.ModVersion + ".");
            return marker;
        }

        private static void ValidateMarker(InstallMarker marker)
        {
            if (marker == null || marker.Files == null ||
                !String.Equals(marker.ModId, BuildInfo.ModId, StringComparison.Ordinal))
            {
                throw new InvalidDataException("The existing install marker does not belong to this mod.");
            }

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (PayloadFile file in marker.Files)
            {
                if (file == null)
                {
                    throw new InvalidDataException("The install marker contains a null file entry.");
                }
                if (String.IsNullOrWhiteSpace(file.Path))
                {
                    throw new InvalidDataException("The install marker contains an empty file path.");
                }
                string relative = PathSecurity.NormalizeRelative(file.Path);
                if (!seen.Add(relative))
                {
                    throw new InvalidDataException("The install marker contains a duplicate path: " + relative);
                }
                RequireHash(file.Sha256, relative);
                if (String.Equals(file.Scope, "mod", StringComparison.Ordinal))
                {
                    if (!IsOwnedModPath(relative))
                    {
                        throw new InvalidDataException("The install marker contains an unowned mod path: " + relative);
                    }
                }
                else if (!String.Equals(file.Scope, "loader", StringComparison.Ordinal))
                {
                    throw new InvalidDataException("The install marker contains an invalid scope: " + relative);
                }
            }
        }

        private static bool IsOwnedModPath(string relative)
        {
            return relative.StartsWith(PluginDirectory + "\\", StringComparison.OrdinalIgnoreCase) ||
                String.Equals(relative, ConfigRelativePath, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsAllowedLoaderPath(string relative)
        {
            return relative.StartsWith("BepInEx\\", StringComparison.OrdinalIgnoreCase) ||
                relative.StartsWith("dotnet\\", StringComparison.OrdinalIgnoreCase) ||
                String.Equals(relative, ".doorstop_version", StringComparison.OrdinalIgnoreCase) ||
                String.Equals(relative, "doorstop_config.ini", StringComparison.OrdinalIgnoreCase) ||
                String.Equals(relative, "winhttp.dll", StringComparison.OrdinalIgnoreCase) ||
                String.Equals(relative, "changelog.txt", StringComparison.OrdinalIgnoreCase);
        }

        private static void RequireHash(string hash, string relative)
        {
            if (String.IsNullOrEmpty(hash) || !Regex.IsMatch(hash, "^[0-9A-Fa-f]{64}$"))
            {
                throw new InvalidDataException("Invalid SHA-256 in manifest for: " + relative);
            }
        }

        private static void CopyPayloadFile(string gameRoot, string payloadRoot, PayloadFile file)
        {
            string relative = PathSecurity.NormalizeRelative(file.Path);
            string source = PathSecurity.CombineInside(payloadRoot, relative);
            string destination = PathSecurity.CombineInside(gameRoot, relative);
            PathSecurity.RejectReparsePoints(gameRoot, destination);
            FileInstall.CopyVerified(source, destination, file.Sha256);
        }

        private static void DeleteOwnedFile(string gameRoot, string relative)
        {
            relative = PathSecurity.NormalizeRelative(relative);
            if (!IsOwnedModPath(relative))
            {
                throw new InvalidDataException("Refusing to delete an unowned path: " + relative);
            }
            string full = PathSecurity.CombineInside(gameRoot, relative);
            PathSecurity.RejectReparsePoints(gameRoot, full);
            if (File.Exists(full))
            {
                File.Delete(full);
                PathSecurity.PruneEmptyParents(gameRoot, full);
            }
        }

        private static PayloadFile CloneFile(PayloadFile source)
        {
            return new PayloadFile
            {
                Path = PathSecurity.NormalizeRelative(source.Path),
                Sha256 = source.Sha256.ToUpperInvariant(),
                Length = source.Length,
                Scope = source.Scope
            };
        }

        private static void WriteMarker(string gameRoot, InstallMarker marker)
        {
            string markerPath = PathSecurity.CombineInside(gameRoot, MarkerRelativePath);
            PathSecurity.RejectReparsePoints(gameRoot, markerPath);
            ValidateMarker(marker);
            Json.Write(markerPath, marker);
            ValidateMarker(Json.Read<InstallMarker>(markerPath));
        }
    }

    internal sealed class SetupForm : Form
    {
        private readonly TextBox gamePath = new TextBox();
        private readonly RichTextBox output = new RichTextBox();
        private readonly Button browseButton = new Button();
        private readonly Button verifyButton = new Button();
        private readonly Button installButton = new Button();
        private readonly Button closeButton = new Button();
        private bool busy;

        internal SetupForm()
        {
            Text = "Business Tour Five Realms - Cài đặt";
            Width = 760;
            Height = 520;
            MinimumSize = new System.Drawing.Size(680, 430);
            StartPosition = FormStartPosition.CenterScreen;
            Font = new System.Drawing.Font("Segoe UI", 9F);

            var title = new Label
            {
                AutoSize = true,
                Font = new System.Drawing.Font("Segoe UI Semibold", 15F),
                Text = "Business Tour Five Realms " + BuildInfo.ModVersion,
                Left = 18,
                Top = 16
            };
            var note = new Label
            {
                AutoSize = true,
                Text = "Chọn đúng thư mục Business Tour. Trình cài đặt sẽ kiểm tra phiên bản trước khi thay đổi file.",
                Left = 20,
                Top = 53
            };
            var pathLabel = new Label { AutoSize = true, Text = "Thư mục game:", Left = 20, Top = 88 };
            gamePath.Left = 20;
            gamePath.Top = 109;
            gamePath.Width = 600;
            gamePath.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            browseButton.Text = "Chọn...";
            browseButton.Left = 630;
            browseButton.Top = 107;
            browseButton.Width = 95;
            browseButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            browseButton.Click += BrowseClicked;

            output.Left = 20;
            output.Top = 145;
            output.Width = 705;
            output.Height = 275;
            output.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            output.ReadOnly = true;
            output.BackColor = System.Drawing.SystemColors.Window;
            output.DetectUrls = false;

            verifyButton.Text = "Chỉ kiểm tra";
            verifyButton.Left = 375;
            verifyButton.Top = 435;
            verifyButton.Width = 110;
            verifyButton.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            verifyButton.Click += delegate { RunOperation(false); };
            installButton.Text = "Cài đặt";
            installButton.Left = 495;
            installButton.Top = 435;
            installButton.Width = 110;
            installButton.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            installButton.Click += delegate { RunOperation(true); };
            closeButton.Text = "Đóng";
            closeButton.Left = 615;
            closeButton.Top = 435;
            closeButton.Width = 110;
            closeButton.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            closeButton.Click += delegate { Close(); };

            Controls.AddRange(new Control[]
            {
                title, note, pathLabel, gamePath, browseButton, output, verifyButton, installButton, closeButton
            });
            AcceptButton = installButton;
            CancelButton = closeButton;
            FormClosing += OnFormClosing;

            IList<string> candidates = GameLocator.FindCandidates();
            if (candidates.Count > 0)
            {
                gamePath.Text = candidates[0];
                Append("Đã tìm thấy thư mục game: " + candidates[0]);
                if (candidates.Count > 1)
                {
                    Append("Có nhiều bản cài đặt; hãy kiểm tra lại đường dẫn trước khi cài.");
                }
            }
            else
            {
                Append("Không tự tìm thấy game. Hãy bấm Chọn... và mở thư mục chứa BusinessTour.exe.");
            }
        }

        private void BrowseClicked(object sender, EventArgs e)
        {
            using (var dialog = new FolderBrowserDialog())
            {
                dialog.Description = "Chọn thư mục chứa BusinessTour.exe";
                dialog.ShowNewFolderButton = false;
                if (Directory.Exists(gamePath.Text))
                {
                    dialog.SelectedPath = gamePath.Text;
                }
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    gamePath.Text = dialog.SelectedPath;
                }
            }
        }

        private void RunOperation(bool install)
        {
            if (busy)
            {
                return;
            }
            string root = gamePath.Text.Trim();
            if (root.Length == 0)
            {
                MessageBox.Show(this, "Hãy chọn thư mục Business Tour.", "Thiếu đường dẫn",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            SetBusy(true);
            Append(install ? "Bắt đầu cài đặt..." : "Bắt đầu kiểm tra...");
            ThreadPool.QueueUserWorkItem(delegate
            {
                Exception failure = null;
                try
                {
                    if (install)
                    {
                        SetupEngine.Install(root, Append);
                    }
                    else
                    {
                        SetupEngine.Verify(root, Append);
                    }
                }
                catch (Exception error)
                {
                    failure = error;
                    Append("LỖI: " + error.Message);
                }

                BeginInvoke((Action)delegate
                {
                    SetBusy(false);
                    if (failure == null)
                    {
                        MessageBox.Show(this,
                            install ? "Cài đặt hoàn tất." : "Kiểm tra hoàn tất, không phát hiện xung đột.",
                            "Five Realms", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        if (install)
                        {
                            Close();
                        }
                    }
                    else
                    {
                        MessageBox.Show(this, failure.Message, "Không thể tiếp tục",
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                });
            });
        }

        private void SetBusy(bool value)
        {
            busy = value;
            gamePath.Enabled = !value;
            browseButton.Enabled = !value;
            verifyButton.Enabled = !value;
            installButton.Enabled = !value;
            closeButton.Enabled = !value;
            UseWaitCursor = value;
        }

        private void Append(string message)
        {
            if (IsDisposed)
            {
                return;
            }
            if (InvokeRequired)
            {
                BeginInvoke((Action<string>)Append, message);
                return;
            }
            output.AppendText("[" + DateTime.Now.ToString("HH:mm:ss") + "] " + message + Environment.NewLine);
            output.SelectionStart = output.TextLength;
            output.ScrollToCaret();
        }

        private void OnFormClosing(object sender, FormClosingEventArgs e)
        {
            if (busy)
            {
                e.Cancel = true;
                MessageBox.Show(this, "Đang xử lý file. Vui lòng chờ thao tác hoàn tất.", "Five Realms",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }
    }

    internal static class SetupProgram
    {
        private static readonly string Usage =
            "Business Tour Five Realms Setup " + BuildInfo.ModVersion + Environment.NewLine +
            "  Setup.exe --verify-only [--game-root <folder>] [--log <file>]" + Environment.NewLine +
            "  Setup.exe --install     [--game-root <folder>] [--log <file>]";

        [STAThread]
        private static int Main(string[] args)
        {
            SetupOptions options = null;
            if (args.Length > 0)
            {
                try
                {
                    options = SetupOptions.Parse(args);
                }
                catch (ArgumentException error)
                {
                    Console.Error.WriteLine(error.Message);
                    Console.Error.WriteLine(Usage);
                    return 2;
                }

                if (options.ShowHelp)
                {
                    Console.WriteLine(Usage);
                    return 0;
                }
                if (options.ShowVersion)
                {
                    Console.WriteLine(BuildInfo.ModVersion);
                    return 0;
                }
            }

            using (IDisposable instance = MaintenanceSingleInstance.TryAcquire())
            {
                if (instance == null)
                {
                    const string busyMessage =
                        "Một cửa sổ cài đặt hoặc gỡ Five Realms đang mở. Hãy hoàn tất hoặc đóng cửa sổ đó trước.";
                    if (args.Length == 0)
                    {
                        if (!MaintenanceSingleInstance.TryActivateExistingWindow())
                        {
                            MessageBox.Show(busyMessage, "Five Realms",
                                MessageBoxButtons.OK, MessageBoxIcon.Information);
                        }
                    }
                    else
                    {
                        Console.Error.WriteLine(busyMessage);
                    }
                    return 4;
                }

                if (args.Length == 0)
                {
                    Application.EnableVisualStyles();
                    Application.SetCompatibleTextRenderingDefault(false);
                    Application.Run(new SetupForm());
                    return 0;
                }

                try
                {
                    string root = ResolveGameRoot(options.GameRoot);
                    using (var log = new TextLog(options.LogPath))
                    {
                        if (options.VerifyOnly)
                        {
                            SetupEngine.Verify(root, log.Write);
                        }
                        else
                        {
                            SetupEngine.Install(root, log.Write);
                        }
                    }
                    return 0;
                }
                catch (ArgumentException error)
                {
                    Console.Error.WriteLine(error.Message);
                    Console.Error.WriteLine(Usage);
                    return 2;
                }
                catch (Exception error)
                {
                    Console.Error.WriteLine("Five Realms setup failed: " + error.Message);
                    return 3;
                }
            }
        }

        private static string ResolveGameRoot(string specified)
        {
            if (!String.IsNullOrWhiteSpace(specified))
            {
                return Path.GetFullPath(specified);
            }
            IList<string> candidates = GameLocator.FindCandidates();
            if (candidates.Count == 1)
            {
                return candidates[0];
            }
            if (candidates.Count == 0)
            {
                throw new ArgumentException("Business Tour was not found. Supply --game-root <folder>.");
            }
            throw new ArgumentException("Multiple Business Tour installations were found. Supply --game-root <folder>.");
        }
    }
}
