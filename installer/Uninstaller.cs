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
    internal sealed class RemovalOptions
    {
        internal bool ShowHelp { get; private set; }
        internal bool ShowVersion { get; private set; }
        internal bool VerifyOnly { get; private set; }
        internal bool Uninstall { get; private set; }
        internal string GameRoot { get; private set; }
        internal string LogPath { get; private set; }

        internal static RemovalOptions Parse(string[] args)
        {
            var result = new RemovalOptions();
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
                else if (String.Equals(value, "--uninstall", StringComparison.OrdinalIgnoreCase))
                {
                    result.Uninstall = true;
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

            int modes = (result.VerifyOnly ? 1 : 0) + (result.Uninstall ? 1 : 0);
            if (!result.ShowHelp && !result.ShowVersion && modes != 1)
            {
                throw new ArgumentException("Choose exactly one operation: --verify-only or --uninstall.");
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

    internal sealed class RemovalLog : IDisposable
    {
        private readonly object sync = new object();
        private readonly StreamWriter writer;

        internal RemovalLog(string path)
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

    internal static class RemovalEngine
    {
        private const string PluginDirectory = @"BepInEx\plugins\BusinessTourFiveRealms";
        private const string MarkerRelativePath = PluginDirectory + @"\install-marker.json";
        private const string ConfigRelativePath = @"BepInEx\config\vn.businesstour.fiverealms.cfg";

        internal static void Verify(string gameRoot, Action<string> log)
        {
            gameRoot = Path.GetFullPath(gameRoot);
            GameValidator.ValidateShape(gameRoot, log);
            InstallMarker marker = ReadMarker(gameRoot);
            int present = 0;
            int changed = 0;
            foreach (PayloadFile file in marker.Files.Where(item =>
                String.Equals(item.Scope, "mod", StringComparison.Ordinal)))
            {
                string relative = PathSecurity.NormalizeRelative(file.Path);
                string full = PathSecurity.CombineInside(gameRoot, relative);
                PathSecurity.RejectReparsePoints(gameRoot, full);
                if (File.Exists(full))
                {
                    present++;
                    if (!String.Equals(Hashing.Sha256(full), file.Sha256, StringComparison.OrdinalIgnoreCase))
                    {
                        changed++;
                        log("Warning: this managed mod file has changed and will be backed up before removal: " + relative);
                    }
                }
            }
            if (File.Exists(PathSecurity.CombineInside(gameRoot, ConfigRelativePath)))
            {
                present++;
            }
            log("Uninstall marker verified for Five Realms " + marker.ModVersion + ".");
            log("Owned files present: " + present + "; changed files: " + changed + ".");
            log("BepInEx and every unrelated plugin will be preserved.");
        }

        internal static void Uninstall(string gameRoot, Action<string> log)
        {
            gameRoot = Path.GetFullPath(gameRoot);
            GameValidator.ValidateShape(gameRoot, log);
            using (IDisposable gameLock = GameValidator.AcquireGameLock(gameRoot))
            {
                InstallMarker marker = ReadMarker(gameRoot);
                List<string> ownedFiles = GetOwnedFiles(marker);
                foreach (PayloadFile file in marker.Files.Where(item =>
                    String.Equals(item.Scope, "mod", StringComparison.Ordinal)))
                {
                    string relative = PathSecurity.NormalizeRelative(file.Path);
                    string full = PathSecurity.CombineInside(gameRoot, relative);
                    if (File.Exists(full) &&
                        !String.Equals(Hashing.Sha256(full), file.Sha256, StringComparison.OrdinalIgnoreCase))
                    {
                        log("Warning: backing up a locally changed file before removal: " + relative);
                    }
                }

                var transactionPaths = new List<string>(ownedFiles) { MarkerRelativePath };
                BackupSession backup = BackupSession.Create(gameRoot, transactionPaths, log);
                try
                {
                    foreach (string relative in ownedFiles)
                    {
                        DeleteOwnedFile(gameRoot, relative);
                    }
                    DeleteMarker(gameRoot);
                    log("Five Realms was removed successfully.");
                    log("BepInEx was intentionally left installed because it may be shared by other mods.");
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
                            "Removal failed and rollback also failed. Backup: " + backup.BackupRoot +
                            Environment.NewLine + "Removal error: " + operationError.Message +
                            Environment.NewLine + "Rollback error: " + rollbackError.Message,
                            operationError);
                    }
                    throw new InvalidOperationException("Removal failed; all managed files were rolled back. " +
                        operationError.Message, operationError);
                }
            }
        }

        private static InstallMarker ReadMarker(string gameRoot)
        {
            string markerPath = PathSecurity.CombineInside(gameRoot, MarkerRelativePath);
            PathSecurity.RejectReparsePoints(gameRoot, markerPath);
            if (!File.Exists(markerPath))
            {
                throw new InvalidDataException("Five Realms is not installed by this installer (install marker is missing).");
            }

            InstallMarker marker;
            try
            {
                marker = Json.Read<InstallMarker>(markerPath);
            }
            catch (Exception error)
            {
                throw new InvalidDataException("The install marker cannot be read. No files were changed: " + error.Message, error);
            }
            ValidateMarker(marker);
            return marker;
        }

        private static void ValidateMarker(InstallMarker marker)
        {
            if (marker == null || marker.Files == null ||
                !String.Equals(marker.ModId, BuildInfo.ModId, StringComparison.Ordinal))
            {
                throw new InvalidDataException("The install marker does not belong to Five Realms.");
            }
            if (!String.IsNullOrEmpty(marker.PayloadSha256) &&
                !Regex.IsMatch(marker.PayloadSha256, "^[0-9A-Fa-f]{64}$"))
            {
                throw new InvalidDataException("The install marker has an invalid payload hash.");
            }

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int modFiles = 0;
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
                if (String.IsNullOrEmpty(file.Sha256) || !Regex.IsMatch(file.Sha256, "^[0-9A-Fa-f]{64}$"))
                {
                    throw new InvalidDataException("The install marker contains an invalid file hash: " + relative);
                }
                if (String.Equals(file.Scope, "mod", StringComparison.Ordinal))
                {
                    if (!IsOwnedModPath(relative) ||
                        String.Equals(relative, MarkerRelativePath, StringComparison.OrdinalIgnoreCase))
                    {
                        throw new InvalidDataException("The install marker points outside the owned mod paths: " + relative);
                    }
                    modFiles++;
                }
                else if (!String.Equals(file.Scope, "loader", StringComparison.Ordinal))
                {
                    throw new InvalidDataException("The install marker contains an invalid scope: " + relative);
                }
            }
            if (modFiles == 0)
            {
                throw new InvalidDataException("The install marker contains no owned mod files.");
            }
        }

        private static List<string> GetOwnedFiles(InstallMarker marker)
        {
            var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (PayloadFile file in marker.Files)
            {
                if (String.Equals(file.Scope, "mod", StringComparison.Ordinal))
                {
                    string relative = PathSecurity.NormalizeRelative(file.Path);
                    if (!IsOwnedModPath(relative))
                    {
                        throw new InvalidDataException("Refusing an unowned path: " + relative);
                    }
                    result.Add(relative);
                }
            }
            result.Add(ConfigRelativePath);
            return result.OrderByDescending(value => value.Length).ToList();
        }

        private static bool IsOwnedModPath(string relative)
        {
            return relative.StartsWith(PluginDirectory + "\\", StringComparison.OrdinalIgnoreCase) ||
                String.Equals(relative, ConfigRelativePath, StringComparison.OrdinalIgnoreCase);
        }

        private static void DeleteOwnedFile(string gameRoot, string relative)
        {
            relative = PathSecurity.NormalizeRelative(relative);
            if (!IsOwnedModPath(relative) ||
                String.Equals(relative, MarkerRelativePath, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("Refusing to delete an unowned file: " + relative);
            }
            string full = PathSecurity.CombineInside(gameRoot, relative);
            PathSecurity.RejectReparsePoints(gameRoot, full);
            if (File.Exists(full))
            {
                File.Delete(full);
                PathSecurity.PruneEmptyParents(gameRoot, full);
            }
        }

        private static void DeleteMarker(string gameRoot)
        {
            string marker = PathSecurity.CombineInside(gameRoot, MarkerRelativePath);
            PathSecurity.RejectReparsePoints(gameRoot, marker);
            if (File.Exists(marker))
            {
                File.Delete(marker);
                PathSecurity.PruneEmptyParents(gameRoot, marker);
            }
        }
    }

    internal sealed class RemovalForm : Form
    {
        private readonly TextBox gamePath = new TextBox();
        private readonly RichTextBox output = new RichTextBox();
        private readonly Button browseButton = new Button();
        private readonly Button verifyButton = new Button();
        private readonly Button removeButton = new Button();
        private readonly Button closeButton = new Button();
        private bool busy;

        internal RemovalForm()
        {
            Text = "Business Tour Five Realms - Gỡ cài đặt";
            Width = 760;
            Height = 520;
            MinimumSize = new System.Drawing.Size(680, 430);
            StartPosition = FormStartPosition.CenterScreen;
            Font = new System.Drawing.Font("Segoe UI", 9F);

            var title = new Label
            {
                AutoSize = true,
                Font = new System.Drawing.Font("Segoe UI Semibold", 15F),
                Text = "Gỡ Business Tour Five Realms",
                Left = 18,
                Top = 16
            };
            var note = new Label
            {
                AutoSize = true,
                Text = "Chỉ file của Five Realms bị xóa. BepInEx và các mod khác sẽ được giữ nguyên.",
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
            removeButton.Text = "Gỡ mod";
            removeButton.Left = 495;
            removeButton.Top = 435;
            removeButton.Width = 110;
            removeButton.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            removeButton.Click += delegate { RunOperation(true); };
            closeButton.Text = "Đóng";
            closeButton.Left = 615;
            closeButton.Top = 435;
            closeButton.Width = 110;
            closeButton.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            closeButton.Click += delegate { Close(); };

            Controls.AddRange(new Control[]
            {
                title, note, pathLabel, gamePath, browseButton, output, verifyButton, removeButton, closeButton
            });
            AcceptButton = removeButton;
            CancelButton = closeButton;
            FormClosing += OnFormClosing;

            IList<string> candidates = GameLocator.FindCandidates();
            if (candidates.Count > 0)
            {
                gamePath.Text = candidates[0];
                Append("Đã tìm thấy thư mục game: " + candidates[0]);
                if (candidates.Count > 1)
                {
                    Append("Có nhiều bản cài đặt; hãy kiểm tra lại đường dẫn trước khi gỡ.");
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

        private void RunOperation(bool uninstall)
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
            if (uninstall && MessageBox.Show(this,
                "Gỡ Five Realms? BepInEx và mod khác vẫn được giữ lại.", "Xác nhận gỡ mod",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            {
                return;
            }

            SetBusy(true);
            Append(uninstall ? "Bắt đầu gỡ mod..." : "Bắt đầu kiểm tra...");
            ThreadPool.QueueUserWorkItem(delegate
            {
                Exception failure = null;
                try
                {
                    if (uninstall)
                    {
                        RemovalEngine.Uninstall(root, Append);
                    }
                    else
                    {
                        RemovalEngine.Verify(root, Append);
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
                            uninstall ? "Đã gỡ Five Realms. BepInEx vẫn được giữ lại." : "Kiểm tra hoàn tất.",
                            "Five Realms", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        if (uninstall)
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
            removeButton.Enabled = !value;
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

    internal static class UninstallProgram
    {
        private static readonly string Usage =
            "Business Tour Five Realms Uninstaller " + BuildInfo.ModVersion + Environment.NewLine +
            "  Uninstall.exe --verify-only [--game-root <folder>] [--log <file>]" + Environment.NewLine +
            "  Uninstall.exe --uninstall   [--game-root <folder>] [--log <file>]";

        [STAThread]
        private static int Main(string[] args)
        {
            RemovalOptions options = null;
            if (args.Length > 0)
            {
                try
                {
                    options = RemovalOptions.Parse(args);
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
                    Application.Run(new RemovalForm());
                    return 0;
                }

                try
                {
                    string root = ResolveGameRoot(options.GameRoot);
                    using (var log = new RemovalLog(options.LogPath))
                    {
                        if (options.VerifyOnly)
                        {
                            RemovalEngine.Verify(root, log.Write);
                        }
                        else
                        {
                            RemovalEngine.Uninstall(root, log.Write);
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
                    Console.Error.WriteLine("Five Realms removal failed: " + error.Message);
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
