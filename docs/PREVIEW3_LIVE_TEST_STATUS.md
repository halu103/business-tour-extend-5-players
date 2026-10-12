# Preview.3 development test status

## Historical record — superseded by release0.2.0/V3

Updated12 October2026: the final V3 source changes service inventory to
3 Chance,2 Island and2 Tax, adds Classic beach artwork and missing native
resort-caption bindings. The user requested packaging without another game
restart, so no final V3 live session is claimed. See
[release notes](../RELEASE_NOTES.md) and the separate release validation report.

After the pending status below was written, the9A1F47D7 V2 candidate reached
actual40-cell gameplay with five cards. Stadium/ship/island models and larger
city names were visible, but World Championships text remained small.
The full-face service band and resort binding were built later, not deployed.
All following pending/version/hash details are historical V2 observations,
not metadata for the release ZIP.

Date: 2026-10-11. Development DLL tests, not a published or stable release.
Preview.3 packaging and its isolated installer cycle are pending. Published
preview.2 artifacts remain frozen.

## Recorded automated validation

- Release-mode compilation of the latest development candidate completed
  with 0 warnings and 0 errors. It was deployed while the game was closed;
  gameplay verification of this exact candidate is still pending.
- Layout rules: 30,000 cases. Board geometry: 8,928 cases. Tile projection:
  340 projection cases plus 3,456 semantic-anchor cases.
- Latest board-label suite: 93,312 clipped footprint cases, 162 bounded
  readability projections, 40 deeper-tile checks preserving outer boundaries,
  27 camera-depth isolation cases, 288 foreground-depth cases and 108
  viewport/camera frames. These are mathematical/source checks, not screenshots.
- Payment layout: 1,348 source/sizing cases. Gameplay HUD: 35,422 cases,
  including money-scale compensation and reverse-order restoration.
- Topology checks cover the 40-cell ring, independent suit 90/art lookup,
  preserved native settings, five rent tiers, 20-step trap separation and
  two-lap graph walks. Fifth lobby: 1,254 readiness/color checks.
- The service-art policy suite passes its exact native catalog fixtures.
  The release checklist now has nine suites: layout rules, board geometry,
  tile projection, board labels, payment layout, gameplay HUD, topology,
  fifth lobby and service art. All nine suites passed on the current source;
  the final release runner/installer cycle is still pending.
- Native patch audit: 48 targets / 47 native bodies, zero unreviewed shared
  bodies and one previously reviewed compatible MapSettings constructor alias.

## Live observations before the latest readability/service-art revision

- Tests were explicitly authorized, private and no-bet, with one human and
  four bots. Main-menu artwork remained visible; five gameplay cards and
  moving pawns were observed. Five matching Pause cards appeared and returned
  to gameplay. This does not cover repeated Pause or ordinary-map restoration.
- Actual native 40-cell gameplay was observed. The native preflight and commit
  logs confirmed 40 cells, five resorts and two traps. The route contains
  23 cities, five Chance cells and five other native service cells as well.
  Earlier 32-cell fallback matches are not counted as expansion success.
- All five native resort rent tiers passed preflight: 25,000 / 50,000 /
  100,000 / 200,000 / 400,000. No native economic formula or serialized rent
  setting was changed. Actual five-resort payments through a full match remain
  unverified.
- Trap indices 12 and 32 were logged/observed, separated by 20 route steps.
  Their nearest visual opposition is about 178.4 degrees through the center,
  not exactly 180 degrees. The trap template still displays **CHANCE**.
- Vertical right-hand controls and the fully visible native emote panel were
  observed; the emote panel measured 554 x 359. Mapped building/service anchors
  were loaded in later tests, rather than remaining only a compiled change.
- A distinct coral-orange native player color appeared. The fifth lobby path
  logged ready=true using actual native readiness. A visible true/false check
  transition has not yet been captured.
- Larger center money text was seen. Moving templates are now uncompressed
  and inherit scale compensation targeting 130% of a full-size native pile.
  The user has not yet approved a live animated transfer after this change.
- The rent-foreground DLL made previously covered prices visible, including
  prices on upper/side cells. The user then reported that names were still too
  small and World Championships artwork was missing. These reports are not
  counted as successful overall board-UI validation.

