# Jak sestavit balíčky v GitHub Actions

V repozitáři je workflow `.github/workflows/build.yml`, které hru sestaví na
strojích GitHubu pokaždé, když se něco pošle do větve `master`, když se pošle
tag začínající na `v`, nebo když ho spustíš ručně. Jednou mu musíš dát licenci
Unity.

> **Stav:** workflow už běželo. 19. 11. 2026 sestavilo všech pět balíčků;
> třetí běh byl první, ve kterém prošlo všech pět úloh. Balíček pro Linux se
> spustil na vývojovém počítači a hru ukazuje. Balíčky pro Windows a macOS se
> jen rozbalily a prohlédly a obě APK se porovnala s těmi sestavenými u tebe;
> tyhle čtyři **spuštěné nebyly**. Viz
> [Stav ověření](../reference/verification-status.md).

## Co sestaví

| Úloha | Výsledek (artefakt) | Jak |
|-------|---------------------|-----|
| Linux | `PrdivyZavod-linux.tar.gz` | standardní sestavení GameCI |
| Windows | `PrdivyZavod-windows.tar.gz` | standardní sestavení GameCI |
| macOS | `PrdivyZavod-macos.tar.gz` (nepodepsaná `.app`) | standardní sestavení GameCI |
| Android | `PrdivyZavod.apk` | `BuildGame.BuildAndroid` |
| Android (staré telefony) | `PrdivyZavod-old.apk` | `BuildGame.BuildAndroidOld` |

