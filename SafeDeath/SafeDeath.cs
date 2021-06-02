//#define QUICKSLOTS

using BepInEx;
using BepInEx.Configuration;
using EquipmentAndQuickSlots;
using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace SafeDeath
{
    [BepInProcess("valheim.exe")]
    [BepInPlugin("juliandeclercq.SafeDeath", "Safe Death", "1.2.1")]
    public class SafeDeath : BaseUnityPlugin
    {
        private static ConfigEntry<bool> _skillLoss;
        private static ConfigEntry<bool> _foodLoss;
        private static ConfigEntry<bool> _itemLoss;

#if QUICKSLOTS
        private static Inventory _quickslotInventoryOnDeath = null;
#endif
        private void Awake()
        {
            _skillLoss = Config.Bind("General", "Skill loss", false, "Lose skill / skill progression on death");
            _foodLoss = Config.Bind("General", "Food loss", false, "Lose food on death");
            _itemLoss = Config.Bind("General", "Item loss", false, "Lose items on death");
            Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), null);
        }

        private void Update()
        {
            //if (Input.GetKeyDown(KeyCode.F10))
            //    LogInventory(Player.m_localPlayer.GetQuickSlotInventory(), "quickslots");
        }

        private static void LogInventory(Inventory inventory, string name)
        {
            Debug.Log($"{name} inventory item count: {inventory.GetAllItems().Count}");

            foreach (var quickslot in inventory.GetAllItems())
                Debug.Log($"{name} item: {quickslot.m_shared.m_name}");
        }

        [HarmonyPatch(typeof(Player), "CreateTombStone")]
        static class CreateTombStonePatch
        {
            static bool Prefix(Player __instance, out Inventory __state)
            {
                if (_itemLoss.Value)
                {
                    __state = null;
                    return true; // execute the prefixes and the original
                }

                var invent = __instance.GetInventory();
                Inventory savedInventory = new Inventory("SavedInventory", null, invent.GetWidth(), invent.GetHeight());
                savedInventory.MoveAll(invent);
                __state = savedInventory;

                return false; // stop executing prefixes and skip the original, this might cause compatibility problems with other mods (depending on which .dll gets loaded first) but is needed to skip gravestones from the original game. Might need manual involvement.
            }

            static void Postfix(Player __instance, Inventory __state)
            {
                if (_itemLoss.Value)
                    return;

                var invent = __instance.GetInventory();
                invent.MoveAll(__state);
            }
        }

        // HardDeath means skill loss
        [HarmonyPatch(typeof(Player), "HardDeath")]
        static class HardDeathPatch
        {
            static bool Prefix(ref bool __result)
            {
                if (_skillLoss.Value)
                    return true; // let the original run after the prefix

                __result = false; // disable hard death and therefore skill loss
                return false; // stop executing prefixes and skip the original
            }
        }

        [HarmonyPatch(typeof(Player), "OnDeath")]
        static class OnDeathPatch
        {
            static void Prefix(Player __instance, List<Player.Food> ___m_foods, out List<Player.Food> __state)
            {
#if QUICKSLOTS
                var quickslots = __instance.GetQuickSlotInventory();
                Inventory savedQuickslots = new Inventory("SavedQuickslots", null, quickslots.GetWidth(), quickslots.GetHeight());
                savedQuickslots.MoveAll(quickslots); // "Item is not in this Inventory" message. current idea is that it is probably in the player inventory itself but the reference of the data is shared in the quickslotinventory
                _quickslotInventoryOnDeath = savedQuickslots;
#endif
                __state = new List<Player.Food>(___m_foods);
            }

            static void Postfix(Player __instance, ref List<Player.Food> ___m_foods, List<Player.Food> __state)
            {
                if (!_foodLoss.Value)
                    ___m_foods = __state;
            }
        }

#if QUICKSLOTS
        // Warning: ugly hack because it wasn't working
        [HarmonyPatch(typeof(Game), "SpawnPlayer")]
        static class SpawnPlayerPatch
        {
            static void Postfix()
            {
                if (!_itemLoss.Value && _quickslotInventoryOnDeath != null)
                {
                    Player.m_localPlayer.GetQuickSlotInventory().MoveAll(_quickslotInventoryOnDeath);
                    Player.m_localPlayer.Extended().Save();
                    //_quickslotInventoryOnDeath = null;
                }
            }
        }
#endif
    }
}
