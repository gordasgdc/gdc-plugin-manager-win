# Changelog — GDC Plugin Manager (Windows)

Jurnal scurt, orientat spre utilizator, al schimbărilor livrate clienților
— o intrare per versiune, cu dată. Complementar jurnalului tehnic detaliat
din CLAUDE.md (acolo sunt și deciziile/motivele/pitfall-urile; aici doar
rezumatul a "ce s-a schimbat", ușor de scanat rapid).

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
