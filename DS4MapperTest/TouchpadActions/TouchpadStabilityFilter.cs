using System;

namespace DS4MapperTest.TouchpadActions
{
    public enum TouchpadStabilityMode : ushort
    {
        Off,
        Light,
        Balanced,
        Strong,
        Custom,
        Feather,
        Gentle,
        Mild,
        Steady,
    }

    /// <summary>
    /// Tunable values for the trackpad stability filter. Settings follow
    /// normal profile/layer inheritance rules. Runtime state lives in
    /// TouchpadStabilityFilter and is never shared between actions.
    /// </summary>
    public class TouchpadStabilitySettings
    {
        // Reference pad width used to express count thresholds.
        // DS4/DualSense pads are 1920 counts wide; other pads scale.
        public const double REFERENCE_PAD_WIDTH = 1920.0;

        public const TouchpadStabilityMode DEFAULT_MODE = TouchpadStabilityMode.Balanced;
        public const double DEFAULT_TOUCH_SETTLE_MS = 8.0;
        public const double DEFAULT_BASE_NOISE_FLOOR = 6.0;
        public const double DEFAULT_HYSTERESIS_EXIT_MULTIPLIER = 1.5;
        public const double DEFAULT_FAST_PASSTHROUGH_THRESHOLD = 25.0;
        public const bool DEFAULT_EDGE_GUARD_ENABLED = true;
        public const double DEFAULT_LEFT_EDGE_PERCENT = 12.0;
        public const double DEFAULT_RIGHT_EDGE_PERCENT = 6.0;
        public const double DEFAULT_TOP_EDGE_PERCENT = 10.0;
        public const double DEFAULT_BOTTOM_EDGE_PERCENT = 6.0;
        public const double DEFAULT_EDGE_JITTER_MULTIPLIER = 2.0;
        public const double DEFAULT_CORNER_JITTER_MULTIPLIER = 2.0;
        public const double DEFAULT_TOP_LEFT_CORNER_MULTIPLIER = 2.5;
        public const double DEFAULT_EDGE_HYSTERESIS_PERCENT = 2.0;
        public const bool DEFAULT_EDGE_START_GATE_ENABLED = false;
        public const double DEFAULT_EDGE_START_THRESHOLD = 40.0;
        public const bool DEFAULT_EDGE_LOCK_ENABLED = true;
        public const bool DEFAULT_STATIONARY_HOLD_ENABLED = true;
        public const double DEFAULT_STATIONARY_DETECTION_MS = 40.0;
        public const double DEFAULT_STATIONARY_NOISE_MULTIPLIER = 1.5;
        public const double DEFAULT_STATIONARY_BREAKOUT_THRESHOLD = 20.0;
        public const bool DEFAULT_DELTA_CLAMP_ENABLED = true;
        public const double DEFAULT_MAX_DELTA_PER_FRAME = 120.0;

        public TouchpadStabilityMode Mode { get; set; } = DEFAULT_MODE;

        // Suppress output for this long after a new touch while the
        // initial samples settle. The anchor tracks the finger meanwhile
        // so movement resumes without a jump.
        public double TouchSettleMs { get; set; } = DEFAULT_TOUCH_SETTLE_MS;

        // Positional noise floor in reference pad counts. Movement of the
        // touch coordinate below this (scaled by zone multipliers) is
        // treated as sensor noise.
        public double BaseNoiseFloor { get; set; } = DEFAULT_BASE_NOISE_FLOOR;

        // Leaving the locked state requires displacement beyond
        // BaseNoiseFloor * this multiplier, so the lock cannot flicker.
        public double HysteresisExitMultiplier { get; set; } = DEFAULT_HYSTERESIS_EXIT_MULTIPLIER;

        // Per-frame delta magnitude (reference counts) that bypasses all
        // suppression immediately, provided direction is consistent with
        // the previous frame. Keeps fast swipes latency-free.
        public double FastPassthroughThreshold { get; set; } = DEFAULT_FAST_PASSTHROUGH_THRESHOLD;

