# Shipping Typst with your app

Typstium runs the official Typst CLI. The `Typstium.Runtime.<rid>` packages bring that binary through NuGet: Typstium's
build targets copy it next to your app, and Typstium finds it there at runtime with no configuration. This guide covers
what to reference and how to deploy. The design and its reasoning are in the
[spec](specs/runtime-packages.md); scenario IDs (RSn) below refer to its catalog.

## What to reference

| You build | Reference |
|---|---|
| An app for one platform | `Typstium` and that platform's package, e.g. `Typstium.Runtime.linux-x64` |
| An app for a few platforms | `Typstium` and each platform's package |
| An app for every platform | `Typstium` and `Typstium.Runtime.All` (all seven packages, about 170 MB to restore) |
| A library or library package | `Typstium` only. The app that uses it chooses the platforms (RS26) |

Platforms: `win-x64`, `win-arm64`, `osx-x64`, `osx-arm64`, `linux-x64`, `linux-arm64`, `linux-arm`. The Linux
binaries are static musl builds and run on glibc and Alpine alike. A `win-x64` or `osx-x64` binary is also used on Arm64
Windows or Apple silicon when it's the only one referenced (emulation).

```shell
dotnet add package Typstium
dotnet add package Typstium.Runtime.linux-x64
```

What gets copied:

- **No `RuntimeIdentifier`** (`dotnet build`, `dotnet run`, a portable `dotnet publish`): every referenced runtime, under
  `typst/<rid>/`. Typstium picks the native one at runtime.
- **A `RuntimeIdentifier`** (`dotnet publish -r linux-x64`, or `<RuntimeIdentifier>` in the project): only the matching
  runtime. Distro and musl RIDs map to the portable ones (`ubuntu.22.04-x64`, `linux-musl-x64` → `linux-x64`).
- **Through a library**: an app that references a library which references runtime packages gets them transitively and
  makes its own selection; the library's copies don't reach the app's output (RS25).

Next to the binaries, the build writes `typstium.runtime.json`, which tells Typstium where they are. Deploy it with
them. `TYPST_PATH`, when set, still takes precedence over the bundled runtime.

## MSBuild properties

| Property | Default | Meaning |
|---|---|---|
| `TypstiumCopyRuntime` | `true` | Master switch. `false` copies nothing and writes no manifest |
| `TypstiumCopyRuntimeOnBuild` | `true` | Copy to the output folder on build |
| `TypstiumCopyRuntimeOnPublish` | `true` | Copy to the publish folder |
| `TypstiumRuntimeIdentifiers` | (empty) | Semicolon-separated list that replaces the automatic selection, e.g. `win-x64;linux-x64` |
| `TypstiumRuntimeDirectory` | `typst/{rid}` | Folder for each binary, relative to the output or publish folder. `{rid}` is replaced by the RID |

On the command line, write a `;` in a list as `%3B`: `-p:TypstiumRuntimeIdentifiers=win-x64%3Blinux-x64`.

## Diagnostics

| Code | Kind | When | What to do |
|---|---|---|---|
| `TYPSTIUM0001` | Warning | A RID is set, but no referenced runtime matches it | Reference the matching package, or ignore it if Typst is installed where the app runs |
| `TYPSTIUM0002` | Error | `TypstiumRuntimeDirectory` has no `{rid}` and more than one runtime is selected | Add `{rid}`, or select one runtime |
| `TYPSTIUM0003` | Error | `TypstiumRuntimeDirectory` is empty, absolute, or contains `..`, `:` or a quote | Use a relative folder inside the output folder |
| `TYPSTIUM0004` | Warning | `TypstiumRuntimeIdentifiers` names a RID that no referenced package provides | Reference that package, or remove it from the list |
| `TYPSTIUM0005` | Warning | A runtime package uses a contract this version of Typstium doesn't know | Update the `Typstium` package |
| `TYPSTIUM0006` | Message | The target platform can't start processes (Android, iOS, Mac Catalyst, tvOS, browser); nothing is copied | Nothing; Typstium can't run Typst there |

## Containers

