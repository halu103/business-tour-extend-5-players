using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;

namespace FiveRealmsInstallerTests
{
    // Invokes only the isolated cache helpers, never SetupEngine or a game.
    // Downloads are deterministic delegates; every writable path is beneath
    // the unique fixture directory supplied by Test-UnityLibraryCache.ps1.
    internal static class UnityLibraryCacheSmoke
    {
        private static Assembly installer;
        private static Type cache;
        private static string fixtureRoot;
        private static string officialArchive;
        private static string expectedHash;
        private static string relativePath;
        private static string expectedUrl;
        private static readonly List<string> messages = new List<string>();
        private static readonly Action<string> log = delegate(string message) { messages.Add(message); };
        private static int checks;

        private static int Main(string[] args)
        {
            try
            {
                Require(args.Length == 5, "Expected SetupExe, FixtureRoot, OfficialArchive, Version and SHA-256.");
                fixtureRoot = Path.GetFullPath(args[1]);
                officialArchive = Path.GetFullPath(args[2]);
                expectedHash = args[4];
                Require(Directory.Exists(fixtureRoot), "The isolated fixture root is missing.");
                Require(Hash(officialArchive) == expectedHash.ToUpperInvariant(), "The known-good archive does not match compatibility.json.");
                installer = Assembly.LoadFile(Path.GetFullPath(args[0]));
                cache = installer.GetType("BusinessTourFiveRealmsInstaller.UnityLibraryCache", true);
                Type buildInfo = installer.GetType("BusinessTourFiveRealmsInstaller.BuildInfo", true);
                Require(Constant(buildInfo, "ModVersion") == args[3], "The fixture must run against the newly built Setup version.");
                Require(String.Equals(Constant(buildInfo, "UnityLibrariesSha256"), expectedHash, StringComparison.OrdinalIgnoreCase),
                    "Setup and compatibility.json disagree about the Unity library SHA-256.");
                expectedUrl = Constant(buildInfo, "UnityLibrariesUrl");
                relativePath = (string)cache.GetProperty("RelativePath", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null, null);
                Require(relativePath == @"BepInEx\unity-libs\" + Constant(buildInfo, "UnityLibrariesVersion") + ".zip",
                    "Unexpected Unity library cache location.");

                CheckBadCaches();
                CheckHealthyCache();
                CheckBlockedResponseThenRetry();
                CheckRetryExhaustion();
                CheckInterruptedDownloads();
                CheckCommitAndRollback(true);
                CheckCommitAndRollback(false);
                CheckConfigWarning();
                Console.WriteLine("PASS: " + checks + " isolated Unity library cache scenarios; SHA-256 validation, retry cleanup, target preservation, commit/rollback and config warning passed.");
                return 0;
            }
            catch (Exception error)
            {
                Console.Error.WriteLine("FAIL: " + error);
                return 1;
            }
        }

        private static void CheckBadCaches()
        {
            string game = NewDirectory("bad-caches-game");
            string target = Target(game);
            string unrelated = AddSentinel(game);
            Require(!Inspect(game), "A missing cache was reported as healthy.");
            Write(target, Encoding.UTF8.GetBytes("<html>HTTP 200: download blocked</html>"));
            Expect<InvalidDataException>(delegate { Validate(target); }, "An HTTP-200 HTML page passed ZIP validation.");
            string htmlHash = Hash(target);
            Require(!Inspect(game), "The cached HTML page was reported as healthy.");
            Require(Hash(target) == htmlHash, "Inspect changed a damaged cache.");
            byte[] truncated = new byte[128];
            using (FileStream input = File.OpenRead(officialArchive))
                Require(input.Read(truncated, 0, truncated.Length) == truncated.Length, "Could not prepare a truncated ZIP.");
            Write(target, truncated);
            Expect<InvalidDataException>(delegate { Validate(target); }, "A truncated ZIP passed validation.");
            string truncatedHash = Hash(target);
            Require(!Inspect(game), "The truncated cached ZIP was reported as healthy.");
            Require(Hash(target) == truncatedHash, "Inspect changed a truncated ZIP.");
            CheckSentinel(unrelated);
            checks++;
            Console.WriteLine("PASS: missing, HTML and truncated ZIP caches are detected without modifying them.");
        }

