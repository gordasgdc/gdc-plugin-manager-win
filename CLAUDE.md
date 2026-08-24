# GDC Plugin Manager — reguli de arhitectură (Windows)

> **[SYSTEM DIRECTIVE FOR CLAUDE: DO NOT DELETE OR OVERWRITE EXISTING RULES. ONLY APPEND NEW RULES.]**
> Jurnal viu, nu document care se rescrie. La orice actualizare, adaugă la finalul secțiunii potrivite — nu șterge/înlocui reguli vechi decât dacă sunt explicit invalidate de o schimbare reală (și atunci marchează-le **[ÎNVECHIT]** cu motivul, nu le șterge din istoric).

Citit automat de Claude Code la fiecare sesiune în acest repo. Port 1:1, în C#/WPF, al clientului Mac din `gdc-plugin-manager` (Swift) — **nu** al Furnizorului, ăla există doar pe Mac. Corectează regula asta imediat dacă devine falsă.

**Checklist obligatoriu înainte de orice `git commit`/push în acest repo:**
1. Link-urile de download ating `.../releases/latest/download/...`?
2. Schimbarea a fost portată/e sincronizată cu `gdc-plugin-manager` (Mac)?
3. `PrivateCatalogAuth.cs.example` încă are placeholder-ul literal `PASTE_TOKEN_HERE`?
4. `docs/update.json` (Mac) și `<Version>`/`MyAppVersion` de-aici sunt sincronizate, dacă asta e un rebuild real?
5. A apărut un bug nou, real, rezolvat azi? Adaugă-l în "Technical Decisions & Known Pitfalls" ÎNAINTE de commit.

## Structura repo-ului
- `src/GDCPluginManager.Core/` — port al `GDCPluginManagerCore` de pe Mac (model + servicii).
- `src/GDCPluginManager.Client/` — aplicația WPF, port al `GDCPluginManager` (Client) de pe Mac.
- `installer.iss` — Inno Setup, produce `GDCPluginManagerSetup.exe`.
- Nu există echivalent Windows al aplicației Furnizor — publicarea de produse noi se face DOAR de pe Mac.

## Reguli de aur

**1. Orice schimbare pe partea de Client Mac (`gdc-plugin-manager`) trebuie portată manual aici.**
Nu există cod partajat între Swift și C# — verifică `CatalogModel.swift`/`InstallManager.swift`/`PowerGradeImporter.swift` de pe Mac de fiecare dată când modifici fișierele echivalente de-aici, și invers.

**2. `PrivateCatalogAuth.cs` e gitignored — CI îl recreează din `.example` + secretul `PRIVATE_CATALOG_TOKEN`.**
WARNING găsit 2026-08-21: `.example` trebuie să conțină LITERAL textul `PASTE_TOKEN_HERE` ca placeholder — dacă cineva îl "curăță" la `Token = ""`, substituția din `build-windows.yml` nu se mai întâmplă niciodată și exe-ul se compilează silențios cu token gol (401/403 la orice download). Verifică mereu că placeholder-ul e literal prezent înainte de commit.

