# How to build the packages in GitHub Actions

The repository contains a workflow, `.github/workflows/build.yml`, that builds
the game on GitHub's machines whenever something is pushed to `master`, a tag
starting with `v` is pushed, or you start it by hand. It needs to be given a
Unity licence once.

> **Status:** the workflow has been written against the current versions of
> the actions it uses and its build images exist for this Unity version, but it
> has **not been run**: that needs a Unity licence in the repository's
> secrets. Expect to adjust it on the first run. See
> [Verification status](../reference/verification-status.md).

## What it builds

| Job | Result (artifact) | How |
|-----|-------------------|-----|
| Linux | `PrdivyZavod-linux.tar.gz` | GameCI's standard build |
| Windows | `PrdivyZavod-windows.tar.gz` | GameCI's standard build |
| macOS | `PrdivyZavod-macos.tar.gz` (an unsigned `.app`) | GameCI's standard build |
| Android | `PrdivyZavod.apk` | `BuildGame.BuildAndroid` |
| Android (old phones) | `PrdivyZavod-old.apk` | `BuildGame.BuildAndroidOld` |

All five run in Linux containers provided by [GameCI](https://game.ci)
(`game-ci/unity-builder`), which picks the image matching the Unity version in
`ProjectSettings/ProjectVersion.txt`. The APKs are debug-signed, like the ones
built locally.

## 1. Give the repository a Unity licence

The free Personal licence works. You need the licence file of a computer on
which Unity Hub is signed in and activated:

| System | Licence file |
|--------|--------------|
| Linux | `~/.local/share/unity3d/Unity/Unity_lic.ulf` |
| macOS | `/Library/Application Support/Unity/Unity_lic.ulf` |
| Windows | `C:\ProgramData\Unity\Unity_lic.ulf` |

On GitHub open the repository's **Settings ▸ Secrets and variables ▸ Actions ▸
New repository secret** and create three secrets:

| Secret | Value |
|--------|-------|
| `UNITY_LICENSE` | the whole contents of the `.ulf` file |
| `UNITY_EMAIL` | the e-mail address of the Unity account |
| `UNITY_PASSWORD` | its password |

With the GitHub command line tool the same is:

```sh
gh secret set UNITY_LICENSE < ~/.local/share/unity3d/Unity/Unity_lic.ulf
gh secret set UNITY_EMAIL
gh secret set UNITY_PASSWORD
```

Secrets are only readable by workflows of this repository and are never shown
in logs. Do not put the licence file or the password into the repository
itself.

Until `UNITY_LICENSE` exists, every job ends after a few seconds with the
warning *"The UNITY_LICENSE secret is not set, so nothing was built"* – the
workflow is green but produces nothing.

## 2. Run it

- **Automatically:** push to `master`, or push a tag such as `v1.0`.
- **By hand:** on GitHub, **Actions ▸ Build packages ▸ Run workflow**; or
  `gh workflow run "Build packages"`.

The first run of each job imports every asset and takes a long time (expect
20–40 minutes for the Android jobs); later runs reuse a cached `Library`
folder.

## 3. Get the packages

Open the finished run on the **Actions** page; the packages are listed under
**Artifacts** at the bottom and are kept for 30 days. From the command line:

```sh
gh run download --name PrdivyZavod-android
```

Unpack the desktop packages with `tar -xzf`; they are tarballs so that the
programs stay executable.

## Things to know

- **Cost.** A public repository builds for free. In a private one the five
  jobs use up included Actions minutes.
- **The macOS app is unsigned.** See step 6 of
  [Build on a Mac with Apple silicon](build-on-a-mac-with-apple-silicon.md)
  for opening it.
- **Desktop builds here and locally differ slightly.** The workflow uses
  GameCI's own build method for the desktop targets, so the result is in
  `Builds/<target>/`; locally, `BuildGame.Build` writes `Builds/PrdivyZavod/`.
  The game inside is the same.
- **Pull requests from forks** do not start the workflow: they would not have
  access to the secrets.
- **To build fewer things,** delete entries from the `matrix` in the workflow
  file.

## If a job fails

| Symptom in the log | Likely cause |
|--------------------|--------------|
| *"No valid license"*, activation errors | `UNITY_LICENSE` is not the complete file, or belongs to another account than `UNITY_EMAIL` |
| *"no space left on device"* in an Android job | the image did not fit; add more paths to the *Free disk space* step |
| The image for this Unity version cannot be pulled | the project was moved to a Unity version GameCI has no image for yet; check <https://game.ci/docs/docker/versions> |
| `error CS…` | the code does not compile – the same error appears locally, see [Run the automated checks](run-the-automated-checks.md) |
