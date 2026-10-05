using Microsoft.Win32;
using System;
using System.ComponentModel;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web.Script.Serialization;

namespace BusinessTourFiveRealmsInstaller
{
    internal static class MaintenanceSingleInstance
    {
        internal const string MutexName = @"Local\BusinessTourFiveRealms.Maintenance";
        private static readonly string[] MutexNames =
        {
            MutexName,
            @"Local\BusinessTourFiveRealms.Setup",
            @"Local\BusinessTourFiveRealms.Uninstall"
        };
        private const int RestoreWindow = 9;

        private sealed class Lease : IDisposable
        {
            private readonly List<Mutex> owned;

            internal Lease(List<Mutex> ownedMutexes)
            {
                owned = ownedMutexes;
            }

            public void Dispose()
            {
                for (int index = owned.Count - 1; index >= 0; index--)
                {
                    try
                    {
                        owned[index].ReleaseMutex();
                    }
                    finally
                    {
                        owned[index].Dispose();
                    }
                }
                owned.Clear();
            }
        }

        [DllImport("user32.dll")]
        private static extern bool ShowWindowAsync(IntPtr window, int command);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr window);

        [DllImport("user32.dll")]
        private static extern bool FlashWindow(IntPtr window, bool invert);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);

        internal static IDisposable TryAcquire()
        {
            var owned = new List<Mutex>();
            try
            {
                foreach (string name in MutexNames)
                {
                    var mutex = new Mutex(false, name);
                    bool acquired;
                    try
                    {
                        acquired = mutex.WaitOne(0, false);
                    }
                    catch (AbandonedMutexException)
                    {
                        acquired = true;
                    }

                    if (!acquired)
                    {
                        mutex.Dispose();
                        Release(owned);
                        return null;
                    }
                    owned.Add(mutex);
                }
                return new Lease(owned);
            }
            catch
            {
                Release(owned);
                throw;
            }
        }

        private static void Release(List<Mutex> mutexes)
        {
            for (int index = mutexes.Count - 1; index >= 0; index--)
            {
                try
                {
                    mutexes[index].ReleaseMutex();
                }
                finally
                {
                    mutexes[index].Dispose();
                }
            }
            mutexes.Clear();
        }

        internal static bool TryActivateExistingWindow()
        {
            using (Process current = Process.GetCurrentProcess())
            {
                int currentProcessId = current.Id;
                int currentSessionId = current.SessionId;
                string currentDirectory = Path.GetDirectoryName(current.MainModule.FileName);
                foreach (string processName in new[]
                {
                    "BusinessTourFiveRealms-Setup",
                    "BusinessTourFiveRealms-Uninstall"
                })
                {
                    foreach (Process process in Process.GetProcessesByName(processName))
                    {
                        using (process)
                        {
                            if (process.Id == currentProcessId)
                            {
                                continue;
                            }

                            try
                            {
                                if (process.SessionId != currentSessionId ||
                                    !String.Equals(Path.GetDirectoryName(process.MainModule.FileName), currentDirectory,
                                        StringComparison.OrdinalIgnoreCase))
                                {
                                    continue;
                                }

                                process.Refresh();
                                IntPtr window = process.MainWindowHandle;
                                if (window == IntPtr.Zero)
                                {
                                    continue;
                                }

                                uint ownerProcessId;
                                GetWindowThreadProcessId(window, out ownerProcessId);
                                if (ownerProcessId != (uint)process.Id)
                                {
                                    continue;
                                }

                                ShowWindowAsync(window, RestoreWindow);
                                if (!SetForegroundWindow(window))
                                {
                                    FlashWindow(window, true);
                                }
                                return true;
                            }
                            catch (InvalidOperationException)
                            {
                                // The other process exited between enumeration and activation.
                            }
                            catch (Win32Exception)
                            {
                                // Ignore inaccessible or already-exiting candidate processes.
                            }
                        }
                    }
                }
                return false;
            }
        }
    }

    internal sealed class PayloadManifest
    {
        public string ModId { get; set; }
        public string ModVersion { get; set; }
        public string BepInExBuild { get; set; }
        public List<PayloadFile> Files { get; set; }
    }

    internal sealed class PayloadFile
    {
        public string Path { get; set; }
        public string Sha256 { get; set; }
        public long Length { get; set; }
        public string Scope { get; set; }
    }

    internal sealed class InstallMarker
    {
        public string ModId { get; set; }
        public string ModVersion { get; set; }
        public string InstalledUtc { get; set; }
        public string PayloadSha256 { get; set; }
        public bool LoaderInstalledByThisRun { get; set; }
        public List<PayloadFile> Files { get; set; }
    }

    internal sealed class BackupManifest
    {
        public string ModId { get; set; }
        public string CreatedUtc { get; set; }
        public string GameRoot { get; set; }
        public List<BackupEntry> Entries { get; set; }
    }

    internal sealed class BackupEntry
    {
        public string RelativePath { get; set; }
        public bool Existed { get; set; }
        public string Sha256 { get; set; }
    }

    internal static class Json
    {
        private const long MaximumJsonBytes = 16L * 1024L * 1024L;

        private static JavaScriptSerializer CreateSerializer()
        {
            return new JavaScriptSerializer
            {
                MaxJsonLength = (int)MaximumJsonBytes,
                RecursionLimit = 100
            };
        }

        internal static T Read<T>(string path)
        {
            var info = new FileInfo(path);
            if (!info.Exists || info.Length > MaximumJsonBytes)
            {
                throw new InvalidDataException("JSON file is missing or too large: " + path);
            }
            return CreateSerializer().Deserialize<T>(File.ReadAllText(path, Encoding.UTF8));
        }

        internal static void Write<T>(string path, T value)
        {
            string parent = Path.GetDirectoryName(path);
            if (!String.IsNullOrEmpty(parent))
            {
                Directory.CreateDirectory(parent);
            }
            string temporary = path + ".bt5tmp." + Guid.NewGuid().ToString("N");
            try
            {
                File.WriteAllText(temporary, CreateSerializer().Serialize(value), new UTF8Encoding(false));
                if (new FileInfo(temporary).Length > MaximumJsonBytes)
                {
                    throw new InvalidDataException("Generated JSON exceeds the safety limit: " + path);
                }
                if (File.Exists(path))
                {
                    File.Replace(temporary, path, null);
                }
                else
                {
                    File.Move(temporary, path);
                }
            }
            finally
            {
                if (File.Exists(temporary))
                {
                    File.Delete(temporary);
                }
            }
        }
    }

    internal static class Hashing
    {
        internal static string Sha256(string path)
        {
            using (FileStream input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (SHA256 hash = SHA256.Create())
            {
                return BitConverter.ToString(hash.ComputeHash(input)).Replace("-", String.Empty);
            }
        }

        internal static string Sha256(Stream input)
        {
            using (SHA256 hash = SHA256.Create())
            {
                return BitConverter.ToString(hash.ComputeHash(input)).Replace("-", String.Empty);
            }
        }

        internal static void Require(string path, string expected)
        {
            if (!File.Exists(path))
            {
                throw new InvalidDataException("Required file is missing: " + path);
            }
            string actual = Sha256(path);
            if (!String.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("Hash mismatch: " + path + Environment.NewLine +
                    "Expected " + expected + Environment.NewLine + "Actual   " + actual);
            }
        }
    }

    internal static class PathSecurity
    {
        private static readonly Regex DosDeviceName = new Regex(
            "^(CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])(?:\\.|$)",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        internal static string NormalizeRelative(string relative)
        {
            if (String.IsNullOrWhiteSpace(relative))
            {
                throw new InvalidDataException("An empty relative path was rejected.");
            }
            string normalized = relative.Replace('/', '\\');
            if (Path.IsPathRooted(normalized) || normalized.IndexOf(':') >= 0)
            {
                throw new InvalidDataException("A rooted or stream path was rejected: " + relative);
            }
            string[] parts = normalized.Split(new[] { '\\' }, StringSplitOptions.None);
            if (parts.Length == 0 || parts.Any(String.IsNullOrEmpty))
            {
                throw new InvalidDataException("An invalid relative path was rejected: " + relative);
            }
            foreach (string part in parts)
            {
                if (part == "." || part == ".." || part.EndsWith(".", StringComparison.Ordinal) ||
                    part.EndsWith(" ", StringComparison.Ordinal) ||
                    part.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
                    part.Any(character => Char.IsControl(character)) || DosDeviceName.IsMatch(part))
                {
                    throw new InvalidDataException("An unsafe path segment was rejected: " + relative);
                }
            }
            return String.Join("\\", parts);
        }

        internal static string CombineInside(string root, string relative)
        {
            string fullRoot = NormalizeRoot(root);
            string full = Path.GetFullPath(Path.Combine(fullRoot, NormalizeRelative(relative)));
            string prefix = fullRoot + Path.DirectorySeparatorChar;
            if (!full.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("Path escaped the managed root: " + relative);
            }
            return full;
        }

        internal static void RejectReparsePoints(string root, string fullPath)
        {
            string fullRoot = NormalizeRoot(root);
            string checkedPath = Path.GetFullPath(fullPath);
            string prefix = fullRoot + Path.DirectorySeparatorChar;
            if (!String.Equals(checkedPath, fullRoot, StringComparison.OrdinalIgnoreCase) &&
                !checkedPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("Path escaped the managed root: " + fullPath);
            }
            string current = fullRoot;
            if (Directory.Exists(current) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
            {
                throw new IOException("Reparse-point game roots are not supported: " + current);
            }
            string relative = checkedPath.Substring(fullRoot.Length).TrimStart(Path.DirectorySeparatorChar);
            foreach (string part in relative.Split(new[] { Path.DirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries))
            {
                current = Path.Combine(current, part);
                if ((Directory.Exists(current) || File.Exists(current)) &&
                    (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                {
                    throw new IOException("A reparse point was rejected: " + current);
                }
            }
        }

        internal static void PruneEmptyParents(string gameRoot, string path)
        {
            string root = NormalizeRoot(gameRoot);
            string current = Directory.Exists(path) ? path : Path.GetDirectoryName(path);
            while (!String.IsNullOrEmpty(current) && !String.Equals(current, root, StringComparison.OrdinalIgnoreCase))
            {
                RejectReparsePoints(root, current);
                if (!Directory.Exists(current) || Directory.EnumerateFileSystemEntries(current).Any())
                {
                    break;
                }
                Directory.Delete(current, false);
                current = Path.GetDirectoryName(current);
            }
        }

        private static string NormalizeRoot(string root)
        {
            string full = Path.GetFullPath(root);
            string volumeRoot = Path.GetPathRoot(full);
            while (full.Length > volumeRoot.Length &&
                (full.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal) ||
                 full.EndsWith(Path.AltDirectorySeparatorChar.ToString(), StringComparison.Ordinal)))
            {
                full = full.Substring(0, full.Length - 1);
            }
            return full;
        }
    }

    internal static class GameLocator
    {
        internal static IList<string> FindCandidates()
        {
            var results = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string steamRoot in FindSteamRoots())
            {
                IEnumerable<string> libraries;
                try
                {
                    libraries = FindLibraries(steamRoot).ToArray();
                }
                catch
                {
                    continue;
                }
                foreach (string library in libraries)
                {
                    try
                    {
                        string manifest = Path.Combine(library, "steamapps", "appmanifest_397900.acf");
                        if (!File.Exists(manifest))
                        {
                            continue;
                        }
                        string text = File.ReadAllText(manifest);
                        Match installDir = Regex.Match(text, "\\\"installdir\\\"\\s+\\\"([^\\\"]+)\\\"", RegexOptions.IgnoreCase);
                        if (!installDir.Success)
                        {
                            continue;
                        }
                        string commonRoot = Path.Combine(library, "steamapps", "common");
                        string candidate = PathSecurity.CombineInside(commonRoot, installDir.Groups[1].Value);
                        if (File.Exists(Path.Combine(candidate, "BusinessTour.exe")))
                        {
                            results.Add(Path.GetFullPath(candidate));
                        }
                    }
                    catch { }
                }
            }
            return results.OrderBy(value => value, StringComparer.OrdinalIgnoreCase).ToList();
        }

        private static IEnumerable<string> FindSteamRoots()
        {
            var roots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            string[] keys =
            {
                @"HKEY_CURRENT_USER\Software\Valve\Steam",
                @"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Valve\Steam",
                @"HKEY_LOCAL_MACHINE\SOFTWARE\Valve\Steam"
            };
            foreach (string key in keys)
            {
                foreach (string valueName in new[] { "SteamPath", "InstallPath" })
                {
                    object value = Registry.GetValue(key, valueName, null);
                    if (value != null && Directory.Exists(Convert.ToString(value)))
                    {
                        roots.Add(Path.GetFullPath(Convert.ToString(value)));
                    }
                }
            }
            return roots;
        }

        private static IEnumerable<string> FindLibraries(string steamRoot)
        {
            var libraries = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { steamRoot };
            string vdf = Path.Combine(steamRoot, "steamapps", "libraryfolders.vdf");
            if (File.Exists(vdf))
            {
                string text = File.ReadAllText(vdf);
                foreach (Match match in Regex.Matches(text, "\\\"path\\\"\\s+\\\"([^\\\"]+)\\\"", RegexOptions.IgnoreCase))
                {
                    string value = match.Groups[1].Value.Replace("\\\\", "\\");
                    if (Directory.Exists(value))
                    {
                        libraries.Add(Path.GetFullPath(value));
                    }
                }
            }
            return libraries;
        }
    }

    internal static class GameValidator
    {
        internal static void ValidateShape(string gameRoot, Action<string> log)
        {
            if (String.IsNullOrWhiteSpace(gameRoot))
            {
                throw new ArgumentException("Select the Business Tour game folder.");
            }
            string root = Path.GetFullPath(gameRoot);
            string exe = Path.Combine(root, "BusinessTour.exe");
            if (!Directory.Exists(root) || !File.Exists(exe) ||
                !Directory.Exists(Path.Combine(root, "BusinessTour_Data")))
            {
                throw new InvalidDataException("This is not a Business Tour game root: " + root);
            }
            PathSecurity.RejectReparsePoints(root, exe);
            log("Game folder shape verified: " + root);
        }

        internal static void ValidateExact(string gameRoot, Action<string> log)
        {
            ValidateShape(gameRoot, log);
            string root = Path.GetFullPath(gameRoot);
            var criticalFiles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { Path.Combine(root, "BusinessTour.exe"), BuildInfo.BusinessTourExeSha256 },
                { Path.Combine(root, "GameAssembly.dll"), BuildInfo.GameAssemblySha256 },
                { Path.Combine(root, "BusinessTour_Data", "il2cpp_data", "Metadata", "global-metadata.dat"), BuildInfo.GlobalMetadataSha256 },
                { Path.Combine(root, "UnityPlayer.dll"), BuildInfo.UnityPlayerSha256 },
                { Path.Combine(root, "BusinessTour_Data", "globalgamemanagers"), BuildInfo.GlobalGameManagersSha256 }
            };
            foreach (KeyValuePair<string, string> criticalFile in criticalFiles)
            {
                PathSecurity.RejectReparsePoints(root, criticalFile.Key);
                Hashing.Require(criticalFile.Key, criticalFile.Value);
            }

            string unityVersion = FileVersionInfo.GetVersionInfo(Path.Combine(root, "UnityPlayer.dll")).ProductVersion;
            if (!String.Equals(unityVersion, BuildInfo.UnityVersion, StringComparison.Ordinal))
            {
                throw new InvalidDataException("Unsupported Unity version: " + unityVersion);
            }

            string appInfo = Path.Combine(root, "BusinessTour_Data", "app.info");
            PathSecurity.RejectReparsePoints(root, appInfo);
            string appInfoText = File.ReadAllText(appInfo).Replace("\r", String.Empty).TrimEnd('\n');
            if (!String.Equals(appInfoText, "8floor\nBusinessTour", StringComparison.Ordinal))
            {
                throw new InvalidDataException("BusinessTour_Data\\app.info does not match the supported game.");
            }

            string manifest = FindAppManifest(root);
            if (manifest != null)
            {
                string manifestText = File.ReadAllText(manifest);
                Match appId = Regex.Match(manifestText, "\\\"appid\\\"\\s+\\\"([^\\\"]+)\\\"");
                Match build = Regex.Match(manifestText, "\\\"buildid\\\"\\s+\\\"([^\\\"]+)\\\"");
                if (!appId.Success || !String.Equals(appId.Groups[1].Value, "397900", StringComparison.Ordinal) || !build.Success)
                {
                    throw new InvalidDataException("The Steam app manifest is incomplete or belongs to another game.");
                }
                if (!String.Equals(build.Groups[1].Value, BuildInfo.SteamBuildId, StringComparison.Ordinal))
                {
                    throw new InvalidDataException("Unsupported Steam build: " + build.Groups[1].Value);
                }
                log("Compatibility verified: Steam build " + BuildInfo.SteamBuildId + ", Unity " + BuildInfo.UnityVersion + ".");
            }
            else
            {
                log("Compatibility verified by exact game hashes and Unity " + BuildInfo.UnityVersion + "; no Steam manifest was found beside this copied game folder.");
            }
        }

        private static string FindAppManifest(string gameRoot)
        {
            DirectoryInfo game = new DirectoryInfo(Path.GetFullPath(gameRoot));
            DirectoryInfo common = game.Parent;
            DirectoryInfo steamApps = common == null ? null : common.Parent;
            if (common != null && steamApps != null &&
                String.Equals(common.Name, "common", StringComparison.OrdinalIgnoreCase) &&
                String.Equals(steamApps.Name, "steamapps", StringComparison.OrdinalIgnoreCase))
            {
                string candidate = Path.Combine(steamApps.FullName, "appmanifest_397900.acf");
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
            return null;
        }

        internal static IDisposable AcquireGameLock(string gameRoot)
        {
            if (IsBusinessTourRunning())
            {
                throw new IOException("Business Tour is running. Close it before installing or uninstalling.");
            }
            FileStream handle = new FileStream(Path.Combine(gameRoot, "BusinessTour.exe"), FileMode.Open, FileAccess.Read, FileShare.None);
            if (IsBusinessTourRunning())
            {
                handle.Dispose();
                throw new IOException("Business Tour started while the operation was beginning. Close it and try again.");
            }
            return handle;
        }

        internal static bool IsBusinessTourRunning()
        {
            Process[] processes = Process.GetProcessesByName("BusinessTour");
            try
            {
                return processes.Length > 0;
            }
            finally
            {
                foreach (Process process in processes)
                {
                    process.Dispose();
                }
            }
        }
    }

    internal sealed class ReleasePayload : IDisposable
    {
        private const long MaximumPayloadBytes = 512L * 1024L * 1024L;
        private const int MaximumEntries = 4096;
        private string tempRoot;

        internal string Root { get; private set; }
        internal PayloadManifest Manifest { get; private set; }

        internal static ReleasePayload ExtractAndValidate(Action<string> log)
        {
            string tempRoot = Path.Combine(Path.GetTempPath(), "BusinessTourFiveRealms", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempRoot);
            string zipPath = Path.Combine(tempRoot, "payload.zip");
            try
            {
                using (Stream resource = typeof(ReleasePayload).Assembly.GetManifestResourceStream(BuildInfo.PayloadResourceName))
                {
                    if (resource == null)
                    {
                        throw new InvalidDataException("Embedded payload resource is missing.");
                    }
                    using (FileStream output = new FileStream(zipPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    {
                        resource.CopyTo(output);
                    }
                }
                Hashing.Require(zipPath, BuildInfo.PayloadSha256);

                string extractRoot = Path.Combine(tempRoot, "extracted");
                Directory.CreateDirectory(extractRoot);
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                long totalLength = 0;
                using (ZipArchive archive = ZipFile.OpenRead(zipPath))
                {
                    if (archive.Entries.Count > MaximumEntries)
                    {
                        throw new InvalidDataException("Embedded payload contains too many entries.");
                    }
                    foreach (ZipArchiveEntry entry in archive.Entries)
                    {
                        if (String.IsNullOrEmpty(entry.Name))
                        {
                            continue;
                        }
                        string relative = PathSecurity.NormalizeRelative(entry.FullName);
                        if (relative.Length > 240 || entry.Length < 0 || entry.Length > MaximumPayloadBytes - totalLength)
                        {
                            throw new InvalidDataException("Embedded payload entry exceeds a safety limit: " + relative);
                        }
                        if (!seen.Add(relative))
                        {
                            throw new InvalidDataException("Duplicate payload path: " + relative);
                        }
                        string destination = PathSecurity.CombineInside(extractRoot, relative);
                        Directory.CreateDirectory(Path.GetDirectoryName(destination));
                        long extractedLength = 0;
                        using (Stream input = entry.Open())
                        using (FileStream output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                        {
                            byte[] buffer = new byte[81920];
                            int read;
                            while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
                            {
                                if (read > MaximumPayloadBytes - totalLength)
                                {
                                    throw new InvalidDataException("Embedded payload exceeds the safety limit.");
                                }
                                output.Write(buffer, 0, read);
                                totalLength += read;
                                extractedLength += read;
                            }
                        }
                        if (extractedLength != entry.Length)
                        {
                            throw new InvalidDataException("ZIP length mismatch: " + relative);
                        }
                    }
                }

                string manifestPath = Path.Combine(extractRoot, "payload-manifest.json");
                PayloadManifest manifest = Json.Read<PayloadManifest>(manifestPath);
                if (manifest == null || manifest.Files == null ||
                    !String.Equals(manifest.ModId, BuildInfo.ModId, StringComparison.Ordinal) ||
                    !String.Equals(manifest.ModVersion, BuildInfo.ModVersion, StringComparison.Ordinal) ||
                    !String.Equals(manifest.BepInExBuild, BuildInfo.BepInExBuild, StringComparison.Ordinal) ||
                    manifest.Files.Count > MaximumEntries)
                {
                    throw new InvalidDataException("Payload manifest identity is invalid.");
                }

                var listed = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "payload-manifest.json" };
                foreach (PayloadFile file in manifest.Files)
                {
                    if (file == null || file.Length < 0 ||
                        String.IsNullOrWhiteSpace(file.Sha256) ||
                        !Regex.IsMatch(file.Sha256, "^[0-9A-Fa-f]{64}$"))
                    {
                        throw new InvalidDataException("Payload manifest contains a malformed file entry.");
                    }
                    string relative = PathSecurity.NormalizeRelative(file.Path);
                    if (!listed.Add(relative) || (file.Scope != "loader" && file.Scope != "mod"))
                    {
                        throw new InvalidDataException("Invalid payload manifest entry: " + file.Path);
                    }
                    string full = PathSecurity.CombineInside(extractRoot, relative);
                    if (new FileInfo(full).Length != file.Length)
                    {
                        throw new InvalidDataException("Payload length mismatch: " + relative);
                    }
                    Hashing.Require(full, file.Sha256);
                }

                foreach (string file in Directory.GetFiles(extractRoot, "*", SearchOption.AllDirectories))
                {
                    string relative = file.Substring(extractRoot.Length).TrimStart(Path.DirectorySeparatorChar);
                    if (!listed.Contains(relative))
                    {
                        throw new InvalidDataException("Unlisted payload file: " + relative);
                    }
                }

                log("Embedded payload verified: " + manifest.Files.Count + " files.");
                return new ReleasePayload { Root = extractRoot, Manifest = manifest, tempRoot = tempRoot };
            }
            catch
            {
                TryDeleteDirectory(tempRoot);
                throw;
            }
        }

        public void Dispose()
        {
            string disposableRoot = tempRoot;
            tempRoot = null;
            Root = null;
            if (!String.IsNullOrEmpty(disposableRoot))
            {
                TryDeleteDirectory(disposableRoot);
            }
        }

        internal static void TryDeleteDirectory(string path)
        {
            try
            {
                if (Directory.Exists(path))
                {
                    Directory.Delete(path, true);
                }
            }
            catch { }
        }
    }

    internal sealed class BackupSession
    {
        private readonly string gameRoot;
        private readonly string backupRoot;
        private readonly BackupManifest manifest;

        private BackupSession(string gameRoot, string backupRoot, BackupManifest manifest)
        {
            this.gameRoot = gameRoot;
            this.backupRoot = backupRoot;
            this.manifest = manifest;
        }

        internal string BackupRoot { get { return backupRoot; } }

        internal static BackupSession Create(string gameRoot, IEnumerable<string> relativePaths, Action<string> log)
        {
            string baseRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "BusinessTourFiveRealms", "Backups");
            string id = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N").Substring(0, 8);
            string backupRoot = Path.Combine(baseRoot, id);
            try
            {
                Directory.CreateDirectory(Path.Combine(backupRoot, "files"));

                var manifest = new BackupManifest
                {
                    ModId = BuildInfo.ModId,
                    CreatedUtc = DateTime.UtcNow.ToString("o"),
                    GameRoot = Path.GetFullPath(gameRoot),
                    Entries = new List<BackupEntry>()
                };

                IEnumerable<string> normalizedPaths = relativePaths
                    .Select(PathSecurity.NormalizeRelative)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(value => value, StringComparer.OrdinalIgnoreCase);
                foreach (string relative in normalizedPaths)
                {
                    string source = PathSecurity.CombineInside(gameRoot, relative);
                    PathSecurity.RejectReparsePoints(gameRoot, source);
                    if (Directory.Exists(source))
                    {
                        throw new IOException("A directory occupies a managed file path: " + source);
                    }
                    var entry = new BackupEntry { RelativePath = relative, Existed = File.Exists(source), Sha256 = null };
                    if (entry.Existed)
                    {
                        entry.Sha256 = Hashing.Sha256(source);
                        string destination = PathSecurity.CombineInside(Path.Combine(backupRoot, "files"), relative);
                        Directory.CreateDirectory(Path.GetDirectoryName(destination));
                        File.Copy(source, destination, false);
                        Hashing.Require(destination, entry.Sha256);
                    }
                    manifest.Entries.Add(entry);
                }
                Json.Write(Path.Combine(backupRoot, "backup-manifest.json"), manifest);
                log("Safety backup created: " + backupRoot);
                return new BackupSession(gameRoot, backupRoot, manifest);
            }
            catch
            {
                ReleasePayload.TryDeleteDirectory(backupRoot);
                throw;
            }
        }

        internal void Restore(Action<string> log)
        {
            foreach (BackupEntry entry in manifest.Entries.AsEnumerable().Reverse())
            {
                string destination = PathSecurity.CombineInside(gameRoot, entry.RelativePath);
                PathSecurity.RejectReparsePoints(gameRoot, destination);
                if (entry.Existed)
                {
                    string filesRoot = Path.Combine(backupRoot, "files");
                    string source = PathSecurity.CombineInside(filesRoot, entry.RelativePath);
                    PathSecurity.RejectReparsePoints(filesRoot, source);
                    Hashing.Require(source, entry.Sha256);
                    FileInstall.CopyVerified(source, destination, entry.Sha256);
                }
                else if (File.Exists(destination))
                {
                    File.Delete(destination);
                    PathSecurity.PruneEmptyParents(gameRoot, destination);
                }
            }
            log("Rollback restored the pre-operation files.");
        }
    }

    internal static class FileInstall
    {
        internal static void CopyVerified(string source, string destination, string expectedHash)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(destination));
            string temporary = destination + ".bt5tmp." + Guid.NewGuid().ToString("N");
            try
            {
                File.Copy(source, temporary, false);
                Hashing.Require(temporary, expectedHash);
                if (File.Exists(destination))
                {
                    File.Replace(temporary, destination, null);
                }
                else
                {
                    File.Move(temporary, destination);
                }
                Hashing.Require(destination, expectedHash);
            }
            finally
            {
                if (File.Exists(temporary))
                {
                    File.Delete(temporary);
                }
            }
        }

        internal static bool IsGameRunning()
        {
            return GameValidator.IsBusinessTourRunning();
        }
    }
}