## Latest corrections — deployed, gameplay test pending

- Keep the board's outer boundary while reducing the inner pentagon scale
  from 0.72 to 0.64 for 40-cell gameplay. This makes the cell strip about 29%
  deeper without pushing the outline into player cards or toolbar controls.
- Replace the narrow name band with separate wider name and rent bands.
  Bounded screen-space targets are 20 pixels per rendered line for names and
  22 for prices, with a 2.5x enlargement cap and final polygon clipping.
  Native TMP line counts are read without modifying text; one line is used
  if unavailable. Long captions may still shrink below the target to stay
  within a cell.
- Use foreground sorting and near-camera depth for both native names and
  rent amounts. The placement preserves screen X/Y, avoids near-plane
  clipping and restores owned renderer/transform changes on cleanup. It does
  not edit native localized content, rent values or font materials.
- Use compatible native Classic service art for Lost Island, World
  Championships and World Tour when the regional catalog lacks the exact
  service prefab. Native Start art and other regional cells remain regional.
  This corrects missing models, not service type, movement or economy.

## Purchase, restoration and AFK limits

- Purchase/repurchase/escape dialogs retain their native parents, anchors,
  geometry, scale, buttons and animation. The earlier enlargement/reparenting
  wrapper is removed. Rent/debt text sizing remains separate. A complete
  purchase dialog has not been captured after the correction; one repurchase
  dialog timed out before full inspection. The currently deployed A462CF34
  DLL reproduced a Fantasy resort `ArgumentNullException` (parameter `key`)
  immediately after the native view was created. The cause is not confirmed;
  this is a release blocker, not merely an old report.
- HUD cleanup restores transforms in reverse order, clears destroyed entries
  and checks object generations. All 120 extraction-order permutations pass
  source tests. Normal four-player maps are guarded in source, but live mode
  switching, reused objects, loading-name fitting and repeated Pause still
  need regression tests.
- An earlier draft logged suppression of the local native missed-roll AFK
  callback. No long unattended/background test, server-kick prevention or
  network-disconnect protection is proven.
- Every card/bot strategy, monopoly condition, bankruptcy/endgame consumer,
  five human clients, Steam invites, reconnects and host migration are not
  verified through a complete game.

## Local deployment and package evidence

- The original DLL is backed up at
  `work/diagnostics/2026-10-11-preview3-live-baseline/BusinessTourFiveRealms.dll`.
  Original SHA256:
  `58AFC58D2F4AFBF7881D90BF3FDD17ADC33BFCCCC65D6F7B44C6580574D7911D`.
- Additional DLL/log backups are retained locally in
  `work/diagnostics/2026-10-11-ui-followup-085402/`.
- The first verified native 40-cell DLL was
  `C03E37898ABE63CD86662541A8117319DEF695E6AE142A3B9245AD8F77A2F4A9`.
  Later art-anchor/money and trap/readiness candidates were also live-tested.
- The last recorded deployed rent-foreground DLL is
  `A462CF34E89C3459CA7B2959F592A729F2F09F236DBFCB5E71C0CA65FC77BDD8`.
  It does not include the latest deeper-tile/readability/service-art fixes.
  Record the replacement DLL hash and observations after the next live test.
- The latest readable/service-art DLL was deployed while the game was closed:
  `9A1F47D79F885D805383D42402CC4B4F5CC444FEB93C0D468836294ADE1E5E97`.
  The prior DLL/log were backed up in
  `work/diagnostics/2026-10-11-readable-service-art/`. Steam launch and plugin
  startup succeeded; board and purchase checks remain pending.
- Only backed-up development DLLs were copied into the Steam plugin folder
  while the game was closed. Older installer metadata in that folder does not
  identify the separately copied development DLL. No preview.3 archive/hash
  or completed isolated installer run is claimed here.
- No wager or paid item purchase was made during these tests. The agent did
  not upload logs. The native game later logged its own automatic PlayFab
  log upload; these tests do not disable or control the game's telemetry.
  The first draft SteamAPI
  VersionMismatch did not reproduce on retry; its cause remains unconfirmed.
