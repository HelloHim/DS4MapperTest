using DS4MapperTest.ButtonActions;
using DS4MapperTest.Common;
using DS4MapperTest.DPadActions;
using DS4MapperTest.GyroActions;
using DS4MapperTest.MapperUtil;
using DS4MapperTest.StickActions;
using DS4MapperTest.TouchpadActions;
using DS4MapperTest.TriggerActions;
using DS4MapperTest.Universal.Editor;
using DS4MapperTest.Universal.Profiles;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DS4MapperTest.Universal.Mapping
{
    public sealed class UniversalMapper : Mapper
    {
        // Single source of truth for which JoypadActionCodes an activation
        // trigger (gyro hold-to-enable/disable, chorded press, etc.) can
        // reference, and which universal input backs each one. Every entry
        // here must be one TryMapActivationCode already understands, since
        // that switch is what IsButtonActive polls at runtime.
        //
        // This list intentionally omits inputs with no JoypadActionCodes to
        // carry them (rear tertiary, grip touch, stick touch, mute, quick
        // access, misc buttons/axes) rather than widening JoypadActionCodes
        // for them - that enum is serialised into saved profiles and used in
        // exhaustive switches well beyond activation triggers.
        private static readonly (JoypadActionCodes Code, UniversalInputId Input)[] ActivationCandidates =
        {
            (JoypadActionCodes.BtnSouth, UniversalInputId.FaceButtonSouth),
            (JoypadActionCodes.BtnEast, UniversalInputId.FaceButtonEast),
            (JoypadActionCodes.BtnWest, UniversalInputId.FaceButtonWest),
            (JoypadActionCodes.BtnNorth, UniversalInputId.FaceButtonNorth),
            (JoypadActionCodes.BtnLShoulder, UniversalInputId.LeftShoulder),
            (JoypadActionCodes.BtnRShoulder, UniversalInputId.RightShoulder),
            (JoypadActionCodes.AxisLTrigger, UniversalInputId.LeftTrigger),
            (JoypadActionCodes.AxisRTrigger, UniversalInputId.RightTrigger),
            (JoypadActionCodes.LTFullPull, UniversalInputId.LeftTriggerFullPull),
            (JoypadActionCodes.RTFullPull, UniversalInputId.RightTriggerFullPull),
            (JoypadActionCodes.BtnLGrip, UniversalInputId.LeftRearPrimary),
            (JoypadActionCodes.BtnRGrip, UniversalInputId.RightRearPrimary),
            (JoypadActionCodes.BtnLGrip2, UniversalInputId.LeftRearSecondary),
            (JoypadActionCodes.BtnRGrip2, UniversalInputId.RightRearSecondary),
            (JoypadActionCodes.BtnThumbL, UniversalInputId.LeftStickClick),
            (JoypadActionCodes.BtnThumbR, UniversalInputId.RightStickClick),
            (JoypadActionCodes.BtnLSideL, UniversalInputId.LeftSidePrimary),
            (JoypadActionCodes.BtnLSideR, UniversalInputId.LeftSideSecondary),
            (JoypadActionCodes.BtnRSideL, UniversalInputId.RightSidePrimary),
            (JoypadActionCodes.BtnRSideR, UniversalInputId.RightSideSecondary),
            (JoypadActionCodes.LPadTouch, UniversalInputId.LeftTouchSurface),
            (JoypadActionCodes.RPadTouch, UniversalInputId.RightTouchSurface),
            (JoypadActionCodes.LPadClick, UniversalInputId.LeftTouchSurfaceClick),
            (JoypadActionCodes.RPadClick, UniversalInputId.RightTouchSurfaceClick),
            (JoypadActionCodes.CenterPadTouch, UniversalInputId.PrimaryTouchSurface),
            (JoypadActionCodes.CenterPadClick, UniversalInputId.PrimaryTouchSurfaceClick),
            (JoypadActionCodes.BtnSelect, UniversalInputId.View),
            (JoypadActionCodes.BtnStart, UniversalInputId.Menu),
            (JoypadActionCodes.BtnMode, UniversalInputId.System),
            (JoypadActionCodes.BtnCapture, UniversalInputId.Capture),
        };

        private static readonly IReadOnlyDictionary<JoypadActionCodes, UniversalInputId> ActivationCodeToInput =
            ActivationCandidates.ToDictionary(pair => pair.Code, pair => pair.Input);

        // A single-pad controller reports its whole surface and that surface's
        // click under the same native label ("Touchpad"), which would leave two
        // identical rows in every activation list. Name them the way the rest of
        // the editor names that pad, and keep the touch/click distinction the
        // left and right region entries already spell out.
        private static readonly IReadOnlyDictionary<JoypadActionCodes, string> ActivationLabelOverrides =
            new Dictionary<JoypadActionCodes, string>
            {
                [JoypadActionCodes.CenterPadTouch] = "Center Touchpad Touch",
                [JoypadActionCodes.CenterPadClick] = "Center Touchpad Click",
            };

        private UniversalControllerStateSnapshot currentSnapshot =
            UniversalControllerStateSnapshot.Disconnected();
        private UniversalControllerStateSnapshot previousSnapshot =
            UniversalControllerStateSnapshot.Disconnected();
        private TouchEventFrame previousPrimaryTouchFrame;
        private TouchEventFrame previousLeftTouchFrame;
        private TouchEventFrame previousRightTouchFrame;
        private readonly GyroCalibration gyroCalibration = new GyroCalibration();
        private bool stopped = true;

        public UniversalMapper(IUniversalController controller, UniversalProfile profile)
        {
            Controller = controller ?? throw new ArgumentNullException(nameof(controller));
            DeviceTypeOverride = ResolveDeviceType(controller);
            ConfigureUniversalBindings();
            RebuildActionTriggerItems();
            ActivateProfile(profile);
        }

        public IUniversalController Controller { get; }
        public UniversalCompiledProfile CompiledProfile { get; private set; }
        public override DeviceReaderBase BaseReader => null;
        public InputDeviceType DeviceTypeOverride { get; private set; }
        public override InputDeviceType DeviceType => DeviceTypeOverride;

        // The native Steam Controller backend feeds this mapper a frame its own
        // reader already built, so that one keeps its family's convention.
        // Everything else arrives through SDL, which normalises every
        // controller it supports into one frame regardless of the hardware, so
        // the family the device was identified as says nothing about how its
        // sensors are oriented.
        public override InputDeviceType GyroSensorConventionDeviceType =>
            Controller.Identity.BackendName == UniversalControllerBackendIds.SteamControllerNative
                ? DeviceTypeOverride
                : SdlSensorConvention.FrameDeviceType;
        public GyroCalibrationStatus GyroCalibrationStatus => gyroCalibration.Status;

        // The last frame handed to the gyro action. Exists so a test can assert
        // that calibration reaches the fields the gyro actions actually read,
        // rather than only that the arithmetic is right somewhere off to one
        // side, which is exactly how it came to be applied to the legacy
        // integer fields and not the angular ones.
        internal GyroEventFrame LastGyroFrameForTest { get; private set; }

        public void RequestGyroCalibration()
        {
            gyroCalibration.RequestCalibrationAfterDelay(1000);
        }

        public override void Start(VirtualKBMBase fakerInputHandler, VirtualKBMMapping eventInputMapping)
        {
            this.eventInputHandler = fakerInputHandler;
            this.eventInputMapping = eventInputMapping;
            stopped = false;
            RefreshViiperOutput();
        }

        public void ActivateProfile(UniversalProfile profile)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));

            UniversalCompiledProfile compiled =
                UniversalProfileRuntimeCompiler.Compile(profile, Controller.Capabilities);

            if (actionProfile != null)
            {
                actionProfile.CurrentActionSet.ReleaseActions(this, true);
                ReleaseAllPendingReleaseFuns();
                SyncKeyboard();
                SyncMouseButtons();
                eventInputHandler?.Sync();
            }

            LoadProfileFromJson(compiled.LegacyJson, string.Empty);
            CompiledProfile = compiled;
            loggedFirstVirtualState = false;
            if (eventInputHandler != null)
            {
                RefreshViiperOutput();
            }

            previousSnapshot = UniversalControllerStateSnapshot.Disconnected();
        }

        public void ProcessSnapshot(UniversalControllerStateSnapshot snapshot)
        {
            if (stopped || quit) return;

            UniversalControllerStateSnapshot nextSnapshot =
                snapshot ?? UniversalControllerStateSnapshot.Disconnected(previousSnapshot.Sequence + 1);
            if (!nextSnapshot.IsConnected)
            {
                Stop(true);
                previousSnapshot = nextSnapshot;
                currentSnapshot = nextSnapshot;
                return;
            }

            mapperActionActive = true;
            currentSnapshot = nextSnapshot;
            mouseX = mouseY = 0.0;
            intermediateState = new IntermediateState();
            currentLatency = CalculateElapsedSeconds(currentSnapshot, previousSnapshot);
            currentRate = currentLatency > 0.0 ? 1.0 / currentLatency : 0.0;

            ProcessReleaseEvents();
            ProcessCycleChecks();

            ActionLayer currentLayer = actionProfile.CurrentActionSet.CurrentActionLayer;
            ProcessActionSetButton(currentLayer);
            ProcessSticks(currentLayer);
            ProcessTriggers(currentLayer);
            ProcessButtons(currentLayer);
            ProcessDPad(currentLayer);
            ProcessTouchSurfaces(currentLayer);
            ProcessGyro(currentLayer);

            gamepadSync = intermediateState.Dirty;
            ProcessSyncEvents();
            ProcessActionSetLayerChecks();
            SyncKeyboard();
            SyncMouseButtons();
            eventInputHandler?.Sync();

            previousSnapshot = currentSnapshot;
            mapperActionActive = false;
        }

        public override void Stop(bool finalSync = false)
        {
            if (stopped && quit) return;
            stopped = true;
            base.Stop(finalSync);
            currentSnapshot = UniversalControllerStateSnapshot.Disconnected(previousSnapshot.Sequence + 1);
            previousSnapshot = currentSnapshot;
        }

        public override void EstablishForceFeedback()
        {
        }

        public override bool IsButtonActive(JoypadActionCodes code)
        {
            if (code == JoypadActionCodes.AlwaysOn) return true;
            if (TryMapActivationCode(code, out UniversalInputId input))
            {
                if (input == UniversalInputId.LeftTrigger)
                {
                    return TryGetAvailable(input, out UniversalInputValue value) && value.AxisValue > 0.0;
                }

                if (input == UniversalInputId.RightTrigger)
                {
                    return TryGetAvailable(input, out UniversalInputValue value) && value.AxisValue > 0.0;
                }

                return UniversalLegacyBindingMap.IsPressed(currentSnapshot, input) ||
                    TryGetAvailable(input, out UniversalInputValue activeValue) && activeValue.IsActive;
            }

            return false;
        }

        public override bool IsButtonsActiveDraft(IEnumerable<JoypadActionCodes> codes, bool andEval = true)
        {
            bool result = andEval;
            foreach (JoypadActionCodes code in codes ?? Enumerable.Empty<JoypadActionCodes>())
            {
                bool active = IsButtonActive(code);
                if (andEval && !active) return false;
                if (!andEval && active) return true;
                result = active;
            }

            return result;
        }

        public override ref TouchEventFrame GetPreviousTouchEventFrame(TouchpadActionCodes padID)
        {
            switch (padID)
            {
                case TouchpadActionCodes.Touch1:
                    return ref previousLeftTouchFrame;
                case TouchpadActionCodes.Touch2:
                    return ref previousRightTouchFrame;
                case TouchpadActionCodes.Touch3:
                    return ref previousPrimaryTouchFrame;
                default:
                    return ref previousPrimaryTouchFrame;
            }
        }

        public override double GetNormalisedTriggerPosition(TriggerSensitivityModifierTrigger trigger)
        {
            switch (trigger)
            {
                case TriggerSensitivityModifierTrigger.Left:
                    return TryGetAvailable(UniversalInputId.LeftTrigger, out UniversalInputValue left)
                        ? left.AxisValue
                        : 0.0;
                case TriggerSensitivityModifierTrigger.Right:
                    return TryGetAvailable(UniversalInputId.RightTrigger, out UniversalInputValue right)
                        ? right.AxisValue
                        : 0.0;
                default:
                    return 0.0;
            }
        }

        // Replaces the base Mapper's static "old Steam Controller defaults"
        // trigger list with one built from this specific controller's actual
        // capabilities, so activation dropdowns (gyro hold-to-enable/disable,
        // chorded press, etc.) only offer buttons the connected pad really
        // has, under that pad's own labels - e.g. separate Left/Right Stick
        // Click instead of a single ambiguous "Stick Click", and "Cross"
        // rather than "A" on a DualShock/DualSense.
        private void RebuildActionTriggerItems()
        {
            actionTriggerItems.Clear();
            actionTriggerItems.Add(new ActionTriggerItem("Always On", JoypadActionCodes.AlwaysOn));

            ControllerCapabilities capabilities = Controller.Capabilities;
            foreach ((JoypadActionCodes code, UniversalInputId input) in ActivationCandidates)
            {
                if (capabilities?.Supports(input) != true) continue;
                string label = ActivationLabelOverrides.TryGetValue(code, out string overrideLabel)
                    ? overrideLabel
                    : ControllerLabelProvider.GetLabel(input, capabilities);
                actionTriggerItems.Add(new ActionTriggerItem(label, code));
            }
        }

        private void ConfigureUniversalBindings()
        {
            bindingList = UniversalLegacyBindingMap.CreateBindingList().ToList();
            bindingDict.Clear();
            foreach (InputBindingMeta binding in bindingList)
            {
                bindingDict[binding.id] = binding;
            }

            knownStickDefinitions.Clear();
            foreach (KeyValuePair<string, StickDefinition> item in UniversalLegacyBindingMap.CreateStickDefinitions())
            {
                knownStickDefinitions[item.Key] = item.Value;
            }

            knownTriggerDefinitions.Clear();
            foreach (KeyValuePair<string, TriggerDefinition> item in UniversalLegacyBindingMap.CreateTriggerDefinitions())
            {
                knownTriggerDefinitions[item.Key] = item.Value;
            }

            knownTouchpadDefinitions.Clear();
            foreach (KeyValuePair<string, TouchpadDefinition> item in UniversalLegacyBindingMap.CreateTouchpadDefinitions())
            {
                knownTouchpadDefinitions[item.Key] = item.Value;
            }

            knownGyroSensDefinitions.Clear();
            foreach (KeyValuePair<string, GyroSensDefinition> item in UniversalLegacyBindingMap.CreateGyroDefinitions())
            {
                knownGyroSensDefinitions[item.Key] = item.Value;
            }
        }

        private void ProcessActionSetButton(ActionLayer currentLayer)
        {
            if (currentLayer.actionSetActionDict.TryGetValue(
                actionProfile.CurrentActionSet.ActionButtonId,
                out ButtonMapAction currentSetAction))
            {
                currentSetAction.Prepare(this, true);
                if (currentSetAction.active) currentSetAction.Event(this);
            }
        }

        private void ProcessSticks(ActionLayer currentLayer)
        {
            ProcessStick(currentLayer, "LeftStick", UniversalInputId.LeftStick);
            ProcessStick(currentLayer, "RightStick", UniversalInputId.RightStick);
        }

        private void ProcessStick(ActionLayer currentLayer, string bindingId, UniversalInputId input)
        {
            if (!currentLayer.stickActionDict.TryGetValue(bindingId, out StickMapAction action)) return;
            UniversalVector2 vector = TryGetAvailable(input, out UniversalInputValue value)
                ? value.Vector2
                : default;
            action.Prepare(
                this,
                UniversalLegacyBindingMap.ScaleStickAxis(vector.X),
                UniversalLegacyBindingMap.ScaleStickAxis(vector.Y));
            if (action.active) action.Event(this);
        }

        private void ProcessTriggers(ActionLayer currentLayer)
        {
            ProcessTrigger(currentLayer, "LeftTrigger", UniversalInputId.LeftTrigger, UniversalInputId.LeftTriggerFullPull);
            ProcessTrigger(currentLayer, "RightTrigger", UniversalInputId.RightTrigger, UniversalInputId.RightTriggerFullPull);
        }

        private void ProcessTrigger(
            ActionLayer currentLayer,
            string bindingId,
            UniversalInputId axisInput,
            UniversalInputId fullPullInput)
        {
            if (!currentLayer.triggerActionDict.TryGetValue(bindingId, out TriggerMapAction action)) return;
            TriggerEventFrame frame = new TriggerEventFrame
            {
                axisValue = TryGetAvailable(axisInput, out UniversalInputValue value)
                    ? UniversalLegacyBindingMap.ScaleAxisToByte(value.AxisValue)
                    : (short)0,
                fullClick = UniversalLegacyBindingMap.IsPressed(currentSnapshot, fullPullInput),
            };
            action.Prepare(this, ref frame);
            if (action.active) action.Event(this);
        }

        private void ProcessButtons(ActionLayer currentLayer)
        {
            foreach (UniversalRuntimeBinding binding in UniversalLegacyBindingMap.Bindings)
            {
                if (binding.ControlType != InputBindingMeta.InputControlType.Button ||
                    string.Equals(binding.LegacyBindingId, "DPad", StringComparison.Ordinal))
                {
                    continue;
                }

                if (!currentLayer.buttonActionDict.TryGetValue(binding.LegacyBindingId, out ButtonMapAction action))
                {
                    continue;
                }

                bool pressed = UniversalLegacyBindingMap.IsPressed(currentSnapshot, binding.UniversalInput);
                bool wasPressed = UniversalLegacyBindingMap.IsPressed(previousSnapshot, binding.UniversalInput);
                if (pressed || pressed != wasPressed)
                {
                    action.Prepare(this, pressed);
                }

                if (action.active) action.Event(this);
            }
        }

        private void ProcessDPad(ActionLayer currentLayer)
        {
            if (!currentLayer.dpadActionDict.TryGetValue("DPad", out DPadMapAction action)) return;
            DpadDirections dpad = UniversalLegacyBindingMap.ComposeDpad(currentSnapshot);
            DpadDirections previous = UniversalLegacyBindingMap.ComposeDpad(previousSnapshot);
            if (dpad != DpadDirections.Centered || dpad != previous)
            {
                action.Prepare(this, dpad);
            }

            if (action.active) action.Event(this);
        }

        private void ProcessTouchSurfaces(ActionLayer currentLayer)
        {
            ProcessTouchSurface(currentLayer, "PrimaryTouchSurface", UniversalInputId.PrimaryTouchSurface, UniversalInputId.PrimaryTouchSurfaceClick, ref previousPrimaryTouchFrame);
            ProcessTouchSurface(currentLayer, "LeftTouchSurface", UniversalInputId.LeftTouchSurface, UniversalInputId.LeftTouchSurfaceClick, ref previousLeftTouchFrame);
            ProcessTouchSurface(currentLayer, "RightTouchSurface", UniversalInputId.RightTouchSurface, UniversalInputId.RightTouchSurfaceClick, ref previousRightTouchFrame);
        }

        private void ProcessTouchSurface(
            ActionLayer currentLayer,
            string bindingId,
            UniversalInputId surfaceInput,
            UniversalInputId clickInput,
            ref TouchEventFrame previousFrame)
        {
            if (!currentLayer.touchpadActionDict.TryGetValue(bindingId, out TouchpadMapAction action)) return;

            UniversalTouchContact first = null;
            UniversalTouchContact second = null;
            if (TryGetAvailable(surfaceInput, out UniversalInputValue value))
            {
                first = value.Contacts.FirstOrDefault(item => item.Active);
                second = value.Contacts.Where(item => item.Active).Skip(1).FirstOrDefault();
            }

            TouchEventFrame frame = new TouchEventFrame
            {
                X = first != null ? UniversalLegacyBindingMap.ScaleTouchAxis(first.X) : (short)0,
                Y = first != null ? UniversalLegacyBindingMap.ScaleTouchAxis(first.Y) : (short)0,
                X2 = second != null ? UniversalLegacyBindingMap.ScaleTouchAxis(second.X) : (short)0,
                Y2 = second != null ? UniversalLegacyBindingMap.ScaleTouchAxis(second.Y) : (short)0,
                Touch = first != null,
                Click = UniversalLegacyBindingMap.IsPressed(currentSnapshot, clickInput) ||
                    (value != null && value.TouchClickPressed),
                numTouches = (uint)(value?.Contacts.Count(item => item.Active) ?? 0),
                timeElapsed = currentLatency,
            };

            action.Prepare(this, ref frame);
            if (action.active) action.Event(this);
            previousFrame = frame;
        }

        private void ProcessGyro(ActionLayer currentLayer)
        {
            if (!currentLayer.gyroActionDict.TryGetValue("Gyroscope", out GyroMapAction action)) return;

            UniversalVector3 gyro = TryGetAvailable(UniversalInputId.Gyroscope, out UniversalInputValue gyroValue)
                ? gyroValue.Vector3
                : default;
            UniversalVector3 accel = TryGetAvailable(UniversalInputId.Accelerometer, out UniversalInputValue accelValue)
                ? accelValue.Vector3
                : default;
            double gyroYawDegrees = RadiansToDegrees(gyro.Y);
            double gyroPitchDegrees = RadiansToDegrees(gyro.X);
            double gyroRollDegrees = RadiansToDegrees(gyro.Z);

            int gyroYaw = ScaleLegacyGyro(gyroYawDegrees);
            int gyroPitch = ScaleLegacyGyro(gyroPitchDegrees);
            int gyroRoll = ScaleLegacyGyro(gyroRollDegrees);
            int accelX = ScaleLegacyAccel(accel.X);
            int accelY = ScaleLegacyAccel(accel.Y);
            int accelZ = ScaleLegacyAccel(accel.Z);
            gyroCalibration.Update(ref gyroYaw, ref gyroPitch, ref gyroRoll,
                ref accelX, ref accelY, ref accelZ);

            // The calibration offset has to reach the angular velocity fields,
            // not just the legacy integer ones. Gyro mouse, directional swipe
            // and the flick/joystick angular path all read AngGyro*, so leaving
            // those raw meant calibrating the gyro changed nothing a user could
            // feel: the drift it exists to remove went straight through to the
            // pointer. The native reader for the 2015 pad has always subtracted
            // first and derived its angular values from the corrected figure;
            // this is the same order.
            double yawOffsetDegrees = gyroCalibration.GyroOffsetXPrecise / LegacyGyroUnitsPerDegreePerSecond;
            double pitchOffsetDegrees = gyroCalibration.GyroOffsetYPrecise / LegacyGyroUnitsPerDegreePerSecond;
            double rollOffsetDegrees = gyroCalibration.GyroOffsetZPrecise / LegacyGyroUnitsPerDegreePerSecond;

            GyroEventFrame frame = new GyroEventFrame
            {
                GyroYaw = ClampLegacySensor(gyroYaw - gyroCalibration.gyro_offset_x),
                GyroPitch = ClampLegacySensor(gyroPitch - gyroCalibration.gyro_offset_y),
                GyroRoll = ClampLegacySensor(gyroRoll - gyroCalibration.gyro_offset_z),
                AngGyroYaw = gyroYawDegrees - yawOffsetDegrees,
                AngGyroPitch = gyroPitchDegrees - pitchOffsetDegrees,
                AngGyroRoll = gyroRollDegrees - rollOffsetDegrees,
                AccelX = ClampLegacySensor(accelX),
                AccelY = ClampLegacySensor(accelY),
                AccelZ = ClampLegacySensor(accelZ),
                AccelXG = accel.X / 9.80665,
                AccelYG = accel.Y / 9.80665,
                AccelZG = accel.Z / 9.80665,
                timeElapsed = currentLatency,
                elapsedReference = 125.0,
            };

            LastGyroFrameForTest = frame;

            if (action.OutputsNativeGyro) PopulateStateGyro(ref frame);
            else ClearStateGyro();
            action.Prepare(this, ref frame);
            if (action.OutputsNativeGyro && !action.active)
            {
                ClearStateGyro();
            }

            if (action.active) action.Event(this);
        }

        private bool TryGetAvailable(UniversalInputId input, out UniversalInputValue value)
        {
            if (currentSnapshot.TryGetValue(input, out value) &&
                value.Status == UniversalInputValueStatus.Available)
            {
                return true;
            }

            value = null;
            return false;
        }

        // The nominal poll period, used whenever a real interval cannot be
        // worked out.
        internal const double DefaultElapsedSeconds = 1.0 / 125.0;

        // Roughly six poll periods. Anything longer is a stall, a resume from
        // sleep or a clock adjustment, never real controller motion.
        internal const double MaxElapsedSeconds = 0.05;

        internal static double CalculateElapsedSeconds(
            UniversalControllerStateSnapshot current,
            UniversalControllerStateSnapshot previous)
        {
            if (previous == null || previous.Sequence == 0)
            {
                return DefaultElapsedSeconds;
            }

            double elapsed = (current.TimestampUtc - previous.TimestampUtc).TotalSeconds;
            if (elapsed <= 0.0) return DefaultElapsedSeconds;

            // Mouse and gyro output integrate this value, so an unbounded gap
            // is multiplied straight into a pointer movement. Suspending the
            // machine with a stick held off centre produced one frame carrying
            // the whole sleep duration, and the cursor crossed the screen the
            // moment the machine woke up.
            return elapsed > MaxElapsedSeconds ? MaxElapsedSeconds : elapsed;
        }

        private static InputDeviceType ResolveDeviceType(IUniversalController controller)
        {
            if (controller.Identity.BackendName == UniversalControllerBackendIds.SteamControllerNative)
            {
                return InputDeviceType.SteamController;
            }

            string family = controller.DisplayInfo?.GlyphFamily ?? string.Empty;
            string name = controller.DisplayInfo?.DisplayName ?? string.Empty;
            if (string.Equals(family, "playstation", StringComparison.OrdinalIgnoreCase))
            {
                return InputDeviceType.DualSense;
            }

            if (string.Equals(family, "nintendo", StringComparison.OrdinalIgnoreCase))
            {
                return name.IndexOf("joy", StringComparison.OrdinalIgnoreCase) >= 0
                    ? InputDeviceType.JoyCon
                    : InputDeviceType.SwitchPro;
            }

            if (name.IndexOf("8bitdo", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return InputDeviceType.EightBitDoUltimate2Wireless;
            }

            if (name.IndexOf("triton", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("steam controller 2026", StringComparison.OrdinalIgnoreCase) >= 0 ||
                SteamController2026Identity.IsSteamController2026(
                    controller.Identity.DeviceIdentity?.VendorId,
                    controller.Identity.DeviceIdentity?.ProductId))
            {
                return InputDeviceType.SteamControllerTriton;
            }

            return InputDeviceType.None;
        }

        private static bool TryMapActivationCode(JoypadActionCodes code, out UniversalInputId input)
        {
            return ActivationCodeToInput.TryGetValue(code, out input);
        }

        private static double RadiansToDegrees(double radians)
        {
            return radians * 180.0 / Math.PI;
        }

        // Legacy integer sensor units per degree per second. Shared so the
        // scale and its inverse cannot drift apart.
        internal const double LegacyGyroUnitsPerDegreePerSecond = 16.0;

        private static short ScaleLegacyGyro(double degreesPerSecond)
        {
            return (short)Math.Clamp(
                Math.Round(degreesPerSecond * LegacyGyroUnitsPerDegreePerSecond),
                short.MinValue, short.MaxValue);
        }

        private static short ScaleLegacyAccel(double metresPerSecondSquared)
        {
            return (short)Math.Clamp(Math.Round((metresPerSecondSquared / 9.80665) * 16384.0), short.MinValue, short.MaxValue);
        }

        private static short ClampLegacySensor(int value)
        {
            return (short)Math.Clamp(value, short.MinValue, short.MaxValue);
        }
    }
}