        public bool EdgeGuardEnabled { get; set; } = DEFAULT_EDGE_GUARD_ENABLED;
        public double LeftEdgePercent { get; set; } = DEFAULT_LEFT_EDGE_PERCENT;
        public double RightEdgePercent { get; set; } = DEFAULT_RIGHT_EDGE_PERCENT;
        public double TopEdgePercent { get; set; } = DEFAULT_TOP_EDGE_PERCENT;
        public double BottomEdgePercent { get; set; } = DEFAULT_BOTTOM_EDGE_PERCENT;
        public double EdgeJitterMultiplier { get; set; } = DEFAULT_EDGE_JITTER_MULTIPLIER;
        public double CornerJitterMultiplier { get; set; } = DEFAULT_CORNER_JITTER_MULTIPLIER;
        public double TopLeftCornerMultiplier { get; set; } = DEFAULT_TOP_LEFT_CORNER_MULTIPLIER;
        // Widens a guard zone boundary while inside it so classification
        // cannot oscillate at the boundary.
        public double EdgeHysteresisPercent { get; set; } = DEFAULT_EDGE_HYSTERESIS_PERCENT;

        // A touch that begins inside a guard zone outputs nothing until it
        // moves inward or travels beyond EdgeStartThreshold.
        public bool EdgeStartGateEnabled { get; set; } = DEFAULT_EDGE_START_GATE_ENABLED;
        public double EdgeStartThreshold { get; set; } = DEFAULT_EDGE_START_THRESHOLD;

        // Re-lock almost immediately when movement pauses inside a guard
        // zone, instead of waiting the normal stationary window.
        public bool EdgeLockEnabled { get; set; } = DEFAULT_EDGE_LOCK_ENABLED;

        public bool StationaryHoldEnabled { get; set; } = DEFAULT_STATIONARY_HOLD_ENABLED;
        public double StationaryDetectionMs { get; set; } = DEFAULT_STATIONARY_DETECTION_MS;
        public double StationaryNoiseMultiplier { get; set; } = DEFAULT_STATIONARY_NOISE_MULTIPLIER;
        public double StationaryBreakoutThreshold { get; set; } = DEFAULT_STATIONARY_BREAKOUT_THRESHOLD;

        public bool DeltaClampEnabled { get; set; } = DEFAULT_DELTA_CLAMP_ENABLED;
        public double MaxDeltaPerFrame { get; set; } = DEFAULT_MAX_DELTA_PER_FRAME;

        private TouchpadStabilitySettings customSnapshot;

        public TouchpadStabilitySettings()
        {
            ApplyPreset(DEFAULT_MODE);
        }

        public void CopyFrom(TouchpadStabilitySettings other)
        {
            Mode = other.Mode;
            CopyValuesFrom(other);
        }

        public void CopyValuesFrom(TouchpadStabilitySettings other)
        {
            TouchSettleMs = other.TouchSettleMs;
            BaseNoiseFloor = other.BaseNoiseFloor;
            HysteresisExitMultiplier = other.HysteresisExitMultiplier;
            FastPassthroughThreshold = other.FastPassthroughThreshold;
            EdgeGuardEnabled = other.EdgeGuardEnabled;
            LeftEdgePercent = other.LeftEdgePercent;
            RightEdgePercent = other.RightEdgePercent;
            TopEdgePercent = other.TopEdgePercent;
            BottomEdgePercent = other.BottomEdgePercent;
            EdgeJitterMultiplier = other.EdgeJitterMultiplier;
            CornerJitterMultiplier = other.CornerJitterMultiplier;
            TopLeftCornerMultiplier = other.TopLeftCornerMultiplier;
            EdgeHysteresisPercent = other.EdgeHysteresisPercent;
            EdgeStartGateEnabled = other.EdgeStartGateEnabled;
            EdgeStartThreshold = other.EdgeStartThreshold;
            EdgeLockEnabled = other.EdgeLockEnabled;
            StationaryHoldEnabled = other.StationaryHoldEnabled;
            StationaryDetectionMs = other.StationaryDetectionMs;
            StationaryNoiseMultiplier = other.StationaryNoiseMultiplier;
            StationaryBreakoutThreshold = other.StationaryBreakoutThreshold;
            DeltaClampEnabled = other.DeltaClampEnabled;
            MaxDeltaPerFrame = other.MaxDeltaPerFrame;
        }

        public void ResetToDefaults()
        {
            ApplyPreset(DEFAULT_MODE);
            customSnapshot = null;
        }

