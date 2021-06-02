using BepInEx;
using HarmonyLib;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEngine;

namespace GenericObjectRemover
{
    [BepInProcess("valheim.exe")]
    [BepInPlugin("juliandeclercq.GenericObjectRemover", "Generic Object Remover", "1.0.0")]
    public class GenericObjectRemover : BaseUnityPlugin
    {
        private static Dictionary<string, ZNetView> _removables = new Dictionary<string, ZNetView>();

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
            const string inspect = "GORinspect";
            const string inspectRadius = "GORinspectradius";
            const string remove = "GORremove";
            const string removeRadius = "GORremoveradius";
            static void Prefix(Console __instance)
            {
                string cmd = __instance.m_input.text.ToLower();

                if (cmd.StartsWith("help"))
                {
                    Traverse.Create(__instance).Method("AddString", new object[] {$"{inspect} - Check the currently looked at object for removables."}).GetValue();
                    Traverse.Create(__instance).Method("AddString", new object[] {$"{remove} [objectname] - Remove an object. (use GORinspect to retrieve a removable object's name)" }).GetValue();
                    Traverse.Create(__instance).Method("AddString", new object[] {$"{inspectRadius} [radius]- Check for removables in given radius around the player."}).GetValue();
                    Traverse.Create(__instance).Method("AddString", new object[] {$"{removeRadius} [objectname] [radius] - Remove all objects with given name in given radius around the player.  (use GORinspect to retrieve a removable object's name)" }).GetValue();
                    return;
                }

                if (cmd.Equals(inspect.ToLower()))
                {
                    var hovering = Traverse.Create(Player.m_localPlayer).Field("m_hovering").GetValue() as GameObject;
                    if (hovering != null)
                    {
                        Traverse.Create(__instance).Method("AddString", new object[] { $"Currently hovering over {InspectObject(hovering)}" }).GetValue();

                        foreach (var removable in _removables)
                            Traverse.Create(Console.instance).Method("AddString", new object[] { $"Removable object found: {removable.Key}" }).GetValue();
                    }
                    else Traverse.Create(__instance).Method("AddString", new object[] { $"No inspectable item detected" }).GetValue();
                    return;
                }

                var regInspectRadius = new Regex($@"{inspectRadius.ToLower()} (\d+)");
                var match = regInspectRadius.Match(cmd);
                if (match.Success)
                {
                    InspectRadius(float.Parse(match.Groups[1].ToString(), CultureInfo.InvariantCulture.NumberFormat));
                    return;
                }

                var regRemove = new Regex($@"{remove.ToLower()} (\w+)");
                match = regRemove.Match(cmd);
                if (match.Success)
                {
                    RemoveObject(match.Groups[1].ToString().ToLower());
                    return;
                }

                var regRemoveRadius = new Regex($@"{removeRadius.ToLower()} (\w+) (\d+)");
                match = regRemoveRadius.Match(cmd);
                if (match.Success)
                {
                    RemoveObjectRadius(match.Groups[1].ToString().ToLower(), float.Parse(match.Groups[2].ToString(), CultureInfo.InvariantCulture.NumberFormat));
                    return;
                }
            }
        }

        private static string InspectObject(GameObject go)
        {
            // clear the removables from last inspect
            _removables.Clear();

            string fullName = go.name;
            while (go.transform.parent != null)
            {
                var znetView = go.GetComponent<ZNetView>();
                if (znetView != null)
                    _removables[TrimTrailingParentheses(go.name).ToLower()] = znetView;
                
                go = go.transform.parent.gameObject;
                fullName = $"{go.name}/{fullName}";
            }
            return fullName;
        }

        private static void InspectRadius(float radius)
        {
            var inRadius = new Dictionary<string, int>();
            Collider[] hitColliders = Physics.OverlapSphere(Player.m_localPlayer.transform.position, radius, ~0);
            foreach (var hitCollider in hitColliders)
            {
                Transform current = hitCollider.transform;
                while (current.parent != null)
                {
                    if (current.GetComponent<ZNetView>() != null)
                    {
                        string name = TrimTrailingParentheses(current.gameObject.name.ToLower());
                        if (inRadius.ContainsKey(name))
                        {
                            inRadius[name]++;
                        }
                        else
                        {
                            inRadius[name] = 1;
                        }
                    }
                    
                    current = current.parent;
                }
            }

            foreach (var removable in inRadius)
                Traverse.Create(Console.instance).Method("AddString", new object[] { $"Found removable: {removable.Value}x {removable.Key} (radius = {radius})" }).GetValue();
        }

        private static void RemoveObject(string objectName)
        {
            ZNetView removable;
            if (!_removables.TryGetValue(objectName, out removable))
            {
                Traverse.Create(Console.instance).Method("AddString", new object[] { $"No removable found with name: {objectName}" }).GetValue();
                return;
            }

            removable.Destroy();
            _removables.Remove(objectName);
            Traverse.Create(Console.instance).Method("AddString", new object[] { $"Removed: {objectName}" }).GetValue();
        }

        private static void RemoveObjectRadius(string objectName, float radius)
        {
            int deletionCounter = 0;
            Collider[] hitColliders = Physics.OverlapSphere(Player.m_localPlayer.transform.position, radius, ~0);
            foreach (var hitCollider in hitColliders)
            {
                Transform current = hitCollider.transform;
                while (current.parent != null)
                {
                    if (current.gameObject.name.ToLower().StartsWith(objectName))
                    {
                        current.GetComponent<ZNetView>()?.Destroy();
                        ++deletionCounter;
                    }
                    current = current.parent;
                }
            }
            Traverse.Create(Console.instance).Method("AddString", new object[] { $"Removed {deletionCounter}x {objectName} (radius = {radius})" }).GetValue();
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

            return input.Substring(0, startingIdx);
        }
    }
}
