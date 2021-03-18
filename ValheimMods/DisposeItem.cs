using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using System.Reflection;
using UnityEngine;

namespace ValheimMods
{
    [BepInProcess("valheim.exe")]
    [BepInPlugin("juliandeclercq.DisposeItem", "Dispose Item", "0.0.1.0")]
    public class DisposeItem : BaseUnityPlugin
    {
        private ConfigEntry<string> greeting;

        private void Awake()
        {
            greeting = Config.Bind("General", "GreetingText", "Julians greeting", "a test to see if config works");
            UnityEngine.Debug.Log("Hello, from Julian's new Valheim mod world!");
            UnityEngine.Debug.Log(greeting.Value);
            Logger.LogMessage("Harmony patching :)");

            Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), null);
        }

        [HarmonyPatch(typeof(InventoryGui), "UpdateItemDrag")]
        static class UpdateItemDrag_Patch
        {
            static void Postfix(InventoryGui __instance, ItemDrop.ItemData ___m_dragItem, Inventory ___m_dragInventory, int ___m_dragAmount, ref GameObject ___m_dragGo)
            {
                if (___m_dragItem != null && ___m_dragInventory.ContainsItem(___m_dragItem))
                {
                    Debug.Log($"Dragging {___m_dragAmount}/{___m_dragItem.m_stack} {___m_dragItem.m_dropPrefab.name}");

                    MessageHud.instance.ShowMessage(MessageHud.MessageType.Center, "My name a Jeff.");
                }
            }
        }

        [HarmonyPatch(typeof(Player), "IsEncumbered")]
        static class NeverEncumbered
        {
            static void Postfix(ref bool __result)
            {
                __result = false;
            }
        }
    }
}
