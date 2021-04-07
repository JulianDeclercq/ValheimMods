using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
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
                if (Input.GetKeyDown(_hotkey))
                {
                    Debug.Log("GlowingMushroomRemover hotkey pressed!");

                    int deletionCounter = 0;
                    Collider[] hitColliders = Physics.OverlapSphere(__instance.transform.position, _effectRadius.Value, ~0);
                    foreach (var hitCollider in hitColliders)
                    {
                        if (hitCollider.gameObject.name.ToLower().Equals("cube"))
                        {
                            if (hitCollider.transform.parent.name.ToLower().StartsWith("glowingmushroom"))
                            {
                                hitCollider.transform.parent.GetComponent<ZNetView>()?.Destroy();
                                ++deletionCounter;
                            }
                        }
                    }
                    Debug.Log($"Destroyed {deletionCounter} GlowingMushrooms");
                }
            }
        }
    }
}
