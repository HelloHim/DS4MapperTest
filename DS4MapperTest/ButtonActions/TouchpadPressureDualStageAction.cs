using System;
using DS4MapperTest.ActionUtil;
using DS4MapperTest.MapperUtil;
using DS4MapperTest.TriggerActions;
using static DS4MapperTest.TriggerActions.TriggerDualStageAction;

namespace DS4MapperTest.ButtonActions
{
    // Drives Soft Press / Full Press for a pressure-sensitive touchpad click (Steam
    // Controller 2 / "Triton"). Reuses the exact trigger dual-stage state machine
    // (DualStageEvaluator, extracted from TriggerDualStageAction) so Threshold/Exclusive
    // Buttons/Hair Trigger/Hip Fire/Hip Fire Exclusive Buttons behave identically to the
    // trigger settings users already know. Unlike triggers, the per-frame driver is raw
    // touchpad pressure (0-32767) plus a finger touch flag rather than an analogue axis with
    // an optional dedicated click button, so PrepareTouchpadPressure (not Prepare/PrepareAnalog)
    // is the real entry point for whichever mapper drives pressure-sensitive touchpad clicks.
    public class TouchpadPressureDualStageAction : ButtonMapAction
    {
        public class PropertyKeyStrings
        {
            public const string NAME = "Name";
            public const string ACTIVATION_STYLE = "ActivationStyle";
            public const string SOFT_THRESHOLD = "SoftPressThreshold";
            public const string FULL_THRESHOLD = "FullPressThreshold";
            public const string HIPFIRE_DELAY = "HipFireDelay";
            public const string FORCE_HIP_FIRE_TIME = "ForceHipFireTime";
            public const string SOFTPRESS_BUTTON = "SoftPressButton";
            public const string FULLPRESS_BUTTON = "FullPressButton";
        }

        public const string ACTION_TYPE_NAME = "TouchpadPressureDualStageAction";

        public const int MAX_PRESSURE = 32767;
        public const int DEFAULT_SOFT_THRESHOLD = 4096;
        public const int DEFAULT_FULL_THRESHOLD = 17096;
        public const int DEFAULT_HIPFIRE_DELAY_MS = 100;

        // Schmitt-trigger nudge applied to the *effective* threshold once a stage is
        // already active, so pressure readings hovering right at the configured value
        // don't chatter. Small enough not to noticeably shift the configured threshold.
        private const int HYSTERESIS = 400;

        private DualStageMode activationStyle = DualStageMode.Threshold;
        public DualStageMode ActivationStyle
        {
            get => activationStyle;
            set => activationStyle = value;
        }

        private int softPressThreshold = DEFAULT_SOFT_THRESHOLD;
        public int SoftPressThreshold
        {
            get => softPressThreshold;
            set
            {
                int clamped = Math.Clamp(value, 0, MAX_PRESSURE);
                softPressThreshold = clamped;
                if (softPressThreshold >= fullPressThreshold)
                {
                    fullPressThreshold = Math.Min(MAX_PRESSURE, softPressThreshold + 1);
                }
            }
        }

        private int fullPressThreshold = DEFAULT_FULL_THRESHOLD;
        public int FullPressThreshold
        {
            get => fullPressThreshold;
            set
            {
                int clamped = Math.Clamp(value, 0, MAX_PRESSURE);
                fullPressThreshold = clamped;
                if (fullPressThreshold <= softPressThreshold)
                {
                    softPressThreshold = Math.Max(0, fullPressThreshold - 1);
                }
            }
        }

        private int hipFireMs = DEFAULT_HIPFIRE_DELAY_MS;
        public int HipFireDelayMs
        {
            get => hipFireMs;
            set => hipFireMs = value;
        }

        private bool forceHipFireDelay;
        public bool ForceHipFireDelay
        {
            get => forceHipFireDelay;
            set => forceHipFireDelay = value;
        }

        private AxisDirButton softPressActButton = new AxisDirButton();
        public AxisDirButton SoftPressActButton
        {
            get => softPressActButton;
            set => softPressActButton = value;
        }

        private AxisDirButton fullPressActButton = new AxisDirButton();
        public AxisDirButton FullPressActButton
        {
            get => fullPressActButton;
            set => fullPressActButton = value;
        }

