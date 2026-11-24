# Library licensing research

Policy: packages must permit proprietary commercial use without a fee, revenue
threshold, usage cap, watermark or source-disclosure obligation merely from
using the library. Permissive licenses still require preservation of notices.
This is technical due diligence, not legal advice. Verification date:
2026-11-24. Version and publication dates below are from NuGet metadata.

| Library / package | Selected / latest stable | Selected release | SPDX | Evidence and decision |
|---|---|---|---|---|
| [UnlimitedPDF](https://www.nuget.org/packages/UnlimitedPDF/1.1.0) | 1.1.0 / 1.1.0 | 2026-11-18 | Apache-2.0 | [Source and license at package commit](https://github.com/unlimited-docs/UnlimitedPDF/blob/b457b0713a56e85be8e0a7a09a1a6dd4d4bffbd7/LICENSE); included, permitted with notice obligations. |
| [PDFsharp](https://www.nuget.org/packages/PDFsharp/6.2.4) | 6.2.4 / 6.5.0 | 2026-01-06 | MIT | [6.2.4 license](https://github.com/empira/PDFsharp/blob/v6.2.4/LICENSE); included under MIT. Latest [6.5.0 NuGet metadata](https://api.nuget.org/v3-flatcontainer/pdfsharp/6.5.0/pdfsharp.nuspec) says AGPL-3.0-only; excluded under this policy. |
| [PdfPig](https://www.nuget.org/packages/PdfPig/0.1.16) | 0.1.16 / 0.1.16 | 2026-08-22 | Apache-2.0 | [Source and license at package commit](https://github.com/UglyToad/PdfPig/blob/a7bb35662bbbf405efddad50aedc9bcdcf515afc/LICENSE); included, subject also to its external-component notices. |

PDFsharp 6.2.4 is an older, permissive release, not the current 6.5.0
(2026-10-14). Whether security fixes will continue to be backported to 6.2.x
is unverified. Older versions can miss subsequent fixes; review advisories
and re-evaluate before deployment. Its dependency on
System.Security.Cryptography.Pkcs 8.0.1 is overridden with 10.0.15 because
NuGet flagged GHSA-2fmv-rv2c-59x7 and GHSA-846f-f764-76mx. No known
vulnerabilities were reported by `dotnet list package --vulnerable
--include-transitive` on the verification date; this does not guarantee safety.
MigraDoc is PDFsharp's official layout companion, not an independent library;
it is not referenced or benchmarked and its exact version must be separately
verified before use.

UnlimitedPDF's net10 package depends on Microsoft.AspNetCore.Components.Web
10.0.0 despite the package description's “no third-party dependencies” wording.
PdfPig's license contains PDFBox, Adobe font metrics and CMap notices.
See [third-party notices](../THIRD-PARTY-NOTICES.md). Lock files specify the
complete resolved dependency graph; license review of every transitive package
and any redistributed binaries remains outstanding. The fixture uses no external
font, image, browser or document. CI dependency review supplements, but does
not replace, manual verification.

## Other candidates

- [QuestPDF](https://www.nuget.org/packages/QuestPDF/) is source-available
  under community/commercial terms with eligibility restrictions. It is
  excluded pending a version-specific policy review; do not assume MIT.
- [iText](https://www.nuget.org/packages/itext7/) offers AGPL/commercial
  licensing; excluded under this policy. AGPL permits commercial use, but
  its source-sharing obligations can conflict with proprietary applications.
- [SkiaSharp](https://www.nuget.org/packages/SkiaSharp/4.157.0) 4.157.0
  has MIT NuGet metadata and supports PDF drawing; it is not included yet.
  Native binaries and their notices, and equivalence of output, need review.
- [Microsoft.Playwright](https://www.nuget.org/packages/Microsoft.Playwright/)
  is a browser automation/HTML conversion candidate, not equivalent to a
  general document layout API. Browser licensing, installation and process
  resources require separate verification; not included.

Selection is limited to the currently implemented workload, not a claim that
these are the best or most popular libraries. Popularity, ongoing maintenance
and additional candidates require further research.
