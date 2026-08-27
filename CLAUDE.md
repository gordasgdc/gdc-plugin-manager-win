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

## [PARTEA 1: REGULI GLOBALE ECOSISTEM GDC — identică în toate proiectele GDC]

> Acest bloc e sincronizat manual în `CLAUDE.md`-ul TUTUROR proiectelor din
> `~/Developer/` (CGConvertor, CursorPro, DataMover, GDCPluginManager,
> GDCPluginManagerWin, GDCVault, GDCVaultWin, gdc-plugin-manager-catalog-vendor,
> gdc-plugin-manager-files, gdc-production-manager, gdc-resolve-encoder, și
> orice proiect GDC nou). Dacă modifici o regulă aici, propag-o manual și în
> celelalte 10 fișiere — nu există un fișier partajat/include, fiecare
> `CLAUDE.md` e citit independent per-repo. Vezi jurnalul "Sincronizare
> CLAUDE.md" din secțiunea Partea 2 a fiecărui repo pentru data ultimei
> unificări.

**1. Directoare & structură.** Toate proiectele GDC trăiesc exclusiv în
`~/Developer/<NumeProiect>/`, niciodată în `~/Downloads` sau `~/Desktop`
(curățate automat de CleanMyMac/Hazel pe acest Mac — au șters repo-uri de
sursă în trecut). Niciun repo nou nu se creează/clonează în afara
`~/Developer/`. Certificatele Apple (`.p12`/`.cer`) și orice cheie privată
(`.p8`/`.key`/`.pem`/`.mobileprovision`) stau EXCLUSIV în
`~/Developer/Certificates/` (folder în afara oricărui repo git) — niciodată
comise, indiferent de `.gitignore`.

**2. Securitate — zero secrete în git.** `.git/config` nu conține niciodată
un token în clar în URL-ul remote-ului (`https://user:TOKEN@github.com/...`)
— autentificare exclusiv prin `gh` (credential helper) sau SSH. Orice token
găsit expus se elimină din config imediat; revocarea efectivă din GitHub
Settings e un pas manual al lui Cristi (Claude nu poate revoca un token).
Un secret comis vreodată în istoricul git (verificat cu
`git log --all -p | grep` sau echivalent) trebuie semnalat explicit, nu doar
curățat din starea curentă.

**3. Licențiere & Donație (GDC Plugin Manager / Furnizor).** Toate
aplicațiile standalone GDC folosesc `LicenseCore`/`MachineID` (Ed25519,
aceeași cheie publică hardcodată în tot ecosistemul — copiată byte-for-byte,
NU printr-o dependință de pachet între repo-uri). Probă gratuită implicită:
**15 zile**. Activare manuală prin WhatsApp (ID de mașină pre-completat) →
cod generat din `GenerateSerialView.swift` (Furnizor, `gdcStandaloneProducts`
trebuie să includă `productID`-ul noii aplicații). Valoarea susținerii
aplicației se exprimă EXCLUSIV ca **donație** — sumă implicită de referință
**23 €** dacă nu există alt preț promoțional documentat pentru acea
aplicație — NICIODATĂ cu cuvintele „preț", „cumpără" sau „vânzare" (RO/EN/ES:
niciodată „price"/„buy"/"sale" nici în engleză/spaniolă). Formularea trebuie
să apară clar în: UI-ul aplicației (ecran/pop-up de licență), ghidul PDF, și
orice pagină web dedicată.

