# Fortnite Auto Stretch

Kleines Windows-Tool für **1720 × 1080** (oder eine andere vorhandene Auflösung). Es erkennt Fortnite, schaltet den gewählten Monitor um und stellt nach dem Spielen die vorherige Auflösung **inklusive Hz-Zahl** wieder her.

## Schnellstart

1. Repository über **Code → Download ZIP** herunterladen und entpacken.
2. **Install.cmd** doppelklicken. Das Tool wird für deinen Benutzer installiert, startet mit Windows und erhält eine Desktop-Verknüpfung. Keine Administratorrechte nötig.
3. Monitor und Auflösung prüfen. Mit **10 Sek. testen** einen kurzen Test starten; danach wird automatisch zurückgestellt.
4. Fortnite wie gewohnt starten. Alternativ **Fortnite starten** im Tool verwenden: Dann wird die Auflösung bereits vor dem Start über den Epic Games Launcher gesetzt.

Die fertige Anwendung liegt unter `app/FortniteAutoStretch.exe`. Sie kann auch ohne Installation geöffnet werden. **Mit Windows starten** lässt sich im Fenster jederzeit deaktivieren. Das Schließen des Fensters lässt das Tool im Infobereich neben der Uhr weiterlaufen. Rechtsklick auf das Symbol → **Beenden** beendet es und stellt den Desktop wieder her.

## NVIDIA einmalig einrichten

- NVIDIA Control Panel → **Change resolution**: deine benutzerdefinierte Auflösung anlegen und testen, falls sie noch fehlt. Dieses Tool erzeugt keine Monitor-Timings.
- **Adjust desktop size and position** → **Full-screen**, Skalierung auf **GPU**. Bei Bedarf **Override the scaling mode set by games and programs** aktivieren. Diese NVIDIA-Einstellung gilt auch für andere Spiele.
- Falls Windows einen vorhandenen benutzerdefinierten Modus nicht auflistet: **einmal in NVIDIA anwenden**, dann im Tool **Neu laden**. Es merkt sich den aktiven Modus. Danach in NVIDIA den normalen Desktop-Modus wieder anwenden. Das Tool prüft gespeicherte Modi vor der Verwendung erneut mit Windows.
- In Fortnite **Fenster-Vollbild / Windowed Fullscreen** nutzen, damit das Spiel der Desktop-Auflösung folgt. Exklusives Vollbild kann die Auflösung selbst überschreiben. Das Tool erzwingt keine Spieloptionen und verändert keine Fortnite-Dateien.

NVIDIA beschreibt Full-screen als Strecken auf die gesamte Bildschirmfläche: [NVIDIA-Skalierung](https://www.nvidia.com/content/Control-Panel-Help/vLatest/en-gb/mergedProjects/nvdspENG/To_scale_my_desktop_-_Windows_Vista_and_later.htm).

## Verhalten

- Prüft jede Sekunde die Fortnite-Prozessnamen (`FortniteClient-Win64-Shipping`, `_EAC_EOS`, `_BE`). Kein Zugriff auf Spielspeicher, keine Injection, kein Overlay.
- Stellt den Desktop nach 12 Sekunden ohne Fortnite-Prozess zurück; kurze Übergänge beim Spielstart werden toleriert.
- Bei Start über das Tool: maximal 3 Minuten auf Fortnite warten, danach zurückstellen. Updates oder lange Anmeldungen können einen erneuten Start erfordern.
- **Zurückstellen** beendet den aktuellen Stretch-Zustand. Solange der erkannte Spielprozess weiterläuft, wird nicht erneut umgeschaltet.
- Änderungen erfolgen temporär mit Windows `ChangeDisplaySettingsEx`. Der gespeicherte Windows-Standard wird nicht überschrieben.
- Ein separater Wächter versucht die Rückstellung auch nach einem Absturz oder dem Beenden des Hauptprozesses. Falls auch der Wächter beendet wird, erfolgt die Wiederherstellung beim nächsten Tool-Start. Stromausfall, abgezogene Monitore und Treiberfehler können eine sofortige Rückstellung verhindern.
- Einstellungen, gelernte Modi und Rückstellungsdatei: `%LOCALAPPDATA%\FortniteAutoStretch`. Keine Übermittlung dieser Daten.
- Beim Monitorwechsel die Auswahl prüfen und **Neu laden** verwenden. Es wird nur der gewählte Monitor umgestellt.

## Selbst bauen und testen

Windows 10/11 x64 mit .NET Framework 4.x. Keine externen Pakete oder SDK-Downloads nötig.

```powershell
.\build.ps1
.\test.ps1
```

Die Tests prüfen Erkennung, Start-Timeout, Prozessübergänge, manuelles Zurückstellen, Fehlermodi und die native Strukturgröße, ohne den Bildschirm umzuschalten. Diagnose ohne Umschalten:

```powershell
.\dist\FortniteAutoStretch.exe --probe "$env:TEMP\stretch-probe.txt"
```

Die Auswahl verwendet echte Treibermodi. [Microsoft: ChangeDisplaySettingsEx](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-changedisplaysettingsexw) dokumentiert den vorangestellten `CDS_TEST` und temporäre Moduswechsel.

## Entfernen

Im Tool **Mit Windows starten** deaktivieren und über das Tray-Menü beenden. Anschließend `%LOCALAPPDATA%\Programs\FortniteAutoStretch` und die Desktop-Verknüpfung löschen. Einstellungen können optional unter `%LOCALAPPDATA%\FortniteAutoStretch` gelöscht werden. NVIDIA-Skalierung bei Bedarf in der NVIDIA-Systemsteuerung zurücksetzen.

Dieses unabhängige Hilfsprogramm ist kein Produkt von Epic Games oder NVIDIA. Ein vollständiger Fortnite-Spielstart muss auf dem jeweiligen Rechner geprüft werden; die Desktop-Umschaltung allein garantiert keine bestimmte Darstellung im exklusiven Vollbild.

