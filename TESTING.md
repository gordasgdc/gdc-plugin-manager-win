# GDC Plugin Manager (Windows) — ghid de testare

## 1. Dependinte necesare

- **.NET 8 SDK** (nu doar Runtime-ul — ai nevoie sa compilezi din sursa):
  https://dotnet.microsoft.com/download/dotnet/8.0
  Verifica dupa instalare: `dotnet --version` (trebuie sa arate `8.x`).
- Windows 10 (1809+) sau Windows 11. Iconitele Fluent (WPF-UI) arata cel mai
  bine pe Windows 11, dar functioneaza si pe Windows 10.
- Nu e nevoie de Visual Studio — doar de SDK-ul de mai sus si un terminal
  (PowerShell sau Command Prompt).

## 2. Build & Run

Dezarhiveaza proiectul, deschide un terminal in folderul `GDCPluginManagerWin`
si ruleaza:

```
dotnet run --project src\GDCPluginManager.Client
```

Prima rulare dureaza mai mult (restore NuGet + build). Aplicatia se deschide
automat intr-o fereastra.

**Alternativ**, un build de tip "portabil" (un `.exe` de sine statator, fara
sa mai trebuiasca `dotnet` instalat pe alta masina):

```
dotnet publish src\GDCPluginManager.Client -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o publish
```

Executabilul rezultat e in `publish\GDCPluginManager.exe` — poate fi trimis
si rulat independent (fisier mare, ~150MB, pentru ca include runtime-ul .NET).

## 3. Checklist rapid de testare

- [ ] **Pornire**: aplicatia se deschide fara erori, fara fereastra de UAC
      (nu trebuie sa ceara admin doar ca sa porneasca).
- [ ] **Incarcare catalog**: la pornire apar produse in grid (daca exista
      conexiune la internet — catalogul vine de pe `gordas.dev/catalog.json`).
- [ ] **Reincarca**: butonul "Reincarca" din bara de sus aduce catalogul din
      nou, fara crash.
- [ ] **Filtre categorii**: clic pe DCTL / LUT / Fuse / PowerGrade / OFX in
      bara laterala — grid-ul se filtreaza corect; "Toate" arata tot.
- [ ] **Cautare**: scrii in campul de cautare — lista se filtreaza live dupa
      nume/descriere.
- [ ] **ID Masina**: in bara laterala, jos, apare un cod (litere+cifre) —
      confirma ca nu e gol si nu e "win-machine-id-unavailable" (daca e asa,
      WMI nu a putut citi UUID-ul hardware — de raportat).
- [ ] **Activare licenta**: apasa "Activeaza licenta" pe un produs platit —
      se deschide dialogul, cere un cod. (Poti testa cu un cod invalid ca sa
      vezi mesajul de eroare; un cod valid il generezi tu din Furnizor pe Mac,
      folosind ID-ul de masina afisat mai sus.)
- [ ] **Instalare + permisiuni**: instaleaza un produs gratuit (`isFree: true`
      in catalog, daca exista unul) — verifica ca fisierul apare in:
      `%ProgramData%\Blackmagic Design\DaVinci Resolve\Support\LUT` (sau
      `...\Fusion\Fuses` pentru Fuse). Daca folderul e protejat, ar trebui sa
      apara promptul de UAC (Windows cere parola/confirmarea de admin) — de
      confirmat ca acel prompt apare si ca dupa acceptare fisierul chiar se
      copiaza.
  - Pentru **OFX** verifica in schimb `C:\Program Files\Common Files\OFX\Plugins`.
- [ ] **Eliminare**: butonul "Elimina" pe un produs instalat sterge fisierul
      de pe disc si butonul revine la "Instaleaza".
- [ ] **Banner actualizare**: daca versiunea din `update.json` e mai noua
      decat cea din aplicatie, ar trebui sa apara un banner verde sus. (Poate
      sa nu apara la primul test, e ok — versiunea Windows 1 e cea mai noua.)
- [ ] **Redimensionare fereastra**: trage de marginea ferestrei — layout-ul
      (grid de carduri, bara laterala) nu se rupe.

## De raportat inapoi

Orice eroare/crash — cu textul exact al mesajului (daca apare popup-ul rosu
de eroare neasteptata) — plus la ce pas din checklist s-a intamplat.
