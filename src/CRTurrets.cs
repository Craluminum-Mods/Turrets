using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.GameContent;

[assembly: ModInfo("Turrets",
  Authors = new[] { "Craluminum2413" })]

namespace CRTurrets
{
  class CRTurrets : ModSystem
  {
    public override void Start(ICoreAPI api)
    {
      base.Start(api);

      api.World.Logger.Event("started 'Turrets' mod");
      api.RegisterEntity("CR_EntityTurret", typeof(EntityTurret));
      api.RegisterItemClass("CR_ItemTurret", typeof(ItemTurret));
      api.RegisterEntityBehaviorClass("cr_aggressor", typeof(EntityBehaviorAggressor));
      api.RegisterEntityBehaviorClass("cr_victim", typeof(EntityBehaviorVictim));
      AiTaskRegistry.Register("cr_turret", typeof(AiTaskTurret));
    }

    public override void StartClientSide(ICoreClientAPI api)
    {
      base.StartClientSide(api);
      api.RegisterEntityRendererClass("CRShapeEntityTurretRenderer", typeof(ShapeEntityTurretRenderer));
    }
  }
}