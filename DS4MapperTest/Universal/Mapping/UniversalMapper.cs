using DS4MapperTest.ButtonActions;
using DS4MapperTest.Common;
using DS4MapperTest.DPadActions;
using DS4MapperTest.GyroActions;
using DS4MapperTest.MapperUtil;
using DS4MapperTest.StickActions;
using DS4MapperTest.TouchpadActions;
using DS4MapperTest.TriggerActions;
using DS4MapperTest.Universal.Profiles;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DS4MapperTest.Universal.Mapping
{
    public sealed class UniversalMapper : Mapper
    {
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
            ActivateProfile(profile);
        }

        public IUniversalController Controller { get; }
        public UniversalCompiledProfile CompiledProfile { get; private set; }
        public override DeviceReaderBase BaseReader => null;
        public InputDeviceType DeviceTypeOverride { get; private set; }
        public override InputDeviceType DeviceType => DeviceTypeOverride;
        public GyroCalibrationStatus GyroCalibrationStatus => gyroCalibration.Status;

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

            GyroEventFrame frame = new GyroEventFrame
            {
                GyroYaw = ClampLegacySensor(gyroYaw - gyroCalibration.gyro_offset_x),
                GyroPitch = ClampLegacySensor(gyroPitch - gyroCalibration.gyro_offset_y),
                GyroRoll = ClampLegacySensor(gyroRoll - gyroCalibration.gyro_offset_z),
                AngGyroYaw = gyroYawDegrees,
                AngGyroPitch = gyroPitchDegrees,
                AngGyroRoll = gyroRollDegrees,
                AccelX = ClampLegacySensor(accelX),
                AccelY = ClampLegacySensor(accelY),
                AccelZ = ClampLegacySensor(accelZ),
                AccelXG = accel.X / 9.80665,
                AccelYG = accel.Y / 9.80665,
                AccelZG = accel.Z / 9.80665,
                timeElapsed = currentLatency,
                elapsedReference = 125.0,
            };

            if (action.OutputsNativeGyro) PopulateStateGyro(ref frame);
            else ClearStateGyro();
            action.Prepare(this, ref frame);
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

        private static double CalculateElapsedSeconds(
            UniversalControllerStateSnapshot current,
            UniversalControllerStateSnapshot previous)
        {
            if (previous == null || previous.Sequence == 0)
            {
                return 1.0 / 125.0;
            }

            double elapsed = (current.TimestampUtc - previous.TimestampUtc).TotalSeconds;
            return elapsed > 0.0 ? elapsed : 1.0 / 125.0;
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
                name.IndexOf("steam controller 2026", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return InputDeviceType.SteamControllerTriton;
            }

            return InputDeviceType.None;
        }

        private static bool TryMapActivationCode(JoypadActionCodes code, out UniversalInputId input)
        {
            switch (code)
            {
                case JoypadActionCodes.BtnSouth:
                    input = UniversalInputId.FaceButtonSouth;
                    return true;
                case JoypadActionCodes.BtnEast:
                    input = UniversalInputId.FaceButtonEast;
                    return true;
                case JoypadActionCodes.BtnNorth:
                    input = UniversalInputId.FaceButtonNorth;
                    return true;
                case JoypadActionCodes.BtnWest:
                    input = UniversalInputId.FaceButtonWest;
                    return true;
                case JoypadActionCodes.BtnLShoulder:
                    input = UniversalInputId.LeftShoulder;
                    return true;
                case JoypadActionCodes.BtnRShoulder:
                    input = UniversalInputId.RightShoulder;
                    return true;
                case JoypadActionCodes.BtnStart:
                    input = UniversalInputId.Menu;
                    return true;
                case JoypadActionCodes.BtnSelect:
                    input = UniversalInputId.View;
                    return true;
                case JoypadActionCodes.BtnMode:
                    input = UniversalInputId.System;
                    return true;
                case JoypadActionCodes.BtnThumbL:
                    input = UniversalInputId.LeftStickClick;
                    return true;
                case JoypadActionCodes.BtnThumbR:
                    input = UniversalInputId.RightStickClick;
                    return true;
                case JoypadActionCodes.AxisLTrigger:
                    input = UniversalInputId.LeftTrigger;
                    return true;
                case JoypadActionCodes.AxisRTrigger:
                    input = UniversalInputId.RightTrigger;
                    return true;
                case JoypadActionCodes.LPadTouch:
                    input = UniversalInputId.LeftTouchSurface;
                    return true;
                case JoypadActionCodes.RPadTouch:
                    input = UniversalInputId.RightTouchSurface;
                    return true;
                case JoypadActionCodes.LPadClick:
                    input = UniversalInputId.LeftTouchSurfaceClick;
                    return true;
                case JoypadActionCodes.RPadClick:
                    input = UniversalInputId.RightTouchSurfaceClick;
                    return true;
                case JoypadActionCodes.LTFullPull:
                    input = UniversalInputId.LeftTriggerFullPull;
                    return true;
                case JoypadActionCodes.RTFullPull:
                    input = UniversalInputId.RightTriggerFullPull;
                    return true;
                default:
                    input = default;
                    return false;
            }
        }

        private static double RadiansToDegrees(double radians)
        {
            return radians * 180.0 / Math.PI;
        }

        private static short ScaleLegacyGyro(double degreesPerSecond)
        {
            return (short)Math.Clamp(Math.Round(degreesPerSecond * 16.0), short.MinValue, short.MaxValue);
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
