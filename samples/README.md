# Samples

Sample templates used by the spike, and later by sample apps and tests. All names and data are fictional:
**Fjordlight Systems** and **Fjordlight Studio** don't exist.

| Folder | Use case | Exercises |
| --- | --- | --- |
| `templates/license/` | B: web API renders a license certificate | JSON data, SVG logo, table, multi-page with running header and page count, bundled font |
| `templates/release-notes/` | A: build pipeline renders release notes | Markdown content via `@preview/cmarker`, PNG logo, cover page, outline, `raw-typst: false` |
| `templates/release-notes/scriban/` | A, data-driven | Scriban template + JSON model rendered to Markdown, including injection test strings |
| `fonts/source-sans-3/` | All | Bundled font for reproducible output; see license below |

Compile from the repo root:

```shell
typst compile --root samples/templates/license --font-path samples/fonts --ignore-system-fonts \
  samples/templates/license/license.typ license.pdf

typst compile --root samples/templates/release-notes --font-path samples/fonts --ignore-system-fonts \
  samples/templates/release-notes/release-notes.typ release-notes.pdf
```

Paths inside templates start with `/` and resolve against `--root`, so a template behaves the same when it's
copied into a per-compile sandbox folder.

## Third-party content

- **Source Sans 3** by Adobe, licensed under the SIL Open Font License 1.1. See `fonts/source-sans-3/LICENSE.md`.
- **cmarker** (`@preview/cmarker`) by Sabrina Jewson, MIT license. Downloaded by Typst from Typst Universe; not stored here.
