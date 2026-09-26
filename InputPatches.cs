using HarmonyLib;
using Il2Cpp;

namespace CommunityMinimap;

[HarmonyPatch]
internal static class InputPatches
{
    [HarmonyPrefix]
    [HarmonyPatch(typeof(InputManager), nameof(InputManager.GetEscapePressed))]
    private static bool SuppressEscapePressed(ref bool __result)
    {
        return MaybeSuppress(ref __result);
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(InputManager), nameof(InputManager.GetPauseMenuTogglePressed))]
    private static bool SuppressPauseToggle(ref bool __result)
    {
        return MaybeSuppress(ref __result);
    }

    private static bool MaybeSuppress(ref bool result)
    {
        if (!ModEntry.ShouldSuppressGameEscape())
            return true;

        result = false;
        return false;
    }
}
