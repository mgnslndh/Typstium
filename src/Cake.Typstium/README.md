# Cake.Typstium

Compile [Typst](https://typst.app) documents in [Cake](https://cakebuild.net) builds, over the Typstium API.

```csharp
#addin nuget:?package=Cake.Typstium&loaddependencies=true
#tool nuget:?package=Typstium.Runtime.win-x64
#tool nuget:?package=Typstium.Runtime.linux-x64&include=./**/typst

Task("Docs").Does(() =>
{
    TypstCompile("docs/spec.typ", "artifacts/spec.pdf");
});
```

`loaddependencies=true` is required: the addin uses the `Typstium` package. Add one `#tool` line per platform the
script runs on; non-Windows packages need `&include=./**/typst`, because Cake's `#tool` only picks up `.exe` and `.dll`
files. Typst can also come from `TYPST_PATH` or an installed Typst. See the user guide in the repository:
`docs/cake.md`.
