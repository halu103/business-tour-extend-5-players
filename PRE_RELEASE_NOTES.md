# Five Realms 0.2.0-preview.2

**Experimental snapshot — 11 October 2026.**

This preview adds payment sizing, camera-aligned tile names and native trap identity correction to the pentagonal-board/five-card UI snapshot. These new UI/data changes have compiled but have not been tested live. It is not a finished five-player conversion. Do not mix this preview with preview.1: trap suit metadata affects gameplay.

## Changes since preview.1

- Purchase/repurchase/escape dialogs enlarged up to 15%, with a viewport-fit cap and native animation/button ownership preserved.
- Native rent/debt captions and money amounts may grow up to 15% with autosizing; no sale-price markers or full-screen selection UI are scaled.
- Tile names and rent labels are camera-aligned and fitted into separate non-overlapping inner/outer bands. Native localized text remains unchanged.
- Trap conversion now uses the native type transition, assigning Service/NoBuilding rather than retaining city suit/decorations.
- README is in English. The archive/installer/manifests/DLL informational version all identify this exact preview.

## Known issues

- **AFK:** a live private bot match still automatically exited after three missed rolls despite the protection setting being enabled. Do not rely on this preview to remain in a room unattended.
- **Purchase dialog:** an `ArgumentNullException` was logged when a native Fantasy resort-purchase dialog opened. The cause is not yet confirmed; do not assume purchases are fully reliable.
- **Board labels:** the fitting change has only been compiled/mathematically tested. Native building decorations are not resized; their art can still overlap. Very long names may become too small when constrained to one tile.
- **Payment UI:** the enlargement is unverified live. Tight text frames may retain native size to prevent overflow; sale markers, icons and action-button layouts remain native.
- **Loading screen:** portrait-aware name fitting is included, but this last correction has only been compiled, not visually retested.
- **Rules:** the native 32-cell route remains. The agreed 40-cell, five-side expansion and fifth resort/beach are not yet enabled; additional suit/asset and rule-consumer work is required.
- **Regression/networking:** normal four-player maps after special-map play, repeated Pause reopening, five human clients, reconnect/host migration and complete endgames still require live tests.

## Safe initial test

Use one human and four bots in a private, no-bet room. Check five lobby slots, five loading cards, five gameplay cards, the pentagon, purchases/payments and Pause. Stay active during your turns. Stop if a dialog is blank or money/turn state looks incorrect; retain the local log for diagnosis.

The original four-player maps must retain their normal rules and UI. Report deviations separately from Five Realms. Do not use ranked, tournament or paid rooms for testing.
