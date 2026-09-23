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

## [PARTEA 1: REGULI GLOBALE ECOSISTEM GDC] — mutată în `~/Developer/CLAUDE.md`

> Din 2026-09-18, regulile globale stau într-un singur fișier,
> `~/Developer/CLAUDE.md`, citit automat de Claude Code în orice proiect din
> `~/Developer/`. Nu se mai copiază aici. Ce era specific acestui repo în fosta
> Partea 1 (statusuri, excepții) e la finalul fișierului.

## [PARTEA 2: SPECIFICAȚII TEHNICE PROIECT]

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

## REGULĂ PERMANENTĂ: Locația proiectelor pe disc (2026-08-25)
Acest repo (și toate cele înrudite: `gdc-plugin-manager`,
`gdc-plugin-manager-files`, `gdc-plugin-manager-catalog-vendor`) trăiesc în
**`~/Developer/`** pe Mac-ul de dezvoltare, NU în `~/Downloads`/`~/Desktop`.
Motiv real: `~/Downloads` e curățat automat de CleanMyMac/Hazel — au
dispărut ambele repo-uri de sursă în timpul unei sesiuni (recuperate din Coș
la timp). Vezi `PROJECT_STRUCTURE.md` pentru harta completă.

## Unde se rulează testele reale
Testarea reală se face pe PC-ul unui prieten al userului, prin AnyDesk la distanță — depinde de disponibilitatea lui, poate dura ore/zile între ferestre. Nu bloca alt lucru așteptând un retest; ține build-urile/release-urile la zi ca testul să poată începe imediat ce se deschide o fereastră.

## v1.29.0 (2026-09-05) — Evenimente multi-locație, sedii suplimentare (port 1:1 Mac)

Port 1:1 al Client v1.29.0 de pe Mac (`gdc-plugin-manager-catalog-vendor`)
— vezi CLAUDE.md de acolo pentru context complet. `CatalogModel.cs`:
record nou `EventOccurrence` (Location/DateDisplay libere, PriceEUR/
PriceLabel opționale) + `Event.Occurrences`, plus `ServiceCenter.
AdditionalAddresses`/`PartnerStore.AdditionalAddresses` — toate
`IReadOnlyList<T>` cu default `Array.Empty<T>()` (System.Text.Json lasă
implicit valoarea declarată când cheia lipsește din JSON — retrocompatibil
automat, fără niciun converter custom, spre deosebire de portul Swift).

`EventViewModel.cs` — `Occurrences: IReadOnlyList<EventOccurrenceViewModel>`
(nou, în același fișier). `AddressLinkViewModel.cs` (nou, reutilizat de
`PartnerStoreViewModel`/`ServiceCenterViewModel`) — un rând cu buton hartă
propriu per adresă suplimentară. `MainWindow.xaml` — câte un `ItemsControl`
nou în cele 3 `DataTemplate` (Event/PartnerStore/ServiceCenter).

**Verificat**: `dotnet build src/GDCPluginManager.Client/
GDCPluginManager.Client.csproj -r win-x64` — 0 erori (Core+Client, XAML→BAML
inclus). Decoder de test separat, rulat REAL pe `docs/catalog.json` de
producție (copiat de pe Mac) + round-trip encode/decode + JSON vechi
("fără cheia nouă deloc") construit manual — toate corecte, identic ca
acoperire cu testul echivalent de pe Mac.

## v1.24.0 (2026-08-31) — Valabilitate temporala pentru banner

Port 1:1 al Mac v1.24.0: `LaunchBannerConfig.Scheduling` (nou, nullable) -
`IsDisplayable` verifica acum si `Scheduling?.IsActiveNow ?? true`. Niciun
cod nou pe Windows in afara acestui camp - Furnizorul (Mac) e singurul loc
care scrie `launch-banner.json`.

**Verificat**: `dotnet build src/GDCPluginManager.Client/GDCPluginManager.Client.csproj -r win-x64` - 0 erori.

## v1.23.0 (2026-08-31) — Banner de lansare, controlabil din Furnizor

Port 1:1 al arhitecturii de pe Mac (`gdc-plugin-manager-catalog-vendor`,
`LaunchBannerModel.swift`/`LaunchBannerChecker.swift`/Furnizor):
- `LaunchBannerModel.cs` (Core, nou) - `LaunchBannerConfig` record, aceleasi
  campuri (Enabled/ImagePath/TopText/MainText).
