# Typstium.Runtime.All

Every `Typstium.Runtime.<rid>` package at the same Typst version: about 110 MB to restore. Prefer referencing only the
platforms you deploy to.

The official [Typst](https://typst.app) command-line compiler for each platform, repackaged unchanged from
[Typst's GitHub releases](https://github.com/typst/typst/releases) for [Typstium](https://github.com/mgnslndh/Typstium).
The package version is the Typst version it contains. Typst is licensed under Apache-2.0; its LICENSE and NOTICE files are
included next to the binary.

Reference it together with the `Typstium` package. Typstium's build targets copy the binary next to your app, and
Typstium finds it there at runtime.

Which packages to reference, the MSBuild properties, containers, single-file, .NET Framework and installers:
[Shipping Typst with your app](https://github.com/mgnslndh/Typstium/blob/main/docs/runtime-packages.md).
