# Local website validation

Historical first-pass record. The English-only locale restriction and very
bright palette below were replaced by the follow-up in LANGUAGE_UI_VALIDATION.md.

12 October 2026, Asia/Saigon. Local-only work; no Git commit/push or site
deployment. Existing environment credentials and non-English message files
were preserved. No game, installer or mod-release artifact was modified.

## Implemented

- Bright Business Tour palette, pentagonal brand mark, code-native 40-cell
  hero illustration, feature inventory and responsive guide sections.
- English copy accurately describes Five Realms, all-client installation,
  the 40-cell V3 board and existing gameplay/AFK verification limits.
- Only English is active. Other locale files stay on disk for later key
  updates rather than serving copied Far Far West instructions.
- SEO title and 168-character description, Open Graph/Twitter metadata and
  safe SoftwareApplication JSON-LD. Canonical/social image tags need explicit
  public configuration; no domain, reviews or installation figures are invented.
- Correct GitHub repository and release assets. The old repository slug
  redirects to business-tour-extend-5-players; actual published tag "Release"
  attaches BusinessTourFiveRealms-0.2.2-win-x64.zip. The package version is
  taken from this filename. Unrelated historical RARs are not relabeled as
  current ZIP packages; the All releases link remains available.
- Network/shape failures yield an honest empty state, stable packages sort
  before previews, and private tokens are not part of returned page data.

## Passed

- `pnpm check`: 0 errors, 0 warnings.
- `pnpm build`: production client/server and retained Vercel adapter output.
- `node --experimental-strip-types --test tests/releases.test.mjs`: 8 tests.
  Covers real non-versioned tags, stable ordering, duplicates, malicious or
  unrelated asset links, ambiguous files, attached checksum discovery,
  configuration, API failures, token isolation and English metadata.
- Production preview returns HTTP 200 with the correct ZIP link, English
  language, metadata and structured data. No copied Far Far West brand or
  private token was present in the checked response. Inactive /vi returns 404.
- Browser QA: desktop default viewport and 320/390/768/1024 pixel widths.
  No horizontal overflow after the narrow-header correction. Mobile menu
  opens/closes and navigates to sections; native FAQ disclosure opens.
  Production-preview browser logs have no errors or warnings for that origin.
- One H1, no broken in-page anchors, safe parsed JSON-LD and actual version0.2.2.

Screenshots: `../work/diagnostics/web-redesign-2026-10-12/desktop.jpg` and
`mobile-faq.jpg`. Preview servers are temporary and stopped after QA.

These checks do not promise SEO ranking, validate every device/browser, or
certify the game mod's network stability. Set PUBLIC_SITE_URL to the actual
HTTPS homepage before deployment to enable canonical/og:url. Add a real
PUBLIC_OG_IMAGE_URL for image-based social sharing if desired.
