using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace Wumms
{
    /// <summary>
    /// Verbindung zu Equalizer APO. Wumms schreibt seine Filter in die Datei "Wumms.txt"
    /// im config-Ordner von Equalizer APO; die config.txt bindet sie per "Include" ein.
    /// Equalizer APO merkt Änderungen sofort und übernimmt sie ohne Neustart.
    /// </summary>
    public static class Apo
    {
        public const string IncludeFile = "Wumms.txt";
        public const string DownloadUrl = "https://sourceforge.net/projects/equalizerapo/";

        public static string ConfigDir()
        {
            try
            {
                using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
                using var k = baseKey.OpenSubKey(@"SOFTWARE\EqualizerAPO");
                if (k != null)
                {
                    if (k.GetValue("ConfigPath") is string cp && Directory.Exists(cp)) return cp;
                    if (k.GetValue("InstallPath") is string ip)
                    {
                        string c = Path.Combine(ip, "config");
                        if (Directory.Exists(c)) return c;
                    }
                }
            }
            catch { }

            string def = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "EqualizerAPO", "config");
            return Directory.Exists(def) ? def : null;
        }

        public static bool Installed() => ConfigDir() != null;

        private static string WummsPath(string dir) => Path.Combine(dir, IncludeFile);
        private static string ConfigPath(string dir) => Path.Combine(dir, "config.txt");

        private static bool HasInclude(string cfg) =>
            cfg.Split('\n').Select(l => l.Trim())
               .Any(l => l.StartsWith("Include:", StringComparison.OrdinalIgnoreCase)
                      && l.IndexOf(IncludeFile, StringComparison.OrdinalIgnoreCase) >= 0);

        /// <summary>Installiert, eingebunden und ohne Admin-Rechte beschreibbar?</summary>
        public static bool IsReady()
        {
            string dir = ConfigDir();
            if (dir == null) return false;
            try
            {
                if (!File.Exists(ConfigPath(dir)) || !HasInclude(File.ReadAllText(ConfigPath(dir)))) return false;
                if (!File.Exists(WummsPath(dir))) return false;
                using var fs = new FileStream(WummsPath(dir), FileMode.Open, FileAccess.Write, FileShare.ReadWrite);
                return true;
            }
            catch { return false; }
        }

        public static void Write(string text)
        {
            string dir = ConfigDir() ?? throw new IOException("Equalizer APO nicht gefunden.");
            byte[] bytes = new UTF8Encoding(false).GetBytes(text);
            using var fs = new FileStream(WummsPath(dir), FileMode.Open, FileAccess.Write, FileShare.ReadWrite);
            fs.SetLength(0);
            fs.Write(bytes, 0, bytes.Length);
        }

        /// <summary>Startet die Einrichtung mit Admin-Rechten (Windows fragt einmal nach).</summary>
        public static bool RunSetupElevated()
        {
            try
            {
                string sid = WindowsIdentity.GetCurrent().User?.Value;
                if (sid == null) return false;
                var psi = new ProcessStartInfo(Environment.ProcessPath, "--setup " + sid)
                {
                    UseShellExecute = true,
                    Verb = "runas"
                };
                using var p = Process.Start(psi);
                p?.WaitForExit();
                return IsReady();
            }
            catch (Win32Exception) { return false; } // Nutzer hat "Nein" geklickt
            catch { return false; }
        }

        /// <summary>Läuft im Admin-Prozess.</summary>
        public static int DoSetup(string sid)
        {
            try
            {
                if (!Regex.IsMatch(sid, @"^S-1-[0-9-]+$")) return 2;
                string dir = ConfigDir();
                if (dir == null) return 3;

                File.WriteAllText(WummsPath(dir), Eq.OffText, new UTF8Encoding(false));

                string cfgPath = ConfigPath(dir);
                string cfg = File.Exists(cfgPath) ? File.ReadAllText(cfgPath) : "";
                if (!HasInclude(cfg))
                {
                    if (cfg.Trim().Length > 0)
                        File.Copy(cfgPath, Path.Combine(dir, "config.vor-wumms.txt"), true);
                    File.WriteAllText(cfgPath, "# Gesteuert von Wumms (Erlin Ramaj)\r\nInclude: " + IncludeFile + "\r\n", new UTF8Encoding(false));
                }

                // Damit Wumms die Datei später ohne Admin-Rechte ändern darf
                var psi = new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "icacls.exe"),
                    "\"" + WummsPath(dir) + "\" /grant *" + sid + ":M")
                {
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using var p = Process.Start(psi);
                p?.WaitForExit();
                return p != null && p.ExitCode == 0 ? 0 : 4;
            }
            catch { return 1; }
        }
    }
}
