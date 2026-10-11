# Brand assets

| File | Use on | Size |
| --- | --- | --- |
| `typstium-icon.svg` | Anywhere: the square mark on its own gradient tile, so it works on light and dark backgrounds | 200 × 200 |
| `typstium-logo-dark-text.svg` | Light backgrounds: the mark plus the wordmark in dark text (`#2A0F2E`) | 774 × 200 |
| `typstium-logo-white-text.svg` | Dark backgrounds: the same with white text | 774 × 200 |

The two logos differ only in the wordmark's color (and their internal SVG IDs, so both can sit in one page).

## On GitHub

The repository README picks the logo that matches the viewer's GitHub theme with `<picture>` and
`prefers-color-scheme`, the approach most projects with light and dark variants use (uv, Vite, QuestPDF, Biome):

```html
<picture>
  <source media="(prefers-color-scheme: dark)" srcset="assets/typstium-logo-white-text.svg">
  <source media="(prefers-color-scheme: light)" srcset="assets/typstium-logo-dark-text.svg">
  <img alt="Typstium" src="assets/typstium-logo-dark-text.svg" width="400">
</picture>
```

- GitHub evaluates the media query against the theme chosen in the viewer's GitHub settings.
- Avoid the older `#gh-dark-mode-only` / `#gh-light-mode-only` URL suffixes, which GitHub has deprecated.
- Don't make one SVG switch its own colors with an internal `@media (prefers-color-scheme: dark)` rule. GitHub serves
  README images through its image proxy as plain images, where that query follows the operating system rather than
  the GitHub theme.
- Relative paths only render on GitHub. They also work while the repository is private.

## Elsewhere

See "Brand and publishing" in [docs/design.md](../docs/design.md#roadmap-open-questions-and-next-steps) for what the
NuGet packages still need.
