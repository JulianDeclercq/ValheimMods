using BepInEx;
using HarmonyLib;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
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
                    if (args.Length != 1)
                        return;

                    InspectHoveringObject(print: true);
                });

                new ConsoleCommand($"{remove}", $"{remove} [objectname] - Remove an object. (use GORinspect to retrieve a removable object's name)", (ConsoleEventArgs args) =>
                {
                    if (args.Length < 2)
                        return;

                    RemoveObject(args[1].ToLower());
                });
                
                new ConsoleCommand($"{inspectRadius}", $"{inspectRadius} [radius] - Check for removables in given radius around the player.", (ConsoleEventArgs args) =>
                {
                    if (args.Length != 2)
                        return;

                    if (int.TryParse(args[1], out int radius))
                        InspectRadius(radius);
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
       
        private static ZNetView InspectHoveringObject(bool print = false)
        {
            var current = Traverse.Create(Player.m_localPlayer).Field("m_hovering").GetValue() as GameObject;
            var view = current?.GetComponentInParent<ZNetView>();

            if (print)
            {
                if (view != null)
                {
                    Traverse.Create(Console.instance).Method("Print", new object[] { $"Player is hovering over {CustomFormat(view.gameObject.name)}." }).GetValue();
                }
                else Traverse.Create(Console.instance).Method("Print", new object[] { $"Player is not hovering over a removable object." }).GetValue();
            }

            return view;
        }
        
        private static void RemoveObject(string objectName)
        {
            var removable = InspectHoveringObject();
            if (removable == null)
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
            var inRadius = new HashSet<ZNetView>();
            var count = new Dictionary<string, int>();
         
            Collider[] hitColliders = Physics.OverlapSphere(Player.m_localPlayer.transform.position, radius, ~0);
            foreach (var hitCollider in hitColliders)
            {
                var view = hitCollider.GetComponentInParent<ZNetView>();
                if (view == null)
                    continue;

                // prevent adding duplicates if hitcolliders were nested
                if (inRadius.Add(view))
                {
                    var key = CustomFormat(view.gameObject.name);
                    count.TryGetValue(key, out int currentCount);
                    count[key] = currentCount + 1;
                }
            }

            foreach (var removable in count)
                Traverse.Create(Console.instance).Method("Print", new object[] { $"Found removable: {removable.Value}x {removable.Key} (radius = {radius})" }).GetValue();
        }

        private static void RemoveObjectRadius(string objectName, int radius)
        {
            return;
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
