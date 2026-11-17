# Tutorial: build and run from source

In this tutorial you will open the project in Unity, run the game inside the
editor and produce a standalone build. Allow half an hour, most of it waiting
for downloads and the first import.

You need a 64-bit computer running Linux, Windows or macOS with about 15 GB of
free disk space, and a free Unity account.

## 1. Get the source

```sh
git clone git@github.com:xjrk58/unity-hra.git
cd unity-hra
```

## 2. Install Unity

1. Install **Unity Hub** from <https://unity.com/download> and sign in.
2. In the Hub open **Installs ▸ Install Editor** and install version
   **6000.4.11f1** (Unity 6). If it is not offered, use the *Archive* tab.
   No extra modules are needed for this tutorial.

The game uses the Universal Render Pipeline, which is part of the editor;
nothing else is downloaded.

## 3. Open the project

In the Hub choose **Projects ▸ Add ▸ Add project from disk** and select the
cloned folder, then open it.

The first opening imports every texture and model. This takes several minutes;
later openings take seconds.

## 4. Play in the editor

1. In the *Project* window open `Assets/Scenes/Main.unity`.
2. Press the **Play** button at the top.

The title screen appears in the *Game* view. Click **Hrát sám** and play: click
into the Game view first so that it has the keyboard and mouse. Press `Esc` to
free the mouse, and Play again to stop.

Notice that the *Hierarchy* contained a single object, `Bootstrap`, before you
pressed Play: the whole world is built by code when the scene starts.

## 5. Make a build

In the editor's menu choose **Tools ▸ Prdivý závod ▸ Build player**.

After a minute or two the console prints
`[Setup] build Succeeded: …` and the game is in `Builds/PrdivyZavod/`. Start it
from there.

## What you have done

You have the project running from source and a standalone build of it. Where to
go next:

- other platforms: [Mac with Apple silicon](../how-to/build-on-a-mac-with-apple-silicon.md),
  [Android](../how-to/build-the-android-apk.md), [old Android phones](../how-to/build-for-old-android-phones.md);
- how the code is organised: [Project layout and scripts](../reference/project-layout.md);
- why there is only one object in the scene: [How the world is built](../explanation/how-the-world-is-built.md).
