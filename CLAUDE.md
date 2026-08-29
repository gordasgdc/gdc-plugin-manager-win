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

  **[COMPLETARE 2026-08-27] Consimțământ obligatoriu (Consent Gate), nu
  doar text afișat.** Nu e suficient ca licența să apară — pasul trebuie
  să blocheze efectiv avansarea fără acceptare explicită:
  - **macOS (`productbuild`/Distribution.xml).** Elementul `<license
    file="License.txt" mime-type="text/plain"/>` din `Distribution.xml`
    (deja folosit de `build_installer.sh` în `gdc-plugin-manager-catalog-vendor`
    și `gdc-vault-mac`) e SUFICIENT — pagina nativă de licență a
    installer-ului macOS oferă mereu doar "Agree"/"Disagree", iar
    "Continue" nu apare fără "Agree" apăsat; nu există flag care s-o
    ocolească. Regula practică: orice `Distribution.xml` nou generat
    TREBUIE să păstreze elementul `<license>` — omiterea lui (ex. un
    installer simplificat fără pas de licență) NU e acceptabilă.
  - **Windows (Inno Setup).** Secțiunea `[Setup]` din `installer.iss`
    TREBUIE să seteze `LicenseFile=license.txt` (sau `.rtf`) — Inno Setup
    arată atunci nativ o pagină cu opțiunile radio "I accept the
    agreement" / "I do not accept", cu butonul "Next" dezactivat până la
    alegerea explicită "I accept". (Dacă vreun installer Windows ar trece
    vreodată pe NSIS în loc de Inno Setup, echivalentul e
    `!insertmacro MUI_PAGE_LICENSE` cu `MUI_LICENSEPAGE_CHECKBOX` definit,
    pentru varianta cu bifă explicită.)
  - Fișierul `license.txt`/`.rtf` folosit la acest pas trebuie să conțină
    (măcar rezumat) cele 4 puncte cheie din Termeni: statut independent
    (non-comercial), licențiere Machine ID, natura de donație a
    susținerii, garanție "as is"/limitarea răspunderii — nu doar un MIT
    License generic.

