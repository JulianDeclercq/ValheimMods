using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace ExploreMap
{
    [BepInProcess("valheim.exe")]
    [BepInPlugin("juliandeclercq.ExploreMap", "Explore Map", "1.0.0.0")]
    public class ExploreMap : BaseUnityPlugin
    {
        private static string _cachedPath = string.Empty;
        private static ConfigEntry<bool> _loadOriginalMap;
        private void Awake()
        {
            _loadOriginalMap = Config.Bind("General", "Load original map", false, "Set to true to revert this mods actions and return to the pre-mod map state.");
            Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), null);
        }

        // Currently doing it in UpdateMap to benefit from dev validity checks at start of the game, once UpdateMap has been reached it is safe to assume everything has been initialised 
        [HarmonyPatch(typeof(Minimap), "UpdateMap")]
        static class UpdateMapPatch
        {

            static void Postfix(Minimap __instance)
            {
                // generate the path for the saved map
                if (string.IsNullOrEmpty(_cachedPath))
                {
                    var world = Traverse.Create(WorldGenerator.instance).Field("m_world").GetValue() as World;
                    string fileName = $"{Game.instance.GetPlayerProfile().GetFilename()}_{world.m_uid}_ExploreMapSave.dat";
                    string currentDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

                    string targetPath = Path.Combine(currentDirectory, @"..\cache\");

                    // create the final path
                    _cachedPath = Path.Combine(targetPath, fileName);
                    Debug.LogError($"cachedPath set to {_cachedPath}");
                }

                // if a saved map exists, the map has been fully explored before
                if (File.Exists(_cachedPath) && !_loadOriginalMap.Value)
                    return;

                // load the original map if specified in config
                if (_loadOriginalMap.Value)
                {
                    if (!File.Exists(_cachedPath))
                    {
                        Debug.LogWarning($"Couldn't load saved map from file {_cachedPath}, file doesn't exist.");
                        return;
                    }
                    var data = File.ReadAllBytes(_cachedPath);
                    Traverse.Create(__instance).Method("SetMapData", data).GetValue(); // getvalue is to ensure the method gets invoked

                    // delete the saved map
                    File.Delete(_cachedPath);
                    return;
                }

                // save the map data, then retrieve it (easiest way to retrieve current map data)
                __instance.SaveMapData();
                byte[] mapData = Game.instance.GetPlayerProfile().GetMapData();

                // write the map to a file
                File.WriteAllBytes(_cachedPath, mapData);
                Debug.Log($"wrote mapdata to {_cachedPath}");

                // explore the whole map
                __instance.ExploreAll();
            }
        }

        [HarmonyPatch(typeof(ZNet), "OnDestroy")]
        static class OnDestroyPatch
        {
            static void Postfix(Minimap __instance)
            {
                _cachedPath = string.Empty;
            }
        }
    }
}
