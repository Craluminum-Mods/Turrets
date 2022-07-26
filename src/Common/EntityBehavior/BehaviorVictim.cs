using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;

namespace CRTurrets
{
  public class EntityBehaviorVictim : EntityBehavior
  {
    public EntityBehaviorVictim(Entity entity) : base(entity) { }
    public override string PropertyName() => "cr_victim";
    string successKillSound;

    public override void Initialize(EntityProperties properties, JsonObject attributes)
    {
      base.Initialize(properties, attributes);

      successKillSound = entity.Properties.Attributes["turretProperties"]["successKillSound"].AsString();
    }

    public override void OnGameTick(float dt)
    {
      base.OnGameTick(dt);

      var aggressorDead = entity.WatchedAttributes.GetLong("tmpDeadAggressorId");

      if (aggressorDead != 0)
      {
        // (Api as ICoreServerAPI)?.BroadcastMessageToAllGroups(aggressorDead + " is dead now", EnumChatType.Notification);
        entity.World.PlaySoundAt(new AssetLocation(successKillSound), entity);
        entity.WatchedAttributes.RemoveAttribute("tmpAggressorId");
        entity.WatchedAttributes.RemoveAttribute("tmpDeadAggressorId");
      }
    }

    public override void OnEntityReceiveDamage(DamageSource damageSource, ref float damage)
    {
      var victim = this;
      var aggressor = entity.World.GetEntityById(damageSource.SourceEntity.EntityId);

      if (aggressor.EntityId != 0 && victim.entity.EntityId != 0)
      {
        victim.entity.WatchedAttributes.SetLong("tmpAggressorId", aggressor.EntityId);
        aggressor.WatchedAttributes.SetLong("tmpVictimId", victim.entity.EntityId);
        aggressor.AddBehavior(new EntityBehaviorAggressor(aggressor));
      }

      base.OnEntityReceiveDamage(damageSource, ref damage);
    }

    public override void GetInfoText(StringBuilder infotext)
    {
      base.GetInfoText(infotext);

      var tmpAggressorId = entity.WatchedAttributes.GetLong("tmpAggressorId");
      var tmpDeadAggressorId = entity.WatchedAttributes.GetLong("tmpDeadAggressorId");

      infotext.AppendLine("Debug info:");
      infotext.AppendLine(Lang.Get("Aggressor: {0}", tmpAggressorId.ToString() ?? "-"));
      infotext.AppendLine(Lang.Get("Dead Aggressor ID: {0}", tmpDeadAggressorId.ToString() ?? "-"));
    }
  }
}