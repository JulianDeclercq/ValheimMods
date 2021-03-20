using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using System;
using System.Collections;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace ExploreMap
{
    [BepInProcess("valheim.exe")]
    [BepInPlugin("juliandeclercq.ExploreMap", "Explore Map", "1.0.2.0")]
    public class ExploreMap : BaseUnityPlugin
    {
        private static BaseUnityPlugin instance;
        private static string _cachedPath = string.Empty;
        public static Minimap _miniMap = null;
        private static ConfigEntry<bool> _exploreFullMap;
        private static ConfigEntry<string> _toggleHotkey;
        private static KeyCode _toggleHotkeyKeyCode;
        private void Awake()
        {
            instance = this;
            _exploreFullMap = Config.Bind("General", "Explore full map", true , "explore the full map");
            _toggleHotkey = Config.Bind("Hotkeys", "Toggle explore full map hotkey", "F6", "The hotkey that toggles showing the full map, for a full list of available keys visit https://docs.unity3d.com/ScriptReference/KeyCode.html");
            _toggleHotkeyKeyCode = (KeyCode)Enum.Parse(typeof(KeyCode), _toggleHotkey.Value);
            Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), null);
        }

        private void Update()
        {
            if (Input.GetKeyDown(_toggleHotkeyKeyCode))
            {
                // ignore input in certain cases
                if (ZNetScene.instance == null || Player.m_localPlayer == null || Console.IsVisible() || TextInput.IsVisible() || ZNet.instance.InPasswordDialog() || Chat.instance?.HasFocus() == true)
                    return;

                ExploreMapToggle();
            }
        }

        private static void LoadOriginalMap()
        {
            instance.StartCoroutine(LoadOriginalMapCoroutine());
        }

        private static IEnumerator LoadOriginalMapCoroutine()
        {
            if (_miniMap == null || !File.Exists(_cachedPath))
                yield break;

            _exploreFullMap.Value = false;

            var data = File.ReadAllBytes(_cachedPath);

            // hide the map to prevent a nasty icon bug
            FieldInfo modeField = _miniMap.GetType().GetField("m_mode", BindingFlags.NonPublic | BindingFlags.Instance);
            object mapModeNone = _miniMap.GetType().GetNestedType("MapMode", BindingFlags.NonPublic).GetField("None").GetValue(_miniMap);

            // recreate Minimap::SetMapMode() functionality with parameter MapMode.None
            modeField.SetValue(_miniMap, mapModeNone);
            _miniMap.m_largeRoot.SetActive(false);
            _miniMap.m_smallRoot.SetActive(false);

            yield return new WaitForEndOfFrame();

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
            const string command = "toggle full map";
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
                    ExploreMapToggle();
                    Traverse.Create(__instance).Method("AddString", new object[] { "Explore map toggled" }).GetValue();
                }
            }
        }

        private static void ExploreMapToggle()
        {
            _exploreFullMap.Value = !_exploreFullMap.Value;

            if (!_exploreFullMap.Value)
                LoadOriginalMap();

            Debug.Log("Explore map toggled");
        }
    }
}
