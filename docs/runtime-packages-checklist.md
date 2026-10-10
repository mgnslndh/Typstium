# Runtime packages: manual checks

The scenarios in [the runtime packages spec](specs/runtime-packages.md#9-scenario-catalog) that CI can't check yet.
Record each run with a date, the Typst and Typstium versions, and the result.

| ID | Check | How | Last run | Result |
|---|---|---|---|---|
| RS12 | Built on Windows without a RID, copied into a Linux image with a read-only root filesystem | `docker run --read-only`; expect a clear "permission denied" start failure, then fix with `chmod` in the Dockerfile | — | — |
| RS14 | Azure App Service (Windows and Linux) and Azure Functions (isolated worker) | Deploy the end-to-end sample app; call an endpoint that compiles | — | — |
| RS15 | ASP.NET Core on modern .NET under IIS (in-process and out-of-process) | Publish to a local IIS site | — | — |
| RS16 | ASP.NET Core 2.x on .NET Framework under IIS | SDK-style net48 web app with Microsoft.AspNetCore 2.3 packages; found through bin/ | — | — |
| RS17 | Classic WebDeploy package (msdeploy zip), beyond the FileSystem publish CI tests | `msbuild -t:Package`; check bin\typst\ in the zip; deploy to IIS | — | — |
| RS36 | macOS Gatekeeper | Download a published app as a zip through a browser (quarantine flag), unzip, run | — | — |
| RS41 (Windows) | Windows on Arm, while the repository is private | `./build.ps1 --target=RuntimeEndToEndTests` on an Arm64 Windows machine | — | — |

## Not yet run on Linux or macOS locally

The CI jobs added with this checklist are the first Linux and macOS runs of the `chmod` targets (RS35), the
containers (RS9–RS11), Arm emulation on macOS (RS41) and the Linux pack. Until a run is green, treat those as
unverified.
