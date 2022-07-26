using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace CRTurrets
{
  public class ShapeEntityTurretRenderer : EntityShapeRenderer
  {
    private ITexPositionSource defaultTexSource;
    public EntityTurret turret;
    private bool loaded;
    private readonly int skipRenderJointId = -2;
    private readonly int skipRenderJointId2 = -2;
    public int DefaultHealthPercent { get; set; }
    public bool DefaultStatusState { get; set; }
    public bool DefaultHealth10 { get; set; }
    public bool DefaultHealth20 { get; set; }
    public bool DefaultHealth30 { get; set; }
    public bool DefaultHealth40 { get; set; }
    public bool DefaultHealth50 { get; set; }
    public bool DefaultHealth60 { get; set; }
    public bool DefaultHealth70 { get; set; }
    public bool DefaultHealth80 { get; set; }
    public bool DefaultHealth90 { get; set; }
    public bool DefaultHealth100 { get; set; }

    public ShapeEntityTurretRenderer(Entity entity, ICoreClientAPI api) : base(entity, api)
    {
      turret = entity as EntityTurret;

      DoRenderHeldItem = true;
      DefaultStatusState = turret.WatchedAttributes.GetBool("crturret-status");
      DefaultHealthPercent = turret.WatchedAttributes.GetInt("healthPercent");

      DefaultHealth10 = DefaultHealthPercent is <= 10 and >= 0;
      DefaultHealth20 = DefaultHealthPercent is <= 20 and >= 10;
      DefaultHealth30 = DefaultHealthPercent is <= 30 and >= 20;
      DefaultHealth40 = DefaultHealthPercent is <= 40 and >= 30;
      DefaultHealth50 = DefaultHealthPercent is <= 50 and >= 40;
      DefaultHealth60 = DefaultHealthPercent is <= 60 and >= 50;
      DefaultHealth70 = DefaultHealthPercent is <= 70 and >= 60;
      DefaultHealth80 = DefaultHealthPercent is <= 80 and >= 70;
      DefaultHealth90 = DefaultHealthPercent is <= 90 and >= 80;
      DefaultHealth100 = false;

      api.Event.RegisterGameTickListener(UpdateTurretInfo, 500);
      api.Event.ReloadShapes += MarkShapeModified;
    }

    public override TextureAtlasPosition this[string textureCode]
      => extraTexturesByTextureName?.TryGetValue(textureCode, out var value) ?? false
      ? capi.EntityTextureAtlas.Positions[value.Baked.TextureSubId]
      : defaultTexSource[textureCode];

    public new event OnEntityShapeTesselationDelegate OnTesselation;

    public void UpdateTurretInfo(float dt)
    {
      var healthPercent = turret.WatchedAttributes.GetInt("healthPercent");

      DefaultStatusState = turret.WatchedAttributes.GetBool("crturret-status");

      if (healthPercent is <= 10 and >= 0) { DefaultHealth10 = true; DefaultHealth20 = true; DefaultHealth30 = true; DefaultHealth40 = true; DefaultHealth50 = true; DefaultHealth60 = true; DefaultHealth70 = true; DefaultHealth80 = true; DefaultHealth90 = true; DefaultHealth100 = true; }
      if (healthPercent is <= 20 and >= 10) { DefaultHealth20 = true; DefaultHealth30 = true; DefaultHealth40 = true; DefaultHealth50 = true; DefaultHealth60 = true; DefaultHealth70 = true; DefaultHealth80 = true; DefaultHealth90 = true; DefaultHealth100 = true; }
      if (healthPercent is <= 30 and >= 20) { DefaultHealth30 = true; DefaultHealth40 = true; DefaultHealth50 = true; DefaultHealth60 = true; DefaultHealth70 = true; DefaultHealth80 = true; DefaultHealth90 = true; DefaultHealth100 = true; }
      if (healthPercent is <= 40 and >= 30) { DefaultHealth40 = true; DefaultHealth50 = true; DefaultHealth60 = true; DefaultHealth70 = true; DefaultHealth80 = true; DefaultHealth90 = true; DefaultHealth100 = true; }
      if (healthPercent is <= 50 and >= 40) { DefaultHealth50 = true; DefaultHealth60 = true; DefaultHealth70 = true; DefaultHealth80 = true; DefaultHealth90 = true; DefaultHealth100 = true; }
      if (healthPercent is <= 60 and >= 50) { DefaultHealth60 = true; DefaultHealth70 = true; DefaultHealth80 = true; DefaultHealth90 = true; DefaultHealth100 = true; }
      if (healthPercent is <= 70 and >= 60) { DefaultHealth70 = true; DefaultHealth80 = true; DefaultHealth90 = true; DefaultHealth100 = true; }
      if (healthPercent is <= 80 and >= 70) { DefaultHealth80 = true; DefaultHealth90 = true; DefaultHealth100 = true; }
      if (healthPercent is <= 90 and >= 80) { DefaultHealth90 = true; DefaultHealth100 = true; }

      MarkShapeModified();
    }

    public override void OnEntityLoaded()
    {
      loaded = true;
      MarkShapeModified();
    }

    public override void TesselateShape()
    {
      if (!loaded) return;

      shapeFresh = true;
      CompositeShape compositeShape = OverrideCompositeShape ?? entity.Properties.Client.Shape;
      Shape entityShape = OverrideEntityShape ?? entity.Properties.Client.LoadedShapeForEntity;

      if (entityShape == null) return;

      OnTesselation?.Invoke(ref entityShape, compositeShape.ToString());
      entity.OnTesselation(ref entityShape, compositeShape.ToString());
      defaultTexSource = GetTextureSource();

      ApplyTurretTextures(turret);
      if (!turret.WatchedAttributes.GetBool("crturret-broken") && turret.WatchedAttributes.GetBool("crturret-status"))
      {
        ApplyTurretTextures(turret);
        if (DefaultHealthPercent != 0) ApplyTurretTextures(turret);
      }
      else
      {
        ApplyTurretTextures(turret);

        defaultTexSource = GetTextureSource();
      }
      TyronThreadPool.QueueTask(delegate
      {
        MeshData modeldata;
        try
        {
          capi.Tesselator.TesselateShape("entity-crturret", entityShape, out modeldata, this);
        }
        catch (Exception ex)
        {
          capi.World.Logger.Fatal("Failed tesselating entity {0} with id {1}. Entity will probably be invisible!. The teselator threw {2}", entity.Code, entity.EntityId, ex);
          return;
        }
        MeshData opaqueMesh = modeldata.Clone().Clear();
        MeshData oitMesh = modeldata.Clone().Clear();
        opaqueMesh.AddMeshData(modeldata, EnumChunkRenderPass.Opaque);
        oitMesh.AddMeshData(modeldata, EnumChunkRenderPass.Transparent);
        capi.Event.EnqueueMainThreadTask(delegate
        {
          if (meshRefOpaque != null)
          {
            meshRefOpaque.Dispose();
            meshRefOpaque = null;
          }
          if (meshRefOit != null)
          {
            meshRefOit.Dispose();
            meshRefOit = null;
          }
          if (!capi.IsShuttingDown)
          {
            if (opaqueMesh.VerticesCount > 0)
            {
              meshRefOpaque = capi.Render.UploadMesh(opaqueMesh);
            }
            if (oitMesh.VerticesCount > 0)
            {
              meshRefOit = capi.Render.UploadMesh(oitMesh);
            }
          }
        }, "uploadentitymesh");
        capi.TesselatorManager.ThreadDispose();
      });
    }

    private void ApplyTurretTextures(EntityTurret turretIn)
    {
      var textures = turretIn.Properties.Client.Textures;

      textures["iron"] = textures["iron"];
      textures["copper"] = textures["copper"];

      textures["status"] = !turretIn.WatchedAttributes.GetBool("crturret-status") ? textures["color-red"] : textures["color-green"];

      textures["health10"] = DefaultHealth10 ? textures["color-gray"] : textures["color-white"];
      textures["health20"] = DefaultHealth20 ? textures["color-gray"] : textures["color-white"];
      textures["health30"] = DefaultHealth30 ? textures["color-gray"] : textures["color-white"];
      textures["health40"] = DefaultHealth40 ? textures["color-gray"] : textures["color-white"];
      textures["health50"] = DefaultHealth50 ? textures["color-gray"] : textures["color-white"];
      textures["health60"] = DefaultHealth60 ? textures["color-gray"] : textures["color-white"];
      textures["health70"] = DefaultHealth70 ? textures["color-gray"] : textures["color-white"];
      textures["health80"] = DefaultHealth80 ? textures["color-gray"] : textures["color-white"];
      textures["health90"] = DefaultHealth90 ? textures["color-gray"] : textures["color-white"];
      textures["health100"] = DefaultHealth100 ? textures["color-gray"] : textures["color-white"];

      defaultTexSource = GetTextureSource();
    }

    protected void TurretTextureModified() => MarkShapeModified();

    public override void reloadSkin() { }

    public override void BeforeRender(float dt)
    {
      if (!shapeFresh) TesselateShape();

      if ((meshRefOpaque == null && meshRefOit == null) || capi.IsGamePaused) return;

      isSpectator = player?.WorldData.CurrentGameMode == EnumGameMode.Spectator;

      if (isSpectator) return;

      ApplyTurretTextures(turret);
    }

    public override void DoRender3DOpaqueBatched(float dt, bool isShadowPass)
    {
      if (isSpectator || (meshRefOpaque == null && meshRefOit == null)) return;

      if (isShadowPass)
      {
        Mat4f.Mul(tmpMvMat, capi.Render.CurrentModelviewMatrix, ModelMat);
        capi.Render.CurrentActiveShader.UniformMatrix("modelViewMatrix", tmpMvMat);
      }
      else
      {
        frostAlpha += (targetFrostAlpha - frostAlpha) * dt / 10f;
        capi.Render.CurrentActiveShader.Uniform("extraGlow", entity.Properties.Client.GlowLevel);
        capi.Render.CurrentActiveShader.UniformMatrix("modelMatrix", ModelMat);
        capi.Render.CurrentActiveShader.UniformMatrix("viewMatrix", capi.Render.CurrentModelviewMatrix);
        capi.Render.CurrentActiveShader.Uniform("addRenderFlags", AddRenderFlags);
        capi.Render.CurrentActiveShader.Uniform("windWaveIntensity", (float)WindWaveIntensity);
        capi.Render.CurrentActiveShader.Uniform("skipRenderJointId", skipRenderJointId);
        capi.Render.CurrentActiveShader.Uniform("skipRenderJointId2", skipRenderJointId2);
        capi.Render.CurrentActiveShader.Uniform("entityId", (int)entity.EntityId);
        capi.Render.CurrentActiveShader.Uniform("glitchFlicker", glitchFlicker ? 1 : 0);
        capi.Render.CurrentActiveShader.Uniform("frostAlpha", GameMath.Clamp(frostAlpha, 0f, 1f));
        capi.Render.CurrentActiveShader.Uniform("waterWaveCounter", capi.Render.ShaderUniforms.WaterWaveCounter);
        color[0] = ((entity.RenderColor >> 16) & 0xFF) / 255f;
        color[1] = ((entity.RenderColor >> 8) & 0xFF) / 255f;
        color[2] = (entity.RenderColor & 0xFF) / 255f;
        color[3] = ((entity.RenderColor >> 24) & 0xFF) / 255f;
        capi.Render.CurrentActiveShader.Uniform("renderColor", color);
        double num = Math.Min(entity.WatchedAttributes.GetDouble("temporalStability", 1.0), capi.World.Player.Entity.WatchedAttributes.GetDouble("temporalStability", 1.0));
        double num2 = glitchAffected ? Math.Max(0.0, 1.0 - (2.5 * num)) : 0.0;
        capi.Render.CurrentActiveShader.Uniform("glitchEffectStrength", (float)num2);
      }

      capi.Render.CurrentActiveShader.UniformMatrices("elementTransforms", 35, entity.AnimManager.Animator.Matrices);

      if (meshRefOpaque != null) capi.Render.RenderMesh(meshRefOpaque);

      if (meshRefOit == null) return;

      capi.Render.RenderMesh(meshRefOit);
    }
  }
}