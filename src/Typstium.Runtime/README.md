# Typstium.Runtime.<rid>

The official [Typst](https://typst.app) command-line compiler for one platform, repackaged unchanged from
[Typst's GitHub releases](https://github.com/typst/typst/releases) for [Typstium](https://github.com/mgnslndh/Typstium).
The package version is the Typst version it contains. Typst is licensed under Apache-2.0; its license is included next
to the binary.

Reference it together with the `Typstium` package. Typstium's build targets copy the binary next to your app, and
Typstium finds it there at runtime. Use `Typstium.Runtime.All` to reference every platform at once.
