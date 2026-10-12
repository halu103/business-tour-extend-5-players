using System;

namespace BusinessTourFiveRealms;

// Pure artwork policy for the inspected Business Tour 2.21 skin catalogs.
// Native cell kinds, suits, links and service settings are not changed here.
internal static class FiveRealmsServiceArtRules
{
    internal const int ClassicSkin = 0;
    internal const int FantasySkin = 1;
    internal const int WonderlandSkin = 2;

    internal static int ResolveSkin(TopologyCellKind kind, int regionSkin)
    {
        if (regionSkin < ClassicSkin || regionSkin > WonderlandSkin)
            throw new ArgumentOutOfRangeException(nameof(regionSkin));

        switch (kind)
        {
            // Classic has the actual palm island, World Championships and
            // World Tour prefabs. Fantasy/Wonderland have no Island3 or
            // TravelNextTurn6 override, and Wonderland has no Tournament5.
            // Keep these original Classic service identities even when a
            // region uses another skin; Travel9/14 are different services.
            case TopologyCellKind.Island:
            case TopologyCellKind.Tournament:
            case TopologyCellKind.TravelNextTurn:
            // Wonderland's decoration10..13 entries are AliceRoom type15,
            // not Resort2; Fantasy substitutes obelisks. Keep the real beach
            // artwork for all five native Resort2 cells, without touching
            // their suit, decoration, ownership or rent calculation.
            case TopologyCellKind.Resort:
                return ClassicSkin;
            case TopologyCellKind.RogueTrap:
                return FantasySkin;
            // All three catalogs have exact overrides/art for these kinds,
            // including Start1. Their region themes remain available.
            case TopologyCellKind.Start:
            case TopologyCellKind.City:
            case TopologyCellKind.Chance:
            case TopologyCellKind.Tax:
                return regionSkin;
            default:
                throw new ArgumentOutOfRangeException(nameof(kind));
        }
    }
}