- `Services/LaunchBannerChecker.cs` (Client, nou) - fetch `gordas.dev/
  launch-banner.json` + retry + cache local pe disc (offline-first),
  imaginea decodata direct din `MemoryStream` (NU `BitmapImage.UriSource` -
  vezi bug-ul critic deja documentat mai jos in acest fisier despre
  WinINet).
- `MainWindow.xaml`/`.xaml.cs` - `Grid` nou, ancorat jos, `Collapsed`
  implicit pana la primul fetch reusit cu `IsDisplayable == true`.
- Furnizorul (Mac) e SINGURUL loc care scrie `launch-banner.json` - Windows
  nu are (si nu are nevoie de) o interfata de publicare, la fel ca restul
  catalogului.

**Verificat**: `dotnet build src/GDCPluginManager.Client/GDCPluginManager.Client.csproj -r win-x64` - 0 erori, XAML->BAML inclus.

## Technical Decisions & Known Pitfalls


## Jurnal tehnic detaliat — arhivat

Pitfall-urile istorice + jurnalul complet al celor 9 etape de paritate v2.0
(căutare fuzzy, social links, "Aplicațiile Mele", scheduling, Maps, filigran
SVG, cache offline, pachete) + Fazele 3/4 (Profil/HWID, Update popup) sunt
mutate în `CLAUDE_ARCHIVE.md` (NU se citește automat) — citește-l explicit
când investighezi o zonă veche de cod. Rezumat "stare curentă" mai jos rămâne
în acest fișier, fiindcă e activ relevant sesiune de sesiune.

## v1.21.0 (2026-08-31) — Ceas live opțional (countdown) + fix 404 self-update

**Bug real, raportat live**: self-update pe Windows dădea `HTTP 404` la
descărcare — `update.json` (comun Mac+Windows) fusese bumpat la `1.21.0`
(countdown pe Mac), dar clientul Windows local rămăsese la `1.20.0`, fără
build/release publicat pentru acea versiune — exact riscul deja documentat
în arhivă ("update.json in avans fata de ce e chiar descarcabil"),
materializat din nou.

**Port countdown (Core + Client)**:
- `Scheduling.ShowCountdown` (bool, `CatalogModel.cs`) + `CountdownText`
  computed — identic cu Mac (`Scheduling.swift`), fără secunde.
- `CountdownRefreshTimer` (nou, `Services/`) — un singur `DispatcherTimer`
  static de 60s, partajat de toate viewmodel-urile de card (evită câte un
  timer per card). Fiecare din cele 10 ViewModel-uri `ObservableObject`
  (Product/DownloadResource/PartnerOffer/Bundle/PartnerStore/ServiceCenter/
  AppLink/AudioTrack/EducationalResource/Event) se aboneaza la `Tick` in
  constructor si expune `CountdownText => <Model>.Scheduling?.CountdownText`.
  **Excepție**: `CourseViewModel` e o clasă simplă (nu `ObservableObject`,
  fără `INotifyPropertyChanged`) — are `CountdownText` computed, dar nu se
  reabonează la tick (badge-ul se calculeaza o singura data, la afisare;
  degradare minora acceptata, nu a fost convertit la ObservableObject doar
  pentru asta).
- `MainWindow.xaml` — badge portocaliu (`#FFB05B00`, icon `Timer24`) adăugat
  în toate cele 11 `DataTemplate` de card: pe cele cu `StackPanel` rădăcină,
  ca prim element vizibil (deasupra copertei); pe cele cu `Grid` rădăcină
  (AppLink/AudioTrack/EducationalResource/Event, care deja foloseau Grid ca
  overlay pentru butonul de tutorial), ca overlay separat, colț stânga-sus.
  Vizibilitate prin `NullToVisibility` (deja existent, reutilizat).

**Fix definitiv al 404**: bump `<Version>` (`.csproj`) și `MyAppVersion`
(`installer.iss`) la `1.21.0`, apoi build CI + upload
`GDCPluginManager-Windows.zip` în release-ul `v1.21.0` de pe
`gdc-plugin-manager` (Mac) — vezi checklist-ul de proces din arhivă:
bump de `update.json` și publicarea reală a binarelor NU sunt opționale
una față de cealaltă, în ACEEAȘI sesiune.

**Verificat**: `dotnet build GDCPluginManagerWin.slnx` — 0 erori (Core +
Client, XAML→BAML inclus, cross-compilat pe Mac).

## Stare curentă (2026-08-29) — versiune `1.19.10`

- **Bug critic imagini rezolvat**: `BitmapImage.UriSource` (WinINet) trecut
  peste tot pe `HttpClient`+`MemoryStream` — coperți/lightbox funcționale,
  confirmat de Cristi după reinstall v1.19.7.
