using BepInEx;
using HarmonyLib;
using System.Reflection;

namespace NoRoofWorkbench
{
    [BepInProcess("valheim.exe")]
    [BepInPlugin("juliandeclercq.NoRoofWorkbench", "No Roof Workbench", "1.0.0")]
    public class NoRoofWorkbench : BaseUnityPlugin
    {
        private void Awake()
        {
            Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), null);
        }

        [HarmonyPatch(typeof(CraftingStation), "CheckUsable")]
        static class CheckUsablePatch
        {
            static bool Prefix(ref bool __result)
            {
                __result = true;
                return false; // stop executing prefixes and skip the original
            }
        }
    }
}