**[COMPLETARE 2026-08-26, închide o lacună de scop reală]** Interdicția de
mai sus se aplică ACUM și produselor din catalogul GDC Plugin Manager
(LUT/DCTL/PowerGrade vândute prin marketplace-ul gratuit) — găsit la audit
un card cu buton „Cumpără" și sume afișate brut („378,00 €"). Butonul
devine „Donează" peste tot (RO/EN/ES); suma documentată de furnizor pentru
acel produs (promoția specifică lui, nu neapărat 23 €) rămâne vizibilă, dar
NICIODATĂ lângă cuvântul „preț"/„cumpără"/„vânzare" — decizia anterioară de
scop (marketplace = "relație comercială diferită, nu se aplică") e
INVALIDATĂ explicit. Excepție: tabelele interne ale Furnizorului (ex.
`SalesHistoryView`, coloana „Preț" din registrul de vânzări al lui Cristi)
nu sunt UI orientat spre client — rămân neatinse.

**15. CRM Furnizor — set minim de funcționalități administrative
(2026-08-26).** Panoul de Clienți al Furnizorului (`SalesHistoryView.swift`)
nu rămâne un log rigid — trebuie să ofere: filtrare rapidă pe produs
(dropdown dinamic, nu hardcodat), export 1-click (clipboard sau fișier) al
email-urilor/HWID-urilor din selecția curentă (filtrată), copiere rapidă
per-câmp direct din tabel (fără să deschizi editarea), Licențiere în Masă
(paste o listă de email-uri/machine ID-uri → generează automat câte o
licență per linie, pentru un produs/durată alese o singură dată), și
editare liberă a duratei unei licențe deja generate (Zile/Luni/Ani/
Lifetime). Furnizorul arată versiunea curentă în UI, la fel ca orice
aplicație client — nu e scutit de Regula 7 doar pentru că e un instrument
intern.

**16. Design Web "Shift" — compact, fără spații goale (2026-08-26).**
Completare la Regula 12: paginile de prezentare NU doar adoptă paleta
amber/cupru — trebuie și dense/aerisite corect, nu găunoase. `min-height:
100svh` pe un hero cu conținut scurt lasă spațiu gol enorm pe orice ecran
mai mare — evită-l sau limitează-l (ex. `78svh`); padding-ul secțiunilor
(`section`) rămâne generos dar nu excesiv (60px, nu 90px+). Orice accent
vechi (verde/teal/albastru folosit ca accent PRIMAR, nu ca stare
semantică precum "verificat cu succes") se înlocuiește cu amber/cupru —
o variabilă CSS poate păstra alt NUME istoric (`--scope`, `--accent-copy`)
atât timp cât VALOAREA ei devine amber, ca să nu rescrii zeci de
apariții `var(--x)` din foaia de stil.

**4. Manager de Dependențe (Standard GDC, opt-in).** Aplicația de bază
rămâne lightweight — orice dependință externă opțională/grea (ex. FFmpeg
static) se descarcă LA CERERE, nu bundle-uită implicit dacă poate fi evitat.
Indicator global 🔴/🟢 vizibil în header/meniu: verde doar dacă TOATE
componentele obligatorii (non-opționale) sunt OK; componentele opționale
(ex. Homebrew pe Mac) nu blochează starea verde. Click pe indicator deschide
un panou dedicat ("Verificare & Dependențe Sistem") cu o listă modulară de
componente (model generic `DependencyItem` — id, nume, opțional/obligatoriu,
verificare headless, acțiune, niciodată câmpuri hardcodate per-dependință),
fiecare cu propriul status + buton de acțiune (descărcare automată a unui
binar static, sau copiere comandă de instalare). Verificarea rulează headless
la fiecare deschidere a panoului/meniului, actualizând starea instant.

**5. Instalare Autonomă.** Mac: `.pkg` semnat Developer ID Application +
Installer, notarizat, stapled, cu `pkgbuild --install-location "/"` și
payload la `Applications/<App>.app` — instalare DIRECTĂ în `/Applications`
la dublu-click, fără drag-and-drop manual (verificabil cu
`pkgutil --payload-files`). Windows: installer Inno Setup cu
`DefaultDirName={autopf}\GDC\<App>` (Program Files) sau varianta x86,
scurtături automate Desktop + Start Menu, dezinstalare nativă prin
"Apps & Features" (fără script separat necesar dacă Inno Setup o acoperă).

**6. Packaging Mac — arhivă cu STRICT 3 fișiere.** Orice
`<App>-Mac.zip` livrat clientului conține la rădăcină EXACT: (1)
executabilul/`.pkg`-ul semnat+notarizat+stapled, (2)
`Dezinstalare_<App>.command` (dezinstalare completă: procese, TCC dacă
relevant, `~/Library/Application Support`, `Caches`, `Preferences`,
`Saved Application State`, `Logs`, orice item Keychain scris de aplicație),
(3) `Instructiuni_Utilizare.pdf` (RO/EN/ES). NICIODATĂ hack-uri
`xattr -dr com.apple.quarantine` sau launchere `Instalare_*.command` —
pachetul stapled e acceptat nativ de Gatekeeper. Curățarea unei instalări
vechi se face în `installer/scripts/preinstall` (`pkgbuild --scripts`,
pkill + `rm -rf`), niciodată legat de quarantine.

**7. UI Standard — varianta "Shift".** Temă dark, profesională, inspirată de
paginile de Color din DaVinci Resolve (fundal `#14161A`/`#1A1D22`, accent
cald cupru/amber sau altă culoare distinctă per-aplicație, text `#EDEFF2`).
Număr de versiune vizibil în UI (About/Meniu/Settings/Footer), fără excepție.
Update Checker automat la lansare + verificare manuală, conectat la
`update.json`/GitHub Releases API, cu notificare atât banner discrét CÂT ȘI
pop-up modal (o singură dată per versiune nouă, stare de dismissal comună
între cele două) — un simplu banner nu e suficient. `mandatory: true` în
`update.json` ignoră dismissal-ul anterior.

**8. Documentație PDF — standard ultra-detaliat.** Orice
`Instructiuni_Utilizare.pdf` (RO/EN/ES) se redactează pentru un utilizator
complet începător, zero presupuneri, cu secțiunile relevante aplicației:
(a) Panoul de Dependențe — ce înseamnă 🔴/🟢, pas-cu-pas ce face userul la
roșu (unde dă clic, ce se deschide, ce buton apasă); (b) Homebrew (Mac,
dacă aplicabil) — pași la nivel de acțiune: copiază comanda din aplicație,
deschide Terminal (Spotlight, `⌘+Space`), lipește (`⌘+V`), Enter, apoi
explică parola de Mac cerută (invizibilă la tastare) + Enter din nou;
(c) Fluxul de utilizare + acțiuni post-proces — cum se adaugă
fișiere/date, ce face fiecare buton rezultat; (d) Licență & Donație — trial
gratuit explicit (zile), suma exactă ca donație (niciodată "preț"/"vânzare");
(e) Cum funcționează actualizarea automată — ce înseamnă pop-up-ul de
versiune nouă, ce face butonul „Actualizează acum" vs „Mai târziu", și că
instalarea noii versiuni rămâne un pas asistat (descărcare + reinstalare),
nu un update silențios în fundal.

**9. Checklist obligatoriu la FIECARE release** (păstrat identic cu
"DIRECTIVĂ PERMANENTĂ SUPREMĂ" din jurnalul fiecărui proiect — punctele
1-4 de acolo sunt subsumate integral de punctele 5-8 de mai sus). Site-ul
public al fiecărei aplicații trebuie să pointeze mereu la
`releases/latest/download/...` (HTTP 200 verificat, nu presupus), niciodată
un tag fix.

**10. Comunicare & jurnal.** Fiecare `CLAUDE.md` rămâne un jurnal
append-only (regulile vechi nu se șterg, doar se marchează
**[ÎNVECHIT]** cu motivul dacă sunt explicit invalidate). Răspunsurile
Claude rămân ultra-concise: fără explicații de proces, direct codul/
diff-ul/comenzile și statusul. La orice modificare de cod, comanda exactă
de rebuild local se include la finalul răspunsului.

**11. Sincronizare dinamică a Standardului Master (CONTINUOUS UPDATE,
2026-08-26).** Orice adăugare/modificare/optimizare a unei reguli globale
din ACEASTĂ Partea 1 — indiferent din ce proiect pornește — devine automat
noul Standard Master și TREBUIE propagată manual, în ACELAȘI commit sau
imediat următorul, în `CLAUDE.md`-ul tuturor celorlalte proiecte din
`~/Developer/` (nu doar notată "pentru mai târziu"). Orice aplicație NOUĂ
creată în `~/Developer/` primește Partea 1 (versiunea curentă, completă)
încă din primul `CLAUDE.md` scris pentru ea — nu se pornește niciodată de
la un fișier gol sau parțial. Regula 1 de mai sus ("Dacă modifici o regulă
aici, propag-o manual...") descrie mecanismul; aceasta îl declară
obligatoriu, nu opțional.

**12. Profil Utilizator/HWID în Sidebar, Sistem de Revocare Licențe &
Standard Design Web Mobile/Desktop "Shift" (2026-08-26).**
- **Profil Utilizator opțional, vizibil în sidebar-ul UI** (Mac + Windows,
  pe toate aplicațiile cu licențiere GDC): Nume (sau „Anonim" dacă nu e
  completat), Email, și Machine ID (HWID) — afișate clar, nu ascunse
  într-un submeniu. Portat din modulul Tracker existent (Mac,
  `AnalyticsClient.registerDevice` → Supabase `devices`) — Windows trebuie
  aliniat la aceeași infrastructură, nu una separată.
- **Revocare/blacklist de licențe, prin Supabase** (ACEEAȘI bază de date
  deja folosită de Tracker — niciun backend nou de construit). O licență
  Ed25519 rămâne verificată local (offline-first, nicio schimbare la
  activarea inițială), dar clientul verifică periodic + la lansare (dacă
  există conexiune) un tabel de revocări după `machineID`/serial. **Fail
  OPEN, nu fail closed**: fără conexiune la internet, o licență deja
  activată local CONTINUĂ să funcționeze (nu bricuim un user legitim offline)
  — revocarea se aplică abia la următoarea verificare online reușită.
  Furnizor capătă unelte de revocare instant + editare a perioadei de
  valabilitate a unei licențe existente deja generate.
- **Generare flexibilă de licențe** (Furnizor): selector explicit al
  duratei — Zile / Luni / Ani / Forever (Lifetime) / Valabil până la
  versiunea X — nu doar trial fix + activare permanentă binară.
- **Standard Design Web "Shift"** — orice pagină de prezentare/descărcare
  GDC (`gordas.dev` și paginile dedicate per-aplicație) adoptă design-ul
  dark, minimalist, accent amber/cupru consacrat de CG Convertor
  (`gordas.dev/cg-convertor`) — niciun accent verde vechi sau stil
  nealiniat. Toate paginile trebuie optimizate explicit pentru mobil
  (iOS Safari + Android Chrome), verificat vizual la lățimi de telefon,
  nu doar "responsive by CSS framework".

**13. Update Checker — specificație UX obligatorie (2026-08-26).** La
lansare, aplicația verifică `update.json`/GitHub Releases; dacă versiunea
locală e mai veche, arată un pop-up/modal Shift (nu doar bannerul discret
din Regula 7) cu: numărul noii versiuni, un rezumat scurt al noutăților
(Release Notes, dacă `update.json` le are — câmp opțional, degradează
elegant dacă lipsește), și DOUĂ butoane explicite — **„Actualizează acum"**
(deschide direct link-ul de descărcare a installer-ului/pachetului nou,
`releases/latest/download/...`, și arată userului că trebuie să
instaleze peste versiunea curentă + repornească aplicația — NU e un
self-update silențios, niciun helper nu înlocuiește bundle-ul/exe-ul în
fundal, vezi WARNING-ul deja existent din `UpdateChecker.swift`/`.cs`) și
**„Mai târziu"** (închide fereastra, aceeași stare de dismissal ca
bannerul). Popup-ul apare o singură dată per versiune nouă, cu excepția
`mandatory: true` (reapare la fiecare lansare). Ghidul PDF (Regula 8(e))
trebuie să explice acest flux exact.

**14. Versionare semantică obligatorie la FIECARE schimbare (2026-08-26).**
Orice modificare de cod livrată clientului — oricât de mică — incrementează
numărul de versiune, sincron în TOATE punctele care îl țin (Info.plist Mac,
`.csproj`/`installer.iss` Windows, `docs/update.json`, orice altă constantă
de versiune din acel repo). Format `MAJOR.MINOR.PATCH` (ex. `2.3.1`):
- **PATCH** (ultima cifră, `2.3.0`→`2.3.1`) — orice fix, ajustare, adăugare
  mică sau schimbare care nu rupe compatibilitatea. Cazul implicit, cel mai
  frecvent.
- **MINOR** (cifra din mijloc, `2.3.x`→`2.4.0`) — funcționalitate nouă
  vizibilă (ex. o fază/etapă întreagă ca Panoul de Dependențe sau Profilul
  HWID), fără schimbări radicale de arhitectură.
- **MAJOR** (prima cifră, `2.x.x`→`3.0.0`) — schimbare radicală: rebranding,
  redesign complet de UI, schimbare de arhitectură (ex. sistem nou de
  licențiere), sau orice prag pe care Cristi îl declară explicit "versiune
  majoră".
**De ce**: `UpdateChecker`/`.cs` compară STRICT numărul de versiune din
`update.json` cu cel instalat (`IsNewer`) — înlocuirea unui binar pe un
release existent, PE ACEEAȘI versiune, nu declanșează nicio notificare la
clienții deja instalați (bug real, găsit și reparat 2026-08-26: Windows
Shift UI + Faza 1/3/4 livrate silențios sub `v1.2.22`, fără niciun bump).
Un bump de versiune fără schimbare reală de cod e la fel de greșit ca
schimbarea de cod fără bump — cele două merg mereu împreună, în același
commit.

**17. Orice fișier descărcabil TREBUIE să poarte numărul versiunii în NUMELE
fișierului (2026-08-26).** Nu doar în interiorul aplicației (Regula 14) —
în numele fizic al pachetului: `DataMover-2.5.5.pkg`, nu `DataMover.pkg`;
`GDCPluginManagerSetup-1.2.8.exe`, nu `GDCPluginManagerSetup.exe`. Motiv
direct de la Cristi: probele/build-urile de test se acumulează local (în
`~/Downloads`, `/tmp`, trimise pentru testare) și devin de nerecunoscut
fără versiune în nume — "am o grămadă de descărcări și nu știu ce versiune
sunt, care, ce și cum sunt".
- **Excepție, NU o contrazicere**: mecanismul `releases/latest/download/
  <nume-stabil>` (site-ul, self-updater-ul) are nevoie STRUCTURAL de un
  nume care nu se schimbă niciodată între release-uri — vezi Regula
  Domeniului & Download. Copia asta stabilă (`DataMover.pkg`,
  `GDCPluginManager.pkg`) tot trebuie publicată, DAR ALĂTURI de copia
  versionată, niciodată singură. `build_installer.sh`/`build_app.sh` din
  fiecare repo produc deja ambele — regula asta cere doar ca ambele să
  ajungă mereu pe release, nu doar cea stabilă.
- **Orice fișier construit/descărcat/trimis lui Cristi în afara acestui
  mecanism** (build local de test, artefact de CI descărcat manual,
  fișier trimis prin `SendUserFile`, copie pusă în `/tmp` pentru
  verificare) TREBUIE redenumit explicit cu versiunea înainte de a fi
  oferit — niciodată livrat cu numele generic/stabil, care are sens doar
  ca țintă a unui link fix, nu ca fișier de sine stătător pe disc.

**18. Standard UX/Arhitectură obligatoriu pentru orice aplicație desktop
NOUĂ, de la primul release (2026-08-26).** Stabilit după MediaFlow Monitor
v1.3.0 — patru cerințe care nu mai sunt opționale pentru nicio aplicație
GDC viitoare (Mac și, unde tehnologia o permite, Windows):
- **Mutare automată în `/Applications` (Mac)** — la lansare, dacă bundle-ul
  rulează în afara `/Applications` sau `~/Applications` (tipic: extras
  direct din `.zip`/Downloads, sub App Translocation), aplicația arată un
  prompt nativ ("Doriți să mutați X în Aplicații?") și, la confirmare,
  copiază bundle-ul, relansează din noua locație și mută originalul la
  Coșul de gunoi. Vezi implementarea de referință `AppMover.swift`
  (MediaFlow Monitor) — fără dependință externă (PFMoveToApplicationsFolder
  nu are un port SPM întreținut), doar `NSAlert` + `FileManager`.
- **Fereastră principală redimensionabilă liber**, cu o dimensiune minimă
  de siguranță (`minSize`/`minWidth`+`minHeight`) sub care conținutul nu
  mai e lizibil — nu ferestre cu dimensiune fixă hardcodată.
- **Selector explicit de temă System/Dark/Light**, independent de setarea
  macOS/Windows — unii clienți vor Light chiar și noaptea, alții Dark
  permanent; NU e suficient să urmezi orbește `prefers-color-scheme`/tema
  sistemului. Persistat local (`UserDefaults`/Registry), aplicat imediat
  fără repornire. Vezi `AppTheme.swift`/`ThemeManager` (MediaFlow Monitor).
- **Protocolul de semnare, notarizare, auto-update și integrare GDC
  Manager rămâne cel deja documentat în Regulile 3, 5, 6, 13, 14, 17** —
  regula asta nu introduce un protocol nou, doar reconfirmă că orice
  aplicație nouă îl respectă de la prima versiune publicată, nu "adăugat
  ulterior quando there's time".

**19. Regulă Legală & Packaging (UE/Global) (2026-08-27).**
- **Pagini Web.** Orice landing page nouă sau actualizare de site publicată
  pe `gordas.dev` (sau pe orice site GDC, inclusiv paginile de proiect
  `gordasgdc.github.io/<repo>`) TREBUIE să conțină în footer link-uri către
  `https://gordas.dev/termeni` (Termeni și Condiții),
  `https://gordas.dev/confidentialitate` (Politică de Confidențialitate
  GDPR) și, unde e relevant, `https://gordas.dev/cookie` (Cookie-uri),
  plus o notă scurtă de statut: *"gordas.dev este o platformă administrată
  de dezvoltatori independenți. Aplicațiile și resursele sunt furnizate ca
  atare (AS IS), iar susținerea proiectului se bazează pe contribuții
  opționale de sprijin și donații."* Sursa canonică a acestor 3 pagini
  legale trăiește în `gdc-plugin-manager-catalog-vendor/docs/` — orice alt
  site GDC linkuiește către ele (absolut), nu le duplică.
- **Installere (.pkg macOS / .exe Windows).** Începând cu următoarele
  versiuni/build-uri (NU retroactiv — fără rebuild al aplicațiilor deja
  publicate doar pentru asta), scripturile de instalare
  (`build_installer.sh`/`productbuild` pe Mac, `installer.iss`/Inno Setup
  pe Windows) TREBUIE să includă un pas de acceptare a licenței (License
  Agreement/SLA), bazat pe un fișier `license.rtf`/`license.txt` cu un
  extras din Termeni și Condiții (statut de proiect independent,
  licențiere legată de Machine ID, natura de donație a susținerii,
  limitarea răspunderii "as is"). Utilizatorul trebuie să apese explicit
  "Agree"/"I accept" înainte ca instalarea să se finalizeze.

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
- **2026-08-24 — Copertă adăugată la `AppLink` (Aplicații), port 1:1 după `PartnerStore`.** `AppLink.CoverImage`/`CoverImageUrl` (Core), `AppLinkViewModel.Cover` (creat în constructor, din parametru, la fel ca `PartnerStoreViewModel`), și `Button Style="CoverButtonStyle"` + `Image` în `DataTemplate DataType="{x:Type vm:AppLinkViewModel}"` din `MainWindow.xaml`, cu `SymbolIcon` ca fallback prin `InverseBoolToVisibility`. Vezi perechea de pe Mac, `AppCard` din `ContentView.swift` (`CoverThumbnail`).
- **2026-08-24 — Pop-up modal de update, pe lângă bannerul existent.** `MainWindow.xaml.cs.MaybeShowUpdatePopupAsync()`, apelat din `OnLoaded` după `InitializeCommand`. Folosește `Wpf.Ui.Controls.MessageBox` (NU `System.Windows.MessageBox` — cel nativ nu poate arăta text custom pe butoane, doar Yes/No/OK/Cancel fixe). Citește direct `UpdateChecker.Shared.AvailableUpdate`, nu prin `MainViewModel` — aceeași sursă pe care `InitializeAsync` a folosit-o deja pentru banner. La închidere (orice buton) apelă `_viewModel.DismissUpdateBannerCommand`, ca să ascundă și bannerul și să nu reapară la fiecare pornire — popup-ul și bannerul trebuie să rămână cuplate pe aceeași stare de „dismissed", la fel ca pe Mac (`ContentView.swift`, `.alert` legat de `updateChecker.availableUpdate`). Textul e cel cerut explicit de Cristi (2026-08-24): explică răspicat că nu e self-update automat.
- **2026-08-24 — Regulă permanentă (întreg ecosistemul GDC): orice aplicație trebuie să vină cu un mecanism dedicat de dezinstalare completă ("Clean Uninstall")** — pe Windows: curăță folderul din `Program Files`, `%AppData%`/`%LocalAppData%`, și orice cheie de Registry creată de aplicație. Regulă completă (inclusiv partea Mac) documentată în `gdc-plugin-manager-catalog-vendor/CLAUDE.md`. Referință de implementare: `gdc-vault-win/uninstall.ps1`. Dacă `GDCPluginManagerWin` capătă vreodată o cheie de Registry sau un fișier persistent nou (în afara `%LocalAppData%\GDCPluginManager` deja acoperit de instalatorul Inno Setup), stergerea lui trebuie adăugată la uninstaller în același commit.
- **2026-08-24 — [ÎNVECHIT 2026-08-25, vezi mai jos] Bug critic: `Image.Source="{Binding Cover.Url}"` (Uri direct) nu functioneaza — `ImageSourceConverter` accepta doar `string`.** Fix ORIGINAL: `Converters/UriToImageSourceConverter.cs`, legat pe toate cele 6 `Image.Source` din `MainWindow.xaml`. Acest converter a fost ELIMINAT pe 2026-08-25 — vezi intrarea de mai jos pentru motiv si inlocuitor.
- **2026-08-25 — Bug real: coperțile la Materiale/Evenimente nu se încărcau DELOC pe un client Windows viu, în timp ce la Aplicații "mergeau greu".** Cauza: `UriToImageSourceConverter` crea un `BitmapImage` nou la fiecare evaluare de binding, FĂRĂ să asculte `DownloadFailed` — orice eșec de descărcare rămânea complet silențios, iar `Cover.HasImage` (bazat doar pe "are URL sau nu") tot arăta cardul ca "are imagine", deci XAML-ul ascundea iconița de rezervă și lăsa un dreptunghi gol — mult mai vizibil la Materiale/Evenimente (`Height` 170-190) decât la Aplicații (`Height` 56). **Fix**: `UriToImageSourceConverter` ELIMINAT complet; `CoverViewModel` acum își gestionează singur descărcarea (`Bitmap`, `LoadFailed`), ascultă explicit `DownloadCompleted`/`DownloadFailed`, iar `HasImage` reflectă bitmap-ul REAL încărcat, nu doar existența URL-ului. XAML leagă direct la `Cover.Bitmap`. Pattern-ul e identic cu cel deja corect din `LightboxWindow.LoadImage` (vezi pitfall 2026-08-23) — de-acum SINGURA implementare de încărcare imagini din client. **Lecție**: orice card nou cu copertă trebuie să folosească `CoverViewModel`, niciodată un converter separat pe `Uri` direct.
- **2026-08-25 — `CatalogAssets.ImageUrl` acum escapează explicit fiecare segment de path** (nu se mai bazează doar pe combinarea implicită `Uri`) — robustețe suplimentară pentru nume de fișiere cu caractere speciale alese liber de furnizor (apostrof, diacritice). Query-ul (`?v=hash`) rămâne neatins.
- **2026-08-25 — Badge compatibilitate OS (🍎/🪟/🔄) e acum vizibil pentru TOATE cele 3 stări, inclusiv `CrossPlatform`/"Ambele".** Decizia inițială (ascuns pentru starea implicită, ca să nu "polueze" vizual cazul comun) a fost o presupunere greșită despre așteptările UX — corectată la cererea explicită a lui Cristi: "Ambele" trebuie să se vadă explicit pe card, nu doar să fie absența unui badge. `ProductViewModel.ShowOSBadge` a fost eliminat (badge-ul e mereu vizibil acum).
- **2026-08-25 — Obfuscarea codului (Obfuscar 3.0.0-beta.19) DEZACTIVATĂ definitiv pe `GDCPluginManager.Core.dll`.** Confirmat live: tool-ul producea metadate corupte ("Duplicate type") în DOUĂ configurații diferite (`HidePrivateApi=true` cu `ReuseNames=false`, apoi `HidePrivateApi=false`) — al doilea caz avea chiar clase generate de compilator (`<>c`) duplicate, dovadă că bug-ul e intern lui Obfuscar (metadata `NestedClass`), nu în setările de renumire. Crash real la un client Windows: `BadImageFormatException` la pornire, în `MainWindow` → `LicensePaneViewModel`. Corectitudinea contează mai mult decât obscurizarea — vezi comentariul din `.github/workflows/build-windows.yml`. NU reactiva fără un test extins pe un client Windows real, nu doar smoke-test-ul simplu din CI (care a trecut fals-pozitiv o dată).
- **2026-08-24 — `Mandatory` din `UpdateInfo` acum e citit efectiv** (vezi `UpdateChecker.CheckAsync()`/`.Dismiss()`) — reaparea la fiecare lansare daca `true`, fara buton de inchidere permanenta. Regula de release (bump versiune + `update.json` + Release GitHub manual la orice fix) documentata complet in `gdc-plugin-manager-catalog-vendor/CLAUDE.md`.
- **2026-08-24 — Badge GRATUIT/LICENȚĂ/PROBĂ + filtru Toate/Gratuite/Premium pe Produse.** `ProductViewModel` capătă `BadgeText`/`BadgeBrush`/`BadgeTooltip`/`ShowPrice`; `MainViewModel` capătă `PriceFilter` (enum) + `SetPriceFilterCommand`, integrat în `FilterProduct`. Filtrul e randat ca al doilea rând în grid-ul deja existent al barei de căutare (Grid.Row="0", cu RowDefinitions noi LOCALE acelui Grid) — NU ca rând nou în grid-ul părinte de nivel superior, ca să nu deranjeze numerotarea `Grid.Row`/`RowSpan` deja folosită mai jos de ScrollViewer-e. Detalii complete (inclusiv investigația imaginilor Facebook) în `gdc-plugin-manager-catalog-vendor/CLAUDE.md`.
- **2026-08-24 — REZOLVAT: codul QR lipsea pe Windows din pagina Android, DELIBERAT (nu bug) — comentariul vechi din `MainWindow.xaml` spunea explicit "WPF nu are un generator inclus si nu merita o dependinta noua doar pentru asta".** Cristi a cerut parity cu Mac-ul. Fix: pachet nou `QRCoder` (MIT, 1.6.0) — `PngByteQRCode` produce PNG pur managed, fără `System.Drawing.Common`. `QrCodeImageGenerator.cs` (Client/Services) generează un `BitmapImage` din `AndroidRelease.ApkUrl` — port 1:1 al `AndroidPane.qrImage(from:)` (Mac, `CIFilter.qrCodeGenerator`, correction level M, scalare ×10). `MainViewModel.OnAndroidReleaseChanged` regenerează `AndroidQrImage` automat de fiecare dată când `android.json` se reîncarcă. Fundal alb obligatoriu (contrast necesar pt. citire), `RenderOptions.BitmapScalingMode="NearestNeighbor"` — echivalentul `.interpolation(.none)` de pe Mac, altfel QR-ul iese neclar la scalare.

## Secțiune nouă "Audio" (2026-08-27) — port 1:1 al Mac
Port al `AudioTrack`/`SidebarSection.audio` din `gdc-plugin-manager`
(Mac). `CatalogModel.cs`: record `AudioTrack` (Id/Name/Description/Url/
YoutubeURL/CoverImage) + `Catalog.AudioTracks`, default `[]` (retrocompatibil,
`PropertyNameCaseInsensitive` mapeaza `audioTracks` fara conflict).
`CatalogService.cs`: proprietate + populare in `RefreshAsync`/`LoadFromCache`,
`Raise(nameof(AudioTracks))`. `AudioTrackViewModel.cs` (nou) — port 1:1 al
`AppLinkViewModel`, cu `Description` in plus. `MainViewModel.cs`:
`SidebarPage.AudioTracks`, `ObservableCollection<AudioTrackViewModel> AudioTracks`,
`ShowAudioTracksCommand`, populare in `RefreshDataAsync` (langa `Apps`).
`MainWindow.xaml`: `RadioButton` plasat imediat langa categoriile
LUT/DCTL/OFX/PowerGrade (ItemsControl `Categories`), NU langa Cursuri/
Materiale — identic cu plasarea din `ContentView.swift` (Mac); `DataTemplate`
pentru `AudioTrackViewModel` (mirror `AppLinkViewModel` + rand de descriere
ca la Materiale) si pagina de continut (mirror pagina Aplicatii). Icon
`MusicNote224` (Wpf.Ui 3.0.5) — verificat prezent prin `strings` pe
`Wpf.Ui.dll` inainte de folosire (vezi pitfall `Symbol="Phone24"`
2026-08-24 — `MusicNote2Play24` NU exista, doar varianta `20`; folosit
`MusicNote224` in loc). **`dotnet build` pe Mac a trecut curat inclusiv pe
Client (`net8.0-windows`), dar asta NU e o dovada ca XAML-ul e valid**
(vezi pitfall 2026-08-23 — `PresentationBuildTasks` e Windows-only) —
validat manual in schimb: XML well-formed (`xml.etree.ElementTree`) +
toate cheile `StaticResource` folosite (`CardBorderStyle`, `BadgeBorderStyle`,
`CoverButtonStyle`, etc.) sunt refolosite verbatim din `DataTemplate`-ul
`AppLinkViewModel` deja existent, deci deja confirmate valide. Test real pe
Windows tot recomandat inainte de release. Versiune: `1.3.1`→`1.4.0`
(MINOR), sincron cu `docs/update.json` (Mac, `gdc-plugin-manager-catalog-vendor`)
si cu Client Mac (`gdc-plugin-manager-catalog-vendor/Info.plist`, 1.4.0).
**Verificat prin CI real** (`gh run watch`, commit `d747abc`, run
`33039378596`, `windows-latest`) — `build-windows` succes complet:
publish self-contained, smoke test, compilare Inno Setup, artefact
incarcat. XAML-ul (`MainWindow.xaml`) a compilat efectiv pe Windows,
nu doar validat manual ca mai sus — confirmare reala, nu presupunere.

## DIRECTIVĂ PERMANENTĂ SUPREMĂ: Checklist obligatoriu la FIECARE release (2026-08-25)
Valabilă pentru TOATE aplicațiile ecosistemului GDC (CursorPro, GDC Plugin
Manager + Furnizor, GDC Plugin Manager Windows, DataMover, GDC Production
Manager, și orice proiect nou). Înainte de a raporta un release ca fiind
gata, TREBUIE bifate intern toate cele 4 puncte de mai jos — dacă unul
lipsește, spune-o explicit, nu declara release-ul "gata".

1. **Versiune vizibilă în UI** — About/Meniu/Settings/Footer trebuie să
   arate versiunea curentă (`v1.2.21` etc.), fără excepție.
2. **Verificator de actualizări** — la pornire sau printr-un buton
   „Caută actualizări", aplicația verifică versiunea de pe server/GitHub
   și notifică userul când există un release mai nou.
3. **Pachetul standard de release** — orice arhivă livrată clientului
   conține FĂRĂ EXCEPȚIE:
   - executabilul/installer-ul semnat + notarizat,
   - `Dezinstalare_[NumeAplicație].command` (dezinstalare completă:
     procese, permisiuni TCC, toate fișierele din `~/Library/`),
   - un ghid/PDF de instrucțiuni.
4. **Sincronizare site ↔ GitHub Releases** — linkurile de download de pe
   site trebuie să pointeze mereu la `releases/latest/download/...`
   (HTTP 200 verificat, nu presupus) și să menționeze numărul ultimei
   versiuni.

## Faza 3 (2026-08-26) — Profil/HWID sidebar + Sistem de Revocare Licențe (Windows)
Port 1:1 al `gdc-plugin-manager` (Mac) — vezi CLAUDE.md de acolo pentru
raționamentul complet. Aici, nou:
- `SupabaseConfig.cs`/`AnalyticsClient.cs` — infrastructura Supabase
  lipsea complet pe Windows înainte de asta.
- `UserProfileStore.cs` (persistă Nume/Email în `%AppData%\GDCPluginManager\profile.txt`)
  + `ProfileEditorWindow.xaml(.cs)` (fereastră modală, pattern identic cu
  `DependencyPanelWindow`/`LightboxWindow`).
- `RevocationCheck.cs` — RPC `is_license_revoked`, fail-open. Verificat
  prin CI real (`windows-latest`) — XAML compilează curat.

## Faza 4 (2026-08-26) — Update Checker popup cu Release Notes (Windows)
Port 1:1 al Mac — `MainWindow.xaml.cs`, `MaybeShowUpdatePopupAsync()`
include acum `info.Changes` în conținutul `Wpf.Ui.Controls.MessageBox` +
buton redenumit "Actualizează acum". Verificat prin CI real
(`windows-latest`) — success.

## Etapa finală (2026-08-26) — Shift UI redesign complet (Windows)
`Theme.xaml`: toate cele 8 token-uri de culoare (Brand/Ink*) înlocuite cu
paleta Shift (identică Mac/gordas.dev) — cascadează automat în toate
ferestrele prin `StaticResource`. Buton "Actualizează" (stare HasUpdate)
recolorat albastru distinct (portocaliul vechi s-ar fi confundat cu
accentul Primary, care e amber acum). Verificat prin CI real — success.
- **2026-08-26 — Bug real: verificarea MANUALĂ de update ("Caută actualizări") minea "Ești la zi" pe o versiune deja respinsă.** Reprodus live, din log-ul real trimis de Cristi: `info.Version=1.3.0, IsNewer=True`, urmat de `dismissed=1.3.0`. Cauza: `AvailableUpdate` (populat de `CheckAsync()`) e filtrat de starea de dismissal — corect pentru bannerul/pop-up-ul PASIV, dar butonul manual citea tot `AvailableUpdate`, deci o respingere veche (chiar din greșeală, un "Mai târziu" apăsat în timp ce userul explora UI-ul) făcea verificarea manuală să mintă la infinit, indiferent câte versiuni noi apăreau după aceea. **Soluție**: `UpdateChecker.LatestInfo` — populat necenzurat de dismissal, la fiecare `CheckAsync()` reușit. `CheckForUpdates_Click` citește acum `LatestInfo`, nu `AvailableUpdate`; bannerul/pop-up-ul pasiv rămân neschimbate. **Notă**: prima mea ipoteză (cache CDN pe `gordas.dev/update.json`, `max-age=600`) a fost greșită — verificat direct că serverul răspundea mereu corect; log-ul real a dovedit altceva. Adăugat și un cache-buster (`?t=<timestamp>`) pe cerere, defensiv, dar NU era cauza acestui bug.
