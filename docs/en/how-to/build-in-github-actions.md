# How to build the packages in GitHub Actions

The repository contains a workflow, `.github/workflows/build.yml`, that builds
the game on GitHub's machines whenever something is pushed to `master`, a tag
starting with `v` is pushed, or you start it by hand. It needs to be given a
Unity licence once.

> **Status:** the workflow has run. On 2026-11-19 it built all five packages;
> its third run was the first in which all five jobs passed. The Linux package
> was started on the development computer and shows the game. The Windows and
> macOS packages were only unpacked and looked at, and the two APKs were
> compared with the ones built locally; those four have **not been run**. See
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
built locally, but with another key (see [Things to know](#things-to-know)).

## 1. Give the repository a Unity licence

The free Personal licence works. You need a licence file, `Unity_lic.ulf`,
from a computer on which Unity Hub is signed in:

| System | Licence file |
|--------|--------------|
| Linux | `~/.local/share/unity3d/Unity/Unity_lic.ulf` |
| macOS | `/Library/Application Support/Unity/Unity_lic.ulf` |
| Windows | `C:\ProgramData\Unity\Unity_lic.ulf` |

**The file may not be there,** even though Unity Hub shows a licence and the
editor works (it was missing on the computer this game was developed on). To
create it, open **Unity Hub ▸ Preferences (the cog) ▸ Licenses ▸ Add ▸ Get a
free personal license**, then look again. GameCI's own
[activation guide](https://game.ci/docs/github/activation) describes the same
steps.

Two things about the Unity account:

- It needs a password of its own. If you sign in to Unity with Google or
  another service, first set a password in the account's security settings on
  Unity's website, and use that one.
- GameCI recommends a password made of letters and digits only: other
  characters have made the activation fail.

GitHub's machines do not accept the licence file itself, because it is bound
to the computer it was made on: the run shows the warning *"The license file's
machine binding doesn't match this machine"* and the build signs in with the
e-mail address and the password instead. Every run so far got its licence
that way, so the warning can be left alone.

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

The last two ask for the value; type it there, not on the command line, where
it would stay in the shell's history.

Secrets are only readable by workflows of this repository and are never shown
in logs. Do not put the licence file or the password into the repository
itself.

Until `UNITY_LICENSE` exists, every job ends after a few seconds with the
warning *"The UNITY_LICENSE secret is not set, so nothing was built"* – the
workflow is green but produces nothing.

## 2. Run it

- **Automatically:** push to `master`, or push a tag such as `v1.0`. Adding
  the secrets does not start a run by itself.
- **By hand:** on GitHub, **Actions ▸ Build packages ▸ Run workflow**; or
  `gh workflow run "Build packages"`.

The first run of each job imports every asset: that took 9–18 minutes for the
desktop jobs and 19–34 minutes for the Android ones. Later runs reuse a cached
`Library` folder and took about 4 minutes for a desktop job and 9–12 minutes
for an Android one.

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
- **Every APK from GitHub is signed with a different key.** The debug key is
  made anew in each job. Android does not install an update signed with
  another key, so remove the game from the phone before installing an APK from
  GitHub over one built on your computer, or over one from another run
  (`adb uninstall com.prdivyzavod.hra`).
- **Signing out of Unity Hub.** GameCI's guide says to sign out of Unity Hub
  once you have copied the file. Building on your own computer without the
  editor window, however, needs the Hub signed in. The runs so far were made
  while the Hub on the development computer stayed signed in, and they got
  their licence.
- **Pull requests from forks** do not start the workflow: they would not have
  access to the secrets.
- **To build fewer things,** delete entries from the `matrix` in the workflow
  file.

## If a job fails

| Symptom in the log | Likely cause |
|--------------------|--------------|
| *"No valid license"*, activation errors | `UNITY_LICENSE` is not the complete file, or belongs to another account than `UNITY_EMAIL`; or `UNITY_PASSWORD` is wrong or contains special characters, or the account has no password of its own (Google sign-in) |
| *"There was an error building the project"* straight after *"Build succeeded"* | GameCI did not find the result block it reads (`# Build results #` … `Build succeeded!`); `BuildGame.Report` prints it, a build method of your own has to as well |
| *"Failed to resolve the latest game-ci CLI release: GitHub API returned 403"* | GitHub refused GameCI's question about its newest version; the workflow passes `GITHUB_TOKEN` to avoid it. If it still happens, run the failed job again |
| *"no space left on device"* in an Android job | the image did not fit; add more paths to the *Free disk space* step |
| The image for this Unity version cannot be pulled | the project was moved to a Unity version GameCI has no image for yet; check <https://game.ci/docs/docker/versions> |
| `error CS…` | the code does not compile – the same error appears locally, see [Run the automated checks](run-the-automated-checks.md) |
