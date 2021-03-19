using BepInEx;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace SafeDeath
{
    [BepInProcess("valheim.exe")]
    [BepInPlugin("juliandeclercq.SafeDeath", "Safe Death", "1.0.0")]
    public class SafeDeath : BaseUnityPlugin
    {
        private void Awake()
        {
            Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), null);
        }

        [HarmonyPatch(typeof(Player), "CreateTombStone")]
        static class CreateTombStonePatch
        {
            static bool Prefix()
            {
                return false; // stop executing prefixes and skip the original
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
    }
}