        private double axisNorm;
        public override double ButtonDistance => axisNorm;
        public override double AxisUnit => axisNorm;

        private DualStageEvaluatorState stageState = new DualStageEvaluatorState();
        public bool softPressActActive;
        public bool fullPressActActive;
        public ActiveZoneButtons currentActiveButtons = ActiveZoneButtons.None;
        public ActiveZoneButtons previousActiveButtons = ActiveZoneButtons.None;

        public TouchpadPressureDualStageAction()
        {
            actionTypeName = ACTION_TYPE_NAME;
        }

        // Real per-frame entry point. Called directly by SteamControllerTritonMapper with
        // the raw left/right pad pressure (0-32767) and finger touch state for that pad.
        public void PrepareTouchpadPressure(Mapper mapper, int pressure, bool touched,
            bool alterState = true)
        {
            int clamped = Math.Clamp(pressure, 0, MAX_PRESSURE);

            // Touch gating: a lifted finger is an unconditional, immediate release -
            // stale pressure must never keep a stage active, and any in-flight Hip Fire
            // timer state is dropped rather than allowed to carry across finger lifts.
            if (!touched)
            {
                stageState.ResetStageState();
                axisNorm = 0.0;
                softPressActActive = fullPressActActive = false;
                currentActiveButtons = ActiveZoneButtons.None;
                active = true;
                activeEvent = true;
                return;
            }

            // Schmitt-trigger hysteresis: once a stage is active, its effective
            // threshold drops slightly so noise near the configured value doesn't
            // cause rapid press/release chatter.
            int effectiveSoft = softPressActActive
                ? Math.Max(0, softPressThreshold - HYSTERESIS)
                : softPressThreshold;
            int effectiveFull = fullPressActActive
                ? Math.Max(effectiveSoft + 1, fullPressThreshold - HYSTERESIS)
                : fullPressThreshold;

            double norm = 0.0;
            if (clamped > effectiveSoft)
            {
                int span = Math.Max(1, effectiveFull - effectiveSoft);
                int offset = Math.Min(clamped, effectiveFull) - effectiveSoft;
                norm = offset / (double)span;
            }

            axisNorm = norm;
            bool fullPullClick = clamped >= effectiveFull;

            ActiveZoneButtons currentStageBtns = DualStageEvaluator.ProcessCurrentStage(
                activationStyle, axisNorm, fullPullClick, forceHipFireDelay, hipFireMs,
                this.fullPressActActive, stageState);

            softPressActActive = fullPressActActive = false;

            if ((currentStageBtns & ActiveZoneButtons.SoftPull) != 0)
            {
                softPressActActive = true;
            }

            if ((currentStageBtns & ActiveZoneButtons.FullPull) != 0)
            {
                fullPressActActive = true;
            }

            currentActiveButtons = currentStageBtns;
            active = true;
            activeEvent = true;
        }

        public override void Prepare(Mapper mapper, bool status, bool alterState = true)
        {
            PrepareTouchpadPressure(mapper, status ? fullPressThreshold : 0, status, alterState);
        }

        public override void PrepareAnalog(Mapper mapper, double axisNormIn, double axisUnitIn,
            bool alterState = true)
        {
            int pressure = (int)Math.Round(Math.Clamp(axisNormIn, 0.0, 1.0) * MAX_PRESSURE);
            PrepareTouchpadPressure(mapper, pressure, pressure > 0, alterState);
        }

        public override void Event(Mapper mapper)
        {
            bool wasSoftPressActive = !softPressActActive &&
                (previousActiveButtons & ActiveZoneButtons.SoftPull) != 0;
            if (wasSoftPressActive)
            {
                softPressActButton.PrepareAnalog(mapper, 0.0, 0.0);
                softPressActButton.Event(mapper);
            }

            bool wasFullPressActive = !fullPressActActive &&
                (previousActiveButtons & ActiveZoneButtons.FullPull) != 0;
            if (wasFullPressActive)
            {
                fullPressActButton.PrepareAnalog(mapper, 0.0, 0.0);
                fullPressActButton.Event(mapper);
            }

            if (softPressActActive)
            {
                softPressActButton.PrepareAnalog(mapper, axisNorm, 1.0);
                if (softPressActButton.active) softPressActButton.Event(mapper);
            }

            if (fullPressActActive)
            {
                fullPressActButton.PrepareAnalog(mapper, axisNorm, 1.0);
                if (fullPressActButton.active) fullPressActButton.Event(mapper);
            }

            previousActiveButtons = currentActiveButtons;
        }