        private static void CheckHealthyCache()
        {
            string game = NewDirectory("healthy-game");
            string stage = NewDirectory("healthy-stage");
            string unrelated = AddSentinel(game);
            CopyGood(Target(game));
            Require(Inspect(game), "The official archive was rejected as a healthy cache.");
            int calls = 0;
            string result = Stage(game, stage, delegate(string url, string output) { calls++; throw new InvalidOperationException("Healthy cache must not download."); });
            Require(result == null && calls == 0, "A healthy cache triggered a download or replacement.");
            Require(Directory.GetFileSystemEntries(stage).Length == 0, "Healthy cache staging created files.");
            Require(Hash(Target(game)) == expectedHash.ToUpperInvariant(), "The healthy cache changed.");
            CheckSentinel(unrelated);
            checks++;
            Console.WriteLine("PASS: a verified existing cache is reused with zero downloader calls.");
        }

        private static void CheckBlockedResponseThenRetry()
        {
            string game = NewDirectory("retry-game");
            string stage = NewDirectory("retry-stage");
            string unrelated = AddSentinel(game);
            Write(Target(game), Encoding.UTF8.GetBytes("old damaged cache"));
            string originalHash = Hash(Target(game));
            int calls = 0;
            string rejectedPath = null;
            string result = Stage(game, stage, delegate(string url, string output)
            {
                calls++;
                CheckDownloadArguments(url, output, stage);
                if (calls == 1)
                {
                    rejectedPath = output;
                    Write(output, Encoding.UTF8.GetBytes("<html>HTTP 200 gateway block</html>"));
                }
                else
                {
                    Require(!File.Exists(rejectedPath), "The rejected HTML response was not cleaned before retry.");
                    CopyGood(output);
                }
            });
            Require(calls == 2, "A blocked response did not retry exactly once before succeeding.");
            Require(result != null && Hash(result) == expectedHash.ToUpperInvariant(), "The retry did not return the verified ZIP.");
            Require(Directory.GetFiles(stage).Length == 1 && !File.Exists(rejectedPath), "Retry left unverified staging files.");
            Require(Hash(Target(game)) == originalHash, "Stage modified the game cache before the transaction.");
            CheckSentinel(unrelated);
            checks++;
            Console.WriteLine("PASS: an HTTP-200 block page is deleted and the good retry is staged before game changes.");
        }

        private static void CheckRetryExhaustion()
        {
            string game = NewDirectory("exhausted-game");
            string stage = NewDirectory("exhausted-stage");
            string unrelated = AddSentinel(game);
            Write(Target(game), Encoding.UTF8.GetBytes("original cache retained after retry exhaustion"));
            string originalHash = Hash(Target(game));
            int calls = 0;
            Expect<IOException>(delegate
            {
                Stage(game, stage, delegate(string url, string output)
                {
                    calls++;
                    CheckDownloadArguments(url, output, stage);
                    Require(Directory.GetFiles(stage).Length == 0, "A prior rejected download survived into the next attempt.");
                    Write(output, Encoding.UTF8.GetBytes("<html>still blocked</html>"));
                });
            }, "Three bad downloads did not block installation.");
            Require(calls == 3, "Download exhaustion did not stop after three attempts.");
            Require(Directory.GetFiles(stage).Length == 0, "Exhaustion left damaged staging files.");
            Require(Hash(Target(game)) == originalHash, "Exhaustion changed the original target.");
            CheckSentinel(unrelated);
            checks++;
            Console.WriteLine("PASS: three invalid downloads fail cleanly and leave the original target untouched.");
        }

        private static void CheckInterruptedDownloads()
        {
            string game = NewDirectory("interrupted-game");
            string stage = NewDirectory("interrupted-stage");
            string unrelated = AddSentinel(game);
            Write(Target(game), Encoding.UTF8.GetBytes("original cache retained after interrupted writes"));
            string originalHash = Hash(Target(game));
            int calls = 0;
            Expect<IOException>(delegate
            {
                Stage(game, stage, delegate(string url, string output)
                {
                    calls++;
                    CheckDownloadArguments(url, output, stage);
                    Require(Directory.GetFiles(stage).Length == 0, "A partial write survived into a retry.");
                    Write(output, new byte[] { 0x50, 0x4b, 3, 4 });
                    if (calls == 2) throw new IOException("Simulated disconnected file stream.");
                    throw new WebException("Simulated interrupted HTTP response.");
                });
            }, "Interrupted downloads did not fail after retries.");
            Require(calls == 3 && Directory.GetFiles(stage).Length == 0, "Partial downloads were not cleaned after every attempt.");
            Require(Hash(Target(game)) == originalHash, "Interrupted downloads changed the original target.");
            CheckSentinel(unrelated);
            checks++;
            Console.WriteLine("PASS: partial writes followed by network or IO exceptions are cleaned without changing the target.");
        }