- **Filigran sezonier — TOT NEREZOLVAT, în lucru activ**:
  `RemoteCertificateNameMismatch`/`RemoteCertificateChainErrors`, DOAR pe
  `HttpClient` (niciodată pe `curl`, 10/10 OK). Ipoteza de ceas VM:
  INFIRMATĂ. **v1.19.9** (`PooledConnectionLifetime=5min`) NU a rezolvat —
  confirmat din log, eroarea persistă identic pe conexiuni proaspete (o
  conexiune TLS eșuată nu rămâne în pool, deci încercarea 2 e oricum nouă
  și eșuează la fel — ipoteza inițială "conexiune veche" era greșită).
  Windows Defender e singurul AV instalat (verificat) — nu face de regulă
  interceptare HTTPS, deci ipoteza "AV MITM" e slăbită, dar nu exclusă.
  **v1.19.10**: `RemoteCertificateValidationCallback` de diagnostic —
  logează Subject/Issuer/Thumbprint REAL + `SslPolicyErrors`/`ChainStatus`
  la orice refuz. **Pas următor concret**: aștept logul cu certificatul
  real din `[TLS] Certificat respins...` — asta va spune definitiv dacă e
  un certificat complet greșit (interceptare/proxy) sau un nod Cloudflare
  cu SAN incomplet pentru `gordas.dev`.
- Detalii complete: `CLAUDE_ARCHIVE.md` (val 3/4) sau `CHANGELOG.md`
  v1.19.7→v1.19.10.

## v1.29.3 (2026-09-06) — Ghid de utilizare (PDF) în fereastra de Setări

Audit ecosistem (cerut de Cristi): clientul Windows nu avea NICIUN acces
la ghidul PDF, deși există de mult pentru Mac (`HelpGuide.swift`,
16 secțiuni, RO/EN/ES). Cele 3 PDF-uri copiate 1:1 în `installer/guides/`
(repo-uri separate, fără cale relativă între ele) — bundle-uite via
`Content`/`CopyToOutputDirectory` în `.csproj`, ajung automat în
`publish/` → `installer.iss` (care copiază tot `publish\*`, fără
modificare necesară acolo). Buton nou „Ghid de utilizare (PDF)” în
`SettingsWindow.xaml` → `OpenHelpGuide_Click` (port 1:1 al
`HelpGuide.swift`) — deschide mereu varianta RO (clientul e RO-only,
fără selector de limbă; EN/ES rămân bundle-uite pentru cand se adaugă
unul).

`docs/update.json` (Mac, secțiunea "windows") sincronizat la 1.29.3.

**Verificat**: `dotnet build ... -r win-x64` — 0 erori, XAML→BAML inclus.

## Etapa 2026-09-11 — v1.29.4 publicat cu semnare Windows activa

Secretele CI (`WIN_SELFSIGN_PFX_BASE64`/`WIN_SELFSIGN_PFX_PASSWORD`,
certificat COMUN ecosistemului) erau deja incarcate de Cristi. Acest release
e primul in care semnarea Regulii 34 chiar a rulat pe un build real.

Verificat direct, nu presupus: pasul de semnare marcat OK in lista de pasi a
job-ului, plus directorul de securitate din header-ul PE al installer-ului
descarcat = 7496 bytes de semnatura Authenticode (acelasi certificat +
timestamp pe toate aplicatiile). Link stabil `releases/latest/download/...`
verificat HTTP 200.

Fara bump de versiune, CI-ul ar fi urcat doar exe-ul semnat peste release-ul
v1.29.3 existent, iar clientii instalati n-ar fi primit nicio notificare
(exact bug-ul documentat in Regula 14). Bump-ul la 1.29.4 a declansat corect
crearea automata a release-ului in `gordasgdc/gdc-plugin-manager` +
`docs/update.json` (sectiunea windows) la 1.29.4 - verificat live.

## Etapa 2026-09-11 — Port `CatalogAccess`: filtrare/grupare/etichete unificate

Paritate Mac/Windows în aceeași sesiune (Regula 31) pentru sistemul universal
de acces introdus în `gdc-plugin-manager-catalog-vendor` — vezi jurnalul de
acolo pentru raționamentul complet al arhitecturii.

**Fișiere noi**: `Core/Models/CatalogAccess.cs` (`AccessKind`, `CatalogGroup`,
`CatalogAccess`, `ResolvedAccess`, `IAccessDescribing`, `AccessResolvers`),
`Client/ViewModels/CatalogFilterViewModel.cs`, `Client/Views/CatalogFilterBar.xaml(.cs)`.

