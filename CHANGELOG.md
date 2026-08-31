# Changelog — GDC Plugin Manager (Windows)

Jurnal scurt, orientat spre utilizator, al schimbărilor livrate clienților
— o intrare per versiune, cu dată. Complementar jurnalului tehnic detaliat
din CLAUDE.md (acolo sunt și deciziile/motivele/pitfall-urile; aici doar
rezumatul a "ce s-a schimbat", ușor de scanat rapid).

## v1.24.5 (2026-08-31) — Sincronizare versiune (fix Marime Text, Mac-only)

Fara schimbari de cod pe Windows - Marime Text functiona deja corect aici
(ScaleTransform). Bump doar pentru sincronizare cu versiunea comuna.

## v1.24.3 (2026-08-31) — Poziție text sus/jos, aleasă din Furnizor

Port 1:1 al Mac v1.24.3: text și imagine separate, fără suprapunere;
poziția (deasupra/sub imagine) aleasă din Furnizor (Mac).

## v1.24.2 (2026-08-31) — Fix: textul bannerului se suprapunea peste imagine

Port 1:1 al Mac v1.24.2: voal întunecat sub text, robust la orice imagine
încărcată prin Furnizor.

## v1.24.1 (2026-08-31) — Sincronizare versiune (fix Mac în v1.24.1)

Fără schimbări de cod pe Windows — bump de versiune pentru a rămâne
sincron cu Mac v1.24.1 (fix real la bannerul de lansare, cod Mac-only).

## v1.24.0 (2026-08-31) — Valabilitate temporală pentru bannerul de lansare

Port 1:1 al Client Mac v1.24.0: bannerul de lansare poate avea acum o
perioadă programată — se ascunde automat după data de sfârșit.

## v1.23.0 (2026-08-31) — Banner de lansare controlabil din Furnizor

Port 1:1 al Client Mac v1.23.0: un banner jos de tot pe ecranul principal,
cu imagine + text de ofertă de lansare, controlat de Cristi din Furnizor
(Mac) fără nicio recompilare pe Windows.

## v1.21.0 (2026-08-31) — Ceas live opțional (countdown) pentru oferte cu termen

Port 1:1 al Client Mac v1.21.0: conținutul cu perioadă limitată (produse,
cursuri, resurse educaționale, evenimente, magazine/centre partenere,
oferte partener, aplicații, audio, resurse descărcabile, bundle-uri) poate
afișa acum, opțional, un badge live „Mai sunt Xz Yh"/"Yh Zm"/"Zm" până la
expirare, auto-actualizat la 60s. Compatibil cu `catalog.json` existent —
apare doar acolo unde Furnizorul a activat explicit countdown-ul.

**Notă**: acest release rezolvă și eroarea HTTP 404 raportată la self-update
— versiunea Windows nu mai rămăsese în urmă față de `update.json`.

## v1.20.0 (2026-08-30) — Iconițe reale în „Aplicațiile Mele" + Scurtături

Port 1:1 din Mac v1.20.0: cardurile din „Aplicații GDC instalate" și
„Scurtăturile mele" extrag acum iconița REALĂ direct din `.exe`-ul instalat
(`System.Drawing.Icon.ExtractAssociatedIcon`), inclusiv pentru scurtături
terțe (DaVinci Resolve, Photoshop, Lightroom etc.) — nu bundle-uim nicio
siglă terță în cod (risc de marcă înregistrată), extragerea se face mereu
din aplicația deja instalată pe mașina userului, exact ca Explorer.
Fallback pe simbol generic doar dacă extragerea eșuează. Adăugarea de
scurtături acceptă acum mai multe `.exe`-uri deodată (`Multiselect`).

**Notă de arhitectură**: Windows nu are un echivalent simplu al
`NSWorkspace.didLaunchApplicationNotification`/watcher de director de pe
Mac — lista se reface la fiecare navigare în pagina „Aplicațiile Mele”
(`ShowMyApps` apelează deja `RefreshAsync`), nu instant la instalare ca pe
Mac. Rămâne un gap real, documentat aici, nu tratat ca rezolvat.

## v1.19.14 (2026-08-29) — Bump doar de versiune (sincronizare cu Mac, fără cod nou)

Mac a primit ghidurile PDF (RO/EN/ES) redesenate cu capturi reale ale
aplicației + ghidul din aplicație completat (Panoul de Dependențe,
Aplicația mobilă) — conținut, nu cod. Windows neschimbat.

## v1.19.13 (2026-08-29) — Bump doar de versiune, ca să validăm end-to-end fix-ul de self-update din v1.19.12

Fără schimbare de cod. Cauza reală a "imaginilor invizibile" (blocaj La Liga
pe intervalul Cloudflare al `gordas.dev`, rezolvat prin DNS-only direct pe
GitHub Pages) e reparată definitiv la nivel de infrastructură — acest bump
există doar ca să existe o versiune "nouă" de descărcat, ca să confirmăm
manual, o dată, că self-updater-ul reparat în v1.19.12 chiar funcționează
din program (nu doar verificat automat "arhiva se descarcă integru").

## v1.19.12 (2026-08-29) — Fix real: self-update din program eșua tăcut, cădea pe "Deschide pagina"