        public override void Release(Mapper mapper, bool resetState = true, bool ignoreReleaseActions = false)
        {
            if (softPressActActive)
            {
                softPressActButton.Release(mapper, resetState, ignoreReleaseActions);
            }

            if (fullPressActActive)
            {
                fullPressActButton.Release(mapper, resetState, ignoreReleaseActions);
            }

            axisNorm = 0.0;
            currentActiveButtons = ActiveZoneButtons.None;
            previousActiveButtons = currentActiveButtons;
            softPressActActive = fullPressActActive = false;
            stageState.ResetStageState();
            active = activeEvent = false;
        }

        public override void SoftRelease(Mapper mapper, MapAction checkAction, bool resetState = true)
        {
            if (softPressActActive)
            {
                softPressActButton.Release(mapper, resetState);
            }

            if (fullPressActActive)
            {
                fullPressActButton.Release(mapper, resetState);
            }

            axisNorm = 0.0;
            currentActiveButtons = ActiveZoneButtons.None;
            previousActiveButtons = currentActiveButtons;
            softPressActActive = fullPressActActive = false;
            stageState.ResetStageState();
            active = activeEvent = false;
        }

        public override ButtonMapAction DuplicateAction()
        {
            TouchpadPressureDualStageAction copy = new TouchpadPressureDualStageAction();
            copy.CopyBaseProps(this);
            copy.activationStyle = activationStyle;
            copy.softPressThreshold = softPressThreshold;
            copy.fullPressThreshold = fullPressThreshold;
            copy.hipFireMs = hipFireMs;
            copy.forceHipFireDelay = forceHipFireDelay;
            copy.softPressActButton = softPressActButton != null ?
                (AxisDirButton)softPressActButton.DuplicateAction() : null;
            copy.fullPressActButton = fullPressActButton != null ?
                (AxisDirButton)fullPressActButton.DuplicateAction() : null;
            return copy;
        }

        public override void CopyAction(ButtonMapAction sourceAction, bool addProps = true)
        {
            if (sourceAction is TouchpadPressureDualStageAction tempSrc)
            {
                name = tempSrc.name;
                activationStyle = tempSrc.activationStyle;
                softPressThreshold = tempSrc.softPressThreshold;
                fullPressThreshold = tempSrc.fullPressThreshold;
                hipFireMs = tempSrc.hipFireMs;
                forceHipFireDelay = tempSrc.forceHipFireDelay;

                softPressActButton.ActionFuncs.Clear();
                foreach (ActionFunc func in tempSrc.softPressActButton.ActionFuncs)
                {
                    softPressActButton.ActionFuncs.Add(ActionFuncCopyFactory.CopyFunc(func));
                }

                fullPressActButton.ActionFuncs.Clear();
                foreach (ActionFunc func in tempSrc.fullPressActButton.ActionFuncs)
                {
                    fullPressActButton.ActionFuncs.Add(ActionFuncCopyFactory.CopyFunc(func));
                }

                if (addProps)
                {
                    changedProperties.Add(PropertyKeyStrings.NAME);
                    changedProperties.Add(PropertyKeyStrings.ACTIVATION_STYLE);
                    changedProperties.Add(PropertyKeyStrings.SOFT_THRESHOLD);
                    changedProperties.Add(PropertyKeyStrings.FULL_THRESHOLD);
                    changedProperties.Add(PropertyKeyStrings.HIPFIRE_DELAY);
                    changedProperties.Add(PropertyKeyStrings.FORCE_HIP_FIRE_TIME);
                    changedProperties.Add(PropertyKeyStrings.SOFTPRESS_BUTTON);
                    changedProperties.Add(PropertyKeyStrings.FULLPRESS_BUTTON);
                }
            }
        }

        public override string Describe()
        {
            return "TouchpadPressureDualStageAction";
        }

        public override string DescribeActions(Mapper mapper)
        {
            string softDesc = softPressActButton.DescribeActions(mapper);
            string fullDesc = fullPressActButton.DescribeActions(mapper);
            return $"Soft: {softDesc} | Full: {fullDesc}";
        }
    }
}
