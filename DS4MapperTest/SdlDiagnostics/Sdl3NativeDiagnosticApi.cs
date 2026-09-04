using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SDL3;
using static SDL3.SDL;

namespace DS4MapperTest.SdlDiagnostics
{
    internal sealed class Sdl3NativeDiagnosticApi : ISdlDiagnosticApi
    {
        private static readonly GamepadButton[] DiagnosticButtons =
            Enum.GetValues(typeof(GamepadButton)).Cast<GamepadButton>().Where(value => IsUsableEnumValue(value)).ToArray();

        private static readonly GamepadAxis[] DiagnosticAxes =
            Enum.GetValues(typeof(GamepadAxis)).Cast<GamepadAxis>().Where(value => IsUsableEnumValue(value)).ToArray();

        private static readonly SensorType[] DiagnosticSensors =
        {
            SensorType.Accel,
            SensorType.Gyro,
            SensorType.AccelL,
            SensorType.GyroL,
            SensorType.AccelR,
            SensorType.GyroR,
        };

        // SDL_Init and SDL_Quit are process-wide, and SDL_Quit shuts every
        // subsystem down regardless of how many callers still want them. Two
        // consumers exist - the universal mapping backend and the diagnostics
        // window - so closing the diagnostics window used to tear SDL down
        // underneath the running mapper, which then stopped seeing controller
        // add and remove events entirely. Only the last consumer out quits.
        private static readonly object InitialisationLock = new object();
        private static int initialisedConsumers;

        private bool initialised;

        // Every member below funnels its native work through
        // SdlNativeCallDispatcher so that SDL is initialised, polled and shut
        // down from a single thread. See that class for why Windows controller
        // hotplug depends on it.
        public SdlDiagnosticVersionInfo VersionInfo =>
            SdlNativeCallDispatcher.Invoke(() => new SdlDiagnosticVersionInfo
            {
                BindingName = "SDL3-CS",
                BindingVersion = "3.4.14.1",
                NativeVersion = SafeString(() => GetVersion().ToString()) ?? string.Empty,
                NativeRevision = SafeString(() => GetRevision()) ?? string.Empty,
            });

        public bool Initialise(out string error)
        {
            (bool started, string message) = SdlNativeCallDispatcher.Invoke(() =>
            {
                bool result = InitialiseCore(out string initError);
                return (result, initError);
            });

            error = message;
            return started;
        }

