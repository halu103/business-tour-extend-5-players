using System;
using System.Collections.Generic;

namespace BusinessTourFiveRealms;

// Pure policy for the existing build's six native PlayerColor enum values.
// No enum extension or global asset recolor is needed to add the fifth hue.
internal static class FifthLobbyPresentationRules
{
    internal const int CoralColor = 4; // PlayerColor.Red2, native light #E44D37

    private static readonly int[] NativePreference = { CoralColor, 3, 2, 5, 0, 1 };

    internal static int SelectAdditionalColor(IReadOnlyList<int> existingColors)
    {
        if (existingColors == null || existingColors.Count < 1 || existingColors.Count > 4)
            throw new ArgumentException("Expected one to four native player colors.", nameof(existingColors));
        for (int index = 0; index < existingColors.Count; index++)
        {
            int color = existingColors[index];
            if (color < 0 || color > 5)
                throw new ArgumentException("Unexpected native player color.", nameof(existingColors));
            for (int previous = 0; previous < index; previous++)
                if (color == existingColors[previous])
                    throw new ArgumentException("The native palette contains duplicate player colors.", nameof(existingColors));
        }
        foreach (int candidate in NativePreference)
        {
            bool present = false;
            for (int index = 0; index < existingColors.Count; index++)
                present |= existingColors[index] == candidate;
            if (!present) return candidate;
        }
        throw new InvalidOperationException("No unused native player color remains.");
    }

    internal static bool ShowReady(bool hasOccupant, bool actualReady) => hasOccupant && actualReady;
}
