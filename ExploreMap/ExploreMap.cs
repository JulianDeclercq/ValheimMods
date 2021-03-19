using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using System;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization.Formatters.Binary;
using UnityEngine;

namespace ExploreMap
{
    [BepInProcess("valheim.exe")]
    [BepInPlugin("juliandeclercq.ExploreMap", "Explore Map", "1.0.0.1")]
    public class ExploreMap : BaseUnityPlugin
    {
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
            private static bool once = false;
            private static string cachedPath = string.Empty;

            static void Postfix(Minimap __instance)
            {
                // avoid actually running this every update
                if (once)
                    return;

                once = true;

                // generate the path for the saved map
                if (string.IsNullOrEmpty(cachedPath))
                {
                    string fileName = "originallyExplored.dat";
                    string currentDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                    string targetPath = Path.Combine(currentDirectory, @"..\cache\");
                    cachedPath =  Path.Combine(targetPath, fileName);
                }

                // load the original map if specified in config
                if (_loadOriginalMap.Value)
                {
                    if (!File.Exists(cachedPath))
                    {
                        Debug.LogWarning($"Couldn't load saved map from file {cachedPath}, file doesn't exist.");
                        return;
                    }
                    var data = File.ReadAllBytes(cachedPath);
                    Traverse.Create(__instance).Method("SetMapData", data).GetValue(); // getvalue is to ensure the method gets invoked afaik

                    // delete the saved map
                    File.Delete(cachedPath);
                    return;
                }

                // if a saved map exists, the map has been fully explored before
                if (File.Exists(cachedPath))
                    return;

                // save the map data, then retrieve it (easiest way to retrieve current map data)
                __instance.SaveMapData();
                byte[] mapData = Game.instance.GetPlayerProfile().GetMapData();

                // write the map to a file
                File.WriteAllBytes(cachedPath, mapData);
                UnityEngine.Debug.Log($"wrote mapdata to {cachedPath}");

                // explore the whole map
                __instance.ExploreAll();
            }
        }
    }

    
}
