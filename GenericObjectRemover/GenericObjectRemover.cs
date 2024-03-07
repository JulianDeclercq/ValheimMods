using BepInEx;
using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using static Terminal;

namespace GenericObjectRemover
{
    [BepInProcess("valheim.exe")]
    [BepInPlugin("juliandeclercq.GenericObjectRemover", "Generic Object Remover", "1.1.4")]
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
            const string remove = "GORremove";
            static void Postfix(Terminal __instance)
            {
                new ConsoleCommand($"{inspect}", $"Check the currently looked at object for removables. \n" +
                    $"{inspect} [radius] - Check for removables in given radius around the player.", (ConsoleEventArgs args) =>
                {
                    switch(args.Length)
                    {
                        case 1:
                            var hovering = HoveringObjectRemovable();
                            ConsolePrint(hovering == null ? "Player is not hovering over a removable object." : $"Player is hovering over {CustomFormat(hovering.gameObject.name)}.");
                            break;
                        case 2:
                            if (int.TryParse(args[1], out int radius))
                                InspectRadius(radius);
                            break;
                        default:
                            ConsolePrint($"Invalid amount of arguments for {inspect}. Expected syntax: |{inspect}| or |{inspect} [radius]|.");
                            break;
                    }
                });

                new ConsoleCommand($"{remove}", "[objectname] - Remove an object. (use GORinspect to retrieve a removable object's name) \n" +
                    $"{remove} [objectname] [radius] - Remove all objects with given name in given radius around the player.", (ConsoleEventArgs args) =>
                {
                    switch (args.Length)
                    {
                        case 2:
                            RemoveObject(args[1].ToLower());
                            break;
                        case 3:
                            if (int.TryParse(args[2], out int radius))
                                RemoveObjectRadius(args[1], radius);
                            break;
                        default:
                            ConsolePrint($"Invalid amount of arguments for {remove}. Expected syntax: |{remove} [objectname]| or |{remove} [objectname] [radius]|.");
                            break;
                    }
                });
            }
        }
       
        private static ZNetView HoveringObjectRemovable()
        {
            var current = Traverse.Create(Player.m_localPlayer).Field("m_hovering").GetValue() as GameObject;
            return current?.GetComponentInParent<ZNetView>();
        }
        private static HashSet<ZNetView> RemovablesInRadius(int radius)
        {
            var inRadius = new HashSet<ZNetView>();

            Collider[] hitColliders = Physics.OverlapSphere(Player.m_localPlayer.transform.position, radius, ~0);
            foreach (var hitCollider in hitColliders)
            {
                var view = hitCollider.GetComponentInParent<ZNetView>();
                if (view != null)
                    inRadius.Add(view);
            }

            return inRadius;
        }
        
        private static void RemoveObject(string objectName)
        {
            var removable = HoveringObjectRemovable();
            if (removable == null)
            {
                ConsolePrint($"Player is not hovering over a removable object.");
                return;
            }

            string lhs = CustomFormat(objectName);
            string rhs = CustomFormat(removable.gameObject.name);
            if (!lhs.Equals(rhs))
            {
                ConsolePrint($"Couldn't remove object. Received |{lhs}| expected |{rhs}|");
                return;
            }

            removable.Destroy();
            ConsolePrint($"Removed: {objectName}");
        }

        private static void InspectRadius(int radius)
        {
            var count = new Dictionary<string, int>();
            var inRadius = RemovablesInRadius(radius);
         
            foreach (var removable in inRadius)
            {
                var key = CustomFormat(removable.gameObject.name);
                count.TryGetValue(key, out int currentCount);
                count[key] = currentCount + 1;
            }

            foreach (var removable in count)
                ConsolePrint($"Found removable: {removable.Value}x {removable.Key} (radius = {radius})");
        }

        private static void RemoveObjectRadius(string objectName, int radius)
        {
            var target = CustomFormat(objectName);
            var removablesInRadius = RemovablesInRadius(radius).Where(x => CustomFormat(x.gameObject.name).Equals(target));
            foreach(var removable in removablesInRadius)
                removable.Destroy();

            ConsolePrint($"Removed {removablesInRadius.Count()}x {target} (radius = {radius})");
        }

        private static string CustomFormat(string input)
        {
            int startingIdx = input.IndexOf('(');
            if (startingIdx == -1)
                return input.ToLower();

            // avoid possible errors with naming inconsistencies
            int endingIdx = input.IndexOf(')');
            if (endingIdx == -1)
                return input.ToLower();

            return input.Substring(0, startingIdx).ToLower();
        }

        private static void ConsolePrint(string line)
        {
            Traverse.Create(Console.instance).Method("Print", new object[] { line }).GetValue();
        }
    }
}
