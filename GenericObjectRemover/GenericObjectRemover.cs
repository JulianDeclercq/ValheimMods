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
        private static string _noHoverHierarchy = "nothing";
        private static List<string> _printQueue = new List<string>();
        private void Awake()
        {
            Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), null);
        }

        [HarmonyPatch(typeof(Console), "InputText")]
        static class InputText_Patch
        {
            const string inspect = "GORinspect";
            const string inspectRadius = "GORinspectradius";
            const string remove = "GORremove";
            const string removeRadius = "GORremoveradius";
            static void Postfix(Console __instance)
            {
                string cmd = __instance.m_input.text.ToLower();

                if (cmd.StartsWith("help"))
                {
                    Traverse.Create(__instance).Method("AddString", new object[] {$"{inspect} - Check the currently looked at object for removables."}).GetValue();
                    Traverse.Create(__instance).Method("AddString", new object[] {$"{remove} [objectname] - Remove an object. (use GORinspect to retrieve a removable object's name)" }).GetValue();
                    Traverse.Create(__instance).Method("AddString", new object[] {$"{inspectRadius} [radius]- Check for removables in given radius around the player."}).GetValue();
                    Traverse.Create(__instance).Method("AddString", new object[] {$"{removeRadius} [objectname] [radius] - Remove all objects with given name in given radius around the player. (use GORinspect to retrieve a removable object's name)" }).GetValue();
                    return;
                }

                if (cmd.Equals(inspect.ToLower()))
                {
                    InspectHoveringObject(out string hierarchy, print : true);
                    Traverse.Create(__instance).Method("AddString", new object[] { $"Currently hovering over {hierarchy}" }).GetValue();
                    
                    foreach (var printable in _printQueue)
                        Traverse.Create(Console.instance).Method("AddString", new object[] { $"Removable object found: {printable}" }).GetValue();

                    _printQueue.Clear();

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
       
        private static ZNetView InspectHoveringObject(out string hierarchy, bool print = false)
        {
            var current = Traverse.Create(Player.m_localPlayer).Field("m_hovering").GetValue() as GameObject;
            if (current == null)
            {
                hierarchy = _noHoverHierarchy;
                return null;
            }

            hierarchy = current.name;
            ZNetView removable = null;
            while (current.transform.parent != null)
            {
                var view = current.GetComponent<ZNetView>();
                if (view != null)
                {
                    if (removable != null)
                        Traverse.Create(Console.instance).Method("AddString", new object[] { $"More than one removable found in an object that was looked at, please report this on the mod page." }).GetValue();
                    
                    removable = view;

                    if (print)
                        _printQueue.Add(CustomFormat(removable.gameObject.name));
                }
                current = current.transform.parent.gameObject;
                hierarchy = $"{current.name}/{hierarchy}";
            }

            return removable;
        }
        
        private static void RemoveObject(string objectName)
        {
            var removable = InspectHoveringObject(out string hierarchy);
            if (hierarchy.Equals(_noHoverHierarchy) || !objectName.ToLower().Equals(CustomFormat(removable.gameObject.name)))
            {
                Traverse.Create(Console.instance).Method("AddString", new object[] { $"Couldn't find removable object {objectName} where player is looking." }).GetValue();
                return;
            }

            removable.Destroy();
            Traverse.Create(Console.instance).Method("AddString", new object[] { $"Removed: {objectName}" }).GetValue();
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
                        string name = CustomFormat(current.gameObject.name);
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

        private static string CustomFormat(string input)
        {
            int startingIdx = input.IndexOf('(');
            if (startingIdx == -1)
                return input;

            // avoid possible errors with naming inconsistencies
            int endingIdx = input.IndexOf(')');
            if (endingIdx == -1)
                return input;

            return input.Substring(0, startingIdx).ToLower();
        }
    }
}
