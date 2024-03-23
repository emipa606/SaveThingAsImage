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
    private static string SavePath;

    [DebugAction("Spawning", "Save Thing as image", actionType = DebugActionType.ToolMap,
        allowedGameStates = AllowedGameStates.PlayingOnMap)]
    private static void Save()
    {
        SavePath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        if (SavePath.NullOrEmpty())
        {
            SavePath = GenFilePaths.SaveDataFolderPath;
        }

        foreach (var thing in Find.CurrentMap.thingGrid.ThingsAt(UI.MouseCell()).ToList())
        {
            SaveThing(thing);
        }
    }

    private static void SaveThing(Thing thing)
    {
        string saveTo;
        Mesh bodyMesh;
        Vector3 meshSize;
        switch (thing)
        {
            case Pawn pawn:
                saveTo = $"{Path.Combine(SavePath, pawn.NameShortColored)}_{thing.Rotation.ToStringHuman()}.png";

                if (pawn.def.thingClass.Name.Contains("Vehicle"))
                {
                    var VehiclePawnType = AccessTools.TypeByName("VehiclePawn");
                    var VehicleGraphicProperty = AccessTools.Property(VehiclePawnType, "VehicleGraphic");
                    var VehicleGraphic = VehicleGraphicProperty.GetValue(pawn);
                    var Graphic_RGBType = AccessTools.TypeByName("Graphic_RGB");
                    var MeshAtMethod = AccessTools.Method(Graphic_RGBType, "MeshAt", [typeof(Rot4)]);
                    bodyMesh = (Mesh)MeshAtMethod.Invoke(VehicleGraphic, [thing.Rotation]);
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
                    $"{Path.Combine(SavePath, corpse.InnerPawn.NameShortColored)}_{thing.Rotation.ToStringHuman()}.png";
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

                saveTo = $"{Path.Combine(SavePath, thing.LabelShort)}.png";
                if (thing.Graphic.data?.graphicClass != null)
                {
                    if (thing.Graphic.data.graphicClass == typeof(Graphic_Multi))
                    {
                        saveTo = $"{Path.Combine(SavePath, thing.LabelShort)}_{thing.Rotation.ToStringHuman()}.png";
                    }

                    if (thing.def.stackLimit > 1)
                    {
                        if (thing.stackCount == 1)
                        {
                            saveTo = $"{Path.Combine(SavePath, thing.LabelShort)}.png";
                        }
                        else if (thing.stackCount == thing.def.stackLimit)
                        {
                            saveTo = $"{Path.Combine(SavePath, thing.LabelShort)}_full_stack.png";
                        }
                        else
                        {
                            saveTo = $"{Path.Combine(SavePath, thing.LabelShort)}_stack.png";
                        }
                    }

                    if (thing.Graphic.data.graphicClass == typeof(Graphic_Random))
                    {
                        var num = thing.overrideGraphicIndex ?? thing.thingIDNumber;
                        var graphicObject = (Graphic_Random)thing.Graphic;
                        var graphicNumber = num % graphicObject.SubGraphicsCount;
                        saveTo = $"{Path.Combine(SavePath, thing.LabelShort)}_{graphicNumber}.png";
                    }
                }

                meshSize = thing.Graphic.MeshAt(thing.Rotation).bounds.size;

                break;
            }
        }

        var thingSize = new Vector2(meshSize.x * 256, meshSize.z * 256);
        var texture = GetThingTexture(thing, thingSize);


        var thingTextureAsPng = texture.EncodeToPNG();
        File.WriteAllBytes(saveTo, thingTextureAsPng);
        Messages.Message($"{thing.def.defName} saved to {Path.GetFullPath(saveTo)}", MessageTypeDefOf.TaskCompletion,
            false);
    }

    private static Texture2D GetThingTexture(Thing thing, Vector2 size)
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
            var regen = TextureToBig(texture);
            while (regen)
            {
                zoomLevel *= 0.95f;
                texture = PortraitsCache.Get(pawn, size, thing.Rotation, default, zoomLevel);
                regen = TextureToBig(texture);
                Log.Message($"Texture too big for {pawn}, recreating");
            }

            Graphics.Blit(texture, renderTexture);
        }
        else
        {
            texture = thing.Graphic.ExtractInnerGraphicFor(thing).MatAt(thing.Rotation).mainTexture;
            if (thing.Graphic is Graphic_StackCount graphic_StackCount)
            {
                texture = graphic_StackCount.SubGraphicForStackCount(thing.stackCount, thing.def).MatSingleFor(thing)
                    .mainTexture;
            }

            Graphics.Blit(texture, renderTexture, thing.Graphic.MatAt(thing.Rotation, thing));
        }

        var image = new Texture2D(renderTexture.width, renderTexture.height);
        image.ReadPixels(new Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0);
        image.Apply();
        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(renderTexture);
        return image;
    }

    private static bool TextureToBig(Texture texture)
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
}