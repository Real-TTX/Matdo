# Kalender per OAuth verbinden (Google & Microsoft)

Diese Anleitung richtet die **einmalige App-Registrierung** für die Kalender-Sync
in einer selbst gehosteten Matdo-Instanz ein. Danach verbindet sich **jeder Nutzer**
nur noch per Klick („Mit Google/Microsoft verbinden") – ohne selbst IDs einzugeben.

> **„Muss das sein?"** – Ja, bei self-hosted OAuth ist genau **eine** App-Registrierung
> nötig (so wie jede „Anmelden mit …"-App eine hat; bei SaaS macht das unsichtbar der
> Anbieter). Wer nur **lesend** syncen will, braucht das **nicht** und nutzt stattdessen
> den ICS-Weg (siehe unten „Alternative ohne OAuth").

Die genauen Redirect-URLs zeigt Matdo unter **Admin → Einstellungen → Kalender** an:

```
https://DEINE-DOMAIN/calendar/callback/google
https://DEINE-DOMAIN/calendar/callback/microsoft
```

**Voraussetzungen:** ein **privates** Google- bzw. Microsoft-Konto genügt, **kostenlos**
(keine Kreditkarte, keine Firma). Die Instanz muss über **HTTPS** unter einer festen
Domain erreichbar sein.

---

## Google

### 1 – Projekt anlegen
[console.cloud.google.com](https://console.cloud.google.com) öffnen, anmelden, neues
Projekt anlegen (z. B. `Matdo`). „Google Cloud" ist nur die Verwaltungsoberfläche – kostenlos.

### 2 – Google Calendar API aktivieren
Menü → *APIs & Dienste → Bibliothek* → **Google Calendar API** → **Aktivieren**.

### 3 – OAuth-Zustimmungsbildschirm
Menü → *APIs & Dienste → OAuth-Zustimmungsbildschirm* → Nutzertyp **Extern**. App-Name +
E-Mail ausfüllen, Scope `.../auth/calendar.events` hinzufügen.

> **Wichtig:** Im *Test*-Modus läuft der Refresh-Token nach 7 Tagen ab und nur eingetragene
> Testnutzer funktionieren. Mit **„App veröffentlichen"** (→ Produktion) läuft es dauerhaft
> (bleibt kostenlos; Nutzer bestätigen einmalig eine „nicht überprüft"-Warnseite).

### 4 – OAuth-Client-ID erstellen
Menü → *Anmeldedaten → Anmeldedaten erstellen → OAuth-Client-ID* → Typ **Webanwendung**.
Unter **Autorisierte Weiterleitungs-URIs** exakt `https://DEINE-DOMAIN/calendar/callback/google`
eintragen (https, korrekte Domain, kein End-Slash). Danach **Client-ID** + **Client-Secret** kopieren.

### 5 – In Matdo eintragen (Admin, einmalig)
*Admin → Einstellungen → Kalender → Google*: **Aktivieren**, Client-ID + Secret einfügen, **Speichern**.

### 6 – Als Nutzer verbinden
*Einstellungen → Kalender → „Mit Google verbinden"* → anmelden → fertig (2-Wege-Sync).

### Fehlerbehebung (Google)
| Symptom | Lösung |
|---|---|
| `redirect_uri_mismatch` | Weiterleitungs-URI nicht **exakt** (https / Domain / kein End-Slash). |
| „Zugriff blockiert" | Konto als **Testnutzer** eintragen – oder App auf Produktion veröffentlichen. |
| Bricht nach ~7 Tagen ab | Noch im **Test**-Modus → auf **Produktion** veröffentlichen. |
| „App nicht überprüft" | Bei self-hosted normal → *Erweitert → Trotzdem fortfahren*. |

---

## Microsoft

### 1 – Portal öffnen
[entra.microsoft.com](https://entra.microsoft.com) (oder portal.azure.com) öffnen und mit dem
**privaten Microsoft-Konto** anmelden. Kostenlos.

### 2 – App registrieren
*Microsoft Entra ID → App-Registrierungen → Neue Registrierung*. Name `Matdo`.

> **Entscheidend für private Konten:** Bei **Unterstützte Kontotypen** unbedingt
> **„Konten in einem beliebigen Organisationsverzeichnis und persönliche Microsoft-Konten"**
> wählen. Matdo nutzt den `common`-Endpunkt – private outlook.com/hotmail/live-Konten
> funktionieren nur mit dieser Option.

### 3 – Redirect-URI
Plattform **Web** hinzufügen mit `https://DEINE-DOMAIN/calendar/callback/microsoft` (exakt).

### 4 – Clientschlüssel (Secret)
*Zertifikate & Geheimnisse → Neuer geheimer Clientschlüssel* → sofort den **Wert** kopieren
(**nicht** die Geheimnis-ID!). Hinweis: Secrets **laufen ab** (max. 24 Monate) und müssen
rechtzeitig erneuert werden.

### 5 – API-Berechtigungen
*API-Berechtigungen → Hinzufügen → Microsoft Graph → Delegierte Berechtigungen* →
`Calendars.ReadWrite`, `offline_access`, `openid`, `email` hinzufügen. Für private Konten ist
keine Administratoreinwilligung nötig.

### 6 – In Matdo eintragen + verbinden
*Admin → Einstellungen → Kalender → Microsoft*: **Aktivieren**, **Anwendungs-/Client-ID**
(aus der Registrierungsübersicht) + Secret-**Wert** einfügen, **Speichern**. Dann als Nutzer:
*Einstellungen → Kalender → „Mit Microsoft verbinden"*.

### Fehlerbehebung (Microsoft)
| Symptom | Lösung |
|---|---|
| `unauthorized_client` / „not supported for this application" | Kontotyp schließt keine persönlichen Konten ein (Schritt 2). |
| `redirect_uri_mismatch` | URI nicht exakt oder Plattform nicht **Web**. |
| Secret wird abgelehnt | Du hast die **Geheimnis-ID** statt des **Werts** kopiert → neuen Schlüssel erstellen, Wert nehmen. |
| Funktioniert später nicht mehr | Das Client-Secret ist **abgelaufen** → neues erstellen und in Matdo eintragen. |

---

## Alternative ohne OAuth (nur lesend, sofort)

Wenn dir **lesender** Sync reicht, brauchst du **nichts** von oben:

- **Matdo → Google/Outlook**: In Matdo unter *Einstellungen → Kalender-Feed* die iCal-URL
  kopieren und im Kalender als Abo „per URL" einfügen. (Aktualisiert verzögert, nur lesend.)
- **Google/Outlook → Matdo**: Die *„Geheime Adresse im iCal-Format"* aus dem Kalender kopieren
  und in Matdo als **ICS-Kalender** hinzufügen.

Der Unterschied zu OAuth: ICS ist **nur lesend** und langsamer; OAuth bietet **echtes 2-Wege**
und schnellere Aktualisierung.
