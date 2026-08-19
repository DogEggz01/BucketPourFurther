using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;

namespace BucketPourFurther
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class BucketPourFurtherPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.DogEggz.BucketPourFurther";
        public const string PluginName = "BucketPourFurther";
        public const string PluginVersion = "0.1.0";

        internal const float VanillaBucketUpDistance = 0.3f;
        internal const float DefaultBucketUpDistance = 0.9f;

        internal static BucketPourFurtherPlugin Instance { get; private set; }

        private Harmony harmony;
        private ConfigEntry<float> bucketUpDistance;

        internal float BucketUpDistance =>
            bucketUpDistance?.Value ?? DefaultBucketUpDistance;

        private void Awake()
        {
            Instance = this;

            // Temporary tuning control. Remove this setting when version 1.0.0
            // locks in the final bucket-up distance.
            bucketUpDistance = Config.Bind(
                "Testing",
                "Bucket Up Distance",
                DefaultBucketUpDistance,
                new ConfigDescription(
                    "Temporary Mug.Spill ray-origin distance for capacity-9 buckets. " +
                    "This testing slider will be removed in version 1.0.0.",
                    new AcceptableValueRange<float>(0.3f, 1.2f)));

            harmony = new Harmony(PluginGuid);
            harmony.PatchAll(typeof(BucketPourFurtherPlugin).Assembly);
            Logger.LogInfo(
                $"{PluginName} loaded: bucket-up distance is {BucketUpDistance:F2}.");
        }

        private void OnDestroy()
        {
            harmony?.UnpatchSelf();

            if (Instance == this)
                Instance = null;
        }

        internal static float GetSpillOriginDistance(ShipItemBottle bottle)
        {
            if (bottle == null || bottle.GetCapacity() != 9f)
                return VanillaBucketUpDistance;

            return Instance?.BucketUpDistance ?? DefaultBucketUpDistance;
        }
    }

    [HarmonyPatch(typeof(Mug), "Spill")]
    internal static class MugSpillOriginPatch
    {
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler(
            IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> code = new List<CodeInstruction>(instructions);
            FieldInfo bottleField = AccessTools.Field(typeof(Mug), "bottle");
            MethodInfo distanceGetter = AccessTools.Method(
                typeof(BucketPourFurtherPlugin),
                nameof(BucketPourFurtherPlugin.GetSpillOriginDistance));
            int replacements = 0;

            if (bottleField == null || distanceGetter == null)
            {
                throw new MissingMemberException(
                    "Could not resolve Mug.bottle or GetSpillOriginDistance().");
            }

            for (int index = 0; index < code.Count; index++)
            {
                CodeInstruction instruction = code[index];

                if (instruction.opcode == OpCodes.Ldc_R4 &&
                    instruction.operand is float value &&
                    value == 0.3f)
                {
                    // Preserve the vanilla stack shape: replace the fixed float
                    // with a runtime value selected from this Mug's bottle.
                    instruction.opcode = OpCodes.Ldarg_0;
                    instruction.operand = null;
                    code.Insert(index + 1, new CodeInstruction(OpCodes.Ldfld, bottleField));
                    code.Insert(index + 2, new CodeInstruction(OpCodes.Call, distanceGetter));

                    replacements++;
                    index += 2;
                }
            }

            if (replacements != 1)
            {
                throw new InvalidOperationException(
                    $"Expected one 0.3f constant in Mug.Spill, found {replacements}.");
            }

            return code;
        }
    }
}