        public void CopyGroupFrom(TouchpadStabilitySettings other, string groupName)
        {
            switch (groupName)
            {
                case "StabilityMode":
                    Mode = other.Mode;
                    break;
                case "StabilityTouchSettle":
                    TouchSettleMs = other.TouchSettleMs;
                    break;
                case "StabilityNoise":
                    BaseNoiseFloor = other.BaseNoiseFloor;
                    HysteresisExitMultiplier = other.HysteresisExitMultiplier;
                    FastPassthroughThreshold = other.FastPassthroughThreshold;
                    break;
                case "StabilityEdgeGuard":
                    EdgeGuardEnabled = other.EdgeGuardEnabled;
                    LeftEdgePercent = other.LeftEdgePercent;
                    RightEdgePercent = other.RightEdgePercent;
                    TopEdgePercent = other.TopEdgePercent;
                    BottomEdgePercent = other.BottomEdgePercent;
                    EdgeJitterMultiplier = other.EdgeJitterMultiplier;
                    CornerJitterMultiplier = other.CornerJitterMultiplier;
                    TopLeftCornerMultiplier = other.TopLeftCornerMultiplier;
                    EdgeHysteresisPercent = other.EdgeHysteresisPercent;
                    EdgeLockEnabled = other.EdgeLockEnabled;
                    break;
                case "StabilityEdgeStartGate":
                    EdgeStartGateEnabled = other.EdgeStartGateEnabled;
                    EdgeStartThreshold = other.EdgeStartThreshold;
                    break;
                case "StabilityStationary":
                    StationaryHoldEnabled = other.StationaryHoldEnabled;
                    StationaryDetectionMs = other.StationaryDetectionMs;
                    StationaryNoiseMultiplier = other.StationaryNoiseMultiplier;
                    StationaryBreakoutThreshold = other.StationaryBreakoutThreshold;
                    break;
                case "StabilityDeltaClamp":
                    DeltaClampEnabled = other.DeltaClampEnabled;
                    MaxDeltaPerFrame = other.MaxDeltaPerFrame;
                    break;
                default:
                    break;
            }
        }

        public void CaptureCustomPreset()
        {
            if (customSnapshot == null)
            {
                customSnapshot = new TouchpadStabilitySettings();
            }

            customSnapshot.Mode = TouchpadStabilityMode.Custom;
            customSnapshot.CopyValuesFrom(this);
        }

        public bool RestoreCustomPreset()
        {
            if (customSnapshot == null)
            {
                return false;
            }

            CopyValuesFrom(customSnapshot);
            Mode = TouchpadStabilityMode.Custom;
            return true;
        }

        public bool TryMatchPreset(out TouchpadStabilityMode mode)
        {
            TouchpadStabilityMode[] presets =
            {
                TouchpadStabilityMode.Feather,
                TouchpadStabilityMode.Gentle,
                TouchpadStabilityMode.Mild,
                TouchpadStabilityMode.Light,
                TouchpadStabilityMode.Balanced,
                TouchpadStabilityMode.Steady,
                TouchpadStabilityMode.Strong,
            };

            foreach (TouchpadStabilityMode preset in presets)
            {
                if (MatchesPreset(preset))
                {
                    mode = preset;
                    return true;
                }
            }

            mode = TouchpadStabilityMode.Custom;
            return false;
        }

        public bool MatchesPreset(TouchpadStabilityMode mode)
        {
            TouchpadStabilitySettings preset = new TouchpadStabilitySettings();
            preset.ApplyPreset(mode);
            return ValuesEqual(preset);
        }

