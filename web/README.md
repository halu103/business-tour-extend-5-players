# Five Realms website

English landing page and installation guide for the unofficial Business Tour
Five Realms mod. Built with SvelteKit, Svelte 5, Tailwind 4 and Paraglide.
The muted sky-blue background, blue-gray panels, lime, gold, pink and coral
accents follow the game without the earlier near-white background glare.
The hero board is a code-native illustration, not a gameplay screenshot.

## Local development

Requirements: Node >=20.19 and pnpm (the project declares pnpm 10.23.0).
Dependencies are locked; use `pnpm install --frozen-lockfile` if missing.
The source-based regression command below requires Node 22.18 or newer.

```sh
pnpm dev --host 127.0.0.1
pnpm check
pnpm build
node --experimental-strip-types --test tests/releases.test.mjs
```

Vercel adapter configuration is retained. No deployment or source push is
performed as part of the local UI redesign.

## Configuration

See `.env.example`. Existing `.env` credentials are not edited by the redesign.
The source defaults to https://github.com/halu103/business-tour-extend-5-players.
The old /halu103/business-tour slug is normalized to that verified renamed repository. Stale copied
Far Far West repository/API links are not used. `PRIVATE_GITHUB_API_URL` from
the old project is intentionally ignored; API URLs are derived from the
validated source repository so its token cannot be forwarded to an arbitrary
host.

GitHub assets must have matching Five Realms filenames and repository URLs.
Stable downloadable releases sort before previews. Network failures render
an honest empty state linking to the repository—not guessed download links.
Package versions are read from the actual attached ZIP filename, including
releases whose tag is simply `Release`. Tags are never used as game-build numbers.

Set `PUBLIC_SITE_URL` to the real HTTPS homepage to enable canonical and
Open Graph URL tags. Set `PUBLIC_OG_IMAGE_URL` for a social image. They are
omitted when absent; no production domain or image is invented. The page
includes an English description, Open Graph/Twitter copy and SoftwareApplication
JSON-LD without fabricated ratings or install statistics. These metadata
improvements do not guarantee search ranking.

## Languages

The language dropdown uses the active locales in `project.inlang/settings.json`:
`en`, `vi`, `zh`, `kr`, `jp`, and `de`. Selection navigates to the localized
URL while preserving the current section hash and query. `messages/en.json`
is the base key set; missing translated keys fall back to English. Existing
translated values are used as-is, not replaced by English. Translation files
are user-maintained and are never rewritten by the UI changes.

Update each file's full key set, including the new hero, feature and inventory
keys. Some copied translations still contain the old project's instructions;
finish these before publishing. `jp`/`kr` filenames and URL keys are retained,
but HTML language tags, number/date formatting and metadata correctly use
`ja`/`ko`. Canonical/hreflang URLs are emitted only with a configured domain.

## Product claims

Copy follows the current 0.2.2 package and preserves its limits: the 40-cell
map/fifth resort are implemented, one-human/four-bot development sessions have
been observed, and five-human networking/complete matches are not certified.
The final 0.2.1 UI fixes still need live verification. AFK options do not prevent
all server/network disconnects. Every participant needs the exact same package;
this is not a host-only mod. Begin with private, no-bet bot rooms.
