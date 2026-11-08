# Wumms – Bass & Equalizer für Windows

von **Erlin Ramaj**

Wumms macht den Bass am PC richtig satt – für alles, was du hörst: Spotify, YouTube, Spiele, Musikprogramme.
Dazu ein 10-Band-Equalizer, fertige Klang-Presets und eigene gespeicherte Einstellungen.

**Wumms startet nie automatisch** und ist beim Öffnen immer **AUS**. Du schaltest es selbst ein und aus.
Schliesst du Wumms, ist der Bass sofort wieder aus.

## Wumms.exe herunterladen

*Actions* → neuester Lauf von „Wumms bauen“ → ganz unten bei *Artifacts* auf **Wumms** klicken → ZIP entpacken → `Wumms.exe`.

## Einmalige Vorbereitung

1. **Equalizer APO** installieren (gratis): https://sourceforge.net/projects/equalizerapo/
2. Am Ende der Installation öffnet sich der **Configurator** → deine Kopfhörer / Lautsprecher anhaken → OK.
3. PC neu starten.
4. `Wumms.exe` öffnen und den grossen Schalter einschalten. Beim allerersten Mal fragt Windows einmal nach
   Administrator-Rechten (damit Wumms sich mit Equalizer APO verbinden darf) – danach nie wieder.

> Deine alte Equalizer-APO-Einstellung wird dabei als `config.vor-wumms.txt` gesichert.

## Bedienung

| Was | Wie |
|---|---|
| Ein / Aus | Grosser Schalter oben rechts · **Strg + Alt + B** · Rechtsklick aufs Symbol unten rechts |
| Bass-Stärke | Oberer Regler in der Bass-Karte (0 bis +20 dB) |
| Bass-Tiefe | Unterer Regler: weniger Hz = tieferer, „körperlicher“ Bass |
| Equalizer | 10 Regler von 31 Hz bis 16 kHz (−12 bis +12 dB) |
| Zurücksetzen | Doppelklick auf einen Regler |
| Feinjustieren | Mausrad über einem Regler |
| Presets | Liste oben in der Equalizer-Karte; „Speichern“ für eigene |
| Im Hintergrund | Fenster minimieren → läuft unten rechts neben der Uhr weiter |

**Schutz vor Verzerren:** Viel Bass macht das Signal lauter, als die Soundkarte darf, dann klirrt es.
Wumms senkt deshalb automatisch die Gesamtlautstärke ein wenig – dreh einfach die Windows-Lautstärke etwas höher.

## Gut zu wissen

- Zum **Musikmachen / Abmischen**: Für den Mix lieber Wumms ausschalten, damit du neutral hörst – sonst klingt dein Track
  auf anderen Geräten zu dünn.
- Musikprogramme mit **ASIO-Treiber** umgehen Windows-Effekte; dort wirkt Wumms nicht.
- Neues Audiogerät (z.B. neue Kopfhörer)? Im Configurator von Equalizer APO dieses Gerät ebenfalls anhaken.
- Einstellungen liegen in `%APPDATA%\Wumms\einstellungen.json`.
- Selbst bauen: .NET 8 SDK installieren und `build.bat` doppelklicken.
