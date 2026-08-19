using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using DS4MapperTest.ButtonActions;
using DS4MapperTest.TriggerActions;
using DS4MapperTest.TouchpadActions;
using DS4MapperTest.StickActions;
using DS4MapperTest.GyroActions;
using DS4MapperTest.DPadActions;
using DS4MapperTest.MapperUtil;
using DS4MapperTest.SteamControllerLibrary;
using System.Windows.Media;
using DS4MapperTest.ViewModels.Common;
using DS4MapperTest.Universal;
using DS4MapperTest.Universal.Editor;
using DS4MapperTest.Universal.Mapping;

namespace DS4MapperTest.ViewModels
{
    public class ProfileEditorTestViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private void RaisePropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private ManualResetEventSlim actionResetEvent = new ManualResetEventSlim(false);
        public ManualResetEventSlim ActionResetEvent => actionResetEvent;

        private Mapper mapper;
        public Mapper DeviceMapper
        {
            get => mapper;
        }

        private ProfileEntity profileEnt;
        public ProfileEntity ProfileEnt
        {
            get => profileEnt;
        }

        private Profile tempProfile;
        public Profile CurrentProfile
        {
            get => tempProfile;
        }

        private bool suppressDirtyTracking;
        private bool refreshingDirtyState;
        private string savedProfileFingerprint;

        public bool IsProfileDirty => tempProfile != null && !string.Equals(
            savedProfileFingerprint, CreateProfileFingerprint(), StringComparison.Ordinal);

        public bool SupportsLightbar =>
            mapper?.DeviceType == InputDeviceType.DS4 ||
            mapper?.DeviceType == InputDeviceType.DualSense;

        public void MarkProfileDirty()
        {
            if (suppressDirtyTracking || tempProfile == null) return;
            RefreshProfileDirtyState();
        }

        private void RefreshProfileDirtyState()
        {
            if (tempProfile == null) return;
            bool isDirty = IsProfileDirty;
            if (tempProfile.Dirty != isDirty)
            {
                refreshingDirtyState = true;
                try
                {
                    tempProfile.Dirty = isDirty;
                }
                finally
                {
                    refreshingDirtyState = false;
                }
            }
            RaisePropertyChanged(nameof(IsProfileDirty));
        }

        public void MarkProfileClean()
        {
            if (tempProfile == null) return;
            savedProfileFingerprint = CreateProfileFingerprint();
            RefreshProfileDirtyState();
        }

        public void RestoreProfileDirtyState(bool isDirty)
        {
            if (tempProfile == null) return;
            tempProfile.Dirty = isDirty;
            RaisePropertyChanged(nameof(IsProfileDirty));
        }

        public IDisposable SuppressDirtyTracking()
        {
            return new DirtyTrackingScope(this);
        }

        private sealed class DirtyTrackingScope : IDisposable
        {
            private ProfileEditorTestViewModel owner;
            private readonly IDisposable mapperScope;

            public DirtyTrackingScope(ProfileEditorTestViewModel owner)
            {
                this.owner = owner;
                owner.suppressDirtyTracking = true;
                mapperScope = owner.mapper.SuppressProfileDirtyTracking();
            }

            public void Dispose()
            {
                if (owner == null) return;
                mapperScope?.Dispose();
                owner.suppressDirtyTracking = false;
                owner = null;
            }
        }

        public string ProfileName
        {
            get => tempProfile.Name;
            set
            {
                if (tempProfile.Name == value) return;
                tempProfile.Name = value;
                MarkProfileDirty();
            }
        }

        public void SetProfileNameWithoutDirty(string value)
        {
            if (tempProfile.Name == value) return;
            bool wasDirty = tempProfile.Dirty;
            using (SuppressDirtyTracking())
            {
                tempProfile.Name = value;
            }
            if (!wasDirty)
            {
                savedProfileFingerprint = CreateProfileFingerprint();
            }
            RefreshProfileDirtyState();
            RaisePropertyChanged(nameof(ProfileName));
        }

        private List<BindingItemsTest> buttonBindings = new List<BindingItemsTest>();
        public List<BindingItemsTest> ButtonBindings
        {
            get => buttonBindings;
        }

        private ObservableCollection<FaceButtonBindingItem> faceButtonBindings =
            new ObservableCollection<FaceButtonBindingItem>();
        public ObservableCollection<FaceButtonBindingItem> FaceButtonBindings => faceButtonBindings;

        private ObservableCollection<FaceButtonBindingItem> bumperButtonBindings =
            new ObservableCollection<FaceButtonBindingItem>();
        public ObservableCollection<FaceButtonBindingItem> BumperButtonBindings => bumperButtonBindings;

        private ObservableCollection<FaceButtonBindingItem> centerButtonBindings =
            new ObservableCollection<FaceButtonBindingItem>();
        public ObservableCollection<FaceButtonBindingItem> CenterButtonBindings => centerButtonBindings;

        private ObservableCollection<FaceButtonBindingItem> paddleButtonBindings =
            new ObservableCollection<FaceButtonBindingItem>();
        public ObservableCollection<FaceButtonBindingItem> PaddleButtonBindings => paddleButtonBindings;

        private ObservableCollection<FaceButtonBindingItem> leftStickClickBinding =
            new ObservableCollection<FaceButtonBindingItem>();
        public ObservableCollection<FaceButtonBindingItem> LeftStickClickBinding => leftStickClickBinding;

        private ObservableCollection<FaceButtonBindingItem> rightStickClickBinding =
            new ObservableCollection<FaceButtonBindingItem>();
        public ObservableCollection<FaceButtonBindingItem> RightStickClickBinding => rightStickClickBinding;

        private ObservableCollection<FaceButtonBindingItem> leftStickTouchBinding =
            new ObservableCollection<FaceButtonBindingItem>();
        public ObservableCollection<FaceButtonBindingItem> LeftStickTouchBinding => leftStickTouchBinding;

        private ObservableCollection<FaceButtonBindingItem> rightStickTouchBinding =
            new ObservableCollection<FaceButtonBindingItem>();
        public ObservableCollection<FaceButtonBindingItem> RightStickTouchBinding => rightStickTouchBinding;

        private ObservableCollection<FaceButtonBindingItem> extraButtonBindings =
            new ObservableCollection<FaceButtonBindingItem>();
        public ObservableCollection<FaceButtonBindingItem> ExtraButtonBindings => extraButtonBindings;

        private ObservableCollection<FaceButtonBindingItem> touchpadButtonBindings =
            new ObservableCollection<FaceButtonBindingItem>();
        public ObservableCollection<FaceButtonBindingItem> TouchpadButtonBindings => touchpadButtonBindings;

        private ObservableCollection<FaceButtonBindingItem> touchpadTouchBindings =
            new ObservableCollection<FaceButtonBindingItem>();
        public ObservableCollection<FaceButtonBindingItem> TouchpadTouchBindings => touchpadTouchBindings;

        private ObservableCollection<FaceButtonBindingItem> touchpadPressBindings =
            new ObservableCollection<FaceButtonBindingItem>();
        public ObservableCollection<FaceButtonBindingItem> TouchpadPressBindings => touchpadPressBindings;

        private Dictionary<string, FaceButtonBindingItem> touchpadButtonBindingItems =
            new Dictionary<string, FaceButtonBindingItem>(StringComparer.OrdinalIgnoreCase);

        private StickSideViewModel leftStickKeybinds;
        public StickSideViewModel LeftStickKeybinds => leftStickKeybinds ??= new StickSideViewModel(this, "LS");

        private StickSideViewModel rightStickKeybinds;
        public StickSideViewModel RightStickKeybinds => rightStickKeybinds ??= new StickSideViewModel(this, "RS");

        private GyroCalibrationViewModel gyroCalibVM;
        public GyroCalibrationViewModel GyroCalibVM => gyroCalibVM ??= new GyroCalibrationViewModel(mapper);

        private ObservableCollection<TriggerKeybindItem> triggerKeybinds =
            new ObservableCollection<TriggerKeybindItem>();
        public ObservableCollection<TriggerKeybindItem> TriggerKeybinds => triggerKeybinds;

        private DPadKeybindsViewModel dpadKeybinds;
        public DPadKeybindsViewModel DPadKeybinds => dpadKeybinds ??= new DPadKeybindsViewModel(this);

        private Dictionary<string, int> buttonBindingsIndexDict =
            new Dictionary<string, int>();
        public Dictionary<string, int> ButtonBindingsIndexDict
        {
            get => buttonBindingsIndexDict;
        }

        // Tracks which backend button bindings have been claimed by a named
        // section so the Extra fallback never shows a duplicate card
        private HashSet<string> claimedButtonBindings =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private ObservableCollection<TouchBindingItemsTest> touchpadBindings =
            new ObservableCollection<TouchBindingItemsTest>();
        public ObservableCollection<TouchBindingItemsTest> TouchpadBindings
        {
            get => touchpadBindings;
        }

        private ObservableCollection<TouchBindingItemsTest> touchpadTouchSurfaceBindings =
            new ObservableCollection<TouchBindingItemsTest>();
        public ObservableCollection<TouchBindingItemsTest> TouchpadTouchSurfaceBindings =>
            touchpadTouchSurfaceBindings;

        private SteamControllerPadRotationViewModel steamPadRotation;
        public SteamControllerPadRotationViewModel SteamPadRotation =>
            steamPadRotation ??= SteamControllerPadRotationViewModel.Create(mapper);

        public bool HasSteamPadRotation => SteamPadRotation != null;

        private PhysicalControllerVisibilityViewModel physicalControllerVisibility;
        public PhysicalControllerVisibilityViewModel PhysicalControllerVisibility =>
            physicalControllerVisibility ??= PhysicalControllerVisibilityViewModel.Create(mapper);

        public bool HasPhysicalControllerVisibility => PhysicalControllerVisibility != null;

        public bool UsesPlayStationTouchpadClickNames => IsPlayStationController;

        public bool IsNintendoController =>
            string.Equals((mapper as UniversalMapper)?.Controller.DisplayInfo.GlyphFamily, "nintendo", StringComparison.OrdinalIgnoreCase) ||
            mapper?.DeviceType == InputDeviceType.SwitchPro ||
            mapper?.DeviceType == InputDeviceType.JoyCon;

        public bool IsPlayStationController =>
            string.Equals((mapper as UniversalMapper)?.Controller.DisplayInfo.GlyphFamily, "playstation", StringComparison.OrdinalIgnoreCase) ||
            mapper?.DeviceType == InputDeviceType.DS4 ||
            mapper?.DeviceType == InputDeviceType.DualSense;

        public bool IsSteamController =>
            string.Equals((mapper as UniversalMapper)?.Controller.DisplayInfo.GlyphFamily, "steam", StringComparison.OrdinalIgnoreCase) ||
            mapper?.DeviceType == InputDeviceType.SteamController ||
            mapper?.DeviceType == InputDeviceType.SteamControllerTriton;

        public bool ShowFaceButtonSwapToggle =>
            !IsPlayStationController && FaceButtonBindings.Count > 0;

        public bool HasCenterTouchpad =>
            IsPlayStationController;

        public bool HasSupportedTouchpadHardware =>
            IsPlayStationController || IsSteamController;

        public bool HasSupportedGyroHardware =>
            ControllerSupportsUniversalInput(UniversalInputId.Gyroscope);

        public bool HasPaddleControls =>
            ControllerSupportsUniversalInput(UniversalInputId.LeftRearPrimary) ||
            ControllerSupportsUniversalInput(UniversalInputId.RightRearPrimary) ||
            ControllerSupportsUniversalInput(UniversalInputId.LeftRearSecondary) ||
            ControllerSupportsUniversalInput(UniversalInputId.RightRearSecondary) ||
            ControllerSupportsUniversalInput(UniversalInputId.LeftGripTouch) ||
            ControllerSupportsUniversalInput(UniversalInputId.RightGripTouch);

        public string TouchpadClickBindingsTabHeader =>
            UsesPlayStationTouchpadClickNames ? "Click Bindings" : "Press Bindings";

        public string TouchpadClickBindingsDescription =>
            UsesPlayStationTouchpadClickNames ?
                "Touchpad physical click bindings." :
                "Touchpad physical press bindings.";

        public string TouchpadClickBindingsEmptyText =>
            UsesPlayStationTouchpadClickNames ?
                "No touchpad click bindings were found for this controller." :
                "No touchpad press bindings were found for this controller.";

        private ObservableCollection<TouchBindingItemsTest> touchpadMouseMovementBindings =
            new ObservableCollection<TouchBindingItemsTest>();
        public ObservableCollection<TouchBindingItemsTest> TouchpadMouseMovementBindings => touchpadMouseMovementBindings;

        private ObservableCollection<TouchBindingItemsTest> touchpadZoneGestureBindings =
            new ObservableCollection<TouchBindingItemsTest>();
        public ObservableCollection<TouchBindingItemsTest> TouchpadZoneGestureBindings => touchpadZoneGestureBindings;

        private ObservableCollection<TouchBindingItemsTest> touchpadTrackballScrollBindings =
            new ObservableCollection<TouchBindingItemsTest>();
        public ObservableCollection<TouchBindingItemsTest> TouchpadTrackballScrollBindings => touchpadTrackballScrollBindings;

        private ObservableCollection<TouchBindingItemsTest> touchpadAdvancedBindings =
            new ObservableCollection<TouchBindingItemsTest>();
        public ObservableCollection<TouchBindingItemsTest> TouchpadAdvancedBindings => touchpadAdvancedBindings;

        public bool HasTouchpadBindings
        {
            get => touchpadBindings.Count > 0;
        }

        private List<TriggerBindingItemsTest> triggerBindings = new List<TriggerBindingItemsTest>();
        public List<TriggerBindingItemsTest> TriggerBindings => triggerBindings;

        public bool HasTriggerBindings
        {
            get => triggerBindings.Count > 0;
        }

