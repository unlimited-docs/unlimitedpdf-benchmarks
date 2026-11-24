# Library licensing research

**Policy.** A library in the primary suite must permit use in proprietary commercial applications without licence
fees, revenue thresholds, developer/server/deployment/document limits, watermarks, or disclosure of application source
code merely from using it. Permissive licences (MIT, Apache-2.0, BSD) are preferred. Copyleft licences such as the AGPL do
permit commercial use; their source-sharing obligations are simply outside this project's permissive policy. Notice and
attribution requirements still apply. This is technical due diligence, not legal advice.

**Verification date:** 2026-11-24, for the exact versions below. Versions, release dates and licence expressions come from
NuGet.org metadata (nuspec and registration); licence texts from the linked repositories at the package commit.

## Included libraries

| Library | NuGet id | Selected | Latest stable | Released | SPDX | Licence | Source | NuGet |
|---|---|---|---|---|---|---|---|---|
| UnlimitedPDF | `UnlimitedPDF` | 1.1.0 | 1.1.0 | 2026-11-18 | Apache-2.0 | [LICENSE](https://github.com/unlimited-docs/UnlimitedPDF/blob/b457b0713a56e85be8e0a7a09a1a6dd4d4bffbd7/LICENSE) | [GitHub](https://github.com/unlimited-docs/UnlimitedPDF) | [NuGet](https://www.nuget.org/packages/UnlimitedPDF/1.1.0) |
| PDFsharp | `PDFsharp` | 6.2.4 | 6.5.0 | 2026-01-06 | MIT | [LICENSE](https://github.com/empira/PDFsharp/blob/v6.2.4/LICENSE) | [GitHub](https://github.com/empira/PDFsharp) | [NuGet](https://www.nuget.org/packages/PDFsharp/6.2.4) |
| PDFsharp-MigraDoc | `PDFsharp-MigraDoc` | 6.2.4 | 6.5.0 | 2026-01-06 | MIT | [LICENSE](https://github.com/empira/PDFsharp/blob/v6.2.4/LICENSE) | [GitHub](https://github.com/empira/PDFsharp) | [NuGet](https://www.nuget.org/packages/PDFsharp-MigraDoc/6.2.4) |
| PdfPig | `PdfPig` | 0.1.16 | 0.1.16 | 2026-08-22 | Apache-2.0 | [LICENSE](https://github.com/UglyToad/PdfPig/blob/a7bb35662bbbf405efddad50aedc9bcdcf515afc/LICENSE) | [GitHub](https://github.com/UglyToad/PdfPig) | [NuGet](https://www.nuget.org/packages/PdfPig/0.1.16) |
| SkiaSharp | `SkiaSharp` (+ `SkiaSharp.NativeAssets.Linux.NoDependencies`) | 4.157.0 | 4.157.0 | 2026-11-18 | MIT | [MIT](https://licenses.nuget.org/MIT) (LICENSE.txt in package) | [GitHub](https://github.com/mono/SkiaSharp) | [NuGet](https://www.nuget.org/packages/SkiaSharp/4.157.0) |
| SkiaSharp.HarfBuzz / HarfBuzzSharp | `SkiaSharp.HarfBuzz`, `HarfBuzzSharp` (+ `.NativeAssets.Linux`) | 4.157.0 / 14.4.0.200 | same | 2026-11-18 | MIT | as SkiaSharp | as SkiaSharp | [NuGet](https://www.nuget.org/packages/SkiaSharp.HarfBuzz/4.157.0) |

Commercial-use eligibility: all included packages are eligible under the policy.

### Restrictions and transitive considerations

- **UnlimitedPDF 1.1.0** depends on `Microsoft.AspNetCore.Components.Web` 10.0.0 and its Microsoft.Extensions
  dependencies (all MIT), despite the package description's "no third-party dependencies". The package ships
  `THIRD-PARTY-NOTICES.txt` for Unicode data (Unicode License v3). Pure managed; no native binaries.
- **PDFsharp / PDFsharp-MigraDoc 6.2.4** is an older release chosen because 6.5.0 (2026-10-14) and later are
  `AGPL-3.0-only` (confirmed in the [6.5.0 nuspec](https://api.nuget.org/v3-flatcontainer/pdfsharp/6.5.0/pdfsharp.nuspec)).
  Whether security fixes will be backported to 6.2.x is unverified; older releases can miss later fixes. Its dependency
  `System.Security.Cryptography.Pkcs` 8.0.1 has advisories GHSA-2fmv-rv2c-59x7 and GHSA-846f-f764-76mx, so it is overridden
  with 10.0.15. Dependabot ignores PDFsharp ≥ 6.5.0, and the CI licence gate rejects it.
- **PdfPig 0.1.16**'s licence also carries PDFBox, Adobe font metrics and CMap notices and terms.
- **SkiaSharp 4.157.0 and HarfBuzzSharp** ship native binaries whose `THIRD-PARTY-NOTICES.txt` covers Skia (BSD-3-Clause),
  HarfBuzz ("Old MIT"), libpng, zlib, libjpeg-turbo, expat, FreeType and others under BSD, MIT, Apache-2.0 and similar
  terms. Skia's GIF decoder is tri-licensed MPL-1.1/GPL-2.0/LGPL-2.1; it is used under MPL-1.1, whose file-level copyleft
  applies only to modifications of those files, not to applications using the library. Redistributors must include these
  notices. The Linux `NoDependencies` native build avoids a fontconfig dependency; fonts are loaded from files.
- **SkiaSharp 4.157.0** was released six days before verification. It is the latest stable version; a native crash with
  lazily decoded PNGs in PDF output was observed and worked around (see methodology.md).

### Validation-only and infrastructure packages

| Package | Version | SPDX | Notes |
|---|---|---|---|
| Docnet.Core | 2.6.0 (2023-09-04) | MIT | PDFium validator only, never benchmarked. Bundles a 2023 PDFium build (BSD-3-Clause with Apache-2.0 and other notices). That PDFium predates later security fixes; it only parses PDFs generated locally by this suite. |
| BenchmarkDotNet | 0.15.8 | MIT | Transitive: Perfolizer, Pragmastat, Iced, Gee.External.Capstone, Microsoft.Diagnostics.* (MIT), CommandLineParser 2.9.1 (MIT, licence file). |
| Microsoft.Extensions.Logging.Console | 10.0.15 | MIT | Progress logging outside measured operations. |
| xunit 2.9.3, xunit.runner.visualstudio 3.1.5, Microsoft.NET.Test.Sdk 18.12.0 | | Apache-2.0 / MIT | Tests only. |

All 77 packages resolved in the lock files on the verification date are MIT or Apache-2.0; three declare their licence by
file or URL (CommandLineParser 2.9.1, Microsoft.DotNet.PlatformAbstractions 3.1.6, xunit.abstractions 2.0.3) and were read
by hand. `dotnet list package --vulnerable --include-transitive` reported no known vulnerabilities; this does not
guarantee safety.

### Fonts and other assets

The Noto Sans, Noto Sans Devanagari, Noto Sans Telugu and Noto Sans Tamil fonts in `assets/fonts` are licensed under
the SIL Open Font License 1.1, which allows embedding and redistribution with the licence (`OFL-*.txt`). Images, HTML,
business data and manipulation input PDFs are generated deterministically by the suite.

## Excluded candidates

| Candidate | Licence | Reason for exclusion |
|---|---|---|
| Microsoft.Playwright 1.65.1 | MIT wrapper; driver Apache-2.0; bundled Node.js MIT; browsers under their own terms | HTML to PDF with Playwright requires downloading a Chromium build separately and running it, plus the bundled Node.js driver, as external processes. The suite compares self-contained NuGet libraries whose complete dependency chain is restored and reviewed from NuGet; a separately installed browser falls outside that and makes cold-start, memory and deployment comparisons non-equivalent. It is therefore excluded from the comparison and from the benchmarks, and UnlimitedPDF's HTML-to-PDF results are reported without a competitor. |
| PDFsharp / PDFsharp-MigraDoc 6.5.0 and later | AGPL-3.0-only | Outside the permissive policy; 6.2.4 (MIT) is used instead. |
| QuestPDF (current) | Community licence with revenue-based eligibility, or commercial | Not OSI-approved; eligibility depends on revenue. |
| iText (current, `itext7`/`itext`) | AGPL-3.0 or commercial | Outside the permissive policy. |

Selection reflects the scenarios implemented, not a claim that these are the only or best libraries. Additional
candidates must pass the same version-specific review, including transitive packages and native binaries.
