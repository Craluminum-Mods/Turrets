using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace CRTurrets
{
  public class EntityTurret : EntityHumanoid
  {
    string turretItem;
    string allowedAmmo;
    protected InventoryGeneric inv;

    // string[] blacklistEntityIds;
    // string[] blacklistPlayerIds;

    public override void Initialize(EntityProperties properties, ICoreAPI api, long InChunkIndex3d)
    {
      base.Initialize(properties, api, InChunkIndex3d);

      turretItem = Properties.Attributes["turretProperties"]["turretItem"].AsString();
      allowedAmmo = Properties.Attributes["turretProperties"]["acceptAmmoClass"].AsString();
      api.Event.RegisterGameTickListener(UpdateTurretInfo, 500);

      inv = new InventoryGeneric(properties.Attributes["turretProperties"]["quantitySlots"].AsInt(4), "turretContents-" + EntityId, Api);
      if (WatchedAttributes["turretInv"] is TreeAttribute tree) inv.FromTreeAttributes(tree);
      inv.PutLocked = false;

      if (World.Side == EnumAppSide.Server) inv.SlotModified += Inv_SlotModified;

      if (World.Side == EnumAppSide.Client) WatchedAttributes.RegisterModifiedListener("turretInv", OnInventoryModified);
    }

    private void OnInventoryModified()
    {
      if (WatchedAttributes["turretInv"] is TreeAttribute tree)
      {
        inv.FromTreeAttributes(tree);
      }
    }

    private void Inv_SlotModified(int slotid)
    {
      var tree = new TreeAttribute();
      inv.ToTreeAttributes(tree);
      WatchedAttributes["turretInv"] = tree;
      WatchedAttributes.MarkPathDirty("turretInv");
    }

    /// <summary>
    /// I found nothing better than tick listener. OnHurt() doesn't update on healing
    /// </summary>
    private void UpdateTurretInfo(float dt) => UpdateHealthPercent();

    private void UpdateHealthPercent()
    {
      var currentHealth = WatchedAttributes.GetTreeAttribute("health").GetFloat("currenthealth");
      var maxHealth = WatchedAttributes.GetTreeAttribute("health").GetFloat("maxhealth");
      WatchedAttributes.SetInt("healthPercent", (int)(currentHealth / maxHealth * 100));
    }

    public override bool ShouldReceiveDamage(DamageSource damageSource, float damage)
    {
      if (damageSource?.SourceEntity != null && damageSource?.SourceEntity?.EntityId == World.PlayerByUid(WatchedAttributes.GetString("ownerUid"))?.Entity?.EntityId)
      {
        return false;
      }

      return base.ShouldReceiveDamage(damageSource, damage);
    }

    public override void OnEntitySpawn()
    {
      base.OnEntitySpawn();

      WatchedAttributes.GetTreeAttribute("health").SetFloat("basemaxhealth", WatchedAttributes.GetFloat("tmpMaxHealth"));
      WatchedAttributes.GetTreeAttribute("health").SetFloat("maxhealth", WatchedAttributes.GetFloat("tmpMaxHealth"));
      WatchedAttributes.GetTreeAttribute("health").SetFloat("currenthealth", WatchedAttributes.GetFloat("tmpHealth"));
      WatchedAttributes.RemoveAttribute("tmpMaxHealth");
      WatchedAttributes.RemoveAttribute("tmpHealth");
      UpdateHealthPercent();
    }

    public override void OnInteract(EntityAgent byEntity, ItemSlot slot, Vec3d hitPosition, EnumInteractMode mode)
    {
      if (!Alive || World.Side == EnumAppSide.Client || mode == 0)
      {
        base.OnInteract(byEntity, slot, hitPosition, mode);
        return;
      }

      string owneruid = WatchedAttributes.GetString("ownerUid", null);
      string agentUid = (byEntity as EntityPlayer)?.PlayerUID;

      if (agentUid == null) return;
      if (!(string.IsNullOrEmpty(owneruid) || owneruid == agentUid)) return;

      bool ctrlKey = byEntity.Controls.CtrlKey;
      bool shiftKey = byEntity.Controls.ShiftKey;
      bool rightSlotEmpty = byEntity.RightHandItemSlot.Empty;

      if (!shiftKey && ctrlKey && rightSlotEmpty)
      {
        GetNewStatus(); return;
      }

      if (shiftKey && !ctrlKey && slot.Itemstack != null)
      {
        TryPutAmmo(slot); return;
      }

      if (!shiftKey && !ctrlKey && rightSlotEmpty)
      {
        TryTakeAmmo(slot); return;
      }

      if (shiftKey && !ctrlKey && rightSlotEmpty)
      {
        TryPickup(byEntity, owneruid); return;
      }

      base.OnInteract(byEntity, slot, hitPosition, mode);
    }

    private void TryPickup(EntityAgent byEntity, string owneruid)
    {
      var stack = new ItemStack(byEntity.World.GetItem(new AssetLocation(turretItem)));
      var currentHealth = WatchedAttributes.GetTreeAttribute("health").GetFloat("currenthealth");

      stack.Attributes.SetFloat("health", currentHealth);
      stack.Attributes.SetInt("healthPercent", WatchedAttributes.GetInt("healthPercent"));
      stack.Attributes.SetString("ownerUid", owneruid);

      if (!byEntity.TryGiveItemStack(stack))
      {
        byEntity.World.SpawnItemEntity(stack, ServerPos.XYZ);
      }

      Die();
    }

    private void TryPutAmmo(ItemSlot slot)
    {
      if (slot.Itemstack == null) return;
      if (slot.Itemstack.Collectible.Class != allowedAmmo) return;
      slot.TryPutInto(World, inv[0], 1);
    }

    private void TryTakeAmmo(ItemSlot slot)
    {
      inv[0].TryPutInto(World, slot, 1);
    }

    private void GetNewStatus() => WatchedAttributes.SetBool("crturret-status", !WatchedAttributes.GetBool("crturret-status"));

    public override string GetInfoText()
    {
      base.GetInfoText();

      var sb = new StringBuilder();
      var playerName = World.PlayerByUid(WatchedAttributes.GetString("ownerUid")).PlayerName;

      sb.AppendLine(Lang.Get("Owner: {0}", playerName ?? "-"));
      GetHealthDescription(sb);
      GetInventorySlotsDescription(sb);

      return sb.ToString();
    }

    private void GetHealthDescription(StringBuilder sb)
    {
      var currentHealth = WatchedAttributes.GetTreeAttribute("health").GetFloat("currenthealth");
      var maxHealth = WatchedAttributes.GetTreeAttribute("health").GetFloat("maxhealth");
      var healthPercent = WatchedAttributes.GetInt("healthPercent");

      sb.AppendLine(Lang.Get("Health: {0}/{1}", currentHealth, maxHealth));
      sb.AppendLine(Lang.Get("Health %: {0}", healthPercent));
    }

    private void GetInventorySlotsDescription(StringBuilder sb)
    {
      sb.AppendLine(Lang.Get("Storage Slots: {0}", inv.Count));

      foreach (var slot in inv)
      {
        if (slot == null) continue;

        var slotid = inv.GetSlotId(slot);

        if (slot.Empty) sb.AppendLine(Lang.Get("Slot {0}: Empty", slotid));

        if (slot.Itemstack == null) continue;

        var name = slot.Itemstack.GetName();
        var quantity = slot.Itemstack.StackSize;

        sb.AppendLine(Lang.Get($"Slot {slotid}: {quantity}x {name}"));
      }
    }

    public override double GetWalkSpeedMultiplier(double groundDragFactor = 0.3) => 0;

    public override WorldInteraction[] GetInteractionHelp(IClientWorldAccessor world, EntitySelection es, IClientPlayer player)
    {
      var interactions = ObjectCacheUtil.GetOrCreate(world.Api, "turretInteractions" + EntityId, () =>
        {
          return new WorldInteraction[] {
            new WorldInteraction()
            {
              ActionLangCode = "On / Off",
              MouseButton = EnumMouseButton.Right,
              HotKeyCode = "ctrl"
            },
            new WorldInteraction()
            {
              ActionLangCode = Code.Domain + ":entityhelp-addammo",
              MouseButton = EnumMouseButton.Right,
              HotKeyCode = "shift",
              Itemstacks = null
            }
          };
        });
      return interactions.Append(base.GetInteractionHelp(world, es, player));
    }
  }
}