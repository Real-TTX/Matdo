# Google-Kalender per OAuth verbinden (Anleitung)

Diese Anleitung richtet die **einmalige App-Registrierung** für die Google-Kalender-Sync
in einer selbst gehosteten Matdo-Instanz ein. Danach verbindet sich **jeder Nutzer**
nur noch per Klick („Mit Google verbinden") – ohne selbst irgendwelche IDs einzugeben.

> **Kurzfassung der Frage „muss das sein?"**
> Ja – bei self-hosted OAuth ist genau **eine** App-Registrierung nötig (so wie jede
> „Mit Google anmelden"-App eine hat; bei SaaS macht das unsichtbar der Anbieter).
> Wer nur **lesend** syncen will, braucht das **nicht** und nutzt stattdessen den
> ICS-Weg (siehe unten „Alternative ohne OAuth").

---

## Voraussetzungen

- Ein **ganz normales privates Google-Konto** (Gmail) genügt.
- **Kostenlos** – keine Kreditkarte, kein Abo, keine Firma, kein Google Workspace.
- Deine Matdo-Instanz muss über **HTTPS** unter einer festen Domain erreichbar sein
  (die Redirect-URL muss exakt passen).

Die genaue Redirect-URL zeigt Matdo dir an unter
**Admin → Einstellungen → Kalender** – sie lautet:

```
https://DEINE-DOMAIN/calendar/callback/google
```

---

## Schritt 1 – Projekt in der Google Cloud Console anlegen

1. Öffne **https://console.cloud.google.com** und melde dich mit deinem privaten
   Google-Konto an.
2. Oben in der Projektauswahl **„Neues Projekt"** → Name z. B. `Matdo` → **Erstellen**.
3. Warte kurz, bis das Projekt oben ausgewählt ist.

> „Google Cloud Console" klingt nach Enterprise – ist aber nur Googles
> Verwaltungsoberfläche. Das Anlegen und die Kalender-API kosten im normalen
> Rahmen **nichts**.

## Schritt 2 – Google Calendar API aktivieren

1. Menü (☰) → **APIs & Dienste → Bibliothek**.
2. Nach **„Google Calendar API"** suchen → öffnen → **Aktivieren**.

## Schritt 3 – OAuth-Zustimmungsbildschirm konfigurieren

1. Menü → **APIs & Dienste → OAuth-Zustimmungsbildschirm**.
2. Nutzertyp **„Extern"** wählen → **Erstellen**.
3. Pflichtfelder ausfüllen:
   - **App-Name**: z. B. `Matdo`
   - **Support-E-Mail**: deine Adresse
   - **Entwickler-Kontakt-E-Mail**: deine Adresse
4. **Bereiche/Scopes**: „Hinzufügen" → den Scope
   `.../auth/calendar.events` suchen und aufnehmen (Matdo fordert genau diesen an,
   plus `openid` und `email`).
5. **Testnutzer**: alle Personen eintragen, die den Sync nutzen sollen
   (im Testmodus max. 100).
6. Speichern.

### Wichtig: Testmodus vs. Veröffentlicht
- **Testmodus** (Standard): funktioniert sofort, **aber** nur für eingetragene
  Testnutzer, und **der Refresh-Token läuft nach 7 Tagen ab** → man müsste sich
  ständig neu verbinden.
- Damit es dauerhaft läuft: auf dem Zustimmungsbildschirm **„App veröffentlichen"**
  (→ „In Produktion"). Bleibt **kostenlos**. Da die App nicht von Google verifiziert
  ist, erscheint beim Verbinden **einmal** eine Warnseite
  „Diese App wurde nicht von Google überprüft" → **Erweitert → Trotzdem fortfahren**.
  Für eine private/Familien-Instanz ist das unbedenklich.

## Schritt 4 – OAuth-Client-ID erstellen

1. Menü → **APIs & Dienste → Anmeldedaten**.
2. **„Anmeldedaten erstellen" → „OAuth-Client-ID"**.
3. **Anwendungstyp: „Webanwendung"**.
4. **Autorisierte Weiterleitungs-URIs → „URI hinzufügen"** und **exakt** eintragen:
   ```
   https://DEINE-DOMAIN/calendar/callback/google
   ```
   (genau die URL aus Admin → Einstellungen → Kalender; **https**, **kein**
   Schrägstrich am Ende, exakte Groß-/Kleinschreibung der Domain).
5. **Erstellen**. Du erhältst **Client-ID** und **Client-Schlüssel (Secret)** –
   beides gleich kopieren.

## Schritt 5 – In Matdo eintragen (als Admin, einmalig)

1. In Matdo: **Admin → Einstellungen → Kalender → Google**.
2. **Aktivieren** ankreuzen.
3. **Client-ID** und **Client-Secret** einfügen → **Speichern**.

## Schritt 6 – Als Nutzer verbinden (jeder selbst, per Klick)

1. **Einstellungen → Kalender → „Mit Google verbinden"**.
2. Google-Login + Zustimmung (ggf. einmal die Warnseite bestätigen).
3. Fertig – ab jetzt synchronisiert Matdo den Kalender dieses Nutzers
   (2-Wege: Termine rein, Aufgaben mit Fälligkeit als Termine raus).

---

## Fehlerbehebung

| Meldung / Symptom | Ursache & Lösung |
|---|---|
| `redirect_uri_mismatch` | Die Weiterleitungs-URI in Google stimmt nicht **exakt** mit `https://DEINE-DOMAIN/calendar/callback/google` überein. Auf `https`, Domain-Schreibweise und **keinen** End-Slash achten. |
| „Zugriff blockiert: … nicht abgeschlossen" | Dein Konto ist nicht als **Testnutzer** eingetragen (Schritt 3.5) – oder App auf „Produktion" veröffentlichen. |
| Verbindung bricht nach ~7 Tagen ab | App hängt im **Testmodus** → auf **„Produktion" veröffentlichen** (Schritt 3). |
| „Diese App wurde nicht überprüft" | Normal bei unverifizierter self-hosted App → **Erweitert → Trotzdem fortfahren**. Optional bei Google verifizieren lassen (nur für große/öffentliche Instanzen sinnvoll). |

---

## Alternative ohne OAuth (nur lesend, sofort)

Wenn dir **lesender** Sync reicht, brauchst du **nichts** von oben:

- **Matdo → Google**: In Matdo unter *Einstellungen → Kalender-Feed* die iCal-URL
  kopieren und in **Google Kalender → Weitere Kalender (+) → Per URL** einfügen.
  (Google aktualisiert langsam, ~8–24 h, in Google nur lesend.)
- **Google → Matdo**: In Google Kalender die *„Geheime Adresse im iCal-Format"*
  kopieren und in Matdo als **ICS-Kalender** hinzufügen.

Der Unterschied zu OAuth: ICS ist **nur lesend** und langsamer; OAuth bietet
**echtes 2-Wege** und schnellere Aktualisierung.