        private bool ValuesEqual(TouchpadStabilitySettings other)
        {
            const double tolerance = 0.0001;
            return Math.Abs(TouchSettleMs - other.TouchSettleMs) < tolerance &&
                Math.Abs(BaseNoiseFloor - other.BaseNoiseFloor) < tolerance &&
                Math.Abs(HysteresisExitMultiplier - other.HysteresisExitMultiplier) < tolerance &&
                Math.Abs(FastPassthroughThreshold - other.FastPassthroughThreshold) < tolerance &&
                EdgeGuardEnabled == other.EdgeGuardEnabled &&
                Math.Abs(LeftEdgePercent - other.LeftEdgePercent) < tolerance &&
                Math.Abs(RightEdgePercent - other.RightEdgePercent) < tolerance &&
                Math.Abs(TopEdgePercent - other.TopEdgePercent) < tolerance &&
                Math.Abs(BottomEdgePercent - other.BottomEdgePercent) < tolerance &&
                Math.Abs(EdgeJitterMultiplier - other.EdgeJitterMultiplier) < tolerance &&
                Math.Abs(CornerJitterMultiplier - other.CornerJitterMultiplier) < tolerance &&
                Math.Abs(TopLeftCornerMultiplier - other.TopLeftCornerMultiplier) < tolerance &&
                Math.Abs(EdgeHysteresisPercent - other.EdgeHysteresisPercent) < tolerance &&
                EdgeStartGateEnabled == other.EdgeStartGateEnabled &&
                Math.Abs(EdgeStartThreshold - other.EdgeStartThreshold) < tolerance &&
                EdgeLockEnabled == other.EdgeLockEnabled &&
                StationaryHoldEnabled == other.StationaryHoldEnabled &&
                Math.Abs(StationaryDetectionMs - other.StationaryDetectionMs) < tolerance &&
                Math.Abs(StationaryNoiseMultiplier - other.StationaryNoiseMultiplier) < tolerance &&
                Math.Abs(StationaryBreakoutThreshold - other.StationaryBreakoutThreshold) < tolerance &&
                DeltaClampEnabled == other.DeltaClampEnabled &&
                Math.Abs(MaxDeltaPerFrame - other.MaxDeltaPerFrame) < tolerance;
        }

