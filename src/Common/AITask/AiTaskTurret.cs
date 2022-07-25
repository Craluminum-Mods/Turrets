using System;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
  public class AiTaskTurret : AiTaskBaseTargetable
  {
    int durationMs;
    int releaseAtMs;
    long lastSearchTotalMs;

    float minVertDist = 2f;
    float minDist = 3f;
    float maxDist = 15f;

    EntityPartitioning partitionUtil;

    float accum = 0;
    bool didThrow;

    float minTurnAnglePerSec;
    float maxTurnAnglePerSec;
    float curTurnRadPerSec;

    public AiTaskTurret(EntityAgent entity) : base(entity) { }

    public override void LoadConfig(JsonObject taskConfig, JsonObject aiConfig)
    {
      partitionUtil = entity.Api.ModLoader.GetModSystem<EntityPartitioning>();

      base.LoadConfig(taskConfig, aiConfig);

      durationMs = taskConfig["durationMs"].AsInt(1500);
      releaseAtMs = taskConfig["releaseAtMs"].AsInt(1000);
      minDist = taskConfig["minDist"].AsFloat(3f);
      minVertDist = taskConfig["minVertDist"].AsFloat(2f);
      maxDist = taskConfig["maxDist"].AsFloat(15f);
    }

    private string GetOwnerUid(Entity entity) => entity.WatchedAttributes.GetString("ownerUid");

    public bool HasSameOwner(Entity e) => GetOwnerUid(entity) == GetOwnerUid(e);

    public Entity GetOwner()
    {
      return (GetOwnerUid(entity) != null) ? entity.World.PlayerByUid(GetOwnerUid(entity)).Entity : null;
    }

    public override bool IsTargetableEntity(Entity e, float range, bool ignoreEntityCode = false)
    {
      return e != GetOwner() && !HasSameOwner(e) && base.IsTargetableEntity(e, range, ignoreEntityCode);
    }

    public override bool ShouldExecute()
    {
      if (!entity.WatchedAttributes.GetBool("crturret-status")) return false;

      if (rand.NextDouble() > 0.1f && (whenInEmotionState == null || bhEmo?.IsInEmotionState(whenInEmotionState) != true)) return false;

      if (whenInEmotionState != null && bhEmo?.IsInEmotionState(whenInEmotionState) != true) return false;
      if (whenNotInEmotionState != null && bhEmo?.IsInEmotionState(whenNotInEmotionState) == true) return false;
      if (whenInEmotionState == null && rand.NextDouble() > 0.5f) return false;
      if (cooldownUntilMs > entity.World.ElapsedMilliseconds) return false;

      float range = maxDist;
      lastSearchTotalMs = entity.World.ElapsedMilliseconds;
      Vec3d ownPos = entity.ServerPos.XYZ;

      targetEntity = partitionUtil.GetNearestEntity(entity.ServerPos.XYZ, range, (e) => IsTargetableEntity(e, range) && hasDirectContact(e, range, range / 2f));

      return targetEntity != null;
    }

    public override void StartExecute()
    {
      accum = 0;
      didThrow = false;

      if (entity?.Properties.Server?.Attributes != null)
      {
        minTurnAnglePerSec = entity.Properties.Server.Attributes.GetTreeAttribute("pathfinder").GetFloat("minTurnAnglePerSec", 250);
        maxTurnAnglePerSec = entity.Properties.Server.Attributes.GetTreeAttribute("pathfinder").GetFloat("maxTurnAnglePerSec", 450);
      }
      else
      {
        minTurnAnglePerSec = 250;
        maxTurnAnglePerSec = 450;
      }

      curTurnRadPerSec = minTurnAnglePerSec + ((float)entity.World.Rand.NextDouble() * (maxTurnAnglePerSec - minTurnAnglePerSec));
      curTurnRadPerSec *= GameMath.DEG2RAD * 50 * 0.02f;
    }

    public override bool ContinueExecute(float dt)
    {
      var targetVec = new Vec3f();

      targetVec.Set(
          (float)(targetEntity.ServerPos.X - entity.ServerPos.X),
          (float)(targetEntity.ServerPos.Y - entity.ServerPos.Y),
          (float)(targetEntity.ServerPos.Z - entity.ServerPos.Z)
      );

      float desiredYaw = (float)Math.Atan2(targetVec.X, targetVec.Z);

      float yawDist = GameMath.AngleRadDistance(entity.ServerPos.Yaw, desiredYaw);
      entity.ServerPos.Yaw += GameMath.Clamp(yawDist, -curTurnRadPerSec * dt, curTurnRadPerSec * dt);
      entity.ServerPos.Yaw %= GameMath.TWOPI;

      if (Math.Abs(yawDist) > 0.02) return true;

      if (animMeta != null)
      {
        animMeta.EaseInSpeed = 1f;
        animMeta.EaseOutSpeed = 1f;
        entity.AnimManager.StartAnimation(animMeta);
      }

      accum += dt;

      if (accum > releaseAtMs / 1000f && !didThrow)
      {
        didThrow = true;

        EntityProperties type = entity.World.GetEntityType(new AssetLocation("thrownstone-granite"));
        Entity entitypr = entity.World.ClassRegistry.CreateEntity(type);
        ((EntityThrownStone)entitypr).FiredBy = entity;
        ((EntityThrownStone)entitypr).Damage = 1;
        ((EntityThrownStone)entitypr).ProjectileStack = new ItemStack(entity.World.GetItem(new AssetLocation("stone-granite")));
        ((EntityThrownStone)entitypr).NonCollectible = false;

        Vec3d pos = entity.ServerPos.XYZ.Add(0, entity.LocalEyePos.Y, 0);
        Vec3d aheadPos = targetEntity.ServerPos.XYZ.Add(0, targetEntity.LocalEyePos.Y, 0);

        double distf = Math.Pow(pos.SquareDistanceTo(aheadPos), 0.1);
        Vec3d velocity = (aheadPos - pos).Normalize() * GameMath.Clamp(distf - 1f, 0.1f, 1f);

        entitypr.ServerPos.SetPos(
            entity.ServerPos.BehindCopy(0.21).XYZ.Add(0, entity.LocalEyePos.Y, 0)
        );

        entitypr.ServerPos.Motion.Set(velocity);

        entitypr.Pos.SetFrom(entitypr.ServerPos);
        entitypr.World = entity.World;
        entity.World.SpawnEntity(entitypr);
      }

      return accum < durationMs / 1000f;
    }
  }
}