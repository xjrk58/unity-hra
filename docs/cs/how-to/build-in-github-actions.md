# Jak sestavit balíčky v GitHub Actions

V repozitáři je workflow `.github/workflows/build.yml`, které hru sestaví na
strojích GitHubu pokaždé, když se něco pošle do větve `master`, když se pošle
tag začínající na `v`, nebo když ho spustíš ručně. Jednou mu musíš dát licenci
Unity.

> **Stav:** workflow je napsané pro aktuální verze akcí, které používá, a
> sestavovací obrazy pro tuhle verzi Unity existují, ale **spuštěné ještě
> nebylo**: potřebuje licenci Unity v tajných údajích repozitáře. Počítej s
> tím, že ho při prvním běhu budeš muset doladit. Viz
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
jako ta sestavená u tebe.

## 1. Dej repozitáři licenci Unity

Stačí bezplatná licence Personal. Potřebuješ licenční soubor z počítače, na
kterém je Unity Hub přihlášený a aktivovaný:

| Systém | Licenční soubor |
|--------|-----------------|
| Linux | `~/.local/share/unity3d/Unity/Unity_lic.ulf` |
| macOS | `/Library/Application Support/Unity/Unity_lic.ulf` |
| Windows | `C:\ProgramData\Unity\Unity_lic.ulf` |

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

Tajné údaje umí přečíst jen workflow tohoto repozitáře a v logách se nikdy
neukážou. Licenční soubor ani heslo nedávej do samotného repozitáře.

Dokud `UNITY_LICENSE` neexistuje, každá úloha po pár vteřinách skončí s
varováním *„The UNITY_LICENSE secret is not set, so nothing was built“* –
workflow je zelené, ale nic nevyrobí.

## 2. Spusť ho

- **Automaticky:** pošli změny do `master`, nebo pošli tag, třeba `v1.0`.
- **Ručně:** na GitHubu **Actions ▸ Build packages ▸ Run workflow**; nebo
  `gh workflow run "Build packages"`.

První běh každé úlohy importuje všechny assety a trvá dlouho (u úloh pro
Android počítej s 20–40 minutami); další běhy použijí uloženou složku
`Library`.

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
- **Pull requesty z forků** workflow nespustí: neměly by přístup k tajným
  údajům.
- **Když chceš sestavovat méně věcí,** smaž položky z `matrix` v souboru
  workflow.

## Když úloha selže

| Co je v logu | Pravděpodobná příčina |
|--------------|------------------------|
| *„No valid license“*, chyby aktivace | `UNITY_LICENSE` není celý soubor, nebo patří jinému účtu než `UNITY_EMAIL` |
| *„no space left on device“* v úloze pro Android | obraz se nevešel; přidej další cesty do kroku *Free disk space* |
| Obraz pro tuhle verzi Unity nejde stáhnout | projekt byl převeden na verzi Unity, pro kterou GameCI ještě obraz nemá; podívej se na <https://game.ci/docs/docker/versions> |
| `error CS…` | kód se nepřeloží – stejná chyba se objeví i u tebe, viz [Spuštění automatických kontrol](run-the-automated-checks.md) |
