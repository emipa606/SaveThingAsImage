using System;
using System.IO;
using System.Linq;
using HarmonyLib;
using LudeonTK;
using RimWorld;
using UnityEngine;
using Verse;

namespace SpawnModContent;

public static class DebugSpawning
{
    private static string savePath;

    [DebugAction("Spawning", "Save Thing as image", actionType = DebugActionType.ToolMap,
        allowedGameStates = AllowedGameStates.PlayingOnMap)]
    private static void save()
    {
        savePath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        if (savePath.NullOrEmpty())
        {
            savePath = GenFilePaths.SaveDataFolderPath;
        }

        foreach (var thing in Find.CurrentMap.thingGrid.ThingsAt(UI.MouseCell()).ToList())
        {
            saveThing(thing);
        }
    }

    private static void saveThing(Thing thing)
    {
        string saveTo;
        Mesh bodyMesh;
        Vector3 meshSize;
        switch (thing)
        {
            case Pawn pawn:
                saveTo = $"{Path.Combine(savePath, pawn.NameShortColored)}_{thing.Rotation.ToStringHuman()}.png";

                if (pawn.def.thingClass.Name.Contains("Vehicle"))
                {
                    var vehiclePawnType = AccessTools.TypeByName("VehiclePawn");
                    var vehicleGraphicProperty = AccessTools.Property(vehiclePawnType, "VehicleGraphic");
                    var vehicleGraphic = vehicleGraphicProperty.GetValue(pawn);
                    var graphicRGBType = AccessTools.TypeByName("Graphic_RGB");
                    var meshAtMethod = AccessTools.Method(graphicRGBType, "MeshAt", [typeof(Rot4)]);
                    bodyMesh = (Mesh)meshAtMethod.Invoke(vehicleGraphic, [thing.Rotation]);
                }
                else

                {
                    bodyMesh = pawn.RaceProps.Humanlike
                        ? HumanlikeMeshPoolUtility.GetHumanlikeBodySetForPawn(pawn).MeshAt(pawn.Rotation)
                        : pawn.Drawer.renderer.BodyGraphic.MeshAt(pawn.Rotation);
                }

                meshSize = bodyMesh.bounds.size;
                break;
            case Corpse corpse:
                saveTo =
                    $"{Path.Combine(savePath, corpse.InnerPawn.NameShortColored)}_{thing.Rotation.ToStringHuman()}.png";
                bodyMesh = corpse.InnerPawn.RaceProps.Humanlike
                    ? HumanlikeMeshPoolUtility.GetHumanlikeBodySetForPawn(corpse.InnerPawn)
                        .MeshAt(corpse.InnerPawn.Rotation)
                    : corpse.InnerPawn.Drawer.renderer.BodyGraphic.MeshAt(corpse.InnerPawn.Rotation);

                meshSize = bodyMesh.bounds.size;
                break;
            default:
            {
                if (thing.Graphic.MatSingle.mainTexture == BaseContent.BadTex)
                {
                    Messages.Message($"Found no texture for {thing.def.defName}", MessageTypeDefOf.NegativeEvent,
                        false);
                    return;
                }

                // Check if this is a modular weapon (Modular Weapons 2 support)
                if (hasModularWeaponComponent(thing))
                {
                    Log.Message($"[SaveThingAsImage] Saving modular weapon: {thing.def.defName}");
                }

                saveTo = $"{Path.Combine(savePath, thing.LabelShort)}.png";
                if (thing.Graphic.data?.graphicClass != null)
                {
                    if (thing.Graphic.data.graphicClass == typeof(Graphic_Multi))
                    {
                        saveTo = $"{Path.Combine(savePath, thing.LabelShort)}_{thing.Rotation.ToStringHuman()}.png";
                    }

                    if (thing.def.stackLimit > 1)
                    {
                        if (thing.stackCount == 1)
                        {
                            saveTo = $"{Path.Combine(savePath, thing.LabelShort)}.png";
                        }
                        else if (thing.stackCount == thing.def.stackLimit)
                        {
                            saveTo = $"{Path.Combine(savePath, thing.LabelShort)}_full_stack.png";
                        }
                        else
                        {
                            saveTo = $"{Path.Combine(savePath, thing.LabelShort)}_stack.png";
                        }
                    }

                    if (thing.Graphic.data.graphicClass == typeof(Graphic_Random))
                    {
                        var num = thing.overrideGraphicIndex ?? thing.thingIDNumber;
                        var graphicObject = (Graphic_Random)thing.Graphic;
                        var graphicNumber = num % graphicObject.SubGraphicsCount;
                        saveTo = $"{Path.Combine(savePath, thing.LabelShort)}_{graphicNumber}.png";
                    }
                }

                meshSize = thing.Graphic.MeshAt(thing.Rotation).bounds.size;

                break;
            }
        }

        var thingSize = new Vector2(meshSize.x * 256, meshSize.z * 256);
        var worldSizeVec = new Vector2(meshSize.x, meshSize.z);

        // For modular weapons, use the actual rendered texture dimensions
        if (hasModularWeaponComponent(thing))
        {
            var texDim = getModularWeaponTextureDimensions(thing);
            if (texDim != Vector2.zero)
            {
                thingSize = texDim;
                worldSizeVec = texDim / 256f; // Calculate world size from texture dimensions
            }
        }

        var texture = getThingTexture(thing, thingSize, worldSizeVec);


        var thingTextureAsPng = texture.EncodeToPNG();
        File.WriteAllBytes(saveTo, thingTextureAsPng);
        Messages.Message($"{thing.def.defName} saved to {Path.GetFullPath(saveTo)}", MessageTypeDefOf.TaskCompletion,
            false);
    }

