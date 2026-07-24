using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.ObjectModel;
using DS4MapperTest.ActionUtil;
using DS4MapperTest.ButtonActions;
using DS4MapperTest.GyroActions;
using DS4MapperTest.MapperUtil;
using DS4MapperTest.StickActions;
using DS4MapperTest.TouchpadActions;
using DS4MapperTest.Common;
using System.Threading;

namespace DS4MapperTest.ViewModels
{
    public class ButtonActionEditViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        public enum ActionComboBoxTypes
        {
            None,
            Gamepad,
            Keyboard,
            MouseButton,
            MouseWheelButton,
            RelativeMouseDir,
            LayerOp,
            SetChange,
            CameraTurn,
        }

        private Dictionary<JoypadActionCodes, int> gamepadIndexAliases;
        private Dictionary<int, JoypadActionCodes> revGamepadIndexAliases;
        private List<GamepadCodeItem> gamepadComboItems;
        public List<GamepadCodeItem> GamepadComboItems => gamepadComboItems;

        public bool OutputControllerDisabled => !mapper.ActionProfile.OutputGamepadSettings.Enabled;

        private List<KeyboardCodeItem> keyboardComboItems;
        public List<KeyboardCodeItem> KeyboardComboItems => keyboardComboItems;

        private List<MouseButtonCodeItem> mouseButtonComboItems;
        public List<MouseButtonCodeItem> MouseButtonComboItems => mouseButtonComboItems;

        private List<MouseButtonCodeItem> mouseWheelButtonComboItems;
        public List<MouseButtonCodeItem> MouseWheelButtonComboItems => mouseWheelButtonComboItems;

        private List<MouseDirItem> mouseDirComboItems;
        public List<MouseDirItem> MouseDirComboItems => mouseDirComboItems;

        private List<LayerOpChoiceItem> layerOperationsComboItems;
        public List<LayerOpChoiceItem> LayerOperationsComboItems => layerOperationsComboItems;

        private List<AvailableLayerChoiceItem> availableLayerComboItems;
        public List<AvailableLayerChoiceItem> AvailableLayerComboItems => availableLayerComboItems;

        private List<AvailableSetChoiceItem> availableSetsComboItems;
        public List<AvailableSetChoiceItem> AvailableSetsComboItems => availableSetsComboItems;

        // Keycode, Index
        private Dictionary<uint, int> revKeyCodeDict = new Dictionary<uint, int>();

        private ButtonAction currentAction;
        public ButtonAction CurrentAction
        {
            get => currentAction;
        }

        private Mapper mapper;
        private ActionFunc func;