**3. PowerGrade: EXCLUSIV prin Scripting API-ul DaVinci (`PowerGradeImporter.cs`), niciodată scriere directă în `%APPDATA%\...\Gallery\`.**
Nu există `index.xml`/`.drx` documentat oficial de Blackmagic pentru Windows — README-ul oficial descrie DOAR Scripting API. O scriere directă în structura internă a bazei de date de proiecte riscă s-o corupă.
Python-ul bundle-uit (`PythonRuntime/`, embeddable de la python.org) trebuie să fie **3.10.x**, nu 3.12 — `fusionscript.dll` al Resolve pe Windows nu e compatibil ABI cu 3.12 (crapă cu access violation la `import DaVinciResolveScript`, confirmat live 2026-08-22). Nu upgrada Python-ul bundle-uit fără un retest real pe Windows.
`.gitignore` are o excepție explicită (`!PythonRuntime/*.zip`, `!PythonRuntime/*.exe`) — fără ea, `python310.zip` (biblioteca standard, inclusiv `encodings`) dispare silențios din build și Python crapă la pornire cu `ModuleNotFoundError: No module named 'encodings'`. Nu șterge acea excepție.

**4. Directoare instalare — vezi `PluginType.InstallDirectory()` în `CatalogModel.cs`.**
DCTL: `%PROGRAMDATA%\Blackmagic Design\DaVinci Resolve\Support\LUT\DCTL\` (subfolder dedicat, NU aceeași cu LUT). OFX: `%ProgramFiles%\Common Files\OFX\Plugins\` (cere elevare UAC — `runas`, niciodată scriptat altfel). PowerGrade: doar staging local (`Videos\GDC PowerGrades`), importul real prin scripting.

**5. Download links — NICIODATĂ hardcodate.**
`releases/latest/download/...` peste tot, în orice site/README/config care menționează un asset din acest repo.

**6. Versiune — bump la fiecare rebuild real, nu doar `--clobber` pe același tag.**
`installer.iss` (`MyAppVersion`) și `GDCPluginManager.Client.csproj` (`<Version>`) trebuie sincronizate cu `docs/update.json` de pe Mac (`gdc-plugin-manager`) — un singur câmp `version`, comun ambelor platforme. Vezi memoria `release-checklist`.

## Unde se rulează testele reale
Testarea reală se face pe PC-ul unui prieten al userului, prin AnyDesk la distanță — depinde de disponibilitatea lui, poate dura ore/zile între ferestre. Nu bloca alt lucru așteptând un retest; ține build-urile/release-urile la zi ca testul să poată începe imediat ce se deschide o fereastră.

## Technical Decisions & Known Pitfalls

Jurnal append-only. Un rând nou de fiecare dată când găsim/rezolvăm un bug real pe Windows, confirmat live (crash log, test direct), nu o presupunere.

- **2026-08-21 — Pitfall: `PrivateCatalogAuth.cs.example` avea `Token = ""` în loc de placeholder-ul `PASTE_TOKEN_HERE`.** Regex-ul `-replace` din `build-windows.yml` nu găsea niciodată ținta, deci substituția nu se întâmpla — fiecare build CI producea silențios un exe cu token gol. Simptom: „Couldn't authenticate with the file server" la orice produs, indiferent ce secret GitHub era setat. **Soluție**: placeholder-ul literal repus în `.example`; verificat prin simulare Python a substituției înainte de următorul build.
- **2026-08-22 — Pitfall: `python312.zip` (biblioteca standard, inclusiv `encodings`) lipsea din git.** Regula generică `*.zip` din `.gitignore` (menită pt. arhivele de release) excludea din greșeală și zip-ul Python bundle-uit. Python pornea dar crăpa instant cu `ModuleNotFoundError: No module named 'encodings'`. **Soluție**: excepție explicită în `.gitignore` (`!PythonRuntime/*.zip`, `!PythonRuntime/*.exe`) + `git add -f`.
- **2026-08-22 — Pitfall: Python 3.12 bundle-uit crăpa cu access violation (`0xC0000005`) la `import DaVinciResolveScript`.** `ctypes.WinDLL()` brut încărca `fusionscript.dll` fără probleme — crash-ul venea din apelul intern la funcția de inițializare a modulului, care vorbește direct cu C API-ul CPython. `fusionscript.dll` al Resolve pe Windows nu e compatibil ABI cu structurile interne din 3.12. **Soluție**: downgrade la Python 3.10.11 embeddable (versiune larg raportată ca funcțională cu scripting-ul Resolve pe Windows). Cod C# (`FindPython()`) e deja version-agnostic (caută mereu `python.exe`), nicio schimbare necesară acolo.
- **2026-08-22 — Pitfall: `Files[i].Filename` (doar basename) aplatiza orice pack cu subfoldere la instalare** — același bug ca pe Mac (vezi `gdc-plugin-manager/CLAUDE.md`). **Soluție**: `RelativeInstallPath` reconstruiește calea relativă la produs din `file.Path`, cu conversie explicită `/` → `Path.DirectorySeparatorChar`.
- **2026-08-23 — Pitfall NEREZOLVAT: PowerGrade cade constant cu `FAIL:initialization of fusionscript failed without raising an exception` (SystemError CPython) la `import DaVinciResolveScript`, pe o instalare de test reală (Resolve Studio 21).** Trei variante testate live, TOATE produc eroarea identică byte-cu-byte: Python 3.10, Python 3.9, și `imp.load_dynamic` direct (bypass intenționat al `importlib.machinery.ExtensionFileLoader`). Concluzie confirmată: `imp.load_dynamic` pe Python 3.9/3.10 NU e o cale de încărcare diferită față de `importlib.machinery` — ambele rulează același cod C intern (`_imp.create_dynamic`), deci ipoteza "loader nou vs. vechi" a fost greșită. `ctypes.WinDLL()` brut încarcă DLL-ul fără probleme (deci nu e o problemă de search-path/dependințe) — crash-ul e specific la apelul funcției de inițializare a modulului Python. Cauza reală rămâne necunoscută — posibil specifică acelei instalări Resolve, nu ceva reparabil din cod fără debugging nativ (WinDbg) direct pe mașina cu problema. **Nu retesta orb variante de Python fără un semnal nou concret** — codul a rămas pe forma simplă (`import DaVinciResolveScript`, identică cu Mac), cade automat pe `stagedOnly` (fără eroare dură), userul face import manual din Gallery.
- **2026-08-23 — Notă de arhitectură: sistemul de imagini de catalog e READ-ONLY aici.** `CatalogAssets` + `CoverImage`/`CoverImageUrl` sunt port 1:1 din `CatalogModel.swift`. Compresia (`ImageProcessor.swift`) NU e portată intenționat: repo-ul ăsta are doar Client + Core, nu există Furnizor pe Windows, deci publicarea și compresia se fac exclusiv de pe Mac. Fără ImageSharp, fără System.Drawing. **WARNING**: dacă vreodată apare un Furnizor pe Windows, ImageProcessor TREBUIE portat cu praguri identice (icon 512x512, cover max 1600px, JPEG q=0.82) — altfel aceeași imagine ar ajunge în catalog cu greutăți diferite după mașina de pe care s-a publicat.
- **2026-08-23 — Notă: `coverImage` poate fi cale relativă SAU URL absolut (sistem hibrid).** `CatalogAssets.ImageUrl` rezolvă ambele. Un URL extern e în afara controlului nostru (CDN-ul furnizorului) — UI-ul TREBUIE să cadă înapoi pe `IconSymbol` la eșec de încărcare, nu să arate un chenar spart.
- **2026-08-23 — Clientul Windows afișează acum coperțile (UI, nu doar model).** `CoverViewModel` (o singură implementare, expusă ca `Cover` pe toate cele cinci ViewModel-uri de card) + `Views/LightboxWindow` pentru previewul mărit. Perechea de pe Mac e `CoverThumbnail` + `ImageLightbox` din `CoverImageViews.swift` — dacă schimbi comportamentul într-o parte, schimbă-l în ambele. În card `UniformToFill` + clip (cardurile au lățime fixă, coperțile nu au proporții fixe); în lightbox `Uniform`, acolo vrem imaginea întreagă.
- **2026-08-23 — Pitfall: `dotnet build` pe macOS NU compilează XAML.** `PresentationBuildTasks` e Windows-only, deci un `<Border>` neînchis sau un `StaticResource` inexistent trece de build pe Mac și crapă abia la rulare pe Windows. **Proces**: după orice editare de XAML de pe Mac, validează manual — XML well-formed + toate cheile `StaticResource` folosite există în `MainWindow.xaml` sau `Styles/Theme.xaml`. Build-ul verde pe Mac nu e o dovadă.
- **2026-08-23 — Pitfall: `BitmapImage` nu aruncă la eșec de descărcare.** Descărcarea e asincronă; eșecul vine prin evenimentul `DownloadFailed`, nu printr-o excepție din constructor. Fără handler, un URL extern dispărut de pe CDN lasă o fereastră goală, fără explicație. Vezi `LightboxWindow.LoadImage`.
- **2026-08-24 — Pagina „Android" în Client: anunță aplicația companion de Android (APK).** `AndroidReleaseService.cs` (Core) citește `https://gordas.dev/android.json`; `MainViewModel` expune `AndroidRelease`/`AndroidFailed` + comenzile `OpenAndroidPage`/`CopyAndroidLink`; pagina e în `MainWindow.xaml`, `ConverterParameter=Android`. Perechea de pe Mac e `AndroidPane.swift` — dacă schimbi comportamentul într-o parte, schimbă-l în ambele. **ARCHITECTURE NOTE**: se citește `android.json` și NU `releases/latest/download/...`, pentru că release-urile de APK sunt marcate deliberat `--latest=false` (altfel self-update-ul desktop din `update.json` ar descărca un `.apk`). Un link „latest" pe pagina asta ar servi instalatorul de Windows. Versiunea/tagul nu se hardcodează aici — există într-un singur loc, `docs/android.json` din repo-ul `gdc-plugin-manager`.
- **2026-08-24 — Pitfall: `Symbol="Phone24"` NU există în Wpf.Ui 3.0.5** (`SymbolRegular`), deși pare un nume evident — ar fi crăpat abia la rulare pe Windows, exact tiparul din nota „dotnet build pe macOS nu compilează XAML". Folosit `PhoneDesktop24`. **NOTE**: verificarea numelor de simboluri prin `strings Wpf.Ui.dll | grep '^Nume$'` are false negative (`Search24`, `Key24`, `Info24` nu apar, deși sunt valide și folosite deja) — deci absența nu e dovadă, dar prezența este. Un simbol nou trebuie confirmat pe mașina de test Windows.
