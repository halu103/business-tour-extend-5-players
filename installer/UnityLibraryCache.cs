using System;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Text.RegularExpressions;

namespace BusinessTourFiveRealmsInstaller
{
    internal static class UnityLibraryCache
    {
        private const long MaximumArchiveBytes = 16L * 1024L * 1024L;
        internal static string RelativePath
        {
            get { return @"BepInEx\unity-libs\" + BuildInfo.UnityLibrariesVersion + ".zip"; }
        }

        internal static bool Inspect(string gameRoot, Action<string> log)
        {
            string path = PathSecurity.CombineInside(gameRoot, RelativePath);
            PathSecurity.RejectReparsePoints(gameRoot, path);
            if (Directory.Exists(path))
                throw new IOException("A directory occupies the Unity library ZIP path: " + path);
            if (!File.Exists(path))
            {
                log("Unity library cache is missing. Install will download and verify it.");
                return false;
            }
            try
            {
                ValidateArchive(path);
                log("Unity library ZIP verified. No download is needed.");
                return true;
            }
            catch (InvalidDataException)
            {
                log("Unity library cache is damaged or does not match the supported version. Install will back it up and replace it.");
                return false;
            }
        }

        internal static void ValidateArchive(string path)
        {
            var info = new FileInfo(path);
            if (!info.Exists || info.Length <= 0 || info.Length > MaximumArchiveBytes)
                throw new InvalidDataException("The Unity library ZIP is missing or has an invalid size.");
            Hashing.Require(path, BuildInfo.UnityLibrariesSha256);
            using (ZipArchive archive = ZipFile.OpenRead(path))
            {
                ZipArchiveEntry core = archive.GetEntry("UnityEngine.CoreModule.dll");
                if (core == null || core.Length <= 0)
                    throw new InvalidDataException("The Unity library ZIP does not contain UnityEngine.CoreModule.dll.");
            }
        }

        internal static string Stage(string gameRoot, string stagingRoot, Action<string> log)
        {
            return Stage(gameRoot, stagingRoot, log, Download);
        }

        // The injected downloader lets regression checks reproduce interrupted
        // downloads and HTTP-200 block pages without changing the public URL.
        internal static string Stage(string gameRoot, string stagingRoot, Action<string> log,
            Action<string, string> download)
        {
            WarnAboutDisabledGeneration(gameRoot, log);
            if (Inspect(gameRoot, log)) return null;
            Exception lastError = null;
            for (int attempt = 1; attempt <= 3; attempt++)
            {
                string staged = PathSecurity.CombineInside(stagingRoot, "unity-libraries-" + Guid.NewGuid().ToString("N") + ".zip");
                PathSecurity.RejectReparsePoints(stagingRoot, staged);
                Directory.CreateDirectory(Path.GetDirectoryName(staged));
                bool validated = false;
                try
                {
                    log("Downloading Unity libraries from the official BepInEx server (attempt " + attempt + " of 3)...");
                    download(BuildInfo.UnityLibrariesUrl, staged);
                    ValidateArchive(staged);
                    validated = true;
                    log("Downloaded Unity library ZIP passed SHA-256 and archive checks.");
                    return staged;
                }
                catch (Exception error)
                {
                    if (!(error is IOException) && !(error is WebException) && !(error is InvalidDataException)) throw;
                    lastError = error;
                    log("Unity library download could not be verified (attempt " + attempt + " of 3).");
                }
                finally
                {
                    if (!validated && File.Exists(staged)) File.Delete(staged);
                }
            }
            throw new IOException("Unable to download a verified Unity library ZIP. No game files were changed. " +
                "Check access to unity.bepinex.dev, then run Install again. " +
                "You can also download " + BuildInfo.UnityLibrariesUrl + " and place the verified ZIP in " + RelativePath + ".", lastError);
        }

        internal static void Commit(string gameRoot, string staged, Action<string> log)
        {
            if (staged == null) return;
            ValidateArchive(staged);
            string destination = PathSecurity.CombineInside(gameRoot, RelativePath);
            PathSecurity.RejectReparsePoints(gameRoot, destination);
            FileInstall.CopyVerified(staged, destination, BuildInfo.UnityLibrariesSha256);
            log("Verified Unity library ZIP installed. BepInEx will generate its own interop assemblies at startup.");
        }

        internal static void WarnAboutDisabledGeneration(string gameRoot, Action<string> log)
        {
            string config = PathSecurity.CombineInside(gameRoot, @"BepInEx\config\BepInEx.cfg");
            PathSecurity.RejectReparsePoints(gameRoot, config);
            if (!File.Exists(config)) return;
            string section = null;
            foreach (string line in File.ReadLines(config))
            {
                string value = line.Trim();
                if (value.StartsWith("#") || value.StartsWith(";")) continue;
                Match match = Regex.Match(value, @"^\[([^\]]+)\]$");
                if (match.Success) { section = match.Groups[1].Value; continue; }
                if (String.Equals(section, "IL2CPP", StringComparison.OrdinalIgnoreCase) &&
                    Regex.IsMatch(value, @"^UpdateInteropAssemblies\s*=\s*false\s*$", RegexOptions.IgnoreCase))
                    log("WARNING: BepInEx.cfg disables interop regeneration. Set [IL2CPP] UpdateInteropAssemblies = true before starting the game. Your config was kept.");
            }
        }

        private static void Download(string url, string destination)
        {
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            var request = (HttpWebRequest)WebRequest.Create(url);
            request.Timeout = 30000;
            request.ReadWriteTimeout = 30000;
            request.UserAgent = "BusinessTourFiveRealms-Setup/" + BuildInfo.ModVersion;
            using (var response = (HttpWebResponse)request.GetResponse())
            {
                if (response.StatusCode != HttpStatusCode.OK || response.ContentLength > MaximumArchiveBytes ||
                    response.ResponseUri.Scheme != Uri.UriSchemeHttps)
                    throw new InvalidDataException("The Unity library server returned an unexpected response.");
                using (Stream input = response.GetResponseStream())
                using (var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    byte[] buffer = new byte[81920];
                    long total = 0;
                    int read;
                    while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        total += read;
                        if (total > MaximumArchiveBytes)
                            throw new InvalidDataException("The Unity library download exceeds its size limit.");
                        output.Write(buffer, 0, read);
                    }
                }
            }
        }
    }
}