    private static Texture2D getThingTexture(Thing thing, Vector2 size, Vector2 worldSize)
    {
        var renderTexture =
            RenderTexture.GetTemporary(
                (int)size.x,
                (int)size.y,
                24,
                RenderTextureFormat.Default,
                RenderTextureReadWrite.Linear);

        var previous = RenderTexture.active;
        RenderTexture.active = renderTexture;
        GL.Clear(true, true, Color.clear);

        Texture texture;
        if (thing is Pawn or Corpse)
        {
            if (thing is not Pawn pawn)
            {
                pawn = ((Corpse)thing).InnerPawn;
            }

            var zoomLevel = 1f;
            texture = PortraitsCache.Get(pawn, size, thing.Rotation);
            var regen = textureToBig(texture);
            while (regen)
            {
                zoomLevel *= 0.95f;
                texture = PortraitsCache.Get(pawn, size, thing.Rotation, default, zoomLevel);
                regen = textureToBig(texture);
                Log.Message($"Texture too big for {pawn}, recreating");
            }

            Graphics.Blit(texture, renderTexture);
        }
        else
        {
            // Check if this is a modular weapon and get its custom material
            var modularMaterial = hasModularWeaponComponent(thing) ? getModularWeaponMaterial(thing) : null;
            var isModularWeapon = false;

            if (modularMaterial != null)
            {
                // Use the modular weapon's rendered material
                texture = modularMaterial.mainTexture;
                isModularWeapon = true;
            }
            else
            {
                // Use the standard rendering for non-modular weapons
                texture = thing.Graphic.ExtractInnerGraphicFor(thing).MatAt(thing.Rotation).mainTexture;
                if (thing.Graphic is Graphic_StackCount graphic_StackCount)
                {
                    texture = graphic_StackCount.SubGraphicForStackCount(thing.stackCount, thing.def)
                        .MatSingleFor(thing)
                        .mainTexture;
                }
            }

            // For modular weapons, blit directly without material scaling
            // For other items, use the material for proper rendering
            if (isModularWeapon)
            {
                Graphics.Blit(texture, renderTexture);
            }
            else
            {
                Graphics.Blit(texture, renderTexture, thing.Graphic.MatAt(thing.Rotation, thing));
            }
        }

        var image = new Texture2D(renderTexture.width, renderTexture.height);
        image.ReadPixels(new Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0);
        image.Apply();

        if (thing is Building_Turret turret)
        {
            drawTurretTop(turret, image, worldSize);
            image.Apply();
        }

        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(renderTexture);
        return image;
    }

    private static void drawTurretTop(Building_Turret turret, Texture2D image, Vector2 worldSize)
    {
        if (turret.TurretTopMaterial?.mainTexture == null || turret.TurretTopMaterial.mainTexture == BaseContent.BadTex)
        {
            return;
        }

        var topImage = toReadableTexture(turret.TurretTopMaterial.mainTexture);
        var topOffset = turret.def.building.turretTopOffset;
        var topDrawSize = turret.def.building.turretTopDrawSize;

        var turretRotation = turret.Rotation.AsAngle;
        if (turret is Building_TurretGun turretGun)
        {
            turretRotation = turretGun.Top.CurRotation;
        }

        var angle = (turretRotation + TurretTop.ArtworkRotation) * Mathf.Deg2Rad;
        var cos = Mathf.Cos(angle);
        var sin = Mathf.Sin(angle);

        var centerX = image.width * 0.5f;
        var centerY = image.height * 0.5f;

        var offsetX = worldSize.x == 0f ? 0f : topOffset.x / worldSize.x * image.width;
        var offsetY = worldSize.y == 0f ? 0f : topOffset.y / worldSize.y * image.height;

        var topWidth = worldSize.x == 0f ? image.width : topDrawSize / worldSize.x * image.width;
        var topHeight = worldSize.y == 0f ? image.height : topDrawSize / worldSize.y * image.height;

        var pivotX = centerX + offsetX;
        var pivotY = centerY - offsetY;

        var halfW = topWidth * 0.5f;
        var halfH = topHeight * 0.5f;

        var minX = Mathf.Max(0, Mathf.FloorToInt(pivotX - Mathf.Max(halfW, halfH)) - 2);
        var maxX = Mathf.Min(image.width - 1, Mathf.CeilToInt(pivotX + Mathf.Max(halfW, halfH)) + 2);
        var minY = Mathf.Max(0, Mathf.FloorToInt(pivotY - Mathf.Max(halfW, halfH)) - 2);
        var maxY = Mathf.Min(image.height - 1, Mathf.CeilToInt(pivotY + Mathf.Max(halfW, halfH)) + 2);

        var tint = turret.TurretTopMaterial.color;

        for (var y = minY; y <= maxY; y++)
        {
            for (var x = minX; x <= maxX; x++)
            {
                var localX = x + 0.5f - pivotX;
                var localY = y + 0.5f - pivotY;

                var srcX = (localX * cos) - (localY * sin);
                var srcY = (localX * sin) + (localY * cos);

                var u = (srcX / topWidth) + 0.5f;
                var v = (srcY / topHeight) + 0.5f;
                if (u < 0f || u > 1f || v < 0f || v > 1f)
                {
                    continue;
                }

                var src = topImage.GetPixelBilinear(u, v);
                src *= tint;
                if (src.a <= 0f)
                {
                    continue;
                }

                var dst = image.GetPixel(x, y);
                var outA = src.a + (dst.a * (1f - src.a));
                if (outA <= 0f)
                {
                    continue;
                }

                var outRgb = ((src * src.a) + (dst * dst.a * (1f - src.a))) / outA;
                outRgb.a = outA;
                image.SetPixel(x, y, outRgb);
            }
        }
    }