        private int selectedTouchBindIndex = -1;
        public int SelectTouchBindIndex
        {
            get => selectedTouchBindIndex;
            set
            {
                if (selectedTouchBindIndex == value) return;
                selectedTouchBindIndex = value;
                SelectTouchBindIndexChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        public event EventHandler SelectTouchBindIndexChanged;

        private int selectTriggerBindIndex = -1;
        public int SelectTriggerBindIndex
        {
            get => selectTriggerBindIndex;
            set
            {
                if (selectTriggerBindIndex == value) return;
                selectTriggerBindIndex = value;
                SelectTriggerBindIndexChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        public event EventHandler SelectTriggerBindIndexChanged;

        private List<StickBindingItemsTest> stickBindings = new List<StickBindingItemsTest>();
        public List<StickBindingItemsTest> StickBindings => stickBindings;

        private int selectStickBindIndex = -1;
        public int SelectStickBindIndex
        {
            get => selectStickBindIndex;
            set
            {
                if (selectStickBindIndex == value) return;
                selectStickBindIndex = value;
                SelectStickBindIndexChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        public event EventHandler SelectStickBindIndexChanged;


        private ObservableCollection<GyroBindingItemsTest> gyroBindings = new ObservableCollection<GyroBindingItemsTest>();
        public ObservableCollection<GyroBindingItemsTest> GyroBindings => gyroBindings;

        public bool HasGyroBindings
        {
            get => gyroBindings.Count > 0;
        }


        private int selectGyroBindIndex = -1;
        public int SelectGyroBindIndex
        {
            get => selectGyroBindIndex;
            set
            {
                if (selectGyroBindIndex == value) return;
                selectGyroBindIndex = value;
                SelectGyroBindIndexChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        public event EventHandler SelectGyroBindIndexChanged;



        private List<BindingItemsTest> alwaysOnBindings = new List<BindingItemsTest>();
        public List<BindingItemsTest> AlwaysOnBindings => alwaysOnBindings;

        private ObservableCollection<AlwaysOnBindingItem> alwaysOnKeybinds =
            new ObservableCollection<AlwaysOnBindingItem>();
        public ObservableCollection<AlwaysOnBindingItem> AlwaysOnKeybinds => alwaysOnKeybinds;

        public bool HasAlwaysOnKeybinds => alwaysOnKeybinds.Count > 0;

        private int selectAlwaysOnBindIndex = -1;
        public int SelectAlwaysOnBindIndex
        {
            get => selectAlwaysOnBindIndex;
            set
            {
                if (selectAlwaysOnBindIndex == value) return;
                selectAlwaysOnBindIndex = value;
                SelectAlwaysOnBindIndexChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        public event EventHandler SelectAlwaysOnBindIndexChanged;



        private List<DPadBindingItemsTest> dpadBindings = new List<DPadBindingItemsTest>();
        public List<DPadBindingItemsTest> DPadBindings => dpadBindings;

        public bool HasDPadBindings
        {
            get => dpadBindings.Count > 0;
        }

        private int selectDPadBindIndex = -1;
        public int SelectDPadBindIndex
        {
            get => selectDPadBindIndex;
            set
            {
                if (selectDPadBindIndex == value) return;
                selectDPadBindIndex = value;
                SelectDPadBindIndexChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        public event EventHandler SelectDPadBindIndexChanged;


        private ObservableCollection<ActionSetItemsTest> actionSetItems = new ObservableCollection<ActionSetItemsTest>();
        public ObservableCollection<ActionSetItemsTest> ActionSetItems => actionSetItems;

        private int selectedActionSetIndex = 0;
        public int SelectedActionSetIndex
        {
            get => selectedActionSetIndex;
            set => selectedActionSetIndex = value;
        }

        private ObservableCollection<ActionLayerItemsTest> layerItems = new ObservableCollection<ActionLayerItemsTest>();
        public ObservableCollection<ActionLayerItemsTest> LayerItems => layerItems;

        private int selectedActionLayerIndex = 0;
        public int SelectedActionLayerIndex
        {
            get => selectedActionLayerIndex;
            set => selectedActionLayerIndex = value;
        }

        public string CurrentLayerName
        {
            get => layerItems[selectedActionLayerIndex].Layer.Name;
            set
            {
                string currentName = layerItems[selectedActionLayerIndex].Layer.Name;
                if (currentName == value) return;
                layerItems[selectedActionLayerIndex].Layer.Name = value;
                layerItems[selectedActionLayerIndex].RaiseDisplayNameChanged();
                MarkProfileDirty();
            }
        }

        public string CurrentSetName
        {
            get => actionSetItems[selectedActionSetIndex].Set.Name;
            set
            {
                string currentName = actionSetItems[selectedActionSetIndex].Set.Name;
                if (currentName == value) return;
                actionSetItems[selectedActionSetIndex].Set.Name = value;
                actionSetItems[selectedActionSetIndex].RaiseDisplayNameChanged();
                MarkProfileDirty();
            }
        }

        private bool overwriteFile;
        public bool OverwriteFile
        {
            get => overwriteFile;
            set => overwriteFile = value;
        }

        public bool OutControllerEnabled
        {
            get => tempProfile.OutputGamepadSettings.enabled;
            set
            {
                if (tempProfile.OutputGamepadSettings.enabled == value) return;
                tempProfile.OutputGamepadSettings.enabled = value;
                RaisePropertyChanged(nameof(OutControllerEnabled));
                MarkProfileDirty();
            }
        }

        private List<EnumChoiceSelection<Mapper.OutputContType>> outputControllerTypeChoices =
            new List<EnumChoiceSelection<Mapper.OutputContType>>()
        {
            new EnumChoiceSelection<Mapper.OutputContType>("Xbox 360", Mapper.OutputContType.Xbox360),
            new EnumChoiceSelection<Mapper.OutputContType>("DualShock 4", Mapper.OutputContType.DualShock4),
            new EnumChoiceSelection<Mapper.OutputContType>("DualSense Edge", Mapper.OutputContType.DualSenseEdge),
            new EnumChoiceSelection<Mapper.OutputContType>("Switch Pro Controller 2", Mapper.OutputContType.SwitchPro2),
        };
        public List<EnumChoiceSelection<Mapper.OutputContType>> OutputControllerTypeOptions => outputControllerTypeChoices;

        public Mapper.OutputContType CurrentOutputControllerType
        {
            get => Mapper.ResolveOutputControllerType(
                tempProfile.OutputGamepadSettings.OutputGamepad);
            set
            {
                Mapper.OutputContType resolvedValue = Mapper.ResolveOutputControllerType(value);
                if (Mapper.ResolveOutputControllerType(
                    tempProfile.OutputGamepadSettings.OutputGamepad) == resolvedValue) return;
                tempProfile.OutputGamepadSettings.OutputGamepad = resolvedValue;
                RaisePropertyChanged(nameof(CurrentOutputControllerType));
                RaisePropertyChanged(nameof(OutputControllerTypeIdx));
                MarkProfileDirty();
            }
        }

        public int OutputControllerTypeIdx
        {
            get
            {
                int result = -1;
                switch (tempProfile.OutputGamepadSettings.OutputGamepad)
                {
                    case Mapper.OutputContType.Xbox360:
                        result = 0;
                        break;
                    case Mapper.OutputContType.DualShock4:
                        result = 1;
                        break;
                    case Mapper.OutputContType.DualSense:
                    case Mapper.OutputContType.DualSenseEdge:
                        result = 2;
                        break;
                    case Mapper.OutputContType.SwitchPro2:
                        result = 3;
                        break;
                    default:
                        break;
                }
                return result;
            }
            set
            {
                Mapper.OutputContType oldValue = tempProfile.OutputGamepadSettings.OutputGamepad;
                switch (value)
                {
                    case 0:
                        tempProfile.OutputGamepadSettings.OutputGamepad = Mapper.OutputContType.Xbox360;
                        break;
                    case 1:
                        tempProfile.OutputGamepadSettings.OutputGamepad = Mapper.OutputContType.DualShock4;
                        break;
                    case 2:
                        tempProfile.OutputGamepadSettings.OutputGamepad = Mapper.OutputContType.DualSenseEdge;
                        break;
                    case 3:
                        tempProfile.OutputGamepadSettings.OutputGamepad = Mapper.OutputContType.SwitchPro2;
                        break;
                    default:
                        break;
                }
                if (oldValue != tempProfile.OutputGamepadSettings.OutputGamepad)
                {
                    RaisePropertyChanged(nameof(OutputControllerTypeIdx));
                    RaisePropertyChanged(nameof(CurrentOutputControllerType));
                    MarkProfileDirty();
                }
            }
        }

        public bool ForceFeedbackEnabled
        {
            get => tempProfile.OutputGamepadSettings.ForceFeedbackEnabled;
            set
            {
                if (tempProfile.OutputGamepadSettings.ForceFeedbackEnabled == value) return;
                tempProfile.OutputGamepadSettings.ForceFeedbackEnabled = value;
                RaisePropertyChanged(nameof(ForceFeedbackEnabled));
                MarkProfileDirty();
            }
        }

        public System.Windows.Media.Color LightbarColor
        {
            get => System.Windows.Media.Color.FromArgb(255,
                tempProfile.LightbarSettings.SolidColor.red,
                tempProfile.LightbarSettings.SolidColor.green,
                tempProfile.LightbarSettings.SolidColor.blue);
            //set
            //{
            //    tempProfile.LightbarSettings.SolidColor.red = value.R;
            //    tempProfile.LightbarSettings.SolidColor.green = value.G;
            //    tempProfile.LightbarSettings.SolidColor.blue = value.B;
            //}
        }

        public string LightbarHexColor
        {
            get => $"#{tempProfile.LightbarSettings.SolidColor.red:X2}{tempProfile.LightbarSettings.SolidColor.green:X2}{tempProfile.LightbarSettings.SolidColor.blue:X2}";
            set
            {
                if (!TryParseHexColor(value, out byte red, out byte green, out byte blue)) return;
                if (tempProfile.LightbarSettings.SolidColor.red == red &&
                    tempProfile.LightbarSettings.SolidColor.green == green &&
                    tempProfile.LightbarSettings.SolidColor.blue == blue)
                {
                    return;
                }

                UpdateSelectedSolidColor(red, green, blue);
            }
        }

        public SolidColorBrush LightbarPreviewBrush => new SolidColorBrush(LightbarColor);

        public bool IsSolidLightbarMode => tempProfile.LightbarSettings.Mode == LightbarMode.SolidColor;
        public bool IsRainbowLightbarMode => tempProfile.LightbarSettings.Mode == LightbarMode.Rainbow;
        public bool IsPulseLightbarMode => tempProfile.LightbarSettings.Mode == LightbarMode.Pulse;
        public bool IsBatteryLightbarMode => tempProfile.LightbarSettings.Mode == LightbarMode.Battery;

        public class LightbarPresetColor
        {
            public string HexColor { get; }
            public SolidColorBrush Brush { get; }

            public LightbarPresetColor(string hexColor)
            {
                HexColor = hexColor;
                if (TryParseHexColor(hexColor, out byte red, out byte green, out byte blue))
                {
                    Brush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(red, green, blue));
                }
                else
                {
                    Brush = new SolidColorBrush(System.Windows.Media.Colors.Transparent);
                }
            }
        }

        public List<LightbarPresetColor> LightbarPresetColors { get; } = new List<LightbarPresetColor>()
        {
            new LightbarPresetColor("#FF0000"),
            new LightbarPresetColor("#FF8000"),
            new LightbarPresetColor("#FFFF00"),
            new LightbarPresetColor("#80FF00"),
            new LightbarPresetColor("#00FF00"),
            new LightbarPresetColor("#00FF80"),
            new LightbarPresetColor("#00FFFF"),
            new LightbarPresetColor("#0080FF"),
            new LightbarPresetColor("#0000FF"),
            new LightbarPresetColor("#8000FF"),
            new LightbarPresetColor("#FF00FF"),
            new LightbarPresetColor("#FF0080"),
            new LightbarPresetColor("#FFFFFF"),
            new LightbarPresetColor("#C0C0C0"),
            new LightbarPresetColor("#808080"),
            new LightbarPresetColor("#404040"),
            new LightbarPresetColor("#000000"),
            new LightbarPresetColor("#3A86FF"),
        };

        private static bool TryParseHexColor(string value, out byte red, out byte green, out byte blue)
        {
            red = green = blue = 0;
            if (string.IsNullOrWhiteSpace(value)) return false;

            string hex = value.Trim();
            if (hex.StartsWith("#")) hex = hex.Substring(1);
            if (hex.Length != 6) return false;

            return byte.TryParse(hex.Substring(0, 2), System.Globalization.NumberStyles.HexNumber, null, out red) &&
                byte.TryParse(hex.Substring(2, 2), System.Globalization.NumberStyles.HexNumber, null, out green) &&
                byte.TryParse(hex.Substring(4, 2), System.Globalization.NumberStyles.HexNumber, null, out blue);
        }

        public System.Windows.Media.Color LightbarPulseColor
        {
            get => System.Windows.Media.Color.FromArgb(255,
                tempProfile.LightbarSettings.PulseColor.red,
                tempProfile.LightbarSettings.PulseColor.green,
                tempProfile.LightbarSettings.PulseColor.blue);
        }

        public string LightbarPulseHexColor
        {
            get => $"#{tempProfile.LightbarSettings.PulseColor.red:X2}{tempProfile.LightbarSettings.PulseColor.green:X2}{tempProfile.LightbarSettings.PulseColor.blue:X2}";
            set
            {
                if (!TryParseHexColor(value, out byte red, out byte green, out byte blue)) return;
                if (tempProfile.LightbarSettings.PulseColor.red == red &&
                    tempProfile.LightbarSettings.PulseColor.green == green &&
                    tempProfile.LightbarSettings.PulseColor.blue == blue)
                {
                    return;
                }

                UpdateSelectedPulseColor(red, green, blue);
            }
        }

        public SolidColorBrush LightbarPulsePreviewBrush => new SolidColorBrush(LightbarPulseColor);

        public System.Windows.Media.Color LightbarBatteryColor
        {
            get => System.Windows.Media.Color.FromArgb(255,
                tempProfile.LightbarSettings.BatteryFullColor.red,
                tempProfile.LightbarSettings.BatteryFullColor.green,
                tempProfile.LightbarSettings.BatteryFullColor.blue);
        }

        public string LightbarBatteryHexColor
        {
            get => $"#{tempProfile.LightbarSettings.BatteryFullColor.red:X2}{tempProfile.LightbarSettings.BatteryFullColor.green:X2}{tempProfile.LightbarSettings.BatteryFullColor.blue:X2}";
            set
            {
                if (!TryParseHexColor(value, out byte red, out byte green, out byte blue)) return;
                if (tempProfile.LightbarSettings.BatteryFullColor.red == red &&
                    tempProfile.LightbarSettings.BatteryFullColor.green == green &&
                    tempProfile.LightbarSettings.BatteryFullColor.blue == blue)
                {
                    return;
                }

                UpdateSelectedBatteryColor(red, green, blue);
            }
        }

        public SolidColorBrush LightbarBatteryPreviewBrush => new SolidColorBrush(LightbarBatteryColor);

        private List<EnumChoiceSelection<LightbarMode>> lightbarModeChoices = new List<EnumChoiceSelection<LightbarMode>>()
        {
            new EnumChoiceSelection<LightbarMode>("Solid Color", LightbarMode.SolidColor),
            new EnumChoiceSelection<LightbarMode>("Rainbow", LightbarMode.Rainbow),
            new EnumChoiceSelection<LightbarMode>("Pulse", LightbarMode.Pulse),
            new EnumChoiceSelection<LightbarMode>("Battery", LightbarMode.Battery),
        };
        public List<EnumChoiceSelection<LightbarMode>> LightbarModeOptions => lightbarModeChoices;

        public LightbarMode CurrentLightbarMode
        {
            get => tempProfile.LightbarSettings.Mode;
            set
            {
                if (tempProfile.LightbarSettings.Mode == value) return;
                tempProfile.LightbarSettings.Mode = value;
                tempProfile.LightbarSettings.RaiseModeChanged();
                CurrentLightbarModeChanged?.Invoke(this, EventArgs.Empty);
                RaisePropertyChanged(nameof(CurrentLightbarMode));
                RaisePropertyChanged(nameof(LightbarOptionsTabIndex));
                RaisePropertyChanged(nameof(IsSolidLightbarMode));
                RaisePropertyChanged(nameof(IsRainbowLightbarMode));
                RaisePropertyChanged(nameof(IsPulseLightbarMode));
                RaisePropertyChanged(nameof(IsBatteryLightbarMode));
                MarkProfileDirty();
            }
        }
        public event EventHandler CurrentLightbarModeChanged;

        public int LightbarOptionsTabIndex
        {
            get => lightbarModeChoices.FindIndex(t => t.ChoiceValue == tempProfile.LightbarSettings.Mode);
        }
        public event EventHandler LightbarOptionsTabIndexChanged;

        public int RainbowSecondsCycle
        {
            get => tempProfile.LightbarSettings.rainbowSecondsCycle;
            set
            {
                int newValue = Math.Clamp(value, 0, 100);
                if (tempProfile.LightbarSettings.rainbowSecondsCycle == newValue) return;
                tempProfile.LightbarSettings.rainbowSecondsCycle = newValue;
                RaisePropertyChanged(nameof(RainbowSecondsCycle));
                MarkProfileDirty();
            }
        }

        public ProfileEditorTestViewModel(Mapper mapper, ProfileEntity profileEnt, Profile currentProfile)
        {
            this.mapper = mapper;
            this.profileEnt = profileEnt;
            this.tempProfile = currentProfile;
            savedProfileFingerprint = CreateProfileFingerprint();

            tempProfile.DirtyChanged += TempProfile_DirtyChanged;
            RefreshProfileDirtyState();
            mapper.ProfileEditCommitted += Mapper_ProfileEditCommitted;
            CurrentLightbarModeChanged += ProfileEditorTestViewModel_CurrentLightbarModeChanged;

            // Editor controls for the binding shown by default (numeric spinners,
            // tab auto-selection, etc.) can push a stray property update the moment
            // they finish loading, before the user has touched anything. Re-baseline
            // once the UI has settled so that startup noise isn't mistaken for a real
            // edit and doesn't trip the unsaved-changes prompt on close.
            System.Windows.Application.Current?.Dispatcher.BeginInvoke(
                System.Windows.Threading.DispatcherPriority.ApplicationIdle,
                new Action(() =>
                {
                    savedProfileFingerprint = CreateProfileFingerprint();
                    RefreshProfileDirtyState();
                }));
        }

        private void Mapper_ProfileEditCommitted(object sender, EventArgs e)
        {
            MarkProfileDirty();
        }

        private void ProfileEditorTestViewModel_CurrentLightbarModeChanged(object sender, EventArgs e)
        {
            LightbarOptionsTabIndexChanged?.Invoke(this, EventArgs.Empty);
        }

        private void TempProfile_DirtyChanged(object sender, EventArgs e)
        {
            if (!refreshingDirtyState)
            {
                MarkProfileDirty();
            }
            else
            {
                RaisePropertyChanged(nameof(IsProfileDirty));
            }
        }

        public void UpdateSelectedSolidColor(byte red, byte green, byte blue)
        {
            tempProfile.LightbarSettings.SolidColor.red = red;
            tempProfile.LightbarSettings.SolidColor.green = green;
            tempProfile.LightbarSettings.SolidColor.blue = blue;
            RaisePropertyChanged(nameof(LightbarColor));
            RaisePropertyChanged(nameof(LightbarHexColor));
            RaisePropertyChanged(nameof(LightbarPreviewBrush));
            MarkProfileDirty();
        }

        public void UpdateSelectedPulseColor(byte red, byte green, byte blue)
        {
            tempProfile.LightbarSettings.PulseColor.red = red;
            tempProfile.LightbarSettings.PulseColor.green = green;
            tempProfile.LightbarSettings.PulseColor.blue = blue;
            RaisePropertyChanged(nameof(LightbarPulseColor));
            RaisePropertyChanged(nameof(LightbarPulseHexColor));
            RaisePropertyChanged(nameof(LightbarPulsePreviewBrush));
            MarkProfileDirty();
        }

        public void UpdateSelectedBatteryColor(byte red, byte green, byte blue)
        {
            tempProfile.LightbarSettings.BatteryFullColor.red = red;
            tempProfile.LightbarSettings.BatteryFullColor.green = green;
            tempProfile.LightbarSettings.BatteryFullColor.blue = blue;
            RaisePropertyChanged(nameof(LightbarBatteryColor));
            RaisePropertyChanged(nameof(LightbarBatteryHexColor));
            RaisePropertyChanged(nameof(LightbarBatteryPreviewBrush));
            MarkProfileDirty();
        }

        private string CreateProfileFingerprint()
        {
            if (tempProfile == null) return string.Empty;

            // ProfileSerializer is the authoritative persisted representation.
            // Comparing it means the unsaved indicator mirrors exactly what Save
            // and Discard operate on, including action and keybind changes.
            ProfileSerializer serializer = new ProfileSerializer(tempProfile);
            return JsonConvert.SerializeObject(serializer, Formatting.None);
        }

        public void Test()
        {
            foreach(ActionSet set in tempProfile.ActionSets)
            {
                ActionSetItemsTest tempItem = new ActionSetItemsTest(set);
                actionSetItems.Add(tempItem);
            }

            //selectedActionLayerIndex = 0;
            //selectedActionSetIndex = 0;
            selectedActionLayerIndex = mapper.ActionProfile.CurrentActionSet.CurrentActionLayer.Index;
            selectedActionSetIndex = mapper.ActionProfile.CurrentActionSetIndex;
            PopulateLayerItems();
            PopulateCurrentLayerBindings();

            layerItems[selectedActionLayerIndex].ItemActive = true;
            actionSetItems[selectedActionSetIndex].ItemActive = true;
        }

        public void RefreshSetBindings()
        {
            buttonBindings.Clear();
            buttonBindingsIndexDict.Clear();
            faceButtonBindings.Clear();
            bumperButtonBindings.Clear();
            centerButtonBindings.Clear();
            paddleButtonBindings.Clear();
            extraButtonBindings.Clear();
            touchpadButtonBindings.Clear();
            touchpadButtonBindingItems.Clear();
            triggerKeybinds.Clear();
            touchpadBindings.Clear();
            touchpadTouchSurfaceBindings.Clear();
            touchpadMouseMovementBindings.Clear();
            touchpadZoneGestureBindings.Clear();
            touchpadTrackballScrollBindings.Clear();
            touchpadAdvancedBindings.Clear();
            triggerBindings.Clear();
            stickBindings.Clear();
            gyroBindings.Clear();
            dpadBindings.Clear();

            PopulateLayerItems();
            PopulateCurrentLayerBindings();

            SelectedActionLayerIndex = 0;
            layerItems[selectedActionLayerIndex].ItemActive = true;
        }

        public void RefreshLayerBindings()
        {
            buttonBindings.Clear();
            buttonBindingsIndexDict.Clear();
            faceButtonBindings.Clear();
            bumperButtonBindings.Clear();
            centerButtonBindings.Clear();
            paddleButtonBindings.Clear();
            extraButtonBindings.Clear();
            touchpadButtonBindings.Clear();
            touchpadButtonBindingItems.Clear();
            triggerKeybinds.Clear();
            touchpadBindings.Clear();
            touchpadTouchSurfaceBindings.Clear();
            touchpadMouseMovementBindings.Clear();
            touchpadZoneGestureBindings.Clear();
            touchpadTrackballScrollBindings.Clear();
            touchpadAdvancedBindings.Clear();
            triggerBindings.Clear();
            stickBindings.Clear();
            gyroBindings.Clear();
            dpadBindings.Clear();
            alwaysOnBindings.Clear();
            alwaysOnKeybinds.Clear();

            PopulateCurrentLayerBindings();
        }

        private void PopulateLayerItems()
        {
            ActionSetItemsTest setItem = actionSetItems[selectedActionSetIndex];
            ActionSet set = setItem.Set;

            layerItems.Clear();
            int tempInd = 0;
            foreach (ActionLayer layer in set.ActionLayers)
            {
                ActionLayerItemsTest tempLayerItem = new ActionLayerItemsTest(set, layer, tempInd++);
                layerItems.Add(tempLayerItem);
            }
        }

        private void PopulateCurrentLayerBindings()
        {
            int tempBtnInd = 0;
            claimedButtonBindings.Clear();
            touchpadButtonBindings.Clear();
            touchpadButtonBindingItems.Clear();

            foreach(InputBindingMeta meta in
                mapper.BindingList.Where((item) => item.controlType == InputBindingMeta.InputControlType.Button))
            {
                if (tempProfile.CurrentActionSet.CurrentActionLayer.buttonActionDict.
                    TryGetValue(meta.id, out ButtonMapAction tempBtnAct))
                {
                    BindingItemsTest tempItem = new BindingItemsTest(meta.id, meta.displayName, tempBtnAct, mapper);
                    buttonBindings.Add(tempItem);
                    buttonBindingsIndexDict.Add(meta.id, tempBtnInd++);
                }
            }

            foreach (InputBindingMeta meta in
                mapper.BindingList.Where((item) => item.controlType == InputBindingMeta.InputControlType.Touchpad))
            {
                if (tempProfile.CurrentActionSet.CurrentActionLayer.touchpadActionDict.
                        TryGetValue(meta.id, out TouchpadMapAction tempTouchAct))
                {
                    TouchBindingItemsTest tempItem = CreateTouchBindingItem(meta, tempTouchAct);
                    tempItem.TouchpadClickBinding = CreateTouchpadClickBinding(tempItem);
                    touchpadBindings.Add(tempItem);
                }
            }

            foreach (InputBindingMeta meta in
                mapper.BindingList.Where((item) => item.controlType == InputBindingMeta.InputControlType.TouchpadRegion))
            {
                if (tempProfile.CurrentActionSet.CurrentActionLayer.touchpadActionDict.
                        TryGetValue(meta.id, out TouchpadMapAction tempTouchAct))
                {
                    TouchBindingItemsTest tempItem = CreateTouchBindingItem(meta, tempTouchAct);
                    tempItem.TouchpadClickBinding = CreateTouchpadClickBinding(tempItem);
                    touchpadBindings.Add(tempItem);
                }
            }

            foreach (InputBindingMeta meta in
                mapper.BindingList.Where((item) => item.controlType == InputBindingMeta.InputControlType.Trigger))
            {
                if (tempProfile.CurrentActionSet.CurrentActionLayer.triggerActionDict.
                        TryGetValue(meta.id, out TriggerMapAction tempTrigAct))
                {
                    TriggerBindingItemsTest tempItem = new TriggerBindingItemsTest(meta.id, meta.displayName, tempTrigAct, mapper);
                    triggerBindings.Add(tempItem);
                }
            }

            foreach (InputBindingMeta meta in
                mapper.BindingList.Where((item) => item.controlType == InputBindingMeta.InputControlType.Stick))
            {
                if (tempProfile.CurrentActionSet.CurrentActionLayer.stickActionDict.
                        TryGetValue(meta.id, out StickMapAction tempTrigAct))
                {
                    StickBindingItemsTest tempItem = new StickBindingItemsTest(meta.id, meta.displayName, tempTrigAct, mapper);
                    stickBindings.Add(tempItem);
                }
            }

            foreach (InputBindingMeta meta in
                mapper.BindingList.Where((item) => item.controlType == InputBindingMeta.InputControlType.DPad))
            {
                if (tempProfile.CurrentActionSet.CurrentActionLayer.dpadActionDict.
                        TryGetValue(meta.id, out DPadMapAction tempDPadAct))
                {
                    DPadBindingItemsTest tempItem = new DPadBindingItemsTest(meta.id, meta.displayName, tempDPadAct, mapper);
                    dpadBindings.Add(tempItem);
                }
            }

            foreach (InputBindingMeta meta in
                mapper.BindingList.Where((item) => item.controlType == InputBindingMeta.InputControlType.Gyro))
            {
                if (tempProfile.CurrentActionSet.CurrentActionLayer.gyroActionDict.
                        TryGetValue(meta.id, out GyroMapAction tempTrigAct))
                {
                    GyroBindingItemsTest tempItem = new GyroBindingItemsTest(meta.id, meta.displayName, tempTrigAct, mapper);
                    gyroBindings.Add(tempItem);
                }
            }

            //foreach (InputBindingMeta meta in
            //    mapper.BindingList.Where((item) => item.controlType == InputBindingMeta.InputControlType.Button))
            {
                if (tempProfile.CurrentActionSet.CurrentActionLayer.actionSetActionDict.
                    TryGetValue($"{tempProfile.CurrentActionSet.ActionButtonId}", out ButtonMapAction tempBtnAct))
                {
                    BindingItemsTest tempItem = new BindingItemsTest(tempBtnAct.MappingId,
                        "Always On",
                        tempBtnAct, mapper);
                    alwaysOnBindings.Add(tempItem);
                    if (tempBtnAct is not ButtonNoAction)
                    {
                        alwaysOnKeybinds.Add(new AlwaysOnBindingItem(this, tempItem,
                            alwaysOnKeybinds.Count));
                    }
                }
            }

            PopulateFaceButtonBindings();
            PopulateBumperButtonBindings();
            PopulateCenterButtonBindings();
            PopulatePaddleButtonBindings();
            PopulateTriggerKeybinds();
            PopulateDPadKeybinds();
            PopulateStickClickBindings();
            PopulateTouchpadButtonBindings();
            PopulateExtraButtonBindings();
            PopulateStickKeybinds();
            PopulateTouchpadGroups();
            RaisePropertyChanged(nameof(HasAlwaysOnKeybinds));
            RaisePropertyChanged(nameof(ShowFaceButtonSwapToggle));
            RaisePropertyChanged(nameof(HasCenterTouchpad));
            RaisePropertyChanged(nameof(HasSupportedTouchpadHardware));
            RaisePropertyChanged(nameof(HasPaddleControls));
        }

        private TouchBindingItemsTest CreateTouchBindingItem(InputBindingMeta meta,
            TouchpadMapAction action)
        {
            bool available = true;
            string unavailableMessage = null;
            if (UniversalLegacyBindingMap.TryGetUniversalInput(meta.id, out UniversalInputId inputId))
            {
                available = ControllerSupportsUniversalInput(inputId);
                if (!available)
                {
                    unavailableMessage = inputId == UniversalInputId.PrimaryTouchSurface
                        ? "The connected controller does not have a centre touchpad touch area. This binding is preserved in the profile and becomes editable when a PlayStation-style touchpad controller is connected."
                        : "The connected controller does not report this touchpad input. This binding is preserved in the profile and becomes editable when compatible hardware is connected.";
                }
            }

            return new TouchBindingItemsTest(meta.id, meta.displayName, action, mapper,
                available, unavailableMessage);
        }

        private void PopulateTouchpadGroups()
        {
            touchpadTouchSurfaceBindings.Clear();
            touchpadMouseMovementBindings.Clear();
            touchpadZoneGestureBindings.Clear();
            touchpadTrackballScrollBindings.Clear();
            touchpadAdvancedBindings.Clear();

            foreach (TouchBindingItemsTest item in touchpadBindings
                .OrderBy(GetTouchpadTouchBindingRank)
                .ThenBy(item => item.BindingName, StringComparer.OrdinalIgnoreCase))
            {
                touchpadTouchSurfaceBindings.Add(item);
            }

            foreach (TouchBindingItemsTest item in touchpadBindings)
            {
                if (item.IsMouseMovementAction)
                {
                    touchpadMouseMovementBindings.Add(item);
                }

                if (item.IsZoneAction || item.IsOuterRingAction || item.IsGestureAction)
                {
                    touchpadZoneGestureBindings.Add(item);
                }

                if (item.IsTrackballScrollAction)
                {
                    touchpadTrackballScrollBindings.Add(item);
                }

                if (item.IsAdvancedAction)
                {
                    touchpadAdvancedBindings.Add(item);
                }
            }
        }

        private void PopulateStickClickBindings()
        {
            leftStickClickBinding.Clear();
            AddFirstMatchingButtonBinding(leftStickClickBinding, claimedButtonBindings,
                new string[] { "L3", "LSClick", "LeftStickClick" },
                "Left Stick Click",
                "Stick click button");

            rightStickClickBinding.Clear();
            AddFirstMatchingButtonBinding(rightStickClickBinding, claimedButtonBindings,
                new string[] { "R3", "RSClick", "RightStickClick" },
                "Right Stick Click",
                "Stick click button");

            const string unavailableStickTouchMessage =
                "The connected controller does not report this capacitive stick touch sensor. This binding is preserved in the profile and becomes editable when compatible hardware is connected.";

            leftStickTouchBinding.Clear();
            AddFirstMatchingButtonBinding(leftStickTouchBinding, claimedButtonBindings,
                new string[] { "LSTouch", "LeftStickTouch" },
                "LS Touch / Left Stick Touch",
                "Stick touch sensor",
                ControllerSupportsUniversalInput(UniversalInputId.LeftStickTouch),
                unavailableStickTouchMessage);

            rightStickTouchBinding.Clear();
            AddFirstMatchingButtonBinding(rightStickTouchBinding, claimedButtonBindings,
                new string[] { "RSTouch", "RightStickTouch" },
                "RS Touch / Right Stick Touch",
                "Stick touch sensor",
                ControllerSupportsUniversalInput(UniversalInputId.RightStickTouch),
                unavailableStickTouchMessage);
        }

        private void PopulateExtraButtonBindings()
        {
            extraButtonBindings.Clear();

            for (int slot = 1; slot <= 6; slot++)
            {
                string bindingName = $"MiscButton{slot}";
                if (!buttonBindingsIndexDict.TryGetValue(bindingName, out int index))
                {
                    continue;
                }

                BindingItemsTest item = buttonBindings[index];
                if (claimedButtonBindings.Contains(item.BindingName))
                {
                    continue;
                }

                claimedButtonBindings.Add(item.BindingName);
                UniversalInputId inputId = (UniversalInputId)((int)UniversalInputId.MiscButton1 + (slot - 1));
                string label = ControllerMiscLabelProvider.GetLabel(
                    inputId,
                    (mapper as UniversalMapper)?.Controller.Capabilities);
                extraButtonBindings.Add(new FaceButtonBindingItem(this, item, label));
            }
        }

        private static int GetTouchpadTouchBindingRank(TouchBindingItemsTest item)
        {
            return item.BindingName switch
            {
                "TouchpadLeft" or "LeftTouchpad" or "LeftTouchSurface" => 0,
                "TouchpadRight" or "RightTouchpad" or "RightTouchSurface" => 1,
                "Touchpad" or "PrimaryTouchSurface" => 2,
                _ => 3,
            };
        }

        private void PopulateTouchpadButtonBindings()
        {
            touchpadTouchBindings.Clear();
            touchpadPressBindings.Clear();

            FaceButtonBindingItem item;

            item = AddTouchpadButtonBinding(
                new string[] { "LeftPadTouch", "LeftTouchpadTouch", "LeftTouchSurfaceTouch" },
                UsesPlayStationTouchpadClickNames ? "Left-side Touch" : "Left Touch",
                "Touchpad touch sensor",
                false,
                ControllerSupportsUniversalInput(UniversalInputId.LeftTouchContact),
                GetUnavailableSideTouchMessage());
            if (item != null) touchpadTouchBindings.Add(item);

            item = AddTouchpadButtonBinding(
                new string[] { "RightPadTouch", "RightTouchpadTouch", "RightTouchSurfaceTouch" },
                UsesPlayStationTouchpadClickNames ? "Right-side Touch" : "Right Touch",
                "Touchpad touch sensor",
                false,
                ControllerSupportsUniversalInput(UniversalInputId.RightTouchContact),
                GetUnavailableSideTouchMessage());
            if (item != null) touchpadTouchBindings.Add(item);

            item = AddTouchpadButtonBinding(
                new string[] { "PrimaryPadTouch", "TouchpadTouch", "PrimaryTouchSurfaceTouch" },
                "Center Touch",
                "Touchpad touch sensor",
                false,
                HasCenterTouchpad,
                GetUnavailableCenterTouchMessage());
            item ??= FaceButtonBindingItem.Unavailable(this, "CenterTouch", "Center Touch",
                "Touchpad touch sensor", GetUnavailableCenterTouchMessage());
            touchpadTouchBindings.Add(item);
            if (!touchpadButtonBindings.Contains(item))
            {
                touchpadButtonBindings.Add(item);
            }

            item = AddTouchpadButtonBinding(
                new string[] { "LeftPadClick", "LeftTouchpadClick", "LeftTouchSurfaceClick" },
                UsesPlayStationTouchpadClickNames ? "Left-side Click" : "Left Press",
                UsesPlayStationTouchpadClickNames ? "Physical left-side click" : "Physical left-pad press",
                UsesPlayStationTouchpadClickNames);
            if (item != null) touchpadPressBindings.Add(item);

            item = AddTouchpadButtonBinding(
                new string[] { "RightPadClick", "RightTouchpadClick", "RightTouchSurfaceClick" },
                UsesPlayStationTouchpadClickNames ? "Right-side Click" : "Right Press",
                UsesPlayStationTouchpadClickNames ? "Physical right-side click" : "Physical right-pad press",
                UsesPlayStationTouchpadClickNames);
            if (item != null) touchpadPressBindings.Add(item);

            item = AddTouchpadButtonBinding(
                new string[] { "TouchClick", "PrimaryTouchSurfaceClick" },
                "Center Press",
                UsesPlayStationTouchpadClickNames ? "Physical full-pad click" : "Physical full-pad press",
                UsesPlayStationTouchpadClickNames,
                HasCenterTouchpad,
                GetUnavailableCenterPressMessage());
            item ??= FaceButtonBindingItem.Unavailable(this, "CenterPress", "Center Press",
                "Physical full-pad press", GetUnavailableCenterPressMessage());
            touchpadPressBindings.Add(item);
            if (!touchpadButtonBindings.Contains(item))
            {
                touchpadButtonBindings.Add(item);
            }
        }

        private void PopulateStickKeybinds()
        {
            // StickTranslate/StickPadAction/StickMouse/StickCircular/StickAbsMouse/StickFlickStick
            // prop view models read mapper.EditActionSet/EditLayer in their constructors (and again
            // whenever a composite-layer-inherited action is first edited) to detect whether the
            // bound action is a base-layer action that needs to be soft-copied into the current
            // layer before editing. These refs stay populated for the life of the profile editor
            // session since the Sticks tab is always live (not a modal edit window).
            PopulateMapperEditActionRefs(mapper);

            (leftStickKeybinds ??= new StickSideViewModel(this, "LS")).Refresh();
            (rightStickKeybinds ??= new StickSideViewModel(this, "RS")).Refresh();
        }

        private void PopulateDPadKeybinds()
        {
            (dpadKeybinds ??= new DPadKeybindsViewModel(this)).Refresh();
        }

        private void PopulateFaceButtonBindings()
        {
            faceButtonBindings.Clear();

            string[][] faceAliases = new string[][]
            {
                new string[] { "A", "Cross", "FaceButtonSouth" },
                new string[] { "B", "Circle", "FaceButtonEast" },
                new string[] { "X", "Square", "FaceButtonWest" },
                new string[] { "Y", "Triangle", "FaceButtonNorth" },
            };

            string[] displayNames = ResolveFaceButtonDisplayNames();

            for (int i = 0; i < faceAliases.Length; i++)
            {
                BindingItemsTest item = null;
                foreach (string alias in faceAliases[i])
                {
                    if (buttonBindingsIndexDict.TryGetValue(alias, out int index))
                    {
                        item = buttonBindings[index];
                        break;
                    }
                }

                if (item != null)
                {
                    faceButtonBindings.Add(new FaceButtonBindingItem(this, item, displayNames[i]));
                    claimedButtonBindings.Add(item.BindingName);
                }
            }
        }

        private void PopulateBumperButtonBindings()
        {
            bumperButtonBindings.Clear();

            string[][] bumperAliases = new string[][]
            {
                new string[] { "L1", "LB", "LShoulder", "LeftShoulder" },
                new string[] { "R1", "RB", "RShoulder", "RightShoulder" },
            };

            string[] displayNames = new string[]
            {
                "Left Bumper",
                "Right Bumper",
            };

            for (int i = 0; i < bumperAliases.Length; i++)
            {
                BindingItemsTest item = null;
                foreach (string alias in bumperAliases[i])
                {
                    if (buttonBindingsIndexDict.TryGetValue(alias, out int index))
                    {
                        item = buttonBindings[index];
                        break;
                    }
                }

                if (item != null)
                {
                    bumperButtonBindings.Add(new FaceButtonBindingItem(this, item, displayNames[i]));
                    claimedButtonBindings.Add(item.BindingName);
                }
            }
        }

        private string[] ResolveFaceButtonDisplayNames()
        {
            ControllerCapabilities capabilities = (mapper as UniversalMapper)?.Controller.Capabilities;
            UniversalInputId[] order =
            {
                UniversalInputId.FaceButtonSouth,
                UniversalInputId.FaceButtonEast,
                UniversalInputId.FaceButtonWest,
                UniversalInputId.FaceButtonNorth,
            };

            string[] labels = order
                .Select(input => ControllerLabelProvider.GetLabel(input, capabilities))
                .ToArray();

            if (ShowFaceButtonSwapToggle && UniversalLiveInputRoutingOptions.NintendoFaceButtonSwapEnabled)
            {
                (labels[0], labels[1]) = (labels[1], labels[0]);
                (labels[2], labels[3]) = (labels[3], labels[2]);
            }

            return labels;
        }

        private void PopulateCenterButtonBindings()
        {
            centerButtonBindings.Clear();

            AddFirstMatchingButtonBinding(centerButtonBindings, claimedButtonBindings,
                new string[] { "Options", "Start", "Plus", "Menu" },
                "Options / Menu",
                "System button");
            AddFirstMatchingButtonBinding(centerButtonBindings, claimedButtonBindings,
                new string[] { "Share", "Create", "Capture", "Back", "Minus", "View" },
                "Share / View",
                "System button");
            AddFirstMatchingButtonBinding(centerButtonBindings, claimedButtonBindings,
                new string[] { "PS", "Home", "Guide", "Steam", "System" },
                "PS / Home",
                "System button");
            AddFirstMatchingButtonBinding(centerButtonBindings, claimedButtonBindings,
                new string[] { "Mute" },
                "Mic",
                "System button");
            AddFirstMatchingButtonBinding(centerButtonBindings, claimedButtonBindings,
                new string[] { "QAM", "QuickAccessMenu" },
                "QAM",
                "Quick Access Menu button");
            AddFirstMatchingButtonBinding(centerButtonBindings, claimedButtonBindings,
                new string[] { "Select" },
                "Select",
                "Center/select button");
        }

        private void PopulatePaddleButtonBindings()
        {
            paddleButtonBindings.Clear();
            const string unavailablePaddleMessage =
                "The connected controller does not report this paddle, rear button, or grip input. This binding is preserved in the profile and becomes editable when compatible hardware is connected.";

            AddFirstMatchingButtonBinding(paddleButtonBindings,
                new string[] { "BLP", "L4", "LSideL", "LeftRearPrimary" },
                "Left Paddle 1",
                ControllerSupportsUniversalInput(UniversalInputId.LeftRearPrimary),
                unavailablePaddleMessage);
            AddFirstMatchingButtonBinding(paddleButtonBindings,
                new string[] { "BRP", "R4", "RSideL", "RightRearPrimary" },
                "Right Paddle 1",
                ControllerSupportsUniversalInput(UniversalInputId.RightRearPrimary),
                unavailablePaddleMessage);
            AddFirstMatchingButtonBinding(paddleButtonBindings,
                new string[] { "L5", "PL", "LSideR", "LeftRearSecondary" },
                "Left Paddle 2",
                ControllerSupportsUniversalInput(UniversalInputId.LeftRearSecondary),
                unavailablePaddleMessage);
            AddFirstMatchingButtonBinding(paddleButtonBindings,
                new string[] { "R5", "PR", "RSideR", "RightRearSecondary" },
                "Right Paddle 2",
                ControllerSupportsUniversalInput(UniversalInputId.RightRearSecondary),
                unavailablePaddleMessage);
            AddFirstMatchingButtonBinding(paddleButtonBindings,
                new string[] { "LeftGrip" },
                "Left Grip",
                ControllerSupportsUniversalInput(UniversalInputId.LeftGripTouch),
                unavailablePaddleMessage);
            AddFirstMatchingButtonBinding(paddleButtonBindings,
                new string[] { "RightGrip" },
                "Right Grip",
                ControllerSupportsUniversalInput(UniversalInputId.RightGripTouch),
                unavailablePaddleMessage);
        }

        private void AddFirstMatchingButtonBinding(
            ObservableCollection<FaceButtonBindingItem> target,
            string[] aliases,
            string displayName,
            bool isAvailable = true,
            string unavailableMessage = null)
        {
            AddFirstMatchingButtonBinding(target, claimedButtonBindings, aliases, displayName,
                null, isAvailable, unavailableMessage);
        }

        private bool AddFirstMatchingButtonBinding(
            ObservableCollection<FaceButtonBindingItem> target,
            HashSet<string> claimedBindings,
            string[] aliases,
            string displayName,
            string subtitle,
            bool isAvailable = true,
            string unavailableMessage = null)
        {
            BindingItemsTest item = null;
            foreach (string alias in aliases)
            {
                if (claimedBindings != null && claimedBindings.Contains(alias))
                {
                    continue;
                }

                if (buttonBindingsIndexDict.TryGetValue(alias, out int index))
                {
                    item = buttonBindings[index];
                    break;
                }
            }

            if (item != null)
            {
                target.Add(new FaceButtonBindingItem(this, item, displayName, subtitle,
                    isAvailable: isAvailable, unavailableMessage: unavailableMessage));
                claimedBindings?.Add(item.BindingName);
                return true;
            }

            return false;
        }

        private FaceButtonBindingItem CreateTouchpadClickBinding(TouchBindingItemsTest touchpadItem)
        {
            string[] aliases = touchpadItem.BindingName switch
            {
                "LeftTouchpad" => new string[] { "LeftPadClick", "LeftTouchpadClick", "LeftTouchSurfaceClick" },
                "TouchpadLeft" => new string[] { "LeftPadClick", "LeftTouchpadClick", "LeftTouchSurfaceClick" },
                "LeftTouchSurface" => new string[] { "LeftTouchSurfaceClick", "LeftPadClick", "LeftTouchpadClick" },
                "RightTouchpad" => new string[] { "RightPadClick", "RightTouchpadClick", "RightTouchSurfaceClick" },
                "TouchpadRight" => new string[] { "RightPadClick", "RightTouchpadClick", "RightTouchSurfaceClick" },
                "RightTouchSurface" => new string[] { "RightTouchSurfaceClick", "RightPadClick", "RightTouchpadClick" },
                _ => Array.Empty<string>(),
            };

            foreach (string alias in aliases)
            {
                if (buttonBindingsIndexDict.TryGetValue(alias, out int index))
                {
                    return AddTouchpadButtonBinding(buttonBindings[index],
                        IsRightTouchSurfaceBinding(touchpadItem.BindingName)
                            ? (UsesPlayStationTouchpadClickNames ? "Right-side Click" : "Right Press")
                            : (UsesPlayStationTouchpadClickNames ? "Left-side Click" : "Left Press"),
                        UsesPlayStationTouchpadClickNames ? "Physical touchpad click" : "Physical touchpad press",
                        UsesPlayStationTouchpadClickNames);
                }
            }

            return null;
        }

        private static bool IsRightTouchSurfaceBinding(string bindingName)
        {
            return bindingName == "RightTouchpad" ||
                bindingName == "TouchpadRight" ||
                bindingName == "RightTouchSurface";
        }

        private FaceButtonBindingItem AddTouchpadButtonBinding(string[] aliases,
            string displayName, string subtitle, bool usesClickTerminology = false,
            bool isAvailable = true, string unavailableMessage = null)
        {
            foreach (string alias in aliases)
            {
                if (buttonBindingsIndexDict.TryGetValue(alias, out int index))
                {
                    return AddTouchpadButtonBinding(buttonBindings[index], displayName, subtitle,
                        usesClickTerminology, isAvailable, unavailableMessage);
                }
            }

            return null;
        }

        private static string GetUnavailableSideTouchMessage()
        {
            return "The connected controller does not report this touchpad's capacitive touch sensor. This binding is preserved in the profile and becomes editable when compatible hardware is connected.";
        }

        private static string GetUnavailableCenterTouchMessage()
        {
            return "The connected controller does not have a centre touchpad touch area. This binding is preserved in the profile and becomes editable when a PlayStation-style touchpad controller is connected.";
        }

        private static string GetUnavailableCenterPressMessage()
        {
            return "The connected controller does not have a centre touchpad press. This binding is preserved in the profile and becomes editable when a PlayStation-style touchpad controller is connected.";
        }

        private bool ControllerSupportsUniversalInput(UniversalInputId inputId)
        {
            return (mapper as UniversalMapper)?.Controller.Capabilities.Supports(inputId) == true;
        }

        private static readonly HashSet<string> PressureCapableClickBindingNames =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "LeftPadClick", "LeftTouchpadClick", "RightPadClick", "RightTouchpadClick",
            };

        private FaceButtonBindingItem AddTouchpadButtonBinding(BindingItemsTest item,
            string displayName, string subtitle, bool usesClickTerminology = false,
            bool isAvailable = true, string unavailableMessage = null)
        {
            if (item == null)
            {
                return null;
            }

            claimedButtonBindings.Add(item.BindingName);
            if (touchpadButtonBindingItems.TryGetValue(item.BindingName, out FaceButtonBindingItem existing))
            {
                return existing;
            }

            // Only Steam Controller 2 exposes analog touchpad pressure - every other
            // controller's touchpad click (DualSense, DS4, original Steam Controller) keeps
            // the plain Regular Press behaviour untouched.
            bool isTouchpadPressureCapable = mapper?.DeviceType == InputDeviceType.SteamControllerTriton &&
                PressureCapableClickBindingNames.Contains(item.BindingName);

            FaceButtonBindingItem bindingItem =
                new FaceButtonBindingItem(this, item, displayName, subtitle,
                    isTouchpadPressureCapable, usesClickTerminology, isAvailable,
                    unavailableMessage);
            touchpadButtonBindingItems.Add(item.BindingName, bindingItem);
            touchpadButtonBindings.Add(bindingItem);
            return bindingItem;
        }

        private void PopulateTriggerKeybinds()
        {
            triggerKeybinds.Clear();

            string[][] triggerAliases = new string[][]
            {
                new string[] { "L2", "LT", "LeftTrigger" },
                new string[] { "R2", "RT", "RightTrigger" },
            };

            string[] displayNames = new string[]
            {
                "L2 / Left Trigger",
                "R2 / Right Trigger",
            };

            for (int i = 0; i < triggerAliases.Length; i++)
            {
                TriggerBindingItemsTest item = null;
                foreach (string alias in triggerAliases[i])
                {
                    item = triggerBindings.FirstOrDefault(binding =>
                        string.Equals(binding.BindingName, alias, StringComparison.OrdinalIgnoreCase));
                    if (item != null) break;
                }

                if (item != null)
                {
                    triggerKeybinds.Add(new TriggerKeybindItem(this, item, displayNames[i]));
                }
            }
        }

        internal void UpdateTriggerKeybindAction(TriggerKeybindItem triggerItem, TriggerMapAction newAction)
        {
            if (triggerItem == null || newAction == null) return;

            ActionSet editSet = actionSetItems[selectedActionSetIndex].Set;
            ActionLayer editLayer = layerItems[selectedActionLayerIndex].Layer;
            TriggerMapAction oldAction = triggerItem.MappedAction;

            mapper.ProcessMappingChangeAction(() =>
            {
                oldAction.Release(mapper, ignoreReleaseActions: true);

                if (oldAction.Id != MapAction.DEFAULT_UNBOUND_ID)
                {
                    editLayer.ReplaceTriggerAction(oldAction, newAction);
                }
                else
                {
                    editLayer.AddTriggerAction(newAction);
                }

                if (editSet.UsingCompositeLayer)
                {
                    MapAction baseLayerAction = editSet.DefaultActionLayer.normalActionDict[oldAction.MappingId];
                    if (MapAction.IsSameType(baseLayerAction, newAction))
                    {
                        newAction.SoftCopyFromParent(baseLayerAction as TriggerMapAction);
                    }

                    editSet.RecompileCompositeLayer(mapper);
                }
                else
                {
                    editLayer.SyncActions();
                    editSet.ClearCompositeLayerActions();
                    editSet.PrepareCompositeLayer();
                }
            });

            TriggerBindingItemsTest bindingItem = triggerBindings.FirstOrDefault(binding =>
                binding.BindingName == newAction.MappingId);
            bindingItem?.UpdateAction(newAction);
            triggerItem.UpdateAction(newAction);
        }

        internal int GetNextTriggerActionId(TriggerMapAction oldAction)
        {
            ActionLayer editLayer = layerItems[selectedActionLayerIndex].Layer;
            return oldAction.Id == MapAction.DEFAULT_UNBOUND_ID
                ? editLayer.FindNextAvailableId()
                : oldAction.Id;
        }

        internal TriggerMapAction EnsureEditableTriggerAction(TriggerKeybindItem triggerItem)
        {
            ActionLayer editLayer = layerItems[selectedActionLayerIndex].Layer;
            TriggerMapAction oldAction = triggerItem.MappedAction;

            if (editLayer.LayerActions.Contains(oldAction))
            {
                return oldAction;
            }

            TriggerMapAction newAction = oldAction switch
            {
                TriggerButtonAction => new TriggerButtonAction(),
                TriggerDualStageAction => new TriggerDualStageAction(),
                TriggerTranslate => new TriggerTranslate(),
                TriggerNoAction => new TriggerNoAction(),
                _ => null,
            };

            if (newAction == null) return oldAction;

            newAction.CopyBaseMapProps(oldAction);
            newAction.Id = GetNextTriggerActionId(oldAction);
            if (MapAction.IsSameType(oldAction, newAction))
            {
                newAction.SoftCopyFromParent(oldAction);
            }

            UpdateTriggerKeybindAction(triggerItem, newAction);
            return newAction;
        }

        internal ButtonAction EnsureEditableFaceButtonAction(FaceButtonBindingItem faceItem)
        {
            ActionSet editSet = actionSetItems[selectedActionSetIndex].Set;
            ActionLayer editLayer = layerItems[selectedActionLayerIndex].Layer;
            ButtonMapAction oldAction = faceItem.MappedAction;

            if (oldAction is ButtonAction existingAction &&
                editLayer.LayerActions.Contains(existingAction))
            {
                EnsureRegularPressFunc(existingAction);
                return existingAction;
            }

            ButtonAction newAction = new ButtonAction();
            if (oldAction is ButtonAction oldButtonAction)
            {
                newAction.CopyBaseProps(oldButtonAction);
                newAction.CopyAction(oldButtonAction);
            }
            else
            {
                newAction.CopyBaseProps(oldAction);
                newAction.ActionFuncs.Add(new ActionUtil.NormalPressFunc(
                    new MapperUtil.OutputActionData(
                        MapperUtil.OutputActionData.ActionType.Empty, 0)));
                FaceButtonBindingItem.MarkFunctionsChanged(newAction);
            }

            newAction.MappingId = oldAction.MappingId;
            newAction.Id = editLayer.LayerActions.Contains(oldAction) &&
                oldAction.Id != MapAction.DEFAULT_UNBOUND_ID
                    ? oldAction.Id
                    : editLayer.FindNextAvailableId();

            EnsureRegularPressFunc(newAction);

            mapper.ProcessMappingChangeAction(() =>
            {
                oldAction.Release(mapper, ignoreReleaseActions: true);
                if (editLayer.LayerActions.Contains(oldAction))
                {
                    editLayer.ReplaceButtonAction(oldAction, newAction);
                }
                else
                {
                    editLayer.AddButtonMapAction(newAction);
                }

                if (editSet.UsingCompositeLayer)
                {
                    editSet.RecompileCompositeLayer(mapper);
                }
                else
                {
                    editLayer.SyncActions();
                    editSet.ClearCompositeLayerActions();
                    editSet.PrepareCompositeLayer();
                }
            });

            if (buttonBindingsIndexDict.TryGetValue(newAction.MappingId, out int buttonIndex))
            {
                buttonBindings[buttonIndex].UpdateAction(newAction);
            }

            faceItem.UpdateAction(newAction);
            return newAction;
        }

        // Mirrors EnsureEditableFaceButtonAction for a Steam Controller 2 touchpad click
        // binding: clone-on-write into the current layer, upgrading from whatever the
        // binding currently is (ButtonNoAction on a never-edited binding, or a legacy
        // Regular Press ButtonAction that predates pressure support) into a
        // TouchpadPressureDualStageAction.
        internal TouchpadPressureDualStageAction EnsureEditableTouchpadPressureAction(FaceButtonBindingItem faceItem)
        {
            ActionSet editSet = actionSetItems[selectedActionSetIndex].Set;
            ActionLayer editLayer = layerItems[selectedActionLayerIndex].Layer;
            ButtonMapAction oldAction = faceItem.MappedAction;

            if (oldAction is TouchpadPressureDualStageAction existingAction &&
                editLayer.LayerActions.Contains(existingAction))
            {
                EnsureSoftFullPressFuncs(existingAction);
                return existingAction;
            }

            TouchpadPressureDualStageAction newAction = new TouchpadPressureDualStageAction();
            if (oldAction is TouchpadPressureDualStageAction oldTouchAction)
            {
                newAction.CopyBaseProps(oldTouchAction);
                newAction.CopyAction(oldTouchAction);
            }
            else if (oldAction is ButtonAction oldButtonAction)
            {
                // Legacy Regular Press ButtonAction that predates pressure support (or
                // wasn't migrated for some other reason). Move its entire output onto Full
                // Press, exactly like the load-time migration, leaving Soft Press unbound.
                newAction.CopyBaseProps(oldButtonAction);
                newAction.FullPressActButton.ActionFuncs.AddRange(oldButtonAction.ActionFuncs);
                FaceButtonBindingItem.MarkFunctionsChanged(newAction.FullPressActButton);
            }
            else
            {
                newAction.CopyBaseProps(oldAction);
            }

            newAction.MappingId = oldAction.MappingId;
            newAction.Id = editLayer.LayerActions.Contains(oldAction) &&
                oldAction.Id != MapAction.DEFAULT_UNBOUND_ID
                    ? oldAction.Id
                    : editLayer.FindNextAvailableId();

            EnsureSoftFullPressFuncs(newAction);

            mapper.ProcessMappingChangeAction(() =>
            {
                oldAction.Release(mapper, ignoreReleaseActions: true);
                if (editLayer.LayerActions.Contains(oldAction))
                {
                    editLayer.ReplaceButtonAction(oldAction, newAction);
                }
                else
                {
                    editLayer.AddButtonMapAction(newAction);
                }

                if (editSet.UsingCompositeLayer)
                {
                    editSet.RecompileCompositeLayer(mapper);
                }
                else
                {
                    editLayer.SyncActions();
                    editSet.ClearCompositeLayerActions();
                    editSet.PrepareCompositeLayer();
                }
            });

            if (buttonBindingsIndexDict.TryGetValue(newAction.MappingId, out int buttonIndex))
            {
                buttonBindings[buttonIndex].UpdateAction(newAction);
            }

            faceItem.UpdateAction(newAction);
            return newAction;
        }

        private static void EnsureSoftFullPressFuncs(TouchpadPressureDualStageAction action)
        {
            if (!action.SoftPressActButton.ActionFuncs.OfType<ActionUtil.NormalPressFunc>().Any())
            {
                action.SoftPressActButton.ActionFuncs.Insert(0, new ActionUtil.NormalPressFunc(
                    new MapperUtil.OutputActionData(MapperUtil.OutputActionData.ActionType.Empty, 0)));
            }

            if (!action.FullPressActButton.ActionFuncs.OfType<ActionUtil.NormalPressFunc>().Any())
            {
                action.FullPressActButton.ActionFuncs.Insert(0, new ActionUtil.NormalPressFunc(
                    new MapperUtil.OutputActionData(MapperUtil.OutputActionData.ActionType.Empty, 0)));
            }
        }

        internal void ReleaseFaceAction(FaceButtonBindingItem faceItem)
        {
            if (faceItem?.MappedAction is ButtonAction action)
            {
                action.Release(mapper, ignoreReleaseActions: true);
            }
            else if (faceItem?.MappedAction is TouchpadPressureDualStageAction touchAction)
            {
                touchAction.Release(mapper, ignoreReleaseActions: true);
            }
        }

        // Resolves the JoypadActionCodes a binding corresponds to, via the DisplayName/
        // BindingName convention ActionTriggerItem and BindingItemsTest already share per
        // device mapper (e.g. DS4Mapper's ActionTriggerItem("Cross", BtnSouth) lines up with
        // a BindingItemsTest whose BindingName is "Cross"). Empty means no match, which
        // callers treat as "mirroring unavailable for this button" rather than an error.
        //
        // The two lists are authored independently per device mapper class and have drifted
        // for at least one entry: SteamControllerTritonMapper's BindingList names the stick
        // touch sensor "LSTouch"/"RSTouch" while its own ActionTriggerItems calls the same
        // button "LS Touch"/"RS Touch" (with a space, for a nicer dropdown label). An exact
        // match silently fails for that pairing - normalising away whitespace tolerates this
        // class of drift instead of requiring every device mapper's two lists to agree on
        // spacing exactly.
        internal JoypadActionCodes FindTriggerCodeForBindingName(string bindingName)
        {
            if (string.IsNullOrEmpty(bindingName)) return JoypadActionCodes.Empty;

            string normalizedName = NormalizeBindingKey(bindingName);
            foreach (ActionTriggerItem item in mapper.ActionTriggerItems)
            {
                if (string.Equals(NormalizeBindingKey(item.DisplayName), normalizedName, StringComparison.OrdinalIgnoreCase))
                {
                    return item.Code;
                }
            }

            return JoypadActionCodes.Empty;
        }

        internal FaceButtonBindingItem FindBindingItemForTriggerCode(JoypadActionCodes code)
        {
            if (code == JoypadActionCodes.Empty) return null;

            ActionTriggerItem triggerItem = mapper.ActionTriggerItems
                .FirstOrDefault(item => item.Code == code);
            if (triggerItem == null) return null;

            string normalizedName = NormalizeBindingKey(triggerItem.DisplayName);
            return AllFaceButtonBindingItems()
                .FirstOrDefault(item => string.Equals(NormalizeBindingKey(item.BindingName), normalizedName, StringComparison.OrdinalIgnoreCase));
        }

        private static string NormalizeBindingKey(string value)
        {
            return string.IsNullOrEmpty(value) ? value : value.Replace(" ", "");
        }

        private IEnumerable<FaceButtonBindingItem> AllFaceButtonBindingItems()
        {
            return faceButtonBindings
                .Concat(bumperButtonBindings)
                .Concat(centerButtonBindings)
                .Concat(paddleButtonBindings)
                .Concat(leftStickClickBinding)
                .Concat(rightStickClickBinding)
                .Concat(extraButtonBindings)
                .Concat(touchpadButtonBindings);
        }

        // Full JSM-style mirroring for Sim Press: writing the pairing on one button
        // auto-registers the same combined output and window on the trigger button too, so
        // pressing either one first produces the same result. Reaches every binding surface
        // that has a real JoypadActionCodes identity (Face-shared buttons, D-Pad directions,
        // Trigger Button-mode) - anything without one (e.g. Stick Action Pad directions) can
        // still set a Sim Press trigger pointing at another button, it just can't be mirrored
        // back onto, the same limit the Chorded Press trigger picker already has. Called from
        // the Sim Press trigger/time/output setters on each binding kind's func item.
        internal void ApplySimultaneousPressMirror(JoypadActionCodes sourceCode, ActionUtil.SimultaneousPressFunc sourceFunc)
        {
            if (sourceFunc == null || sourceCode == JoypadActionCodes.Empty) return;

            JoypadActionCodes targetCode = sourceFunc.TriggerButton;
            if (targetCode == JoypadActionCodes.Empty || targetCode == sourceCode) return;

            ButtonAction targetAction = EnsureEditableSimultaneousPressMirrorAction(targetCode);
            if (targetAction == null) return;

            ActionUtil.SimultaneousPressFunc targetFunc = targetAction.ActionFuncs
                .OfType<ActionUtil.SimultaneousPressFunc>().FirstOrDefault();
            bool isNewFunc = targetFunc == null;
            if (isNewFunc)
            {
                targetFunc = new ActionUtil.SimultaneousPressFunc();
            }

            mapper.ProcessMappingChangeAction(() =>
            {
                targetAction.Release(mapper, ignoreReleaseActions: true);

                if (isNewFunc)
                {
                    targetAction.ActionFuncs.Add(targetFunc);
                }

                targetFunc.TriggerButton = sourceCode;
                targetFunc.SimultaneousPressTimeMs = sourceFunc.SimultaneousPressTimeMs;
                targetFunc.OutputActions.Clear();
                foreach (OutputActionData data in sourceFunc.OutputActions)
                {
                    targetFunc.OutputActions.Add(new OutputActionData(data));
                }

                FaceButtonBindingItem.MarkFunctionsChanged(targetAction);
            });

            RefreshSimultaneousPressMirrorTarget(targetCode);
        }

        // Removes a previously-mirrored Sim Press func from the old trigger button when the
        // source button's trigger changes, is cleared, or the binding itself is deleted - but
        // only if that button's Sim Press still points back at the source, so a target the
        // user has since repointed elsewhere independently is left alone.
        internal void RemoveSimultaneousPressMirror(JoypadActionCodes sourceCode, JoypadActionCodes oldTriggerCode)
        {
            if (sourceCode == JoypadActionCodes.Empty || oldTriggerCode == JoypadActionCodes.Empty) return;

            ButtonAction targetAction = ResolveSimultaneousPressMirrorAction(oldTriggerCode);
            ActionUtil.SimultaneousPressFunc targetFunc = targetAction?.ActionFuncs
                .OfType<ActionUtil.SimultaneousPressFunc>().FirstOrDefault();
            if (targetFunc == null || targetFunc.TriggerButton != sourceCode) return;

            int index = targetAction.ActionFuncs.IndexOf(targetFunc);
            if (index < 0) return;

            mapper.ProcessMappingChangeAction(() =>
            {
                targetAction.Release(mapper, ignoreReleaseActions: true);
                targetAction.ActionFuncs.RemoveAt(index);
                FaceButtonBindingItem.MarkFunctionsChanged(targetAction);
            });

            RefreshSimultaneousPressMirrorTarget(oldTriggerCode);
        }

        private ButtonAction EnsureEditableSimultaneousPressMirrorAction(JoypadActionCodes code)
        {
            FaceButtonBindingItem faceItem = FindBindingItemForTriggerCode(code);
            if (faceItem != null) return faceItem.EnsureEditableHostButtonAction(FaceBindingFuncKind.SimultaneousPress);

            DPadDirectionKind? dpadKind = DPadDirectionKindForCode(code);
            if (dpadKind.HasValue) return EnsureEditableDPadDirectionAction(dpadKind.Value);

            TriggerKeybindItem triggerItem = FindTriggerButtonItemForCode(code);
            if (triggerItem != null) return triggerItem.EnsureEditableButtonActionForFunctionEdits()?.EventButton;

            return null;
        }

        private ButtonAction ResolveSimultaneousPressMirrorAction(JoypadActionCodes code)
        {
            FaceButtonBindingItem faceItem = FindBindingItemForTriggerCode(code);
            if (faceItem != null) return faceItem.ResolveHostButtonAction(FaceBindingFuncKind.SimultaneousPress);

            DPadDirectionKind? dpadKind = DPadDirectionKindForCode(code);
            if (dpadKind.HasValue) return PeekDPadDirectionAction(dpadKind.Value);

            TriggerKeybindItem triggerItem = FindTriggerButtonItemForCode(code);
            return (triggerItem?.MappedAction as TriggerButtonAction)?.EventButton;
        }

        private void RefreshSimultaneousPressMirrorTarget(JoypadActionCodes code)
        {
            FaceButtonBindingItem faceItem = FindBindingItemForTriggerCode(code);
            if (faceItem != null)
            {
                faceItem.RefreshFunctions();
                return;
            }

            DPadDirectionKind? dpadKind = DPadDirectionKindForCode(code);
            if (dpadKind.HasValue)
            {
                dpadKeybinds?.Directions.FirstOrDefault(item => item.Kind == dpadKind.Value)?.RefreshFunctions();
                return;
            }

            FindTriggerButtonItemForCode(code)?.RefreshFunctions();
        }

        private static DPadDirectionKind? DPadDirectionKindForCode(JoypadActionCodes code)
        {
            return code switch
            {
                JoypadActionCodes.BtnDPadUp => DPadDirectionKind.Up,
                JoypadActionCodes.BtnDPadDown => DPadDirectionKind.Down,
                JoypadActionCodes.BtnDPadLeft => DPadDirectionKind.Left,
                JoypadActionCodes.BtnDPadRight => DPadDirectionKind.Right,
                _ => null,
            };
        }

        private TriggerKeybindItem FindTriggerButtonItemForCode(JoypadActionCodes code)
        {
            if (code != JoypadActionCodes.AxisLTrigger && code != JoypadActionCodes.AxisRTrigger) return null;

            return triggerKeybinds.FirstOrDefault(item => item.IsButtonMode &&
                FindTriggerCodeForBindingName(item.BindingName) == code);
        }

        internal ButtonMapAction GetCurrentAlwaysOnAction()
        {
            ActionSet editSet = actionSetItems[selectedActionSetIndex].Set;
            ActionLayer editLayer = layerItems[selectedActionLayerIndex].Layer;
            editLayer.actionSetActionDict.TryGetValue(editSet.ActionButtonId,
                out ButtonMapAction action);

            return action;
        }

        internal AlwaysOnBindingItem AddAlwaysOnBinding()
        {
            ButtonMapAction oldAction = GetCurrentAlwaysOnAction();
            if (oldAction == null) return null;
            if (oldAction is not ButtonNoAction)
            {
                return alwaysOnKeybinds.FirstOrDefault();
            }

            ButtonAction newAction = new ButtonAction(new ActionUtil.NormalPressFunc(
                new MapperUtil.OutputActionData(
                    MapperUtil.OutputActionData.ActionType.Empty, 0)));
            newAction.MappingId = oldAction.MappingId;
            newAction.Id = oldAction.Id != MapAction.DEFAULT_UNBOUND_ID
                ? oldAction.Id
                : mapper.EditLayer.FindNextAvailableId();

            ReplaceAlwaysOnAction(oldAction, newAction);
            RefreshLayerBindings();

            AlwaysOnBindingItem item = alwaysOnKeybinds.FirstOrDefault();
            if (item != null)
            {
                item.RestoreActionOnCancel = new ButtonNoAction
                {
                    MappingId = newAction.MappingId,
                    Id = newAction.Id,
                };
            }

            return item;
        }

        internal void RemoveAlwaysOnBinding(AlwaysOnBindingItem item)
        {
            if (item == null) return;

            ButtonMapAction oldAction = item.MappedAction;
            ButtonNoAction newAction = new ButtonNoAction
            {
                MappingId = oldAction.MappingId,
                Id = oldAction.Id,
            };

            ReplaceAlwaysOnAction(oldAction, newAction, copyProps: false);
            RefreshLayerBindings();
        }

        internal void ReplaceAlwaysOnAction(ButtonMapAction oldAction,
            ButtonMapAction newAction, bool copyProps = true)
        {
            if (oldAction == null || newAction == null) return;

            AlwaysOnButtonFuncEditViewModel editVm =
                new AlwaysOnButtonFuncEditViewModel(mapper, oldAction);
            if (newAction.Id == MapAction.DEFAULT_UNBOUND_ID)
            {
                editVm.MigrationActionId(newAction);
            }

            newAction.MappingId = oldAction.MappingId;
            editVm.SwitchLayerAction(oldAction, newAction, copyProps);
        }

        private static void EnsureRegularPressFunc(ButtonAction action)
        {
            if (action.ActionFuncs.OfType<ActionUtil.NormalPressFunc>().Any()) return;

            action.ActionFuncs.Insert(0, new ActionUtil.NormalPressFunc(
                new MapperUtil.OutputActionData(
                    MapperUtil.OutputActionData.ActionType.Empty, 0)));
            FaceButtonBindingItem.MarkFunctionsChanged(action);
        }

        internal DPadMapAction GetCurrentDPadMapAction()
        {
            return dpadBindings.Count > 0 ? dpadBindings[0].MappedAction : null;
        }

        internal ButtonAction PeekDPadDirectionAction(DPadDirectionKind kind)
        {
            if (GetCurrentDPadMapAction() is not DPadAction dpadAction) return null;
            return dpadAction.EventCodes4[(int)ToDpadDirections(kind)];
        }

        internal string GetDPadTranslatedDirectionDisplay(DPadDirectionKind kind)
        {
            if (GetCurrentDPadMapAction() is not DPadTranslate dpadTranslate ||
                dpadTranslate.OutputAction.DpadCode == DPadActionCodes.Empty)
            {
                return "";
            }

            string outputDpad = DPadCodeHelper.Convert(dpadTranslate.OutputAction.DpadCode);
            string direction = kind switch
            {
                DPadDirectionKind.Up => "UP",
                DPadDirectionKind.Down => "DOWN",
                DPadDirectionKind.Left => "LEFT",
                DPadDirectionKind.Right => "RIGHT",
                _ => "",
            };

            return string.IsNullOrWhiteSpace(direction)
                ? outputDpad
                : $"{outputDpad}_{direction}";
        }


        internal DPadAction EnsureActionPadAction()
        {
            DPadBindingItemsTest bindingItem = dpadBindings.Count > 0 ? dpadBindings[0] : null;
            if (bindingItem == null) return null;

            ActionSet editSet = actionSetItems[selectedActionSetIndex].Set;
            ActionLayer editLayer = layerItems[selectedActionLayerIndex].Layer;
            DPadMapAction oldAction = bindingItem.MappedAction;

            if (oldAction is DPadAction existingAction && editLayer.LayerActions.Contains(existingAction))
            {
                return existingAction;
            }

            DPadAction newAction = new DPadAction();
            newAction.CopyBaseMapProps(oldAction);
            newAction.MappingId = oldAction.MappingId;
            newAction.Id = editLayer.LayerActions.Contains(oldAction) &&
                oldAction.Id != MapAction.DEFAULT_UNBOUND_ID
                    ? oldAction.Id
                    : editLayer.FindNextAvailableId();

            mapper.ProcessMappingChangeAction(() =>
            {
                oldAction.Release(mapper, ignoreReleaseActions: true);
                if (editLayer.LayerActions.Contains(oldAction))
                {
                    editLayer.ReplaceDPadAction(oldAction, newAction);
                }
                else
                {
                    editLayer.AddDPadAction(newAction);
                }

                if (editSet.UsingCompositeLayer)
                {
                    editSet.RecompileCompositeLayer(mapper);
                }
                else
                {
                    editLayer.SyncActions();
                    editSet.ClearCompositeLayerActions();
                    editSet.PrepareCompositeLayer();
                }
            });

            bindingItem.UpdateAction(newAction);
            return newAction;
        }

        internal void SetDPadTopLevelMode(DPadTopLevelMode mode)
        {
            DPadMapAction oldAction = GetCurrentDPadMapAction();
            if (oldAction == null) return;

            if ((mode == DPadTopLevelMode.ActionPad && oldAction is DPadAction) ||
                (mode == DPadTopLevelMode.Translate && oldAction is DPadTranslate) ||
                (mode == DPadTopLevelMode.NoAction && oldAction is DPadNoAction))
            {
                return;
            }

            DPadMapAction newAction = mode switch
            {
                DPadTopLevelMode.ActionPad => new DPadAction(),
                DPadTopLevelMode.Translate => new DPadTranslate(),
                DPadTopLevelMode.NoAction => new DPadNoAction(),
                _ => null,
            };
            if (newAction == null) return;

            ReplaceDPadAction(oldAction, newAction);
            PopulateDPadKeybinds();
        }

        internal void SetDPadTranslateName(string name)
        {
            DPadTranslate action = EnsureEditableDPadTranslateAction();
            if (action == null || action.Name == name) return;

            mapper.ProcessMappingChangeAction(() =>
            {
                action.Name = name;
                if (!action.ChangedProperties.Contains(DPadTranslate.PropertyKeyStrings.NAME))
                {
                    action.ChangedProperties.Add(DPadTranslate.PropertyKeyStrings.NAME);
                }
                action.RaiseNotifyPropertyChange(mapper, DPadTranslate.PropertyKeyStrings.NAME);
            });
        }

        internal void SetDPadTranslateOutputDPad(DPadActionCodes code)
        {
            DPadTranslate action = EnsureEditableDPadTranslateAction();
            if (action == null || action.OutputAction.DpadCode == code) return;

            mapper.ProcessMappingChangeAction(() =>
            {
                action.Release(mapper, ignoreReleaseActions: true);
                action.OutputAction.DpadCode = code;
                if (!action.ChangedProperties.Contains(DPadTranslate.PropertyKeyStrings.OUTPUT_PAD))
                {
                    action.ChangedProperties.Add(DPadTranslate.PropertyKeyStrings.OUTPUT_PAD);
                }
                action.RaiseNotifyPropertyChange(mapper, DPadTranslate.PropertyKeyStrings.OUTPUT_PAD);
            });
        }

        private DPadTranslate EnsureEditableDPadTranslateAction()
        {
            DPadBindingItemsTest bindingItem = dpadBindings.Count > 0 ? dpadBindings[0] : null;
            if (bindingItem?.MappedAction is not DPadTranslate oldAction) return null;

            ActionSet editSet = actionSetItems[selectedActionSetIndex].Set;
            ActionLayer editLayer = layerItems[selectedActionLayerIndex].Layer;
            if (editLayer.LayerActions.Contains(oldAction))
            {
                return oldAction;
            }

            DPadTranslate newAction = new DPadTranslate();
            if (editSet.UsingCompositeLayer &&
                editSet.DefaultActionLayer.normalActionDict.TryGetValue(oldAction.MappingId, out MapAction baseAction) &&
                baseAction is DPadTranslate baseTranslate)
            {
                newAction.SoftCopyFromParent(baseTranslate);
            }
            else
            {
                newAction.CopyBaseMapProps(oldAction);
            }

            newAction.Id = editLayer.FindNextAvailableId();
            mapper.ProcessMappingChangeAction(() =>
            {
                oldAction.Release(mapper, ignoreReleaseActions: true);
                editLayer.AddDPadAction(newAction);

                if (editSet.UsingCompositeLayer)
                {
                    editSet.RecompileCompositeLayer(mapper);
                }
                else
                {
                    editLayer.SyncActions();
                    editSet.ClearCompositeLayerActions();
                    editSet.PrepareCompositeLayer();
                }
            });

            bindingItem.UpdateAction(newAction);
            return newAction;
        }

        private void ReplaceDPadAction(DPadMapAction oldAction, DPadMapAction newAction)
        {
            if (oldAction == null || newAction == null) return;

            ActionSet editSet = actionSetItems[selectedActionSetIndex].Set;
            ActionLayer editLayer = layerItems[selectedActionLayerIndex].Layer;

            newAction.CopyBaseMapProps(oldAction);
            newAction.Id = oldAction.Id == MapAction.DEFAULT_UNBOUND_ID
                ? editLayer.FindNextAvailableId()
                : oldAction.Id;

            mapper.ProcessMappingChangeAction(() =>
            {
                oldAction.Release(mapper, ignoreReleaseActions: true);
                if (editLayer.LayerActions.Contains(oldAction) &&
                    oldAction.Id != MapAction.DEFAULT_UNBOUND_ID)
                {
                    editLayer.ReplaceDPadAction(oldAction, newAction);
                }
                else
                {
                    editLayer.AddDPadAction(newAction);
                }

                if (editSet.UsingCompositeLayer)
                {
                    MapAction baseLayerAction = editSet.DefaultActionLayer.normalActionDict[oldAction.MappingId];
                    if (MapAction.IsSameType(baseLayerAction, newAction))
                    {
                        newAction.SoftCopyFromParent(baseLayerAction as DPadMapAction);
                    }

                    editSet.RecompileCompositeLayer(mapper);
                }
                else
                {
                    editLayer.SyncActions();
                    editSet.ClearCompositeLayerActions();
                    editSet.PrepareCompositeLayer();
                }
            });

            if (dpadBindings.Count > 0)
            {
                dpadBindings[0].UpdateAction(newAction);
            }
        }

        internal ButtonAction EnsureEditableDPadDirectionAction(DPadDirectionKind kind)
        {
            DPadAction action = EnsureActionPadAction();
            if (action == null) return null;

            int dirIndex = (int)ToDpadDirections(kind);
            ButtonAction existing = action.EventCodes4[dirIndex];

            if (existing != null && !action.UsingParentActionButton[dirIndex])
            {
                mapper.ProcessMappingChangeAction(() => EnsureRegularPressFunc(existing));
                return existing;
            }

            ButtonAction newButtonAction = new ButtonAction();
            if (existing != null)
            {
                newButtonAction.CopyBaseProps(existing);
                newButtonAction.CopyAction(existing);
            }

            EnsureRegularPressFunc(newButtonAction);

            string propertyKey = ToPadDirPropertyKey(kind);
            mapper.ProcessMappingChangeAction(() =>
            {
                existing?.Release(mapper, ignoreReleaseActions: true);
                action.EventCodes4[dirIndex] = newButtonAction;
                action.UsingParentActionButton[dirIndex] = false;
                if (!action.ChangedProperties.Contains(propertyKey))
                {
                    action.ChangedProperties.Add(propertyKey);
                }
                action.RaiseNotifyPropertyChange(mapper, propertyKey);
            });

            return newButtonAction;
        }

        internal void SetDPadMode(DPadAction.DPadMode mode)
        {
            DPadAction action = EnsureActionPadAction();
            if (action == null || action.CurrentMode == mode) return;

            mapper.ProcessMappingChangeAction(() =>
            {
                action.CurrentMode = mode;
                if (!action.ChangedProperties.Contains(DPadAction.PropertyKeyStrings.PAD_MODE))
                {
                    action.ChangedProperties.Add(DPadAction.PropertyKeyStrings.PAD_MODE);
                }
                action.RaiseNotifyPropertyChange(mapper, DPadAction.PropertyKeyStrings.PAD_MODE);
            });
        }

        private static DpadDirections ToDpadDirections(DPadDirectionKind kind)
        {
            return kind switch
            {
                DPadDirectionKind.Up => DpadDirections.Up,
                DPadDirectionKind.Down => DpadDirections.Down,
                DPadDirectionKind.Left => DpadDirections.Left,
                DPadDirectionKind.Right => DpadDirections.Right,
                _ => DpadDirections.Centered,
            };
        }

        private static string ToPadDirPropertyKey(DPadDirectionKind kind)
        {
            return kind switch
            {
                DPadDirectionKind.Up => DPadAction.PropertyKeyStrings.PAD_DIR_UP,
                DPadDirectionKind.Down => DPadAction.PropertyKeyStrings.PAD_DIR_DOWN,
                DPadDirectionKind.Left => DPadAction.PropertyKeyStrings.PAD_DIR_LEFT,
                DPadDirectionKind.Right => DPadAction.PropertyKeyStrings.PAD_DIR_RIGHT,
                _ => DPadAction.PropertyKeyStrings.NAME,
            };
        }

        public void SwitchActionSets(int ind)
        {
            actionSetItems[selectedActionSetIndex].ItemActive = false;

            selectedActionSetIndex = ind;
            actionSetItems[ind].ItemActive = true;

            actionResetEvent.Reset();
            using (SuppressDirtyTracking())
            {
                mapper.ProcessMappingChangeAction(() =>
                {
                    mapper.ActionProfile.SwitchSets(ind, mapper);
                    mapper.ActionProfile.CurrentActionSet.RecompileCompositeLayer(mapper);

                    actionResetEvent.Set();
                });
            }

            SelectedActionLayerIndex = 0;
        }

        public void SwitchActionLayer(int layerInd)
        {
            layerItems[selectedActionLayerIndex].ItemActive = false;

            selectedActionLayerIndex = layerInd;
            layerItems[layerInd].ItemActive = true;

            actionResetEvent.Reset();
            using (SuppressDirtyTracking())
            {
                mapper.ProcessMappingChangeAction(() =>
                {
                    mapper.ActionProfile.CurrentActionSet.SwitchActionLayer(mapper, layerInd);
                    actionResetEvent.Set();
                });
            }
        }

        public void TestFakeSave(ProfileEntity entity, Profile profile)
        {
            ProfileEntity tempEntity = entity;
            Profile tempProfile = profile;
            string tempOutJson = string.Empty;
            actionResetEvent.Reset();

            mapper.ProcessMappingChangeAction(() =>
            {
                ProfileSerializer profileSerializer = new ProfileSerializer(tempProfile);
                tempOutJson = JsonConvert.SerializeObject(profileSerializer, Formatting.Indented,
                    new JsonSerializerSettings()
                    {
                        //Converters = new List<JsonConverter>()
                        //{
                        //    new MapActionSubTypeConverter(),
                        //}
                        //TypeNameHandling = TypeNameHandling.Objects
                        //ReferenceLoopHandling = ReferenceLoopHandling.Ignore
                    });
                Trace.WriteLine(tempOutJson);

                actionResetEvent.Set();
            });

            // ProcessMappingChangeAction only tries once, for up to 500ms, to halt the
            // input reading thread before giving up and never running the queued action
            // at all. Without a bounded wait here, a missed halt window left this method
            // (and the whole window, which stays disabled while it awaits) hung forever.
            if (!actionResetEvent.Wait(TimeSpan.FromSeconds(5)))
            {
                throw new TimeoutException("Timed out waiting for the mapper thread to become available for saving.");
            }

            if (!string.IsNullOrEmpty(tempOutJson) && overwriteFile)
            {
                AtomicFileWriter.WriteJson(tempEntity.ProfilePath, JObject.Parse(tempOutJson));
            }
        }

        public void TestSave(ProfileEntity entity, Profile profile)
        {
            ProfileEntity tempEntity = entity;
            Profile tempProfile = profile;
            string tempOutJson = string.Empty;
            actionResetEvent.Reset();

            using (SuppressDirtyTracking())
            {
                mapper.ProcessMappingChangeAction(() =>
                {
                    ProfileSerializer profileSerializer = new ProfileSerializer(tempProfile);
                    tempOutJson = JsonConvert.SerializeObject(profileSerializer, Formatting.Indented,
                        new JsonSerializerSettings()
                        {
                            //Converters = new List<JsonConverter>()
                            //{
                            //    new MapActionSubTypeConverter(),
                            //}
                            //TypeNameHandling = TypeNameHandling.Objects
                            //ReferenceLoopHandling = ReferenceLoopHandling.Ignore
                        });
                    //Trace.WriteLine(tempOutJson);

                    actionResetEvent.Set();
                });
            }

            // See comment in TestFakeSave: ProcessMappingChangeAction gives up silently if
            // it can't halt the input reading thread within 500ms, so this wait must be
            // bounded or a missed halt window hangs the whole window forever.
            if (!actionResetEvent.Wait(TimeSpan.FromSeconds(5)))
            {
                throw new TimeoutException("Timed out waiting for the mapper thread to become available for saving.");
            }

            if (!string.IsNullOrEmpty(tempOutJson))
            {
                AtomicFileWriter.WriteJson(tempEntity.ProfilePath, JObject.Parse(tempOutJson));
            }
        }

        public int AddLayer()
        {
            ActionLayer tempLayer = null;
            int newIndex = -1;
            actionResetEvent.Reset();
            mapper.ProcessMappingChangeAction(() =>
            {
                int ind = mapper.ActionProfile.CurrentActionSet.ActionLayers.Count;
                tempLayer = new ActionLayer(ind);
                tempLayer.Name = $"Layer {ind+1}";
                mapper.ActionProfile.CurrentActionSet.ActionLayers.Add(tempLayer);
                newIndex = ind;
                actionResetEvent.Set();
            });

            if (!actionResetEvent.Wait(TimeSpan.FromSeconds(5)) || tempLayer == null || newIndex < 0)
            {
                throw new TimeoutException("Timed out waiting for the mapper thread to create an Action Layer.");
            }

            ActionLayerItemsTest tempItem = new ActionLayerItemsTest(mapper.ActionProfile.CurrentActionSet, tempLayer, layerItems.Count);
            layerItems.Add(tempItem);
            MarkProfileDirty();
            return newIndex;
        }

        public void RemoveLayer()
        {
            if (selectedActionLayerIndex <= 0) return;

            mapper.ProcessMappingChangeAction(() =>
            {
                ActionLayer tempLayer = mapper.ActionProfile.CurrentActionSet.RecentAppliedLayer;
                tempLayer.ReleaseActions(mapper, ignoreReleaseActions: true);
                mapper.ActionProfile.CurrentActionSet.ActionLayers.Remove(tempLayer);
                mapper.ActionProfile.CurrentActionSet.RecompileCompositeLayer(mapper);
            });

            layerItems.RemoveAt(selectedActionLayerIndex);
            SelectedActionLayerIndex = 0;
        }

        public int AddSet()
        {
            ActionSet tempSet = null;
            int newIndex = -1;
            actionResetEvent.Reset();
            mapper.ProcessMappingChangeAction(() =>
            {
                int ind = mapper.ActionProfile.ActionSets.Count;
                tempSet = new ActionSet(ind, $"Set {ind+1}");
                tempSet.DefaultActionLayer.Name = "Default";
                mapper.ActionProfile.ActionSets.Add(tempSet);
                mapper.PrepopulateBlankActionLayer(tempSet.DefaultActionLayer);

                tempSet.ClearCompositeLayerActions();
                tempSet.PrepareCompositeLayer();
                newIndex = ind;
                actionResetEvent.Set();
            });

            if (!actionResetEvent.Wait(TimeSpan.FromSeconds(5)) || tempSet == null || newIndex < 0)
            {
                throw new TimeoutException("Timed out waiting for the mapper thread to create an Action Set.");
            }

            ActionSetItemsTest tempItem = new ActionSetItemsTest(tempSet);
            actionSetItems.Add(tempItem);
            MarkProfileDirty();
            return newIndex;
        }

        public void RemoveSet()
        {
            if (selectedActionSetIndex <= 0) return;

            mapper.ProcessMappingChangeAction(() =>
            {
                ActionSet tempSet = mapper.ActionProfile.CurrentActionSet;
                tempSet.ReleaseActions(mapper, ignoreReleaseActions: true);

                // Switch to default set before removing current ActionSet
                mapper.ActionProfile.SwitchSets(0, mapper);
                mapper.ActionProfile.ActionSets.Remove(tempSet);

                mapper.ActionProfile.CurrentActionSet.RecompileCompositeLayer(mapper);
            });

            actionSetItems.RemoveAt(SelectedActionSetIndex);
            SelectedActionSetIndex = 0;
        }

        public void PopulateMapperEditActionRefs(Mapper mapper)
        {
            mapper.EditActionSet = actionSetItems[selectedActionSetIndex].Set;
            mapper.EditLayer = layerItems[selectedActionLayerIndex].Layer;
        }

        public void ResetMapperEditActionRefs(Mapper mapper)
        {
            mapper.EditActionSet = null;
            mapper.EditLayer = null;
        }

        public void UnregisterEvents()
        {
            steamPadRotation?.Dispose();
            physicalControllerVisibility?.Dispose();
            tempProfile.DirtyChanged -= TempProfile_DirtyChanged;
            mapper.ProfileEditCommitted -= Mapper_ProfileEditCommitted;
        }
    }

    public class ActionLayerItemsTest
    {
        private ActionSet set;
        public ActionSet Set => set;

        private ActionLayer layer;
        public ActionLayer Layer => layer;

        public string DisplayName
        {
            get
            {
                string result = $"Layer {layer.Index+1}";
                if (!string.IsNullOrEmpty(layer.Name))
                {
                    result = layer.Name;
                }

                return result;
            }
        }
        public event EventHandler DisplayNameChanged;

        private int index;
        public int LayerIndex
        {
            get => index;
        }

        private bool itemActive;
        public bool ItemActive
        {
            get => itemActive;
            set
            {
                if (itemActive == value) return;
                itemActive = value;
                ItemActiveChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        public event EventHandler ItemActiveChanged;

        public ActionLayerItemsTest(ActionSet set, ActionLayer layer, int index)
        {
            this.set = set;
            this.layer = layer;
            this.index = index;
        }

        public void RaiseDisplayNameChanged()
        {
            DisplayNameChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public class ActionSetItemsTest
    {
        private ActionSet set;
        public ActionSet Set => set;

        public string DisplayName
        {
            get
            {
                string result = $"Set {set.Index+1}";
                if (!string.IsNullOrEmpty(set.Name))
                {
                    result = set.Name;
                }

                return result;
            }
        }
        public event EventHandler DisplayNameChanged;

        public int SetIndex
        {
            get => set.Index;
        }

        private bool itemActive;
        public bool ItemActive
        {
            get => itemActive;
            set
            {
                if (itemActive == value) return;
                itemActive = value;
                ItemActiveChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        public event EventHandler ItemActiveChanged;

        public ActionSetItemsTest(ActionSet set)
        {
            this.set = set;
        }

        public void RaiseDisplayNameChanged()
        {
            DisplayNameChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public class TouchpadActionOption
    {
        public int Index { get; }
        public string DisplayName { get; }

        public TouchpadActionOption(int index, string displayName)
        {
            Index = index;
            DisplayName = displayName;
        }
    }

    public class TouchBindingItemsTest : INotifyPropertyChanged
    {
        public static readonly IReadOnlyList<TouchpadActionOption> ActionOptions =
            new List<TouchpadActionOption>
            {
                new TouchpadActionOption(0, "Unbound"),
                new TouchpadActionOption(1, "Passthru"),
                new TouchpadActionOption(2, "Joystick"),
                new TouchpadActionOption(3, "Directional Pad"),
                new TouchpadActionOption(4, "Mouse-like Joystick"),
                new TouchpadActionOption(5, "Relative Mouse"),
                new TouchpadActionOption(6, "Circular Scroll"),
                new TouchpadActionOption(7, "Absolute Mouse"),
                new TouchpadActionOption(8, "Directional Swipes"),
                new TouchpadActionOption(9, "Flick Stick"),
            };

        public static readonly IReadOnlyList<TouchpadActionOption> LegacySingleButtonActionOptions =
            new List<TouchpadActionOption>
            {
                new TouchpadActionOption(0, "Unbound"),
                new TouchpadActionOption(1, "Passthru"),
                new TouchpadActionOption(2, "Joystick"),
                new TouchpadActionOption(3, "Directional Pad"),
                new TouchpadActionOption(4, "Mouse-like Joystick"),
                new TouchpadActionOption(5, "Relative Mouse"),
                new TouchpadActionOption(6, "Circular Scroll"),
                new TouchpadActionOption(7, "Absolute Mouse"),
                new TouchpadActionOption(8, "Directional Swipes"),
                new TouchpadActionOption(9, "Single Button"),
                new TouchpadActionOption(10, "Flick Stick"),
            };

        public IReadOnlyList<TouchpadActionOption> AvailableActionOptions =>
            mappedAction is TouchpadSingleButton ? LegacySingleButtonActionOptions : ActionOptions;

        private string displayInputMapString;
        public string DisplayInputMapString
        {
            get => displayInputMapString;
        }

        public bool IsAvailable { get; }
        public string UnavailableMessage { get; }
        public bool HasUnavailableMessage =>
            !IsAvailable && !string.IsNullOrWhiteSpace(UnavailableMessage);

        public string bindingName;
        public string BindingName
        {
            get => bindingName;
            //set => bindingName = value;
        }
        //public event EventHandler BindingNameChanged;

        public FaceButtonBindingItem TouchpadClickBinding { get; set; }
        public bool HasTouchpadClickBinding => TouchpadClickBinding != null;

        private TouchpadMapAction mappedAction;
        public TouchpadMapAction MappedAction
        {
            get => mappedAction;
        }

        public string MappedActionType
        {
            get => mappedAction.ActionTypeName;
        }
        public event EventHandler MappedActionTypeChanged;
        public event PropertyChangedEventHandler PropertyChanged;

        public string DisplayName
        {
            get
            {
                return bindingName switch
                {
                    "Touchpad" or "PrimaryTouchSurface" => "Center Touchpad",
                    "TouchpadLeft" or "LeftTouchpad" or "LeftTouchSurface" => UsesSteamTouchpadSideNames ? "Left Touchpad" : "Left-side Touchpad",
                    "TouchpadRight" or "RightTouchpad" or "RightTouchSurface" => UsesSteamTouchpadSideNames ? "Right Touchpad" : "Right-side Touchpad",
                    _ => displayInputMapString,
                };
            }
        }

        public bool UsesSteamTouchpadSideNames =>
            mapper?.DeviceType == InputDeviceType.SteamController ||
            mapper?.DeviceType == InputDeviceType.SteamControllerTriton;

        public bool UsesClickTerminology =>
            mapper?.DeviceType == InputDeviceType.DS4 ||
            mapper?.DeviceType == InputDeviceType.DualSense;

        public string ActionDisplayName
        {
            get
            {
                return mappedAction switch
                {
                    TouchpadNoAction => "Unbound",
                    TouchpadPassthruAction => "Passthru",
                    TouchpadSingleButton => "Single Button",
                    TouchpadMouse => "Relative Mouse",
                    TouchpadAbsAction => "Absolute Mouse",
                    TouchpadMouseJoystick => "Mouse-like Joystick",
                    TouchpadStickAction => "Joystick",
                    TouchpadActionPad => "Directional Pad",
                    TouchpadDirectionalSwipe => "Directional Swipes",
                    TouchpadCircular => "Circular Scroll",
                    TouchpadFlickStick => "Flick Stick",
                    _ => mappedAction.ActionTypeName,
                };
            }
        }

        public int SelectedActionIndex
        {
            get => GetActionIndex(mappedAction);
            set
            {
                if (!IsAvailable) return;
                if (value == SelectedActionIndex) return;

                TouchpadBindEditViewModel editVM = new TouchpadBindEditViewModel(mapper, mappedAction);
                TouchpadMapAction newAction = editVM.PrepareNewAction(value);
                if (newAction == null) return;

                newAction.CopyBaseMapProps(mappedAction);
                editVM.MigrateActionId(newAction);
                editVM.SwitchAction(newAction);
                mappedAction = newAction;
                RaiseUIUpdate();
            }
        }

        public string ActionSummary
        {
            get
            {
                return mappedAction switch
                {
                    TouchpadNoAction => "No touchpad output is assigned.",
                    TouchpadPassthruAction => $"Native touch coordinates pass through to the virtual PlayStation touchpad output. Touchpad click output is configured separately in {TouchpadClickBindingsLabel}.",
                    TouchpadSingleButton => "Maps touchpad activation to a button-style output.",
                    TouchpadMouse => "Uses touch movement for relative mouse output, including supported trackball settings.",
                    TouchpadAbsAction => "Uses touch position for absolute mouse output.",
                    TouchpadMouseJoystick => "Converts touch movement to mouse-like joystick output.",
                    TouchpadStickAction => "Converts touch movement to joystick output.",
                    TouchpadActionPad => "Maps touchpad regions to directional button outputs.",
                    TouchpadDirectionalSwipe => "Maps supported swipe directions to button outputs.",
                    TouchpadCircular => "Uses circular touch movement for scroll-style output.",
                    TouchpadFlickStick => "Uses touch movement for flick-stick style output.",
                    _ => "Uses an existing DS4MapperTest touchpad action.",
                };
            }
        }

        public string BindingStatus
        {
            get
            {
                return mappedAction switch
                {
                    TouchpadNoAction => "No touchpad output is assigned.",
                    TouchpadPassthruAction => $"Native touch coordinates pass through to the virtual PlayStation touchpad output. Touchpad click output is controlled separately in {TouchpadClickBindingsLabel}. When Center Touchpad is set to Passthru, side touchpad passthru modes are ignored. Action name is in Mode.",
                    TouchpadSingleButton => $"Button binding settings are available in {TouchpadClickBindingsLabel}.",
                    TouchpadMouse => "Movement is in Mouse & Movement. Sensitivity and calibration are in Sensitivity & Calibration. Deadzone, smoothing, and trackball settings are in the later settings tabs. Action name is in Mode.",
                    TouchpadAbsAction => "Movement is in Mouse & Movement. Deadzone, outer ring, and release settings are in the later settings tabs. Action name is in Mode.",
                    TouchpadMouseJoystick => "Movement is in Mouse & Movement. Output curve is in Sensitivity & Calibration. Deadzone, smoothing, and trackball settings are in the later settings tabs. Action name is in Mode.",
                    TouchpadStickAction => "Movement is in Mouse & Movement. Output curve and vertical scale are in Sensitivity & Calibration. Deadzone, smoothing, and outer ring settings are in the later settings tabs. Action name is in Mode.",
                    TouchpadActionPad => "D-Pad mode and direction binds are in Mode Settings. Deadzone and diagonal range are in Zones. Outer ring settings are in Outer Ring.",
                    TouchpadDirectionalSwipe => "Gesture bindings are in Gestures. Deadzone and delay are in Filtering & Stabilisation. Action name is in Mode.",
                    TouchpadCircular => "Scroll settings are available in Trackball & Scroll. Action name is in Mode.",
                    TouchpadFlickStick => "Flick, snap, and rotation settings are in Mouse & Movement. Calibration and multiplier compensation are in Sensitivity & Calibration. Action name is in Mode.",
                    _ => "This touchpad mode uses DS4MapperTest's existing settings.",
                };
            }
        }

        public bool IsMouseMovementAction =>
            mappedAction is TouchpadMouseJoystick ||
            mappedAction is TouchpadStickAction ||
            mappedAction is TouchpadFlickStick;

        public bool IsRelativeMouseAction => mappedAction is TouchpadMouse;

        public bool IsSensitivityCalibrationAction =>
            mappedAction is TouchpadMouse ||
            mappedAction is TouchpadMouseJoystick ||
            mappedAction is TouchpadStickAction ||
            mappedAction is TouchpadFlickStick;

        public bool IsFilteringStabilisationAction =>
            mappedAction is TouchpadMouse ||
            mappedAction is TouchpadMouseJoystick ||
            mappedAction is TouchpadStickAction ||
            mappedAction is TouchpadAbsAction ||
            mappedAction is TouchpadDirectionalSwipe;

        public bool IsZoneAction =>
            mappedAction is TouchpadActionPad;

        public bool IsOuterRingAction =>
            mappedAction is TouchpadAbsAction ||
            mappedAction is TouchpadActionPad ||
            mappedAction is TouchpadStickAction;

        public bool IsGestureAction =>
            mappedAction is TouchpadDirectionalSwipe;

        public bool IsModeSettingsAction =>
            mappedAction is TouchpadActionPad;

        public bool IsTrackballScrollAction =>
            mappedAction is TouchpadMouse ||
            mappedAction is TouchpadMouseJoystick ||
            mappedAction is TouchpadCircular;

        public bool IsAdvancedAction => false;

        public bool IsExtraAction => mappedAction is not TouchpadNoAction;

        public bool IsUnbound => mappedAction is TouchpadNoAction;

        public bool IsWholeTouchpadBinding =>
            bindingName == "Touchpad" ||
            bindingName == "PrimaryTouchSurface";

        public bool IsSideTouchpadBinding =>
            bindingName == "TouchpadLeft" ||
            bindingName == "LeftTouchpad" ||
            bindingName == "TouchpadRight" ||
            bindingName == "RightTouchpad" ||
            bindingName == "LeftTouchSurface" ||
            bindingName == "RightTouchSurface";

        public bool ShowPassthruPrecedenceNotice =>
            UsesClickTerminology &&
            ((IsWholeTouchpadBinding && mappedAction is TouchpadPassthruAction) ||
            IsSideTouchpadBinding);

        public string TouchpadClickBindingsLabel =>
            UsesClickTerminology ? "Click Bindings" : "Press Bindings";

        public string PassthruPrecedenceMessage
        {
            get
            {
                if (IsWholeTouchpadBinding && mappedAction is TouchpadPassthruAction)
                {
                    return $"Center Touchpad passthrough takes priority over left and right touchpad passthrough. While Center Touchpad is set to Passthru, the side touchpad passthru modes are ignored. Touchpad clicks still come from {TouchpadClickBindingsLabel}.";
                }

                if (UsesClickTerminology && IsSideTouchpadBinding && WholeTouchpadUsesPassthru)
                {
                    return $"This {DisplayName.ToLowerInvariant()} passthrough is currently inactive because Center Touchpad is set to Passthru. Turn Center Touchpad off Passthru to use side passthrough here.";
                }

                if (UsesClickTerminology && IsSideTouchpadBinding)
                {
                    return "Side touchpad passthrough only applies when Center Touchpad is not set to Passthru.";
                }

                return string.Empty;
            }
        }

        public bool HasAdvancedTouchpadSettings =>
            IsFilteringStabilisationAction ||
            IsZoneAction ||
            IsOuterRingAction ||
            IsGestureAction ||
            IsTrackballScrollAction ||
            IsAdvancedAction;

        public bool ShowAdvancedTouchpadSettingsEmptyMessage => !HasAdvancedTouchpadSettings;

        public string AdvancedTouchpadSettingsEmptyMessage =>
            IsUnbound
                ? $"{DisplayName} is currently set to Unbound. Choose a touchpad mode in Mode to configure advanced settings."
                : $"{DisplayName} is set to {ActionDisplayName}. This mode has no filtering, zone, trackball, or advanced settings.";

        private Mapper mapper;
        public Mapper Mapper
        {
            get => mapper;
        }

        public TouchBindingItemsTest(string bindingName, string displayInputMap,
            MapAction mappedAction, Mapper mapper, bool isAvailable = true,
            string unavailableMessage = null)
        {
            this.bindingName = bindingName;
            this.displayInputMapString = displayInputMap;
            this.mappedAction = mappedAction as TouchpadMapAction;
            this.mapper = mapper;
            IsAvailable = isAvailable;
            UnavailableMessage = unavailableMessage;
        }

        public void UpdateAction(TouchpadMapAction action)
        {
            this.mappedAction = action;
            RaiseUIUpdate();
        }

        public void RefreshPassthruUI()
        {
            OnPropertyChanged(nameof(ActionSummary));
            OnPropertyChanged(nameof(BindingStatus));
            OnPropertyChanged(nameof(ShowPassthruPrecedenceNotice));
            OnPropertyChanged(nameof(PassthruPrecedenceMessage));
        }

        private void RaiseUIUpdate()
        {
            MappedActionTypeChanged?.Invoke(this, EventArgs.Empty);
            OnPropertyChanged(nameof(MappedAction));
            OnPropertyChanged(nameof(MappedActionType));
            OnPropertyChanged(nameof(ActionDisplayName));
            OnPropertyChanged(nameof(SelectedActionIndex));
            OnPropertyChanged(nameof(ActionSummary));
            OnPropertyChanged(nameof(BindingStatus));
            OnPropertyChanged(nameof(IsMouseMovementAction));
            OnPropertyChanged(nameof(IsRelativeMouseAction));
            OnPropertyChanged(nameof(IsSensitivityCalibrationAction));
            OnPropertyChanged(nameof(IsFilteringStabilisationAction));
            OnPropertyChanged(nameof(IsZoneAction));
            OnPropertyChanged(nameof(IsOuterRingAction));
            OnPropertyChanged(nameof(IsGestureAction));
            OnPropertyChanged(nameof(IsModeSettingsAction));
            OnPropertyChanged(nameof(IsTrackballScrollAction));
            OnPropertyChanged(nameof(IsAdvancedAction));
            OnPropertyChanged(nameof(IsExtraAction));
            OnPropertyChanged(nameof(IsUnbound));
            OnPropertyChanged(nameof(IsWholeTouchpadBinding));
            OnPropertyChanged(nameof(IsSideTouchpadBinding));
            OnPropertyChanged(nameof(ShowPassthruPrecedenceNotice));
            OnPropertyChanged(nameof(TouchpadClickBindingsLabel));
            OnPropertyChanged(nameof(PassthruPrecedenceMessage));
            OnPropertyChanged(nameof(HasAdvancedTouchpadSettings));
            OnPropertyChanged(nameof(ShowAdvancedTouchpadSettingsEmptyMessage));
            OnPropertyChanged(nameof(AdvancedTouchpadSettingsEmptyMessage));
            OnPropertyChanged(nameof(IsAvailable));
            OnPropertyChanged(nameof(UnavailableMessage));
            OnPropertyChanged(nameof(HasUnavailableMessage));
        }

        private bool WholeTouchpadUsesPassthru
        {
            get
            {
                if (mapper?.ActionProfile?.CurrentActionSet?.CurrentActionLayer?.touchpadActionDict != null &&
                    (mapper.ActionProfile.CurrentActionSet.CurrentActionLayer.touchpadActionDict.TryGetValue("Touchpad", out TouchpadMapAction wholeTouchAction) ||
                    mapper.ActionProfile.CurrentActionSet.CurrentActionLayer.touchpadActionDict.TryGetValue("PrimaryTouchSurface", out wholeTouchAction)))
                {
                    return wholeTouchAction.OutputsNativeTouch;
                }

                return false;
            }
        }

        private static int GetActionIndex(TouchpadMapAction action)
        {
            return action switch
            {
                TouchpadNoAction => 0,
                TouchpadPassthruAction => 1,
                TouchpadStickAction => 2,
                TouchpadActionPad => 3,
                TouchpadMouseJoystick => 4,
                TouchpadMouse => 5,
                TouchpadCircular => 6,
                TouchpadAbsAction => 7,
                TouchpadDirectionalSwipe => 8,
                TouchpadSingleButton => 9,
                TouchpadFlickStick => 9,
                _ => -1,
            };
        }

        private void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class BindingItemsTest
    {
        private string displayInputMapString;
        public string DisplayInputMapString
        {
            get => displayInputMapString;
        }

        public string bindingName;
        public string BindingName
        {
            get => bindingName;
            //set => bindingName = value;
        }
        //public event EventHandler BindingNameChanged;

        private ButtonMapAction mappedAction;
        public ButtonMapAction MappedAction
        {
            get => mappedAction;
        }

        public string MappedActionType
        {
            get => mappedAction.ActionTypeName;
        }
        public event EventHandler MappedActionTypeChanged;

        public string DisplayBind
        {
            get
            {
                string result = mappedAction.DescribeActions(mapper);
                if (string.IsNullOrEmpty(result))
                {
                    result = "Unknown";
                }

                return result;
            }
        }
        public event EventHandler DisplayBindChanged;

        private Mapper mapper;
        public Mapper Mapper
        {
            get => mapper;
        }

        public BindingItemsTest(string bindingName, string displayInputMap, MapAction mappedAction, Mapper mapper)
        {
            this.bindingName = bindingName;
            this.displayInputMapString = displayInputMap;
            this.mappedAction = mappedAction as ButtonMapAction;
            this.mapper = mapper;
        }

        public void UpdateAction(MapAction action)
        {
            this.mappedAction = action as ButtonMapAction;
            RaiseUIUpdate();
        }

        private void RaiseUIUpdate()
        {
            MappedActionTypeChanged?.Invoke(this, EventArgs.Empty);
            DisplayBindChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public class AlwaysOnBindingItem : INotifyPropertyChanged
    {
        private readonly ProfileEditorTestViewModel owner;
        private readonly BindingItemsTest sourceItem;

        public event PropertyChangedEventHandler PropertyChanged;

        public ProfileEditorTestViewModel Owner => owner;
        public int Index { get; }
        public string DisplayName => $"Always-On Action {Index + 1}";
        public string HelperText => "Current action set/layer";
        public ButtonMapAction MappedAction => sourceItem.MappedAction;
        public bool IsUnbound => MappedAction is ButtonNoAction;
        public ButtonMapAction RestoreActionOnCancel { get; set; }

        public string DisplayBind
        {
            get
            {
                string result = MappedAction?.DescribeActions(owner.DeviceMapper);
                return string.IsNullOrWhiteSpace(result) ? "Unbound" : result;
            }
        }

        public AlwaysOnBindingItem(ProfileEditorTestViewModel owner,
            BindingItemsTest sourceItem, int index)
        {
            this.owner = owner;
            this.sourceItem = sourceItem;
            Index = index;
        }

        public void Refresh()
        {
            OnPropertyChanged(nameof(MappedAction));
            OnPropertyChanged(nameof(IsUnbound));
            OnPropertyChanged(nameof(DisplayBind));
        }

        private void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class TriggerBindingItemsTest
    {
        private string displayInputMapString;
        public string DisplayInputMapString
        {
            get => displayInputMapString;
        }

        public string bindingName;
        public string BindingName
        {
            get => bindingName;
            //set => bindingName = value;
        }
        //public event EventHandler BindingNameChanged;

        private TriggerMapAction mappedAction;
        public TriggerMapAction MappedAction
        {
            get => mappedAction;
        }

        public string MappedActionType
        {
            get => mappedAction.ActionTypeName;
        }
        public event EventHandler MappedActionTypeChanged;

        private Mapper mapper;
        public Mapper Mapper
        {
            get => mapper;
        }

        public TriggerBindingItemsTest(string bindingName, string displayInputMap,
            MapAction mappedAction, Mapper mapper)
        {
            this.bindingName = bindingName;
            this.displayInputMapString = displayInputMap;
            this.mappedAction = mappedAction as TriggerMapAction;
            this.mapper = mapper;
        }

        public void UpdateAction(TriggerMapAction action)
        {
            this.mappedAction = action;
            RaiseUIUpdate();
        }

        private void RaiseUIUpdate()
        {
            MappedActionTypeChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public class StickBindingItemsTest
    {
        private string displayInputMapString;
        public string DisplayInputMapString
        {
            get => displayInputMapString;
        }

        public string bindingName;
        public string BindingName
        {
            get => bindingName;
            //set => bindingName = value;
        }
        //public event EventHandler BindingNameChanged;

        private StickMapAction mappedAction;
        public StickMapAction MappedAction
        {
            get => mappedAction;
        }

        public string MappedActionType
        {
            get => mappedAction.ActionTypeName;
        }
        public event EventHandler MappedActionTypeChanged;

        private Mapper mapper;
        public Mapper Mapper
        {
            get => mapper;
        }

        public StickBindingItemsTest(string bindingName, string displayInputMap,
            MapAction mappedAction, Mapper mapper)
        {
            this.bindingName = bindingName;
            this.displayInputMapString = displayInputMap;
            this.mappedAction = mappedAction as StickMapAction;
            this.mapper = mapper;
        }

        public void UpdateAction(StickMapAction action)
        {
            this.mappedAction = action;
            RaiseUIUpdate();
        }

        private void RaiseUIUpdate()
        {
            MappedActionTypeChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public class DPadBindingItemsTest
    {
        private string displayInputMapString;
        public string DisplayInputMapString
        {
            get => displayInputMapString;
        }

        public string bindingName;
        public string BindingName
        {
            get => bindingName;
            //set => bindingName = value;
        }
        //public event EventHandler BindingNameChanged;

        private DPadMapAction mappedAction;
        public DPadMapAction MappedAction
        {
            get => mappedAction;
        }

        public string MappedActionType
        {
            get => mappedAction.ActionTypeName;
        }
        public event EventHandler MappedActionTypeChanged;

        private Mapper mapper;
        public Mapper Mapper
        {
            get => mapper;
        }

        public DPadBindingItemsTest(string bindingName, string displayInputMap,
            MapAction mappedAction, Mapper mapper)
        {
            this.bindingName = bindingName;
            this.displayInputMapString = displayInputMap;
            this.mappedAction = mappedAction as DPadMapAction;
            this.mapper = mapper;
        }

        public void UpdateAction(DPadMapAction action)
        {
            this.mappedAction = action;
            RaiseUIUpdate();
        }

        private void RaiseUIUpdate()
        {
            MappedActionTypeChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public class GyroBindingItemsTest : INotifyPropertyChanged
    {
        private string displayInputMapString;
        public string DisplayInputMapString
        {
            get => displayInputMapString;
        }

        public string bindingName;
        public string BindingName
        {
            get => bindingName;
            //set => bindingName = value;
        }
        //public event EventHandler BindingNameChanged;

        private GyroMapAction mappedAction;
        public GyroMapAction MappedAction
        {
            get => mappedAction;
        }

        public string MappedActionType
        {
            get => mappedAction.ActionTypeName;
        }
        public event EventHandler MappedActionTypeChanged;
        public event PropertyChangedEventHandler PropertyChanged;

        public string ActionDisplayName
        {
            get => mappedAction switch
            {
                GyroNoMapAction => "Unbound",
                GyroPassthruAction => "Passthru",
                GyroMouse => "Gyro Mouse",
                GyroMouseJoystick => "Gyro Mouse-like Joystick",
                GyroDirectionalSwipe => "Gyro Directional Swipe",
                _ => mappedAction.ActionTypeName,
            };
        }

        public string BindingStatus
        {
            get => mappedAction switch
            {
                GyroNoMapAction => "Gyro output is disabled.",
                GyroPassthruAction => "Native gyro and accelerometer data pass through to the virtual DualShock 4 output when available.",
                GyroMouse => "Sensitivity, acceleration, and noise steadying settings are available in the Sensitivity and Noise & Steadying tabs.",
                GyroMouseJoystick => "Joystick output settings are available below.",
                GyroDirectionalSwipe => "Swipe deadzone, trigger, and directional binding settings are available below.",
                _ => "Uses an existing DS4MapperTest gyro action.",
            };
        }

        public bool IsUnbound => mappedAction is GyroNoMapAction;

        public bool IsGyroMouseAction => mappedAction is GyroMouse;
        public bool IsGyroMouseJoystickAction => mappedAction is GyroMouseJoystick;
        public bool IsGyroDirSwipeAction => mappedAction is GyroDirectionalSwipe;

        private Mapper mapper;
        public Mapper Mapper
        {
            get => mapper;
        }

        public GyroBindingItemsTest(string bindingName, string displayInputMap,
            MapAction mappedAction, Mapper mapper)
        {
            this.bindingName = bindingName;
            this.displayInputMapString = displayInputMap;
            this.mappedAction = mappedAction as GyroMapAction;
            this.mapper = mapper;
        }

        public void UpdateAction(GyroMapAction action)
        {
            this.mappedAction = action;
            RaiseUIUpdate();
        }

        private void RaiseUIUpdate()
        {
            MappedActionTypeChanged?.Invoke(this, EventArgs.Empty);
            OnPropertyChanged(nameof(MappedAction));
            OnPropertyChanged(nameof(MappedActionType));
            OnPropertyChanged(nameof(ActionDisplayName));
            OnPropertyChanged(nameof(BindingStatus));
            OnPropertyChanged(nameof(IsUnbound));
            OnPropertyChanged(nameof(IsGyroMouseAction));
            OnPropertyChanged(nameof(IsGyroMouseJoystickAction));
            OnPropertyChanged(nameof(IsGyroDirSwipeAction));
        }

        private void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }

    public class SteamControllerPadRotationViewModel : INotifyPropertyChanged, IDisposable
    {
        private readonly SteamControllerDevice device;
        private readonly SteamControllerControllerOptions options;

        public event PropertyChangedEventHandler PropertyChanged;

        public int LeftPadRotation
        {
            get => options.LeftTouchpadRotation;
            set
            {
                if (options.LeftTouchpadRotation == value) return;
                options.LeftTouchpadRotation = value;
            }
        }

        public int RightPadRotation
        {
            get => options.RightTouchpadRotation;
            set
            {
                if (options.RightTouchpadRotation == value) return;
                options.RightTouchpadRotation = value;
            }
        }

        private SteamControllerPadRotationViewModel(SteamControllerDevice device)
        {
            this.device = device;
            options = device.NativeDeviceOptions;
            options.LeftTouchpadRotationChanged += LeftTouchpadRotationChanged;
            options.RightTouchpadRotationChanged += RightTouchpadRotationChanged;
        }

        public static SteamControllerPadRotationViewModel Create(Mapper mapper)
        {
            if (mapper?.BaseDevice is not SteamControllerDevice steamDevice)
            {
                return null;
            }

            return new SteamControllerPadRotationViewModel(steamDevice);
        }

        private void LeftTouchpadRotationChanged(object sender, EventArgs e)
        {
            Save();
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LeftPadRotation)));
        }

        private void RightTouchpadRotationChanged(object sender, EventArgs e)
        {
            Save();
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(RightPadRotation)));
        }

        private void Save()
        {
            AppGlobalDataSingleton.Instance.SaveControllerDeviceSettings(device, device.DeviceOptions);
        }

        public void Dispose()
        {
            options.LeftTouchpadRotationChanged -= LeftTouchpadRotationChanged;
            options.RightTouchpadRotationChanged -= RightTouchpadRotationChanged;
        }
    }

    public sealed class PhysicalControllerVisibilityViewModel : INotifyPropertyChanged, IDisposable
    {
        private readonly InputDeviceBase device;
        private readonly UniversalMapper universalMapper;
        private readonly ControllerOptionsStore options;
        private readonly AppGlobalData appGlobal;
        private readonly bool hasPossibleHidHideTarget;

        public event PropertyChangedEventHandler PropertyChanged;

        public bool IsAvailable => appGlobal?.hidHideInstalled == true &&
            hasPossibleHidHideTarget;

        public bool Enabled
        {
            get => options.HidePhysicalController;
            set
            {
                if (options.HidePhysicalController == value) return;
                options.HidePhysicalController = value;
            }
        }

        public string Description
        {
            get
            {
                if (appGlobal?.hidHideInstalled != true)
                {
                    return "Install HidHide to hide this controller from other apps while the remapper is running. This is an app-level setting and is not tied to any profile. This setting is unavailable because HidHide is not installed.";
                }

                if (!hasPossibleHidHideTarget)
                {
                    return "SDL has not exposed a physical device path or VID/PID for this controller, so HidHide cannot identify the physical device to hide. This is an app-level setting and is not tied to any profile.";
                }

                return "Temporarily hides this physical controller from other apps while the remapper is running. This is an app-level setting and is not tied to any profile. You may need to restart the game.";
            }
        }

        private PhysicalControllerVisibilityViewModel(InputDeviceBase device, AppGlobalData appGlobal)
        {
            this.device = device;
            this.appGlobal = appGlobal;
            options = device.DeviceOptions;
            hasPossibleHidHideTarget = true;
            options.HidePhysicalControllerChanged += HidePhysicalControllerChanged;
        }

        private PhysicalControllerVisibilityViewModel(UniversalMapper mapper)
        {
            universalMapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
            appGlobal = AppGlobalDataSingleton.Instance;
            options = UniversalControllerDeviceOptionsStore.LoadOptions(
                universalMapper.Controller,
                universalMapper.DeviceType);
            hasPossibleHidHideTarget =
                UniversalControllerDeviceOptionsStore.HasPossibleHidHideTarget(
                    universalMapper.Controller.Identity?.DeviceIdentity);
            options.HidePhysicalControllerChanged += HidePhysicalControllerChanged;
        }

        public static PhysicalControllerVisibilityViewModel Create(Mapper mapper)
        {
            if (mapper is UniversalMapper universalMapper)
            {
                return new PhysicalControllerVisibilityViewModel(universalMapper);
            }

            if (mapper?.BaseDevice?.DeviceOptions == null)
            {
                return null;
            }

            return new PhysicalControllerVisibilityViewModel(mapper.BaseDevice, mapper.AppGlobal);
        }

        private void HidePhysicalControllerChanged(object sender, EventArgs e)
        {
            if (universalMapper != null)
            {
                UniversalControllerDeviceOptionsStore.SaveOptions(
                    universalMapper.Controller,
                    universalMapper.DeviceType,
                    options);
            }
            else
            {
                AppGlobalDataSingleton.Instance.SaveControllerDeviceSettings(
                    device,
                    device.DeviceOptions);
            }

            (System.Windows.Application.Current as App)?.Manager?.RefreshControllerVisibilityState();
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Enabled)));
        }

        public void Dispose()
        {
            options.HidePhysicalControllerChanged -= HidePhysicalControllerChanged;
        }
    }
}
