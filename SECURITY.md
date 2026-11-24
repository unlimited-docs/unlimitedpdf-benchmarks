# Security policy

## Reporting a vulnerability

Please do not open a public issue for security problems. Report them privately through GitHub's
**Security → Report a vulnerability** (private vulnerability reporting) on this repository, with steps to reproduce.
Maintainers aim to acknowledge reports within seven days.

Vulnerabilities in the benchmarked libraries themselves should be reported to those projects:
[UnlimitedPDF](https://github.com/unlimited-docs/UnlimitedPDF), [PDFsharp](https://github.com/empira/PDFsharp),
[PdfPig](https://github.com/UglyToad/PdfPig), [SkiaSharp](https://github.com/mono/SkiaSharp) and
[Docnet](https://github.com/GowenGit/docnet).

## Scope and known considerations

- This repository is a benchmark suite, not a library for production use. It processes only PDFs, images, fonts and HTML
  that it generates itself.
- PDFsharp 6.2.4 is deliberately pinned below the AGPL-licensed 6.5.0; it may not receive security fixes. Its vulnerable
  `System.Security.Cryptography.Pkcs` 8.0.1 dependency is overridden with 10.0.15.
- The PDFium build bundled in Docnet.Core 2.6.0 (validation only) dates from 2023 and predates later PDFium security
  fixes. Do not use this suite's validator on untrusted PDFs.
- CI fails on known vulnerable packages (`NuGetAudit` and `dotnet list package --vulnerable --include-transitive`) and on
  licence-policy violations. No credentials or secrets are needed or stored.