        /// <summary>
        /// Write the tuned values for a named preset. Off and Custom leave
        /// current values untouched; Off bypasses the filter entirely.
        /// </summary>
        public void ApplyPreset(TouchpadStabilityMode mode)
        {
            Mode = mode;
            switch (mode)
            {
                case TouchpadStabilityMode.Feather:
                    TouchSettleMs = 0.0;
                    BaseNoiseFloor = 1.5;
                    HysteresisExitMultiplier = 1.1;
                    FastPassthroughThreshold = 12.0;
                    EdgeGuardEnabled = false;
                    LeftEdgePercent = 4.0;
                    RightEdgePercent = 2.0;
                    TopEdgePercent = 3.0;
                    BottomEdgePercent = 2.0;
                    EdgeJitterMultiplier = 1.1;
                    CornerJitterMultiplier = 1.1;
                    TopLeftCornerMultiplier = 1.25;
                    EdgeHysteresisPercent = 1.0;
                    EdgeStartGateEnabled = false;
                    EdgeStartThreshold = 25.0;
                    EdgeLockEnabled = false;
                    StationaryHoldEnabled = false;
                    StationaryDetectionMs = 50.0;
                    StationaryNoiseMultiplier = 1.1;
                    StationaryBreakoutThreshold = 12.0;
                    DeltaClampEnabled = true;
                    MaxDeltaPerFrame = 180.0;
                    break;
                case TouchpadStabilityMode.Gentle:
                    TouchSettleMs = 2.0;
                    BaseNoiseFloor = 2.5;
                    HysteresisExitMultiplier = 1.15;
                    FastPassthroughThreshold = 15.0;
                    EdgeGuardEnabled = true;
                    LeftEdgePercent = 5.0;
                    RightEdgePercent = 3.0;
                    TopEdgePercent = 4.0;
                    BottomEdgePercent = 3.0;
                    EdgeJitterMultiplier = 1.2;
                    CornerJitterMultiplier = 1.2;
                    TopLeftCornerMultiplier = 1.5;
                    EdgeHysteresisPercent = 1.0;
                    EdgeStartGateEnabled = false;
                    EdgeStartThreshold = 30.0;
                    EdgeLockEnabled = false;
                    StationaryHoldEnabled = false;
                    StationaryDetectionMs = 45.0;
                    StationaryNoiseMultiplier = 1.15;
                    StationaryBreakoutThreshold = 14.0;
                    DeltaClampEnabled = true;
                    MaxDeltaPerFrame = 170.0;
                    break;
                case TouchpadStabilityMode.Mild:
                    TouchSettleMs = 3.0;
                    BaseNoiseFloor = 3.25;
                    HysteresisExitMultiplier = 1.2;
                    FastPassthroughThreshold = 18.0;
                    EdgeGuardEnabled = true;
                    LeftEdgePercent = 6.0;
                    RightEdgePercent = 3.0;
                    TopEdgePercent = 5.0;
                    BottomEdgePercent = 3.0;
                    EdgeJitterMultiplier = 1.35;
                    CornerJitterMultiplier = 1.35;
                    TopLeftCornerMultiplier = 1.75;
                    EdgeHysteresisPercent = 1.5;
                    EdgeStartGateEnabled = false;
                    EdgeStartThreshold = 35.0;
                    EdgeLockEnabled = false;
                    StationaryHoldEnabled = false;
                    StationaryDetectionMs = 40.0;
                    StationaryNoiseMultiplier = 1.2;
                    StationaryBreakoutThreshold = 16.0;
                    DeltaClampEnabled = true;
                    MaxDeltaPerFrame = 160.0;
                    break;
                case TouchpadStabilityMode.Light:
                    TouchSettleMs = 4.0;
                    BaseNoiseFloor = 4.0;
                    HysteresisExitMultiplier = 1.3;
                    FastPassthroughThreshold = 20.0;
                    EdgeGuardEnabled = true;
                    LeftEdgePercent = 8.0;
                    RightEdgePercent = 4.0;
                    TopEdgePercent = 6.0;
                    BottomEdgePercent = 4.0;
                    EdgeJitterMultiplier = 1.5;
                    CornerJitterMultiplier = 1.5;
                    TopLeftCornerMultiplier = 2.0;
                    EdgeHysteresisPercent = 2.0;
                    EdgeStartGateEnabled = false;
                    EdgeStartThreshold = 40.0;
                    EdgeLockEnabled = true;
                    StationaryHoldEnabled = false;
                    StationaryDetectionMs = 40.0;
                    StationaryNoiseMultiplier = 1.3;
                    StationaryBreakoutThreshold = 18.0;
                    DeltaClampEnabled = true;
                    MaxDeltaPerFrame = 150.0;
                    break;
                case TouchpadStabilityMode.Balanced:
                    TouchSettleMs = 8.0;
                    BaseNoiseFloor = 6.0;
                    HysteresisExitMultiplier = 1.5;
                    FastPassthroughThreshold = 25.0;
                    EdgeGuardEnabled = true;
                    LeftEdgePercent = 12.0;
                    RightEdgePercent = 6.0;
                    TopEdgePercent = 10.0;
                    BottomEdgePercent = 6.0;
                    EdgeJitterMultiplier = 2.0;
                    CornerJitterMultiplier = 2.0;
                    TopLeftCornerMultiplier = 2.5;
                    EdgeHysteresisPercent = 2.0;
                    EdgeStartGateEnabled = false;
                    EdgeStartThreshold = 40.0;
                    EdgeLockEnabled = true;
                    StationaryHoldEnabled = true;
                    StationaryDetectionMs = 40.0;
                    StationaryNoiseMultiplier = 1.5;
                    StationaryBreakoutThreshold = 20.0;
                    DeltaClampEnabled = true;
                    MaxDeltaPerFrame = 120.0;
                    break;
                case TouchpadStabilityMode.Steady:
                    TouchSettleMs = 10.0;
                    BaseNoiseFloor = 7.5;
                    HysteresisExitMultiplier = 1.6;
                    FastPassthroughThreshold = 28.0;
                    EdgeGuardEnabled = true;
                    LeftEdgePercent = 13.0;
                    RightEdgePercent = 7.0;
                    TopEdgePercent = 11.0;
                    BottomEdgePercent = 7.0;
                    EdgeJitterMultiplier = 2.25;
                    CornerJitterMultiplier = 2.25;
                    TopLeftCornerMultiplier = 2.75;
                    EdgeHysteresisPercent = 2.5;
                    EdgeStartGateEnabled = true;
                    EdgeStartThreshold = 45.0;
                    EdgeLockEnabled = true;
                    StationaryHoldEnabled = true;
                    StationaryDetectionMs = 35.0;
                    StationaryNoiseMultiplier = 1.75;
                    StationaryBreakoutThreshold = 22.0;
                    DeltaClampEnabled = true;
                    MaxDeltaPerFrame = 110.0;
                    break;
                case TouchpadStabilityMode.Strong:
                    TouchSettleMs = 12.0;
                    BaseNoiseFloor = 9.0;
                    HysteresisExitMultiplier = 1.7;
                    FastPassthroughThreshold = 30.0;
                    EdgeGuardEnabled = true;
                    LeftEdgePercent = 15.0;
                    RightEdgePercent = 8.0;
                    TopEdgePercent = 12.0;
                    BottomEdgePercent = 8.0;
                    EdgeJitterMultiplier = 2.5;
                    CornerJitterMultiplier = 2.5;
                    TopLeftCornerMultiplier = 3.0;
                    EdgeHysteresisPercent = 3.0;
                    EdgeStartGateEnabled = true;
                    EdgeStartThreshold = 50.0;
                    EdgeLockEnabled = true;
                    StationaryHoldEnabled = true;
                    StationaryDetectionMs = 30.0;
                    StationaryNoiseMultiplier = 2.0;
                    StationaryBreakoutThreshold = 24.0;
                    DeltaClampEnabled = true;
                    MaxDeltaPerFrame = 100.0;
                    break;
                default:
                    break;
            }
        }
    }

