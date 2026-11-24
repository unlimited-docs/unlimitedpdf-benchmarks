# Third-party notices

This repository's own code is licensed under Apache 2.0. It references NuGet packages rather than redistributing their
binaries, with the exception of the fonts in `assets/fonts`. Anyone redistributing build output must include the licences
and notices shipped in each resolved package, including native binaries. See [library licenses](docs/library-licenses.md)
for the versions and evidence.

## Redistributed in this repository

- **Noto Sans, Noto Sans Devanagari, Noto Sans Telugu, Noto Sans Tamil** — Copyright 2022 The Noto Project Authors.
  SIL Open Font License 1.1. Licence texts: `assets/fonts/OFL-latin-greek-cyrillic.txt`, `OFL-devanagari.txt`,
  `OFL-telugu.txt`, `OFL-tamil.txt`. Sources: https://github.com/notofonts. Unmodified.

## Referenced packages

- **UnlimitedPDF** 1.1.0 — © 2025–2026 UnlimitedPDF, Apache-2.0. Includes Unicode data under the Unicode License v3
  ([notices](https://github.com/unlimited-docs/UnlimitedPDF/blob/b457b0713a56e85be8e0a7a09a1a6dd4d4bffbd7/THIRD-PARTY-NOTICES.txt)).
  Depends on Microsoft.AspNetCore.Components.Web (MIT).
- **PDFsharp** and **PDFsharp-MigraDoc** 6.2.4 — © 2001–2026 empira Software GmbH, MIT.
- **PdfPig** 0.1.16 — Apache-2.0; its licence includes PDFBox, Adobe font metrics and CMap notices and terms.
- **SkiaSharp** 4.157.0, **SkiaSharp.HarfBuzz** 4.157.0, **HarfBuzzSharp** 14.4.0.200 — © Microsoft Corporation, MIT. The
  native assets include Skia (BSD-3-Clause), HarfBuzz (Old MIT), FreeType, libpng, zlib, libjpeg-turbo, expat and other
  components listed in the packages' `THIRD-PARTY-NOTICES.txt`; Skia's GIF decoder is MPL-1.1/GPL-2.0/LGPL-2.1, used
  under MPL-1.1.
- **Docnet.Core** 2.6.0 — MIT; bundles PDFium (BSD-3-Clause, with Apache-2.0 and other notices in its `LICENSE`).
  Used only for validation.
- **BenchmarkDotNet** 0.15.8 and its dependencies (Perfolizer, Pragmastat, Iced, Gee.External.Capstone,
  Microsoft.Diagnostics.*, CommandLineParser) — MIT.
- **Microsoft.Extensions.*** and **System.Security.Cryptography.Pkcs** — © .NET Foundation and contributors, MIT.
- **xunit** — Apache-2.0; **Microsoft.NET.Test.Sdk** — MIT.

All other names and trademarks belong to their respective owners.