Všech pět běží v linuxových kontejnerech projektu [GameCI](https://game.ci)
(`game-ci/unity-builder`), který vybere obraz podle verze Unity v
`ProjectSettings/ProjectVersion.txt`. APK jsou podepsaná ladicím klíčem stejně
jako ta sestavená u tebe, jen jiným (viz [Co je dobré vědět](#co-je-dobré-vědět)).

## 1. Dej repozitáři licenci Unity

Stačí bezplatná licence Personal. Potřebuješ licenční soubor `Unity_lic.ulf` z
počítače, na kterém je Unity Hub přihlášený:

| Systém | Licenční soubor |
|--------|-----------------|
| Linux | `~/.local/share/unity3d/Unity/Unity_lic.ulf` |
| macOS | `/Library/Application Support/Unity/Unity_lic.ulf` |
| Windows | `C:\ProgramData\Unity\Unity_lic.ulf` |

**Soubor tam být nemusí,** i když Unity Hub licenci ukazuje a editor funguje
(na počítači, kde tahle hra vznikala, chyběl). Vytvoříš ho v Unity Hubu:
**Preferences (ozubené kolečko) ▸ Licenses ▸ Add ▸ Get a free personal
license**; pak se podívej znovu. Stejný postup popisuje
[návod GameCI k aktivaci](https://game.ci/docs/github/activation).

K účtu Unity dvě věci:

- Musí mít vlastní heslo. Pokud se k Unity přihlašuješ přes Google nebo jinou
  službu, nastav si nejdřív heslo na webu Unity v nastavení zabezpečení účtu a
  použij to.
- GameCI doporučuje heslo jen z písmen a číslic: s jinými znaky se aktivace
  už nepovedla.

Samotný licenční soubor stroje GitHubu nepřijmou, protože je vázaný na
počítač, na kterém vznikl: běh ukáže varování *„The license file's machine
binding doesn't match this machine“* a sestavení se místo toho přihlásí
e-mailovou adresou a heslem. Takhle dostal licenci zatím každý běh, takže
varování si nemusíš všímat.

Na GitHubu otevři v repozitáři **Settings ▸ Secrets and variables ▸ Actions ▸
New repository secret** a vytvoř tři tajné údaje:

| Secret | Hodnota |
|--------|---------|
| `UNITY_LICENSE` | celý obsah souboru `.ulf` |
| `UNITY_EMAIL` | e-mailová adresa účtu Unity |
| `UNITY_PASSWORD` | jeho heslo |

Totéž nástrojem GitHubu pro příkazovou řádku:

```sh
gh secret set UNITY_LICENSE < ~/.local/share/unity3d/Unity/Unity_lic.ulf
gh secret set UNITY_EMAIL
gh secret set UNITY_PASSWORD
```

Poslední dva příkazy se na hodnotu zeptají; napiš ji až tam, ne na příkazovou
řádku, kde by zůstala v historii shellu.

Tajné údaje umí přečíst jen workflow tohoto repozitáře a v logách se nikdy
neukážou. Licenční soubor ani heslo nedávej do samotného repozitáře.

Dokud `UNITY_LICENSE` neexistuje, každá úloha po pár vteřinách skončí s
varováním *„The UNITY_LICENSE secret is not set, so nothing was built“* –
workflow je zelené, ale nic nevyrobí.

## 2. Spusť ho

- **Automaticky:** pošli změny do `master`, nebo pošli tag, třeba `v1.0`.
  Samotné přidání tajných údajů nic nespustí.
- **Ručně:** na GitHubu **Actions ▸ Build packages ▸ Run workflow**; nebo
  `gh workflow run "Build packages"`.

První běh každé úlohy importuje všechny assety: u úloh pro počítače to trvalo
9–18 minut, u úloh pro Android 19–34 minut. Další běhy použijí uloženou složku
`Library` a trvaly asi 4 minuty u úlohy pro počítač a 9–12 minut u úlohy pro
Android.

## 3. Stáhni si balíčky

Otevři dokončený běh na stránce **Actions**; balíčky jsou dole v části
**Artifacts** a uchovávají se 30 dní. Z příkazové řádky:

```sh
gh run download --name PrdivyZavod-android
```

Balíčky pro počítače rozbal pomocí `tar -xzf`; jsou to tarbally, aby programy
zůstaly spustitelné.

## Co je dobré vědět

- **Cena.** Veřejný repozitář se sestavuje zdarma. V soukromém těch pět úloh
  ubírá z minut Actions, které máš v ceně.
- **Aplikace pro macOS není podepsaná.** Jak ji otevřít, je v kroku 6 návodu
  [Sestavení na Macu s Apple silicon](build-on-a-mac-with-apple-silicon.md).
- **Sestavení pro počítače se tady a u tebe trochu liší.** Workflow používá
  pro počítače vlastní sestavovací metodu GameCI, takže výsledek je v
  `Builds/<target>/`; u tebe `BuildGame.Build` zapisuje do `Builds/PrdivyZavod/`.
  Hra uvnitř je stejná.
- **Každé APK z GitHubu je podepsané jiným klíčem.** Ladicí klíč vzniká v
  každé úloze znovu. Android nenainstaluje aktualizaci podepsanou jiným
  klíčem, takže než nainstaluješ APK z GitHubu přes to sestavené u tebe nebo
  přes APK z jiného běhu, hru z telefonu odeber
  (`adb uninstall com.prdivyzavod.hra`).
- **Odhlášení z Unity Hubu.** Návod GameCI říká, že se po zkopírování souboru
  máš z Unity Hubu odhlásit. Sestavování na vlastním počítači bez okna editoru
  ale přihlášený Hub potřebuje. Dosavadní běhy proběhly, když Hub na vývojovém
  počítači zůstal přihlášený, a licenci dostaly.
- **Pull requesty z forků** workflow nespustí: neměly by přístup k tajným
  údajům.
- **Když chceš sestavovat méně věcí,** smaž položky z `matrix` v souboru
  workflow.

## Když úloha selže

| Co je v logu | Pravděpodobná příčina |
|--------------|------------------------|
| *„No valid license“*, chyby aktivace | `UNITY_LICENSE` není celý soubor, nebo patří jinému účtu než `UNITY_EMAIL`; nebo je `UNITY_PASSWORD` špatně či obsahuje zvláštní znaky, nebo účet vlastní heslo nemá (přihlášení přes Google) |
| *„There was an error building the project“* hned po *„Build succeeded“* | GameCI nenašlo blok s výsledkem, který čte (`# Build results #` … `Build succeeded!`); `BuildGame.Report` ho vypisuje a tvoje vlastní sestavovací metoda musí taky |
| *„Failed to resolve the latest game-ci CLI release: GitHub API returned 403“* | GitHub odmítl dotaz GameCI na jeho nejnovější verzi; workflow předává `GITHUB_TOKEN`, aby se tomu vyhnulo. Kdyby se to přesto stalo, spusť neúspěšnou úlohu znovu |
| *„no space left on device“* v úloze pro Android | obraz se nevešel; přidej další cesty do kroku *Free disk space* |
| Obraz pro tuhle verzi Unity nejde stáhnout | projekt byl převeden na verzi Unity, pro kterou GameCI ještě obraz nemá; podívej se na <https://game.ci/docs/docker/versions> |
| `error CS…` | kód se nepřeloží – stejná chyba se objeví i u tebe, viz [Spuštění automatických kontrol](run-the-automated-checks.md) |