        private int selectedIndex = -1;
        public int SelectedIndex
        {
            get => selectedIndex;
            set
            {
                selectedIndex = value;
                SelectedIndexChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        public event EventHandler SelectedIndexChanged;

        private int selectedKeyboardIndex = -1;
        public int SelectedKeyboardIndex
        {
            get => selectedKeyboardIndex;
            set
            {
                selectedKeyboardIndex = value;
                SelectedKeyboardIndexChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        public event EventHandler SelectedKeyboardIndexChanged;

        private int selectedMouseButtonIndex = -1;
        public int SelectedMouseButtonIndex
        {
            get => selectedMouseButtonIndex;
            set
            {
                selectedMouseButtonIndex = value;
                SelectedMouseButtonIndexChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        public event EventHandler SelectedMouseButtonIndexChanged;

        private int selectedMouseWheelButtonIndex = -1;
        public int SelectedMouseWheelButtonIndex
        {
            get => selectedMouseWheelButtonIndex;
            set
            {
                selectedMouseWheelButtonIndex = value;
                SelectedMouseWheelButtonIndexChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        public event EventHandler SelectedMouseWheelButtonIndexChanged;

        private bool _tickTimeEnabled;
        public bool TickTimeEnabled
        {
            get
            {
                return _tickTimeEnabled;
            }
            set
            {
                if (_tickTimeEnabled == value) return;
                _tickTimeEnabled = value;
                TickTimeEnabledChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        public event EventHandler TickTimeEnabledChanged;

        private int _tickTime;
        public int TickTime
        {
            get
            {
                return _tickTime;
            }
            set
            {
                if (_tickTime == value) return;
                _tickTime = Math.Clamp(value, 0, 10000);
                TickTimeChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        //public event EventHandler<double> TickTimeValueChanged;
        public event EventHandler TickTimeChanged;

        private bool showWheelTickOptions;
        public bool ShowWheelTickOptions
        {
            get => showWheelTickOptions;
            set
            {
                if (showWheelTickOptions == value) return;
                showWheelTickOptions = value;
                ShowWheelTickOptionsChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        public event EventHandler ShowWheelTickOptionsChanged;


        private int selectedMouseDirIndex = -1;
        public int SelectedMouseDirIndex
        {
            get => selectedMouseDirIndex;
            set
            {
                selectedMouseDirIndex = value;
                SelectedMouseDirIndexChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        public event EventHandler SelectedMouseDirIndexChanged;

        private int mouseXSpeed = OutputActionDataBindSettings.MOUSE_X_SPEED;
        public int MouseXSpeed
        {
            get => mouseXSpeed;
            set
            {
                if (mouseXSpeed == value) return;
                mouseXSpeed = value;
                MouseXSpeedChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        public event EventHandler MouseXSpeedChanged;

        public int MouseXSpeedOutput
        {
            get => mouseXSpeed * OutputActionDataBindSettings.SPEED_UNIT_REFERENCE;
        }
        public event EventHandler MouseXSpeedOutputChanged;

        private int mouseYSpeed = OutputActionDataBindSettings.MOUSE_Y_SPEED;
        public int MouseYSpeed
        {
            get => mouseYSpeed;
            set
            {
                if (mouseYSpeed == value) return;
                mouseYSpeed = value;
                MouseYSpeedChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        public event EventHandler MouseYSpeedChanged;

        public int MouseYSpeedOutput
        {
            get => mouseYSpeed * OutputActionDataBindSettings.SPEED_UNIT_REFERENCE;
        }
        public event EventHandler MouseYSpeedOutputChanged;

        private bool showMouseDirOptions;
        public bool ShowMouseDirOptions
        {
            get => showMouseDirOptions;
            set
            {
                if (showMouseDirOptions == value) return;
                showMouseDirOptions = value;
                ShowMouseDirOptionsChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        public event EventHandler ShowMouseDirOptionsChanged;

        private int selectedLayerOpsIndex = -1;
        public int SelectedLayerOpsIndex
        {
            get => selectedLayerOpsIndex;
            set
            {
                selectedLayerOpsIndex = value;
                SelectedLayerOpsIndexChanged?.Invoke(this, EventArgs.Empty);
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedLayerOpsIndex)));
            }
        }
        public event EventHandler SelectedLayerOpsIndexChanged;

        private int selectedLayerChoiceIndex = -1;
        public int SelectedLayerChoiceIndex
        {
            get => selectedLayerChoiceIndex;
            set
            {
                selectedLayerChoiceIndex = value;
                SelectedLayerChoiceIndexChanged?.Invoke(this, EventArgs.Empty);
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedLayerChoiceIndex)));
            }
        }
        public event EventHandler SelectedLayerChoiceIndexChanged;

        private bool showAvailableLayers;
        public bool ShowAvailableLayers
        {
            get => showAvailableLayers;
            set
            {
                showAvailableLayers = value;
                ShowAvailableLayersChanged?.Invoke(this, EventArgs.Empty);
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ShowAvailableLayers)));
            }
        }
        public event EventHandler ShowAvailableLayersChanged;

        private bool showLayerChangeConditions;
        public bool ShowLayerChangeConditions
        {
            get => showLayerChangeConditions;
            set
            {
                if (showLayerChangeConditions == value) return;
                showLayerChangeConditions = value;
                ShowLayerChangeConditionsChanged?.Invoke(this, EventArgs.Empty);
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ShowLayerChangeConditions)));
            }
        }
        public event EventHandler ShowLayerChangeConditionsChanged;

        private int selectedLayerChangeConditionIndex = 0;
        public int SelectedLayerChangeConditionIndex
        {
            get => selectedLayerChangeConditionIndex;
            set
            {
                selectedLayerChangeConditionIndex = value;
                SelectedLayerChangeConditionIndexChanged?.Invoke(this, EventArgs.Empty);
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedLayerChangeConditionIndex)));
            }
        }
        public event EventHandler SelectedLayerChangeConditionIndexChanged;


        private bool showAvailableSets;
        public bool ShowAvailableSets
        {
            get => showAvailableSets;
            set
            {
                showAvailableSets = value;
                ShowAvailableSetsChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        public event EventHandler ShowAvailableSetsChanged;

        private int selectedSetChoiceIndex = -1;
        public int SelectedSetChoiceIndex
        {
            get => selectedSetChoiceIndex;
            set
            {
                selectedSetChoiceIndex = value;
                SelectedSetChoiceIndexChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        public event EventHandler SelectedSetChoiceIndexChanged;

        private int selectedSetChangeConditionIndex = -1;
        public int SelectedSetChangeConditionIndex
        {
            get => selectedSetChangeConditionIndex;
            set
            {
                selectedSetChangeConditionIndex = value;
                SelectedSetChangeConditionIndexChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        public event EventHandler SelectedSetChangeConditionIndexChanged;

        // Camera Turn
        private const double DEFAULT_CAMERA_TURN_ANGLE = 180.0;
        private const double DEFAULT_CAMERA_TURN_DURATION_MS = 100.0;

        private double cameraTurnAngle = DEFAULT_CAMERA_TURN_ANGLE;
        public double CameraTurnAngle
        {
            get => cameraTurnAngle;
            set { cameraTurnAngle = value; CameraTurnAngleChanged?.Invoke(this, EventArgs.Empty); }
        }
        public event EventHandler CameraTurnAngleChanged;

        private double cameraTurnDurationMs = DEFAULT_CAMERA_TURN_DURATION_MS;
        public double CameraTurnDurationMs
        {
            get => cameraTurnDurationMs;
            set { cameraTurnDurationMs = value; CameraTurnDurationMsChanged?.Invoke(this, EventArgs.Empty); }
        }
        public event EventHandler CameraTurnDurationMsChanged;

        private double cameraTurnCounts360 = 1800.0;
        public double CameraTurnCounts360
        {
            get => cameraTurnCounts360;
            set
            {
                if (cameraTurnCounts360 == value) return;
                cameraTurnCounts360 = value;
                CameraTurnCounts360Changed?.Invoke(this, EventArgs.Empty);
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CameraTurnCounts360)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(MasterCalibrationValue)));
            }
        }
        public event EventHandler CameraTurnCounts360Changed;

        private double cameraTurnRWC = 0.0;
        public double CameraTurnRWC
        {
            get => cameraTurnRWC;
            set
            {
                if (cameraTurnRWC == value) return;
                cameraTurnRWC = value;
                if (!_applyingCameraTurnPreset) TryMatchCameraTurnPreset();
                CameraTurnRWCChanged?.Invoke(this, EventArgs.Empty);
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CameraTurnRWC)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(MasterCalibrationValue)));
            }
        }
        public event EventHandler CameraTurnRWCChanged;

        private bool _cameraTurnReady = false;
        private bool _applyingCameraTurnPreset = false;
        private GameCalibPreset _selectedCameraTurnPreset = GameCalibPreset.Custom;

        public IReadOnlyList<GameCalibPreset> CameraTurnGamePresets => GameCalibPreset.All;

        public GameCalibPreset SelectedCameraTurnPreset
        {
            get => _selectedCameraTurnPreset;
            set
            {
                if (_selectedCameraTurnPreset == value) return;
                _selectedCameraTurnPreset = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedCameraTurnPreset)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedPreset)));
                if (value == null || value.IsCustom || !_cameraTurnReady) return;
                _applyingCameraTurnPreset = true;
                double preservedCounts = CameraTurnCounts360;
                CameraTurnInGameSens = value.RWC * 360.0 / preservedCounts;
                CameraTurnRWC = value.RWC;
                _applyingCameraTurnPreset = false;
            }
        }

        private void SetSelectedCameraTurnPresetCustom()
        {
            _selectedCameraTurnPreset = GameCalibPreset.Custom;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedCameraTurnPreset)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedPreset)));
        }

        private void TryMatchCameraTurnPreset()
        {
            double rwc = IsCountsMode ? cameraTurnCalculatedRWC : cameraTurnRWC;
            GameCalibPreset match = GameCalibPreset.All.FirstOrDefault(
                p => !p.IsCustom &&
                     Math.Abs(p.RWC - rwc) < 1e-3);
            GameCalibPreset next = match ?? GameCalibPreset.Custom;
            if (_selectedCameraTurnPreset == next) return;
            _selectedCameraTurnPreset = next;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedCameraTurnPreset)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedPreset)));
        }

        private void LoadCameraTurnCalibFromProfile(bool updateSelectedSlot)
        {
            cameraTurnInGameSens = mapper.ActionProfile.CalibInGameSens;
            cameraTurnRWC = mapper.ActionProfile.CalibRwc;
            cameraTurnCounts360 = mapper.ActionProfile.CalibCounts > 0.0
                ? mapper.ActionProfile.CalibCounts
                : cameraTurnCounts360;
            cameraTurnCalculatedRWC = cameraTurnCounts360 > 0.0
                ? cameraTurnInGameSens / (360.0 / cameraTurnCounts360)
                : 0.0;

            TryMatchCameraTurnPreset();

            if (updateSelectedSlot && selectedSlotItemIndex >= 0)
            {
                OutputSlotItem slotItem = slotItems[selectedSlotItemIndex];
                if (slotItem.Data.OutputType == OutputActionData.ActionType.CameraTurn)
                {
                    mapper.ProcessMappingChangeAction(() =>
                    {
                        slotItem.Data.cameraTurnCounts360 = cameraTurnCounts360;
                    });
                }
            }
        }

        private void RaiseCameraTurnCalibPropertiesChanged()
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CameraTurnInGameSens)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CameraTurnRWC)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CameraTurnCounts360)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CameraTurnCalculatedRWC)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedCameraTurnPreset)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(InGameSens)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedPreset)));
            RaiseCalibModePropertyChanges();
            CameraTurnCalculatedRWCChanged?.Invoke(this, EventArgs.Empty);
        }

        private double cameraTurnInGameSens = 1.0;
        public double CameraTurnInGameSens
        {
            get => cameraTurnInGameSens;
            set
            {
                if (!_cameraTurnReady) return;
                if (cameraTurnInGameSens == value) return;
                cameraTurnInGameSens = value;
                CameraTurnInGameSensChanged?.Invoke(this, EventArgs.Empty);
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CameraTurnInGameSens)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(InGameSens)));
            }
        }
        public event EventHandler CameraTurnInGameSensChanged;

        // Profile-level RWC/Counts mode toggle, shared across the gyro mouse,
        // flick stick, trackpad mouse and flick turn calibration panels.
        public CalibMode CalibMode
        {
            get => mapper.ActionProfile.CalibMode;
            set
            {
                if (mapper.ActionProfile.CalibMode == value) return;
                mapper.ActionProfile.CalibMode = value;
                RaiseCalibModePropertyChanges();
                SyncCalibFromCameraTurnToProfile();
            }
        }

        public bool IsRwcMode
        {
            get => CalibMode == DS4MapperTest.CalibMode.RwcMode;
            set { if (value) CalibMode = DS4MapperTest.CalibMode.RwcMode; }
        }

        public bool IsCountsMode
        {
            get => CalibMode == DS4MapperTest.CalibMode.CountsMode;
            set { if (value) CalibMode = DS4MapperTest.CalibMode.CountsMode; }
        }

        public string MasterCalibrationLabel => IsCountsMode ? "Counts" : "RWC";

        public double MasterCalibrationValue
        {
            get => IsCountsMode ? CameraTurnCounts360 : CameraTurnRWC;
            set
            {
                if (IsCountsMode) CameraTurnCounts360 = value;
                else CameraTurnRWC = value;
            }
        }

        public IReadOnlyList<GameCalibPreset> GamePresets => CameraTurnGamePresets;

        public GameCalibPreset SelectedPreset
        {
            get => SelectedCameraTurnPreset;
            set => SelectedCameraTurnPreset = value;
        }

        public double InGameSens
        {
            get => CameraTurnInGameSens;
            set => CameraTurnInGameSens = value;
        }

        private void RaiseCalibModePropertyChanges()
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CalibMode)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsRwcMode)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsCountsMode)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(MasterCalibrationLabel)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(MasterCalibrationValue)));
        }

        private void ActionProfile_CalibModeChanged(object sender, EventArgs e)
        {
            RaiseCalibModePropertyChanges();
        }

        private double cameraTurnCalculatedRWC = 0.0;
        public double CameraTurnCalculatedRWC
        {
            get => cameraTurnCalculatedRWC;
            set
            {
                if (cameraTurnCalculatedRWC == value) return;
                cameraTurnCalculatedRWC = value;
                CameraTurnCalculatedRWCChanged?.Invoke(this, EventArgs.Empty);
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CameraTurnCalculatedRWC)));
            }
        }
        public event EventHandler CameraTurnCalculatedRWCChanged;

        private BasicActionCommand copyFlickTurnRWCComm;
        public BasicActionCommand CopyFlickTurnRWCComm => copyFlickTurnRWCComm;

        private bool showCameraTurnOptions;
        public bool ShowCameraTurnOptions
        {
            get => showCameraTurnOptions;
            set
            {
                if (showCameraTurnOptions == value) return;
                showCameraTurnOptions = value;
                if (value)
                {
                    _cameraTurnReady = false;
                    LoadCameraTurnCalibFromProfile(updateSelectedSlot: true);
                    RaiseCameraTurnCalibPropertiesChanged();
                    System.Windows.Application.Current.Dispatcher.BeginInvoke(
                        System.Windows.Threading.DispatcherPriority.Background,
                        new Action(() =>
                        {
                            LoadCameraTurnCalibFromProfile(updateSelectedSlot: true);
                            RaiseCameraTurnCalibPropertiesChanged();
                            System.Windows.Application.Current.Dispatcher.BeginInvoke(
                                System.Windows.Threading.DispatcherPriority.ApplicationIdle,
                                new Action(() =>
                                {
                                    LoadCameraTurnCalibFromProfile(updateSelectedSlot: true);
                                    _cameraTurnReady = true;
                                    RaiseCameraTurnCalibPropertiesChanged();
                                }));
                        }));
                }
                ShowCameraTurnOptionsChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        public event EventHandler ShowCameraTurnOptionsChanged;

        public bool CameraTurnActive
        {
            get
            {
                if (selectedSlotItemIndex < 0) return false;
                return slotItems[selectedSlotItemIndex].Data.OutputType == OutputActionData.ActionType.CameraTurn;
            }
        }
        public event EventHandler CameraTurnActiveChanged;


        private ObservableCollection<OutputSlotItem> slotItems;
        public ObservableCollection<OutputSlotItem> SlotItems => slotItems;

        private int selectedSlotItemIndex = -1;
        public int SelectedSlotItemIndex
        {
            get => selectedSlotItemIndex;
            set
            {
                selectedSlotItemIndex = value;
                SelectedSlotItemIndexChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        public event EventHandler SelectedSlotItemIndexChanged;

        public bool HasMultipleSlots
        {
            get => slotItems.Count > 1;
        }
        public event EventHandler HasMultipleSlotsChanged;

        public bool UnboundActive
        {
            get
            {
                bool result = false;
                if (selectedSlotItemIndex >= 0)
                {
                    result =
                        slotItems[selectedSlotItemIndex].Data.OutputType == OutputActionData.ActionType.Empty;
                }

                return result;
            }
        }
        public event EventHandler UnboundActiveChanged;

        public ButtonActionEditViewModel(Mapper mapper, ButtonAction currentAction, ActionFunc func)
            : this(mapper, currentAction, func, 0)
        {
        }

        public ButtonActionEditViewModel(Mapper mapper, ButtonAction currentAction, ActionFunc func,
            int initialSlotIndex)
        {
            this.currentAction = currentAction;
            this.mapper = mapper;
            this.func = func;

            gamepadComboItems = new List<GamepadCodeItem>();
            keyboardComboItems = new List<KeyboardCodeItem>();
            mouseButtonComboItems = new List<MouseButtonCodeItem>();
            mouseWheelButtonComboItems = new List<MouseButtonCodeItem>();
            mouseDirComboItems = new List<MouseDirItem>();
            layerOperationsComboItems = new List<LayerOpChoiceItem>();
            availableLayerComboItems = new List<AvailableLayerChoiceItem>();
            availableSetsComboItems = new List<AvailableSetChoiceItem>();
            slotItems = new ObservableCollection<OutputSlotItem>();

            PopulateComboBoxAliases();
            revGamepadIndexAliases = new Dictionary<int, JoypadActionCodes>();
            foreach (KeyValuePair<JoypadActionCodes, int> pair in gamepadIndexAliases)
            {
                revGamepadIndexAliases.Add(pair.Value, pair.Key);
            }

            int tempKeyInd = 0;
            keyboardComboItems.ForEach((item) =>
            {
                uint tempCode = ProfileSerializer.EventInputMapper.GetRealEventKey((uint)item.Code);
                revKeyCodeDict.Add(tempCode, tempKeyInd++);
            });

            int tempInd = 0;
            foreach (OutputActionData data in func.OutputActions)
            {
                OutputActionData tempData = data;
                OutputSlotItem tempItem = new OutputSlotItem(tempData, tempInd++);
                slotItems.Add(tempItem);
            }

            if (slotItems.Count > 0)
            {
                int selectedIndex = Math.Clamp(initialSlotIndex, 0, slotItems.Count - 1);
                PrepareControlsForSlot(slotItems[selectedIndex]);
                SelectedSlotItemIndex = selectedIndex;
            }

            copyFlickTurnRWCComm = new BasicActionCommand((parameter) =>
            {
                CameraTurnRWC = cameraTurnCalculatedRWC;
            });

            mapper.ActionProfile.CalibModeChanged += ActionProfile_CalibModeChanged;
            mapper.ActionProfile.OutputGamepadSettings.OutputGamepadChanged += OutputGamepadSettings_OutputGamepadChanged;

            SetupEvents();
        }

        private void OutputGamepadSettings_OutputGamepadChanged(object sender, EventArgs e)
        {
            RefreshGamepadLabels();
        }

        private void RefreshGamepadLabels()
        {
            bool useDualShock4Labels = UsingDualShock4Output;
            foreach (var def in gamepadButtonDefs)
            {
                if (gamepadIndexAliases.TryGetValue(def.Code, out int index))
                {
                    gamepadComboItems[index].DisplayName = useDualShock4Labels ? def.DualShock4Label : def.XboxLabel;
                }
            }
        }

        private void ButtonActionEditViewModel_SelectedSlotItemIndexChanged(object sender, EventArgs e)
        {
            OutputSlotItem item = slotItems[selectedSlotItemIndex];
            PrepareControlsForSlot(item);
        }

        private void SetupEvents()
        {
            ConnectOutputSlotEvents();

            SelectedSlotItemIndexChanged += ButtonActionEditViewModel_SelectedSlotItemIndexChanged;
            slotItems.CollectionChanged += SlotItems_CollectionChanged;
        }

        private void SlotItems_CollectionChanged(object sender,
            System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            HasMultipleSlotsChanged?.Invoke(this, EventArgs.Empty);
        }

        private void ConnectOutputSlotEvents()
        {
            SelectedIndexChanged += ButtonActionEditViewModel_SelectedIndexChanged;
            SelectedKeyboardIndexChanged += ButtonActionEditViewModel_SelectedKeyboardIndexChanged;
            SelectedMouseButtonIndexChanged += ButtonActionEditViewModel_SelectedMouseButtonIndexChanged;
            SelectedMouseWheelButtonIndexChanged += ButtonActionEditViewModel_SelectedMouseWheelButtonIndexChanged;
            TickTimeEnabledChanged += ButtonActionEditViewModel_TickTimeEnabledChanged;
            TickTimeChanged += ButtonActionEditViewModel_TickTimeChanged;
            SelectedMouseDirIndexChanged += ButtonActionEditViewModel_SelectedMouseDirIndexChanged;
            MouseXSpeedChanged += ButtonActionEditViewModel_MouseXSpeedChanged;
            MouseXSpeedChanged += UpdateMouseXSpeedOutput;
            MouseYSpeedChanged += ButtonActionEditViewModel_MouseYSpeedChanged;
            MouseYSpeedChanged += UpdateMouseYSpeedOutput;
            SelectedLayerOpsIndexChanged += ButtonActionEditViewModel_SelectedLayerOpsIndexChanged;
            SelectedLayerChoiceIndexChanged += ButtonActionEditViewModel_SelectedLayerChoiceIndexChanged;
            SelectedLayerChangeConditionIndexChanged += ButtonActionEditViewModel_SelectedLayerChangeConditionIndexChanged;
            SelectedSetChoiceIndexChanged += ButtonActionEditViewModel_SelectedSetChoiceIndexChanged;
            SelectedSetChangeConditionIndexChanged += ButtonActionEditViewModel_SelectedSetChangeConditionIndexChanged;
            CameraTurnAngleChanged += ButtonActionEditViewModel_CameraTurnAngleChanged;
            CameraTurnDurationMsChanged += ButtonActionEditViewModel_CameraTurnDurationMsChanged;
            CameraTurnCounts360Changed += ButtonActionEditViewModel_CameraTurnCounts360Changed;
            CameraTurnRWCChanged += ButtonActionEditViewModel_CameraTurnRWCChanged;
            CameraTurnInGameSensChanged += ButtonActionEditViewModel_CameraTurnInGameSensChanged;
        }

        private void UpdateMouseYSpeedOutput(object sender, EventArgs e)
        {
            MouseYSpeedOutputChanged?.Invoke(this, EventArgs.Empty);
        }

        private void UpdateMouseXSpeedOutput(object sender, EventArgs e)
        {
            MouseXSpeedOutputChanged?.Invoke(this, EventArgs.Empty);
        }

        private void ButtonActionEditViewModel_MouseYSpeedChanged(object sender, EventArgs e)
        {
            int index = selectedMouseDirIndex;
            if (index == -1) return;

            MouseDirItem item = mouseDirComboItems[index];
            mapper.ProcessMappingChangeAction(() =>
            {
                currentAction.Release(mapper, ignoreReleaseActions: true);
                OutputSlotItem slotItem = slotItems[selectedSlotItemIndex];
                OutputActionData tempData = slotItem.Data;

                if (tempData.OutputType != OutputActionData.ActionType.RelativeMouse)
                {
                    tempData.Reset();

                    tempData.Prepare(OutputActionData.ActionType.RelativeMouse, item.Code);
                    tempData.OutputCodeStr = ((OutputActionData.RelativeMouseDir)item.Code).ToString();
                }

                tempData.extraSettings.mouseYSpeed = mouseYSpeed;
            });

            PostSlotChangeChecks();
        }

        private void ButtonActionEditViewModel_MouseXSpeedChanged(object sender, EventArgs e)
        {
            int index = selectedMouseDirIndex;
            if (index == -1) return;

            MouseDirItem item = mouseDirComboItems[index];
            mapper.ProcessMappingChangeAction(() =>
            {
                currentAction.Release(mapper, ignoreReleaseActions: true);
                OutputSlotItem slotItem = slotItems[selectedSlotItemIndex];
                OutputActionData tempData = slotItem.Data;

                if (tempData.OutputType != OutputActionData.ActionType.RelativeMouse)
                {
                    tempData.Reset();

                    tempData.Prepare(OutputActionData.ActionType.RelativeMouse, item.Code);
                    tempData.OutputCodeStr = ((OutputActionData.RelativeMouseDir)item.Code).ToString();
                }

                tempData.extraSettings.mouseXSpeed = mouseXSpeed;
            });

            PostSlotChangeChecks();
        }

        private void ButtonActionEditViewModel_TickTimeEnabledChanged(object sender, EventArgs e)
        {
            int index = selectedMouseWheelButtonIndex;
            if (index == -1) return;

            MouseButtonCodeItem item = mouseWheelButtonComboItems[index];
            // Tick time might get reset to 0. Keep reference to new value for later
            // inspection
            int tempDuration = 0;
            mapper.ProcessMappingChangeAction(() =>
            {
                currentAction.Release(mapper, ignoreReleaseActions: true);
                OutputSlotItem slotItem = slotItems[selectedSlotItemIndex];
                OutputActionData tempData = slotItem.Data;

                if (tempData.OutputType != OutputActionData.ActionType.MouseWheel)
                {
                    tempData.Reset();

                    tempData.Prepare(OutputActionData.ActionType.MouseWheel, item.Code);
                    tempData.OutputCodeStr = ((OutputActionDataSerializer.MouseWheelAliases)item.Code).ToString();
                }

                tempData.checkTick = _tickTimeEnabled;
                if (!tempData.checkTick)
                {
                    tempData.DurationMs = 0;
                }
                else if (tempData.checkTick && tempData.DurationMs == 0)
                {
                    tempData.DurationMs = OutputActionData.DEFAULT_TICK_DURATION_MS;
                }

                tempDuration = tempData.DurationMs;
            });

            PostSlotChangeChecks();

            if (tempDuration != TickTime)
            {
                TickTime = tempDuration;
            }
        }

        private void ButtonActionEditViewModel_TickTimeChanged(object sender, EventArgs e)
        {
            int index = selectedMouseWheelButtonIndex;
            if (index == -1) return;

            MouseButtonCodeItem item = mouseWheelButtonComboItems[index];
            mapper.ProcessMappingChangeAction(() =>
            {
                currentAction.Release(mapper, ignoreReleaseActions: true);
                OutputSlotItem slotItem = slotItems[selectedSlotItemIndex];
                OutputActionData tempData = slotItem.Data;

                if (tempData.OutputType != OutputActionData.ActionType.MouseWheel)
                {
                    tempData.Reset();

                    tempData.Prepare(OutputActionData.ActionType.MouseWheel, item.Code);
                    tempData.OutputCodeStr = ((OutputActionDataSerializer.MouseWheelAliases)item.Code).ToString();
                }

                tempData.DurationMs = _tickTime;

            });

            PostSlotChangeChecks();

            if (_tickTime == 0)
            {
                TickTimeEnabled = false;
            }
        }

        private void ButtonActionEditViewModel_SelectedSetChangeConditionIndexChanged(object sender, EventArgs e)
        {
            int index = selectedSetChangeConditionIndex;
            if (index == -1) return;

            AvailableSetChoiceItem tempItem = availableSetsComboItems[index];
            mapper.ProcessMappingChangeAction(() =>
            {
                currentAction.Release(mapper, ignoreReleaseActions: true);
                OutputSlotItem slotItem = slotItems[selectedSlotItemIndex];
                OutputActionData tempData = slotItem.Data;

                tempData.Reset();
                tempData.Prepare(OutputActionData.ActionType.SwitchSet, 0);
                tempData.ChangeToSet = tempItem.Set.Index;
                tempData.LayerChangeCondition = (OutputActionData.ActionLayerChangeCondition)selectedSetChangeConditionIndex;
            });
        }

        private void ButtonActionEditViewModel_SelectedSetChoiceIndexChanged(object sender, EventArgs e)
        {
            int index = selectedSetChoiceIndex;
            if (index == -1) return;

            ResetComboBoxIndex(ActionComboBoxTypes.SetChange);
            AvailableSetChoiceItem tempItem = availableSetsComboItems[index];
            bool fireConditionChangedEvent = false;
            mapper.ProcessMappingChangeAction(() =>
            {
                currentAction.Release(mapper, ignoreReleaseActions: true);
                OutputSlotItem slotItem = slotItems[selectedSlotItemIndex];
                OutputActionData tempData = slotItem.Data;

                tempData.Reset();
                tempData.Prepare(OutputActionData.ActionType.SwitchSet, 0);
                tempData.ChangeToSet = tempItem.Set.Index;
                if (selectedSetChangeConditionIndex >= 0)
                {
                    tempData.ChangeCondition = (OutputActionData.SetChangeCondition)selectedSetChangeConditionIndex;
                }
                else
                {
                    tempData.ChangeCondition = OutputActionData.SetChangeCondition.Pressed;
                    // Use field to update value without firing event. Set flag to call event
                    // later from main thread
                    selectedSetChangeConditionIndex = 1;
                    fireConditionChangedEvent = true;
                }
            });

            ShowAvailableSets = true;

            if (fireConditionChangedEvent)
            {
                // Need to disconnect VM handler before firing event
                SelectedSetChangeConditionIndexChanged -= ButtonActionEditViewModel_SelectedSetChangeConditionIndexChanged;
                SelectedSetChangeConditionIndexChanged?.Invoke(this, EventArgs.Empty);

                // Re-connect event
                SelectedSetChangeConditionIndexChanged += ButtonActionEditViewModel_SelectedSetChangeConditionIndexChanged;
            }

            PostSlotChangeChecks();
        }

        private void ButtonActionEditViewModel_SelectedMouseDirIndexChanged(object sender, EventArgs e)
        {
            int index = selectedMouseDirIndex;
            if (index == -1) return;

            ResetComboBoxIndex(ActionComboBoxTypes.RelativeMouseDir);
            MouseDirItem item = mouseDirComboItems[index];
            mapper.ProcessMappingChangeAction(() =>
            {
                currentAction.Release(mapper, ignoreReleaseActions: true);
                OutputSlotItem slotItem = slotItems[selectedSlotItemIndex];
                OutputActionData tempData = slotItem.Data;
                tempData.Reset();

                OutputActionData.RelativeMouseDir tempDir = (OutputActionData.RelativeMouseDir)item.Code;
                tempData.Prepare(OutputActionData.ActionType.RelativeMouse, 0);
                tempData.mouseDir = tempDir;
                tempData.OutputCodeStr = tempDir.ToString();
            });

            ShowMouseDirOptions = true;
            PostSlotChangeChecks();
        }

        private void ButtonActionEditViewModel_SelectedMouseWheelButtonIndexChanged(object sender, EventArgs e)
        {
            int index = selectedMouseWheelButtonIndex;
            if (index == -1)
            {
                return;
            }

            ResetComboBoxIndex(ActionComboBoxTypes.MouseWheelButton);
            MouseButtonCodeItem item = mouseWheelButtonComboItems[index];
            mapper.ProcessMappingChangeAction(() =>
            {
                currentAction.Release(mapper, ignoreReleaseActions: true);
                OutputSlotItem slotItem = slotItems[selectedSlotItemIndex];
                OutputActionData tempData = slotItem.Data;
                tempData.Reset();

                tempData.Prepare(OutputActionData.ActionType.MouseWheel, item.Code);
                tempData.OutputCodeStr = ((OutputActionDataSerializer.MouseWheelAliases)item.Code).ToString();
            });

            ShowWheelTickOptions = true;
            PostSlotChangeChecks();
        }

        private void ButtonActionEditViewModel_SelectedLayerChangeConditionIndexChanged(object sender, EventArgs e)
        {
            int index = selectedLayerChangeConditionIndex;
            if (index == -1) return;

            mapper.ProcessMappingChangeAction(() =>
            {
                currentAction.Release(mapper, ignoreReleaseActions: true);
                OutputSlotItem slotItem = slotItems[selectedSlotItemIndex];
                OutputActionData tempData = slotItem.Data;
                tempData.LayerChangeCondition = (OutputActionData.ActionLayerChangeCondition)index;
            });
        }

        private void ButtonActionEditViewModel_SelectedLayerChoiceIndexChanged(object sender, EventArgs e)
        {
            int index = selectedLayerChoiceIndex;
            if (index == -1) return;

            AvailableLayerChoiceItem tempItem = availableLayerComboItems[index];
            LayerOpChoiceItem opItem = layerOperationsComboItems[selectedLayerOpsIndex];
            mapper.ProcessMappingChangeAction(() =>
            {
                currentAction.Release(mapper, ignoreReleaseActions: true);
                OutputSlotItem slotItem = slotItems[selectedSlotItemIndex];
                OutputActionData tempData = slotItem.Data;

                tempData.Reset();
                tempData.Prepare(opItem.LayerOp, 0);
                tempData.ChangeToLayer = tempItem.Layer.Index;
                tempData.FromProfileChangeLayer = tempItem.Layer.Index;
                if (opItem.LayerOp != OutputActionData.ActionType.HoldActionLayer &&
                    selectedLayerChangeConditionIndex >= 0)
                {
                    tempData.LayerChangeCondition = (OutputActionData.ActionLayerChangeCondition)selectedLayerChangeConditionIndex;
                }
            });
        }

        private void ButtonActionEditViewModel_SelectedLayerOpsIndexChanged(object sender, EventArgs e)
        {
            int index = selectedLayerOpsIndex;
            if (index == -1) return;

            ResetComboBoxIndex(ActionComboBoxTypes.LayerOp);
            ShowAvailableLayers = true;

            LayerOpChoiceItem tempItem = layerOperationsComboItems[index];
            switch(tempItem.LayerOp)
            {
                case OutputActionData.ActionType.HoldActionLayer:
                    ShowAvailableLayers = true;
                    SelectedLayerChangeConditionIndex = 0;
                    ShowLayerChangeConditions = false;
                    break;
                case OutputActionData.ActionType.ApplyActionLayer:
                case OutputActionData.ActionType.SwitchActionLayer:
                    ShowAvailableLayers = true;
                    SelectedLayerChangeConditionIndex = 1;
                    ShowLayerChangeConditions = true;
                    break;
                case OutputActionData.ActionType.RemoveActionLayer:
                    ShowAvailableLayers = true;
                    SelectedLayerChangeConditionIndex = 2;
                    ShowLayerChangeConditions = true;
                    SelectedLayerChoiceIndex = -1;
                    break;
                default:
                    break;
            }

            AvailableLayerChoiceItem tempLayerChoiceItem = selectedLayerChoiceIndex != -1 ? availableLayerComboItems[selectedLayerChoiceIndex] : null;
            LayerOpChoiceItem opItem = layerOperationsComboItems[selectedLayerOpsIndex];
            mapper.ProcessMappingChangeAction(() =>
            {
                currentAction.Release(mapper, ignoreReleaseActions: true);
                OutputSlotItem slotItem = slotItems[selectedSlotItemIndex];
                OutputActionData tempData = slotItem.Data;

                tempData.Reset();
                tempData.Prepare(opItem.LayerOp, 0);

                if (tempLayerChoiceItem != null)
                {
                    tempData.ChangeToLayer = tempLayerChoiceItem.Layer.Index;
                    tempData.FromProfileChangeLayer = tempLayerChoiceItem.Layer.Index;
                }

                if (opItem.LayerOp != OutputActionData.ActionType.HoldActionLayer &&
                    selectedLayerChangeConditionIndex >= 0)
                {
                    tempData.LayerChangeCondition = (OutputActionData.ActionLayerChangeCondition)selectedLayerChangeConditionIndex;
                }
            });

            PostSlotChangeChecks();
        }

        private void DisconnectOutputSlotEvents()
        {
            SelectedIndexChanged -= ButtonActionEditViewModel_SelectedIndexChanged;
            SelectedKeyboardIndexChanged -= ButtonActionEditViewModel_SelectedKeyboardIndexChanged;
            SelectedMouseButtonIndexChanged -= ButtonActionEditViewModel_SelectedMouseButtonIndexChanged;
            SelectedMouseWheelButtonIndexChanged -= ButtonActionEditViewModel_SelectedMouseWheelButtonIndexChanged;
            TickTimeEnabledChanged -= ButtonActionEditViewModel_TickTimeEnabledChanged;
            TickTimeChanged -= ButtonActionEditViewModel_TickTimeChanged;
            MouseXSpeedChanged -= ButtonActionEditViewModel_MouseXSpeedChanged;
            MouseXSpeedChanged -= UpdateMouseXSpeedOutput;
            MouseYSpeedChanged -= ButtonActionEditViewModel_MouseYSpeedChanged;
            MouseYSpeedChanged -= UpdateMouseYSpeedOutput;
            SelectedMouseDirIndexChanged -= ButtonActionEditViewModel_SelectedMouseDirIndexChanged;
            SelectedLayerOpsIndexChanged -= ButtonActionEditViewModel_SelectedLayerOpsIndexChanged;
            SelectedLayerChoiceIndexChanged -= ButtonActionEditViewModel_SelectedLayerChoiceIndexChanged;
            SelectedSetChoiceIndexChanged -= ButtonActionEditViewModel_SelectedSetChoiceIndexChanged;
            SelectedSetChangeConditionIndexChanged -= ButtonActionEditViewModel_SelectedSetChangeConditionIndexChanged;
            CameraTurnAngleChanged -= ButtonActionEditViewModel_CameraTurnAngleChanged;
            CameraTurnDurationMsChanged -= ButtonActionEditViewModel_CameraTurnDurationMsChanged;
            CameraTurnCounts360Changed -= ButtonActionEditViewModel_CameraTurnCounts360Changed;
            CameraTurnRWCChanged -= ButtonActionEditViewModel_CameraTurnRWCChanged;
            CameraTurnInGameSensChanged -= ButtonActionEditViewModel_CameraTurnInGameSensChanged;
        }

        private void PostSlotChangeChecks()
        {
            UnboundActiveChanged?.Invoke(this, EventArgs.Empty);
        }

        private void PrepareControlsForSlot(OutputSlotItem item)
        {
            UnboundActiveChanged?.Invoke(this, EventArgs.Empty);
            ResetComboBoxIndex(ActionComboBoxTypes.None);

            DisconnectOutputSlotEvents();

            switch (item.Data.OutputType)
            {
                case OutputActionData.ActionType.GamepadControl:
                    {
                        JoypadActionCodes temp = item.Data.JoypadCode;
                        if (gamepadIndexAliases.TryGetValue(temp, out int tempInd))
                        {
                            SelectedIndex = tempInd;
                        }
                        else
                        {
                            SelectedIndex = -1;
                        }
                    }

                    break;
                case OutputActionData.ActionType.Keyboard:
                    {
                        if (item.Data.OutputCodeAlias > 0)
                        {
                            if (revKeyCodeDict.TryGetValue(item.Data.OutputCodeAlias,
                                out int keyInd))
                            {
                                //int keyInd = revKeyCodeDict[item.Data.OutputCodeAlias];
                                SelectedKeyboardIndex = keyInd;
                            }
                        }
                        else
                        {
                            SelectedKeyboardIndex = -1;
                        }
                    }

                    break;
                case OutputActionData.ActionType.MouseButton:
                    {
                        int code = item.Data.OutputCode;
                        MouseButtonCodeItem tempMBItem = mouseButtonComboItems.FirstOrDefault((item) => item.Code == code);
                        if (tempMBItem != null)
                        {
                            SelectedMouseButtonIndex = tempMBItem.Index;
                        }
                    }

                    break;
                case OutputActionData.ActionType.MouseWheel:
                    {
                        int code = item.Data.OutputCode;
                        MouseButtonCodeItem tempWheelItem = mouseWheelButtonComboItems.FirstOrDefault((item) => item.Code == code);
                        if (tempWheelItem != null)
                        {
                            SelectedMouseWheelButtonIndex = tempWheelItem.Index;
                            TickTimeEnabled = item.Data.CheckTick;
                            TickTime = item.Data.DurationMs;
                            ShowWheelTickOptions = true;
                        }
                    }

                    break;
                case OutputActionData.ActionType.RelativeMouse:
                    {
                        OutputActionData.RelativeMouseDir tempDir = item.Data.mouseDir;
                        int dirCode = (int)tempDir;
                        MouseDirItem tempItem = mouseDirComboItems.FirstOrDefault((item) => item.Code == dirCode);
                        if (tempItem != null)
                        {
                            SelectedMouseDirIndex = tempItem.Index;
                            MouseXSpeed = item.Data.extraSettings.mouseXSpeed;
                            MouseYSpeed = item.Data.extraSettings.mouseYSpeed;
                            ShowMouseDirOptions = true;
                        }
                    }
                    break;
                case OutputActionData.ActionType.SwitchSet:
                    {
                        int ind = availableSetsComboItems.FindIndex((setItem) => setItem.Set.Index == item.Data.ChangeToSet);
                        if (ind >= 0)
                        {
                            SelectedSetChoiceIndex = ind;
                            SelectedSetChangeConditionIndex = (int)item.Data.ChangeCondition;
                            ShowAvailableSets = true;
                        }
                    }

                    break;
                case OutputActionData.ActionType.HoldActionLayer:
                case OutputActionData.ActionType.ApplyActionLayer:
                case OutputActionData.ActionType.RemoveActionLayer:
                case OutputActionData.ActionType.SwitchActionLayer:
                    {
                        int ind = layerOperationsComboItems.FindIndex((opItem) => opItem.LayerOp == item.Data.OutputType);
                        if (ind >= 0)
                        {
                            SelectedLayerOpsIndex = ind;
                            SelectedLayerChoiceIndex = item.Data.ChangeToLayer;
                            ShowAvailableLayers = true;

                            if (item.Data.OutputType != OutputActionData.ActionType.HoldActionLayer)
                            {
                                SelectedLayerChangeConditionIndex = (int)item.Data.LayerChangeCondition;
                                ShowLayerChangeConditions = true;
                            }
                            else
                            {
                                SelectedLayerChangeConditionIndex = 0;
                                ShowLayerChangeConditions = false;
                                //SelectedLayerChoiceIndex = -1;
                            }
                        }
                    }

                    break;
                case OutputActionData.ActionType.CameraTurn:
                    {
                        cameraTurnAngle = item.Data.cameraTurnAngle;
                        cameraTurnDurationMs = item.Data.cameraTurnDurationMs;
                        cameraTurnCounts360 = item.Data.cameraTurnCounts360;
                        cameraTurnInGameSens = mapper.ActionProfile.CalibInGameSens;
                        cameraTurnRWC = mapper.ActionProfile.CalibRwc;
                        cameraTurnCalculatedRWC = cameraTurnCounts360 > 0.0
                            ? cameraTurnInGameSens / (360.0 / cameraTurnCounts360)
                            : 0.0;
                        ShowCameraTurnOptions = true;
                        CameraTurnActiveChanged?.Invoke(this, EventArgs.Empty);
                        CameraTurnCalculatedRWCChanged?.Invoke(this, EventArgs.Empty);
                    }

                    break;
                default:
                    break;
            }

            ConnectOutputSlotEvents();
        }

        private void ButtonActionEditViewModel_SelectedKeyboardIndexChanged(object sender, EventArgs e)
        {
            int index = selectedKeyboardIndex;
            if (index == -1)
            {
                return;
            }

            ResetComboBoxIndex(ActionComboBoxTypes.Keyboard);
            KeyboardCodeItem item = keyboardComboItems[index];
            mapper.ProcessMappingChangeAction(() =>
            {
                currentAction.Release(mapper, ignoreReleaseActions: true);
                OutputSlotItem slotItem = slotItems[selectedSlotItemIndex];
                OutputActionData tempData = slotItem.Data;
                if (tempData.OutputType == OutputActionData.ActionType.Keyboard)
                {
                    tempData.Reset();

                    uint tempCode = ProfileSerializer.EventInputMapper.GetRealEventKey((uint)item.Code);
                    tempData.Prepare(OutputActionData.ActionType.Keyboard, (int)item.Code);
                    tempData.OutputCodeStr = item.CodeAlias;
                    tempData.OutputCodeAlias = tempCode;
                }
                else
                {
                    tempData.Reset();

                    uint tempCode = ProfileSerializer.EventInputMapper.GetRealEventKey((uint)item.Code);
                    //tempData = new OutputActionData(OutputActionData.ActionType.Keyboard, tempCode);
                    tempData.Prepare(OutputActionData.ActionType.Keyboard, (int)tempCode);
                    tempData.OutputCodeStr = item.CodeAlias;
                    tempData.OutputCodeAlias = tempCode;
                }
            });

            PostSlotChangeChecks();
        }

        private void ButtonActionEditViewModel_SelectedIndexChanged(object sender, EventArgs e)
        {
            int index = selectedIndex;
            if (index == -1)
            {
                return;
            }

            ResetComboBoxIndex(ActionComboBoxTypes.Gamepad);
            JoypadActionCodes temp = revGamepadIndexAliases[index];
            OutputSlotItem item = slotItems[selectedSlotItemIndex];
            mapper.ProcessMappingChangeAction(() =>
            {
                currentAction.Release(mapper, ignoreReleaseActions: true);
                //currentAction.ActionFuncs[0].Release(mapper);

                OutputActionData tempData = item.Data;
                if (tempData.OutputType == OutputActionData.ActionType.GamepadControl)
                {
                    tempData.Reset();

                    tempData.OutputType = OutputActionData.ActionType.GamepadControl;
                    tempData.JoypadCode = temp;
                }
                else
                {
                    tempData.Reset();

                    tempData.OutputType = OutputActionData.ActionType.GamepadControl;
                    tempData.JoypadCode = temp;
                    //tempData =
                    //    new OutputActionData(OutputActionData.ActionType.GamepadControl, temp);
                }
            });

            PostSlotChangeChecks();
        }

        private void ButtonActionEditViewModel_SelectedMouseButtonIndexChanged(object sender, EventArgs e)
        {
            int index = selectedMouseButtonIndex;
            if (index == -1)
            {
                return;
            }

            ResetComboBoxIndex(ActionComboBoxTypes.MouseButton);
            MouseButtonCodeItem item = mouseButtonComboItems[index];
            mapper.ProcessMappingChangeAction(() =>
            {
                currentAction.Release(mapper, ignoreReleaseActions: true);
                OutputSlotItem slotItem = slotItems[selectedSlotItemIndex];
                OutputActionData tempData = slotItem.Data;
                tempData.Reset();

                tempData.Prepare(OutputActionData.ActionType.MouseButton, item.Code);
                tempData.OutputCodeStr = ((OutputActionDataSerializer.MouseButtonOutputAliases)item.Code).ToString();
            });

            PostSlotChangeChecks();
        }

        private void ResetComboBoxIndex(ActionComboBoxTypes ignoreCombo)
        {
            if (ignoreCombo != ActionComboBoxTypes.Gamepad)
            {
                SelectedIndex = -1;
            }

            if (ignoreCombo != ActionComboBoxTypes.Keyboard)
            {
                SelectedKeyboardIndex = -1;
            }

            if (ignoreCombo != ActionComboBoxTypes.MouseButton)
            {
                SelectedMouseButtonIndex = -1;
            }

            if (ignoreCombo != ActionComboBoxTypes.MouseWheelButton)
            {
                SelectedMouseWheelButtonIndex = -1;
                TickTimeEnabled = false;
                TickTime = 0;
                ShowWheelTickOptions = false;
            }

            if (ignoreCombo != ActionComboBoxTypes.RelativeMouseDir)
            {
                SelectedMouseDirIndex = -1;
                ShowMouseDirOptions = false;
            }

            if (ignoreCombo != ActionComboBoxTypes.LayerOp)
            {
                SelectedLayerOpsIndex = -1;
                SelectedLayerChoiceIndex = -1;
                ShowAvailableLayers = false;
                SelectedLayerChangeConditionIndex = -1;
            }

            if (ignoreCombo != ActionComboBoxTypes.CameraTurn)
            {
                ShowCameraTurnOptions = false;
            }
        }

        // Button name shown per output controller type. DPad and stick axes read the
        // same on both pads, so only the face/shoulder/special buttons need two labels.
        private static readonly (JoypadActionCodes Code, string XboxLabel, string DualShock4Label)[] gamepadButtonDefs =
            new (JoypadActionCodes, string, string)[]
            {
                (JoypadActionCodes.X360_A, "A", "Cross"),
                (JoypadActionCodes.X360_B, "B", "Circle"),
                (JoypadActionCodes.X360_X, "X", "Square"),
                (JoypadActionCodes.X360_Y, "Y", "Triangle"),
                (JoypadActionCodes.X360_LB, "LB", "L1"),
                (JoypadActionCodes.X360_RB, "RB", "R1"),
                (JoypadActionCodes.X360_LT, "LT", "L2"),
                (JoypadActionCodes.X360_RT, "RT", "R2"),
                (JoypadActionCodes.X360_Guide, "Guide", "PS"),
                (JoypadActionCodes.X360_Back, "Back", "Share"),
                (JoypadActionCodes.X360_Start, "Start", "Options"),
                (JoypadActionCodes.X360_ThumbL, "LStick Click", "L3"),
                (JoypadActionCodes.X360_ThumbR, "RStick Click", "R3"),
                (JoypadActionCodes.X360_DPAD_UP, "DPad Up", "DPad Up"),
                (JoypadActionCodes.X360_DPAD_DOWN, "DPad Down", "DPad Down"),
                (JoypadActionCodes.X360_DPAD_LEFT, "DPad Left", "DPad Left"),
                (JoypadActionCodes.X360_DPAD_RIGHT, "DPad Right", "DPad Right"),
            };

        private bool UsingDualShock4Output =>
            mapper.ActionProfile.OutputGamepadSettings.OutputGamepad == Mapper.OutputContType.DualShock4;

        public void PopulateComboBoxAliases()
        {
            int tempInd = 0;

            {
                bool useDualShock4Labels = UsingDualShock4Output;

                tempInd = 0;
                gamepadComboItems.Add(new GamepadCodeItem("Unbound", JoypadActionCodes.Empty, tempInd++));
                foreach (var def in gamepadButtonDefs)
                {
                    string label = useDualShock4Labels ? def.DualShock4Label : def.XboxLabel;
                    gamepadComboItems.Add(new GamepadCodeItem(label, def.Code, tempInd++));
                }
                // Real hardware feature, only present on DS4/DualSense pads, so it keeps
                // its DS4 prefix and appears the same regardless of output controller type.
                gamepadComboItems.Add(new GamepadCodeItem("DS4_Touchpad_Click", JoypadActionCodes.CenterPadClick, tempInd++));

                gamepadComboItems.AddRange(new GamepadCodeItem[]
                {
                    new GamepadCodeItem("LS Up", JoypadActionCodes.AxisLYNeg, tempInd++),
                    new GamepadCodeItem("LS Down", JoypadActionCodes.AxisLYPos, tempInd++),
                    new GamepadCodeItem("LS Left", JoypadActionCodes.AxisLXNeg, tempInd++),
                    new GamepadCodeItem("LS Right", JoypadActionCodes.AxisLXPos, tempInd++),
                    new GamepadCodeItem("RS Up", JoypadActionCodes.AxisRYNeg, tempInd++),
                    new GamepadCodeItem("RS Down", JoypadActionCodes.AxisRYPos, tempInd++),
                    new GamepadCodeItem("RS Left", JoypadActionCodes.AxisRXNeg, tempInd++),
                    new GamepadCodeItem("RS Right", JoypadActionCodes.AxisRXPos, tempInd++),
                });

                gamepadIndexAliases = new Dictionary<JoypadActionCodes, int>();
                gamepadComboItems.ForEach((item) =>
                {
                    gamepadIndexAliases.Add(item.Code, item.Index);
                });
            }

            tempInd = 0;
            keyboardComboItems.AddRange(new KeyboardCodeItem[]
            {
                new KeyboardCodeItem("A", VirtualKeys.A, "A", tempInd++),
                new KeyboardCodeItem("B", VirtualKeys.B, "B", tempInd++),
                new KeyboardCodeItem("C", VirtualKeys.C, "C", tempInd++),
                new KeyboardCodeItem("D", VirtualKeys.D, "D", tempInd++),
                new KeyboardCodeItem("E", VirtualKeys.E, "E", tempInd++),
                new KeyboardCodeItem("F", VirtualKeys.F, "F", tempInd++),
                new KeyboardCodeItem("G", VirtualKeys.G, "G", tempInd++),
                new KeyboardCodeItem("H", VirtualKeys.H, "H", tempInd++),
                new KeyboardCodeItem("I", VirtualKeys.I, "I", tempInd++),
                new KeyboardCodeItem("J", VirtualKeys.J, "J", tempInd++),
                new KeyboardCodeItem("K", VirtualKeys.K, "K", tempInd++),
                new KeyboardCodeItem("L", VirtualKeys.L, "L", tempInd++),
                new KeyboardCodeItem("M", VirtualKeys.M, "M", tempInd++),
                new KeyboardCodeItem("N", VirtualKeys.N, "N", tempInd++),
                new KeyboardCodeItem("O", VirtualKeys.O, "O", tempInd++),
                new KeyboardCodeItem("P", VirtualKeys.P, "P", tempInd++),
                new KeyboardCodeItem("Q", VirtualKeys.Q, "Q", tempInd++),
                new KeyboardCodeItem("R", VirtualKeys.R, "R", tempInd++),
                new KeyboardCodeItem("S", VirtualKeys.S, "S", tempInd++),
                new KeyboardCodeItem("T", VirtualKeys.T, "T", tempInd++),
                new KeyboardCodeItem("U", VirtualKeys.U, "U", tempInd++),
                new KeyboardCodeItem("V", VirtualKeys.V, "V", tempInd++),
                new KeyboardCodeItem("W", VirtualKeys.W, "W", tempInd++),
                new KeyboardCodeItem("X", VirtualKeys.X, "X", tempInd++),
                new KeyboardCodeItem("Y", VirtualKeys.Y, "Y", tempInd++),
                new KeyboardCodeItem("Z", VirtualKeys.Z, "Z", tempInd++),
                new KeyboardCodeItem("0", VirtualKeys.N0, "N0", tempInd++),
                new KeyboardCodeItem("1", VirtualKeys.N1, "N1", tempInd++),
                new KeyboardCodeItem("2", VirtualKeys.N2, "N2", tempInd++),
                new KeyboardCodeItem("3", VirtualKeys.N3, "N3", tempInd++),
                new KeyboardCodeItem("4", VirtualKeys.N4, "N4", tempInd++),
                new KeyboardCodeItem("5", VirtualKeys.N5, "N5", tempInd++),
                new KeyboardCodeItem("6", VirtualKeys.N6, "N6", tempInd++),
                new KeyboardCodeItem("7", VirtualKeys.N7, "N7", tempInd++),
                new KeyboardCodeItem("8", VirtualKeys.N8, "N8", tempInd++),
                new KeyboardCodeItem("9", VirtualKeys.N9, "N9", tempInd++),

                new KeyboardCodeItem("Escape", VirtualKeys.Escape, "Escape", tempInd++),
                new KeyboardCodeItem("Space", VirtualKeys.Space, "Space", tempInd++),
                new KeyboardCodeItem("Tab", VirtualKeys.Tab, "Tab", tempInd++),
                new KeyboardCodeItem("Grave", VirtualKeys.OEM3, "Grave", tempInd++),
                new KeyboardCodeItem("Caps Lock", VirtualKeys.CapsLock, "CapsLock", tempInd++),
                new KeyboardCodeItem("Minus", VirtualKeys.OEMMinus, "Minus", tempInd++),
                new KeyboardCodeItem("Equal", VirtualKeys.OEMPlus, "Equal", tempInd++),
                new KeyboardCodeItem("Left Bracket", VirtualKeys.OEM4, "LeftBracket", tempInd++),
                new KeyboardCodeItem("Right Bracket", VirtualKeys.OEM6, "RightBracket", tempInd++),
                new KeyboardCodeItem("Backslash", VirtualKeys.OEM5, "Backslash", tempInd++),
                new KeyboardCodeItem("Semicolon", VirtualKeys.OEM1, "Semicolon", tempInd++),
                new KeyboardCodeItem("Quote", VirtualKeys.OEM7, "Quote", tempInd++),
                new KeyboardCodeItem("Comma", VirtualKeys.OEMComma, "Comma", tempInd++),
                new KeyboardCodeItem("Period", VirtualKeys.OEMPeriod, "Period", tempInd++),
                new KeyboardCodeItem("Slash", VirtualKeys.OEM2, "Slash", tempInd++),

                new KeyboardCodeItem("Insert", VirtualKeys.Insert, "Insert", tempInd++),
                new KeyboardCodeItem("Delete", VirtualKeys.Delete, "Delete", tempInd++),
                new KeyboardCodeItem("Home", VirtualKeys.Home, "Home", tempInd++),
                new KeyboardCodeItem("End", VirtualKeys.End, "End", tempInd++),
                new KeyboardCodeItem("Page Up", VirtualKeys.Prior, "PageUp", tempInd++),
                new KeyboardCodeItem("Page Down", VirtualKeys.Next, "PageDown", tempInd++),

                new KeyboardCodeItem("Enter", VirtualKeys.Return, "Enter", tempInd++),
                new KeyboardCodeItem("Print Screen", VirtualKeys.Snapshot, "PrintScreen", tempInd++),
                new KeyboardCodeItem("Scroll Lock", VirtualKeys.ScrollLock, "ScrollLock", tempInd++),
                new KeyboardCodeItem("Pause", VirtualKeys.Pause, "Pause", tempInd++),

                new KeyboardCodeItem("Left Alt", VirtualKeys.LeftMenu, "LeftAlt", tempInd++),
                new KeyboardCodeItem("Right Alt", VirtualKeys.RightMenu, "RightAlt", tempInd++),
                new KeyboardCodeItem("Left Shift", VirtualKeys.LeftShift, "LeftShift", tempInd++),
                new KeyboardCodeItem("Right Shift", VirtualKeys.RightShift, "RightShift", tempInd++),
                new KeyboardCodeItem("Left Control", VirtualKeys.LeftControl, "LeftControl", tempInd++),
                new KeyboardCodeItem("Right Control", VirtualKeys.RightControl, "RightControl", tempInd++),
                new KeyboardCodeItem("LWin", VirtualKeys.LeftWindows, "LeftWindows", tempInd++),
                new KeyboardCodeItem("RLWin", VirtualKeys.RightWindows, "RightWindows", tempInd++),
                new KeyboardCodeItem("Up", VirtualKeys.Up, "Up", tempInd++),
                new KeyboardCodeItem("Down", VirtualKeys.Down, "Down", tempInd++),
                new KeyboardCodeItem("Left", VirtualKeys.Left, "Left", tempInd++),
                new KeyboardCodeItem("Right", VirtualKeys.Right, "Right", tempInd++),

                new KeyboardCodeItem("F1", VirtualKeys.F1, "F1", tempInd++),
                new KeyboardCodeItem("F2", VirtualKeys.F2, "F2", tempInd++),
                new KeyboardCodeItem("F3", VirtualKeys.F3, "F3", tempInd++),
                new KeyboardCodeItem("F4", VirtualKeys.F4, "F4", tempInd++),
                new KeyboardCodeItem("F5", VirtualKeys.F5, "F5", tempInd++),
                new KeyboardCodeItem("F6", VirtualKeys.F6, "F6", tempInd++),
                new KeyboardCodeItem("F7", VirtualKeys.F7, "F7", tempInd++),
                new KeyboardCodeItem("F8", VirtualKeys.F8, "F8", tempInd++),
                new KeyboardCodeItem("F9", VirtualKeys.F9, "F9", tempInd++),
                new KeyboardCodeItem("F10", VirtualKeys.F10, "F10", tempInd++),
                new KeyboardCodeItem("F11", VirtualKeys.F11, "F11", tempInd++),
                new KeyboardCodeItem("F12", VirtualKeys.F12, "F12", tempInd++),
            });

            tempInd = 0;
            mouseButtonComboItems.AddRange(new MouseButtonCodeItem[]
            {
                new MouseButtonCodeItem("Left Button", MouseButtonCodes.MOUSE_LEFT_BUTTON, tempInd++),
                new MouseButtonCodeItem("Right Button", MouseButtonCodes.MOUSE_RIGHT_BUTTON, tempInd++),
                new MouseButtonCodeItem("Middle Button", MouseButtonCodes.MOUSE_MIDDLE_BUTTON, tempInd++),
                new MouseButtonCodeItem("Mouse 4", MouseButtonCodes.MOUSE_XBUTTON1, tempInd++),
                new MouseButtonCodeItem("Mouse 5", MouseButtonCodes.MOUSE_XBUTTON2, tempInd++),
            });

            tempInd = 0;
            mouseWheelButtonComboItems.AddRange(new MouseButtonCodeItem[]
            {
                new MouseButtonCodeItem("Wheel Up", (int)MouseWheelCodes.WheelUp, tempInd++),
                new MouseButtonCodeItem("Wheel Down", (int)MouseWheelCodes.WheelDown, tempInd++),
                new MouseButtonCodeItem("Wheel Left", (int)MouseWheelCodes.WheelLeft, tempInd++),
                new MouseButtonCodeItem("Wheel Right", (int)MouseWheelCodes.WheelRight, tempInd++),
            });

            tempInd = 0;
            mouseDirComboItems.AddRange(new MouseDirItem[]
            {
                new MouseDirItem("Mouse Up", (int)OutputActionData.RelativeMouseDir.MouseUp, tempInd++),
                new MouseDirItem("Mouse Down", (int)OutputActionData.RelativeMouseDir.MouseDown, tempInd++),
                new MouseDirItem("Mouse Left", (int)OutputActionData.RelativeMouseDir.MouseLeft, tempInd++),
                new MouseDirItem("Mouse Right", (int)OutputActionData.RelativeMouseDir.MouseRight, tempInd++),
            });

            tempInd = 0;
            layerOperationsComboItems.AddRange(new LayerOpChoiceItem[]
            {
                new LayerOpChoiceItem("Hold Layer", OutputActionData.ActionType.HoldActionLayer, tempInd++),
                new LayerOpChoiceItem("Apply Layer", OutputActionData.ActionType.ApplyActionLayer, tempInd++),
                new LayerOpChoiceItem("Remove Layer", OutputActionData.ActionType.RemoveActionLayer, tempInd++),
                new LayerOpChoiceItem("Switch Layer", OutputActionData.ActionType.SwitchActionLayer, tempInd++),
            });

            tempInd = 0;
            mapper.ActionProfile.CurrentActionSet.ActionLayers.ForEach((layer) =>
            {
                AvailableLayerChoiceItem tempChoiceItem = new AvailableLayerChoiceItem(layer, tempInd++);
                availableLayerComboItems.Add(tempChoiceItem);
            });

            tempInd = 0;
            mapper.ActionProfile.ActionSets.ForEach((actionSet) =>
            {
                AvailableSetChoiceItem tempChoiceItem = new AvailableSetChoiceItem(actionSet, tempInd++);
                availableSetsComboItems.Add(tempChoiceItem);
            });
        }

        public void AddTempOutputSlot()
        {
            int ind = slotItems.Count;
            OutputActionData tempData = new OutputActionData(OutputActionData.ActionType.Empty, 0);
            OutputSlotItem item = new OutputSlotItem(tempData, ind);
            slotItems.Add(item);

            SelectedSlotItemIndex = ind;

            mapper.ProcessMappingChangeAction(() =>
            {
                currentAction.Release(mapper, ignoreReleaseActions: true);
                func.OutputActions.Add(tempData);
            });

            //PrepareControlsForSlot(item);
        }

        public void AssignUnbound()
        {
            if (selectedSlotItemIndex <= -1) return;

            OutputSlotItem item = slotItems[selectedSlotItemIndex];
            mapper.ProcessMappingChangeAction(() =>
            {
                currentAction.Release(mapper, ignoreReleaseActions: true);

                OutputActionData tempData = item.Data;
                tempData.Reset();

                tempData.Prepare(OutputActionData.ActionType.Empty, 0);
                tempData.OutputCodeStr = OutputActionData.ActionType.Empty.ToString();
            });

            ResetComboBoxIndex(ActionComboBoxTypes.None);
            PostSlotChangeChecks();
        }

        public void AssignCameraTurn()
        {
            if (selectedSlotItemIndex <= -1) return;

            OutputSlotItem item = slotItems[selectedSlotItemIndex];
            cameraTurnAngle = DEFAULT_CAMERA_TURN_ANGLE;
            cameraTurnDurationMs = DEFAULT_CAMERA_TURN_DURATION_MS;
            LoadCameraTurnCalibFromProfile(updateSelectedSlot: false);
            mapper.ProcessMappingChangeAction(() =>
            {
                currentAction.Release(mapper, ignoreReleaseActions: true);
                OutputActionData tempData = item.Data;
                tempData.Reset();
                tempData.OutputType = OutputActionData.ActionType.CameraTurn;
                tempData.cameraTurnAngle = cameraTurnAngle;
                tempData.cameraTurnDurationMs = cameraTurnDurationMs;
                tempData.cameraTurnCounts360 = cameraTurnCounts360;
            });

            ResetComboBoxIndex(ActionComboBoxTypes.CameraTurn);
            ShowCameraTurnOptions = true;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CameraTurnAngle)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CameraTurnDurationMs)));
            PostSlotChangeChecks();
            CameraTurnActiveChanged?.Invoke(this, EventArgs.Empty);
        }

        public void AssignRecalibrateGyro()
        {
            if (selectedSlotItemIndex <= -1) return;

            OutputSlotItem item = slotItems[selectedSlotItemIndex];
            mapper.ProcessMappingChangeAction(() =>
            {
                currentAction.Release(mapper, ignoreReleaseActions: true);
                OutputActionData tempData = item.Data;
                tempData.Reset();
                tempData.OutputType = OutputActionData.ActionType.RecalibrateGyro;
            });

            ResetComboBoxIndex(ActionComboBoxTypes.None);
            PostSlotChangeChecks();
        }

        private void ButtonActionEditViewModel_CameraTurnAngleChanged(object sender, EventArgs e)
        {
            if (selectedSlotItemIndex < 0) return;
            OutputSlotItem slotItem = slotItems[selectedSlotItemIndex];
            if (slotItem.Data.OutputType != OutputActionData.ActionType.CameraTurn) return;
            mapper.ProcessMappingChangeAction(() =>
            {
                currentAction.Release(mapper, ignoreReleaseActions: true);
                slotItem.Data.cameraTurnAngle = cameraTurnAngle;
            });
        }

        private void ButtonActionEditViewModel_CameraTurnDurationMsChanged(object sender, EventArgs e)
        {
            if (selectedSlotItemIndex < 0) return;
            OutputSlotItem slotItem = slotItems[selectedSlotItemIndex];
            if (slotItem.Data.OutputType != OutputActionData.ActionType.CameraTurn) return;
            mapper.ProcessMappingChangeAction(() =>
            {
                currentAction.Release(mapper, ignoreReleaseActions: true);
                slotItem.Data.cameraTurnDurationMs = cameraTurnDurationMs;
            });
        }

        private void ButtonActionEditViewModel_CameraTurnCounts360Changed(object sender, EventArgs e)
        {
            if (selectedSlotItemIndex < 0) return;
            OutputSlotItem slotItem = slotItems[selectedSlotItemIndex];
            if (slotItem.Data.OutputType != OutputActionData.ActionType.CameraTurn) return;
            mapper.ProcessMappingChangeAction(() =>
            {
                currentAction.Release(mapper, ignoreReleaseActions: true);
                slotItem.Data.cameraTurnCounts360 = cameraTurnCounts360;
            });
            RecalculateCameraTurnRWC();
            SyncCalibFromCameraTurnToProfile();
        }

        private void RecalculateCameraTurnRWC()
        {
            CameraTurnCalculatedRWC = cameraTurnCounts360 > 0.0
                ? cameraTurnInGameSens / (360.0 / cameraTurnCounts360)
                : 0.0;
        }

        private void SyncCalibFromCameraTurnToProfile()
        {
            double counts = cameraTurnCounts360;
            double inGameSens = cameraTurnInGameSens;
            double rwc = inGameSens > 0.0 ? inGameSens * counts / 360.0 : 0.0;
            mapper.ActionProfile.CalibCounts = counts;
            mapper.ActionProfile.CalibInGameSens = inGameSens;
            mapper.ActionProfile.CalibRwc = rwc;
            mapper.ProcessMappingChangeAction(() =>
            {
                foreach (var set in mapper.ActionProfile.ActionSets)
                    foreach (var layer in set.ActionLayers)
                        foreach (var mapAction in layer.normalActionDict.Values)
                        {
                            if (mapAction is ButtonAction ba)
                                foreach (var func in ba.ActionFuncs)
                                    foreach (var data in func.OutputActions)
                                        if (data.OutputType == OutputActionData.ActionType.CameraTurn)
                                            data.cameraTurnCounts360 = counts;
                            if (mapAction is StickFlickStick sfs)
                            {
                                sfs.RealWorldCalibration = rwc;
                                sfs.InGameSens = inGameSens;
                            }
                            if (mapAction is TouchpadFlickStick tfs)
                            {
                                tfs.RealWorldCalibration = rwc;
                                tfs.InGameSens = inGameSens;
                            }
                        }
            });
        }

        private void ButtonActionEditViewModel_CameraTurnRWCChanged(object sender, EventArgs e)
        {
            if (cameraTurnInGameSens == 0.0) return;
            CameraTurnCounts360 = (cameraTurnRWC * 360.0) / cameraTurnInGameSens;
        }

        private void ButtonActionEditViewModel_CameraTurnInGameSensChanged(object sender, EventArgs e)
        {
            RecalculateCameraTurnRWC();
            SyncCalibFromCameraTurnToProfile();
        }

        public void RemoveOutputSlot(int ind)
        {
            if (slotItems.Count == 1)
            {
                return;
            }
            else if (ind >= slotItems.Count)
            {
                return;
            }

            int tempInd = ind;
            slotItems.RemoveAt(tempInd);
            SelectedSlotItemIndex = ind < slotItems.Count ? ind : slotItems.Count - 1;
            mapper.ProcessMappingChangeAction(() =>
            {
                currentAction.Release(mapper, ignoreReleaseActions: true);
                func.OutputActions.RemoveAt(tempInd);
            });

            int tempSlotInd = 0;
            foreach (OutputSlotItem item in slotItems)
            {
                item.SlotIndex = tempSlotInd++;
            }
        }
    }

    public class OutputSlotItem
    {
        private OutputActionData data;
        public OutputActionData Data
        {
            get => data;
            set => data = value;
        }

        public string DisplayName
        {
            get
            {
                return index.ToString();
            }
        }

        private int index;
        public int SlotIndex
        {
            get => index;
            set => index = value;
        }

        public OutputSlotItem(OutputActionData data, int index)
        {
            this.data = data;
            this.index = index;
        }
    }

    public class GamepadCodeItem : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private string displayName;
        public string DisplayName
        {
            get => displayName;
            set
            {
                if (displayName == value) return;
                displayName = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DisplayName)));
            }
        }

        private JoypadActionCodes code;
        public JoypadActionCodes Code => code;

        private int index;
        public int Index => index;

        public GamepadCodeItem(string displayName, JoypadActionCodes code, int index)
        {
            this.displayName = displayName;
            this.code = code;
            this.index = index;
        }
    }

    public class KeyboardCodeItem
    {
        private string displayName;
        public string DisplayName => displayName;

        private VirtualKeys code;
        public VirtualKeys Code => code;

        private int index;
        public int Index => index;

        private string codeAlias;
        public string CodeAlias => codeAlias;

        public KeyboardCodeItem(string displayName, VirtualKeys code, string codeAlias, int index)
        {
            this.displayName = displayName;
            this.code = code;
            this.codeAlias = codeAlias;
            this.index = index;
        }
    }

    public class MouseButtonCodeItem
    {
        private string displayName;
        public string DisplayName => displayName;

        private int code;
        public int Code => code;

        private int index;
        public int Index => index;

        public MouseButtonCodeItem(string displayName, int code, int index)
        {
            this.displayName = displayName;
            this.code = code;
            this.index = index;
        }
    }

    public class MouseDirItem
    {
        private string displayName;
        public string DisplayName => displayName;

        private int code;
        public int Code => code;

        private int index;
        public int Index => index;

        public MouseDirItem(string displayName, int code, int index)
        {
            this.displayName = displayName;
            this.code = code;
            this.index = index;
        }
    }

    public class LayerOpChoiceItem
    {
        private string displayName;
        public string DisplayName => displayName;

        private OutputActionData.ActionType layerOp;
        public OutputActionData.ActionType LayerOp => layerOp;

        private int index;
        public int Index => index;

        public LayerOpChoiceItem(string displayName, OutputActionData.ActionType layerOp,
            int index)
        {
            this.displayName = displayName;
            this.layerOp = layerOp;
            this.index = index;
        }
    }

    public class AvailableLayerChoiceItem
    {
        private string displayName;
        public string DisplayName => displayName;

        private ActionLayer layer;
        public ActionLayer Layer => layer;

        private int index;
        public int Index => index;

        public AvailableLayerChoiceItem(ActionLayer layer, int index)
        {
            this.layer = layer;
            this.index = index;

            if (!string.IsNullOrEmpty(layer.Name))
            {
                displayName = $"{layer.Name} ({index})";
            }
            else
            {
                displayName = $"{index}";
            }
        }
    }

    public class AvailableSetChoiceItem
    {
        private string displayName;
        public string DisplayName => displayName;

        private ActionSet set;
        public ActionSet Set => set;

        private int index;
        public int Index => index;

        public AvailableSetChoiceItem(ActionSet set, int index)
        {
            this.set = set;
            this.index = index;

            if (!string.IsNullOrEmpty(set.Name))
            {
                displayName = $"{set.Name} ({index})";
            }
            else
            {
                displayName = $"{index}";
            }
        }
    }
}
