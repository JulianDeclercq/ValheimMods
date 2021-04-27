using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;

namespace SafeDeath
{
    [BepInProcess("valheim.exe")]
    [BepInPlugin("juliandeclercq.SafeDeath", "Safe Death", "1.1.0")]
    public class SafeDeath : BaseUnityPlugin
    {
        private static ConfigEntry<bool> _skillLoss;
        private static ConfigEntry<bool> _foodLoss;
        private static ConfigEntry<bool> _itemLoss;
        private void Awake()
        {
            _skillLoss = Config.Bind("General", "Skill loss", false, "Lose skill / skill progression on death");
            _foodLoss = Config.Bind("General", "Food loss", false, "Lose food on death");
            _itemLoss = Config.Bind("General", "Item loss", false, "Lose items on death");
            Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), null);
        }

        [HarmonyPatch(typeof(Player), "CreateTombStone")]
        static class CreateTombStonePatch
        {
            static bool Prefix(Player __instance, out Inventory __state)
            {
                var invent = __instance.GetInventory();
                
                Inventory savedInventory = new Inventory("SavedInventory", null, invent.GetWidth(), invent.GetHeight());
                savedInventory.MoveAll(invent);
                __state = savedInventory;

                if (_itemLoss.Value)
                {
                    return true; // execute the prefixes and the original
                }
                else
                {
                    return false; // stop executing prefixes and skip the original
                }
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
            static void Prefix(List<Player.Food> ___m_foods, out List<Player.Food> __state)
            {
                __state = new List<Player.Food>(___m_foods);
            }

            static void Postfix(ref List<Player.Food> ___m_foods, List<Player.Food> __state)
            {
                if (_foodLoss.Value)
                    return;

                ___m_foods = __state;
            }
        }
    }
}
