using System;
using System.Collections.Generic;
using System.Text;
using HarmonyLib;
using UnityEngine;

namespace ChoiceEnhanced.Patches
{
    [HarmonyPatch(typeof(GUIManager))]
    internal static class GUIManagerPatch
    {
        [HarmonyPostfix]
        [HarmonyPatch(nameof(GUIManager.LateUpdate))]
        private static void NothinJustTest_Postfix()
        {
            Plugin.Log.LogInfo("Testing that the mod loaded correctly. <MAGIC STRING> 12345 </MAGIC STRING>");
        }
    }
}

