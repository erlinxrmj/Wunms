using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Wumms
{
    public class MainForm : Form
    {
        private readonly AppSettings settings;
        private EqState state;
        private bool eqOn;          // startet IMMER aus
        private bool apoReady;
        private bool loading;
        private bool balloonShown;
        private readonly float S;

        private ToggleSwitch mainToggle, guardToggle;
        private Label lblState, lblBassVal, lblFreq, lblPreamp, lblStatus;
        private Slider bassSlider, freqSlider;
        private readonly Slider[] bandSliders = new Slider[10];
        private readonly Label[] bandVals = new Label[10];
        private CurveView curve;
        private ComboBox presetBox;
        private FlatButton btnSave, btnDelete;
        private NotifyIcon tray;
        private ToolStripMenuItem trayToggleItem;
        private readonly Timer applyTimer = new Timer { Interval = 80 };
        private readonly List<Preset> presetList = new List<Preset>();

        private const int HotkeyId = 1;

        [DllImport("user32.dll")] private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint mods, uint vk);
        [DllImport("user32.dll")] private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
        [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

        public MainForm()
        {
            settings = AppSettings.Load();
            state = settings.Current.Clone();

            AutoScaleMode = AutoScaleMode.None;
            S = DeviceDpi / 96f;
            Text = "Wumms – Bass & Equalizer";
            try { Icon = Icon.ExtractAssociatedIcon(Environment.ProcessPath); } catch { }
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Theme.Bg;
            ForeColor = Theme.Text;
            Font = new Font("Segoe UI", 9f);
            ClientSize = new Size(D(980), D(636));

            BuildUi();
            BuildTray();

            applyTimer.Tick += (s, e) => { applyTimer.Stop(); Apply(); };

            RefreshPresetBox(settings.LastPreset);
            LoadStateIntoUi();

            // Beim Start immer AUS – auch falls Wumms beim letzten Mal abgestürzt ist
            apoReady = Apo.IsReady();
            if (apoReady) SafeWrite(Eq.OffText);
            UpdateUi();
        }

        private int D(int v) => (int)Math.Round(v * S);
        private Rectangle R(int x, int y, int w, int h) => new Rectangle(D(x), D(y), D(w), D(h));

        private Label L(Control parent, string text, float size, FontStyle style, Color color, int x, int y)
        {
            var l = new Label
            {
                Text = text,
                AutoSize = true,
                Font = new Font("Segoe UI", size, style),
                ForeColor = color,
                BackColor = parent.BackColor,
                Location = new Point(D(x), D(y))
            };
            parent.Controls.Add(l);
            return l;
        }

        // ---------------------------------------------------------------- Aufbau

        private void BuildUi()
        {
            const int W = 980;

            // Kopfzeile
            L(this, "Wumms", 28f, FontStyle.Bold, Theme.Text, 24, 10);
            L(this, "Bass & Equalizer  ·  von Erlin Ramaj", 9.5f, FontStyle.Regular, Theme.Sub, 30, 66);

            mainToggle = new ToggleSwitch { Bounds = R(W - 28 - 112, 26, 112, 52), BackColor = Theme.Bg };
            mainToggle.CheckedChanged += (s, e) => OnMainToggle();
            Controls.Add(mainToggle);

            lblState = new Label
            {
                AutoSize = false,
                Bounds = R(W - 28 - 112 - 150, 26, 140, 52),
                TextAlign = ContentAlignment.MiddleRight,
                Font = new Font("Segoe UI", 18f, FontStyle.Bold),
                ForeColor = Theme.Sub,
                BackColor = Theme.Bg,
                Text = "AUS"
            };
            Controls.Add(lblState);

            Controls.Add(new Label
            {
                AutoSize = false,
                Bounds = R(W - 28 - 300, 80, 300, 18),
                TextAlign = ContentAlignment.TopRight,
                Font = new Font("Segoe UI", 8.5f),
                ForeColor = Theme.Sub,
                BackColor = Theme.Bg,
                Text = "Ein/Aus auch mit Strg + Alt + B"
            });

            curve = new CurveView { Bounds = R(28, 106, W - 56, 176), Response = f => Eq.ResponseDb(state, f) };
            Controls.Add(curve);

            // Bass-Karte
            var bass = new Card { Bounds = R(28, 298, 330, 290) };
            Controls.Add(bass);
            L(bass, "BASS", 9f, FontStyle.Bold, Theme.Sub, 20, 16);
            lblBassVal = L(bass, "", 30f, FontStyle.Bold, Theme.Text, 14, 32);

            bassSlider = new Slider { Bounds = R(12, 102, 306, 30), Minimum = 0, Maximum = 20, Step = 0.5, DefaultValue = 8 };
            bassSlider.ValueChanged += (s, e) => { if (loading) return; state.BassGain = bassSlider.Value; Changed(); };
            bass.Controls.Add(bassSlider);

            lblFreq = L(bass, "", 9f, FontStyle.Regular, Theme.Sub, 20, 144);
            freqSlider = new Slider { Bounds = R(12, 166, 306, 30), Minimum = 40, Maximum = 200, Step = 5, DefaultValue = 90, Accent = Theme.Accent2 };
            freqSlider.ValueChanged += (s, e) => { if (loading) return; state.BassFreq = freqSlider.Value; Changed(); };
            bass.Controls.Add(freqSlider);

            guardToggle = new ToggleSwitch { Bounds = R(20, 214, 46, 26), BackColor = Theme.Card, OnColor = Theme.Accent2 };
            guardToggle.CheckedChanged += (s, e) => { if (loading) return; state.ClipGuard = guardToggle.Checked; Changed(false); };
            bass.Controls.Add(guardToggle);
            L(bass, "Schutz vor Verzerren", 9.5f, FontStyle.Regular, Theme.Text, 74, 216);
            lblPreamp = L(bass, "", 8.5f, FontStyle.Regular, Theme.Sub, 20, 252);

            // Equalizer-Karte
            var eq = new Card { Bounds = R(372, 298, W - 28 - 372, 290) };
            Controls.Add(eq);
            L(eq, "EQUALIZER", 9f, FontStyle.Bold, Theme.Sub, 20, 16);

            presetBox = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                FlatStyle = FlatStyle.Flat,
                BackColor = Theme.Panel2,
                ForeColor = Theme.Text,
                DrawMode = DrawMode.OwnerDrawFixed,
                Font = new Font("Segoe UI", 9.5f),
                ItemHeight = D(22),
                MaxDropDownItems = 14,
                Location = new Point(D(150), D(11)),
                Width = D(210)
            };
            presetBox.DrawItem += DrawPresetItem;
            presetBox.SelectedIndexChanged += (s, e) => { if (!loading) LoadPreset(); };
            eq.Controls.Add(presetBox);

            btnSave = new FlatButton { Text = "Speichern", Bounds = R(370, 11, 92, 28) };
            btnSave.Click += (s, e) => SavePreset();
            eq.Controls.Add(btnSave);

            btnDelete = new FlatButton { Text = "Löschen", Bounds = R(468, 11, 84, 28) };
            btnDelete.Click += (s, e) => DeletePreset();
            eq.Controls.Add(btnDelete);

            string[] names = { "31", "62", "125", "250", "500", "1k", "2k", "4k", "8k", "16k" };
            const int colW = 54;
            for (int i = 0; i < 10; i++)
            {
                int cx = 20 + i * colW;
                bandVals[i] = new Label
                {
                    AutoSize = false,
                    Bounds = R(cx, 52, colW, 18),
                    TextAlign = ContentAlignment.MiddleCenter,
                    Font = new Font("Segoe UI", 8.5f),
                    ForeColor = Theme.Sub,
                    BackColor = Theme.Card
                };
                eq.Controls.Add(bandVals[i]);

                var sl = new Slider
                {
                    Vertical = true,
                    Bipolar = true,
                    Minimum = -12,
                    Maximum = 12,
                    Step = 0.5,
                    DefaultValue = 0,
                    Bounds = R(cx + 12, 72, 30, 162)
                };
                int idx = i;
                sl.ValueChanged += (s, e) => { if (loading) return; state.Bands[idx] = sl.Value; Changed(); };
                bandSliders[i] = sl;
                eq.Controls.Add(sl);

                eq.Controls.Add(new Label
                {
                    AutoSize = false,
                    Bounds = R(cx, 240, colW, 20),
                    TextAlign = ContentAlignment.MiddleCenter,
                    Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                    ForeColor = Theme.Text,
                    BackColor = Theme.Card,
                    Text = names[i]
                });
            }
            eq.Controls.Add(new Label
            {
                AutoSize = false,
                Bounds = R(20, 262, 540, 18),
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 8f),
                ForeColor = Theme.Sub,
                BackColor = Theme.Card,
                Text = "Hz  ·  Doppelklick auf einen Regler setzt ihn zurück  ·  Mausrad für feine Schritte"
            });

            // Fusszeile
            lblStatus = L(this, "", 9f, FontStyle.Regular, Theme.Sub, 28, 602);
        }

        private void BuildTray()
        {
            var menu = new ContextMenuStrip();
            trayToggleItem = new ToolStripMenuItem("Bass einschalten", null, (s, e) => mainToggle.Checked = !mainToggle.Checked);
            menu.Items.Add(trayToggleItem);
            menu.Items.Add("Wumms öffnen", null, (s, e) => ShowFromTray());
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Beenden (Bass aus)", null, (s, e) => Close());

            tray = new NotifyIcon
            {
                Icon = Icon ?? SystemIcons.Application,
                Text = "Wumms – Bass AUS",
                Visible = true,
                ContextMenuStrip = menu
            };
            tray.DoubleClick += (s, e) => ShowFromTray();
        }

        // ---------------------------------------------------------------- Logik

        private void OnMainToggle()
        {
            if (mainToggle.Checked)
            {
                if (!EnsureApo())
                {
                    mainToggle.SetSilent(false);
                    eqOn = false;
                    UpdateUi();
                    return;
                }
                eqOn = true;
            }
            else
            {
                eqOn = false;
            }
            applyTimer.Stop();
            Apply();
            UpdateUi();
        }

        private bool EnsureApo()
        {
            if (apoReady && Apo.IsReady()) return true;

            if (!Apo.Installed())
            {
                var r = MessageBox.Show(this,
                    "Wumms braucht den kostenlosen Audio-Treiber „Equalizer APO“, damit der Bass auf allen Ton am PC wirkt.\n\n" +
                    "So geht's (nur einmal nötig):\n" +
                    "1. Equalizer APO herunterladen und installieren\n" +
                    "2. Im Fenster „Configurator“ deine Kopfhörer / Lautsprecher anhaken\n" +
                    "3. PC neu starten, dann Wumms öffnen und einschalten\n\n" +
                    "Download-Seite jetzt öffnen?",
                    "Equalizer APO fehlt", MessageBoxButtons.YesNo, MessageBoxIcon.Information);
                if (r == DialogResult.Yes) OpenUrl(Apo.DownloadUrl);
                return false;
            }

            var ok = MessageBox.Show(this,
                "Einmalige Einrichtung: Wumms verbindet sich jetzt mit Equalizer APO.\n" +
                "Windows fragt dafür einmal nach Administrator-Rechten – danach nie wieder.\n\n" +
                "Deine bisherige Equalizer-APO-Einstellung wird als „config.vor-wumms.txt“ gesichert.",
                "Wumms einrichten", MessageBoxButtons.OKCancel, MessageBoxIcon.Information);
            if (ok != DialogResult.OK) return false;

            apoReady = Apo.RunSetupElevated();
            if (!apoReady)
                MessageBox.Show(this,
                    "Die Einrichtung hat nicht geklappt. Hast du bei der Windows-Abfrage „Ja“ geklickt?\n" +
                    "Versuch es einfach nochmal mit dem Schalter.",
                    "Wumms", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return apoReady;
        }

        private void Changed(bool markCustom = true)
        {
            if (markCustom && presetBox.SelectedIndex != -1)
            {
                loading = true;
                presetBox.SelectedIndex = -1;
                loading = false;
                settings.LastPreset = null;
                UpdateDeleteButton();
            }
            UpdateUi();
            if (eqOn)
            {
                applyTimer.Stop();
                applyTimer.Start();
            }
        }

        private void Apply()
        {
            if (!apoReady) return;
            SafeWrite(eqOn ? Eq.BuildConfig(state) : Eq.OffText);
        }

        private void SafeWrite(string text)
        {
            try { Apo.Write(text); }
            catch (Exception ex)
            {
                apoReady = false;
                if (lblStatus != null) lblStatus.Text = "Konnte Equalizer APO nicht ansteuern: " + ex.Message;
            }
        }

        // ---------------------------------------------------------------- Anzeige

        private static string Db(double v, string fmt) => (v > 0 ? "+" : "") + v.ToString(fmt);

        private void LoadStateIntoUi()
        {
            loading = true;
            bassSlider.SetValueSilent(state.BassGain);
            freqSlider.SetValueSilent(state.BassFreq);
            for (int i = 0; i < 10; i++) bandSliders[i].SetValueSilent(state.Bands[i]);
            guardToggle.SetSilent(state.ClipGuard);
            loading = false;
            UpdateUi();
        }

        private void UpdateUi()
        {
            lblBassVal.Text = Db(state.BassGain, "0.0") + " dB";
            lblFreq.Text = "Tiefe: " + state.BassFreq.ToString("0") + " Hz   (weniger = tiefer, mehr Druck)";
            for (int i = 0; i < 10; i++)
            {
                bandVals[i].Text = Db(state.Bands[i], "0.#");
                bandVals[i].ForeColor = Math.Abs(state.Bands[i]) > 0.01 ? Theme.Text : Theme.Sub;
            }

            double pre = Eq.Preamp(state);
            lblPreamp.Text = !state.ClipGuard
                ? "Achtung: bei viel Bass kann es verzerren"
                : pre < 0 ? "Wird automatisch um " + Math.Abs(pre).ToString("0.0") + " dB leiser gemacht" : "Kein Ausgleich nötig";

            lblState.Text = eqOn ? "AN" : "AUS";
            lblState.ForeColor = eqOn ? Theme.Accent : Theme.Sub;

            foreach (var sl in bandSliders) { sl.Dimmed = !eqOn; sl.Invalidate(); }
            bassSlider.Dimmed = freqSlider.Dimmed = !eqOn;
            bassSlider.Invalidate();
            freqSlider.Invalidate();

            curve.Active = eqOn;
            curve.Invalidate();

            if (tray != null)
            {
                tray.Text = eqOn ? "Wumms – Bass AN" : "Wumms – Bass AUS";
                trayToggleItem.Text = eqOn ? "Bass ausschalten" : "Bass einschalten";
            }

            UpdateStatus();
        }

        private void UpdateStatus()
        {
            if (lblStatus == null) return;
            if (!Apo.Installed())
                lblStatus.Text = "Equalizer APO nicht gefunden – schalte Wumms ein, dann zeige ich dir, wie's geht.";
            else if (!apoReady)
                lblStatus.Text = "Equalizer APO gefunden – beim ersten Einschalten wird Wumms einmalig verbunden.";
            else
                lblStatus.Text = eqOn
                    ? "●  Aktiv – dein Sound läuft durch Wumms. Tipp: Windows-Lautstärke etwas höher drehen."
                    : "Bereit – der Ton läuft gerade unverändert.";
        }

        // ---------------------------------------------------------------- Presets

        private void RefreshPresetBox(string select)
        {
            loading = true;
            presetBox.Items.Clear();
            presetList.Clear();
            foreach (var p in Presets.BuiltIn) { presetList.Add(p); presetBox.Items.Add(p.Name); }
            foreach (var p in settings.UserPresets) { presetList.Add(p); presetBox.Items.Add("★ " + p.Name); }
            presetBox.SelectedIndex = select == null ? -1 : presetList.FindIndex(p => p.Name == select);
            loading = false;
            UpdateDeleteButton();
        }

        private bool IsUserPreset(int idx) => idx >= Presets.BuiltIn.Count && idx < presetList.Count;

        private void UpdateDeleteButton() => btnDelete.Enabled = IsUserPreset(presetBox.SelectedIndex);

        private void LoadPreset()
        {
            int idx = presetBox.SelectedIndex;
            if (idx < 0 || idx >= presetList.Count) return;
            var p = presetList[idx];
            bool guard = state.ClipGuard;
            state = p.State.Clone();
            state.ClipGuard = guard;
            settings.LastPreset = p.Name;
            LoadStateIntoUi();
            UpdateDeleteButton();
            if (eqOn) Apply();
        }

        private void SavePreset()
        {
            string name = Prompt("Einstellung speichern", "Wie soll deine Einstellung heissen?");
            if (string.IsNullOrWhiteSpace(name)) return;
            if (Presets.BuiltIn.Exists(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
                name += " (eigen)";
            settings.UserPresets.RemoveAll(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            settings.UserPresets.Add(new Preset { Name = name, State = state.Clone() });
            settings.LastPreset = name;
            settings.Save();
            RefreshPresetBox(name);
        }

        private void DeletePreset()
        {
            int idx = presetBox.SelectedIndex;
            if (!IsUserPreset(idx)) return;
            var p = presetList[idx];
            if (MessageBox.Show(this, "„" + p.Name + "“ löschen?", "Wumms", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;
            settings.UserPresets.Remove(p);
            settings.LastPreset = null;
            settings.Save();
            RefreshPresetBox(null);
        }

        private void DrawPresetItem(object sender, DrawItemEventArgs e)
        {
            bool selected = (e.State & DrawItemState.Selected) != 0 && (e.State & DrawItemState.ComboBoxEdit) == 0;
            using (var bg = new SolidBrush(selected ? Theme.Accent2 : Theme.Panel2))
                e.Graphics.FillRectangle(bg, e.Bounds);
            string text = e.Index >= 0 ? presetBox.Items[e.Index].ToString() : "Eigene Einstellung";
            var rect = new Rectangle(e.Bounds.X + D(6), e.Bounds.Y, e.Bounds.Width - D(6), e.Bounds.Height);
            TextRenderer.DrawText(e.Graphics, text, presetBox.Font, rect, e.Index >= 0 ? Theme.Text : Theme.Sub,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }

        private string Prompt(string title, string question)
        {
            using var f = new Form
            {
                Text = title,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                StartPosition = FormStartPosition.CenterParent,
                MinimizeBox = false,
                MaximizeBox = false,
                ShowInTaskbar = false,
                AutoScaleMode = AutoScaleMode.None,
                BackColor = Theme.Bg,
                ForeColor = Theme.Text,
                Font = new Font("Segoe UI", 9.5f),
                ClientSize = new Size(D(360), D(124))
            };
            var lbl = new Label { Text = question, AutoSize = true, Location = new Point(D(16), D(14)), BackColor = Theme.Bg };
            var tb = new TextBox
            {
                Bounds = R(16, 40, 328, 26),
                BackColor = Theme.Panel2,
                ForeColor = Theme.Text,
                BorderStyle = BorderStyle.FixedSingle,
                MaxLength = 40
            };
            var ok = new Button { Text = "Speichern", DialogResult = DialogResult.OK, Bounds = R(160, 80, 90, 30), FlatStyle = FlatStyle.Flat, BackColor = Theme.Accent, ForeColor = Color.White };
            ok.FlatAppearance.BorderSize = 0;
            var cancel = new Button { Text = "Abbrechen", DialogResult = DialogResult.Cancel, Bounds = R(256, 80, 88, 30), FlatStyle = FlatStyle.Flat, BackColor = Theme.Panel2, ForeColor = Theme.Text };
            cancel.FlatAppearance.BorderSize = 0;
            f.Controls.AddRange(new Control[] { lbl, tb, ok, cancel });
            f.AcceptButton = ok;
            f.CancelButton = cancel;
            return f.ShowDialog(this) == DialogResult.OK ? tb.Text.Trim() : null;
        }

        // ---------------------------------------------------------------- Fenster, Tray, Tastenkürzel

        private void ShowFromTray()
        {
            Show();
            WindowState = FormWindowState.Normal;
            Activate();
        }

        private static void OpenUrl(string url)
        {
            try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); } catch { }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            // Strg + Alt + B  (MOD_ALT | MOD_CONTROL | MOD_NOREPEAT)
            RegisterHotKey(Handle, HotkeyId, 0x0001 | 0x0002 | 0x4000, 0x42);
            try { int dark = 1; DwmSetWindowAttribute(Handle, 20, ref dark, sizeof(int)); } catch { }
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == 0x0312 && m.WParam.ToInt32() == HotkeyId)
            {
                mainToggle.Checked = !mainToggle.Checked;
                if (!Visible) tray?.ShowBalloonTip(800, "Wumms", eqOn ? "Bass AN" : "Bass AUS", ToolTipIcon.None);
            }
            base.WndProc(ref m);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (WindowState == FormWindowState.Minimized && tray != null)
            {
                Hide();
                if (!balloonShown)
                {
                    tray.ShowBalloonTip(2000, "Wumms läuft weiter",
                        "Unten rechts neben der Uhr. Rechtsklick für Ein/Aus, oder Strg + Alt + B.", ToolTipIcon.None);
                    balloonShown = true;
                }
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            // Wumms zu = Bass aus (auch beim Herunterfahren)
            applyTimer.Stop();
            if (apoReady) { try { Apo.Write(Eq.OffText); } catch { } }
            settings.Current = state.Clone();
            settings.Save();
            try { UnregisterHotKey(Handle, HotkeyId); } catch { }
            if (tray != null) { tray.Visible = false; tray.Dispose(); tray = null; }
            base.OnFormClosing(e);
        }
    }
}
