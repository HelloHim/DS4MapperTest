using System;

namespace DS4MapperTest.GyroActions
{
    public enum TriggerSensitivityModifierTrigger { Left, Right }
    public enum TriggerSensitivityModifierBehaviour { DecreaseWithPull, IncreaseWithPull }
    public enum TriggerSensitivityModifierConfigureUsing { AbsoluteSensitivity, Multiplier }
    public enum TriggerSensitivityModifierResponseCurve { Linear, Quadratic, Cubic }

    public struct TriggerSensitivityModifierSettings
    {
        public const bool ENABLED_DEFAULT = false;
        public const double MULTIPLIER_DEFAULT = 0.5;
        public bool enabled;
        public TriggerSensitivityModifierTrigger trigger;
        public TriggerSensitivityModifierBehaviour behaviour;
        public TriggerSensitivityModifierConfigureUsing configureUsing;
        public TriggerSensitivityModifierResponseCurve responseCurve;
        public double targetSensitivity;
        public double multiplier;
        public bool modifyVerticalSensitivity;
        public TriggerSensitivityModifierSettings(double baseSensitivity)
        {
            enabled = ENABLED_DEFAULT;
            trigger = TriggerSensitivityModifierTrigger.Left;
            behaviour = TriggerSensitivityModifierBehaviour.DecreaseWithPull;
            configureUsing = TriggerSensitivityModifierConfigureUsing.AbsoluteSensitivity;
            responseCurve = TriggerSensitivityModifierResponseCurve.Linear;
            targetSensitivity = baseSensitivity * MULTIPLIER_DEFAULT;
            multiplier = MULTIPLIER_DEFAULT;
            modifyVerticalSensitivity = false;
        }
    }

    public static class TriggerSensitivityModifier
    {
        public static double ResolveTarget(in TriggerSensitivityModifierSettings settings, double baseSensitivity) =>
            settings.configureUsing == TriggerSensitivityModifierConfigureUsing.Multiplier
                ? baseSensitivity * settings.multiplier : settings.targetSensitivity;

        public static bool IsValid(in TriggerSensitivityModifierSettings settings, double baseSensitivity) =>
            settings.behaviour == TriggerSensitivityModifierBehaviour.DecreaseWithPull
                ? ResolveTarget(settings, baseSensitivity) <= baseSensitivity
                : ResolveTarget(settings, baseSensitivity) >= baseSensitivity;

        public static double Evaluate(in TriggerSensitivityModifierSettings settings,
            double baseSensitivity, double triggerPosition)
        {
            if (!settings.enabled) return baseSensitivity;
            double target = ResolveTarget(settings, baseSensitivity);
            target = settings.behaviour == TriggerSensitivityModifierBehaviour.DecreaseWithPull
                ? Math.Min(target, baseSensitivity) : Math.Max(target, baseSensitivity);
            double progress = Math.Clamp(triggerPosition, 0.0, 1.0);
            progress = settings.responseCurve switch
            {
                TriggerSensitivityModifierResponseCurve.Quadratic => progress * progress,
                TriggerSensitivityModifierResponseCurve.Cubic => progress * progress * progress,
                _ => progress,
            };
            return baseSensitivity + (target - baseSensitivity) * progress;
        }
    }
}
