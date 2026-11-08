using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;

namespace Wumms
{
    /// <summary>Alle Klangeinstellungen.</summary>
    public class EqState
    {
        public double BassGain { get; set; } = 8;     // dB, 0..20
        public double BassFreq { get; set; } = 90;    // Hz, 40..200
        public double[] Bands { get; set; } = new double[10]; // dB, -12..+12
        public bool ClipGuard { get; set; } = true;

        public EqState Clone() => new EqState
        {
            BassGain = BassGain,
            BassFreq = BassFreq,
            Bands = (double[])(Bands != null && Bands.Length == 10 ? Bands : new double[10]).Clone(),
            ClipGuard = ClipGuard
        };
    }

    public class Preset
    {
        public string Name { get; set; }
        public EqState State { get; set; }
    }

    public static class Eq
    {
        public static readonly double[] BandFreqs = { 31, 62, 125, 250, 500, 1000, 2000, 4000, 8000, 16000 };
        public const double BandQ = 1.41;
        public const double ShelfQ = 0.71;
        private const double Fs = 48000;

        public const string OffText = "# Wumms - AUS. Der Ton laeuft unveraendert.\r\n";

        private enum Kind { Peak, LowShelf }

        /// <summary>Gesamtverstärkung (dB) bei Frequenz f – für die Kurve und den Pegelausgleich.</summary>
        public static double ResponseDb(EqState s, double f)
        {
            double sum = 0;
            if (Math.Abs(s.BassGain) > 0.01) sum += Mag(Kind.LowShelf, s.BassFreq, s.BassGain, ShelfQ, f);
            for (int i = 0; i < 10; i++)
                if (Math.Abs(s.Bands[i]) > 0.01) sum += Mag(Kind.Peak, BandFreqs[i], s.Bands[i], BandQ, f);
            return sum;
        }

        public static double PeakDb(EqState s)
        {
            double max = double.MinValue;
            for (int i = 0; i <= 400; i++)
            {
                double f = 20 * Math.Pow(1000, i / 400.0);
                max = Math.Max(max, ResponseDb(s, f));
            }
            return max;
        }

        /// <summary>So viel wird automatisch leiser gemacht, damit nichts übersteuert.</summary>
        public static double Preamp(EqState s)
        {
            if (!s.ClipGuard) return 0;
            double p = PeakDb(s);
            return p > 0 ? -Math.Round(p + 0.2, 1) : 0;
        }

        /// <summary>Text für Equalizer APO.</summary>
        public static string BuildConfig(EqState s)
        {
            var ic = CultureInfo.InvariantCulture;
            var sb = new StringBuilder();
            sb.Append("# Wumms (Erlin Ramaj) - AN. Wird automatisch geschrieben.\r\n");
            sb.Append(string.Format(ic, "Preamp: {0:0.0} dB\r\n", Preamp(s)));
            if (Math.Abs(s.BassGain) > 0.01)
                sb.Append(string.Format(ic, "Filter: ON LSC Fc {0:0} Hz Gain {1:0.0} dB Q {2:0.00}\r\n", s.BassFreq, s.BassGain, ShelfQ));
            for (int i = 0; i < 10; i++)
                if (Math.Abs(s.Bands[i]) > 0.01)
                    sb.Append(string.Format(ic, "Filter: ON PK Fc {0:0} Hz Gain {1:0.0} dB Q {2:0.00}\r\n", BandFreqs[i], s.Bands[i], BandQ));
            return sb.ToString();
        }

        // RBJ-Audio-EQ-Cookbook: Betrag des Biquads bei Frequenz f
        private static double Mag(Kind k, double f0, double gainDb, double q, double f)
        {
            double A = Math.Pow(10, gainDb / 40);
            double w0 = 2 * Math.PI * f0 / Fs;
            double cs = Math.Cos(w0), sn = Math.Sin(w0);
            double alpha = sn / (2 * q);
            double b0, b1, b2, a0, a1, a2;
            if (k == Kind.Peak)
            {
                b0 = 1 + alpha * A; b1 = -2 * cs; b2 = 1 - alpha * A;
                a0 = 1 + alpha / A; a1 = -2 * cs; a2 = 1 - alpha / A;
            }
            else
            {
                double sq = 2 * Math.Sqrt(A) * alpha;
                b0 = A * ((A + 1) - (A - 1) * cs + sq);
                b1 = 2 * A * ((A - 1) - (A + 1) * cs);
                b2 = A * ((A + 1) - (A - 1) * cs - sq);
                a0 = (A + 1) + (A - 1) * cs + sq;
                a1 = -2 * ((A - 1) + (A + 1) * cs);
                a2 = (A + 1) + (A - 1) * cs - sq;
            }
            double w = 2 * Math.PI * f / Fs;
            double c1 = Math.Cos(w), s1 = Math.Sin(w), c2 = Math.Cos(2 * w), s2 = Math.Sin(2 * w);
            double br = b0 + b1 * c1 + b2 * c2, bi = -(b1 * s1 + b2 * s2);
            double ar = a0 + a1 * c1 + a2 * c2, ai = -(a1 * s1 + a2 * s2);
            double mag = Math.Sqrt((br * br + bi * bi) / (ar * ar + ai * ai));
            return 20 * Math.Log10(Math.Max(mag, 1e-9));
        }
    }

    public static class Presets
    {
        public static readonly List<Preset> BuiltIn = new List<Preset>
        {
            P("Neutral",            0,  90,  0, 0, 0, 0, 0, 0, 0, 0, 0, 0),
            P("Bass Boost",         8,  90,  2, 2, 1, 0, 0, 0, 0, 0, 0, 0),
            P("Bass Extrem",       14,  80,  4, 4, 2, 0,-1, 0, 0, 1, 1, 0),
            P("Tiefbass (Sub)",    12,  55,  6, 3, 0,-1,-1, 0, 0, 0, 0, 0),
            P("Hip-Hop / Trap",    10,  75,  3, 3, 1,-1,-1, 0, 1, 1, 2, 1),
            P("Elektro / EDM",      9,  85,  3, 2, 0,-1,-2, 0, 1, 2, 3, 3),
            P("Rock",               4, 100,  2, 2, 1, 0,-1,-1, 1, 2, 3, 2),
            P("Pop",                4, 100,  1, 1, 1, 0, 0, 1, 2, 2, 1, 1),
            P("Stimmen / Podcast",  0,  90, -3,-2,-1, 0, 1, 3, 3, 2, 1, 0),
            P("Gaming (Schritte)",  2,  90,  0, 0, 0,-1, 0, 1, 3, 4, 2, 0),
        };

        private static Preset P(string name, double bass, double freq, params double[] bands) =>
            new Preset { Name = name, State = new EqState { BassGain = bass, BassFreq = freq, Bands = bands } };
    }

    public class AppSettings
    {
        public EqState Current { get; set; } = new EqState();
        public List<Preset> UserPresets { get; set; } = new List<Preset>();
        public string LastPreset { get; set; }

        private static string Dir => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Wumms");
        private static string FilePath => Path.Combine(Dir, "einstellungen.json");

        public static AppSettings Load()
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    var s = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath));
                    if (s != null) { s.Normalize(); return s; }
                }
            }
            catch { }
            return new AppSettings();
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(Dir);
                File.WriteAllText(FilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch { }
        }

        private void Normalize()
        {
            Current = (Current ?? new EqState()).Clone();
            UserPresets ??= new List<Preset>();
            UserPresets.RemoveAll(p => p == null || string.IsNullOrWhiteSpace(p.Name) || p.State == null);
            foreach (var p in UserPresets) p.State = p.State.Clone();
        }
    }
}
