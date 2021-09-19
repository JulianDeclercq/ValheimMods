using BepInEx;
using HarmonyLib;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEngine;
using static Terminal;

namespace GenericObjectRemover
{
    [BepInProcess("valheim.exe")]
    [BepInPlugin("juliandeclercq.GenericObjectRemover", "Generic Object Remover", "1.1.0")]
    public class GenericObjectRemover : BaseUnityPlugin
    {
        private const string _noHoverHierarchy = "nothing";
        private static List<string> _printQueue = new List<string>();
        private void Awake()
        {
            Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), null);
        }

        [HarmonyPatch(typeof(Terminal), "InitTerminal")]
        static class InputText_Patch
        {
            const string inspect = "GORinspect";
            const string inspectRadius = "GORinspectradius";
            const string remove = "GORremove";
            const string removeRadius = "GORremoveradius";
            static void Postfix(Terminal __instance)
            {
                new ConsoleCommand($"{inspect}", "Check the currently looked at object for removables.", (ConsoleEventArgs args) =>
                {
                    args.Context.AddString($"arguments size: {args.Length}");
                    for (int i = 0; i < args.Length; ++i)
                        args.Context.AddString($"Arguments {args[i]}");

                    if (args.Length != 1)
                        return;

                    InspectHoveringObject(out string hierarchy, print: true);
                    args.Context.AddString($"Currently hovering over {hierarchy}");

                    foreach (var printable in _printQueue)
                        args.Context.AddString($"Removable object found: {printable}");

                    _printQueue.Clear();
                });

                new ConsoleCommand($"{remove}", $"{remove} [objectname] - Remove an object. (use GORinspect to retrieve a removable object's name)", (ConsoleEventArgs args) =>
                {
                    if (args.Length < 2)
                        return;

                    RemoveObject(args[1].ToLower());
                });
                
                new ConsoleCommand($"{inspectRadius}", $"{inspectRadius} [radius] - Check for removables in given radius around the player.", (ConsoleEventArgs args) =>
                {
                    Traverse.Create(Console.instance).Method("Print", new object[] { $"PRINT TEST." }).GetValue();

                    if (args.Length != 2)
                        return;

                    if (int.TryParse(args[1], out int radius))
                    {
                        args.Context.AddString($"Calling InspectRadius with radius {radius}");
                        InspectRadius(radius);
                    }
                });

                new ConsoleCommand($"{removeRadius}", $"{removeRadius} [objectname] [radius] - Remove all objects with given name in given radius around the player. (use GORinspect to retrieve a removable object's name)", (ConsoleEventArgs args) =>
                {
                    if (args.Length < 3)
                        return;

                    if (int.TryParse(args[2], out int radius))
                        RemoveObjectRadius(args[1], radius);
                });
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

            var view = current.GetComponent<ZNetView>();
            while (view == null && current.transform.parent != null)
            {
                current = current.transform.parent.gameObject;
                view = current.GetComponent<ZNetView>();
                hierarchy = $"{current.name}/{hierarchy}";
            }

            if (print)
                _printQueue.Add(CustomFormat(view.gameObject.name));

            //Traverse.Create(Console.instance).Method("Print", new object[] { $"InspectHoveringObject hierarchy: {hierarchy}" }).GetValue();
            return view;
        }
        
        private static void RemoveObject(string objectName)
        {
            Traverse.Create(Console.instance).Method("Print", new object[] { $"Called RemoveObject for {objectName}." }).GetValue();
            var removable = InspectHoveringObject(out string hierarchy);

            if (hierarchy.Equals(_noHoverHierarchy))
            {
                Traverse.Create(Console.instance).Method("Print", new object[] { $"Player is not hovering over a removable object." }).GetValue();
                return;
            }

            string received = objectName.ToLower();
            string expected = CustomFormat(removable.gameObject.name);
            if (!received.Equals(expected))
            {
                Traverse.Create(Console.instance).Method("Print", new object[] { $"Couldn't remove object. Received |{received}| expected |{expected}|" }).GetValue();
                return;
            }

            removable.Destroy();
            Traverse.Create(Console.instance).Method("Print", new object[] { $"Removed: {objectName}" }).GetValue();
        }

        private static void InspectRadius(int radius)
        {
            Traverse.Create(Console.instance).Method("Print", new object[] { $"Called InspectRadius with radius {radius}" }).GetValue();
            var inRadius = new Dictionary<string, int>();
            Collider[] hitColliders = Physics.OverlapSphere(Player.m_localPlayer.transform.position, radius, ~0);
            Traverse.Create(Console.instance).Method("Print", new object[] { $"{hitColliders.Length} hitcolliders found" }).GetValue();
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
                Traverse.Create(Console.instance).Method("Print", new object[] { $"Found removable: {removable.Value}x {removable.Key} (radius = {radius})" }).GetValue();
        }

        private static void RemoveObjectRadius(string objectName, int radius)
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
            Traverse.Create(Console.instance).Method("Print", new object[] { $"Removed {deletionCounter}x {objectName} (radius = {radius})" }).GetValue();
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
