using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using HarmonyLib;
using Verse;
using RimWorld;
using UnityEngine;
using Verse.AI;

namespace Tent
{
    [StaticConstructorOnStartup]
    public class MainHarmonyInstance : Mod
    {
        public MainHarmonyInstance(ModContentPack content) : base(content)
        {
            var harmony = new Harmony("com.otters.rimworld.mod.Tents");
            harmony.PatchAll(Assembly.GetExecutingAssembly());

            CompatibilityPatches.ExecuteCompatibilityPatches(harmony);
        }
    }
    [HarmonyPatch(typeof(PawnRenderer), nameof(PawnRenderer.RenderCache), new[] { typeof(Rot4), typeof(float), typeof(Vector3), typeof(bool), typeof(bool), typeof(bool), typeof(bool), typeof(IReadOnlyDictionary<Apparel, Color>), typeof(Color?), typeof(bool)})]
    public class PawnRenderer_RenderCache
    {
        public static bool Prefix(Pawn ___pawn)
        {
            if (___pawn?.Map == null || ___pawn?.RaceProps?.Humanlike != true) return true;
            return !(___pawn?.CurrentBed()?.def?.HasModExtension<TentModExtension>() == true);
        }
    }
    [HarmonyPatch(typeof(PawnRenderer), nameof(PawnRenderer.RenderPawnAt), new[] { typeof(Vector3), typeof(Rot4?), typeof(bool)})]
    public class PawnRenderer_RenderPawnAt
    {
        public static bool Prefix(Pawn ___pawn)
        {
            if (___pawn?.Map == null || ___pawn?.RaceProps?.Humanlike != true) return true;
            return !(___pawn?.CurrentBed()?.def?.HasModExtension<TentModExtension>() == true);
        }
    }

    [HarmonyPatch(typeof(Pawn_MindState), "MindStateTick")]
    public class Pawn_MindState_MindStateTick
    {
        public static void Postfix(Pawn_MindState __instance)
        {
            if (Find.TickManager.TicksGame % 123 == 0 && __instance.pawn.Spawned && __instance.pawn.RaceProps.IsFlesh && __instance.pawn.needs.mood != null)
            {
                var currBed = __instance.pawn.CurrentBed();
                if (currBed == null) return;
                var modExt = currBed.def.GetModExtension<TentModExtension>();
                if (modExt == null || !modExt.negateWater) return;

                WeatherDef curWeatherLerped = __instance.pawn.Map.weatherManager.CurWeatherLerped;
                if (curWeatherLerped.weatherThought != null && curWeatherLerped.weatherThought == ThoughtDef.Named("SoakingWet") && !__instance.pawn.Position.Roofed(__instance.pawn.Map))
                {
                    __instance.pawn.needs.mood.thoughts.memories.RemoveMemoriesOfDef(curWeatherLerped.weatherThought);
                }
            }
        }
    }
    [HarmonyPatch(typeof(BedInteractionCellSearchPattern),nameof(BedInteractionCellSearchPattern.BedCellOffsets))]
    public class Patch_BedInteractionCellSearchPattern
    {
        public static bool Prefix(BedInteractionCellSearchPattern __instance, List<IntVec3> offsets, IntVec2 size)
        {

            if (size.z > 2)
            {
                offsets.Add(IntVec3.West);
                offsets.Add(IntVec3.East);
                offsets.Add(IntVec3.South);
                offsets.Add(IntVec3.North);
                offsets.Add(IntVec3.South + IntVec3.West);
                offsets.Add(IntVec3.South + IntVec3.East);
                offsets.Add(IntVec3.North + IntVec3.West);
                offsets.Add(IntVec3.North + IntVec3.East);
                offsets.Add(IntVec3.Zero);
                return false;
            }
            return true;

        }
    }

    [HarmonyPatch(typeof(Toils_LayDown), "ApplyBedThoughts", new Type[] { typeof(Pawn), typeof(Building_Bed) })]
    public class Toils_LayDown_ApplyBedThoughts
    {
        public static void Postfix(Pawn actor)
        {
            Building_Bed building_Bed = actor.CurrentBed();
            if (building_Bed == null) return;
            var modExt = building_Bed.def.GetModExtension<TentModExtension>();
            if (modExt == null) return;

            var effect = ModSettings.effects.FirstOrDefault(x => x?.tentDefName == building_Bed.def.defName);
            if (effect == null) return;
            if (effect.negateSleptOutside) actor.needs.mood.thoughts.memories.RemoveMemoriesOfDef(ThoughtDefOf.SleptOutside);
            if (effect.negateSleptInCold) actor.needs.mood.thoughts.memories.RemoveMemoriesOfDef(ThoughtDefOf.SleptInCold);
            if (effect.negateSleptInHeat) actor.needs.mood.thoughts.memories.RemoveMemoriesOfDef(ThoughtDefOf.SleptInHeat);
            if (effect.negateSleptInBarracks) actor.needs.mood.thoughts.memories.RemoveMemoriesOfDef(ThoughtDefOf.SleptInBarracks);
        }
    }

    [HarmonyPatch(typeof(CompAssignableToPawn_Bed), "IdeoligionForbids")]
    public class CompAssignableToPawn_Bed_IdeoligionForbids
    {
        public static void Postfix(CompAssignableToPawn_Bed __instance, ref bool __result, Pawn pawn)
        {
            if (__instance?.parent == null) return;
            var modExt = __instance.parent.def.GetModExtension<TentModExtension>();
            if (modExt == null) return;

            var effect = ModSettings.effects.FirstOrDefault(x => x?.tentDefName == __instance.parent.def.defName);
            if (effect == null) return;
            if (effect.ideologyTentAssignmentAllowed) __result = false;
        }
    }
}   
