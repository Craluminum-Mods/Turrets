using System.Collections.Generic;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace CRTurrets
{
  public class EntityTurret : EntityHumanoid
  {
    string turretItem;

    // string[] blacklistEntityIds;
    // string[] blacklistPlayerIds;

    public override void Initialize(EntityProperties properties, ICoreAPI api, long InChunkIndex3d)
    {
      base.Initialize(properties, api, InChunkIndex3d);

      turretItem = Properties.Attributes["turretProperties"]["turretItem"].AsString();
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

      WatchedAttributes.SetBool("crturret-status", false);
      WatchedAttributes.GetTreeAttribute("health").SetFloat("basemaxhealth", WatchedAttributes.GetFloat("tmpMaxHealth"));
      WatchedAttributes.GetTreeAttribute("health").SetFloat("maxhealth", WatchedAttributes.GetFloat("tmpMaxHealth"));
      WatchedAttributes.GetTreeAttribute("health").SetFloat("currenthealth", WatchedAttributes.GetFloat("tmpHealth"));
      WatchedAttributes.RemoveAttribute("tmpMaxHealth");
      WatchedAttributes.RemoveAttribute("tmpHealth");
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
        var status = WatchedAttributes.GetBool("crturret-status");
        WatchedAttributes.SetBool("crturret-status", !status);

        return;
      }

      if (shiftKey && !ctrlKey && rightSlotEmpty)
      {
        var stack = new ItemStack(byEntity.World.GetItem(new AssetLocation(turretItem)));
        var currentHealth = WatchedAttributes.GetTreeAttribute("health").GetFloat("currenthealth");

        stack.Attributes.SetFloat("health", currentHealth);
        stack.Attributes.SetString("ownerUid", owneruid);

        if (!byEntity.TryGiveItemStack(stack))
        {
          byEntity.World.SpawnItemEntity(stack, ServerPos.XYZ);
        }

        Die();
        return;
      }

      base.OnInteract(byEntity, slot, hitPosition, mode);
    }

    public override string GetInfoText()
    {
      base.GetInfoText();

      var sb = new StringBuilder();

      var currentHealth = WatchedAttributes.GetTreeAttribute("health").GetFloat("currenthealth");
      var maxHealth = WatchedAttributes.GetTreeAttribute("health").GetFloat("maxhealth");

      var playerName = World.PlayerByUid(WatchedAttributes.GetString("ownerUid")).PlayerName;

      var status = WatchedAttributes.GetBool("crturret-status");
      var color = status ? "#84ff84" : "#ff8484";
      var onoff = Lang.Get(status ? "On" : "Off");

      sb.Append("<font size=\"24\" weight=\"bold\" color=\"")
      .Append(color)
      .Append("\">")
      .Append(onoff)
      .AppendLine("</font>");
      sb.AppendLine(Lang.Get("Health: {0}/{1}", currentHealth.ToString() ?? "-", maxHealth.ToString() ?? "-"));
      sb.AppendLine(Lang.Get("Owner: {0}", playerName ?? "-"));

      return sb.ToString();
    }

    public override double GetWalkSpeedMultiplier(double groundDragFactor = 0.3) => 0;

    public override WorldInteraction[] GetInteractionHelp(IClientWorldAccessor world, EntitySelection es, IClientPlayer player)
    {
      var interactions = ObjectCacheUtil.GetOrCreate(world.Api, "turretInteractions" + EntityId, () =>
        {
          var arrowStacklist = new List<ItemStack>();
          var stoneStacklist = new List<ItemStack>();

          foreach (var collobj in world.Collectibles)
          {
            if (collobj.Code == null) continue;

            if (collobj is ItemArrow) arrowStacklist.Add(new ItemStack(collobj));
            if (collobj is ItemStone) stoneStacklist.Add(new ItemStack(collobj));
          }

          return new WorldInteraction[] {
            new WorldInteraction()
            {
              ActionLangCode = "On / Off",
              MouseButton = EnumMouseButton.Right,
              HotKeyCode = "ctrl"
            },
            new WorldInteraction()
            {
              ActionLangCode = Code.Domain + ":entityhelp-addammo-arrow",
              MouseButton = EnumMouseButton.Right,
              HotKeyCode = "shift",
              Itemstacks = arrowStacklist.ToArray()
            },
            new WorldInteraction()
            {
              ActionLangCode = Code.Domain + ":entityhelp-addammo-stone",
              MouseButton = EnumMouseButton.Right,
              HotKeyCode = "shift",
              Itemstacks = stoneStacklist.ToArray()
            }
          };
        });
      return interactions.Append(base.GetInteractionHelp(world, es, player));
    }
  }
}