using BepInEx;
using HarmonyLib;
using System.Reflection;
using UnityEngine;

namespace ValheimMods
{
    [BepInProcess("valheim.exe")]
    [BepInPlugin("juliandeclercq.NeverEncumbered", "Never Encumbered", "1.0.0.0")]
    public class NeverEncumbered : BaseUnityPlugin
    {
        private void Awake()
        {
            Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), null);
        }

        [HarmonyPatch(typeof(Player), "IsEncumbered")]
        static class IsEncumberedPatch
        {
            static void Postfix(ref bool __result)
            {
                __result = false;
            }
        }

        [HarmonyPatch(typeof(Player), "AutoPickup")]
        static class AutoPickupPatch
        {
            static void Postfix(float dt, Player __instance, Inventory ___m_inventory, float ___m_autoPickupRange, int ___m_autoPickupMask)
            {
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
    }
}