**20. Self-Updater real — obligatoriu, niciodată deschidere de browser/
GitHub (2026-08-27).** Descoperit ca bug real, repetat, pe GDC Vault (Mac
și Windows): un simplu link `releases/latest/download/...` deschis în
browser NU e suficient — utilizatorul tot ajunge pe un tab de
browser/GitHub, ceea ce Cristi consideră inacceptabil ("clientul niciodată
nu trebuie să vadă GitHub"). Orice aplicație desktop GDC (Mac/Windows) cu
proces propriu de rulat TREBUIE să implementeze un Self-Updater REAL, nu
doar un link:
- **Mac.** Descarcă `.pkg`-ul cu `URLSession.download`, cu URL-ul citit
  direct din `assets[]` al ultimului release GitHub (nu hardcodat), apoi
  îl instalează printr-un script bash elevat cu `osascript ... with
  administrator privileges` (promptul NATIV de parolă admin macOS —
  NICIODATĂ `sudo` interactiv sau Terminal vizibil), care rulează
  `installer -pkg ... -target /` și relansează aplicația singur. Vezi
  implementarea de referință `SelfUpdater.swift` (DataMover,
  `gdc-plugin-manager-catalog-vendor`, `GDCVault`).
- **Windows.** Descarcă installer-ul (`.exe`) cu `HttpClient` direct pe
  disc, redenumit cu versiunea (Regula 17), apoi îl lansează
  (`Process.Start(UseShellExecute:true)`) — fereastra NATIVĂ Inno Setup
  apare, NICIODATĂ browserul. Aplicația curentă se închide
  (`Application.Current.Shutdown()`) înainte ca userul să ajungă la pasul
  de copiere din wizard; `[Run] ... Flags: nowait postinstall
  skipifsilent` din `installer.iss` relansează aplicația după instalare —
  nu e nevoie de `AppMutex`/`CloseApplications` suplimentar. Vezi
  `SelfUpdater.cs` (`GDCPluginManagerWin`, `GDCVaultWin`).
- O fereastră minimală de progres (`UpdateProgressWindow`, text + spinner
  indeterminat) e obligatorie cât timp durează descărcarea/instalarea —
  userul nu trebuie să creadă că aplicația a înghețat.
- **WARNING permanent**: pasul efectiv de instalare (promptul de parolă
  admin pe Mac, wizardul Inno pe Windows) NU poate fi verificat automat de
  Claude — cere interacțiune fizică reală cu fereastra de sistem.
  Verificarea automată se oprește la "fișierul s-a descărcat integru,
  HTTP 200" — instalarea + relansarea efectivă TREBUIE confirmată manual,
  o dată, de Cristi, înainte ca fluxul să fie declarat complet dovedit.
- **Excepție arhitecturală, nu o abatere**: aplicații FĂRĂ proces propriu
  de rulat (plugin-uri încărcate de o gazdă terță, ex. un IOPlugin
  DaVinci Resolve) nu pot avea un "self-updater" în acest sens — rămân la
  reinstalare manuală ghidată de PDF (Regula 8), fără relansare automată.
- **Regula 13 (Update Checker) rămâne valabilă pentru DETECTAREA
  versiunii noi** (pop-up, texte, dismissal) — doar acțiunea butonului
  principal se schimbă: NU mai deschide un link, cheamă Self-Updater-ul.

**Status acest repo (2026-08-27): IMPLEMENTAT (Windows).** `src/GDCPluginManager.Client/Services/SelfUpdater.cs`. Perechea Mac trăiește în `gdc-plugin-manager-catalog-vendor`.


**21. Memory & I/O Performance — obligatoriu pentru orice aplicatie care
proceseaza date/fisiere/fluxuri mari (2026-08-27).** Descoperit ca bug real
pe DataMover: un transfer de 3 TB (SSD -> HDD) umplea RAM + swap pana la
eroarea nativa macOS "Your system has run out of application memory".
Cauza radacina reala pe Mac (Swift/DataMoverMac): bucla de citire/scriere
in bucati (`FileHandle.read(upToCount:)`) rula pe un thread de fundal FARA
`autoreleasepool` per iteratie — obiectele Objective-C (`NSData`) din
spatele fiecarui `Data` bridge-uit nu se eliberau decat la finalul
INTREGULUI job (GCD creeaza un autorelease pool per bloc dispatch-uit, nu
per iteratie de bucla), deci memoria temporara se acumula neintrerupt pe
toata durata copierii unui fisier urias sau a unui transfer intreg.
Regula, valabila pentru orice aplicatie GDC (Mac/Windows) care citeste,
scrie, copiaza sau proceseaza fisiere/fluxuri de retea/date mari:

- **Zero acumulare in memorie / streaming intai.** Interzisa incarcarea
  completa a unui fisier/array/raspuns de retea mare in RAM (fara
  `Data(contentsOf:)`, `file.read()` fara argument, `shutil.copy2` pe
  fisiere mari, liste Python/array-uri Swift care colecteaza TOATE
  intrarile unei scanari mari). Orice citire/scriere/procesare foloseste
  un buffer FIX, mic (8-32 MB implicit, configurabil - vezi mai jos), care
  se citeste, se scrie si se elibereaza pe rand.
- **Backpressure.** Daca rata de citire/procesare depaseste rata de
  scriere/iesire (SSD -> HDD, retea lenta etc.), cititorul TREBUIE sa se
  incetineasca (citire sincrona, secvential cu scrierea - fara buffer de
  "read-ahead" care ar acumula date nescrise in RAM), NU sa stocheze
  diferenta in memorie/swap. Daca aplicatia are un plafon de memorie
  configurat (vezi mai jos) si il depaseste, face o pauza scurta intre
  fisiere/blocuri pana cand memoria scade, in loc sa continue orbeste.
- **UI & State Throttling.** Interzisa pastrarea in starea aplicatiei
  (RAM) a TUTUROR obiectelor procesate pentru afisare — un istoric/log de
  sute de mii de intrari intr-un `tk.Text`/`NSTextView`/array `@Published`
  neplafonat e o scurgere de memorie reala, nu doar o "UI mare". UI-ul
  primeste doar: contoare agregate (fisiere procesate, bytes transferati,
  viteza curenta) si o fereastra plafonata cu ultimele N evenimente (ex.
  200 de linii) — restul, daca trebuie pastrat, se scrie INCREMENTAL pe
  disc (CSV/log file), nu se tine intr-o lista in memorie pana la final.
  La fel, un raport final (PDF/CSV) nu tine in RAM randul fiecarui fisier
  dintr-un transfer urias doar ca sa-l scrie o singura data la sfarsit -
  CSV-ul se scrie incremental, iar un PDF/raport vizual pastreaza doar un
  esantion plafonat (plus toate erorile).
- **Scanare/recursivitate fara memorie acumulata.** La enumerarea
  recursiva a unui folder mare, nu se construieste o lista/array cu TOATE
  intrarile deodata daca sursa poate avea sute de mii/milioane de fisiere
  — se foloseste un iterator/generator sau o scriere incrementala pe disc
  (manifest), citit apoi in loturi (batch de 500-1000), ca memoria de varf
  sa ramana plafonata indiferent de dimensiunea sursei.
- **Auto-Release & eliberare explicita in bucle mari.** Pe macOS/Swift,
  orice bucla `while`/`for` care citeste/scrie/proceseaza fisiere mari pe
  un thread de fundal (`DispatchQueue.global`) foloseste `autoreleasepool { }`
  EXPLICIT per iteratie — GCD NU dreneaza automat un pool intre iteratiile
  unei bucle sincrone in interiorul unui singur bloc dispatch-uit. Pe
  Python/alte platforme, echivalentul e eliberarea explicita a
  buffer-elor/resurselor unmanaged (context manageri `with`, `close()`
  explicit) - nu te baza pe garbage collection amanata pentru resurse care
  cresc proportional cu volumul de date procesat.
- **Resource Limits & configurabilitate.** Orice aplicatie care proceseaza
  volume mari de date expune in Setari: (a) dimensiunea buffer-ului de
  citire/scriere (ex. 4/8/16/32/64 MB, implicit 8 MB), si (b) un plafon
  orientativ de memorie a aplicatiei (ex. 512 MB / 1 GB / 2 GB / 4 GB /
  fara limita), peste care se aplica backpressure-ul descris mai sus.
  Plafonul e o limita ORIENTATIVA la nivel de proces (nu un cgroup impus
  de OS) - scopul e sa incetineasca sursa cand memoria creste anormal, nu
  sa garanteze un maxim absolut.
- **Implementare de referinta**: `DataMover` — `IOSettings.swift` +
  fix-ul de `autoreleasepool` din `copyFileCancelable`/`genericHash`
  (`OffloadEngine.swift`, Mac), si `core/io_settings.py` +
  `scan_files_streaming`/`iter_manifest_batches` + raport CSV incremental
  (`core/offload_engine.py`, Windows/Python). Orice aplicatie GDC noua sau
  modificata care atinge fisiere/fluxuri mari respecta acest standard de
  la urmatoarea ei actualizare, nu doar DataMover.

**Status acest repo (2026-08-28, verificat): NU SE APLICA ACUM (buffer configurabil), de verificat un detaliu.** Auditat la cererea lui Cristi — `InstallManager.cs` copiaza LUT/DCTL/PowerGrade cu `File.Copy` (streaming la nivel de OS, nu incarca fisierul in memoria aplicatiei) - fisiere tipic KB-cateva MB, fara risc de memorie la volumul actual. **De verificat**: `FetchPrivateFileDataAsync` intoarce `byte[]` (incarca fisierul INTREG in memorie) - daca se adauga vreodata un tip de asset mai mare (ex. un preset video), migreaza la `Stream`/citire in bucati inainte sa devina un risc real, dupa modelul DataMover.

**22. `PlatformTarget` explicit obligatoriu pentru orice proiect .NET/WPF cu
pachete NuGet native (2026-08-28).** Gasit pe DataMover (client WPF): un
`.csproj` implicit "Any CPU" ruleaza, pe host-ul Windows al lui Cristi
(Parallels pe Mac Apple Silicon), ca `win-arm64` - iar biblioteci cu
binare native (QuestPDF/Skia, si potential altele similare) NU au build
pentru arhitectura asta, cazand tacut cu `DllNotFoundException`/
`TypeInitializationException` doar la runtime, niciodata la `dotnet build`.
Orice `.csproj` nou (sau existent, la prima dependinta nativa adaugata) din
`GDCVaultWin`/`GDCPluginManagerWin`/`DataMover`/orice client Windows viitor
seteaza explicit `<PlatformTarget>x64</PlatformTarget>` - Windows 11 ARM
ruleaza procesul x64 prin emulatie nativa a OS-ului, deci functioneaza
identic pe Windows x64 real si pe ARM64/Parallels. Nu te baza pe "Any CPU"
doar pentru ca merge la compilare.

**23. Garda obligatorie impotriva `dist/` detinut de root, in orice
`build_app.sh` Mac (2026-08-28).** Bug real, repetat de mai multe ori pe
DataMover in aceeasi sesiune (cauza exacta neconfirmata - posibil o
instalare de test cu `sudo installer -pkg ... -target /` care a atins
accidental folderul local): `dist/<App>.app` ramas detinut de `root:wheel`
dintr-un build anterior face ca `rm -rf "dist"` de la inceputul scriptului
sa esueze partial, tacut, cu o gramada de "Permission denied" greu de
gasit in mijlocul unui log lung. Orice `build_app.sh` din ecosistem
(DataMover, GDCVault, CursorPro, gdc-plugin-manager-catalog-vendor, orice
build Mac viitor) verifica ACEST lucru explicit INAINTE de `rm -rf`, cu un
mesaj clar si actionabil (`sudo rm -rf $(pwd)/dist`, de rulat manual O
SINGURA DATA de Cristi - Claude nu poate rula `sudo`), in loc sa lase
`rm -rf` sa esueze criptic:
\`\`\`bash
if [ -d "dist" ] && ! [ -w "dist" ] || find dist -maxdepth 2 -user root -print -quit 2>/dev/null | grep -q .; then
    echo "EROARE: 'dist/' contine fisiere detinute de root. Ruleaza manual:" >&2
    echo "    sudo rm -rf \$(pwd)/dist" >&2
    exit 1
fi
\`\`\`
Practic, inaintea oricarui `release.sh`: `ls -la mac-native/dist` (listare
COMPLETA, nu trunchiata cu `head`) - o listare trunchiata poate rata
`<App>.app` daca sorteaza dupa alte fisiere (`.pkg`/`.zip`), dand o
verificare falsa de "curat".

**24. Standard UI obligatoriu: Setare explicită "Mărime Text" + Layout
robust la redimensionare (2026-08-29).** Completare la Regula 18 — găsit pe
GDC Plugin Manager (Mac): un bug real de layout la resize RAPID al
ferestrei (blocul de profil/footer din sidebar rămânea temporar suprapus
peste conținutul de deasupra) cauzat de `.safeAreaInset(edge:)` atașat
DIRECT pe un `List`/`ScrollView` — la resize rapid pe macOS, content-insetul
intern al listei nu se resincronizează mereu instant cu safe-area-ul
suprapus (bug de sincronizare AppKit/SwiftUI, nu o presupunere). Regulă
practică, valabilă pentru orice fereastră GDC (Mac/Windows) cu o zonă
fixă (footer/header) lângă o listă/grid scrollabilă:
- **Niciodată `.safeAreaInset` direct pe un `List`/`ScrollView` pentru un
  element care trebuie să rămână mereu vizibil și nesuprapus** — pune
  lista și elementul fix ca FRAȚI într-un `VStack`/`Grid` simplu (cu
  `Divider()` între ele, dacă are sens vizual). Layout-ul calculat direct
  de container e mereu sincron, cadru cu cadru, spre deosebire de
  safe-area-ul suprapus peste scroll.
- **Fereastra principală rămâne liber redimensionabilă** (Regula 18), dar
  cu `minWidth`/`minHeight` verificate să nu lase conținutul ilizibil sub
  acel prag — nu doar prezente, ci suficient de generoase pentru sidebar-ul
  cu cele mai multe secțiuni al aplicației respective.
- **Setare explicită "Mărime Text" (Mic/Normal/Mare/Foarte mare) e acum
  standard**, alături de selectorul de temă din Regula 18 — pe SwiftUI/Mac,
  prin infrastructura NATIVĂ de accesibilitate (`dynamicTypeSize()` aplicat
  la rădăcina ferestrei principale, NU un multiplicator brut de font — text
  semantic (`.font(.headline)`/`.caption`/etc) + `dynamicTypeSize` garantează
  reflow corect, spre deosebire de o scalare custom care poate tăia conținut
  în frame-uri fixe). Pe Windows/WPF, echivalentul e un `FontSizeConverter`/
  resursă de `FontSize` global legată de o setare persistată (`Registry`/JSON),
  aplicată la nivelul `Application.Resources`. Persistat local, aplicat
  imediat, fără repornire — la fel ca selectorul de temă.
- Referință de implementare: `TextScalePreference`/`TextScaleManager`
  (`Sources/GDCPluginManagerCore/AppTheme.swift`, `gdc-plugin-manager-catalog-vendor`)
  + restructurarea `NavigationSplitView`/`List` din `ContentView.swift`
  (același repo) — port-ul pe orice altă aplicație GDC (Mac/Windows) cu
  panou lateral fix trebuie verificat la fel pentru acest pattern.

**25. `CHANGELOG.md` obligatoriu la fiecare bump de versiune + Log de
Diagnostic permanent, nu print-uri temporare (2026-08-29).**
- **`CHANGELOG.md`** (rădăcina fiecărui repo) — separat de jurnalul tehnic
  detaliat din acest fișier (CLAUDE.md păstrează deciziile/motivele/
  pitfall-urile complete; `CHANGELOG.md` e un rezumat SCURT, orientat spre
  ce s-a schimbat pentru utilizator, o intrare per versiune/dată, ușor de
  scanat rapid fără să citești tot jurnalul). Actualizează-l în ACELAȘI
  commit ca bump-ul de versiune — la fel de obligatoriu ca bump-ul însuși.
  Dacă repo-ul nu are încă `CHANGELOG.md`, creează-l la prima actualizare
  viitoare (nu aștepta o cerere explicită).
- **Log de Diagnostic PERMANENT** (`DiagnosticLog.write(tag:, message:)` —
  Mac: `GDCPluginManagerCore/DiagnosticLog.swift`, `%TEMP%/gdcpm-crash.log`;
  Windows: `DiagnosticLog.cs`, echivalent) — pentru orice flux nou cu
  potențial de eșec silențios (fetch de rețea, decodare, publicare/commit
  git, încărcare de imagine/resursă asincronă): adaugă apeluri de log DE LA
  ÎNCEPUT, nu abia când apare un bug de investigat. Motiv real, găsit chiar
  în această sesiune: bug-ul cu filigranul sezonier care nu se încărca
  niciodată a fost diagnosticat DOAR după ce am adăugat manual print-uri
  temporare și am rulat aplicația din Terminal — cu logul permanent deja
  acolo, diagnosticul ar fi durat un fișier citit, nu o sesiune de
  reproducere manuală. Un singur fișier de log, comun tuturor componentelor
  aceleiași aplicații (Client + Furnizor, dacă există) — userul trimite UN
  fișier, nu trebuie să știe care componentă a scris eroarea.

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
- **2026-08-29 — Badge OS: `DesktopMac24`/`DesktopTower24`/`ArrowSync24` (Fluent `ui:SymbolIcon`), nu emoji 🍎/🪟/🔄.** Port 1:1 al fix-ului de pe Mac (Cristi: "simbolurile de măr... nu-mi place, prefer SVG... impecabil, profesionist"). `SupportedOSExtensions.BadgeSymbol()` (înlocuiește `BadgeEmoji()`) + `Converters/SymbolNameConverter.cs` (nou — `Enum.TryParse<SymbolRegular>`, fallback `Circle24`) + `MainWindow.xaml` (`ui:SymbolIcon Symbol="{Binding OSBadgeSymbol, Converter={StaticResource SymbolName}}"` în loc de `TextBlock` cu emoji). Cele 3 nume de simbol confirmate PREZENTE prin `strings Wpf.Ui.dll` (vezi pitfall 2026-08-24 despre absență-nu-e-dovadă-dar-prezența-da). Versiune `1.4.0`→`1.5.0`. **Verificat**: `dotnet build` (C# only) — 0 erori; XML validat manual well-formed (XAML nu compilează pe Mac).
- **2026-08-26 — Bug real: verificarea MANUALĂ de update ("Caută actualizări") minea "Ești la zi" pe o versiune deja respinsă.** Reprodus live, din log-ul real trimis de Cristi: `info.Version=1.3.0, IsNewer=True`, urmat de `dismissed=1.3.0`. Cauza: `AvailableUpdate` (populat de `CheckAsync()`) e filtrat de starea de dismissal — corect pentru bannerul/pop-up-ul PASIV, dar butonul manual citea tot `AvailableUpdate`, deci o respingere veche (chiar din greșeală, un "Mai târziu" apăsat în timp ce userul explora UI-ul) făcea verificarea manuală să mintă la infinit, indiferent câte versiuni noi apăreau după aceea. **Soluție**: `UpdateChecker.LatestInfo` — populat necenzurat de dismissal, la fiecare `CheckAsync()` reușit. `CheckForUpdates_Click` citește acum `LatestInfo`, nu `AvailableUpdate`; bannerul/pop-up-ul pasiv rămân neschimbate. **Notă**: prima mea ipoteză (cache CDN pe `gordas.dev/update.json`, `max-age=600`) a fost greșită — verificat direct că serverul răspundea mereu corect; log-ul real a dovedit altceva. Adăugat și un cache-buster (`?t=<timestamp>`) pe cerere, defensiv, dar NU era cauza acestui bug.

## Paritate v2.0 cu Mac — cele 9 etape (2026-08-29, Windows display-only)
Context: `gdc-plugin-manager-catalog-vendor` (Mac) a primit 9 etape de
upgrade v2.0; Windows rămăsese la 1.5.0, fără niciuna. Windows NU are
aplicație Furnizor — deci se portează DOAR modelele de date (deserializare
identică, retrocompatibilă) + AFIȘAREA/filtrarea/licențierea din Client.
Etapa 7 (filtrare avansată + export email pe loturi) e N/A: e exclusiv
Furnizor, confirmat direct de jurnalul de pe Mac.

- **[CORECȚIE IMPORTANTĂ a unui pitfall vechi] `dotnet build` pe macOS
  COMPILEAZĂ acum XAML-ul.** Pitfall-ul din 2026-08-23 („`PresentationBuildTasks`
  e Windows-only, build-ul verde pe Mac nu e o dovadă") NU mai e adevărat
  cu SDK-ul .NET 10.0.400 instalat pe acest Mac. Verificat DIRECT, nu
  presupus: `MainWindow.baml` + `MainWindow.g.cs` sunt regenerate la
  fiecare build în `obj/Debug/net8.0-windows/`, iar o eroare XAML
  introdusă deliberat (element rădăcină în plus) a fost prinsă la
  compilare cu `error MC3000: 'There are multiple root elements.'`.
  Deci XAML-ul din etapele de mai jos e validat de compilator, nu doar
  "XML well-formed manual" ca la etapele vechi. **Nu șterge pitfall-ul
  vechi din istoric** (regula append-only) — dar de-acum e ÎNVECHIT pe
  această mașină; pe un SDK mai vechi s-ar putea reactiva, deci verifică
  prezența `MainWindow.baml` înainte de a te baza pe el.

### Etapa 1 — Căutare fuzzy globală + istoric + filtru OS
`FuzzySearch.cs` (Core, nou) — port 1:1 al `FuzzySearch.swift`: substring
pe text normalizat + Levenshtein mărginit per-cuvânt (prag 1 pentru
interogări ≤4 caractere, altfel 2). `Normalize` folosește
`NormalizationForm.FormD` + eliminarea `NonSpacingMark` + `ToLowerInvariant`
(echivalentul `folding(.diacriticInsensitive, .caseInsensitive)` din Swift).
**`ToLowerInvariant`, NU `ToLower()`** — pe o mașină cu locale turcească
`ToLower()` mapează "I"→"ı" și căutarea s-ar rupe silențios.

`SearchHistoryStore.cs` (Core, nou) — max 8, fără duplicate
(case-insensitive), cea mai recentă prima. Mac folosește `UserDefaults`;
aici e un JSON în `%AppData%\GDCPluginManager\search-history-global.json`,
același tipar ca `licenses.json`/`catalog-cache.json`. Stare 100% locală.

Client: bara de căutare NU mai e legată de pagina de catalog — e globală,
vizibilă pe orice rubrică. `MainViewModel.ContentPage` (nou) e ce se randă
efectiv: `CurrentPage` normal, `SidebarPage.GlobalSearch` cât timp câmpul e
nevid. Toate cele 10 panouri de conținut din `MainWindow.xaml` s-au mutat de
pe `CurrentPage` pe `ContentPage` — o singură condiție le ascunde pe toate
în timpul căutării, fără s-o dubleze pe fiecare. `CurrentPage` rămâne
neatinsă, deci sidebar-ul își păstrează selecția și revenirea la golirea
câmpului e instantanee. Rezultatele globale acoperă toate cele 8 colecții
existente, fiecare secțiune randată doar dacă are potriviri
(`NonZeroToVisibilityConverter`, nou). Cardurile sunt `DataTemplate`-urile
deja existente (rezolvate după `DataType`) — zero UI duplicat, ca pe Mac.

Filtru OS (Toate/Mac/Windows) — `OSFilter` (enum nou) + `MatchesOS`.
`CrossPlatform` apare la ORICE filtru (chiar rulează pe ambele platforme).
**Notă de scop, nu omisiune**: doar `PluginItem` poartă `supportedOS` în
model (la fel ca pe Mac) — Cursuri/Materiale/Evenimente/Magazine/Service/
Aplicații/Audio nu au câmpul deloc, deci sunt tratate implicit ca
`CrossPlatform` și apar la orice filtru.

**Simplificare deliberată față de Mac**: istoricul e un rând de "chip"-uri
sub bară (vizibil doar când câmpul e GOL), nu un dropdown de sugestii
ancorat — același conținut, fără complexitatea de focus/popup din WPF.
Enter salvează termenul în istoric, Escape golește câmpul; filtrarea în
sine e live la fiecare tastă (altfel istoricul s-ar umple cu prefixe).

**Verificat**: `dotnet build` — 0 erori, 0 avertismente, XAML compilat real.

### Etapa 2 — Linkuri multiple/Social pe produse + Resurse Download (LUT/SFX/VFX/Plugin)
`SocialLinks` (Core, nou) — Facebook/YouTube/Instagram/TikTok, toate
opționale. Cheile JSON sunt fixate EXPLICIT cu `[JsonPropertyName]`
(`facebookURL` etc.), nu lăsate pe seama lui `PropertyNameCaseInsensitive`:
acela ajută doar la CITIRE, iar la scriere System.Text.Json ar emite
`FacebookURL` (PascalCase), divergent de ce scrie Furnizorul Mac. Windows nu
publică azi, dar modelul rămâne simetric.
`PluginItem` capătă `PurchaseURL`/`DemoURL`/`SocialLinks` (Etapa 2) —
retrocompatibil (`TryGetProperty` → null). Client: rând de iconițe pe card,
fiecare afișată DOAR dacă linkul ei e completat (`ExtraLinkButtonStyle`, nou
în `Theme.xaml`).

`DownloadCategory` (lut/sfx/vfx/plugin) + `DownloadableResource` (Core, noi)
— model 1:1 pe `AudioTrack` + linkuri/social + `SupportedOS` + licențiere
completă. 4 rubrici noi în sidebar (grup "RESURSE DOWNLOAD", lângă Audio,
ca pe Mac) + 4 pagini + `DownloadResourceViewModel`/`DataTemplate` (mirror
cardul Audio + badge/licențiere ca la Produse). A 9-a colecție în căutarea
globală.

**CAPCANA CRITICĂ, respectată și verificată**: `DownloadableResource.IsFree`
decodează implicit **TRUE** când cheia lipsește, spre deosebire de
`PluginItem.IsFree` care decodează **FALSE**. De-asta `DownloadableResource`
are converter custom (nu deserializare implicită). Inversarea ar transforma
orice resursă publicată înainte de acest câmp într-un "produs plătit fără
licență activabilă". Ambele comportamente sunt verificate direct (vezi mai
jos), nu doar presupuse din citirea Swift-ului.

Licențiere: `LicenseManager.IsUnlocked(DownloadableResource)` (overload nou)
refolosește ACELAȘI `_licensedProducts` (cheiat generic după ID de produs) și
același `RevocationCheck` — zero infrastructură nouă, port 1:1 al deciziei de
pe Mac. `MainViewModel` adaugă ID-urile resurselor la `allProductIds`
(candidații la activare) și la `productName` — **fără asta o resursă plătită
ar fi fost imposibil de deblocat**, deși cardul ar fi arătat corect.

**Verificat**: `dotnet build` — 0 erori, 0 avertismente (XAML compilat real).
Plus un harness aruncabil (în scratchpad, NU în repo — nu există
infrastructură de testare aici și nu s-a adăugat una) care rulează 25 de
verificări pe `GDCPluginManager.Core` real: ambele default-uri opuse de
`isFree`, `supportedOS` implicit, chei camelCase pe `socialLinks`,
retrocompatibilitatea `Catalog` (colecție absentă → listă goală), și
semantica `FuzzySearch` (diacritice în ambele sensuri, toleranță la typo,
prag mai strict pe cuvinte scurte). Toate 25 au trecut.

**Notă de metodă**: numele de simboluri Fluent (`Eyedropper24`, `Sparkle24`,
`PuzzlePiece24`, `MusicNote224`, `Cart24`, `Play24`, `Share24`, `Video24`,
`Camera24`, `Info24`) au fost confirmate punând COMPILATORUL să le valideze —
un `ui:SymbolIcon Symbol="..."` literal e un membru de enum, deci un nume
inexistent oprește build-ul. Metodă mult mai sigură decât `strings Wpf.Ui.dll`
(care are false negative, vezi pitfall 2026-08-24).

### Etapa 3 — "Aplicațiile Mele" (detectare prin Registry)
`MyAppsService.cs` + `MyAppsViewModel.cs` (Client, noi) — sidebar nou
"Aplicatiile Mele", lângă Aplicații.

**Diferență reală de platformă**: Mac folosește
`NSWorkspace.urlForApplication(withBundleIdentifier:)`. Windows n-are
echivalent — sursa de adevăr e cheia de dezinstalare scrisă de Inno Setup:
`HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\<AppId>_is1`.
Căutăm în TREI locuri (HKLM view 64, HKLM view 32 / `WOW6432Node`, HKCU),
fiindcă depinde cum a fost instalată aplicația; fallback pe Program Files
pentru o copie dezarhivată manual. Versiunea instalată e `DisplayVersion` din
acea cheie (NU versiunea din fișierul .exe — ar putea diferi de ce a
înregistrat installer-ul).

**3 aplicații, nu 4 — verificat, nu presupus.** Mac listează DataMover,
CursorPro GDC, GDC Vault, MediaFlow Monitor. Inspectat direct `~/Developer`:
`CursorPro` are DOAR `Package.swift`/`.icns`/`build_app.sh` — niciun
`.csproj`, `.iss` sau folder Windows. **Nu există build Windows de detectat,
deci e EXCLUS deliberat.** Celelalte trei au `installer.iss` real, iar
`AppId`/`AppName`/`ExeName`/`DefaultDirName` din cod sunt copiate VERBATIM de
acolo, nu ghicite:
- DataMover — `{A4E1C3F0-2F0F-4B0E-9C1A-DATAMOVERSETUP1}`, `DataMover.exe`
- GDC Vault — `{E4A9C2D1-7B3F-4E5A-9F0C-GDCVAULT00001}`, `GDCVault.exe`
- MediaFlow Monitor — `{A3F1D9E4-6B27-4C88-9A45-MEDIAFLOWMON01}`, `MediaFlowMonitor.exe`

**Endpoint-uri de versiune verificate LIVE** (nu presupuse): DataMover
`api.github.com/repos/gordasgdc/datamover/releases/latest` → HTTP 200,
`v2.7.1`; GDC Vault `.../gdc-vault-win/...` → HTTP 200, `v0.5.4`; MediaFlow
Monitor are `update.json` propriu (`gordas.dev/media-flow-monitor/update.json`)
→ HTTP 200, `1.8.0`, cu `download_url.windows` prezent (confirmă build Windows).
Cele două surse diferite sunt portate ca atare (`VersionSourceKind`), exact ca
pe Mac.

**Capcană evitată**: `tag_name` de pe GitHub poartă prefixul `v` în tot
ecosistemul GDC, dar `update.json`/`AssemblyVersion` nu — fără
`VersionCompare.NormalizeTag`, "v2.7.1" s-ar parsa ca `0.7.1` și fiecare
aplicație ar fi părut MEREU la zi (badge-ul n-ar fi apărut niciodată).
Badge-ul "ACTUALIZARE" apare doar când verificarea a REUȘIT și versiunea
publicată e strict mai nouă — la eșec de rețea nu apare nimic, nu un badge
fals pe o informație pur opțională.

**Refactor conex, nu gold-plating**: comparația de versiuni era `private` în
`UpdateChecker`; extrasă în `VersionCompare` (Core) și refolosită de ambele —
o a doua copie ar fi putut diverge tăcut de cea folosită la self-update.
`HttpClientFactory` a devenit `public` (era `internal`): `api.github.com`
răspunde **403 fără User-Agent**, iar un `HttpClient` gol creat în Client ar
fi ratat antetul.

Scurtături personalizate (`CustomLauncherStore`) — `OpenFileDialog` pe `.exe`,
persistate în `%AppData%\GDCPluginManager\custom-launchers.json` (pe Mac:
`fileImporter` + `UserDefaults`).

**Verificat**: `dotnet build` — 0 erori, 0 avertismente (XAML compilat real).
**Neverificabil de aici, rămâne pentru testul pe Windows**: citirea efectivă
a cheilor de Registry — acest Mac n-are Registry. Logica e scrisă defensiv
(orice vedere inaccesibilă e sărită, nu oprește căutarea), dar detectarea în
sine TREBUIE confirmată o dată pe mașina de test.

### Etapa 4 — Scheduling pe toate modelele + Oferte Parteneri + sumă promoțională

**CAPCANA DE ENCODING — verificată pe catalogul LIVE, nu presupusă.**
Furnizorul Mac serializează cu `JSONEncoder()` fără `dateEncodingStrategy`.
Strategia implicită a lui Foundation (`.deferredToDate`) scrie un `Date` ca
**NUMĂR** — secunde (cu fracțiuni) de la **2001-01-01 00:00:00 UTC**
(referința `NSDate`/Core Data). NU e ISO-8601 și NU e epoch Unix.

Dovadă directă, luată cu `curl` din `gordas.dev/catalog.json` (2026-08-29):
`"scheduling":{"startDate":809661338.592533,"endDate":815021738.592533}`
- citit ca epoch Unix → **1995-08-29** (absurd)
- citit cu referința 2001 → **2026-08-29** (exact ziua curentă)

`SwiftDateJsonConverter` (Core, nou) face conversia explicit
(`new DateTime(2001,1,1,0,0,0,DateTimeKind.Utc).AddSeconds(v)`), citind
**Double** (valorile au fracțiuni de secundă, nu sunt întregi). O legare naivă
la `DateTimeOffset`/ISO ar fi plasat TĂCUT toate datele în 1995, `IsActiveNow`
ar fi fost false peste tot, și **fiecare element programat ar fi devenit
invizibil în client, fără nicio eroare**. Converterul acceptă defensiv și un
string ISO-8601, ca o eventuală schimbare viitoare de partea Furnizorului să
nu spargă clienții deja instalați.

`Scheduling` (Core, nou) + `SchedulingExtensions.IsVisibleNow()` (un singur
loc care știe regula "fără scheduling = mereu vizibil"). Adăugat pe TOATE
modelele: `PluginItem`, `AppLink`, `AudioTrack`, `Course`,
`EducationalResource`, `Event`, `PartnerStore`, `ServiceCenter`,
`DownloadableResource`, `PartnerOffer`. Filtrarea se aplică la popularea
fiecărei colecții în `RebuildFromCatalog` — deci acoperă automat și căutarea
globală (care derivă din aceleași colecții), fără o a doua listă de condiții.

`PartnerOffer` (Core, nou) + sidebar + grid + a 10-a colecție în căutarea
globală. **Decizie de scop explicită, portată de pe Mac**: badge-ul ROȘU cu
limbaj de discount/procent există DOAR pe acest card — e o relație comercială
cu un brand terț. `discountText` e text liber (acoperă și "2 la preț de 1").
Cod de cupon cu buton de copiere.

`PromoPriceEUR`/`EffectivePriceEUR`/`IsPromoActive` pe `PluginItem` și
`DownloadableResource`. **CONFORMITATE (Regula 3, Partea 1)**: pe conținut
PROPRIU GDC suma rămâne DONAȚIE — se afișează suma veche TĂIATĂ + badge
**"Susținere promoțională"**, NICIODATĂ "reducere"/"discount"/"-X% OFF".
Promoția e activă doar cât timp `scheduling` e activ (o promo fără scheduling
NU se aplică — verificat, identic cu Mac). Mesajul WhatsApp de deblocare
folosește `EffectivePriceDisplay`, deci suma promoțională activă ajunge automat
în mesaj — altfel userul ar fi cerut deblocarea la suma veche, mai mare, în
plină promoție.

**Verificat**: `dotnet build` — 0 erori, 0 avertismente. Harness-ul din
scratchpad extins la 24 de verificări, dintre care decodarea
**catalogului LIVE real** descărcat de pe gordas.dev: 4 produse, 7 evenimente,
1 ofertă parteneră, scheduling-ul evenimentului real citit corect ca
`2026-08-29`, plus round-trip de dată, ferestre expirate/viitoare/deschise, și
comportamentul prețului promoțional în toate cele 4 combinații. Toate au trecut.

**Observație reală despre catalogul live** (nu un bug al portului): oferta
parteneră și pachetul publicate acum au ferestre de scheduling DEJA EXPIRATE
(s-au încheiat la 23:57 și 01:10, ora curentă 03:43), iar oferta e o intrare de
test (`brandName: "test"`, `discountText` gol). Cu filtrarea corectă, ele NU
apar în client — comportament corect, de așteptat, nu o regresie.

### Etapa 5 — Google Maps din adresă + memorare folder de descărcare
`MapsLink` (Core, nou) — deep-link către endpoint-ul public de căutare Google
Maps (`api=1&query=<text>`), fără cheie API. `PartnerStore` și `ServiceCenter`
capătă câmpul nou `Address` (opțional, distinct de `WebsiteURL`/`Url`); `Event`
folosește `Location`-ul deja existent (fără câmp nou), exact ca pe Mac.
`MapsUrl` computed pe toate trei; butonul nu se randează DELOC când e null (nu
apare dezactivat).

**Stoplist de termeni non-fizici** (`online`, `webinar`, `virtual`, `remote`,
`la distanta`, `distanta`, `zoom`, `internet`, `n/a`, `-`), comparat pe textul
NORMALIZAT prin exact aceeași `FuzzySearch.Normalize` din Etapa 1 — deci
"Online", "ONLINE", "  online  " și "la distanță" (cu diacritice) sunt toate
prinse. Verificat cu 13 cazuri, inclusiv contra-exemplul "Online Studio
Bucuresti", care NU trebuie prins de stoplist (e o adresă reală care se
întâmplă să înceapă cu acel cuvânt — stoplist-ul compară textul ÎNTREG, nu un
prefix).

**BUG REAL GĂSIT ȘI REPARAT în timpul verificării** (nu ar fi apărut la
compilare): comanda de deschidere folosea `url.ToString()`, iar
`Uri.ToString()` întoarce forma **DEZESCAPATĂ** — `?query=Strada Victoriei 10,
București`, cu spații brute și diacritice ne-encodate, exact așa cum ar fi
ajuns la `ShellExecute`. Fix: `url.AbsoluteUri`, care păstrează
percent-encoding-ul corect
(`query=Strada%20Victoriei%2010%2C%20Bucure%C8%99ti`). Regulă practică nouă:
**pentru orice `Uri` trimis către `Process.Start`, folosește `AbsoluteUri`,
niciodată `ToString()`.**

`DownloadLocationStore` (Core, nou) — memorează per resursă folderul unde
userul și-a salvat descărcarea. Stare 100% locală
(`%AppData%\GDCPluginManager\download-locations.json`), NU parte din
catalog.json. `Get` verifică `Directory.Exists` înainte să întoarcă calea, ca
să nu afișeze o cale moartă și un buton "Deschide" care eșuează. Pe card:
"Unde l-ai salvat?" → alegere folder → apoi calea + "Deschide"/"Schimbă"/
"Uită". Rândul apare doar pe resurse DEBLOCATE, ca pe Mac.

**Simplificare față de plan**: s-a folosit `Microsoft.Win32.OpenFolderDialog`
(nativ în WPF din .NET 8) în loc de `FolderBrowserDialog` — acela ar fi cerut o
referință la Windows Forms doar pentru un selector de folder.

**Verificat**: `dotnet build` — 0 erori, 0 avertismente; 38 de verificări în
harness, toate trecute.

### Etapa 6 — Filigran sezonier (SVG randat real, nu fallback tăcut)
`Catalog.SeasonalBackground` (Core, nou) + `SeasonalBackgroundUrl` (același
sistem hibrid cale-relativă/URL-extern ca `CoverImage`).
`SeasonalBackgroundLoader.cs` (Client, nou) + strat de fundal în
`MainWindow.xaml`: imagine mare (480x480 bounding box), **opacitate 7%**, ÎN
SPATELE conținutului (declarat primul în Grid + `Panel.ZIndex="-1"`), cu
`IsHitTestVisible="False"` ca să nu înghită niciun click destinat cardurilor.

**LIBRĂRIE NOUĂ: `SharpVectors.Reloaded` 1.8.5 — decizie documentată, nu
preferință.** WPF **nu are decodor SVG nativ** (`BitmapImage` acceptă doar
BMP/GIF/ICO/JPEG/PNG/TIFF/WMP), iar filigranul din producție **este SVG** —
verificat live: `covers/seasonal/background.svg?v=27081ef5`, HTTP 200,
`content-type: image/svg+xml`, 33 KB. Fără librărie ar fi eșuat TĂCUT și n-ar
fi apărut niciodată — exact clasa de bug raportată pe Mac cu `AsyncImage`.

**De ce SharpVectors și NU Svg.Skia**: `Svg.Skia` depinde de SkiaSharp, care
livrează **binare NATIVE per arhitectură**. Regula 22 (Partea 1) documentează
un bug REAL de pe DataMover: pe host-ul Windows al lui Cristi (Parallels pe Mac
Apple Silicon) procesul rulează ca `win-arm64`, iar pachetele Skia native n-au
build pentru acea arhitectură — cad tăcut cu `DllNotFoundException` doar la
RUNTIME, niciodată la `dotnet build`. Ar fi fost exact aceeași clasă de eșec
silențios pe care etapa asta o repară. `SharpVectors.Reloaded` e **100%
managed** — verificat direct: toate assembly-urile din pachet sunt
`Mono/.Net assembly`, iar pachetul **nu are folder `runtimes/`** cu binare
native. Rulează identic pe x64 și pe ARM64 emulat, și randează în
`DrawingImage` WPF, deci filigranul rămâne **vectorial**, nu rasterizat.

Detectarea SVG se face **după conținut** (`<svg` în primii 512 bytes), nu doar
după extensie — valoarea reală din producție are query (`?v=27081ef5`), iar un
URL extern poate să n-aibă deloc extensie. Fallback automat pe decodor raster
pentru PNG/JPG. Orice eșec (rețea, format, SVG invalid) → `null` → stratul pur
și simplu nu se randează; filigranul e decorativ, nu produce nicio eroare
vizibilă.

**Ce e verificat și ce NU**: verificat — pachetul e pur managed; asset-ul live
e SVG real, XML well-formed, compus DOAR din `<g>`/`<path>` (subsetul cel mai
simplu, fără text/gradienți/filtre/CSS); codul compilează contra API-ului
`FileSvgReader`/`WpfDrawingSettings` folosit. **NEVERIFICABIL de pe Mac**:
randarea efectivă — SharpVectors e o librărie WPF, deci decodarea nu poate
rula decât pe Windows. Rămâne de confirmat vizual, o dată, pe mașina de test.

### Etapa 7 — N/A pe Windows (SKIP explicit, nu omisiune)
Etapa 7 de pe Mac (filtrare avansată + export email pe loturi pentru BCC din
`SalesHistoryView`) e **exclusiv Furnizor**. `GDCPluginManagerWin` nu are
aplicație Furnizor — publicarea și CRM-ul rămân doar pe Mac. Jurnalul de pe Mac
o spune direct: "TODO paritate: `GDCPluginManagerWin` nu are Furnizor — nu se
aplică." Nimic de portat.

### Etapa 8 — Cache offline
**Constatare (verificată, nu presupusă)**: `CatalogService.cs` avea DEJA cache
offline complet funcțional — `catalog-cache.json` în `%AppData%`, scris la
fiecare fetch reușit, citit în constructor, cu fallback automat la eșec de
rețea (`if (Items.Count > 0) return;` păstrează ce era deja încărcat). Nimic de
adăugat acolo.

**Gap real, același ca pe Mac**: filigranul sezonier (Etapa 6) se descărca de
la zero la fiecare pornire, fără persistare — deci **offline dispărea complet**,
deși restul aplicației funcționa din cache. Adăugat cache pe disc
(`%AppData%\GDCPluginManager\seasonal-background-cache`), după exact același
model ca `catalog-cache.json`: la succes salvează bytes, la eșec de rețea
încearcă ultima variantă salvată.

**Detaliu deliberat**: se salvează în cache DOAR bytes care s-au și DECODAT cu
succes. Altfel un răspuns corupt (sau o pagină HTML de eroare servită cu 200)
ar fi fost cache-uită și reîncercată la infinit, fără ca filigranul să apară
vreodată.

### Etapa 9 — Pachete / Bundle-uri
`BundleItemKind` (6 tipuri: `product`/`download`/`course`/`audio`/`app`/
`material`), `BundleItemRef`, `ProductBundle` (Core, noi) +
`Catalog.ProductBundles` (default `[]`). Sidebar nou "Pachete" (grup
COMUNITATE) + grid + a 11-a colecție în căutarea globală.

**Decizie arhitecturală portată ca atare**: pachetul e DOAR un construct de
prezentare/marketing (grupare + preț total afișat), **NU un mecanism nou de
licențiere** — achiziția rămâne prin WhatsApp, reutilizând exact tiparul din
`ProductViewModel.Buy()` (mesaj + `MachineID.Display`), doar cu lista
conținutului inclus adăugată în mesaj. Furnizorul generează în continuare,
manual, câte o licență per produs inclus. Oferte Parteneri (terți) și
Evenimente (informativ) rămân EXCLUSE din tipurile combinabile, ca pe Mac.

Produsele incluse se rezolvă **live din catalog** la construirea cardului. **Un
ID care nu mai există (produs retras între timp) e omis SILENȚIOS** — cardul nu
crapă și nu afișează un rând gol; pachetul rămâne utilizabil cu ce a mai rămas.

Suma individuală (afișată tăiată lângă prețul pachetului) însumează doar
elementele care AU preț propriu în model: produse, resurse download, cursuri.
Audio/Aplicații/Materiale n-au preț în model — apar în lista de conținut dar NU
contribuie la sumă, exact ca pe Mac. Pentru un curs (care are mai multe opțiuni
de preț) se ia **cea mai mică** — estimare conservatoare, ca suma tăiată să nu
fie niciodată umflată artificial. Suma tăiată apare doar dacă e strict mai mare
decât prețul pachetului.

`BundleViewModel` se construiește ULTIMUL în `RebuildFromCatalog`, fiindcă
rezolvă elementele direct din `CatalogService`.

**Verificat**: `dotnet build` — 0 erori. Harness: toate cele 6 tipuri de
`BundleItemKind` se decodează, plus **pachetul REAL din catalogul live** (9
elemente, combinând `app` + `course` + `product`) și `seasonalBackground`-ul
live cu query-ul de cache-busting păstrat corect în URL-ul absolut.

### Bump de versiune final — 1.5.0 → 1.13.2
Sincronizat în ambele puncte (Regula 6 + Regula 14): `<Version>` din
`GDCPluginManager.Client.csproj` și `MyAppVersion` din `installer.iss`.
1.13.2 e exact versiunea Clientului Mac — `docs/update.json` are un singur câmp
`version`, comun ambelor platforme, deci Windows trebuie să ajungă la aceeași
valoare ca 404-ul de la `download_url.windows` (documentat ca RISC CUNOSCUT în
jurnalul de pe Mac) să poată dispărea după un build+upload real.

**Verificare finală**: `dotnet build` pe toată soluția, de la zero
(`obj/`+`bin/` șterse) — **0 erori, 0 avertismente**. `MainWindow.baml` a
crescut de la 37.967 la 67.135 bytes, dovadă că tot XAML-ul nou chiar a
compilat, nu doar a trecut de C#.

**Notă pentru un release viitor, NU rezolvată aici (în afara scopului)**:
`installer.iss` are `OutputBaseFilename=GDCPluginManagerSetup`, fără versiune
în nume — Regula 17 cere ca fișierul livrat să poarte versiunea
(`GDCPluginManagerSetup-1.13.2.exe`), ALĂTURI de copia cu nume stabil necesară
mecanismului `releases/latest/download/`. E o abatere PREEXISTENTĂ, care ține
de plumbing-ul de release, nu de portul funcțional din etapele astea — n-am
atins-o ca să nu schimb unilateral cum se numesc artefactele de release.

## Bump versiune 1.13.2 → 1.16.0 (2026-08-29) — sincronizare cu update.json comun

Bump DOAR de versiune (fără cod nou încă) — necesar ca `update.json` comun
(un singur câmp `version` pt Mac+Windows) să poată indica un release real
existent pe ambele platforme. Mac a primit între timp 3 funcționalități noi
(commit-uri `0e045b7`/`13ac854`/`ce7d720` pe `gdc-plugin-manager-catalog-vendor`):
social links + LinkedIn pe toate rubricile, selector temă System/Light/Dark,
bibliotecă de filigrane sezoniere cu scheduling+poziție.

**TODO paritate explicit, NU implementat încă pe Windows** (gap cunoscut,
documentat, nu ascuns):
1. `SocialLinks.LinkedinUrl` + `SocialLinks` pe Course/EducationalResource/
   Event/PartnerStore/ServiceCenter/AppLink (Windows are azi `SocialLinks`
   doar pe `PluginItem`/`DownloadableResource`, fără LinkedIn).
2. Selector explicit temă System/Light/Dark (Regula 18) — Windows n-are
   încă.
3. Filigran sezonier cu bibliotecă/scheduling/poziție — Windows are azi
   doar `Catalog.SeasonalBackground` (slot unic, fără scheduling/poziție,
   din Etapa 6 v2.0).

Acest release (v1.16.0) e funcțional identic pe Windows cu v1.13.2 (cele 9
etape) — doar numărul de versiune s-a aliniat la Mac ca update.json să poată
funcționa pe ambele platforme. Cele 3 funcționalități de mai sus rămân de
portat într-un release următor.

## [PARITATE 2026-08-29] Setare "Mărime Text" (Regula 24) — port pe Windows

Port al `TextScalePreference`/`TextScaleManager` de pe Mac
(`gdc-plugin-manager-catalog-vendor/Sources/GDCPluginManagerCore/AppTheme.swift`).

**Diferență de platformă, deliberată**: SwiftUI are `dynamicTypeSize()`
(infrastructură nativă de accesibilitate care reflowează orice text
semantic). WPF n-are echivalent direct. Fix ales: `LayoutTransform`
(`ScaleTransform`) aplicat pe `RootGrid` (Grid rădăcină din
`MainWindow.xaml`, acum numit explicit) — scalează UNIFORM tot arborele
vizual dintr-un singur punct, fără să umble prin fiecare `FontSize`
hardcodat din XAML (sute de apariții, risc mare de rupere dacă le-am fi
atins pe toate). `LayoutTransform` (spre deosebire de `RenderTransform`)
participă la calculul de layout — deci fereastra chiar are nevoie de mai
mult spațiu la scară >1.0; `MainWindow.ApplyTextScale()` redimensionează
și fereastra proporțional (`BaseWindowWidth/Height` × scară), clampat la
`SystemParameters.WorkArea` curentă, ca nimic să nu iasă tăiat vizual în
afara ecranului.

`TextScaleStore.cs` (Core, nou) — persistare simplă (`%AppData%\
GDCPluginManager\text-scale.txt`, un singur enum ca text, nu JSON — nu are
nevoie de mai mult). `SettingsWindow.xaml(.cs)` (Client, nou) — prima
fereastră de Setări din tot repo-ul Windows (nu exista deloc înainte);
urmează exact tiparul `ProfileEditorWindow` (fereastră modală mică,
`ShowFor` static). Buton "Setări" nou în footer-ul sidebar-ului, lângă
"Caută actualizări".

**Verificat**: `dotnet build` — 0 erori, 0 avertismente. `SettingsWindow.baml`
generat (confirmă că XAML-ul chiar a compilat pe acest SDK, nu doar C#-ul
— vezi corecția pitfall-ului din 2026-08-23, valabilă pe acest Mac).
**NEVERIFICABIL de aici**: comportamentul VIZUAL real al `LayoutTransform`
la scară >1.0 (dacă fereastra se redimensionează corect, fără artefacte de
randare) — necesită un test real pe Windows, o dată, înainte de a considera
feature-ul complet dovedit (aceeași categorie de risc ca Registry
detection/randare SVG din etapele anterioare).

**TODO paritate rămas, neatins în acest pas** (din batch-ul de 3 cerințe
de pe Mac — social links+LinkedIn și filigran sezonier cu bibliotecă
rămân neportate): vezi bump-ul de versiune anterior (1.13.2→1.16.0) pentru
detalii complete.

Versiune: `1.16.0`→`1.16.1` (PATCH — fix/feature mic, nu o etapă întreagă).

## [PARITATE FINALĂ 2026-08-29] Social links+LinkedIn, Temă, Bibliotecă filigrane — Windows la zi cu Mac

Ultimele 3 lucruri rămase TODO din batch-ul anterior (1.13.2→1.16.0 pe Mac)
sunt acum portate complet pe Windows:

**1. Social links + LinkedIn pe toate 6 rubricile** (Course/AppLink/
EducationalResource/Event/PartnerStore/ServiceCenter). `SocialLinks.LinkedinUrl`
adăugat în Core. Iconițe COLORATE de brand (Facebook/YouTube/Instagram/
TikTok/LinkedIn) — PNG bundle-uit (`Assets/Social/*.png`, generate din
aceleași SVG-uri cu paleta oficială folosite pe Mac, rasterizate offline),
NU SVG: precauție portată direct din bug-ul găsit pe Mac (ImageIO nu
randează `<text>`) — chiar dacă SharpVectors (Windows) NU are aceeași
limitare (`TextAsGeometry=true` convertește textul în geometrie), PNG
rămâne alegerea implicit sigură pentru iconițe fixe, universal recunoscute,
fără nicio dependință de randare la runtime. `SocialLinkCommands.Open`
(Client, nou) — UN SINGUR `ICommand` static, partajat de toate cardurile
(evită 30 de comenzi `[RelayCommand]` identice pe 6 ViewModel-uri).
`SocialLinksPanel.xaml` (UserControl nou, `DependencyProperty SocialLinks`)
— reutilizat de toate cele 6 `DataTemplate`-uri din `MainWindow.xaml`.

**2. Selector temă System/Light/Dark.** Diferență reală față de Mac:
SwiftUI are culori SEMANTICE care se adaptează singure la
`NSApp.appearance` — WPF `Theme.xaml` avea culori HARDCODATE. Fix:
paleta de culori extrasă din `Theme.xaml` în două dicționare noi,
`Theme.Dark.xaml`/`Theme.Light.xaml` (aceleași 8 chei, valori diferite —
paleta Light e nouă, autorată acum), iar TOATE referințele rămase în
`Theme.xaml` (stilurile) și în toate ferestrele (`MainWindow.xaml` + 5
ferestre modale) au fost convertite din `StaticResource` în
`DynamicResource` (71 de apariții) — altfel schimbarea de temă n-ar fi avut
niciun efect vizual fără repornirea aplicației (`StaticResource` se
rezolvă o singură dată, la parse). `WindowsThemeManager.cs` (Client, nou)
înlocuiește la runtime intrarea de la indexul fix 2 din
`Application.Resources.MergedDictionaries` (vezi comentariul din
`App.xaml` pentru ordinea exactă) + aplică și `Wpf.Ui.Appearance.
ApplicationThemeManager` (pentru controalele native Wpf.Ui — SymbolIcon,
MessageBox). `AppThemeStore.cs` (Core) persistă preferința
(`%AppData%\GDCPluginManager\theme.txt`). "System" citește
`HKCU\...\Personalize\AppsUseLightTheme` din Registry. Selector nou în
`SettingsWindow` (alături de Mărime Text, adăugată în pasul anterior).

**3. Bibliotecă filigrane sezoniere** (înlocuiește slotul unic din Etapa 6).
`SeasonalPosition` (enum, 5 poziții) + `SeasonalBackgroundConfig` (Id/
Label/ImagePath/Scheduling/Position/IsEnabled/Opacity) + `Catalog.
SeasonalBackgrounds` (listă, nouă) — port 1:1 al modelului de pe Mac,
verificat DIRECT (nu presupus) printr-un harness aruncabil care a decodat
catalogul LIVE de pe `gordas.dev`: 4 filigrane reale găsite, inclusiv
coliziunea de poziție rezolvată corect ("ultimul câștigă" — exact regula
de pe Mac). `ActiveNowDeduplicated()` (extension method) — port 1:1.
`SeasonalBackgroundLoader.cs` (existent din Etapa 6) — cache-ul pe disc
trecut de la UN SINGUR fișier global la unul PER FILIGRAN (cheiat după
id), altfel o bibliotecă cu mai multe filigrane active simultan ar fi
însemnat că ultimul descărcat suprascrie cache-ul tuturor celorlalte
offline — exact fix-ul aplicat deja pe Mac la același gap.
`SeasonalBackgroundItemViewModel.cs` (Client, nou) — traduce
`SeasonalPosition` în `HorizontalAlignment`/`VerticalAlignment`/`Thickness`
WPF, cu marginea de **48px** (nu 24px — port al corecției de pe Mac,
"24pt îl lipea prea aproape de margine"). `MainViewModel.SeasonalBackgrounds`
a trecut de la un singur `ImageSource?` la o `ObservableCollection`
de items gata de randat; `MainWindow.xaml` randează printr-un
`ItemsControl` cu `ItemsPanel=Grid` (filigranele se SUPRAPUN pe pozițiile
lor, nu se așază unul lângă altul).

**Simplificare deliberată, documentată**: spre deosebire de Mac, NU s-a
scris un `JsonConverter` custom pentru migrarea cheii vechi singulare
`seasonalBackground` → `seasonalBackgrounds` — Windows n-are Furnizor,
deci nu publică niciodată formatul vechi; catalogul live e deja migrat.
Câmpul vechi `Catalog.SeasonalBackground` rămâne în model, neatins, dar
efectiv neutilizat de-acum.

**Verificat**: `dotnet build` (întreaga soluție, `--no-incremental`) —
0 erori, 0 avertismente. Toate XAML noi (`SocialLinksPanel`, `Theme.Dark`,
`Theme.Light`, `SettingsWindow` extins) confirmate compilate (`.baml`
generat). Modelul de filigrane verificat cu un harness separat (nu parte
din repo), decodând catalogul LIVE — 4 filigrane reale, coliziune corectă.
**NEVERIFICAT de aici** (necesită test real pe Windows): randarea vizuală
efectivă a temei Light (culori alese acum, nu confirmate vizual de Cristi)
și a filigranelor multiple suprapuse.

Versiune: `1.16.1`→`1.19.2` (aliniat cu versiunea Client Mac, pentru
`docs/update.json` comun).

## [FIX ROBUSTEȚE 2026-08-29, val 2] Coperte Magazine/Cursuri/Materiale invizibile pe Windows

**Raportat live de Cristi cu captură reală**: coperta magazinului MITMAG
nu apărea deloc (doar iconița de rezervă), la fel și la Cursuri/Materiale.

**Cauza reală, găsită direct în cod**: `CoverViewModel.BeginLoad` loga
eroarea de `DownloadFailed` DOAR prin `Debug.WriteLine` — invizibil când
aplicația rulează normal (fără Visual Studio/debugger atașat), exact
situația lui Cristi. Nu exista NICIO urmă persistentă a eșecului real.

**Fix, aceeași rețetă ca la filigran** (Regula 25):
- `Debug.WriteLine` → `DiagnosticLog.Write` (Core, acum public).
- Retry automat (1 reîncercare, 0.8s pauză) — aceeași ipoteză de blip
  tranzitoriu CDN (Cloudflare + Fastly/GitHub Pages pe `gordas.dev`).
- Verificat: fișierul `MITMAG.jpg` era disponibil pe server (HTTP 200,
  22 KB) exact în momentul raportat — deci nu o problemă de date.
- Adăugat logging și în `LightboxWindow.xaml.cs` (previewul mărit),
  pentru consecvență.

**Rămas de confirmat**: cu logul nou, la următorul test pe Windows vom
avea EROAREA EXACTĂ (excepția .NET completă), nu doar simptomul — dacă
problema persistă după acest fix, diagnosticul următor pornește de la
date reale, nu presupuneri.

Versiune: `1.19.5`→`1.19.6` (PATCH).
**Verificat**: `dotnet build` — 0 erori.