**Două capcane reale, prinse la portare (nu presupuse):**

1. **`JsonStringEnumMemberName` e .NET 9+, proiectul e .NET 8.** Prima
   încercare n-a compilat. Rezolvat cu convertoare explicite, pe tiparul deja
   existent al lui `SupportedOSJsonConverter` — string-urile din JSON trebuie
   să fie IDENTICE cu `rawValue`-urile Swift, altfel Mac-ul și Windows-ul ar
   scrie același catalog în două dialecte.

2. **`PluginItemJsonConverter` și `DownloadableResourceJsonConverter` sunt
   scrise MANUAL** — nu moștenesc nimic automat de la model. Câmpul `access`
   s-ar fi pierdut tăcut la fiecare deserializare, deși modelul îl declara.
   Adăugate citirea ȘI scrierea explicită în ambele. Lecție generală: la orice
   câmp nou pe un model cu convertor JSON scris de mână, verifică activ
   convertorul — declararea proprietății nu e suficientă.

**Verificat pe catalogul LIVE real**, cu aceleași date ca pe Swift: rezultate
identice (378 €, 487 €, gratuit), precedența confirmată (`IsFree` nativ bate
`access.kind=.paid`/999 €), round-trip prin convertorul custom păstrează
`access`. Rulat cu `RollForward` (runtime-ul .NET 8 nu e instalat pe acest Mac,
doar SDK 10) — doar în proiectul de test, nu în cel real.

`AppLink.SupportedOS` adăugat și pe Windows, pentru paritate cu Swift.

Versiune: 1.29.4 → **1.30.0** (MINOR, Regula 14).

## Etapa 2026-09-14 (v1.31.0) — interfața Windows pentru PDF-uri și Scripturi

Completează Etapele 1-3 portate anterior doar la nivel de model.

**PDF-uri**: `SidebarPage.DownloadPdf`, buton în sidebar, secțiune proprie în
`MainWindow.xaml` legată de `MainViewModel.DownloadPdfs`, plus
`InstallManager.DownloadResourceFileAsync` — port 1:1 al variantei de pe Mac:
același mecanism autentificat, aceeași verificare SHA-256, salvare în folderul
ales de user (altfel `Downloads`) și deschiderea Explorer-ului pe fișier.
Comanda existentă `DownloadCommand` rămâne aceeași: pentru o resursă cu fișier
direct descarcă din aplicație, altfel deschide linkul extern ca înainte.

**Scripturi**: `PluginType.Scripts` apare ca filtru în lista de categorii de
produse (Windows nu are pagini separate per tip, ci un filtru), iar produsele se
construiesc acum din `Items + ScriptItems`. Calea de instalare:
`%APPDATA%\Blackmagic Design\DaVinci Resolve\Support\Fusion\Scripts\<subfolder>`
— **nivel utilizator**, cu un segment `Support` în plus față de macOS.
`DestinationDirectory` nu mai adaugă un folder cu numele produsului pentru
scripturi, iar dezinstalarea nu șterge folderul comun — exact ca pe Mac.

### Cât de mult verifică `dotnet build` un XAML — măsurat, nu presupus

Am testat deliberat trei tipuri de greșeli, ca să știu exact ce acoperă build-ul
de pe Mac și ce NU:

| Greșeală introdusă intenționat | Prinsă? |
|---|---|
| XML stricat | **DA** — `MC3000` |
| `x:Static` către un membru inexistent | **DA** — `MC3011` |
| `{Binding CampInexistentXyz}` | **NU** — 0 erori |

Deci structura XAML și referințele de tip sunt garantate de compilator; **căile
de binding nu**, fiindcă se rezolvă la runtime. Pentru ele am verificat manual
că fiecare nume legat există în cod (`DownloadPdfs`, `ContentPage`,
`CurrentPage`, `ShowDownloadCategoryCommand`), iar pentru `DownloadCommand` am
citit **sursa generată** de CommunityToolkit:

```
public IAsyncRelayCommand DownloadCommand => ... new AsyncRelayCommand(DownloadAsync);
```

— confirmând că redenumirea metodei în `DownloadAsync` păstrează exact numele de
comandă pe care XAML-ul îl lega deja.

**Rămâne de confirmat vizual pe Windows**: aspectul efectiv al secțiunii și al
butonului. Nu se poate randa de pe Mac.

### Completări specifice acestui repo, mutate din fosta Partea 1 (2026-09-18)

