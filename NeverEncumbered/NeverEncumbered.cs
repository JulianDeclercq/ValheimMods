using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

namespace ValheimMods
{
    [BepInProcess("valheim.exe")]
    [BepInPlugin("juliandeclercq.NeverEncumbered", "Never Encumbered", "1.0.2")]
    public class NeverEncumbered : BaseUnityPlugin
    {
        private static ConfigEntry<bool> _neverEncumbered;
        private static ConfigEntry<bool> _autoPickupPastWeightlimit;
        private static ConfigEntry<bool> _encumberedUIFlicker;
        private void Awake()
        {
            _neverEncumbered = Config.Bind("General", "Never encumbered", true, "Never become encumbered");
            _autoPickupPastWeightlimit = Config.Bind("General", "Auto pickup past weight limit", true, "Autopickup even if the weight limit has been passed");
            _encumberedUIFlicker = Config.Bind("General", "Inventory weight UI flicker", false, "Show the red UI flicker when over the weightlimit like the original");
            Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), null);
        }

        [HarmonyPatch(typeof(Player), "IsEncumbered")]
        static class IsEncumberedPatch
        {
            static void Postfix(ref bool __result)
            {
                if (_neverEncumbered.Value)
                    __result = false;
            }
        }

        [HarmonyPatch(typeof(Player), "AutoPickup")]
        static class AutoPickupPatch
        {
            static void Postfix(float dt, Player __instance, Inventory ___m_inventory, float ___m_autoPickupRange, int ___m_autoPickupMask)
            {
                if (!_autoPickupPastWeightlimit.Value)
                    return;

                if (__instance.IsTeleporting())
                {
                    return;
                }
                Vector3 vector = __instance.transform.position + Vector3.up;
                Collider[] array = Physics.OverlapSphere(vector, ___m_autoPickupRange, ___m_autoPickupMask);
                foreach (Collider val in array)
                {
                    var rb = val.attachedRigidbody;
                    if (rb == null)
                    {
                        continue;
                    }
                    
                    ItemDrop component = rb.GetComponent<ItemDrop>();
                    if (component == null || !component.m_autoPickup || __instance.HaveUniqueKey(component.m_itemData.m_shared.m_name) || !component.GetComponent<ZNetView>().IsValid())
                    {
                        continue;
                    }

                    // If the original AutoPickup picked this item up, ignore it in the postfix
                    // shouldn't happen as the overlapsphere shouldn't find it anymore but i'm not sure if the timing is tight enough for that to be consistent and reliable)
                    if (___m_inventory.ContainsItem(component.m_itemData))
                    {
                        Debug.LogWarning("Component was already in inventory");
                        continue;
                    }

                    if (!component.CanPickup())
                    {
                        component.RequestOwn();
                        continue;
                    }
                    
                    // Leave out the overencumbered check
                    if (!___m_inventory.CanAddItem(component.m_itemData)) // || component.m_itemData.GetWeight() + ___m_inventory.GetTotalWeight() > GetMaxCarryWeight())
                    {
                        continue;
                    }

                    float num = Vector3.Distance(component.transform.position, vector);
                    if (!(num > ___m_autoPickupRange))
                    {
                        if (num < 0.3f)
                        {
                            __instance.Pickup(component.gameObject);
                            continue;
                        }
                        Vector3 vector2 = Vector3.Normalize(vector - component.transform.position);
                        float num2 = 15f;
                        component.transform.position = component.transform.position + vector2 * num2 * dt;
                    }
                }
            }
        }

        [HarmonyPatch(typeof(Console), "InputText")]
        static class InputText_Patch
        {
            const string commandEncumber = "toggle encumber";
            const string commandAutopickup = "toggle autopickuppastlimit";
            static void Prefix(Console __instance)
            {
                string cmd = __instance.m_input.text;

                if (cmd.StartsWith("help"))
                {
                    Traverse.Create(__instance).Method("AddString", new object[] { commandEncumber }).GetValue();
                    Traverse.Create(__instance).Method("AddString", new object[] { commandAutopickup }).GetValue();
                    return;
                }

                if (cmd.ToLower().Equals(commandEncumber.ToLower()))
                {
                    _neverEncumbered.Value = !_neverEncumbered.Value;

                    Traverse.Create(__instance).Method("AddString", new object[] { $"toggled encumber ({!_neverEncumbered.Value})" }).GetValue(); // flip for "never" keyword
                }
                else if (cmd.ToLower().Equals(commandAutopickup.ToLower()))
                {
                    _autoPickupPastWeightlimit.Value = !_autoPickupPastWeightlimit.Value;

                    Traverse.Create(__instance).Method("AddString", new object[] { $"toggled autopickuppastlimit ({_autoPickupPastWeightlimit.Value})" }).GetValue();
                }
            }
        }

        // Disable flickering
        [HarmonyPatch(typeof(InventoryGui), "UpdateInventoryWeight")]
        static class UpdateInventoryWeight_Patch
        {
            static bool Prefix(Player player, Text ___m_weight)
            {
                if (_encumberedUIFlicker.Value)
                    return true; // let the original method run

                // disable the flicker
                int num = Mathf.CeilToInt(player.GetInventory().GetTotalWeight());
                int num2 = Mathf.CeilToInt(player.GetMaxCarryWeight());
                ___m_weight.text = num + "/" + num2;
                return false; // stop executing prefixes and skip the original
            }
        }
    }
}
