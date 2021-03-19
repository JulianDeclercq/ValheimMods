using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace ExploreMap
{
    [BepInProcess("valheim.exe")]
    [BepInPlugin("juliandeclercq.ExploreMap", "Explore Map", "1.0.0.1")]
    public class ExploreMap : BaseUnityPlugin
    {
        private static string _cachedPath = string.Empty;
        public static Minimap _miniMap = null;
        private static ConfigEntry<bool> _exploreFullMap;
        private void Awake()
        {
            _exploreFullMap = Config.Bind("General", "Explore full map", true , "explore the full map");
            Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), null);
        }

        private static void LoadOriginalMap()
        {
            if (_miniMap == null || !File.Exists(_cachedPath))
                return;

            _exploreFullMap.Value = false;

            var data = File.ReadAllBytes(_cachedPath);
            Traverse.Create(_miniMap).Method("SetMapData", data).GetValue(); // getvalue is to ensure the method gets invoked

            // delete the saved map
            File.Delete(_cachedPath);
        }

        // Currently doing it in UpdateMap to benefit from dev validity checks at start of the game, once UpdateMap has been reached it is safe to assume everything has been initialised 
        [HarmonyPatch(typeof(Minimap), "UpdateMap")]
        static class UpdateMapPatch
        {
            static void Postfix(Minimap __instance)
            {
                if (_miniMap == null)
                    _miniMap = __instance;

                if (!_exploreFullMap.Value)
                    return;

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
                if (File.Exists(_cachedPath))
                    return;

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

        [HarmonyPatch(typeof(Console), "InputText")]
        static class InputText_Patch
        {
            const string command = "explore map reset";
            static void Prefix(Console __instance)
            {
                string cmd = __instance.m_input.text;

                if (cmd.StartsWith("help"))
                {
                    Traverse.Create(__instance).Method("AddString", new object[] { command }).GetValue();
                    return;
                }

                if (cmd.ToLower().Equals(command.ToLower()))
                {
                    LoadOriginalMap();
                    Traverse.Create(__instance).Method("AddString", new object[] { cmd }).GetValue();
                    Traverse.Create(__instance).Method("AddString", new object[] { "reloaded original map" }).GetValue();
                }
            }
        }
    }
}
