# Assets

| Folder | Contents |
|---|---|
| `fonts/` | Noto Sans (Regular, Bold, Italic), Noto Sans Devanagari, Noto Sans Telugu and Noto Sans Tamil, unmodified, with their SIL Open Font License 1.1 texts. Every library receives these same files. |

Images, HTML, business data and manipulation input PDFs are not stored here: they are generated deterministically by
`src/UnlimitedPDF.Benchmarks.Core/Fixtures` (seeded `System.Random`, an in-repo PNG encoder, SkiaSharp's JPEG encoder at
quality 85, and an independent PDF writer), so no third-party images or documents are redistributed. The fixture version
and a SHA-256 over all fixtures and fonts are recorded in each run's `manifest.json`.

## Font sources (downloaded 2026-11-24)

| File | Source | SHA-256 |
|---|---|---|
| NotoSans-Regular.ttf | notofonts.github.io `fonts/NotoSans/hinted/ttf` | 478c558ea716033cd60c03438f628dfa75694dcf6b5f6d505a2f05fd2b4f3823 |
| NotoSans-Bold.ttf | same | 1df075a380fc7cb898acf64c1f7b3b4dd780de3caa860178bf929de35817a913 |
| NotoSans-Italic.ttf | same | 467e3f89eeca4108bb8710a2b9e0cf2281ac56d5b0609211a83776d0505eecb5 |
| NotoSansDevanagari-Regular.ttf | `fonts/NotoSansDevanagari/hinted/ttf` | 4e3c66638958c3e2ab5d37f47a8deb89fffeb7be9985c665a519bbc7ba762313 |
| NotoSansTelugu-Regular.ttf | `fonts/NotoSansTelugu/hinted/ttf` | b274780b69d1d23fe84b55e809a152cb2ac5306d33864b1f87622f6971871aae |
| NotoSansTamil-Regular.ttf | `fonts/NotoSansTamil/hinted/ttf` | 3c0a186feb3c63c7f6d63e1511dcdc144e745ae09b98e217c83f3e317974f6f9 |

Licence texts come from the matching repositories (`notofonts/latin-greek-cyrillic`, `devanagari`, `telugu`, `tamil`).