        private static void CheckCommitAndRollback(bool existed)
        {
            string game = NewDirectory(existed ? "rollback-existing-game" : "rollback-new-game");
            string stage = NewDirectory(existed ? "rollback-existing-stage" : "rollback-new-stage");
            string unrelated = AddSentinel(game);
            string target = Target(game);
            string originalHash = null;
            if (existed)
            {
                Write(target, Encoding.UTF8.GetBytes("damaged original cache preserved by transaction backup"));
                originalHash = Hash(target);
            }
            string staged = Path.Combine(stage, "verified.zip");
            CopyGood(staged);
            object backup = IsolatedBackup(game, existed, originalHash);
            Invoke(cache, "Commit", new[] { typeof(string), typeof(string), typeof(Action<string>) }, game, staged, log);
            Require(Hash(target) == expectedHash.ToUpperInvariant(), "Commit did not install the verified archive.");
            Require(Directory.GetFiles(Path.GetDirectoryName(target), "*.bt5tmp.*").Length == 0, "Commit left temporary replacement files.");
            Type backupType = backup.GetType();
            InvokeInstance(backupType, backup, "Restore", new[] { typeof(Action<string>) }, log);
            if (existed) Require(File.Exists(target) && Hash(target) == originalHash, "Rollback did not restore the backed-up damaged cache byte-for-byte.");
            else Require(!File.Exists(target), "Rollback did not remove a newly created cache.");
            CheckSentinel(unrelated);

            // Validation must precede destination writes even when a staged
            // file is changed after the download validation succeeded.
            Write(staged, Encoding.UTF8.GetBytes("tampered staged ZIP"));
            Expect<InvalidDataException>(delegate
            {
                Invoke(cache, "Commit", new[] { typeof(string), typeof(string), typeof(Action<string>) }, game, staged, log);
            }, "Commit accepted a tampered staged ZIP.");
            if (existed) Require(Hash(target) == originalHash, "Rejected Commit changed the restored target.");
            else Require(!File.Exists(target), "Rejected Commit created a target.");
            checks++;
            Console.WriteLine("PASS: verified Commit and real BackupSession.Restore preserve " + (existed ? "the original cached bytes" : "the previously absent cache") + "; tampered Commit is rejected.");
        }