    /// <summary>
    /// Positional stability filter for relative trackpad mouse/camera
    /// output. Suppresses sensor jitter, most notably the violent
    /// buzzing produced when a thumb rests on the physical pad edge or
    /// the top-left corner, while letting intentional movement through
    /// with minimal latency. All state is per-touch and private to this
        /// instance.
    /// </summary>
    public class TouchpadStabilityFilter
    {
        private enum FilterState : ushort
        {
            Idle,
            Settling,
            Gated,
            Locked,
            Stationary,
            Moving,
        }

        // Re-lock window used inside guard zones when Edge Lock is active
        private const double EDGE_RELOCK_MS = 15.0;

        private readonly TouchpadStabilitySettings settings;

        private FilterState state = FilterState.Idle;
        private double anchorX;
        private double anchorY;
        private double touchStartX;
        private double touchStartY;
        private double settleElapsedMs;
        private double stationaryElapsedMs;
        // Reference point and timer used to detect that movement has
        // effectively stopped while in the Moving state
        private double quietRefX;
        private double quietRefY;
        private double quietElapsedMs;
        // Previous raw per-frame delta, for direction consistency checks
        private double prevRawDx;
        private double prevRawDy;
        // Locked-state breakout requires two consecutive frames displaced
        // on the same side of the anchor, so alternating buzz of any
        // amplitude can never break the lock
        private bool exitCandidate;
        private double exitCandidateDx;
        private double exitCandidateDy;
        // Zone classification persisted for boundary hysteresis
        private bool zoneLeft;
        private bool zoneRight;
        private bool zoneTop;
        private bool zoneBottom;

        public TouchpadStabilityFilter(TouchpadStabilitySettings settings)
        {
            this.settings = settings;
        }

        public bool Enabled => settings.Mode != TouchpadStabilityMode.Off;

        public void Reset()
        {
            state = FilterState.Idle;
            settleElapsedMs = 0.0;
            stationaryElapsedMs = 0.0;
            quietElapsedMs = 0.0;
            prevRawDx = prevRawDy = 0.0;
            exitCandidate = false;
            zoneLeft = zoneRight = zoneTop = zoneBottom = false;
        }

        public void OnTouchStart(ref TouchEventFrame frame, TouchpadDefinition padDef)
        {
            Reset();
            touchStartX = anchorX = quietRefX = frame.X;
            touchStartY = anchorY = quietRefY = frame.Y;
            state = FilterState.Settling;
        }

        public void OnTouchEnd()
        {
            Reset();
        }

