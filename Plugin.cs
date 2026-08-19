using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using BepInEx;
using HarmonyLib;
using UnityEngine;

namespace BucketPourFurther
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class Plugin : BaseUnityPlugin
    {
        private const string PluginGuid = "com.DogEggz.BucketPourFurther";
        private const string PluginName = "BucketPourFurther";
        private const string PluginVersion = "1.0.0";

        private Harmony harmony;

        private void Awake()
        {
            harmony = new Harmony(PluginGuid);
            harmony.PatchAll(Assembly.GetExecutingAssembly());

            Logger.LogInfo(PluginName + " " + PluginVersion + " loaded.");
        }

        private void OnDestroy()
        {
            harmony?.UnpatchSelf();
        }
    }

    [HarmonyPatch(typeof(Mug), "Spill")]
    internal static class MugSpillPatch
    {
        private const float VanillaPourOffset = 0.3f;

        private static readonly FieldInfo BottleField =
            AccessTools.Field(typeof(Mug), "bottle");

        private static readonly MethodInfo VectorMultiplyMethod =
            AccessTools.Method(
                typeof(Vector3),
                "op_Multiply",
                new[] { typeof(Vector3), typeof(float) });

        private static readonly MethodInfo SelectPourOffsetMethod =
            AccessTools.Method(typeof(MugSpillPatch), nameof(SelectPourOffset));

        private static IEnumerable<CodeInstruction> Transpiler(
            IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> code = new List<CodeInstruction>(instructions);

            for (int i = 0; i < code.Count - 1; i++)
            {
                if (!LoadsVanillaPourOffset(code[i]) ||
                    !Equals(code[i + 1].operand, VectorMultiplyMethod))
                {
                    continue;
                }

                CodeInstruction original = code[i];
                CodeInstruction loadMug = new CodeInstruction(OpCodes.Ldarg_0);
                loadMug.labels.AddRange(original.labels);
                loadMug.blocks.AddRange(original.blocks);

                code[i] = loadMug;
                code.Insert(
                    i + 1,
                    new CodeInstruction(OpCodes.Call, SelectPourOffsetMethod));

                return code;
            }

            Debug.LogError(
                "[BucketPourFurther] Could not find the vanilla Mug.Spill pour offset.");
            return code;
        }

        private static float SelectPourOffset(Mug mug)
        {
            return IsSeaWaterBucket(mug) ? 0.6f : VanillaPourOffset;
        }

        private static bool IsSeaWaterBucket(Mug mug)
        {
            ShipItemBottle bottle = mug == null || BottleField == null
                ? null
                : BottleField.GetValue(mug) as ShipItemBottle;

            return bottle != null &&
                   bottle.GetCapacity() == 9f &&
                   bottle.amount == (float)LiquidType.seaWater;
        }

        private static bool LoadsVanillaPourOffset(CodeInstruction instruction)
        {
            return instruction.opcode == OpCodes.Ldc_R4 &&
                   instruction.operand is float value &&
                   value == VanillaPourOffset;
        }
    }
}