- **Publish inside the image** (RS9): `RUN dotnet publish -c Release -r linux-x64 -o /app` in a Linux build stage
  copies only the Linux binary and sets its execute bit. Alpine images work with the same `linux-x64` package (RS11).
- **SDK container publish** (RS10): `dotnet publish -t:PublishContainer -r linux-x64`. For multi-arch images, set
  `ContainerRuntimeIdentifiers` (e.g. `linux-x64;linux-arm64`) and reference both packages; each image gets its own
  binary.
- **Built on Windows, copied into a Linux image** (RS12): Windows can't set the execute bit. Typstium sets it at
  runtime, but only if the file system is writable. For a read-only root file system, publish with `-r` inside the
  image, or add `RUN chmod +x /app/typst/linux-x64/typst` to the Dockerfile.
- **The image already has Typst** (RS13): set `TypstiumCopyRuntime=false` (or don't reference a runtime package);
  Typstium finds Typst through `TYPST_PATH` or `PATH`.

## Single-file and Native AOT

`PublishSingleFile`, Native AOT and trimmed publishes work unchanged (RS4, RS5). The binary is never bundled: it stays
next to the app in `typst/<rid>/`, with the manifest. Ship the whole publish folder, not only the app's executable.

## .NET Framework

- **SDK-style projects**: the .NET SDK infers `RuntimeIdentifier` `win-x86` for .NET Framework executables, and a
  32-bit app may run on x64 or Arm64 Windows, so every referenced `win-*` runtime is copied (RS40). To ship one,
  reference only `Typstium.Runtime.win-x64`, or set `TypstiumRuntimeIdentifiers=win-x64`.
- **packages.config**: reference `Typstium` and the RID package (e.g. `Typstium.Runtime.win-x64`) directly in the app.
  packages.config doesn't run transitive build logic, so `Typstium.Runtime.All` and references through another
  package don't work there (RS18).
- **Classic ASP.NET (System.Web)**: binaries go to `bin/typst/<rid>/` and are included in WebDeploy packages (RS17).

## Installers and deployment

- **Installers** (Velopack, Inno Setup, WiX; RS23): build the installer from the publish folder and include the
  `typst/` folder and `typstium.runtime.json`. Publish with `-r` to ship one platform's binary.
- **Publish to a clean folder.** Publish folders aren't cleaned, so a reused folder can keep binaries from an earlier
  configuration. The manifest is always rewritten and points at the current ones, but the stale files still ship.
- **AWS Lambda (zip)** (RS14): the deployment folder is read-only, so Typstium can't set the execute bit at runtime.
  Publish on Linux or in a Linux container (`dotnet publish -r linux-x64`), and keep file modes when you zip. Azure
  App Service and Azure Functions work like any other publish.
- **Locked-down Windows** (RS37): the binary is a third-party executable in the app folder. AppLocker or WDAC policies
  may block it, and Defender or SmartScreen may scan or flag it. Allow it by path or hash in your policy, or install
  Typst through an approved channel, set `TypstiumCopyRuntime=false` and point `TYPST_PATH` at it.

## Feeds, versions and sizes

- **Private feeds, Central Package Management, offline restore** (RS28): nothing special. Mirror the `Typstium.Runtime.*`
  packages you use like any other package; with package source mapping, map `Typstium.*` to that source. With CPM, add a
  `PackageVersion` for each runtime package.
- **Choosing a Typst version** (RS39): a runtime package's version is the Typst version it holds (`0.15.1`).
  `Typstium.Runtime.All` pins its RID packages to its own version exactly, so for another Typst version reference the
  RID packages directly at that version instead. Typstium requires Typst 0.15 or newer.
- **Package sizes**: each RID package is 22–26 MB (Typst 0.15.1), and each binary is about 50 MB once restored; `Typstium.Runtime.All` restores all seven, about 170 MB. On slow
  feeds or CI caches, reference only the platforms you deploy to.

Typst is licensed under Apache-2.0. Each runtime package carries Typst's `LICENSE` and `NOTICE`, and the build
copies both next to the binary in your output and publish folders, so shipping the publish folder ships the notices
too.