- Cauza reală (raportată de Cristi: "de ce trebuie tot timpul să descarc
  de pe pagina web" — pe GDC Vault/DataMover funcționează direct din
  program): `SelfUpdater.cs` folosea `new HttpClient()` propriu, fără
  User-Agent (GitHub poate respinge cu 403 fără el — bug deja documentat
  în `HttpClientFactory.cs`, dar neaplicat aici) și fără logare la eșec
  (Regula 25). Orice descărcare eșuată cădea direct pe fallback-ul
  "Deschide pagina", fără nicio urmă în log. Fix: `SelfUpdater` folosește
  acum `HttpClientFactory.Create()` (User-Agent + reciclare conexiune +
  validare TLS diagnosticabilă) + logare reală a erorii la eșec.

## v1.19.11 (2026-08-29) — Forțat HTTP/1.1 explicit (elimină variabila ALPN/HTTP2)

- v1.19.9/10 nu au rezolvat eroarea SSL — certificatul fals apărea DOAR pe
  `gordas.dev`, DOAR din HttpClient (.NET), niciodată din `curl` (verificat
  identic, în aceeași sesiune, pe același host). Singura diferență reală
  rămasă: `curl` pe această mașină nu suportă deloc HTTP/2, `HttpClient`
  putea oferi ALPN h2. Forțat explicit HTTP/1.1 (`ApplicationProtocols`,
  `DefaultRequestVersion`/`DefaultVersionPolicy`) — elimină complet
  această variabilă, indiferent dacă era cauza.

## v1.19.10 (2026-08-29) — Diagnostic: certificatul REAL respins, logat explicit

- v1.19.9 (reciclare conexiune) NU a rezolvat eroarea SSL la filigran —
  confirmat din log, eroarea persistă identic pe conexiuni proaspete.
- Adăugat `RemoteCertificateValidationCallback` de diagnostic — la orice
  refuz de certificat, logul arată acum Subject/Issuer/Thumbprint-ul REAL
  primit de la server + motivul exact (`SslPolicyErrors`/`ChainStatus`).
  Nu schimbă comportamentul de securitate (tot respinge certificate
  invalide) — doar face vizibil DE CE, ca să nu mai ghicim.

## v1.19.9 (2026-08-29) — Fix real: conexiune HTTPS reciclată la 5 min (nu mai infinit)

- Cauza reală a eșecului SSL intermitent la filigran (diagnosticată din log
  `Describe(ex)` din v1.19.8): `RemoteCertificateNameMismatch`, DOAR pe
  `HttpClient` static, niciodată pe `curl` (10/10 OK, conexiune nouă de
  fiecare dată). `gordas.dev` e în spatele Cloudflare (anycast) —
  `HttpClient`-urile aplicației țineau o conexiune TLS deschisă la infinit.
- Fix: `PooledConnectionLifetime = 5 minute` pe toate `HttpClient`-urile
  aplicației — conexiunea se reface periodic, la fel de robust ca un
  handshake nou per cerere.

## v1.19.8 (2026-08-29) — Logare detaliată a erorii SSL reale

- Eroarea de rețea din log arată acum lanțul COMPLET de InnerException
  (nu doar "SSL connection could not be established") — diagnostic direct
  pentru cazuri de VM cu ceas de sistem greșit.

## v1.19.7 (2026-08-29) — Fix critic: imagini invizibile peste tot

- Cauza reală: `BitmapImage.UriSource` trecea prin WinINet (stack de rețea
  legacy, separat de HttpClient). Coperți/lightbox rescrise să descarce
  prin HttpClient + MemoryStream — la fel ca restul aplicației.
- Retry automat + logare reală a erorii, dacă problema persistă.

## v1.19.2 (2026-08-29) — Paritate finală cu Mac

- Social links + LinkedIn pe toate 6 rubricile (Course/App/Materiale/
  Eveniment/Magazin/Service), iconițe colorate de brand.
- Selector explicit de temă System/Light/Dark, aplicat instant.
- Bibliotecă filigrane sezoniere (perioadă, poziție, intensitate reglabilă).
- Setare Mărime Text (System/Light/Dark era deja etapa precedentă).

## v1.13.2 (2026-08-29) — Cele 9 etape de upgrade v2.0

Căutare fuzzy globală + filtru OS, resurse download cu licențiere,
Aplicațiile Mele, scheduling + Susținere promoțională, hărți + folder de
descărcare reținut, filigran sezonier (versiunea inițială, slot unic),
pachete/bundle-uri.

## v1.19.3 (2026-08-29) — Sincronizare versiune (fix Furnizor Mac, fara schimbari de cod pe Windows)

Bump doar de versiune, ca `update.json` comun (Mac+Windows) sa reflecte
un release real existent pe ambele platforme. Fix-ul propriu-zis (draft
orfan la filigrane) a fost exclusiv in Furnizor (Mac, unealta interna,
Windows nu are Furnizor).

## v1.19.5 (2026-08-29) — Fix retry filigran + fix ferestre goale

- Fetch filigran sezonier acum reîncearcă automat o dată (blip-uri
  tranzitorii de CDN pe gordas.dev) + log de eroare reală, nu generică.
- Fix: ferestrele Setări/Profil/Dependențe/Progres update se puteau
  desena goale o clipă la deschidere (lipsea `MinHeight`) — reparat.
