# codesigning/ — semnare Windows (Self-Signed, testare internă)

Acest folder acoperă DOAR semnarea Windows (adăugată 2026-09-06,
CLAUDE.md Regula 34). Acest repo nu are un echivalent Mac (nu produce
un `.app`/`.pkg`).

## De ce Self-Signed, și ce NU rezolvă

Un certificat self-signed **nu elimină avertismentul SmartScreen/"Unknown
publisher"** pentru publicul larg — doar un certificat real de la o CA
publică (cu reputație acumulată) sau un certificat EV fac asta. Self-signed
e util STRICT pentru:
- testare internă (buildurile pe care le rulează Cristi însuși),
- distribuire către un cerc restrâns de colaboratori care importă manual
  certificatul public (`.cer`) în Trusted Root o singură dată.

La lansarea comercială publică, planul e Azure Trusted Signing sau un
certificat EV (HSM cloud) — vezi CLAUDE.md Regula 34 pentru context complet.

## Certificat COMUN tuturor aplicațiilor GDC

Decizie explicită Cristi: un singur certificat self-signed, partajat de
TOATE aplicațiile Windows din ecosistem (nu unul separat per repo) —
secretele CI se numesc IDENTIC în toate repo-urile
(`WIN_SELFSIGN_PFX_BASE64`/`WIN_SELFSIGN_PFX_PASSWORD`), încărcate
separat, o dată per repo, de Cristi. Dacă certificatul a fost deja
generat pentru un alt repo GDC (ex. CGConvertor), NU se generează unul
nou aici — doar se reîncarcă ACELEAȘI valori ca secrete pe acest repo.

## Setup unic (o dată, făcut DIRECT de Cristi pe Windows real)

Certificatul (privat, cu cheie) nu trece niciodată prin conversația cu
Claude — la fel ca orice altă parolă/cheie din ecosistem.

1. Pe Windows real (Parallels e suficient), deschide PowerShell **ca
   Administrator** și rulează (doar dacă certificatul comun NU există
   încă din alt repo):
   ```powershell
   .\codesigning\generate-self-signed-cert.ps1
   ```
   Scriptul cere o parolă nouă (pentru `.pfx`) și produce două fișiere:
   - `gdc-selfsign.pfx` — **PRIVAT**, nu se distribuie, nu se
     comite în git.
   - `gdc-selfsign.cer` — **PUBLIC**, se distribuie colaboratorilor.

2. Încarcă `.pfx`-ul ca secrete GitHub Actions — comenzile exacte sunt
   afișate la finalul scriptului (necesită `gh` CLI autentificat pe acea
   mașină). Pentru acest repo:
   ```powershell
   gh secret set WIN_SELFSIGN_PFX_BASE64 --repo gordasgdc/gdc-plugin-manager-win --body $b64
   gh secret set WIN_SELFSIGN_PFX_PASSWORD --repo gordasgdc/gdc-plugin-manager-win
   ```

3. Șterge `.pfx`-ul local imediat după (`Remove-Item gdc-selfsign.pfx -Force`)
   — rămâne doar în secretele CI, criptate.

4. Distribuie `gdc-selfsign.cer` colaboratorilor. Pe fiecare
   mașină a lor, o singură dată: dublu-click → **Install Certificate** →
   **Local Machine** → "Place all certificates in the following store" →
   **Trusted Root Certification Authorities**.

Odată făcuți pașii 1-4, **fiecare build viitor din CI** (`git push`
pe `main`) semnează automat `.exe`-ul și installer-ul cu ACELAȘI
certificat — colaboratorii nu mai trebuie să reimporte nimic la
versiunile următoare, indiferent din ce repo GDC vine build-ul.

## Ce face CI-ul automat (`.github/workflows/build-windows.yml`)

- Dacă secretele NU sunt setate: build-ul continuă **nesemnat**, exact ca
  până acum — nicio eroare, nicio schimbare de comportament.
- Dacă secretele SUNT setate: după ce `GDCPluginManager.exe` (.NET/WPF) și
  installer-ul final (Inno Setup) există, ambele sunt semnate cu
  `signtool.exe` (localizat dinamic din Windows Kits, cu timestamp), apoi
  verificate cu `Get-AuthenticodeSignature` — confirmă DOAR că semnătura
  a fost atașată corect, fără să ceară lanț de încredere complet (asta ar
  eșua mereu pe un runner CI proaspăt, care nu are certificatul în
  Trusted Root — normal pentru self-signed, nu un bug). Un eșec real de
  semnare (fișier fără nicio semnătură) tot oprește build-ul (CI roșu).

## Regenerarea certificatului (dacă expiră sau e compromis)

Rulează din nou `generate-self-signed-cert.ps1`, reîncarcă secretele
(pasul 2 de mai sus îi suprascrie pe cei vechi) — **pe TOATE repo-urile
GDC care folosesc acest certificat comun**, altfel unele build-uri rămân
semnate cu certificatul vechi. Toți colaboratorii trebuie să reimporte
noul `.cer`, altfel văd din nou avertismentul pentru versiunile semnate
cu noul certificat. Evită regenerarea inutilă — de asta scriptul
folosește o valabilitate de 5 ani.
