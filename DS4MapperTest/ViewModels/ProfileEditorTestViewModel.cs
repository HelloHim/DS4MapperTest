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

        public bool IsProfileDirty => tempProfile?.Dirty == true;

        public bool SupportsLightbar =>
            mapper?.DeviceType == InputDeviceType.DS4 ||
            mapper?.DeviceType == InputDeviceType.DualSense;

        public void MarkProfileDirty()
        {
            if (suppressDirtyTracking || tempProfile == null) return;
            if (tempProfile.Dirty) return;

            tempProfile.Dirty = true;
            RaisePropertyChanged(nameof(IsProfileDirty));
        }

        public void MarkProfileClean()
        {
            if (tempProfile == null) return;
            if (!tempProfile.Dirty)
            {
                RaisePropertyChanged(nameof(IsProfileDirty));
                return;
            }

            tempProfile.Dirty = false;
            RaisePropertyChanged(nameof(IsProfileDirty));
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
            RestoreProfileDirtyState(wasDirty);
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

        private ObservableCollection<FaceButtonBindingItem> extraButtonBindings =
            new ObservableCollection<FaceButtonBindingItem>();
        public ObservableCollection<FaceButtonBindingItem> ExtraButtonBindings => extraButtonBindings;

        private ObservableCollection<FaceButtonBindingItem> touchpadButtonBindings =
            new ObservableCollection<FaceButtonBindingItem>();
        public ObservableCollection<FaceButtonBindingItem> TouchpadButtonBindings => touchpadButtonBindings;

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

        private SteamControllerPadRotationViewModel steamPadRotation;
        public SteamControllerPadRotationViewModel SteamPadRotation =>
            steamPadRotation ??= SteamControllerPadRotationViewModel.Create(mapper);

        public bool HasSteamPadRotation => SteamPadRotation != null;

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
        };
        public List<EnumChoiceSelection<Mapper.OutputContType>> OutputControllerTypeOptions => outputControllerTypeChoices;

        public Mapper.OutputContType CurrentOutputControllerType
        {
            get => tempProfile.OutputGamepadSettings.OutputGamepad;
            set
            {
                if (tempProfile.OutputGamepadSettings.OutputGamepad == value) return;
                tempProfile.OutputGamepadSettings.OutputGamepad = value;
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

            tempProfile.DirtyChanged += TempProfile_DirtyChanged;
            mapper.ProfileEditCommitted += Mapper_ProfileEditCommitted;
            CurrentLightbarModeChanged += ProfileEditorTestViewModel_CurrentLightbarModeChanged;
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
            RaisePropertyChanged(nameof(IsProfileDirty));
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
                    TouchBindingItemsTest tempItem = new TouchBindingItemsTest(meta.id, meta.displayName, tempTouchAct, mapper);
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
                    TouchBindingItemsTest tempItem = new TouchBindingItemsTest(meta.id, meta.displayName, tempTouchAct, mapper);
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
        }

        private void PopulateTouchpadGroups()
        {
            touchpadMouseMovementBindings.Clear();
            touchpadZoneGestureBindings.Clear();
            touchpadTrackballScrollBindings.Clear();
            touchpadAdvancedBindings.Clear();

            foreach (TouchBindingItemsTest item in touchpadBindings)
            {
                if (item.IsMouseMovementAction)
                {
                    touchpadMouseMovementBindings.Add(item);
                }

                if (item.IsZoneGestureAction)
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
                new string[] { "L3", "LSClick" },
                "Left Stick Click",
                "Stick click button");
            AddFirstMatchingButtonBinding(leftStickClickBinding, claimedButtonBindings,
                new string[] { "LSTouch" },
                "LS Touch / Left Stick Touch",
                "Stick touch sensor");

            rightStickClickBinding.Clear();
            AddFirstMatchingButtonBinding(rightStickClickBinding, claimedButtonBindings,
                new string[] { "R3", "RSClick" },
                "Right Stick Click",
                "Stick click button");
            AddFirstMatchingButtonBinding(rightStickClickBinding, claimedButtonBindings,
                new string[] { "RSTouch" },
                "RS Touch / Right Stick Touch",
                "Stick touch sensor");
        }

        private void PopulateExtraButtonBindings()
        {
            extraButtonBindings.Clear();

            AddFirstMatchingButtonBinding(extraButtonBindings, claimedButtonBindings,
                new string[] { "FnL" },
                "Fn Left",
                "Function button");
            AddFirstMatchingButtonBinding(extraButtonBindings, claimedButtonBindings,
                new string[] { "FnR" },
                "Fn Right",
                "Function button");
            AddFirstMatchingButtonBinding(extraButtonBindings, claimedButtonBindings,
                new string[] { "LeftGripSense" },
                "Left Grip Sense",
                "Grip sensor");
            AddFirstMatchingButtonBinding(extraButtonBindings, claimedButtonBindings,
                new string[] { "RightGripSense" },
                "Right Grip Sense",
                "Grip sensor");
            // Fallback: surface any backend button binding no named section
            // claimed. Follows mapper BindingList order so the list is stable
            foreach (BindingItemsTest item in buttonBindings)
            {
                if (claimedButtonBindings.Contains(item.BindingName))
                {
                    continue;
                }

                claimedButtonBindings.Add(item.BindingName);
                string label = !string.IsNullOrWhiteSpace(item.DisplayInputMapString)
                    ? item.DisplayInputMapString
                    : item.BindingName;
                extraButtonBindings.Add(new FaceButtonBindingItem(this, item, label));
            }
        }

        private void PopulateTouchpadButtonBindings()
        {
            AddTouchpadButtonBinding(
                new string[] { "LeftPadTouch", "LeftTouchpadTouch" },
                "Left Touch",
                "Touchpad touch sensor");
            AddTouchpadButtonBinding(
                new string[] { "RightPadTouch", "RightTouchpadTouch" },
                "Right Touch",
                "Touchpad touch sensor");
            AddTouchpadButtonBinding(
                new string[] { "TouchClick" },
                "Main Click",
                "Physical full-pad click");
            AddTouchpadButtonBinding(
                new string[] { "LeftPadClick", "LeftTouchpadClick" },
                "Left Click",
                "Physical left-pad click");
            AddTouchpadButtonBinding(
                new string[] { "RightPadClick", "RightTouchpadClick" },
                "Right Click",
                "Physical right-pad click");
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
                new string[] { "A", "Cross" },
                new string[] { "B", "Circle" },
                new string[] { "X", "Square" },
                new string[] { "Y", "Triangle" },
            };

            string[] displayNames = new string[]
            {
                "A / Cross",
                "B / Circle",
                "X / Square",
                "Y / Triangle",
            };

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
                new string[] { "L1", "LB", "LShoulder" },
                new string[] { "R1", "RB", "RShoulder" },
            };

            string[] displayNames = new string[]
            {
                "L1 / Left Bumper",
                "R1 / Right Bumper",
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

        private void PopulateCenterButtonBindings()
        {
            centerButtonBindings.Clear();

            AddFirstMatchingButtonBinding(centerButtonBindings, claimedButtonBindings,
                new string[] { "Options", "Start", "Plus" },
                "Options / Menu",
                "System button");
            AddFirstMatchingButtonBinding(centerButtonBindings, claimedButtonBindings,
                new string[] { "Share", "Create", "Capture", "Back", "Minus" },
                "Share / View",
                "System button");
            AddFirstMatchingButtonBinding(centerButtonBindings, claimedButtonBindings,
                new string[] { "PS", "Home", "Guide", "Steam" },
                "PS / Home",
                "System button");
            AddFirstMatchingButtonBinding(centerButtonBindings, claimedButtonBindings,
                new string[] { "Mute" },
                "Mic",
                "System button");
            AddFirstMatchingButtonBinding(centerButtonBindings, claimedButtonBindings,
                new string[] { "QAM" },
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

            AddFirstMatchingButtonBinding(paddleButtonBindings,
                new string[] { "BLP", "L4", "LSideL" },
                "L Paddle 1 / L4 / L SL");
            AddFirstMatchingButtonBinding(paddleButtonBindings,
                new string[] { "BRP", "R4", "RSideL" },
                "R Paddle 1 / R4 / R SL");
            AddFirstMatchingButtonBinding(paddleButtonBindings,
                new string[] { "L5", "PL", "LSideR" },
                "L Paddle 2 / L5 / L SR");
            AddFirstMatchingButtonBinding(paddleButtonBindings,
                new string[] { "R5", "PR", "RSideR" },
                "R Paddle 2 / R5 / R SR");
            AddFirstMatchingButtonBinding(paddleButtonBindings,
                new string[] { "LeftGrip" },
                "Left Grip");
            AddFirstMatchingButtonBinding(paddleButtonBindings,
                new string[] { "RightGrip" },
                "Right Grip");
        }

        private void AddFirstMatchingButtonBinding(
            ObservableCollection<FaceButtonBindingItem> target,
            string[] aliases,
            string displayName)
        {
            AddFirstMatchingButtonBinding(target, claimedButtonBindings, aliases, displayName, null);
        }

        private bool AddFirstMatchingButtonBinding(
            ObservableCollection<FaceButtonBindingItem> target,
            HashSet<string> claimedBindings,
            string[] aliases,
            string displayName,
            string subtitle)
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
                target.Add(new FaceButtonBindingItem(this, item, displayName, subtitle));
                claimedBindings?.Add(item.BindingName);
                return true;
            }

            return false;
        }

        private FaceButtonBindingItem CreateTouchpadClickBinding(TouchBindingItemsTest touchpadItem)
        {
            string[] aliases = touchpadItem.BindingName switch
            {
                "LeftTouchpad" => new string[] { "LeftPadClick", "LeftTouchpadClick" },
                "TouchpadLeft" => new string[] { "LeftPadClick", "LeftTouchpadClick" },
                "RightTouchpad" => new string[] { "RightPadClick", "RightTouchpadClick" },
                "TouchpadRight" => new string[] { "RightPadClick", "RightTouchpadClick" },
                _ => Array.Empty<string>(),
            };

            foreach (string alias in aliases)
            {
                if (buttonBindingsIndexDict.TryGetValue(alias, out int index))
                {
                    return AddTouchpadButtonBinding(buttonBindings[index],
                        touchpadItem.BindingName == "RightTouchpad" || touchpadItem.BindingName == "TouchpadRight"
                            ? "Right Click"
                            : "Left Click",
                        "Physical pad click");
                }
            }

            return null;
        }

        private FaceButtonBindingItem AddTouchpadButtonBinding(string[] aliases,
            string displayName, string subtitle)
        {
            foreach (string alias in aliases)
            {
                if (buttonBindingsIndexDict.TryGetValue(alias, out int index))
                {
                    return AddTouchpadButtonBinding(buttonBindings[index], displayName, subtitle);
                }
            }

            return null;
        }

        private FaceButtonBindingItem AddTouchpadButtonBinding(BindingItemsTest item,
            string displayName, string subtitle)
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

            FaceButtonBindingItem bindingItem =
                new FaceButtonBindingItem(this, item, displayName, subtitle);
            touchpadButtonBindingItems.Add(item.BindingName, bindingItem);
            touchpadButtonBindings.Add(bindingItem);
            return bindingItem;
        }

        private void PopulateTriggerKeybinds()
        {
            triggerKeybinds.Clear();

            string[][] triggerAliases = new string[][]
            {
                new string[] { "L2", "LT" },
                new string[] { "R2", "RT" },
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

        internal void ReleaseFaceAction(FaceButtonBindingItem faceItem)
        {
            if (faceItem?.MappedAction is ButtonAction action)
            {
                action.Release(mapper, ignoreReleaseActions: true);
            }
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

            actionResetEvent.Wait();

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

            actionResetEvent.Wait();

            if (!string.IsNullOrEmpty(tempOutJson))
            {
                AtomicFileWriter.WriteJson(tempEntity.ProfilePath, JObject.Parse(tempOutJson));
            }
        }

        public void AddLayer()
        {
            ActionLayer tempLayer = null;
            mapper.ProcessMappingChangeAction(() =>
            {
                int ind = mapper.ActionProfile.CurrentActionSet.ActionLayers.Count;
                tempLayer = new ActionLayer(ind);
                tempLayer.Name = $"Layer {ind+1}";
                mapper.ActionProfile.CurrentActionSet.ActionLayers.Add(tempLayer);
            });

            ActionLayerItemsTest tempItem = new ActionLayerItemsTest(mapper.ActionProfile.CurrentActionSet, tempLayer, layerItems.Count);
            layerItems.Add(tempItem);
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

        public void AddSet()
        {
            ActionSet tempSet = null;
            mapper.ProcessMappingChangeAction(() =>
            {
                int ind = mapper.ActionProfile.ActionSets.Count;
                tempSet = new ActionSet(ind, $"Set {ind+1}");
                mapper.ActionProfile.ActionSets.Add(tempSet);
                mapper.PrepopulateBlankActionLayer(tempSet.DefaultActionLayer);

                tempSet.ClearCompositeLayerActions();
                tempSet.PrepareCompositeLayer();
            });

            ActionSetItemsTest tempItem = new ActionSetItemsTest(tempSet);
            actionSetItems.Add(tempItem);
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
                new TouchpadActionOption(1, "Joystick"),
                new TouchpadActionOption(2, "D-Pad Zones"),
                new TouchpadActionOption(3, "Mouse-like Joystick"),
                new TouchpadActionOption(4, "Relative Mouse"),
                new TouchpadActionOption(5, "Circular Scroll"),
                new TouchpadActionOption(6, "Absolute Mouse"),
                new TouchpadActionOption(7, "Directional Swipes"),
                new TouchpadActionOption(8, "Single Button"),
                new TouchpadActionOption(9, "Flick Stick"),
            };

        public IReadOnlyList<TouchpadActionOption> AvailableActionOptions => ActionOptions;

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
                    "Touchpad" => "Main Touchpad",
                    "TouchpadLeft" => "Left Touchpad",
                    "LeftTouchpad" => "Left Touchpad",
                    "TouchpadRight" => "Right Touchpad",
                    "RightTouchpad" => "Right Touchpad",
                    _ => displayInputMapString,
                };
            }
        }

        public string ActionDisplayName
        {
            get
            {
                return mappedAction switch
                {
                    TouchpadNoAction => "Unbound",
                    TouchpadSingleButton => "Single Button",
                    TouchpadMouse => "Relative Mouse",
                    TouchpadAbsAction => "Absolute Mouse",
                    TouchpadMouseJoystick => "Mouse-like Joystick",
                    TouchpadStickAction => "Joystick",
                    TouchpadActionPad => "D-Pad Zones",
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
                    TouchpadSingleButton => "Button binding settings are available below.",
                    TouchpadMouse => "Movement is in Mouse & Movement. Sensitivity and calibration are in Sensitivity & Calibration. Deadzone, smoothing, and trackball settings are in the later settings tabs. Action name is in Mode.",
                    TouchpadAbsAction => "Movement is in Mouse & Movement. Deadzone, outer ring, and release settings are in the later settings tabs. Action name is in Mode.",
                    TouchpadMouseJoystick => "Movement is in Mouse & Movement. Output curve is in Sensitivity & Calibration. Deadzone, smoothing, and trackball settings are in the later settings tabs. Action name is in Mode.",
                    TouchpadStickAction => "Movement is in Mouse & Movement. Output curve and vertical scale are in Sensitivity & Calibration. Deadzone, smoothing, and outer ring settings are in the later settings tabs. Action name is in Mode.",
                    TouchpadActionPad => "D-Pad mode and direction binds are in Mode Settings. Deadzone and diagonal range are in Zones & Gestures.",
                    TouchpadDirectionalSwipe => "Gesture bindings, deadzone, and delay are in Zones & Gestures. Action name is in Mode.",
                    TouchpadCircular => "Scroll settings are available in Trackball & Scroll. Action name is in Mode.",
                    TouchpadFlickStick => "Movement is in Mouse & Movement. Calibration is in Sensitivity & Calibration. Flick threshold is in Zones & Gestures. Action name is in Mode.",
                    _ => "This touchpad mode uses DS4MapperTest's existing settings.",
                };
            }
        }

        public bool IsMouseMovementAction =>
            mappedAction is TouchpadMouseJoystick ||
            mappedAction is TouchpadStickAction ||
            mappedAction is TouchpadFlickStick;

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
            mappedAction is TouchpadDirectionalSwipe ||
            mappedAction is TouchpadFlickStick;

        public bool IsZoneGestureAction =>
            mappedAction is TouchpadActionPad ||
            mappedAction is TouchpadDirectionalSwipe;

        public bool IsModeSettingsAction =>
            mappedAction is TouchpadActionPad;

        public bool IsTrackballScrollAction =>
            mappedAction is TouchpadMouse ||
            mappedAction is TouchpadMouseJoystick ||
            mappedAction is TouchpadCircular;

        public bool IsAdvancedAction =>
            mappedAction is TouchpadAbsAction ||
            mappedAction is TouchpadActionPad ||
            mappedAction is TouchpadStickAction;

        public bool IsExtraAction => mappedAction is not TouchpadNoAction;

        public bool IsUnbound => mappedAction is TouchpadNoAction;

        public bool HasAdvancedTouchpadSettings =>
            IsFilteringStabilisationAction ||
            IsZoneGestureAction ||
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
            MapAction mappedAction, Mapper mapper)
        {
            this.bindingName = bindingName;
            this.displayInputMapString = displayInputMap;
            this.mappedAction = mappedAction as TouchpadMapAction;
            this.mapper = mapper;
        }

        public void UpdateAction(TouchpadMapAction action)
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
            OnPropertyChanged(nameof(SelectedActionIndex));
            OnPropertyChanged(nameof(ActionSummary));
            OnPropertyChanged(nameof(BindingStatus));
            OnPropertyChanged(nameof(IsMouseMovementAction));
            OnPropertyChanged(nameof(IsSensitivityCalibrationAction));
            OnPropertyChanged(nameof(IsFilteringStabilisationAction));
            OnPropertyChanged(nameof(IsZoneGestureAction));
            OnPropertyChanged(nameof(IsModeSettingsAction));
            OnPropertyChanged(nameof(IsTrackballScrollAction));
            OnPropertyChanged(nameof(IsAdvancedAction));
            OnPropertyChanged(nameof(IsExtraAction));
            OnPropertyChanged(nameof(IsUnbound));
            OnPropertyChanged(nameof(HasAdvancedTouchpadSettings));
            OnPropertyChanged(nameof(ShowAdvancedTouchpadSettingsEmptyMessage));
            OnPropertyChanged(nameof(AdvancedTouchpadSettingsEmptyMessage));
        }

        private static int GetActionIndex(TouchpadMapAction action)
        {
            return action switch
            {
                TouchpadNoAction => 0,
                TouchpadStickAction => 1,
                TouchpadActionPad => 2,
                TouchpadMouseJoystick => 3,
                TouchpadMouse => 4,
                TouchpadCircular => 5,
                TouchpadAbsAction => 6,
                TouchpadDirectionalSwipe => 7,
                TouchpadSingleButton => 8,
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
                GyroNoMapAction => "No gyro action is assigned.",
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
}