        /// <summary>
        /// Filter one delta-capable frame (current and previous frames both
        /// touching). Outputs deltas in pad coordinate space; the caller
        /// applies its own axis conventions. Returns true when movement
        /// was suppressed
        /// </summary>
        public bool Filter(ref TouchEventFrame frame, ref TouchEventFrame previous,
            TouchpadDefinition padDef, out int dx, out int dy)
        {
            int rawDx = frame.X - previous.X;
            int rawDy = frame.Y - previous.Y;
            dx = rawDx;
            dy = rawDy;

            if (!Enabled)
            {
                return false;
            }

            if (state == FilterState.Idle)
            {
                // Filter enabled mid-touch. Treat as a fresh touch
                OnTouchStart(ref frame, padDef);
            }

            double frameMs = frame.timeElapsed * 1000.0;
            double countScale = ComputeCountScale(padDef);
            ClassifyZones(frame.X, frame.Y, padDef);
            double localFloor = settings.BaseNoiseFloor * countScale * ZoneMultiplier();
            double exitThreshold = localFloor * Math.Max(1.0, settings.HysteresisExitMultiplier);
            double passthrough = settings.FastPassthroughThreshold * countScale;
            double rawMag = Math.Sqrt((rawDx * rawDx) + (rawDy * rawDy));
            // Genuine fast movement keeps direction between frames;
            // alternating buzz flips it
            bool directionConsistent = ((rawDx * prevRawDx) + (rawDy * prevRawDy)) > 0.0;
            bool fastMove = rawMag > passthrough && directionConsistent;
            bool suppressed = false;

            switch (state)
            {
                case FilterState.Settling:
                    settleElapsedMs += frameMs;
                    if (settleElapsedMs < settings.TouchSettleMs)
                    {
                        anchorX = frame.X; anchorY = frame.Y;
                        suppressed = true;
                    }
                    else
                    {
                        anchorX = frame.X; anchorY = frame.Y;
                        bool gateStart = settings.EdgeStartGateEnabled &&
                            settings.EdgeGuardEnabled && InAnyGuardZone();
                        state = gateStart ? FilterState.Gated : FilterState.Locked;
                        stationaryElapsedMs = 0.0;
                        suppressed = true;
                    }

                    break;
                case FilterState.Gated:
                    if (fastMove)
                    {
                        EnterMoving(frame.X, frame.Y);
                    }
                    else if (!InAnyGuardZone())
                    {
                        // Finger moved inward to the safe zone
                        anchorX = frame.X; anchorY = frame.Y;
                        state = FilterState.Locked;
                        stationaryElapsedMs = 0.0;
                        suppressed = true;
                    }
                    else
                    {
                        double startDist = Distance(frame.X, frame.Y, touchStartX, touchStartY);
                        if (startDist > settings.EdgeStartThreshold * countScale)
                        {
                            EnterMoving(frame.X, frame.Y);
                        }
                        else
                        {
                            suppressed = true;
                        }
                    }

                    break;
                case FilterState.Locked:
                case FilterState.Stationary:
                    if (state == FilterState.Stationary)
                    {
                        localFloor *= Math.Max(1.0, settings.StationaryNoiseMultiplier);
                        exitThreshold = Math.Max(exitThreshold,
                            settings.StationaryBreakoutThreshold * countScale);
                    }

                    if (fastMove)
                    {
                        EnterMoving(frame.X, frame.Y);
                    }
                    else
                    {
                        double anchorDx = frame.X - anchorX;
                        double anchorDy = frame.Y - anchorY;
                        double anchorDist = Math.Sqrt((anchorDx * anchorDx) + (anchorDy * anchorDy));
                        if (anchorDist > exitThreshold)
                        {
                            // Require two consecutive frames displaced on the
                            // same side of the anchor before unlocking
                            bool sameSide = exitCandidate &&
                                ((anchorDx * exitCandidateDx) + (anchorDy * exitCandidateDy)) > 0.0;
                            if (sameSide)
                            {
                                EnterMoving(frame.X, frame.Y);
                            }
                            else
                            {
                                exitCandidate = true;
                                exitCandidateDx = anchorDx;
                                exitCandidateDy = anchorDy;
                                suppressed = true;
                            }
                        }
                        else
                        {
                            exitCandidate = false;
                            suppressed = true;
                            if (state == FilterState.Locked)
                            {
                                stationaryElapsedMs += frameMs;
                                if (settings.StationaryHoldEnabled &&
                                    stationaryElapsedMs >= settings.StationaryDetectionMs)
                                {
                                    state = FilterState.Stationary;
                                }
                            }
                        }
                    }

                    break;
                case FilterState.Moving:
                    // Detect that movement has effectively stopped: net
                    // displacement from the quiet reference stays within the
                    // local threshold for the re-lock window
                    double quietDist = Distance(frame.X, frame.Y, quietRefX, quietRefY);
                    if (quietDist > exitThreshold || fastMove)
                    {
                        quietRefX = frame.X; quietRefY = frame.Y;
                        quietElapsedMs = 0.0;
                    }
                    else
                    {
                        quietElapsedMs += frameMs;
                        double relockMs = (settings.EdgeLockEnabled &&
                            settings.EdgeGuardEnabled && InAnyGuardZone())
                            ? EDGE_RELOCK_MS : settings.StationaryDetectionMs;
                        if (quietElapsedMs >= relockMs)
                        {
                            anchorX = quietRefX; anchorY = quietRefY;
                            state = FilterState.Locked;
                            stationaryElapsedMs = 0.0;
                            exitCandidate = false;
                            suppressed = true;
                        }
                    }

                    if (!suppressed)
                    {
                        anchorX = frame.X; anchorY = frame.Y;
                    }

                    break;
                default:
                    break;
            }

            if (suppressed)
            {
                dx = 0;
                dy = 0;
            }
            else if (settings.DeltaClampEnabled)
            {
                int maxDelta = (int)Math.Max(1.0, settings.MaxDeltaPerFrame * countScale);
                dx = Math.Clamp(dx, -maxDelta, maxDelta);
                dy = Math.Clamp(dy, -maxDelta, maxDelta);
            }

            prevRawDx = rawDx;
            prevRawDy = rawDy;

            return suppressed;
        }

