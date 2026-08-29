# Changelog — GDC Plugin Manager (Windows)

Jurnal scurt, orientat spre utilizator, al schimbărilor livrate clienților
— o intrare per versiune, cu dată. Complementar jurnalului tehnic detaliat
din CLAUDE.md (acolo sunt și deciziile/motivele/pitfall-urile; aici doar
rezumatul a "ce s-a schimbat", ușor de scanat rapid).

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