        private bool InitialiseCore(out string error)
        {
            try
            {
                lock (InitialisationLock)
                {
                    if (initialised)
                    {
                        error = string.Empty;
                        return true;
                    }

                    ConfigureControllerDiscoveryHints();
                    if (!Init(InitFlags.Gamepad | InitFlags.Joystick | InitFlags.Sensor))
                    {
                        error = SafeGetError();
                        return false;
                    }

                    initialised = true;
                    initialisedConsumers++;
                }

                lock (EventPumpLock)
                {
                    if (!EventSubscribers.Contains(pendingEvents))
                    {
                        EventSubscribers.Add(pendingEvents);
                    }
                }

                error = string.Empty;
                return true;
            }
            catch (DllNotFoundException ex)
            {
                error = $"SDL3 native library could not be loaded. Expected {SdlNativeLibraryLocator.NativeLibraryFileName} in the application output. {ex.Message}";
                return false;
            }
            catch (BadImageFormatException ex)
            {
                error = $"SDL3 native library has the wrong architecture for this x64 build. {ex.Message}";
                return false;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public void Shutdown()
        {
            SdlNativeCallDispatcher.Invoke(ShutdownCore);
        }

        private void ShutdownCore()
        {
            lock (EventPumpLock)
            {
                EventSubscribers.Remove(pendingEvents);
                pendingEvents.Clear();
            }

            lock (InitialisationLock)
            {
                if (!initialised) return;

                initialised = false;
                initialisedConsumers--;
                if (initialisedConsumers <= 0)
                {
                    initialisedConsumers = 0;
                    Quit();
                }
            }
        }

        public IReadOnlyList<uint> EnumerateGamepads(out string error)
        {
            (IReadOnlyList<uint> ids, string message) = SdlNativeCallDispatcher.Invoke(() =>
            {
                IReadOnlyList<uint> result = EnumerateGamepadsCore(out string enumError);
                return (result, enumError);
            });

            error = message;
            return ids;
        }

        private IReadOnlyList<uint> EnumerateGamepadsCore(out string error)
        {
            error = string.Empty;
            UpdateGamepads();
            RefreshJoysticks();

            uint[] gamepadIds = SDL.GetGamepads(out int gamepadCount);
            uint[] joystickIds = SDL.GetJoysticks(out int joystickCount);
            IReadOnlyList<uint> ids = MergeGamepadInstanceIds(gamepadIds, gamepadCount, joystickIds, joystickCount, IsGamepad);
            if (ids.Count == 0 && gamepadIds == null && joystickIds == null)
            {
                error = SafeGetError();
            }

            return ids;
        }

        public SdlGamepadHandle OpenGamepad(uint instanceId, out string error)
        {
            (SdlGamepadHandle handle, string message) = SdlNativeCallDispatcher.Invoke(() =>
            {
                IntPtr nativeHandle = SDL.OpenGamepad(instanceId);
                string openError = nativeHandle == IntPtr.Zero ? SafeGetError() : string.Empty;
                return (new SdlGamepadHandle(nativeHandle), openError);
            });

            error = message;
            return handle;
        }

        public void CloseGamepad(SdlGamepadHandle handle)
        {
            if (handle.IsNull) return;
            SdlNativeCallDispatcher.Invoke(() => SDL.CloseGamepad(handle.NativeHandle));
        }

        public SdlRawGamepadInfo QueryGamepadInfo(uint instanceId, SdlGamepadHandle handle)
        {
            return SdlNativeCallDispatcher.Invoke(() => QueryGamepadInfoCore(instanceId, handle));
        }

        private SdlRawGamepadInfo QueryGamepadInfoCore(uint instanceId, SdlGamepadHandle handle)
        {
            SdlRawGamepadInfo info = new SdlRawGamepadInfo
            {
                InstanceId = instanceId,
                Name = SafeString(() => GetGamepadName(handle.NativeHandle)) ?? SafeString(() => GetGamepadNameForID(instanceId)) ?? string.Empty,
                MappingName = SafeString(() => GetGamepadMappingForID(instanceId)) ?? string.Empty,
                Guid = GuidToText(GetGamepadGUIDForID(instanceId)),
                VendorId = ZeroToNull(SafeUShort(() => GetGamepadVendor(handle.NativeHandle))),
                ProductId = ZeroToNull(SafeUShort(() => GetGamepadProduct(handle.NativeHandle))),
                ProductVersion = ZeroToNull(SafeUShort(() => GetGamepadProductVersion(handle.NativeHandle))),
                SerialNumber = SafeString(() => GetGamepadSerial(handle.NativeHandle)) ?? string.Empty,
                DevicePath = SafeString(() => GetGamepadPath(handle.NativeHandle)) ?? string.Empty,
                ConnectionType = SafeString(() => GetGamepadConnectionState(handle.NativeHandle).ToString()) ?? string.Empty,
                PlayerIndex = NegativeToNull(SafeInt(() => GetGamepadPlayerIndex(handle.NativeHandle), -1)),
                BatteryPercent = QueryBatteryPercent(handle),
                BatteryState = QueryBatteryState(handle),
                IsMappedGamepad = SafeBool(() => IsGamepad(instanceId)),
            };

            info.Buttons = DiagnosticButtons.Select(button => CreateButtonState(handle, button)).ToList();
            info.Axes = DiagnosticAxes.Select(axis => CreateAxisState(handle, axis)).ToList();
            info.Touchpads = CreateTouchpadStates(handle);
            info.Sensors = CreateSensorStates(handle);
            info.BestEffortPersistentKey = CreateBestEffortPersistentKey(info);
            info.IdentityNotes = "SDL joystick instance IDs are session-local. The persistent key is best-effort and may not distinguish identical controllers when serial/path data is unavailable.";
            return info;
        }

        // SDL keeps one process-wide event queue and hands each event to
        // whoever polls first. Both the universal mapping backend and the
        // diagnostics window own an instance of this class and poll it from
        // their own threads, so opening the diagnostics window used to steal
        // controller add and remove events from the running mapper at random.
        //
        // Draining is now done once under a lock and the result copied to every
        // live consumer, so each of them sees the full stream.
        private static readonly object EventPumpLock = new object();
        private static readonly List<Queue<SdlDiagnosticEvent>> EventSubscribers =
            new List<Queue<SdlDiagnosticEvent>>();
        // A consumer that stops polling (a diagnostics window left open with
        // its refresh timer stopped) must not grow its queue without bound.
        private const int MaxQueuedEventsPerConsumer = 512;
        private readonly Queue<SdlDiagnosticEvent> pendingEvents =
            new Queue<SdlDiagnosticEvent>();

        public bool PollEvent(out SdlDiagnosticEvent diagnosticEvent)
        {
            // Callers drain in a loop, and one drain of SDL's queue fills this
            // consumer's backlog in full. Serving that backlog without going
            // near SDL keeps a controller reporting motion at several hundred
            // hertz from costing a thread handoff per event.
            SdlDiagnosticEvent next = TryDequeuePendingEvent();
            next ??= SdlNativeCallDispatcher.Invoke(() =>
            {
                lock (EventPumpLock)
                {
                    if (pendingEvents.Count == 0) DrainSdlEventQueue();
                    return pendingEvents.Count > 0 ? pendingEvents.Dequeue() : null;
                }
            });

            diagnosticEvent = next;
            return next != null;
        }

        private SdlDiagnosticEvent TryDequeuePendingEvent()
        {
            lock (EventPumpLock)
            {
                return pendingEvents.Count > 0 ? pendingEvents.Dequeue() : null;
            }
        }

        // Always called with EventPumpLock held.
        private static void DrainSdlEventQueue()
        {
            SdlDiagnosticEvent diagnosticEvent;
            while (TranslateNextEvent(out diagnosticEvent))
            {
                foreach (Queue<SdlDiagnosticEvent> subscriber in EventSubscribers)
                {
                    if (subscriber.Count >= MaxQueuedEventsPerConsumer)
                    {
                        subscriber.Dequeue();
                    }

                    subscriber.Enqueue(diagnosticEvent);
                }
            }
        }

        private static bool TranslateNextEvent(out SdlDiagnosticEvent diagnosticEvent)
        {
            diagnosticEvent = null;
            while (SDL.PollEvent(out Event sdlEvent))
            {
                EventType eventType = (EventType)sdlEvent.Type;
                diagnosticEvent = eventType switch
                {
                    EventType.GamepadAdded => new SdlDiagnosticEvent { Kind = SdlDiagnosticInputEventKind.DeviceAdded, InstanceId = sdlEvent.GDevice.Which },
                    EventType.GamepadRemoved => new SdlDiagnosticEvent { Kind = SdlDiagnosticInputEventKind.DeviceRemoved, InstanceId = sdlEvent.GDevice.Which },
                    EventType.GamepadRemapped => new SdlDiagnosticEvent { Kind = SdlDiagnosticInputEventKind.DeviceRemapped, InstanceId = sdlEvent.GDevice.Which },
                    EventType.GamepadButtonDown or EventType.GamepadButtonUp => new SdlDiagnosticEvent { Kind = SdlDiagnosticInputEventKind.ButtonChanged, InstanceId = sdlEvent.GButton.Which, ControlIndex = Convert.ToInt32(sdlEvent.GButton.Button), ControlName = sdlEvent.GButton.Button.ToString(), ButtonPressed = sdlEvent.GButton.Down },
                    EventType.GamepadAxisMotion => new SdlDiagnosticEvent { Kind = SdlDiagnosticInputEventKind.AxisChanged, InstanceId = sdlEvent.GAxis.Which, ControlIndex = Convert.ToInt32(sdlEvent.GAxis.Axis), ControlName = sdlEvent.GAxis.Axis.ToString(), AxisValue = sdlEvent.GAxis.Value },
                    EventType.GamepadTouchpadDown or EventType.GamepadTouchpadMotion or EventType.GamepadTouchpadUp => new SdlDiagnosticEvent { Kind = SdlDiagnosticInputEventKind.TouchpadChanged, InstanceId = sdlEvent.GTouchpad.Which, TouchpadIndex = sdlEvent.GTouchpad.Touchpad, FingerIndex = sdlEvent.GTouchpad.Finger, TouchActive = eventType != EventType.GamepadTouchpadUp, X = sdlEvent.GTouchpad.X, Y = sdlEvent.GTouchpad.Y, Pressure = sdlEvent.GTouchpad.Pressure },
                    EventType.GamepadSensorUpdate => new SdlDiagnosticEvent { Kind = SdlDiagnosticInputEventKind.SensorChanged, InstanceId = sdlEvent.GSensor.Which, SensorName = sdlEvent.GSensor.Sensor.ToString(), SensorValues = Array.Empty<float>() },
                    _ => null,
                };

                if (diagnosticEvent != null) return true;
            }

            return false;
        }

        public void RefreshGamepads() => SdlNativeCallDispatcher.Invoke(() => UpdateGamepads());
        private static void RefreshJoysticks() => UpdateJoysticks();
        public void RefreshSensors() => SdlNativeCallDispatcher.Invoke(() => UpdateSensors());

        internal static IReadOnlyList<uint> MergeGamepadInstanceIds(
            uint[] gamepadIds,
            int gamepadCount,
            uint[] joystickIds,
            int joystickCount,
            Func<uint, bool> isGamepad)
        {
            HashSet<uint> ids = new HashSet<uint>();
            AddReportedIds(ids, gamepadIds, gamepadCount, null);
            AddReportedIds(ids, joystickIds, joystickCount, isGamepad);
            return ids.OrderBy(id => id).ToArray();
        }

        private static void AddReportedIds(
            HashSet<uint> target,
            uint[] source,
            int count,
            Func<uint, bool> predicate)
        {
            if (target == null || source == null || count <= 0)
            {
                return;
            }

            int limit = Math.Min(count, source.Length);
            for (int index = 0; index < limit; index++)
            {
                uint id = source[index];
                if (predicate == null || SafeBool(() => predicate(id)))
                {
                    target.Add(id);
                }
            }
        }

        private static void ConfigureControllerDiscoveryHints()
        {
            SetHint(Hints.XInputEnabled, "1");
            SetHint(Hints.JoystickWGI, "1");
            SetHint(Hints.JoystickHIDAPI, "1");
            SetHint(Hints.JoystickHIDAPIXbox, "1");
            SetHint(Hints.JoystickHIDAPIXbox360, "1");
            SetHint(Hints.JoystickHIDAPIXbox360Wireless, "1");
            SetHint(Hints.JoystickHIDAPIXboxOne, "1");
        }

        public void RefreshLiveState(SdlGamepadHandle handle, SdlRawGamepadInfo info)
        {
            SdlNativeCallDispatcher.Invoke(() => RefreshLiveStateCore(handle, info));
        }

        private static void RefreshLiveStateCore(SdlGamepadHandle handle, SdlRawGamepadInfo info)
        {
            foreach (SdlRawButtonState button in info.Buttons.Where(item => item.Supported))
            {
                bool pressed = GetGamepadButton(handle.NativeHandle, (GamepadButton)button.Index);
                if (button.Pressed != pressed)
                {
                    button.Pressed = pressed;
                    button.LastChangedUtc = DateTimeOffset.UtcNow;
                }
            }

            foreach (SdlRawAxisState axis in info.Axes.Where(item => item.Supported))
            {
                short rawValue = GetGamepadAxis(handle.NativeHandle, (GamepadAxis)axis.Index);
                if (axis.RawValue != rawValue)
                {
                    axis.RawValue = rawValue;
                    axis.NormalizedValue = SdlDiagnosticValueNormalizer.NormalizeAxis(rawValue);
                    axis.LastChangedUtc = DateTimeOffset.UtcNow;
                }
            }

            RefreshTouchpads(handle, info);
            RefreshSensorValues(handle, info);
            int? batteryPercent = QueryBatteryPercent(handle);
            if (batteryPercent != info.BatteryPercent)
            {
                info.BatteryPercent = batteryPercent;
            }
            info.BatteryState = QueryBatteryState(handle);
        }

        private static int? QueryBatteryPercent(SdlGamepadHandle handle)
        {
            int percent = -1;
            try
            {
                PowerState state = GetGamepadPowerInfo(handle.NativeHandle, out percent);
                return state == PowerState.Error || state == PowerState.Unknown || percent < 0 || percent > 100
                    ? null
                    : percent;
            }
            catch
            {
                return null;
            }
        }

        private static string QueryBatteryState(SdlGamepadHandle handle)
        {
            int percent = -1;
            try
            {
                return GetGamepadPowerInfo(handle.NativeHandle, out percent).ToString();
            }
            catch
            {
                return string.Empty;
            }
        }

        private static SdlRawButtonState CreateButtonState(SdlGamepadHandle handle, GamepadButton button)
        {
            bool supported = SafeBool(() => GamepadHasButton(handle.NativeHandle, button));
            return new SdlRawButtonState { Index = Convert.ToInt32(button), Name = button.ToString(), Supported = supported, Pressed = supported && SafeBool(() => GetGamepadButton(handle.NativeHandle, button)) };
        }

        private static SdlRawAxisState CreateAxisState(SdlGamepadHandle handle, GamepadAxis axis)
        {
            bool supported = SafeBool(() => GamepadHasAxis(handle.NativeHandle, axis));
            short raw = supported ? SafeShort(() => GetGamepadAxis(handle.NativeHandle, axis)) : (short)0;
            return new SdlRawAxisState { Index = Convert.ToInt32(axis), Name = axis.ToString(), Supported = supported, RawValue = raw, NormalizedValue = SdlDiagnosticValueNormalizer.NormalizeAxis(raw) };
        }

        private static List<SdlRawTouchpadState> CreateTouchpadStates(SdlGamepadHandle handle)
        {
            int count = Math.Max(0, SafeInt(() => GetNumGamepadTouchpads(handle.NativeHandle), 0));
            List<SdlRawTouchpadState> touchpads = new List<SdlRawTouchpadState>(count);
            for (int touchpadIndex = 0; touchpadIndex < count; touchpadIndex++)
            {
                int fingerCount = Math.Max(0, SafeInt(() => GetNumGamepadTouchpadFingers(handle.NativeHandle, touchpadIndex), 0));
                SdlRawTouchpadState touchpad = new SdlRawTouchpadState { TouchpadIndex = touchpadIndex, FingerCapacity = fingerCount };
                for (int fingerIndex = 0; fingerIndex < fingerCount; fingerIndex++)
                {
                    SdlRawTouchFingerState finger = new SdlRawTouchFingerState { FingerIndex = fingerIndex };
                    UpdateTouchFinger(handle, touchpadIndex, finger);
                    touchpad.Fingers.Add(finger);
                }
                touchpads.Add(touchpad);
            }
            return touchpads;
        }

        private static List<SdlRawSensorState> CreateSensorStates(SdlGamepadHandle handle)
        {
            List<SdlRawSensorState> sensors = new List<SdlRawSensorState>();
            foreach (SensorType sensorType in DiagnosticSensors)
            {
                bool supported = SafeBool(() => GamepadHasSensor(handle.NativeHandle, sensorType));
                SdlRawSensorState sensor = new SdlRawSensorState { Name = sensorType.ToString(), Supported = supported, Units = SensorUnits(sensorType) };
                if (supported)
                {
                    sensor.EnableAttempted = true;
                    sensor.EnableSucceeded = SafeBool(() => SetGamepadSensorEnabled(handle.NativeHandle, sensorType, true));
                    sensor.Enabled = SafeBool(() => GamepadSensorEnabled(handle.NativeHandle, sensorType));
                    sensor.DataRateHz = SafeFloat(() => GetGamepadSensorDataRate(handle.NativeHandle, sensorType), 0);
                    if (!sensor.EnableSucceeded) sensor.LastError = SafeGetError();
                }
                sensors.Add(sensor);
            }
            return sensors;
        }

        private static void RefreshTouchpads(SdlGamepadHandle handle, SdlRawGamepadInfo info)
        {
            foreach (SdlRawTouchpadState touchpad in info.Touchpads)
            {
                foreach (SdlRawTouchFingerState finger in touchpad.Fingers)
                {
                    UpdateTouchFinger(handle, touchpad.TouchpadIndex, finger);
                }
            }
        }

        private static void UpdateTouchFinger(SdlGamepadHandle handle, int touchpadIndex, SdlRawTouchFingerState finger)
        {
            if (GetGamepadTouchpadFinger(handle.NativeHandle, touchpadIndex, finger.FingerIndex, out bool down, out float x, out float y, out float pressure))
            {
                finger.Active = down;
                finger.X = x;
                finger.Y = y;
                finger.Pressure = pressure;
                finger.LastChangedUtc = DateTimeOffset.UtcNow;
                finger.LastError = string.Empty;
            }
            else
            {
                finger.LastError = SafeGetError();
            }
        }

        private static void RefreshSensorValues(SdlGamepadHandle handle, SdlRawGamepadInfo info)
        {
            foreach (SdlRawSensorState sensor in info.Sensors.Where(item => item.Supported && item.Enabled))
            {
                SensorType sensorType = (SensorType)Enum.Parse(typeof(SensorType), sensor.Name);
                float[] values = new float[3];
                if (GetGamepadSensorData(handle.NativeHandle, sensorType, values, values.Length))
                {
                    sensor.Values = values;
                    sensor.LastChangedUtc = DateTimeOffset.UtcNow;
                    sensor.LastError = string.Empty;
                }
                else
                {
                    sensor.LastError = SafeGetError();
                }
            }
        }

        private static string CreateBestEffortPersistentKey(SdlRawGamepadInfo info)
        {
            return string.Join("|",
                string.IsNullOrWhiteSpace(info.Guid) ? "guid-unknown" : $"guid-{info.Guid}",
                info.VendorId.HasValue ? $"vid-{info.VendorId.Value:X4}" : "vid-unknown",
                info.ProductId.HasValue ? $"pid-{info.ProductId.Value:X4}" : "pid-unknown",
                string.IsNullOrWhiteSpace(info.SerialNumber) ? "serial-unknown" : $"serial-{info.SerialNumber}");
        }

        private static string GuidToText(GUID guid)
        {
            byte[] buffer = new byte[33];
            GUIDToString(guid, buffer, buffer.Length);
            return Encoding.UTF8.GetString(buffer).TrimEnd('\0');
        }

        private static bool IsUsableEnumValue(Enum value)
        {
            string name = value.ToString();
            return !string.Equals(name, "Invalid", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(name, "Count", StringComparison.OrdinalIgnoreCase);
        }

        private static string SensorUnits(SensorType sensorType)
        {
            string name = sensorType.ToString();
            if (name.StartsWith("Accel", StringComparison.OrdinalIgnoreCase)) return "m/s^2";
            if (name.StartsWith("Gyro", StringComparison.OrdinalIgnoreCase)) return "radians/s";
            return string.Empty;
        }

        private static ushort? ZeroToNull(ushort value) => value == 0 ? null : value;
        private static int? NegativeToNull(int value) => value < 0 ? null : value;
        private static string SafeGetError() { try { return GetError() ?? string.Empty; } catch (Exception ex) { return ex.Message; } }
        private static bool SafeBool(Func<bool> getter) { try { return getter(); } catch { return false; } }
        private static short SafeShort(Func<short> getter) { try { return getter(); } catch { return 0; } }
        private static ushort SafeUShort(Func<ushort> getter) { try { return getter(); } catch { return 0; } }
        private static int SafeInt(Func<int> getter, int fallback) { try { return getter(); } catch { return fallback; } }
        private static float SafeFloat(Func<float> getter, float fallback) { try { return getter(); } catch { return fallback; } }
        private static string SafeString(Func<string> getter) { try { return getter(); } catch { return null; } }
    }
}
