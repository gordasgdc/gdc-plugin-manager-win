# Structura proiectului — GDC Plugin Manager (Windows)

> Pentru orice sesiune viitoare (Claude sau om): citește asta ÎNAINTE de a
> presupune unde e ceva pe disc. Actualizat ultima dată: 2026-08-25.

## Locația canonică

Toate repo-urile GDC legate de acest ecosistem trăiesc în **`~/Developer/`**
(pe Mac-ul de dezvoltare — clientul Windows se compilează cross-platform de
pe Mac via `dotnet publish -r win-x64`, sau prin CI pe un runner Windows):

```
~/Developer/
├── GDCPluginManager/                    ← sursa Mac (gordasgdc/gdc-plugin-manager)
├── GDCPluginManagerWin/                 ← acest repo (gordasgdc/gdc-plugin-manager-win)
├── gdc-plugin-manager-files/            ← repo PRIVAT: fișierele vandabile
└── gdc-plugin-manager-catalog-vendor/   ← checkout Furnizor pentru catalog.json
```

**Istoric relocare**: până pe 2026-08-25, acest repo stătea în
`~/Downloads/GDCPluginManagerWin` — mutat în `~/Developer/` pentru că
`~/Downloads` e curățat automat de CleanMyMac/Hazel pe acest Mac (a dispărut
în timpul unei sesiuni de lucru, recuperat din Coșul de gunoi la timp).
Acest repo NU are nicio cale hardcodată către `~/Downloads` (verificat) — nu
a fost nevoie de relink de cod aici, doar de mutarea folderului.

## Structura codului

```
src/
├── GDCPluginManager.Core/       ← model de date + logică partajată (port 1:1 din Swift)
│   ├── Models/CatalogModel.cs   ← PluginItem, ServiceCenter, SupportedOS, etc.
│   └── Services/LicenseCore.cs  ← criptografie licențe (Ed25519), payload v1+v2
└── GDCPluginManager.Client/     ← clientul WPF
    ├── MainWindow.xaml          ← toate șabloanele de card din catalog
    ├── ViewModels/CoverViewModel.cs  ← incarcarea coperilor (fix 2026-08-25, vezi mai jos)
    └── ViewModels/ProductViewModel.cs
```

## Fix-uri importante de reținut (2026-08-25)

- **Obfuscarea codului (Obfuscar) e DEZACTIVATĂ definitiv** pe `Core.dll` —
  vezi comentariul din `.github/workflows/build-windows.yml`. Tool-ul
  producea metadate corupte (tipuri duplicate) în două configurații
  diferite, cauzând crash real la pornire pe un client viu. Corectitudinea
  contează mai mult decât obscurizarea. Nu reactiva fără un test extins.
- **Încărcarea coperilor** se face acum direct în `CoverViewModel.cs` (nu
  printr-un `IValueConverter`) — ascultă explicit `DownloadFailed`, cu
  fallback vizual real (`HasImage` reflectă bitmap-ul chiar încărcat, nu
  doar existența unui URL). Vezi comentariul din fișier pentru motivul
  exact al fix-ului.
- **`CatalogAssets.ImageUrl`** escapează explicit fiecare segment de path
  (nu doar se bazează pe combinarea implicită `Uri`) — robustețe pentru
  nume de fișiere cu caractere speciale alese liber de furnizor.

## Build local

```bash
cd ~/Developer/GDCPluginManagerWin
dotnet build src/GDCPluginManager.Client       # build rapid, fără obfuscare/installer
dotnet publish src/GDCPluginManager.Client -c Release -r win-x64 --self-contained -o publish
```

Build-ul de RELEASE (obfuscare Core.dll, instalator Inno Setup, semnare) se
face EXCLUSIV prin CI (`.github/workflows/build-windows.yml`), pe un runner
Windows — nu se poate reproduce integral de pe Mac.

## Repo-uri înrudite

- `gordasgdc/gdc-plugin-manager` → `~/Developer/GDCPluginManager` (sursa Mac,
  vezi `PROJECT_STRUCTURE.md` de acolo)
- `gordasgdc/gdc-plugin-manager-files` (privat) → `~/Developer/gdc-plugin-manager-files`
