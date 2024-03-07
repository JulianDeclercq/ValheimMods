using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using System;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace GlowingMushroomRemover
{
    [BepInProcess("valheim.exe")]
    [BepInPlugin("juliandeclercq.GlowingMushroomRemover", "Glowing Mushroom Remover", "1.0.0")]
    public class GlowingMushroomRemover : BaseUnityPlugin
    {
        private static ConfigEntry<string> _hotkeyEntry;
        private static KeyCode _hotkey;
        private static ConfigEntry<float> _effectRadius;

        private void Awake()
        {
            _hotkeyEntry = Config.Bind("Hotkeys", "Activation Hotkey", "F8", "The hotkey that removes all glowing mushrooms within given radius, for a full list of available keys visit https://docs.unity3d.com/ScriptReference/KeyCode.html");
            _hotkey = (KeyCode)Enum.Parse(typeof(KeyCode), _hotkeyEntry.Value);
            _effectRadius = Config.Bind("General", "Effect Radius", 5f, "The radius within which GlowingMushrooms should be removed.");
            Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), null);
        }

        [HarmonyPatch(typeof(Player), "Update")]
        static class UpdatePatch
        {
            static void Postfix(Player __instance)
            {
                if (!Input.GetKeyDown(_hotkey))
                    return;
                
                Debug.Log("GlowingMushroomRemover hotkey pressed!");

                var deletionCounter = 0;
                var hitColliders = Physics.OverlapSphere(__instance.transform.position, _effectRadius.Value, ~0);
                foreach (var hitCollider in hitColliders.Where(hc => IsGlowingMushroom(hc)))
                {
                    hitCollider.transform.parent.GetComponent<ZNetView>()?.Destroy();
                    deletionCounter++;
                }
                
                Debug.Log($"Destroyed {deletionCounter} GlowingMushrooms");
            }

            private static bool IsGlowingMushroom(Collider collider)
            {
                return collider.gameObject.name.ToLower().Equals("cube") && collider.transform.parent.name.ToLower().StartsWith("glowingmushroom");
            }
        }
    }
}