        private static object IsolatedBackup(string game, bool existed, string oldHash)
        {
            // Create normally writes to LocalAppData. Construct the same
            // private session/manifest with fixture-local backup storage so
            // this regression exercises the production Restore implementation
            // without leaving backup data outside the isolated workspace.
            string backupRoot = NewDirectory(existed ? "backup-existing" : "backup-new");
            if (existed)
            {
                string saved = Path.Combine(backupRoot, "files", relativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(saved));
                File.Copy(Target(game), saved);
            }
            Type entryType = installer.GetType("BusinessTourFiveRealmsInstaller.BackupEntry", true);
            object entry = Activator.CreateInstance(entryType, true);
            entryType.GetProperty("RelativePath").SetValue(entry, relativePath, null);
            entryType.GetProperty("Existed").SetValue(entry, existed, null);
            entryType.GetProperty("Sha256").SetValue(entry, oldHash, null);
            var entries = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(entryType));
            entries.Add(entry);
            Type manifestType = installer.GetType("BusinessTourFiveRealmsInstaller.BackupManifest", true);
            object manifest = Activator.CreateInstance(manifestType, true);
            manifestType.GetProperty("GameRoot").SetValue(manifest, game, null);
            manifestType.GetProperty("Entries").SetValue(manifest, entries, null);
            Type backupType = installer.GetType("BusinessTourFiveRealmsInstaller.BackupSession", true);
            ConstructorInfo constructor = backupType.GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic, null,
                new[] { typeof(string), typeof(string), manifestType }, null);
            Require(constructor != null, "The transaction backup constructor is missing.");
            return constructor.Invoke(new[] { (object)game, backupRoot, manifest });
        }

        private static void CheckConfigWarning()
        {
            string game = NewDirectory("config-game");
            string config = Path.Combine(game, @"BepInEx\config\BepInEx.cfg");
            Write(config, Encoding.UTF8.GetBytes("# Custom file retained\r\n[Logging]\r\nUpdateInteropAssemblies = false\r\n[IL2CPP]\r\nUpdateInteropAssemblies = false\r\nOtherSetting = custom\r\n"));
            string originalHash = Hash(config);
            messages.Clear();
            Invoke(cache, "WarnAboutDisabledGeneration", new[] { typeof(string), typeof(Action<string>) }, game, log);
            Require(messages.Count == 1 && messages[0].Contains("WARNING") && messages[0].Contains("UpdateInteropAssemblies = true"),
                "Disabled IL2CPP generation did not produce an actionable warning.");
            Require(Hash(config) == originalHash, "The warning changed the user's custom BepInEx config.");
            Write(config, Encoding.UTF8.GetBytes("[Logging]\r\nUpdateInteropAssemblies = false\r\n[IL2CPP]\r\n# UpdateInteropAssemblies = false\r\nUpdateInteropAssemblies = true\r\n"));
            originalHash = Hash(config);
            messages.Clear();
            Invoke(cache, "WarnAboutDisabledGeneration", new[] { typeof(string), typeof(Action<string>) }, game, log);
            Require(messages.Count == 0 && Hash(config) == originalHash, "Enabled generation, unrelated sections or comments caused a false warning.");
            checks++;
            Console.WriteLine("PASS: disabled interop regeneration warns without editing custom settings; comments/unrelated sections do not warn.");
        }

        private static string Constant(Type type, string name)
        {
            return (string)type.GetField(name, BindingFlags.Static | BindingFlags.NonPublic).GetRawConstantValue();
        }

        private static bool Inspect(string game)
        {
            return (bool)Invoke(cache, "Inspect", new[] { typeof(string), typeof(Action<string>) }, game, log);
        }

        private static void Validate(string path)
        {
            Invoke(cache, "ValidateArchive", new[] { typeof(string) }, path);
        }

        private static string Stage(string game, string stage, Action<string, string> download)
        {
            return (string)Invoke(cache, "Stage", new[] { typeof(string), typeof(string), typeof(Action<string>), typeof(Action<string, string>) }, game, stage, log, download);
        }

        private static object Invoke(Type type, string name, Type[] parameterTypes, params object[] args)
        {
            return InvokeMethod(type.GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic, null, parameterTypes, null), null, args);
        }

        private static object InvokeInstance(Type type, object instance, string name, Type[] parameterTypes, params object[] args)
        {
            return InvokeMethod(type.GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic, null, parameterTypes, null), instance, args);
        }

        private static object InvokeMethod(MethodInfo method, object instance, object[] args)
        {
            Require(method != null, "A required internal installer helper is missing.");
            try { return method.Invoke(instance, args); }
            catch (TargetInvocationException error)
            {
                if (error.InnerException != null) throw error.InnerException;
                throw;
            }
        }

        private static string NewDirectory(string name)
        {
            string path = Path.GetFullPath(Path.Combine(fixtureRoot, name));
            RequireInside(path, fixtureRoot);
            Directory.CreateDirectory(path);
            return path;
        }

        private static string Target(string game)
        {
            string path = Path.GetFullPath(Path.Combine(game, relativePath));
            RequireInside(path, game);
            RequireInside(path, fixtureRoot);
            return path;
        }

        private static void CheckDownloadArguments(string url, string output, string stage)
        {
            Require(url == expectedUrl && new Uri(url).Scheme == Uri.UriSchemeHttps, "The downloader was given an unexpected source.");
            RequireInside(output, stage);
            RequireInside(output, fixtureRoot);
        }

        private static void RequireInside(string path, string root)
        {
            string full = Path.GetFullPath(path);
            string prefix = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            Require(full.StartsWith(prefix, StringComparison.OrdinalIgnoreCase), "A writable fixture path escaped its root: " + full);
        }

        private static void Write(string path, byte[] bytes)
        {
            RequireInside(path, fixtureRoot);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, bytes);
        }

        private static void CopyGood(string path)
        {
            RequireInside(path, fixtureRoot);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.Copy(officialArchive, path, true);
        }

        private static string AddSentinel(string game)
        {
            string path = Path.Combine(game, "unrelated-other-mod.txt");
            Write(path, Encoding.UTF8.GetBytes("Keep this unrelated file exactly as it is."));
            return path;
        }

        private static void CheckSentinel(string path)
        {
            Require(File.ReadAllText(path) == "Keep this unrelated file exactly as it is.", "An unrelated game file was modified.");
        }

        private static string Hash(string path)
        {
            using (FileStream input = File.OpenRead(path))
            using (SHA256 sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(input)).Replace("-", String.Empty);
        }

        private static void Expect<T>(Action action, string message) where T : Exception
        {
            try { action(); }
            catch (T) { return; }
            throw new InvalidOperationException(message);
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
