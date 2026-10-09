// Sample release notes layout (use case A).
// The .typ file owns the layout; the content is Markdown rendered by cmarker.
// Override the Markdown file with `--input content=<path>` (resolved against --root).
#import "@preview/cmarker:0.1.10"

#let content-path = sys.inputs.at("content", default: "/release-notes.md")
#let product = sys.inputs.at("product", default: "Fjordlight Studio")
#let version = sys.inputs.at("version", default: "4.2.0")

#let brand = rgb("#14365d")
#let accent = rgb("#7cc4f0")
#let muted = rgb("#4a6a8a")

#set document(title: [#product #version release notes], author: "Fjordlight Systems")
#set text(font: ("Source Sans 3", "Libertinus Serif"), size: 10.5pt, lang: "en")
#set par(justify: true)
#set heading(numbering: none)
#show heading.where(level: 1): it => {
  pagebreak(weak: true)
  set text(20pt, fill: brand)
  block(below: 0.8em, it)
}
#show heading.where(level: 2): set text(14pt, fill: brand)
#show heading.where(level: 3): set text(11.5pt, fill: muted)
#show raw.where(block: true): block.with(fill: luma(245), inset: 8pt, radius: 3pt, width: 100%)
#show quote.where(block: true): it => block(
  stroke: (left: 3pt + accent), inset: (left: 10pt, y: 4pt), fill: rgb("#f3f9fd"), width: 100%, it.body,
)
#show table: set table(
  stroke: (x, y) => if y == 0 { (bottom: 1pt + brand) } else { (bottom: 0.4pt + luma(220)) },
  inset: (x: 6pt, y: 5pt),
)

// Cover page
#page(margin: 2.5cm, header: none, footer: none)[
  #image("/logo.png", height: 1.6cm)
  #v(1fr)
  #text(32pt, weight: "bold", fill: brand)[#product]\
  #text(20pt, fill: muted)[Release notes · version #version]
  #v(0.4cm)
  #line(length: 30%, stroke: 3pt + accent)
  #v(2fr)
  #text(9pt, fill: muted)[Fjordlight Systems is a fictional company used by the Typstium samples.]
]

#set page(
  paper: "a4",
  margin: (x: 2.2cm, top: 2.5cm, bottom: 2.5cm),
  header: [
    #set text(8pt, fill: muted)
    #product #version #h(1fr) Release notes
  ],
  footer: context [
    #set text(8pt, fill: muted)
    #h(1fr) #counter(page).display()
  ],
)
#counter(page).update(1)

#outline(title: [Contents], depth: 2)

// Untrusted-content posture: raw-typst is off so Markdown cannot inject Typst code.
// Images are resolved by Typst, so they stay inside --root.
#cmarker.render(
  read(content-path),
  raw-typst: false,
  h1-level: 1,
  scope: (image: (source, ..args) => image(source, width: 50%, ..args)),
)
