using BepInEx;
using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace GenericObjectDestroyer
{
    [BepInProcess("valheim.exe")]
    [BepInPlugin("juliandeclercq.GenericObjectDestroyer", "Generic Object Destroyer", "1.0.0")]
    public class GenericObjectDestroyer : BaseUnityPlugin
    {
        private static Dictionary<string, ZNetView> _destroyables = new Dictionary<string, ZNetView>();

        private void Awake()
        {
            Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), null);
        }

        private void Update()
        {
            return;
            var hovering = Traverse.Create(Player.m_localPlayer).Field("m_hovering").GetValue() as GameObject;
            if (hovering != null)
                Debug.Log($"Currently hovering over {InspectObject(hovering)}");
            else
                Debug.Log("No hovering object");
        }

        [HarmonyPatch(typeof(Console), "InputText")]
        static class InputText_Patch
        {
            const string inspect = "destroyerinspect";
            const string destroy = "destroyerdestroy";
            static void Prefix(Console __instance)
            {
                string cmd = __instance.m_input.text;

                if (cmd.ToLower().StartsWith("help"))
                {
                    Traverse.Create(__instance).Method("AddString", new object[] { inspect }).GetValue();
                    return;
                }

                if (cmd.ToLower().Equals(inspect.ToLower()))
                {
                    var hovering = Traverse.Create(Player.m_localPlayer).Field("m_hovering").GetValue() as GameObject;
                    if (hovering != null)
                    {
                        Traverse.Create(__instance).Method("AddString", new object[] { $"Currently hovering over {InspectObject(hovering)}" }).GetValue();

                        foreach (var destroyable in _destroyables)
                            Traverse.Create(Console.instance).Method("AddString", new object[] { $"Destroyable object found: {destroyable.Key}" }).GetValue();
                    }
                    else Traverse.Create(__instance).Method("AddString", new object[] { $"No inspectable item detected" }).GetValue();
                }
                else if (cmd.ToLower().StartsWith(destroy.ToLower()))
                {
                    DestroyObject(cmd.Substring(cmd.IndexOf(' ') + 1).ToLower());
                }
                // TODO: Radius destroy
            }
        }

        private static string InspectObject(GameObject go)
        {
            // clear the destroyables from last inspect
            _destroyables.Clear();

            string fullName = go.name;
            while (go.transform.parent != null)
            {
                var znetView = go.GetComponent<ZNetView>();
                if (znetView != null)
                    _destroyables[TrimTrailingParentheses(go.name).ToLower()] = znetView;
                
                go = go.transform.parent.gameObject;
                fullName = $"{go.name}/{fullName}";
            }
            return fullName;
        }

        private static void DestroyObject(string objectName)
        {
            ZNetView destroyable;
            if (!_destroyables.TryGetValue(objectName, out destroyable))
            {
                Traverse.Create(Console.instance).Method("AddString", new object[] { $"No destroyable found with name: {objectName}" }).GetValue();
                return;
            }

            destroyable.Destroy();
            _destroyables.Remove(objectName);
            Traverse.Create(Console.instance).Method("AddString", new object[] { $"Destroyed: {objectName}" }).GetValue();
        }

        private static string TrimTrailingParentheses(string input)
        {
            int startingIdx = input.IndexOf('(');
            if (startingIdx == -1)
                return input;

            // avoid possible errors with naming inconsistencies
            int endingIdx = input.IndexOf(')');
            if (endingIdx == -1)
                return input;

            var output = input.Substring(0, startingIdx);
            Debug.Log($"Trimmed {input} into {output}");
            return output;
        }
    }
}
