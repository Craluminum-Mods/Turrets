using System;
using System.Collections.Generic;
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
    public bool DefaultStatusState { get; set; }

    public ShapeEntityTurretRenderer(Entity entity, ICoreClientAPI api) : base(entity, api)
    {
      turret = entity as EntityTurret;

      DoRenderHeldItem = true;
      DefaultStatusState = turret.WatchedAttributes.GetBool("crturret-status");

      api.Event.RegisterGameTickListener(UpdateTurretInfo, 1000);
      api.Event.ReloadShapes += MarkShapeModified;
    }

    public override TextureAtlasPosition this[string textureCode]
      => extraTexturesByTextureName?.TryGetValue(textureCode, out var value) ?? false
      ? capi.EntityTextureAtlas.Positions[value.Baked.TextureSubId]
      : defaultTexSource[textureCode];

    public new event OnEntityShapeTesselationDelegate OnTesselation;

    public void UpdateTurretInfo(float dt)
    {
      if (turret.WatchedAttributes.GetBool("crturret-status") != DefaultStatusState)
      {
        MarkShapeModified();
        DefaultStatusState = turret.WatchedAttributes.GetBool("crturret-status");
      }
    }

    public override void OnEntityLoaded()
    {
      loaded = true;
      MarkShapeModified();
    }

    public override void TesselateShape()
    {
      if (!loaded)
      {
        return;
      }
      shapeFresh = true;
      CompositeShape compositeShape = OverrideCompositeShape ?? entity.Properties.Client.Shape;
      Shape entityShape = OverrideEntityShape ?? entity.Properties.Client.LoadedShapeForEntity;
      if (entityShape == null)
      {
        return;
      }
      OnTesselation?.Invoke(ref entityShape, compositeShape.ToString());
      entity.OnTesselation(ref entityShape, compositeShape.ToString());
      defaultTexSource = GetTextureSource();

      ApplyTurretPanelTextures(turret);
      if (!turret.WatchedAttributes.GetBool("crturret-broken"))
      {
        if (turret.WatchedAttributes.GetBool("crturret-status"))
        {
          ApplyTurretPanelTextures(turret);
        }
        else
        {
          IDictionary<string, CompositeTexture> textures = entity.Properties.Client.Textures;
          textures["color-black"] = textures["color-black"];
          textures["color-white"] = textures["color-white"];
          textures["iron"] = textures["iron"];
          textures["copper"] = textures["copper"];
          textures["status"] = textures["color-red"];
          defaultTexSource = GetTextureSource();
        }
      }
      else
      {
        IDictionary<string, CompositeTexture> textures2 = entity.Properties.Client.Textures;
        textures2["color-black"] = textures2["color-black"];
        textures2["color-white"] = textures2["color-white"];
        textures2["iron"] = textures2["iron"];
        textures2["copper"] = textures2["copper"];
        textures2["status"] = textures2["color-red"];
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

    protected void TurretTextureModified() => MarkShapeModified();

    public override void reloadSkin() { }

    public override void BeforeRender(float dt)
    {
      if (!shapeFresh)
      {
        TesselateShape();
      }
      if ((meshRefOpaque != null || meshRefOit != null) && !capi.IsGamePaused)
      {
        isSpectator = player?.WorldData.CurrentGameMode == EnumGameMode.Spectator;
        if (!isSpectator)
        {
          ApplyTurretPanelTextures(turret);
        }
      }
    }

    public override void DoRender3DOpaqueBatched(float dt, bool isShadowPass)
    {
      if (!isSpectator && (meshRefOpaque != null || meshRefOit != null))
      {
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
        if (meshRefOpaque != null)
        {
          capi.Render.RenderMesh(meshRefOpaque);
        }
        if (meshRefOit != null)
        {
          capi.Render.RenderMesh(meshRefOit);
        }
      }
    }

    private void ApplyTurretPanelTextures(EntityTurret turretIn)
    {
      IDictionary<string, CompositeTexture> textures = entity.Properties.Client.Textures;
      RunScreenlockTextureInfo(turretIn, textures);
      RunDisplayTextureSetup(textures);
      defaultTexSource = GetTextureSource();
    }

    private void RunDisplayTextureSetup(IDictionary<string, CompositeTexture> newTexturesIn)
    {
      newTexturesIn["color-black"] = newTexturesIn["color-black"];
      newTexturesIn["color-white"] = newTexturesIn["color-white"];
      newTexturesIn["iron"] = newTexturesIn["iron"];
      newTexturesIn["copper"] = newTexturesIn["copper"];
    }

    public static void RunScreenlockTextureInfo(EntityTurret turretIn, IDictionary<string, CompositeTexture> newTexturesIn)
    {
      newTexturesIn["status"] = !turretIn.WatchedAttributes.GetBool("crturret-status") ? newTexturesIn["color-red"] : newTexturesIn["color-green"];
    }
  }
}