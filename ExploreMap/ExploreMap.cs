using BepInEx;
using HarmonyLib;
using System.Reflection;

namespace ExploreMap
{
    [BepInProcess("valheim.exe")]
    [BepInPlugin("juliandeclercq.ExploreMap", "Explore Map", "1.0.0.0")]
    public class ExploreMap : BaseUnityPlugin
    {
        private void Awake()
        {
            Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), null);
        }

        [HarmonyPatch(typeof(Minimap), "UpdateMap")]
        static class UpdateMapPatch
        {
            private static bool _explored = false;
            static void Postfix(Minimap __instance)
            {
                if (_explored)
                    return;
                
                __instance.ExploreAll();
                _explored = true;
                UnityEngine.Debug.Log("Explored whole minimap!"); // minimap <-> map??
            }
        }
    }
}
