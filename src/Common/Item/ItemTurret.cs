using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace CRTurrets
{
  public class ItemTurret : Item
  {
    float maxHealth;
    string pickupSound;
    string turretEntity;

    public override void OnLoaded(ICoreAPI api)
    {
      base.OnLoaded(api);
      maxHealth = Attributes["turretProperties"]["maxHealth"].AsFloat();
      pickupSound = Attributes["turretProperties"]["pickupSound"].AsString();
      turretEntity = Attributes["turretProperties"]["turretEntity"].AsString();
    }

    public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
    {
      if (blockSel == null) return;
      var player = byEntity.World.PlayerByUid((byEntity as EntityPlayer)?.PlayerUID);

      if (!byEntity.World.Claims.TryAccess(player, blockSel.Position, EnumBlockAccessFlags.BuildOrBreak))
      {
        slot.MarkDirty();
        return;
      }

      if (byEntity is not EntityPlayer || player.WorldData.CurrentGameMode != EnumGameMode.Creative)
      {
        slot.TakeOut(1);
        slot.MarkDirty();
      }

      var type = byEntity.World.GetEntityType(new AssetLocation(turretEntity));
      var entity = byEntity.World.ClassRegistry.CreateEntity(type);

      if (entity == null) return;

      entity.ServerPos.X = blockSel.Position.X + (blockSel.DidOffset ? 0 : blockSel.Face.Normali.X) + 0.5f;
      entity.ServerPos.Y = blockSel.Position.Y + (blockSel.DidOffset ? 0 : blockSel.Face.Normali.Y);
      entity.ServerPos.Z = blockSel.Position.Z + (blockSel.DidOffset ? 0 : blockSel.Face.Normali.Z) + 0.5f;
      entity.ServerPos.Yaw = byEntity.SidedPos.Yaw + GameMath.PI;

      if ((player?.PlayerUID) != null)
      {
        entity.WatchedAttributes.SetString("ownerUid", player.PlayerUID);
      }

      entity.WatchedAttributes.SetFloat("tmpHealth", GetHealth(slot));
      entity.WatchedAttributes.SetFloat("tmpMaxHealth", maxHealth);

      if (!player.Entity.Controls.ShiftKey && player.Entity.Controls.CtrlKey)
      {
        entity.WatchedAttributes.SetBool("crturret-status", true);
      }

      entity.Pos.SetFrom(entity.ServerPos);

      byEntity.World.PlaySoundAt(new AssetLocation(pickupSound), entity, player);

      byEntity.World.SpawnEntity(entity);
      handling = EnumHandHandling.PreventDefaultAction;
    }

    public float GetHealth(ItemSlot slot)
    {
      var health = slot?.Itemstack?.Attributes?.TryGetFloat("health");

      if (health == null || health == 0) return maxHealth;

      return (float)health;
    }

    public override WorldInteraction[] GetHeldInteractionHelp(ItemSlot inSlot)
    {
      return new WorldInteraction[]
      {
        new WorldInteraction
        {
          ActionLangCode = "heldhelp-place",
          MouseButton = EnumMouseButton.Right
        },
        new WorldInteraction
        {
          ActionLangCode = Code.Domain + ":heldhelp-place-enabled",
          MouseButton = EnumMouseButton.Right,
          HotKeyCode = "ctrl"
        }
      }.Append(base.GetHeldInteractionHelp(inSlot));
    }

    public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
    {
      base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);

      var currentHealth = inSlot.Itemstack.Attributes.GetFloat("health");
      var ownerUid = inSlot.Itemstack.Attributes.GetString("ownerUid");

      if (currentHealth != 0)
        dsc.AppendLine(Lang.Get("Health: {0}/{1}", currentHealth, maxHealth));

      if (ownerUid != null)
      {
        var playerName = world.PlayerByUid(ownerUid).PlayerName;
        dsc.AppendLine(Lang.Get("Owner: {0}", playerName ?? "-"));
      }
    }
  }
}