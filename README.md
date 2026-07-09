# Matdo

Eine selbst gehostete Aufgaben-/Todo-Web-App (inkl. **PWA** für App-Feeling auf Android & iOS).
Matdo ist als kleine „Mini-Plattform“ gedacht: Benutzerverwaltung, Rollen, Projekte,
Kanban-Boards, Etiketten, Erinnerungen und das Teilen von Aufgaben mit Familie & Freunden.

> Alternative zu inzwischen kostenpflichtigen Todo-Apps – im Look an Todoist angelehnt.

## Features

- ✅ **Aufgabenverwaltung** mit Unteraufgaben (Schritten)
- 📅 **Fälligkeit** & **Deadline** (jeweils mit optionaler Uhrzeit)
- ⏰ **Erinnerungen** – fester Zeitpunkt oder „vor der Fälligkeit“, per **E-Mail** und/oder **Browser-Push**
- 🏷️ **Etiketten** (Tags) & Favoriten
- 📁 **Projekte** mit **Listen-** und **Kanban-Ansicht** (frei definierbare Spalten, Drag & Drop)
- 👀 Ansichten **Heute**, **Demnächst**, **Eingang**, **Suche**, **Reporting**
- 🤝 **Teilen** von Aufgaben und Projekten mit anderen Benutzern
- 👥 **Benutzer-, Gruppen- und Rollenverwaltung** (Admin-Bereich)
- 🔐 Lokale Anmeldung (E-Mail/Passwort), **persistente Sessions** (überstehen Container-Neustarts)
- 📱 **PWA** installierbar, inkl. Icons/Favicon für iOS & Android, Offline-Fallback

## Technologie

| Bereich        | Wahl                                             |
|----------------|--------------------------------------------------|
| Backend        | C# / ASP.NET Core **10.0** (Razor Pages)         |
| Frontend       | Razor + Vanilla-JavaScript (keine JS-Frameworks) |
| Datenbank      | **PostgreSQL** (Logik) + JSON (Konfiguration)    |
| ORM            | EF Core 10 (Npgsql)                              |
| Container      | Docker / Docker Compose (dev & release)          |
| Port           | **6006**                                         |

Wiederverwendbare UI-Controls (mit Code-Basis) als Tag-Helper: `list-view`,
`list-toolbar`, `list-table`, `list-actions`, `pagination`, `tab-bar`/`tab-item`,
`form-field`, `form-buttons`, `icon`.

## Schnellstart

Voraussetzung: Docker Desktop.

```powershell
# Windows / PowerShell – lokaler Build + Deploy
./build.ps1
```

```bash
# Linux/macOS/Git-Bash
./build.sh
```

Danach im Browser öffnen: <http://localhost:6006>

Der **erste registrierte Benutzer** wird automatisch **Administrator**.

### Manuell mit Docker Compose

```bash
# Entwicklung
docker compose up -d --build

# Release/Produktion (Passwort setzen!)
POSTGRES_PASSWORD=... docker compose -f docker-compose.yml -f docker-compose.release.yml up -d --build
```

## Versionierung

| Kanal    | Schema                               | Beispiel            |
|----------|--------------------------------------|---------------------|
| Release  | `<major>.<minor>.<build>-<datum>`    | `1.0.7-20260710`    |
| Nightly  | `nightly-<build>-<datum>`            | `nightly-7-20260710`|
| Local    | `local-<datum>`                      | `local-20260710`    |

Die Version wird beim Build als `MATDO_VERSION` gesetzt und in der App (Seitenleiste) angezeigt.

## Daten & Persistenz

- **PostgreSQL** liegt im Volume `matdo_pgdata`.
- **JSON-Konfiguration** (SMTP, Web-Push) liegt im Volume `matdo_config` unter `/data/config`.
- Sessions liegen in der Datenbank → Anmeldung bleibt auch nach `docker compose restart` erhalten.

## E-Mail & Push einrichten

Als Administrator unter **Administration → System-Einstellungen**:

- **SMTP** aktivieren und Host/Port/Zugangsdaten hinterlegen.
- **Web-Push**: VAPID-Schlüssel erzeugen lassen; Benutzer aktivieren Push unter *Einstellungen → Benachrichtigungen*.

## Projektstruktur

```
Matdo/
├─ src/Matdo.Web/            ASP.NET Core Razor-Pages-App
│  ├─ Data/                  EF-Core Kontext, Entitäten, Migrationen
│  ├─ Services/              Geschäftslogik (Auth, Tasks, Projects, ...)
│  ├─ TagHelpers/            Wiederverwendbare UI-Controls
│  ├─ Pages/                 Razor Pages (Tasks, Projects, Labels, Admin, ...)
│  ├─ Controllers/           API-Endpunkte (AJAX, Push)
│  └─ wwwroot/               CSS, JS, Icons, PWA-Manifest, Service Worker
├─ Dockerfile
├─ docker-compose.yml / docker-compose.release.yml
├─ build.ps1 / build.sh
└─ build/                    Versions-/Build-Nummern
```

## Datenbank-Konventionen

- Tabellen **PascalCase**, Primärschlüssel immer `Id` (BIGINT).
- Sicherheitsrelevante Schlüssel als `Token` (UUID), z. B. `UserSession`.
- Jeder Datensatz führt `CreateDate`, `CreateUserId`, `UpdateDate`, `UpdateUserId`.