Păstrate verbatim. Regula generală la care se referă fiecare e în
`~/Developer/CLAUDE.md`.

**Regula 20:**

**Status acest repo (2026-08-27): IMPLEMENTAT (Windows).** `src/GDCPluginManager.Client/Services/SelfUpdater.cs`. Perechea Mac trăiește în `gdc-plugin-manager-catalog-vendor`.

**Regula 32:**

- **Repo-uri deja curățate** (istoric verificat, 0 apariții reale — cele
  câteva rămase sunt mențiuni ale regulii ÎN CONȚINUTUL acestui fișier,
  nu atribuiri reale de commit): CGConvertor, gdc-plugin-manager (Mac),
  **GDCPluginManagerWin (acest repo, 2026-09-05)** — `git filter-repo`
  rulat, verificat pe clonă de test (arbore identic, 135 commit-uri/5
  tag-uri păstrate), apoi aplicat pe repo-ul real + `push --force` pe
  `main` și toate tag-urile. Restul repo-urilor din ecosistem rămân de
  curățat INCREMENTAL, la următoarea lor atingere reală.

**Regula 21:**

**Status acest repo (2026-08-28, verificat): NU SE APLICA ACUM (buffer configurabil), de verificat un detaliu.** Auditat la cererea lui Cristi — `InstallManager.cs` copiaza LUT/DCTL/PowerGrade cu `File.Copy` (streaming la nivel de OS, nu incarca fisierul in memoria aplicatiei) - fisiere tipic KB-cateva MB, fara risc de memorie la volumul actual. **De verificat**: `FetchPrivateFileDataAsync` intoarce `byte[]` (incarca fisierul INTREG in memorie) - daca se adauga vreodata un tip de asset mai mare (ex. un preset video), migreaza la `Stream`/citire in bucati inainte sa devina un risc real, dupa modelul DataMover.

**Regula 34:**

  installer (asset de release sau folder `dist/`) — colaboratorii îl
  importă o SINGURĂ dată în Trusted Root, apoi orice build viitor semnat
  cu ACELAȘI certificat (persistent via secret CI, NU regenerat la
  fiecare build — un cert nou la fiecare release ar rupe încrederea deja
  acordată) e automat de încredere pe mașinile lor.
- **Aplicare**: la fiecare build de release/actualizare Windows, pe orice
  aplicație din `~/Developer/` care produce un `.exe`/installer Windows —
  aplicată incremental, la următoarea atingere reală a fiecărui repo
  (Regula 11), nu retroactiv peste tot dintr-o sesiune dedicată.
- **Implementare de referință**: CGConvertor (`build-windows.spec` +
  `.github/workflows/build-windows.yml`, 2026-09-06) — vezi
  `codesigning/README-windows.md` din acel repo pentru pașii exacți pe
  care Cristi trebuie să-i ruleze o singură dată (generare cert + upload
  secret CI).

**Status acest repo (2026-09-06): IMPLEMENTAT în CI, secret NEÎNCĂRCAT
încă.** `codesigning/sign-windows.ps1` + `generate-self-signed-cert.ps1` +
`README-windows.md` (adaptate din CGConvertor, certificat COMUN tuturor
aplicațiilor GDC — Cristi încarcă separat `WIN_SELFSIGN_PFX_BASE64`/
`WIN_SELFSIGN_PFX_PASSWORD` pe acest repo, aceleași valori ca la
CGConvertor). `.github/workflows/build-windows.yml`: `env:
HAS_WIN_SELFSIGN` la nivel de job + 2 pași noi de semnare (`publish\
GDCPluginManager.exe` după smoke test, `Output\GDCPluginManagerSetup.exe`
după Inno Setup) — `actionlint` 0 erori, YAML valid. Fără secretele
încărcate, build-ul continuă nesemnat, exact ca înainte.

### Handoff — fișierul de stare (Regula 50, `~/Developer/CLAUDE.md`)

- Fișierul de stare al acestui proiect: `PROJECT_STATE.md` (rădăcina repo-ului). La orice sesiune nouă se citește
  ÎNTÂI el, apoi doar fragmentele strict necesare; se actualizează la milestone-uri și obligatoriu la final.
  Dacă lipsește, se creează la prima sesiune care atinge proiectul. Repo PUBLIC: fișierul e intern, listat în `.gitignore` (doar local, Regula 29).
- Restructurarea/ștergerea lui și orice modificare a acestui `CLAUDE.md`: doar cu diff-ul arătat și acordul lui Cristi.
