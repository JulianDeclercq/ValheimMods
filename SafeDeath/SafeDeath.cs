using BepInEx;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace SafeDeath
{
    [BepInProcess("valheim.exe")]
    [BepInPlugin("juliandeclercq.SafeDeath", "Safe Death", "1.1.0")]
    public class SafeDeath : BaseUnityPlugin
    {
        private void Awake()
        {
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
                return false; // stop executing prefixes and skip the original
            }

            static void Postfix(Player __instance, Inventory __state)
            {
                var invent = __instance.GetInventory();
                invent.MoveAll(__state);
            }
        }

        [HarmonyPatch(typeof(Player), "HardDeath")]
        static class HardDeathPatch
        {
            static bool Prefix(ref bool __result)
            {
                __result = false;
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
                ___m_foods = __state;
            }
        }
    }
}
