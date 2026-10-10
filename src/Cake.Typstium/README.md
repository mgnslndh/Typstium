# Cake.Typstium

Compile [Typst](https://typst.app) documents in [Cake](https://cakebuild.net) builds, over the Typstium API.

```csharp
#addin nuget:?package=Cake.Typstium&loaddependencies=true
#tool nuget:?package=Typstium.Runtime.linux-x64

Task("Docs").Does(() =>
{
    TypstCompile("docs/spec.typ", "artifacts/spec.pdf");
});
```

`loaddependencies=true` is required: the addin uses the `Typstium` package. Typst comes from a `#tool` runtime package,
`TYPST_PATH`, or an installed Typst. See the user guide in the repository: `docs/cake.md`.