    private static Texture2D toReadableTexture(Texture texture)
    {
        var renderTexture = RenderTexture.GetTemporary(
            texture.width,
            texture.height,
            0,
            RenderTextureFormat.Default,
            RenderTextureReadWrite.Linear);

        Graphics.Blit(texture, renderTexture);
        var previous = RenderTexture.active;
        RenderTexture.active = renderTexture;

        var readable = new Texture2D(texture.width, texture.height);
        readable.ReadPixels(new Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0);
        readable.Apply();

        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(renderTexture);
        return readable;
    }

    private static bool textureToBig(Texture texture)
    {
        var renderTexture = RenderTexture.GetTemporary(
            texture.width,
            texture.height,
            0,
            RenderTextureFormat.Default,
            RenderTextureReadWrite.Linear);
        Graphics.Blit(texture, renderTexture);
        var previous = RenderTexture.active;
        RenderTexture.active = renderTexture;
        var icon = new Texture2D(texture.width, texture.height);
        icon.ReadPixels(new Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0);
        icon.Apply();
        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(renderTexture);

        var y = 0;
        int x;
        for (x = 0; x < icon.width; x++)
        {
            if (icon.GetPixel(x, y).a > 0)
            {
                return true;
            }
        }

        y = icon.height - 1;
        for (x = 0; x < icon.width; x++)
        {
            if (icon.GetPixel(x, y).a > 0)
            {
                return true;
            }
        }

        x = 0;
        for (y = 1; y < icon.height - 1; y++)
        {
            if (icon.GetPixel(x, y).a > 0)
            {
                return true;
            }
        }


        x = icon.width - 1;
        for (y = 1; y < icon.height - 1; y++)
        {
            if (icon.GetPixel(x, y).a > 0)
            {
                return true;
            }
        }

        return false;
    }

    private static bool hasModularWeaponComponent(Thing thing)
    {
        try
        {
            var compType = AccessTools.TypeByName("ModularWeapons2.CompModularWeapon");
            if (compType == null)
            {
                return false;
            }

            if (thing is not ThingWithComps thingWithComps)
            {
                return false;
            }

            // Use reflection to call GetComp<CompModularWeapon>()
            var getCompMethod = typeof(ThingWithComps).GetMethod("GetComp");
            if (getCompMethod == null)
            {
                return false;
            }

            var result = getCompMethod.MakeGenericMethod(compType).Invoke(thingWithComps, null);
            return result != null;
        }
        catch
        {
            return false;
        }
    }

    private static Material getModularWeaponMaterial(Thing thing)
    {
        try
        {
            var compType = AccessTools.TypeByName("ModularWeapons2.CompModularWeapon");
            if (compType == null)
            {
                return null;
            }

            if (thing is not ThingWithComps thingWithComps)
            {
                return null;
            }

            // Get the CompModularWeapon component
            var getCompMethod = typeof(ThingWithComps).GetMethod("GetComp");
            if (getCompMethod == null)
            {
                return null;
            }

            var comp = getCompMethod.MakeGenericMethod(compType).Invoke(thingWithComps, null);
            if (comp == null)
            {
                return null;
            }

            // Call GetMaterial() on the component
            var getMaterialMethod = AccessTools.Method(compType, "GetMaterial");
            if (getMaterialMethod == null)
            {
                return null;
            }

            var material = getMaterialMethod.Invoke(comp, null);
            return material as Material;
        }
        catch
        {
            return null;
        }
    }

    private static Vector2 getModularWeaponTextureDimensions(Thing thing)
    {
        try
        {
            var modularMaterial = getModularWeaponMaterial(thing);
            if (modularMaterial == null || modularMaterial.mainTexture == null)
            {
                return Vector2.zero;
            }

            var texture = modularMaterial.mainTexture;
            return new Vector2(texture.width, texture.height);
        }
        catch
        {
            return Vector2.zero;
        }
    }
}