        private void EnterMoving(double x, double y)
        {
            state = FilterState.Moving;
            anchorX = quietRefX = x;
            anchorY = quietRefY = y;
            quietElapsedMs = 0.0;
            stationaryElapsedMs = 0.0;
            exitCandidate = false;
        }

        private static double Distance(double x1, double y1, double x2, double y2)
        {
            double dx = x1 - x2;
            double dy = y1 - y2;
            return Math.Sqrt((dx * dx) + (dy * dy));
        }

        private static double ComputeCountScale(TouchpadDefinition padDef)
        {
            double width = padDef.xAxis.max - (double)padDef.xAxis.min;
            return width > 0.0 ? width / TouchpadStabilitySettings.REFERENCE_PAD_WIDTH : 1.0;
        }

        private void ClassifyZones(double x, double y, TouchpadDefinition padDef)
        {
            if (!settings.EdgeGuardEnabled)
            {
                zoneLeft = zoneRight = zoneTop = zoneBottom = false;
                return;
            }

            double width = Math.Max(1.0, padDef.xAxis.max - (double)padDef.xAxis.min);
            double height = Math.Max(1.0, padDef.yAxis.max - (double)padDef.yAxis.min);
            double nx = (x - padDef.xAxis.min) / width;
            double ny = (y - padDef.yAxis.min) / height;
            double hyst = settings.EdgeHysteresisPercent / 100.0;

            // Frame Y increases toward the physical top of the pad
            // (mappers flip the raw axis so positive delta means mouse up)
            zoneLeft = nx < (settings.LeftEdgePercent / 100.0) + (zoneLeft ? hyst : 0.0);
            zoneRight = nx > 1.0 - ((settings.RightEdgePercent / 100.0) + (zoneRight ? hyst : 0.0));
            zoneTop = ny > 1.0 - ((settings.TopEdgePercent / 100.0) + (zoneTop ? hyst : 0.0));
            zoneBottom = ny < (settings.BottomEdgePercent / 100.0) + (zoneBottom ? hyst : 0.0);
        }

        private bool InAnyGuardZone()
        {
            return zoneLeft || zoneRight || zoneTop || zoneBottom;
        }

        private double ZoneMultiplier()
        {
            if (!settings.EdgeGuardEnabled)
            {
                return 1.0;
            }

            if (zoneLeft && zoneTop)
            {
                return settings.TopLeftCornerMultiplier;
            }

            bool corner = (zoneLeft || zoneRight) && (zoneTop || zoneBottom);
            if (corner)
            {
                return settings.CornerJitterMultiplier;
            }

            return InAnyGuardZone() ? settings.EdgeJitterMultiplier : 1.0;
        }

        private string ZoneText()
        {
            if (!InAnyGuardZone())
            {
                return "centre";
            }

            return $"{(zoneLeft ? "L" : "")}{(zoneRight ? "R" : "")}{(zoneTop ? "T" : "")}{(zoneBottom ? "B" : "")}";
        }
    }
}
