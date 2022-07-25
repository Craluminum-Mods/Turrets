using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;

namespace CRTurrets
{
  public class EntityBehaviorAggressor : EntityBehavior
  {
    public EntityBehaviorAggressor(Entity entity) : base(entity) { }
    public override string PropertyName() => "cr_aggressor";

    // public override void OnEntityDeath(DamageSource damageSourceForDeath)
    // {
    //   var aggressor = this;
    //   var victimId = entity.WatchedAttributes.GetLong("tmpVictimId");

    //   if (victimId is not 0)
    //   {
    //     aggressor.entity.WatchedAttributes.RemoveAttribute("tmpVictimId");

    //     var victim = aggressor.entity.World.GetEntityById(victimId);

    //     if (!victim.WatchedAttributes.HasAttribute("tmpAggressorId"))
    //     {
    //       base.OnEntityDeath(damageSourceForDeath);
    //     }

    //     victim.WatchedAttributes.RemoveAttribute("tmpAggressorId");

    //     victim.WatchedAttributes.SetLong("tmpDeadAggressorId", aggressor.entity.EntityId);
    //   }

    //   base.OnEntityDeath(damageSourceForDeath);
    // }
  }
}