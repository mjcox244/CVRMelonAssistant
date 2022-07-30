using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using CVRMelonAssistant.Pages;

namespace CVRMelonAssistant
{
    public static class InstallHandlers
    {
        public static bool IsMelonLoaderInstalled()
        {
            return File.Exists(Path.Combine(App.ChilloutInstallDirectory, "version.dll")) && File.Exists(Path.Combine(App.ChilloutInstallDirectory, "MelonLoader", "Dependencies", "Bootstrap.dll"));
        }

        public static bool RemoveMelonLoader()
        {
            MainWindow.Instance.MainText = $"{(string)App.Current.FindResource("Mods:UnInstallingMelonLoader")}...";

            try
            {
                var versionDllPath = Path.Combine(App.ChilloutInstallDirectory, "version.dll");
                var melonLoaderDirPath = Path.Combine(App.ChilloutInstallDirectory, "MelonLoader");

                if (File.Exists(versionDllPath))
                    File.Delete(versionDllPath);
                if (Directory.Exists(melonLoaderDirPath))
                    Directory.Delete(melonLoaderDirPath, true);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"{App.Current.FindResource("Mods:UninstallMLFailed")}.\n\n" + ex);
                return false;
            }

            return true;
        }

        public static async Task InstallMelonLoader()
        {
            if (!RemoveMelonLoader()) return;

            try
            {
                MainWindow.Instance.MainText = $"{(string) App.Current.FindResource("Mods:DownloadingMelonLoader")}...";

                using var installerZip = await DownloadFileToMemory("https://github.com/LavaGang/MelonLoader/releases/latest/download/MelonLoader.x64.zip");
                using var zipReader = new ZipArchive(installerZip, ZipArchiveMode.Read);

                MainWindow.Instance.MainText = $"{(string) App.Current.FindResource("Mods:UnpackingMelonLoader")}...";

                foreach (var zipArchiveEntry in zipReader.Entries)
                {
                    var targetFileName = Path.Combine(App.ChilloutInstallDirectory, zipArchiveEntry.FullName);
                    Directory.CreateDirectory(Path.GetDirectoryName(targetFileName));
                    using var targetFile = File.OpenWrite(targetFileName);
                    using var entryStream = zipArchiveEntry.Open();
                    await entryStream.CopyToAsync(targetFile);
                }

                Directory.CreateDirectory(Path.Combine(App.ChilloutInstallDirectory, "Mods"));
                Directory.CreateDirectory(Path.Combine(App.ChilloutInstallDirectory, "Plugins"));
            }
            catch (Exception ex)
            {
                MessageBox.Show($"{App.Current.FindResource("Mods:InstallMLFailed")}.\n\n" + ex);
            }
        }

        internal static async Task<Stream> DownloadFileToMemory(string link)
        {
            using var resp = await Http.HttpClient.GetAsync(link);
            var newStream = new MemoryStream();
            await resp.Content.CopyToAsync(newStream);
            newStream.Position = 0;
            return newStream;
        }

        public static async Task InstallMod(Mod mod)
        {
            string downloadLink = mod.versions[0].downloadLink;

            if (string.IsNullOrEmpty(downloadLink))
            {
                MessageBox.Show(string.Format((string)App.Current.FindResource("Mods:ModDownloadLinkMissing"), mod.versions[0].name));
                return;
            }

            if (mod.installedFilePath != null)
                File.Delete(mod.installedFilePath);


            string targetFilePath = "";

            using (var resp = await Http.HttpClient.GetAsync(downloadLink))
            {
                var stream = new MemoryStream();
                await resp.Content.CopyToAsync(stream);
                stream.Position = 0;

                targetFilePath = Path.Combine(App.ChilloutInstallDirectory, mod.versions[0].IsPlugin ? "Plugins" : "Mods",
                    mod.versions[0].IsBroken ? "Broken" : (mod.versions[0].IsRetired ? "Retired" : ""), resp.RequestMessage.RequestUri.Segments.Last());

                Directory.CreateDirectory(Path.GetDirectoryName(targetFilePath));

                using var targetFile = File.OpenWrite(targetFilePath);
                await stream.CopyToAsync(targetFile);
            }

            mod.ListItem.IsInstalled = true;
            mod.installedFilePath = targetFilePath;
            mod.ListItem.InstalledVersion = mod.versions[0].modVersion;
            mod.ListItem.InstalledModInfo = mod;
        }
    }
}
