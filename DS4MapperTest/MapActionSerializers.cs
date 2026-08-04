using DS4MapperTest.ActionUtil;
using DS4MapperTest.ButtonActions;
using DS4MapperTest.Common;
using DS4MapperTest.GyroActions;
using DS4MapperTest.MapperUtil;
using DS4MapperTest.StickActions;
using DS4MapperTest.StickModifiers;
using DS4MapperTest.TouchpadActions;
using DS4MapperTest.TriggerActions;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using static DS4MapperTest.MapperUtil.OutputActionData;
using static DS4MapperTest.TouchpadActionPadSerializer;

namespace DS4MapperTest
{
    [JsonConverter(typeof(MapActionTypeConverter))]
    public class MapActionSerializer
    {
        protected ActionLayer tempLayer;
        [JsonIgnore]
        public ActionLayer TempLayer => tempLayer;

        protected MapAction mapAction =
            new ButtonNoAction();
        [JsonIgnore]
        public MapAction MapAction { get => mapAction; set => mapAction = value; }

        [JsonProperty(Required = Required.Always, Order = -4)]
        public int Id
        {
            get => mapAction.Id;
            set => mapAction.Id = value;
        }

        [JsonProperty(Order = -3)]
        public string Name
        {
            get => mapAction.Name;
            set
            {
                mapAction.Name = value;
                NameChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        public bool ShouldSerializeName()
        {
            return !string.IsNullOrEmpty(mapAction.Name);
        }

        [JsonProperty(Order = -2)]
        public string ActionMode
        {
            get => mapAction.ActionTypeName;
            set => mapAction.ActionTypeName = value;
        }

        public event EventHandler NameChanged;

        public MapActionSerializer()
        {
        }

        public MapActionSerializer(ActionLayer tempLayer, MapAction mapAction)
        {
            this.mapAction = mapAction;
            this.tempLayer = tempLayer;
        }

        public virtual void PopulateMap()
        {
        }

        public virtual void PostPopulateMap(ActionSet tempSet,
            ActionLayer tempLayer)
        {
        }

        [OnDeserializing]
        internal void OnDeserializingMethod(StreamingContext context)
        {
            Trace.WriteLine("IN MapActionSerializer.OnDeserializingMethod");
            ActionLayerSerializer.CurrentActionIndex++;
        }

        [OnDeserialized]
        internal void OnDeserializedMethod(StreamingContext context)
        {
            Trace.WriteLine("IN MapActionSerializer.OnDeserializedMethod");
        }
    }

    public class ButtonActionSerializer : MapActionSerializer
    {
        private ButtonAction buttonAction = new ButtonAction();
        [JsonIgnore]
        internal ButtonAction ButtonAction
        {
            get => buttonAction;
            set => buttonAction = value;
        }

        private List<ActionFuncSerializer> actionFuncSerializers =
            new List<ActionFuncSerializer>();
        [JsonProperty("Functions", Required = Required.Always)]
        //[JsonConverter(typeof(ActionFuncsListConverter))]
        public List<ActionFuncSerializer> ActionFuncSerializers
        {
            get => actionFuncSerializers;
            set
            {
                actionFuncSerializers = value;
                ActionFuncSerializersChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public event EventHandler ActionFuncSerializersChanged;

        public ButtonActionSerializer() : base()
        {
            mapAction = buttonAction;

            NameChanged += ButtonActionSerializer_NameChanged;
            ActionFuncSerializersChanged += ButtonActionSerializer_ActionFuncSerializersChanged;
        }

        private void ButtonActionSerializer_ActionFuncSerializersChanged(object sender, EventArgs e)
        {
            //if (buttonAction.ParentAction != null &&
            //    actionFuncSerializers?.Count > 0)
            if (actionFuncSerializers?.Count > 0)
            {
                buttonAction.ChangedProperties.Add(ButtonAction.PropertyKeyStrings.FUNCTIONS);
            }
        }

        private void ButtonActionSerializer_NameChanged(object sender, EventArgs e)
        {
            buttonAction.ChangedProperties.Add(ButtonAction.PropertyKeyStrings.NAME);
        }

        public ButtonActionSerializer(ActionLayer tempLayer, MapAction mapAction) :
            base(tempLayer, mapAction)
        {
            if (mapAction is ButtonAction temp)
            {
                buttonAction = temp;
                this.mapAction = buttonAction;
                PopulateFuncs();
            }
        }

        public void RaiseActionFuncSerializersChanged()
        {
            ActionFuncSerializersChanged?.Invoke(this, EventArgs.Empty);
        }

        private void PopulateFuncs()
        {
            if (!buttonAction.UseParentActions)
            {
                foreach (ActionFunc tempFunc in buttonAction.ActionFuncs)
                {
                    ActionFuncSerializer tempSerializer = ActionFuncSerializerFactory.CreateSerializer(tempFunc);
                    if (tempSerializer != null)
                    {
                        actionFuncSerializers.Add(tempSerializer);
                    }
                }
            }
        }

        public override void PopulateMap()
        {
            buttonAction.ActionFuncs.Clear();
            foreach (ActionFuncSerializer serializer in actionFuncSerializers)
            {
                serializer.PopulateFunc();
                buttonAction.ActionFuncs.Add(serializer.ActionFunc);
            }
        }

        public override void PostPopulateMap(ActionSet tempSet,
            ActionLayer tempLayer)
        {
            foreach (ActionFunc func in buttonAction.ActionFuncs)
            {
                foreach(OutputActionData data in func.OutputActions)
                {
                    switch (data.OutputType)
                    {
                        case ActionType.RemoveActionLayer:
                            if (data.FromProfileChangeLayer == -1)
                            {
                                data.ChangeToLayer = tempLayer.Index;
                            }

                            break;
                        default:
                            break;
                    }
                }
            }
        }

        [OnDeserializing]
        internal void OnButtonActionDeserializingMethod(StreamingContext context)
        {
            Trace.WriteLine("IN ButtonActionSerializer.OnDeserializingMethod");
            ActionLayerSerializer.CurrentActionIndex++;
        }

        [OnDeserialized]
        internal void OnButtionActionDeserializedMethod(StreamingContext context)
        {
            Trace.WriteLine("IN ButtonActionSerializer.OnDeserializedMethod");
        }
    }

    public class DpadActionSerializer : MapActionSerializer
    {
        public class DPadPadDirBinding
        {
            private string actionDirName;
            [JsonProperty("Name", Required = Required.Default)]
            public string ActionDirName
            {
                get => actionDirName;
                set => actionDirName = value;
            }
            public bool ShouldSerializeActionDirName()
            {
                return !string.IsNullOrEmpty(actionDirName);
            }

            private List<ActionFuncSerializer> actionFuncSerializers =
                new List<ActionFuncSerializer>();
            [JsonProperty("Functions", Required = Required.Always)]
            public List<ActionFuncSerializer> ActionFuncSerializers
            {
                get => actionFuncSerializers;
                set
                {
                    actionFuncSerializers = value;
                    ActionFuncSerializersChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler ActionFuncSerializersChanged;
        }

        public class DpadPadActionSettings
        {
            private DPadActions.DPadAction padAction;

            [JsonConverter(typeof(StringEnumConverter))]
            public DPadActions.DPadAction.DPadMode PadMode
            {
                get => padAction.CurrentMode;
                set
                {
                    padAction.CurrentMode = value;
                    PadModeChanged?.Invoke(this, EventArgs.Empty);
                }
            }

            public double DelayTime
            {
                get => padAction.DelayTime;
                set
                {
                    padAction.DelayTime = Math.Clamp(value, 0.0, 3600.0);
                    DelayTimeChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler DelayTimeChanged;
            public bool ShouldSerializeDelayTime()
            {
                return padAction.DelayTime != 0.0;
            }

            public DpadPadActionSettings(DPadActions.DPadAction padAction)
            {
                this.padAction = padAction;
            }

            public event EventHandler PadModeChanged;
        }

        private DPadActions.DPadAction dPadAction = new DPadActions.DPadAction();

        private Dictionary<DPadActions.DpadDirections, DPadPadDirBinding> dictPadBindings =
            new Dictionary<DPadActions.DpadDirections, DPadPadDirBinding>();

        [JsonProperty("Bindings", Required = Required.Always)]
        public Dictionary<DPadActions.DpadDirections, DPadPadDirBinding> DictPadBindings
        {
            get => dictPadBindings;
            set => dictPadBindings = value;
        }

        private DpadPadActionSettings settings;
        public DpadPadActionSettings Settings
        {
            get => settings;
            set => settings = value;
        }

        public DpadActionSerializer() : base()
        {
            mapAction = dPadAction;
            settings = new DpadPadActionSettings(dPadAction);

            settings.PadModeChanged += Settings_PadModeChanged;
            settings.DelayTimeChanged += Settings_DelayTimeChanged;
        }

        private void Settings_DelayTimeChanged(object sender, EventArgs e)
        {
            dPadAction.ChangedProperties.Add(DPadActions.DPadAction.PropertyKeyStrings.DELAY_TIME);
        }

        private void Settings_PadModeChanged(object sender, EventArgs e)
        {
            //if (mapAction.ParentAction != null)
            {
                dPadAction.ChangedProperties.Add(DPadActions.DPadAction.PropertyKeyStrings.PAD_MODE);
                //mapAction.ChangedProperties.Add("PadMode");
            }
        }

        public DpadActionSerializer(ActionLayer tempLayer, MapAction action) :
            base(tempLayer, action)
        {
            if (action is DPadActions.DPadAction temp)
            {
                dPadAction = temp;
                mapAction = dPadAction;
                settings = new DpadPadActionSettings(dPadAction);
                PopulateFuncs();
            }
        }

        // Post-deserialize
        public override void PopulateMap()
        {
            foreach (ButtonAction dirButton in dPadAction.EventCodes4)
            {
                dirButton?.ActionFuncs.Clear();
            }

            //foreach (KeyValuePair<DPadActions.DpadDirections, List<ActionFuncSerializer>> tempKeyPair in dictPadBindings)
            foreach (KeyValuePair<DPadActions.DpadDirections, DPadPadDirBinding> tempKeyPair in dictPadBindings)
            {
                DPadActions.DpadDirections dir = tempKeyPair.Key;
                List<ActionFuncSerializer> tempSerializers = tempKeyPair.Value.ActionFuncSerializers;
                //List<ActionFuncSerializer> tempSerializers = tempKeyPair.Value;

                //ButtonAction tempDirButton = dPadAction.EventCodes4[(int)dir];
                //if (tempDirButton != null)
                {
                    //tempDirButton.Name = tempKeyPair.Value.ActionDirName;
                    ButtonAction dirButton = new ButtonAction();
                    dirButton.Name = tempKeyPair.Value.ActionDirName;
                    foreach (ActionFuncSerializer serializer in tempSerializers)
                    {
                        //ButtonAction dirButton = dPadAction.EventCodes4[(int)dir];
                        serializer.PopulateFunc();
                        dirButton.ActionFuncs.Add(serializer.ActionFunc);
                        //dPadAction.EventCodes4[(int)dir] = dirButton;
                    }

                    dPadAction.EventCodes4[(int)dir] = dirButton;
                    FlagBtnChangedDirection(dir, dPadAction);
                }
            }
        }

        // Pre-serialize
        private void PopulateFuncs()
        {
            List<ActionFuncSerializer> tempFuncs = new List<ActionFuncSerializer>();
            DPadActions.DpadDirections[] tempDirs = null;

            if (dPadAction.CurrentMode == DPadActions.DPadAction.DPadMode.Standard ||
                dPadAction.CurrentMode == DPadActions.DPadAction.DPadMode.FourWayCardinal)
            {
                tempDirs = new DPadActions.DpadDirections[4]
                {
                    DPadActions.DpadDirections.Up, DPadActions.DpadDirections.Down,
                    DPadActions.DpadDirections.Left, DPadActions.DpadDirections.Right
                };
            }
            else if (dPadAction.CurrentMode == DPadActions.DPadAction.DPadMode.EightWay)
            {
                tempDirs = new DPadActions.DpadDirections[8]
                {
                    DPadActions.DpadDirections.Up, DPadActions.DpadDirections.Down,
                    DPadActions.DpadDirections.Left, DPadActions.DpadDirections.Right,
                    DPadActions.DpadDirections.UpLeft, DPadActions.DpadDirections.UpRight,
                    DPadActions.DpadDirections.DownLeft, DPadActions.DpadDirections.DownRight
                };
            }
            else if (dPadAction.CurrentMode == DPadActions.DPadAction.DPadMode.FourWayDiagonal)
            {
                tempDirs = new DPadActions.DpadDirections[4]
                {
                    DPadActions.DpadDirections.UpLeft, DPadActions.DpadDirections.UpRight,
                    DPadActions.DpadDirections.DownLeft, DPadActions.DpadDirections.DownRight
                };
            }

            for (int i = 0; i < tempDirs.Length; i++)
            {
                DPadActions.DpadDirections tempDir = tempDirs[i];
                ButtonAction dirButton = dPadAction.EventCodes4[(int)tempDir];

                tempFuncs.Clear();
                foreach (ActionFunc tempFunc in dirButton.ActionFuncs)
                {
                    ActionFuncSerializer tempSerializer =
                        ActionFuncSerializerFactory.CreateSerializer(tempFunc);
                    if (tempSerializer != null)
                    {
                        tempFuncs.Add(tempSerializer);
                    }
                }

                //dictPadBindings.Add(tempDir, tempFuncs);
                dictPadBindings.Add(tempDir, new DPadPadDirBinding()
                {
                    ActionDirName = dirButton.Name,
                    ActionFuncSerializers = new List<ActionFuncSerializer>(tempFuncs),
                });
            }
        }

        public void FlagBtnChangedDirection(DPadActions.DpadDirections dir,
            DPadActions.DPadAction action)
        {
            switch (dir)
            {
                case DPadActions.DpadDirections.Up:
                    action.ChangedProperties.Add(DPadActions.DPadAction.PropertyKeyStrings.PAD_DIR_UP);
                    break;
                case DPadActions.DpadDirections.Down:
                    action.ChangedProperties.Add(DPadActions.DPadAction.PropertyKeyStrings.PAD_DIR_DOWN);
                    break;
                case DPadActions.DpadDirections.Left:
                    action.ChangedProperties.Add(DPadActions.DPadAction.PropertyKeyStrings.PAD_DIR_LEFT);
                    break;
                case DPadActions.DpadDirections.Right:
                    action.ChangedProperties.Add(DPadActions.DPadAction.PropertyKeyStrings.PAD_DIR_RIGHT);
                    break;
                case DPadActions.DpadDirections.UpLeft:
                    action.ChangedProperties.Add(DPadActions.DPadAction.PropertyKeyStrings.PAD_DIR_UPLEFT);
                    break;
                case DPadActions.DpadDirections.UpRight:
                    action.ChangedProperties.Add(DPadActions.DPadAction.PropertyKeyStrings.PAD_DIR_UPRIGHT);
                    break;
                case DPadActions.DpadDirections.DownLeft:
                    action.ChangedProperties.Add(DPadActions.DPadAction.PropertyKeyStrings.PAD_DIR_DOWNLEFT);
                    break;
                case DPadActions.DpadDirections.DownRight:
                    action.ChangedProperties.Add(DPadActions.DPadAction.PropertyKeyStrings.PAD_DIR_DOWNRIGHT);
                    break;
                default:
                    break;
            }
        }
    }

    public class ButtonNoActionSerializer : MapActionSerializer
    {
        private ButtonNoAction buttonNoAction = new ButtonNoAction();

        public ButtonNoActionSerializer() : base()
        {
            mapAction = buttonNoAction;
        }

        public ButtonNoActionSerializer(ActionLayer tempLayer, MapAction action) :
            base(tempLayer, action)
        {
            if (action is ButtonNoAction temp)
            {
                buttonNoAction = temp;
                mapAction = buttonNoAction;
            }
        }
    }

    public class DpadNoActionSerializer : MapActionSerializer
    {
        private DPadActions.DPadNoAction dpadNoAction = new DPadActions.DPadNoAction();

        public DpadNoActionSerializer() : base()
        {
            mapAction = dpadNoAction;
        }

        public DpadNoActionSerializer(ActionLayer tempLayer, MapAction action) :
            base(tempLayer, action)
        {
            if (action is DPadActions.DPadNoAction temp)
            {
                dpadNoAction = temp;
                mapAction = dpadNoAction;
            }
        }
    }

    public class DpadTranslateSerializer : MapActionSerializer
    {
        public class DpadTranslateSettings
        {
            private DPadActions.DPadTranslate dpadTransAct;

            public string OutputDPad
            {
                get => DPadCodeHelper.Convert(dpadTransAct.OutputAction.DpadCode);
                set
                {
                    if (Enum.TryParse(value, out DPadActionCodes code))
                    {
                        dpadTransAct.OutputAction.DpadCode = code;
                    }
                }
            }

            public DpadTranslateSettings(DPadActions.DPadTranslate action)
            {
                this.dpadTransAct = action;
            }
        }

        private DPadActions.DPadTranslate dpadTransAct =
            new DPadActions.DPadTranslate();

        private DpadTranslateSettings settings;
        public DpadTranslateSettings Settings
        {
            get => settings;
            set => settings = value;
        }

        public DpadTranslateSerializer() : base()
        {
            mapAction = dpadTransAct;
            settings = new DpadTranslateSettings(dpadTransAct);
        }

        // Serialize ctor
        public DpadTranslateSerializer(ActionLayer tempLayer, MapAction action) :
            base(tempLayer, action)
        {
            if (action is DPadActions.DPadTranslate temp)
            {
                dpadTransAct = temp;
                mapAction = dpadTransAct;
                settings = new DpadTranslateSettings(dpadTransAct);
            }
        }
    }

    public class TriggerTranslateActionSerializer : MapActionSerializer
    {
        public class TriggerTranslateSettings
        {
            private TriggerTranslate triggerAction;

            [JsonConverter(typeof(StringEnumConverter))]
            public JoypadActionCodes OutputTrigger
            {
                get => triggerAction.OutputData.JoypadCode;
                set
                {
                    triggerAction.OutputData.JoypadCode = value;
                    OutputTriggerChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler OutputTriggerChanged;

            public double DeadZone
            {
                get => triggerAction.DeadMod.DeadZone;
                set
                {
                    triggerAction.DeadMod.DeadZone = Math.Clamp(value, 0.0, 1.0);
                    DeadZoneChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler DeadZoneChanged;

            public double MaxZone
            {
                get => triggerAction.DeadMod.MaxZone;
                set
                {
                    triggerAction.DeadMod.MaxZone = Math.Clamp(value, 0.0, 1.0);
                    MaxZoneChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler MaxZoneChanged;

            public double AntiDeadZone
            {
                get => triggerAction.DeadMod.AntiDeadZone;
                set
                {
                    triggerAction.DeadMod.AntiDeadZone = Math.Clamp(value, 0.0, 1.0);
                    AntiDeadZoneChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler AntiDeadZoneChanged;

            public TriggerTranslateSettings(TriggerTranslate action)
            {
                triggerAction = action;
            }
        }

        private TriggerTranslate triggerAction
            = new TriggerTranslate();

        private TriggerTranslateSettings settings;
        public TriggerTranslateSettings Settings
        {
            get => settings;
            set => settings = value;
        }

        // Deserialize
        public TriggerTranslateActionSerializer() : base()
        {
            mapAction = triggerAction;
            settings = new TriggerTranslateSettings(triggerAction);

            NameChanged += TriggerTranslateActionSerializer_NameChanged;
            settings.OutputTriggerChanged += Settings_OutputTriggerChanged;
            settings.DeadZoneChanged += Settings_DeadZoneChanged;
            settings.MaxZoneChanged += Settings_MaxZoneChanged;
            settings.AntiDeadZoneChanged += Settings_AntiDeadZoneChanged;
        }

        private void TriggerTranslateActionSerializer_NameChanged(object sender, EventArgs e)
        {
            triggerAction.ChangedProperties.Add(TriggerTranslate.PropertyKeyStrings.NAME);
        }

        private void Settings_AntiDeadZoneChanged(object sender, EventArgs e)
        {
            triggerAction.ChangedProperties.Add(TriggerTranslate.PropertyKeyStrings.ANTIDEAD_ZONE);
        }

        private void Settings_MaxZoneChanged(object sender, EventArgs e)
        {
            triggerAction.ChangedProperties.Add(TriggerTranslate.PropertyKeyStrings.MAX_ZONE);
        }

        private void Settings_DeadZoneChanged(object sender, EventArgs e)
        {
            triggerAction.ChangedProperties.Add(TriggerTranslate.PropertyKeyStrings.DEAD_ZONE);
        }

        private void Settings_OutputTriggerChanged(object sender, EventArgs e)
        {
            triggerAction.ChangedProperties.Add(TriggerTranslate.PropertyKeyStrings.OUTPUT_TRIGGER);
        }

        // Serialize
        public TriggerTranslateActionSerializer(ActionLayer tempLayer, MapAction mapAction) :
            base(tempLayer, mapAction)
        {
            if (mapAction is TriggerTranslate temp)
            {
                triggerAction = temp;
                this.mapAction = triggerAction;
                settings = new TriggerTranslateSettings(triggerAction);
            }
        }
    }

    public class TriggerMouseActionSerializer : MapActionSerializer
    {
        public class DeltaAccelSettingsSerializer
        {
            private TriggerMouse triggerMouseAction;

            public bool Enabled
            {
                get => triggerMouseAction.MouseDeltaSettings.Enabled;
                set => triggerMouseAction.MouseDeltaSettings.Enabled = value;
            }

            public double Multiplier
            {
                get => triggerMouseAction.MouseDeltaSettings.Multiplier;
                set => triggerMouseAction.MouseDeltaSettings.Multiplier = value;
            }

            public double MaxTravel
            {
                get => triggerMouseAction.MouseDeltaSettings.MaxTravel;
                set => triggerMouseAction.MouseDeltaSettings.MaxTravel = value;
            }

            public double MinTravel
            {
                get => triggerMouseAction.MouseDeltaSettings.MinTravel;
                set => triggerMouseAction.MouseDeltaSettings.MinTravel = value;
            }

            public double EasingDuration
            {
                get => triggerMouseAction.MouseDeltaSettings.EasingDuration;
                set => triggerMouseAction.MouseDeltaSettings.EasingDuration = value;
            }

            public double MinFactor
            {
                get => triggerMouseAction.MouseDeltaSettings.MinFactor;
                set => triggerMouseAction.MouseDeltaSettings.MinFactor = value;
            }

            public DeltaAccelSettingsSerializer(TriggerMouse mouseAction)
            {
                this.triggerMouseAction = mouseAction;
            }
        }

        public class TriggerMouseSettings
        {
            private TriggerMouse triggerMouseAction;
            private DeltaAccelSettingsSerializer mouseDeltaSettingsSerializer;

            public double DeadZone
            {
                get => triggerMouseAction.DeadMod.DeadZone;
                set
                {
                    triggerMouseAction.DeadMod.DeadZone = Math.Clamp(value, 0.0, 1.0);
                    DeadZoneChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler DeadZoneChanged;

            public double MaxZone
            {
                get => triggerMouseAction.DeadMod.MaxZone;
                set
                {
                    triggerMouseAction.DeadMod.MaxZone = Math.Clamp(value, 0.0, 1.0);
                    MaxZoneChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler MaxZoneChanged;

            public int MouseSpeed
            {
                get => triggerMouseAction.MouseSpeed;
                set
                {
                    triggerMouseAction.MouseSpeed = value;
                    MouseSpeedChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler MouseSpeedChanged;

            [JsonConverter(typeof(SafeStringEnumConverter),
                StickOutCurve.Curve.Linear)]
            public StickOutCurve.Curve OutputCurve
            {
                get => triggerMouseAction.OutputCurve;
                set
                {
                    triggerMouseAction.OutputCurve = value;
                    OutputCurveChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler OutputCurveChanged;

            public double DirectionDegrees
            {
                get => triggerMouseAction.DirectionDegrees;
                set
                {
                    triggerMouseAction.DirectionDegrees = value;
                    DirectionDegreesChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler DirectionDegreesChanged;

            public DeltaAccelSettingsSerializer DeltaSettings
            {
                get => mouseDeltaSettingsSerializer;
                set
                {
                    mouseDeltaSettingsSerializer = value;
                    DeltaSettingsChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler DeltaSettingsChanged;

            public TriggerMouseSettings(TriggerMouse mouseAction)
            {
                triggerMouseAction = mouseAction;
                mouseDeltaSettingsSerializer = new DeltaAccelSettingsSerializer(mouseAction);
            }
        }

        private TriggerMouse triggerMouseAction =
            new TriggerMouse();

        private TriggerMouseSettings settings;
        public TriggerMouseSettings Settings { get => settings; set => settings = value; }

        // Deserialize
        public TriggerMouseActionSerializer() : base()
        {
            mapAction = triggerMouseAction;
            settings = new TriggerMouseSettings(triggerMouseAction);

            NameChanged += TriggerMouseActionSerializer_NameChanged;
            settings.DeadZoneChanged += Settings_DeadZoneChanged;
            settings.MaxZoneChanged += Settings_MaxZoneChanged;
            settings.MouseSpeedChanged += Settings_MouseSpeedChanged;
            settings.OutputCurveChanged += Settings_OutputCurveChanged;
            settings.DirectionDegreesChanged += Settings_DirectionDegreesChanged;
            settings.DeltaSettingsChanged += Settings_DeltaSettingsChanged;
        }

        private void TriggerMouseActionSerializer_NameChanged(object sender, EventArgs e)
        {
            triggerMouseAction.ChangedProperties.Add(TriggerMouse.PropertyKeyStrings.NAME);
        }

        private void Settings_DeadZoneChanged(object sender, EventArgs e)
        {
            triggerMouseAction.ChangedProperties.Add(TriggerMouse.PropertyKeyStrings.DEAD_ZONE);
        }

        private void Settings_MaxZoneChanged(object sender, EventArgs e)
        {
            triggerMouseAction.ChangedProperties.Add(TriggerMouse.PropertyKeyStrings.MAX_ZONE);
        }

        private void Settings_MouseSpeedChanged(object sender, EventArgs e)
        {
            triggerMouseAction.ChangedProperties.Add(TriggerMouse.PropertyKeyStrings.MOUSE_SPEED);
        }

        private void Settings_OutputCurveChanged(object sender, EventArgs e)
        {
            triggerMouseAction.ChangedProperties.Add(TriggerMouse.PropertyKeyStrings.OUTPUT_CURVE);
        }

        private void Settings_DirectionDegreesChanged(object sender, EventArgs e)
        {
            triggerMouseAction.ChangedProperties.Add(TriggerMouse.PropertyKeyStrings.DIRECTION_DEGREES);
        }

        private void Settings_DeltaSettingsChanged(object sender, EventArgs e)
        {
            triggerMouseAction.ChangedProperties.Add(TriggerMouse.PropertyKeyStrings.DELTA_SETTINGS);
        }

        // Serialize
        public TriggerMouseActionSerializer(ActionLayer tempLayer, MapAction mapAction) :
            base(tempLayer, mapAction)
        {
            if (mapAction is TriggerMouse temp)
            {
                triggerMouseAction = temp;
                this.mapAction = triggerMouseAction;
                settings = new TriggerMouseSettings(triggerMouseAction);
            }
        }
    }

    public class TriggerDualStageActionSerializer : MapActionSerializer
    {
        public class TriggerDualStageSettings
        {
            private TriggerDualStageAction triggerDualAction;

            public double DeadZone
            {
                get => triggerDualAction.DeadMod.DeadZone;
                set
                {
                    triggerDualAction.DeadMod.DeadZone = Math.Clamp(value, 0.0, 1.0);
                    DeadZoneChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler DeadZoneChanged;

            public double MaxZone
            {
                get => triggerDualAction.DeadMod.MaxZone;
                set
                {
                    triggerDualAction.DeadMod.MaxZone = Math.Clamp(value, 0.0, 1.0);
                    MaxZoneChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler MaxZoneChanged;

            public double AntiDeadZone
            {
                get => triggerDualAction.DeadMod.AntiDeadZone;
                set
                {
                    triggerDualAction.DeadMod.AntiDeadZone = Math.Clamp(value, 0.0, 1.0);
                    AntiDeadZoneChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler AntiDeadZoneChanged;

            [JsonConverter(typeof(StringEnumConverter))]
            public TriggerDualStageAction.DualStageMode DualStageMode
            {
                get => triggerDualAction.TriggerStateMode;
                set
                {
                    triggerDualAction.TriggerStateMode = value;
                    DualStageModeChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler DualStageModeChanged;

            public int HipFireDelay
            {
                get => triggerDualAction.HipFireMS;
                set
                {
                    triggerDualAction.HipFireMS = value;
                    HipFireDelayChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler HipFireDelayChanged;

            public bool ForceHipFireDelay
            {
                get => triggerDualAction.ForceHipTime;
                set
                {
                    triggerDualAction.ForceHipTime = value;
                    ForceHipFireDelayChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler ForceHipFireDelayChanged;

            [JsonConverter(typeof(StringEnumConverter))]
            public MapAction.HapticsIntensity SoftPullHapticsIntensity
            {
                get => triggerDualAction.SoftPullActionHapticsIntensity;
                set
                {
                    triggerDualAction.SoftPullActionHapticsIntensity = value;
                    SoftPullHapticsIntensityChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler SoftPullHapticsIntensityChanged;
            public bool ShouldSerializeSoftPullHapticsIntensity()
            {
                return triggerDualAction.ChangedProperties.Contains(TriggerDualStageAction.PropertyKeyStrings.SOFT_PULL_HAPTICS_INTENSITY);
            }

            [JsonConverter(typeof(StringEnumConverter))]
            public MapAction.HapticsIntensity FullPullHapticsIntensity
            {
                get => triggerDualAction.FullPullActionHapticsIntensity;
                set
                {
                    triggerDualAction.FullPullActionHapticsIntensity = value;
                    FullPullHapticsIntensityChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler FullPullHapticsIntensityChanged;
            public bool ShouldSerializeFullPullHapticsIntensity()
            {
                return triggerDualAction.ChangedProperties.Contains(TriggerDualStageAction.PropertyKeyStrings.FULL_PULL_HAPTICS_INTENSITY);
            }

            public TriggerDualStageSettings(TriggerDualStageAction action)
            {
                triggerDualAction = action;
            }
        }

        public class StageButtonBinding
        {
            private string actionDirName;
            [JsonProperty("Name", Required = Required.Default)]
            public string ActionDirName
            {
                get => actionDirName;
                set => actionDirName = value;
            }
            public bool ShouldSerializeActionDirName()
            {
                return !string.IsNullOrEmpty(actionDirName);
            }

            private List<ActionFuncSerializer> actionFuncSerializers =
                new List<ActionFuncSerializer>();
            [JsonProperty("Functions", Required = Required.Always)]
            public List<ActionFuncSerializer> ActionFuncSerializers
            {
                get => actionFuncSerializers;
                set
                {
                    actionFuncSerializers = value;
                    ActionFuncSerializersChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler ActionFuncSerializersChanged;
        }

        private TriggerDualStageAction triggerDualAction = new TriggerDualStageAction();

        private StageButtonBinding softPullStageButton = new StageButtonBinding();
        public StageButtonBinding SoftPull
        {
            get => softPullStageButton;
            set
            {
                softPullStageButton = value;
                SoftPullChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        public event EventHandler SoftPullChanged;
        public bool ShouldSerializeSoftPull()
        {
            return softPullStageButton.ActionFuncSerializers != null &&
                softPullStageButton.ActionFuncSerializers.Count > 0;
        }

        private StageButtonBinding fullPullStageButton = new StageButtonBinding();
        public StageButtonBinding FullPull
        {
            get => fullPullStageButton;
            set
            {
                fullPullStageButton = value;
                FullPullChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        public event EventHandler FullPullChanged;
        public bool ShouldSerializeFullPull()
        {
            return fullPullStageButton.ActionFuncSerializers != null &&
                fullPullStageButton.ActionFuncSerializers.Count > 0;
        }

        private TriggerDualStageSettings settings;
        public TriggerDualStageSettings Settings
        {
            get => settings;
            set => settings = value;
        }

        // Deserialize
        public TriggerDualStageActionSerializer() : base()
        {
            mapAction = triggerDualAction;
            settings = new TriggerDualStageSettings(triggerDualAction);

            NameChanged += TriggerDualStageActionSerializer_NameChanged;
            SoftPullChanged += TriggerDualStageActionSerializer_SoftPullChanged;
            FullPullChanged += TriggerDualStageActionSerializer_FullPullChanged;
            settings.DeadZoneChanged += Settings_DeadZoneChanged;
            settings.MaxZoneChanged += Settings_MaxZoneChanged;
            settings.AntiDeadZoneChanged += Settings_AntiDeadZoneChanged;
            settings.DualStageModeChanged += Settings_DualStageModeChanged;
            settings.HipFireDelayChanged += Settings_HipFireDelayChanged;
            settings.ForceHipFireDelayChanged += Settings_ForceHipFireDelayChanged;
            settings.SoftPullHapticsIntensityChanged += Settings_SoftPullHapticsIntensityChanged;
            settings.FullPullHapticsIntensityChanged += Settings_FullPullHapticsIntensityChanged;
        }

        private void Settings_SoftPullHapticsIntensityChanged(object sender, EventArgs e)
        {
            triggerDualAction.ChangedProperties.Add(TriggerDualStageAction.PropertyKeyStrings.SOFT_PULL_HAPTICS_INTENSITY);
        }

        private void Settings_FullPullHapticsIntensityChanged(object sender, EventArgs e)
        {
            triggerDualAction.ChangedProperties.Add(TriggerDualStageAction.PropertyKeyStrings.FULL_PULL_HAPTICS_INTENSITY);
        }

        private void Settings_ForceHipFireDelayChanged(object sender, EventArgs e)
        {
            triggerDualAction.ChangedProperties.Add(TriggerDualStageAction.PropertyKeyStrings.FORCE_HIP_FIRE_TIME);
        }

        private void Settings_AntiDeadZoneChanged(object sender, EventArgs e)
        {
            triggerDualAction.ChangedProperties.Add(TriggerDualStageAction.PropertyKeyStrings.ANTIDEAD_ZONE);
        }

        private void Settings_HipFireDelayChanged(object sender, EventArgs e)
        {
            triggerDualAction.ChangedProperties.Add(TriggerDualStageAction.PropertyKeyStrings.HIPFIRE_DELAY);
        }

        private void Settings_DualStageModeChanged(object sender, EventArgs e)
        {
            triggerDualAction.ChangedProperties.Add(TriggerDualStageAction.PropertyKeyStrings.DUALSTAGE_MODE);
        }

        private void TriggerDualStageActionSerializer_FullPullChanged(object sender, EventArgs e)
        {
            triggerDualAction.ChangedProperties.Add(TriggerDualStageAction.PropertyKeyStrings.FULLPULL_BUTTON);
        }

        private void TriggerDualStageActionSerializer_SoftPullChanged(object sender, EventArgs e)
        {
            triggerDualAction.ChangedProperties.Add(TriggerDualStageAction.PropertyKeyStrings.SOFTPULL_BUTTON);
        }

        private void Settings_MaxZoneChanged(object sender, EventArgs e)
        {
            triggerDualAction.ChangedProperties.Add(TriggerDualStageAction.PropertyKeyStrings.MAX_ZONE);
        }

        private void Settings_DeadZoneChanged(object sender, EventArgs e)
        {
            triggerDualAction.ChangedProperties.Add(TriggerDualStageAction.PropertyKeyStrings.DEAD_ZONE);
        }

        private void TriggerDualStageActionSerializer_NameChanged(object sender, EventArgs e)
        {
            triggerDualAction.ChangedProperties.Add(TriggerDualStageAction.PropertyKeyStrings.NAME);
        }

        // Pre-serialize ctor
        public TriggerDualStageActionSerializer(ActionLayer tempLayer, MapAction mapAction) :
            base(tempLayer, mapAction)
        {
            if (mapAction is TriggerDualStageAction temp)
            {
                triggerDualAction = temp;
                //mapAction = triggerDualAction;
                settings = new TriggerDualStageSettings(triggerDualAction);

                softPullStageButton.ActionDirName = triggerDualAction.SoftPullActButton.Name;
                fullPullStageButton.ActionDirName = triggerDualAction.FullPullActButton.Name;

                PopulateFuncs();
            }
        }

        // Deserialize
        public override void PopulateMap()
        {
            triggerDualAction.SoftPullActButton.ActionFuncs.Clear();
            triggerDualAction.FullPullActButton.ActionFuncs.Clear();

            AxisDirButton tempButton = triggerDualAction.SoftPullActButton;
            foreach(ActionFuncSerializer serializer in softPullStageButton.ActionFuncSerializers)
            {
                serializer.PopulateFunc();
                tempButton.ActionFuncs.Add(serializer.ActionFunc);
            }
            tempButton.Name = softPullStageButton.ActionDirName;

            tempButton = triggerDualAction.FullPullActButton;
            foreach (ActionFuncSerializer serializer in fullPullStageButton.ActionFuncSerializers)
            {
                serializer.PopulateFunc();
                tempButton.ActionFuncs.Add(serializer.ActionFunc);
            }
            tempButton.Name = fullPullStageButton.ActionDirName;
        }

        public void PopulateFuncs()
        {
            List<ActionFuncSerializer> tempFuncs = new List<ActionFuncSerializer>();
            foreach(ActionFunc tempFunc in triggerDualAction.SoftPullActButton.ActionFuncs)
            {
                tempFuncs.Add(ActionFuncSerializerFactory.CreateSerializer(tempFunc));
            }
            softPullStageButton.ActionFuncSerializers.AddRange(tempFuncs);

            tempFuncs.Clear();

            foreach (ActionFunc tempFunc in triggerDualAction.FullPullActButton.ActionFuncs)
            {
                tempFuncs.Add(ActionFuncSerializerFactory.CreateSerializer(tempFunc));
            }

            fullPullStageButton.ActionFuncSerializers.AddRange(tempFuncs);
        }
    }

    public class TriggerButtonActionSerializer : MapActionSerializer
    {
        public class TriggerButtonActionSettings
        {
            private TriggerButtonAction _action;

            public double DeadZone
            {
                get => _action.DeadZone.DeadZone;
                set
                {
                    _action.DeadZone.DeadZone = Math.Clamp(value, 0.0, 1.0);
                    DeadZoneChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler DeadZoneChanged;

            public TriggerButtonActionSettings(TriggerButtonAction action)
            {
                _action = action;
            }
        }

        private TriggerButtonAction trigBtnAction = new TriggerButtonAction();

        private List<ActionFuncSerializer> actionFuncSerializers =
                new List<ActionFuncSerializer>();
        [JsonProperty("Functions", Required = Required.Always)]
        public List<ActionFuncSerializer> ActionFuncSerializers
        {
            get => actionFuncSerializers;
            set
            {
                actionFuncSerializers = value;
                ActionFuncSerializersChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        public event EventHandler ActionFuncSerializersChanged;

        private TriggerButtonActionSettings settings;
        public TriggerButtonActionSettings Settings
        {
            get => settings;
            set => settings = value;
        }

        // Deserialize
        public TriggerButtonActionSerializer() : base()
        {
            mapAction = trigBtnAction;
            this.settings = new TriggerButtonActionSettings(trigBtnAction);

            NameChanged += TriggerButtonActionSerializer_NameChanged;
            ActionFuncSerializersChanged += TriggerButtonActionSerializer_ActionFuncSerializersChanged;
            settings.DeadZoneChanged += Settings_DeadZoneChanged;
        }

        private void TriggerButtonActionSerializer_ActionFuncSerializersChanged(object sender, EventArgs e)
        {
            trigBtnAction.ChangedProperties.Add(TriggerButtonAction.PropertyKeyStrings.OUTPUT_BINDING);
        }

        private void Settings_DeadZoneChanged(object sender, EventArgs e)
        {
            trigBtnAction.ChangedProperties.Add(TriggerButtonAction.PropertyKeyStrings.DEAD_ZONE);
        }

        private void TriggerButtonActionSerializer_NameChanged(object sender, EventArgs e)
        {
            trigBtnAction.ChangedProperties.Add(TriggerButtonAction.PropertyKeyStrings.NAME);
        }

        // Serialize
        public TriggerButtonActionSerializer(ActionLayer tempLayer, MapAction mapAction) :
            base(tempLayer, mapAction)
        {
            if (mapAction is TriggerButtonAction temp)
            {
                trigBtnAction = temp;
                this.mapAction = trigBtnAction;
                this.settings = new TriggerButtonActionSettings(trigBtnAction);
                PopulateFuncs();
            }
        }

        public override void PopulateMap()
        {
            trigBtnAction.EventButton.ActionFuncs.Clear();
            foreach (ActionFuncSerializer serializer in actionFuncSerializers)
            {
                serializer.PopulateFunc();
                trigBtnAction.EventButton.ActionFuncs.Add(serializer.ActionFunc);
            }
        }

        private void PopulateFuncs()
        {
            //if (!buttonAction.UseParentActions)
            {
                foreach (ActionFunc tempFunc in trigBtnAction.EventButton.ActionFuncs)
                {
                    ActionFuncSerializer tempSerializer =
                        ActionFuncSerializerFactory.CreateSerializer(tempFunc);
                    if (tempSerializer != null)
                    {
                        actionFuncSerializers.Add(tempSerializer);
                    }
                }
            }
        }
    }

    public class TouchpadActionPadSerializer : MapActionSerializer
    {
        public class TouchPadDirBinding
        {
            //private StickPadAction.DpadDirections direction;
            //[JsonIgnore]
            //public StickPadAction.DpadDirections Direction
            //{
            //    get => direction;
            //}

            private string actionDirName;
            [JsonProperty("Name", Required = Required.Default)]
            public string ActionDirName
            {
                get => actionDirName;
                set => actionDirName = value;
            }
            public bool ShouldSerializeActionDirName()
            {
                return !string.IsNullOrEmpty(actionDirName);
            }

            private List<ActionFuncSerializer> actionFuncSerializers =
                new List<ActionFuncSerializer>();
            [JsonProperty("Functions")]
            public List<ActionFuncSerializer> ActionFuncSerializers
            {
                get => actionFuncSerializers;
                set
                {
                    actionFuncSerializers = value;
                    ActionFuncSerializersChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler ActionFuncSerializersChanged;
            // TODO: Decide whether Functions property should be always required or not.
            // Conflicting statements here
            public bool ShouldSerializeActionFuncSerializers()
            {
                return actionFuncSerializers != null &&
                    actionFuncSerializers.Count > 0;
            }
        }

        public class TouchpadActionPadSettings
        {
            private TouchpadActionPad touchActionPadAction;

            public double DeadZone
            {
                get => touchActionPadAction.DeadMod.DeadZone;
                set
                {
                    touchActionPadAction.DeadMod.DeadZone = value;
                    DeadZoneChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler DeadZoneChanged;
            public bool ShouldSerializeDeadZone()
            {
                return touchActionPadAction.ChangedProperties.Contains(TouchpadActionPad.PropertyKeyStrings.DEAD_ZONE);
            }

            public double MaxZone
            {
                get => touchActionPadAction.DeadMod.MaxZone;
                set
                {
                    touchActionPadAction.DeadMod.MaxZone = Math.Clamp(value, 0.0, 1.0);
                    MaxZoneChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler MaxZoneChanged;
            public bool ShouldSerializeMaxZone()
            {
                return touchActionPadAction.ChangedProperties.Contains(TouchpadActionPad.PropertyKeyStrings.MAX_ZONE);
            }

            public bool RequiresClick
            {
                get => touchActionPadAction.RequiresClick;
                set
                {
                    touchActionPadAction.RequiresClick = value;
                    RequiresClickChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler RequiresClickChanged;

            [JsonConverter(typeof(SafeStringEnumConverter),
                TouchpadActionPad.DPadMode.Standard)]
            public TouchpadActionPad.DPadMode PadMode
            {
                get => touchActionPadAction.CurrentMode;
                set
                {
                    touchActionPadAction.CurrentMode = value;
                    PadModeChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler PadModeChanged;
            public bool ShouldSerializePadMode()
            {
                return touchActionPadAction.ChangedProperties.Contains(TouchpadActionPad.PropertyKeyStrings.PAD_MODE);
            }

            [JsonConverter(typeof(StringEnumConverter))]
            public StickDeadZone.DeadZoneTypes DeadZoneType
            {
                get => touchActionPadAction.DeadMod.DeadZoneType;
                set
                {
                    touchActionPadAction.DeadMod.DeadZoneType = value;
                    DeadZoneTypeChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler DeadZoneTypeChanged;
            public bool ShouldSerializeDeadZoneType()
            {
                return touchActionPadAction.ChangedProperties.Contains(TouchpadActionPad.PropertyKeyStrings.DEAD_ZONE_TYPE);
            }

            public int DiagonalRange
            {
                get => touchActionPadAction.DiagonalRange;
                set
                {
                    touchActionPadAction.DiagonalRange = Math.Clamp(value, -180, 180);
                    DiagonalRangeChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler DiagonalRangeChanged;
            public bool ShouldSerializeDiagonalRange()
            {
                return touchActionPadAction.ChangedProperties.Contains(TouchpadActionPad.PropertyKeyStrings.DIAGONAL_RANGE);
            }

            public int Rotation
            {
                get => touchActionPadAction.Rotation;
                set
                {
                    touchActionPadAction.Rotation = value;
                    RotationChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler RotationChanged;
            public bool ShouldSerializeRotation()
            {
                return touchActionPadAction.ChangedProperties.Contains(TouchpadActionPad.PropertyKeyStrings.ROTATION);
            }

            public bool DelayEnabled
            {
                get => touchActionPadAction.DelayEnabled;
                set
                {
                    touchActionPadAction.DelayEnabled = value;
                    DelayEnabledChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler DelayEnabledChanged;
            public bool ShouldSerializeDelayEnabled()
            {
                return touchActionPadAction.ChangedProperties.Contains(TouchpadActionPad.PropertyKeyStrings.DELAY_ENABLED);
            }

            // TODO: Double check time interval used
            public double DelayTime
            {
                get => touchActionPadAction.DelayTime;
                set
                {
                    touchActionPadAction.DelayTime = Math.Clamp(value, 0.0, 3600.0);
                    DelayTimeChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler DelayTimeChanged;
            public bool ShouldSerializeDelayTime()
            {
                return touchActionPadAction.ChangedProperties.Contains(TouchpadActionPad.PropertyKeyStrings.DELAY_TIME);
            }


            [JsonProperty("UseOuterRing")]
            public bool UseOuterRing
            {
                get => touchActionPadAction.UseRingButton;
                set
                {
                    touchActionPadAction.UseRingButton = value;
                    UseOuterRingChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler UseOuterRingChanged;
            public bool ShouldSerializeUseOuterRing()
            {
                return touchActionPadAction.ChangedProperties.Contains(TouchpadActionPad.PropertyKeyStrings.USE_OUTER_RING);
            }

            [JsonProperty("OuterRingDeadZone")]
            public double OuterRingDeadZone
            {
                get => touchActionPadAction.OuterRingDeadZone;
                set
                {
                    touchActionPadAction.OuterRingDeadZone = value;
                    OuterRingDeadZoneChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler OuterRingDeadZoneChanged;
            public bool ShouldSerializeOuterRingDeadZone()
            {
                return touchActionPadAction.ChangedProperties.Contains(TouchpadActionPad.PropertyKeyStrings.OUTER_RING_DEAD_ZONE);
            }

            [JsonProperty("UseAsOuterRing")]
            public bool UseAsOuterRing
            {
                get => touchActionPadAction.UseAsOuterRing;
                set
                {
                    touchActionPadAction.UseAsOuterRing = value;
                    UseAsOuterRingChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler UseAsOuterRingChanged;
            public bool ShouldSerializeUseAsOuterRing()
            {
                return touchActionPadAction.ChangedProperties.Contains(TouchpadActionPad.PropertyKeyStrings.USE_AS_OUTER_RING);
            }

            [JsonConverter(typeof(StringEnumConverter))]
            public OuterRingUseRange OuterRingRange
            {
                get => touchActionPadAction.UsedOuterRingRange;
                set
                {
                    touchActionPadAction.UsedOuterRingRange = value;
                    OuterRingRangeChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler OuterRingRangeChanged;
            public bool ShouldSerializeOuterRange()
            {
                return touchActionPadAction.ChangedProperties.Contains(TouchpadActionPad.PropertyKeyStrings.OUTER_RING_FULL_RANGE);
            }

            public bool CounterMovementReleasePressEnabled
            {
                get => touchActionPadAction.ReleaseBrake.Enabled;
                set
                {
                    touchActionPadAction.ReleaseBrake.Enabled = value;
                    CounterMovementReleasePressEnabledChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler CounterMovementReleasePressEnabledChanged;
            public bool ShouldSerializeCounterMovementReleasePressEnabled()
            {
                return touchActionPadAction.ChangedProperties.Contains(TouchpadActionPad.PropertyKeyStrings.COUNTER_MOVEMENT_ENABLED);
            }

            public string CounterMovementTapLengthPreset
            {
                get => touchActionPadAction.ReleaseBrake.TapLengthPreset.ToString();
                set
                {
                    if (Enum.TryParse(value, out DS4MapperTest.StickActions.CounterMovementTapLengthPreset temp))
                    {
                        touchActionPadAction.ReleaseBrake.TapLengthPreset = temp;
                        CounterMovementTapLengthPresetChanged?.Invoke(this, EventArgs.Empty);
                    }
                }
            }
            public event EventHandler CounterMovementTapLengthPresetChanged;
            public bool ShouldSerializeCounterMovementTapLengthPreset()
            {
                return touchActionPadAction.ChangedProperties.Contains(TouchpadActionPad.PropertyKeyStrings.COUNTER_MOVEMENT_TAP_LENGTH_PRESET);
            }

            [JsonConverter(typeof(StringEnumConverter))]
            public OppositeTapLengthMode OppositeTapLengthMode
            {
                get => touchActionPadAction.ReleaseBrake.OppositeTapLengthMode;
                set
                {
                    touchActionPadAction.ReleaseBrake.OppositeTapLengthMode = value;
                    OppositeTapLengthModeChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler OppositeTapLengthModeChanged;
            public bool ShouldSerializeOppositeTapLengthMode()
            {
                return touchActionPadAction.ChangedProperties.Contains(TouchpadActionPad.PropertyKeyStrings.COUNTER_MOVEMENT_TAP_LENGTH_MODE);
            }

            public int OppositeTapLengthMs
            {
                get => touchActionPadAction.ReleaseBrake.OppositeTapLengthMs;
                set
                {
                    touchActionPadAction.ReleaseBrake.OppositeTapLengthMs = value;
                    OppositeTapLengthMsChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler OppositeTapLengthMsChanged;
            public bool ShouldSerializeOppositeTapLengthMs()
            {
                return touchActionPadAction.ChangedProperties.Contains(TouchpadActionPad.PropertyKeyStrings.COUNTER_MOVEMENT_TAP_LENGTH_FIXED_MS);
            }

            public int OppositeTapLengthVariancePercent
            {
                get => touchActionPadAction.ReleaseBrake.OppositeTapLengthVariancePercent;
                set
                {
                    touchActionPadAction.ReleaseBrake.OppositeTapLengthVariancePercent = value;
                    OppositeTapLengthVariancePercentChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler OppositeTapLengthVariancePercentChanged;
            public bool ShouldSerializeOppositeTapLengthVariancePercent()
            {
                return touchActionPadAction.ChangedProperties.Contains(TouchpadActionPad.PropertyKeyStrings.COUNTER_MOVEMENT_TAP_LENGTH_VARIANCE_PERCENT);
            }

            public int OppositeTapLengthMinimumMs
            {
                get => touchActionPadAction.ReleaseBrake.OppositeTapLengthMinimumMs;
                set
                {
                    touchActionPadAction.ReleaseBrake.OppositeTapLengthMinimumMs = value;
                    OppositeTapLengthMinimumMsChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler OppositeTapLengthMinimumMsChanged;
            public bool ShouldSerializeOppositeTapLengthMinimumMs()
            {
                return touchActionPadAction.ChangedProperties.Contains(TouchpadActionPad.PropertyKeyStrings.COUNTER_MOVEMENT_TAP_LENGTH_MIN_MS);
            }

            public int OppositeTapLengthMaximumMs
            {
                get => touchActionPadAction.ReleaseBrake.OppositeTapLengthMaximumMs;
                set
                {
                    touchActionPadAction.ReleaseBrake.OppositeTapLengthMaximumMs = value;
                    OppositeTapLengthMaximumMsChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler OppositeTapLengthMaximumMsChanged;
            public bool ShouldSerializeOppositeTapLengthMaximumMs()
            {
                return touchActionPadAction.ChangedProperties.Contains(TouchpadActionPad.PropertyKeyStrings.COUNTER_MOVEMENT_TAP_LENGTH_MAX_MS);
            }

            public int OppositeTapStartDelayMinimumMs
            {
                get => touchActionPadAction.ReleaseBrake.OppositeTapStartDelayMinimumMs;
                set
                {
                    touchActionPadAction.ReleaseBrake.OppositeTapStartDelayMinimumMs = value;
                    OppositeTapStartDelayMinimumMsChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler OppositeTapStartDelayMinimumMsChanged;
            public bool ShouldSerializeOppositeTapStartDelayMinimumMs()
            {
                return touchActionPadAction.ChangedProperties.Contains(TouchpadActionPad.PropertyKeyStrings.COUNTER_MOVEMENT_START_DELAY_MIN_MS);
            }

            public int OppositeTapStartDelayMaximumMs
            {
                get => touchActionPadAction.ReleaseBrake.OppositeTapStartDelayMaximumMs;
                set
                {
                    touchActionPadAction.ReleaseBrake.OppositeTapStartDelayMaximumMs = value;
                    OppositeTapStartDelayMaximumMsChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler OppositeTapStartDelayMaximumMsChanged;
            public bool ShouldSerializeOppositeTapStartDelayMaximumMs()
            {
                return touchActionPadAction.ChangedProperties.Contains(TouchpadActionPad.PropertyKeyStrings.COUNTER_MOVEMENT_START_DELAY_MAX_MS);
            }

            public bool CounterMovementDeadZoneReleaseEnabled
            {
                get => touchActionPadAction.ReleaseBrake.TriggerOnDeadZoneReleaseEnabled;
                set
                {
                    touchActionPadAction.ReleaseBrake.TriggerOnDeadZoneReleaseEnabled = value;
                    CounterMovementDeadZoneReleaseEnabledChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler CounterMovementDeadZoneReleaseEnabledChanged;
            public bool ShouldSerializeCounterMovementDeadZoneReleaseEnabled()
            {
                return touchActionPadAction.ChangedProperties.Contains(TouchpadActionPad.PropertyKeyStrings.COUNTER_MOVEMENT_DEADZONE_RELEASE_ENABLED);
            }

            [JsonProperty("BrakeMinimumHoldMs")]
            public int BrakeMinimumHoldMs
            {
                get => touchActionPadAction.ReleaseBrake.MinimumHoldMs;
                set
                {
                    touchActionPadAction.ReleaseBrake.MinimumHoldMs = value;
                    BrakeMinimumHoldMsChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler BrakeMinimumHoldMsChanged;
            public bool ShouldSerializeBrakeMinimumHoldMs()
            {
                return touchActionPadAction.ChangedProperties.Contains(TouchpadActionPad.PropertyKeyStrings.BRAKE_MIN_HOLD_MS);
            }

            private bool? legacyBrakeEnabled;
            internal bool? LegacyBrakeEnabled => legacyBrakeEnabled;
            public bool BrakeEnabled
            {
                get => false;
                set => legacyBrakeEnabled = value;
            }
            public bool ShouldSerializeBrakeEnabled() => false;

            private int? legacyBrakeDurationMs;
            internal int? LegacyBrakeDurationMs => legacyBrakeDurationMs;
            public int BrakeDurationMs
            {
                get => 0;
                set => legacyBrakeDurationMs = value;
            }
            public bool ShouldSerializeBrakeDurationMs() => false;

            public TouchpadActionPadSettings(TouchpadActionPad action)
            {
                touchActionPadAction = action;
            }
        }
        
        private Dictionary<TouchpadActionPad.DpadDirections, TouchPadDirBinding> dictPadBindings =
            new Dictionary<TouchpadActionPad.DpadDirections, TouchPadDirBinding>();

        [JsonProperty("Bindings", Required = Required.Always)]
        public Dictionary<TouchpadActionPad.DpadDirections, TouchPadDirBinding> DictPadBindings
        {
            get => dictPadBindings;
            set => dictPadBindings = value;
        }
        public bool ShouldSerializeDictPadBindings()
        {
            return dictPadBindings != null && dictPadBindings.Count > 0;
        }

        private TouchPadDirBinding ringBinding;

        [JsonProperty("OuterRingBinding")]
        public TouchPadDirBinding RingBinding
        {
            get => ringBinding;
            set
            {
                ringBinding = value;
                RingBindingChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        public event EventHandler RingBindingChanged;
        public bool ShouldSerializeRingBinding()
        {
            return ringBinding != null && ringBinding.ActionFuncSerializers.Count > 0;
        }

        private TouchpadActionPad touchActionPadAction = new TouchpadActionPad();

        private TouchpadActionPadSettings settings;
        public TouchpadActionPadSettings Settings
        {
            get => settings;
            set => settings = value;
        }
        public bool ShouldSerializeSettings()
        {
            return touchActionPadAction.ChangedProperties.Count > 0;
        }

        // Deserialize
        public TouchpadActionPadSerializer() : base()
        {
            mapAction = touchActionPadAction;
            settings = new TouchpadActionPadSettings(touchActionPadAction);

            NameChanged += TouchpadActionPadSerializer_NameChanged;
            RingBindingChanged += TouchpadActionPadSerializer_RingBindingChanged;
            settings.RequiresClickChanged += Settings_RequiresClickChanged;
            settings.DeadZoneChanged += Settings_DeadZoneChanged;
            settings.MaxZoneChanged += Settings_MaxZoneChanged;
            settings.DeadZoneTypeChanged += Settings_DeadZoneTypeChanged;
            settings.DiagonalRangeChanged += Settings_DiagonalRangeChanged;
            settings.PadModeChanged += Settings_PadModeChanged;
            settings.RotationChanged += Settings_RotationChanged;
            settings.DelayEnabledChanged += Settings_DelayEnabledChanged;
            settings.DelayTimeChanged += Settings_DelayTimeChanged;

            settings.UseOuterRingChanged += Settings_UseOuterRingChanged;
            settings.UseAsOuterRingChanged += Settings_UseAsOuterRingChanged;
            settings.OuterRingDeadZoneChanged += Settings_OuterRingDeadZoneChanged;
            settings.OuterRingRangeChanged += Settings_OuterRingRangeChanged;
            settings.CounterMovementReleasePressEnabledChanged += Settings_CounterMovementReleasePressEnabledChanged;
            settings.CounterMovementTapLengthPresetChanged += Settings_CounterMovementTapLengthPresetChanged;
            settings.OppositeTapLengthModeChanged += Settings_OppositeTapLengthModeChanged;
            settings.OppositeTapLengthMsChanged += Settings_OppositeTapLengthMsChanged;
            settings.OppositeTapLengthVariancePercentChanged += Settings_OppositeTapLengthVariancePercentChanged;
            settings.OppositeTapLengthMinimumMsChanged += Settings_OppositeTapLengthMinimumMsChanged;
            settings.OppositeTapLengthMaximumMsChanged += Settings_OppositeTapLengthMaximumMsChanged;
            settings.OppositeTapStartDelayMinimumMsChanged += Settings_OppositeTapStartDelayMinimumMsChanged;
            settings.OppositeTapStartDelayMaximumMsChanged += Settings_OppositeTapStartDelayMaximumMsChanged;
            settings.CounterMovementDeadZoneReleaseEnabledChanged += Settings_CounterMovementDeadZoneReleaseEnabledChanged;
            settings.BrakeMinimumHoldMsChanged += Settings_BrakeMinimumHoldMsChanged;
        }

        private void Settings_CounterMovementReleasePressEnabledChanged(object sender, EventArgs e)
        {
            touchActionPadAction.ChangedProperties.Add(TouchpadActionPad.PropertyKeyStrings.COUNTER_MOVEMENT_ENABLED);
        }

        private void Settings_CounterMovementTapLengthPresetChanged(object sender, EventArgs e)
        {
            touchActionPadAction.ChangedProperties.Add(TouchpadActionPad.PropertyKeyStrings.COUNTER_MOVEMENT_TAP_LENGTH_PRESET);
        }

        private void Settings_OppositeTapLengthModeChanged(object sender, EventArgs e)
        {
            touchActionPadAction.ChangedProperties.Add(TouchpadActionPad.PropertyKeyStrings.COUNTER_MOVEMENT_TAP_LENGTH_MODE);
        }

        private void Settings_OppositeTapLengthMsChanged(object sender, EventArgs e)
        {
            touchActionPadAction.ChangedProperties.Add(TouchpadActionPad.PropertyKeyStrings.COUNTER_MOVEMENT_TAP_LENGTH_FIXED_MS);
        }

        private void Settings_OppositeTapLengthVariancePercentChanged(object sender, EventArgs e)
        {
            touchActionPadAction.ChangedProperties.Add(TouchpadActionPad.PropertyKeyStrings.COUNTER_MOVEMENT_TAP_LENGTH_VARIANCE_PERCENT);
        }

        private void Settings_OppositeTapLengthMinimumMsChanged(object sender, EventArgs e)
        {
            touchActionPadAction.ChangedProperties.Add(TouchpadActionPad.PropertyKeyStrings.COUNTER_MOVEMENT_TAP_LENGTH_MIN_MS);
        }

        private void Settings_OppositeTapLengthMaximumMsChanged(object sender, EventArgs e)
        {
            touchActionPadAction.ChangedProperties.Add(TouchpadActionPad.PropertyKeyStrings.COUNTER_MOVEMENT_TAP_LENGTH_MAX_MS);
        }

        private void Settings_OppositeTapStartDelayMinimumMsChanged(object sender, EventArgs e)
        {
            touchActionPadAction.ChangedProperties.Add(TouchpadActionPad.PropertyKeyStrings.COUNTER_MOVEMENT_START_DELAY_MIN_MS);
        }

        private void Settings_OppositeTapStartDelayMaximumMsChanged(object sender, EventArgs e)
        {
            touchActionPadAction.ChangedProperties.Add(TouchpadActionPad.PropertyKeyStrings.COUNTER_MOVEMENT_START_DELAY_MAX_MS);
        }

        private void Settings_CounterMovementDeadZoneReleaseEnabledChanged(object sender, EventArgs e)
        {
            touchActionPadAction.ChangedProperties.Add(TouchpadActionPad.PropertyKeyStrings.COUNTER_MOVEMENT_DEADZONE_RELEASE_ENABLED);
        }

        private void Settings_BrakeMinimumHoldMsChanged(object sender, EventArgs e)
        {
            touchActionPadAction.ChangedProperties.Add(TouchpadActionPad.PropertyKeyStrings.BRAKE_MIN_HOLD_MS);
        }

        private void Settings_RequiresClickChanged(object sender, EventArgs e)
        {
            touchActionPadAction.ChangedProperties.Add(TouchpadActionPad.PropertyKeyStrings.REQUIRES_CLICK);
        }

        private void Settings_OuterRingRangeChanged(object sender, EventArgs e)
        {
            touchActionPadAction.ChangedProperties.Add(TouchpadActionPad.PropertyKeyStrings.OUTER_RING_FULL_RANGE);
        }

        private void Settings_DelayTimeChanged(object sender, EventArgs e)
        {
            touchActionPadAction.ChangedProperties.Add(TouchpadActionPad.PropertyKeyStrings.DELAY_TIME);
        }

        private void Settings_DelayEnabledChanged(object sender, EventArgs e)
        {
            touchActionPadAction.ChangedProperties.Add(TouchpadActionPad.PropertyKeyStrings.DELAY_ENABLED);
        }

        private void Settings_DeadZoneTypeChanged(object sender, EventArgs e)
        {
            touchActionPadAction.ChangedProperties.Add(TouchpadActionPad.PropertyKeyStrings.DEAD_ZONE_TYPE);
        }

        private void Settings_RotationChanged(object sender, EventArgs e)
        {
            touchActionPadAction.ChangedProperties.Add(TouchpadActionPad.PropertyKeyStrings.ROTATION);
        }

        private void Settings_MaxZoneChanged(object sender, EventArgs e)
        {
            touchActionPadAction.ChangedProperties.Add(TouchpadActionPad.PropertyKeyStrings.MAX_ZONE);
        }

        private void Settings_OuterRingDeadZoneChanged(object sender, EventArgs e)
        {
            touchActionPadAction.ChangedProperties.Add(TouchpadActionPad.PropertyKeyStrings.OUTER_RING_DEAD_ZONE);
        }

        private void Settings_UseAsOuterRingChanged(object sender, EventArgs e)
        {
            touchActionPadAction.ChangedProperties.Add(TouchpadActionPad.PropertyKeyStrings.USE_AS_OUTER_RING);
        }

        private void Settings_UseOuterRingChanged(object sender, EventArgs e)
        {
            touchActionPadAction.ChangedProperties.Add(TouchpadActionPad.PropertyKeyStrings.USE_OUTER_RING);
        }

        private void TouchpadActionPadSerializer_RingBindingChanged(object sender, EventArgs e)
        {
            touchActionPadAction.ChangedProperties.Add(TouchpadActionPad.PropertyKeyStrings.OUTER_RING_BUTTON);
        }

        private void Settings_PadModeChanged(object sender, EventArgs e)
        {
            touchActionPadAction.ChangedProperties.Add(TouchpadActionPad.PropertyKeyStrings.PAD_MODE);
        }

        private void Settings_DiagonalRangeChanged(object sender, EventArgs e)
        {
            touchActionPadAction.ChangedProperties.Add(TouchpadActionPad.PropertyKeyStrings.DIAGONAL_RANGE);
        }

        private void Settings_DeadZoneChanged(object sender, EventArgs e)
        {
            touchActionPadAction.ChangedProperties.Add(TouchpadActionPad.PropertyKeyStrings.DEAD_ZONE);
        }

        private void TouchpadActionPadSerializer_NameChanged(object sender, EventArgs e)
        {
            touchActionPadAction.ChangedProperties.Add(TouchpadActionPad.PropertyKeyStrings.NAME);
        }

        // Pre-serialize
        public TouchpadActionPadSerializer(ActionLayer tempLayer, MapAction mapAction) :
            base(tempLayer, mapAction)
        {
            if (mapAction is TouchpadActionPad temp)
            {
                touchActionPadAction = temp;
                this.mapAction = touchActionPadAction;
                settings = new TouchpadActionPadSettings(touchActionPadAction);
                PopulateFuncs();
            }
        }

        // Pre-serialize
        private void PopulateFuncs()
        {
            List<ActionFuncSerializer> tempFuncs = new List<ActionFuncSerializer>();
            TouchpadActionPad.DpadDirections[] tempDirs = null;

            if (touchActionPadAction.CurrentMode == TouchpadActionPad.DPadMode.Standard ||
                touchActionPadAction.CurrentMode == TouchpadActionPad.DPadMode.FourWayCardinal)
            {
                tempDirs = new TouchpadActionPad.DpadDirections[4]
                {
                    TouchpadActionPad.DpadDirections.Up, TouchpadActionPad.DpadDirections.Down,
                    TouchpadActionPad.DpadDirections.Left, TouchpadActionPad.DpadDirections.Right
                };
            }
            else if (touchActionPadAction.CurrentMode == TouchpadActionPad.DPadMode.EightWay)
            {
                tempDirs = new TouchpadActionPad.DpadDirections[8]
                {
                    TouchpadActionPad.DpadDirections.Up, TouchpadActionPad.DpadDirections.Down,
                    TouchpadActionPad.DpadDirections.Left, TouchpadActionPad.DpadDirections.Right,
                    TouchpadActionPad.DpadDirections.UpLeft, TouchpadActionPad.DpadDirections.UpRight,
                    TouchpadActionPad.DpadDirections.DownLeft, TouchpadActionPad.DpadDirections.DownRight
                };
            }
            else if (touchActionPadAction.CurrentMode == TouchpadActionPad.DPadMode.FourWayDiagonal)
            {
                tempDirs = new TouchpadActionPad.DpadDirections[4]
                {
                    TouchpadActionPad.DpadDirections.UpLeft, TouchpadActionPad.DpadDirections.UpRight,
                    TouchpadActionPad.DpadDirections.DownLeft, TouchpadActionPad.DpadDirections.DownRight
                };
            }

            for (int i = 0; i < tempDirs.Length; i++)
            {
                TouchpadActionPad.DpadDirections tempDir = tempDirs[i];
                ButtonAction dirButton = touchActionPadAction.EventCodes4[(int)tempDir];

                tempFuncs.Clear();
                foreach (ActionFunc tempFunc in dirButton.ActionFuncs)
                {
                    ActionFuncSerializer tempSerializer =
                        ActionFuncSerializerFactory.CreateSerializer(tempFunc);
                    if (tempSerializer != null)
                    {
                        tempFuncs.Add(tempSerializer);
                    }
                }

                //dictPadBindings.Add(tempDir, tempFuncs);
                dictPadBindings.Add(tempDir,
                    new TouchPadDirBinding()
                    {
                        ActionDirName = dirButton.Name,
                        ActionFuncSerializers = new List<ActionFuncSerializer>(tempFuncs),
                    });
            }

            if (touchActionPadAction.RingButton != null)
            {
                ringBinding = new TouchPadDirBinding();
                ringBinding.ActionDirName = touchActionPadAction.RingButton.Name;
                foreach (ActionFunc tempFunc in touchActionPadAction.RingButton.ActionFuncs)
                {
                    ActionFuncSerializer tempSerializer =
                        ActionFuncSerializerFactory.CreateSerializer(tempFunc);
                    if (tempSerializer != null)
                    {
                        ringBinding.ActionFuncSerializers.Add(tempSerializer);
                    }
                }
            }
        }

        // Post-deserialize
        public override void PopulateMap()
        {
            foreach (ButtonAction dirButton in touchActionPadAction.EventCodes4)
            {
                dirButton.ActionFuncs.Clear();
            }

            foreach (KeyValuePair<TouchpadActionPad.DpadDirections, TouchPadDirBinding> tempKeyPair in dictPadBindings)
            //foreach(KeyValuePair<StickPadAction.DpadDirections, List<ActionFuncSerializer>> tempKeyPair in dictPadBindings)
            //foreach(DictionaryEntry entry in dictPadBindings)
            {
                TouchpadActionPad.DpadDirections dir = tempKeyPair.Key;
                List<ActionFuncSerializer> tempSerializers = tempKeyPair.Value.ActionFuncSerializers;
                //List<ActionFuncSerializer> tempSerializers = tempKeyPair.Value;
                //StickPadAction.DpadDirections dir = (StickPadAction.DpadDirections)entry.Key;
                //List<ActionFuncSerializer> tempSerializers = entry.Value as List<ActionFuncSerializer>;

                ButtonAction tempDirButton = null;
                //foreach (AxisDirButton dirButton in stickPadAct.EventCodes4)
                {
                    tempDirButton = touchActionPadAction.EventCodes4[(int)dir];
                    if (tempDirButton != null)
                    {
                        tempDirButton.Name = tempKeyPair.Value.ActionDirName;
                    }
                }

                if (tempDirButton != null)
                {
                    foreach (ActionFuncSerializer serializer in tempSerializers)
                    {
                        serializer.PopulateFunc();
                        tempDirButton.ActionFuncs.Add(serializer.ActionFunc);
                    }

                    FlagBtnChangedDirection(dir, touchActionPadAction);
                    tempDirButton = null;
                }
            }

            if (ringBinding != null)
            {
                touchActionPadAction.RingButton.Name = ringBinding.ActionDirName;
                List<ActionFuncSerializer> tempSerializers = ringBinding.ActionFuncSerializers;
                foreach (ActionFuncSerializer serializer in tempSerializers)
                {
                    serializer.PopulateFunc();
                    touchActionPadAction.RingButton.ActionFuncs.Add(serializer.ActionFunc);
                }

                touchActionPadAction.ChangedProperties.Add(StickPadAction.PropertyKeyStrings.OUTER_RING_BUTTON);
                //stickPadAct.RingButton.ChangedProperties.Add(StickPadAction.PropertyKeyStrings.OUTER_RING_BUTTON);
            }

            MigrateLegacyCounterMovementReleasePressFields();
        }

        // Migrates up to three profile generations for the tap-length representation:
        // (a) current profiles that already have an explicit Minimum/Maximum range but no
        //     Mode field (saved before timing modes existed) migrate to Minimum and Maximum
        //     mode, deriving the best-fit Fixed/Percent representation from that range;
        // (b) older profiles with only a legacy Brake Duration migrate to Fixed mode at that
        //     exact duration, 0% variance, preserving the old immediate-start behaviour;
        // (c) brand new profiles with none of the above simply keep the constructed CS2/Wait
        //     Variance Percentage defaults untouched.
        private void MigrateLegacyCounterMovementReleasePressFields()
        {
            if (settings.LegacyBrakeEnabled.HasValue &&
                !touchActionPadAction.ChangedProperties.Contains(TouchpadActionPad.PropertyKeyStrings.COUNTER_MOVEMENT_ENABLED))
            {
                touchActionPadAction.ReleaseBrake.Enabled = settings.LegacyBrakeEnabled.Value;
                touchActionPadAction.ChangedProperties.Add(TouchpadActionPad.PropertyKeyStrings.COUNTER_MOVEMENT_ENABLED);
            }

            bool modeFieldPresent = touchActionPadAction.ChangedProperties.Contains(TouchpadActionPad.PropertyKeyStrings.COUNTER_MOVEMENT_TAP_LENGTH_MODE);

            if (settings.LegacyBrakeDurationMs.HasValue)
            {
                int legacyDuration = DigitalReleaseBrakePulse.ClampBrakeDurationMs(settings.LegacyBrakeDurationMs.Value);

                if (!touchActionPadAction.ChangedProperties.Contains(TouchpadActionPad.PropertyKeyStrings.COUNTER_MOVEMENT_TAP_LENGTH_MIN_MS))
                {
                    touchActionPadAction.ReleaseBrake.OppositeTapLengthMinimumMs = legacyDuration;
                    touchActionPadAction.ChangedProperties.Add(TouchpadActionPad.PropertyKeyStrings.COUNTER_MOVEMENT_TAP_LENGTH_MIN_MS);
                }

                if (!touchActionPadAction.ChangedProperties.Contains(TouchpadActionPad.PropertyKeyStrings.COUNTER_MOVEMENT_TAP_LENGTH_MAX_MS))
                {
                    touchActionPadAction.ReleaseBrake.OppositeTapLengthMaximumMs = legacyDuration;
                    touchActionPadAction.ChangedProperties.Add(TouchpadActionPad.PropertyKeyStrings.COUNTER_MOVEMENT_TAP_LENGTH_MAX_MS);
                }

                if (!touchActionPadAction.ChangedProperties.Contains(TouchpadActionPad.PropertyKeyStrings.COUNTER_MOVEMENT_TAP_LENGTH_FIXED_MS))
                {
                    touchActionPadAction.ReleaseBrake.OppositeTapLengthMs = legacyDuration;
                    touchActionPadAction.ChangedProperties.Add(TouchpadActionPad.PropertyKeyStrings.COUNTER_MOVEMENT_TAP_LENGTH_FIXED_MS);
                }

                if (!touchActionPadAction.ChangedProperties.Contains(TouchpadActionPad.PropertyKeyStrings.COUNTER_MOVEMENT_TAP_LENGTH_VARIANCE_PERCENT))
                {
                    touchActionPadAction.ReleaseBrake.OppositeTapLengthVariancePercent = 0;
                    touchActionPadAction.ChangedProperties.Add(TouchpadActionPad.PropertyKeyStrings.COUNTER_MOVEMENT_TAP_LENGTH_VARIANCE_PERCENT);
                }

                if (!modeFieldPresent)
                {
                    touchActionPadAction.ReleaseBrake.OppositeTapLengthMode = OppositeTapLengthMode.Fixed;
                    touchActionPadAction.ChangedProperties.Add(TouchpadActionPad.PropertyKeyStrings.COUNTER_MOVEMENT_TAP_LENGTH_MODE);
                }

                if (!touchActionPadAction.ChangedProperties.Contains(TouchpadActionPad.PropertyKeyStrings.COUNTER_MOVEMENT_START_DELAY_MIN_MS))
                {
                    touchActionPadAction.ReleaseBrake.OppositeTapStartDelayMinimumMs = 0;
                    touchActionPadAction.ChangedProperties.Add(TouchpadActionPad.PropertyKeyStrings.COUNTER_MOVEMENT_START_DELAY_MIN_MS);
                }

                if (!touchActionPadAction.ChangedProperties.Contains(TouchpadActionPad.PropertyKeyStrings.COUNTER_MOVEMENT_START_DELAY_MAX_MS))
                {
                    touchActionPadAction.ReleaseBrake.OppositeTapStartDelayMaximumMs = 0;
                    touchActionPadAction.ChangedProperties.Add(TouchpadActionPad.PropertyKeyStrings.COUNTER_MOVEMENT_START_DELAY_MAX_MS);
                }

                if (!touchActionPadAction.ChangedProperties.Contains(TouchpadActionPad.PropertyKeyStrings.COUNTER_MOVEMENT_START_DELAY_MODE))
                {
                    touchActionPadAction.ReleaseBrake.OppositeTapStartDelayMode = OppositeTapStartDelayMode.Fixed;
                    touchActionPadAction.ChangedProperties.Add(TouchpadActionPad.PropertyKeyStrings.COUNTER_MOVEMENT_START_DELAY_MODE);
                }

                if (!touchActionPadAction.ChangedProperties.Contains(TouchpadActionPad.PropertyKeyStrings.COUNTER_MOVEMENT_START_DELAY_FIXED_MS))
                {
                    touchActionPadAction.ReleaseBrake.OppositeTapStartDelayMs = 0;
                    touchActionPadAction.ChangedProperties.Add(TouchpadActionPad.PropertyKeyStrings.COUNTER_MOVEMENT_START_DELAY_FIXED_MS);
                }

                if (!touchActionPadAction.ChangedProperties.Contains(TouchpadActionPad.PropertyKeyStrings.COUNTER_MOVEMENT_START_DELAY_VARIANCE_PERCENT))
                {
                    touchActionPadAction.ReleaseBrake.OppositeTapStartDelayVariancePercent = 0;
                    touchActionPadAction.ChangedProperties.Add(TouchpadActionPad.PropertyKeyStrings.COUNTER_MOVEMENT_START_DELAY_VARIANCE_PERCENT);
                }

                if (!touchActionPadAction.ChangedProperties.Contains(TouchpadActionPad.PropertyKeyStrings.COUNTER_MOVEMENT_TAP_LENGTH_PRESET))
                {
                    touchActionPadAction.ReleaseBrake.TapLengthPreset =
                        DS4MapperTest.StickActions.CounterMovementTapLengthPreset.Custom;
                    touchActionPadAction.ChangedProperties.Add(TouchpadActionPad.PropertyKeyStrings.COUNTER_MOVEMENT_TAP_LENGTH_PRESET);
                }
            }
            else if (!modeFieldPresent &&
                (touchActionPadAction.ChangedProperties.Contains(TouchpadActionPad.PropertyKeyStrings.COUNTER_MOVEMENT_TAP_LENGTH_MIN_MS) ||
                 touchActionPadAction.ChangedProperties.Contains(TouchpadActionPad.PropertyKeyStrings.COUNTER_MOVEMENT_TAP_LENGTH_MAX_MS)))
            {
                touchActionPadAction.ReleaseBrake.OppositeTapLengthMode = OppositeTapLengthMode.MinimumAndMaximum;
                touchActionPadAction.ChangedProperties.Add(TouchpadActionPad.PropertyKeyStrings.COUNTER_MOVEMENT_TAP_LENGTH_MODE);
                touchActionPadAction.ReleaseBrake.ApplyMinimumAndMaximum(
                    touchActionPadAction.ReleaseBrake.OppositeTapLengthMinimumMs,
                    touchActionPadAction.ReleaseBrake.OppositeTapLengthMaximumMs);
            }

            touchActionPadAction.ReleaseBrake.NormalizeRanges();
        }

        public void FlagBtnChangedDirection(TouchpadActionPad.DpadDirections dir,
            TouchpadActionPad action)
        {
            switch (dir)
            {
                case TouchpadActionPad.DpadDirections.Up:
                    action.ChangedProperties.Add(TouchpadActionPad.PropertyKeyStrings.PAD_DIR_UP);
                    break;
                case TouchpadActionPad.DpadDirections.Down:
                    action.ChangedProperties.Add(TouchpadActionPad.PropertyKeyStrings.PAD_DIR_DOWN);
                    break;
                case TouchpadActionPad.DpadDirections.Left:
                    action.ChangedProperties.Add(TouchpadActionPad.PropertyKeyStrings.PAD_DIR_LEFT);
                    break;
                case TouchpadActionPad.DpadDirections.Right:
                    action.ChangedProperties.Add(TouchpadActionPad.PropertyKeyStrings.PAD_DIR_RIGHT);
                    break;
                case TouchpadActionPad.DpadDirections.UpLeft:
                    action.ChangedProperties.Add(TouchpadActionPad.PropertyKeyStrings.PAD_DIR_UPLEFT);
                    break;
                case TouchpadActionPad.DpadDirections.UpRight:
                    action.ChangedProperties.Add(TouchpadActionPad.PropertyKeyStrings.PAD_DIR_UPRIGHT);
                    break;
                case TouchpadActionPad.DpadDirections.DownLeft:
                    action.ChangedProperties.Add(TouchpadActionPad.PropertyKeyStrings.PAD_DIR_DOWNLEFT);
                    break;
                case TouchpadActionPad.DpadDirections.DownRight:
                    action.ChangedProperties.Add(TouchpadActionPad.PropertyKeyStrings.PAD_DIR_DOWNRIGHT);
                    break;
                default:
                    break;
            }
        }
    }

    public class TouchpadMouseSerializer : MapActionSerializer
    {
        public class TouchpadMouseSettings
        {
            private TouchpadMouse touchMouseAct;

            public int DeadZone
            {
                get => touchMouseAct.DeadZone;
                set
                {
                    touchMouseAct.DeadZone = value;
                    DeadZoneChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler DeadZoneChanged;
            public bool ShouldSerializeDeadZone()
            {
                return touchMouseAct.ChangedProperties.Contains(TouchpadMouse.PropertyKeyStrings.DEAD_ZONE);
            }

            public int VerticalDeadZone
            {
                get => touchMouseAct.VerticalDeadZone;
                set
                {
                    touchMouseAct.VerticalDeadZone = Math.Clamp(value, 0, 10000);
                    VerticalDeadZoneChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler VerticalDeadZoneChanged;
            public bool ShouldSerializeVerticalDeadZone()
            {
                return touchMouseAct.ChangedProperties.Contains(TouchpadMouse.PropertyKeyStrings.VERTICAL_DEAD_ZONE);
            }

            public double TrackpadAngleSnapDegrees
            {
                get => touchMouseAct.TrackpadAngleSnapDegrees;
                set
                {
                    touchMouseAct.TrackpadAngleSnapDegrees = Math.Clamp(value, 0.0, 45.0);
                    TrackpadAngleSnapDegreesChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler TrackpadAngleSnapDegreesChanged;
            public bool ShouldSerializeTrackpadAngleSnapDegrees()
            {
                return touchMouseAct.ChangedProperties.Contains(TouchpadMouse.PropertyKeyStrings.ANGLE_SNAP_DEGREES);
            }

            public bool TrackpadSmoothAngleSnap
            {
                get => touchMouseAct.TrackpadSmoothAngleSnap;
                set
                {
                    touchMouseAct.TrackpadSmoothAngleSnap = value;
                    TrackpadSmoothAngleSnapChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler TrackpadSmoothAngleSnapChanged;
            public bool ShouldSerializeTrackpadSmoothAngleSnap()
            {
                return touchMouseAct.ChangedProperties.Contains(TouchpadMouse.PropertyKeyStrings.SMOOTH_ANGLE_SNAP);
            }

            public bool TrackballEnabled
            {
                get => touchMouseAct.TrackballEnabled;
                set
                {
                    touchMouseAct.TrackballEnabled = value;
                    TrackballEnabledChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler TrackballEnabledChanged;
            public bool ShouldSerializeTrackballEnabled()
            {
                return touchMouseAct.ChangedProperties.Contains(TouchpadMouse.PropertyKeyStrings.TRACKBALL_MODE);
            }

            public int TrackballFriction
            {
                get => touchMouseAct.TrackballFriction;
                set
                {
                    touchMouseAct.TrackballFriction = value;
                    TrackballFrictionChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler TrackballFrictionChanged;
            public bool ShouldSerializeTrackballFriction()
            {
                return touchMouseAct.ChangedProperties.Contains(TouchpadMouse.PropertyKeyStrings.TRACKBALL_FRICTION);
            }

            public double SwipesPer360
            {
                get => touchMouseAct.SwipesPer360;
                set
                {
                    touchMouseAct.SwipesPer360 = Math.Clamp(value, 0.0, 100.0);
                    SwipesPer360Changed?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler SwipesPer360Changed;
            public bool ShouldSerializeSwipesPer360()
            {
                return touchMouseAct.ChangedProperties.Contains(TouchpadMouse.PropertyKeyStrings.SWIPES_PER_360);
            }

            public double VerticalScale
            {
                get => touchMouseAct.VerticalScale;
                set
                {
                    touchMouseAct.VerticalScale = Math.Clamp(value, 0.0, 10.0);
                    VerticalScaleChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler VerticalScaleChanged;
            public bool ShouldSerializeVerticalScale()
            {
                return touchMouseAct.ChangedProperties.Contains(TouchpadMouse.PropertyKeyStrings.VERTICAL_SCALE);
            }

            public bool SmoothingEnabled
            {
                get => touchMouseAct.SmoothingEnabled;
                set
                {
                    touchMouseAct.SmoothingEnabled = value;
                    SmoothingEnabledChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler SmoothingEnabledChanged;
            public bool ShouldSerializeSmoothingEnabled()
            {
                return touchMouseAct.ChangedProperties.Contains(TouchpadMouse.PropertyKeyStrings.SMOOTHING_ENABLED);
            }

            public double SmoothingMinCutoff
            {
                get => touchMouseAct.ActionSmoothingSettings.minCutOff;
                set
                {
                    touchMouseAct.ActionSmoothingSettings.minCutOff = Math.Clamp(value, 0.0, 10.0);
                    touchMouseAct.ActionSmoothingSettings.UpdateSmoothingFilters();
                    SmoothingMinCutoffChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler SmoothingMinCutoffChanged;
            public bool ShouldSerializeSmoothingMinCutoff()
            {
                return touchMouseAct.ChangedProperties.Contains(TouchpadMouse.PropertyKeyStrings.SMOOTHING_FILTER);
            }

            public double SmoothingBeta
            {
                get => touchMouseAct.ActionSmoothingSettings.beta;
                set
                {
                    touchMouseAct.ActionSmoothingSettings.beta = Math.Clamp(value, 0.0, 1.0);
                    touchMouseAct.ActionSmoothingSettings.UpdateSmoothingFilters();
                    SmoothingBetaChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler SmoothingBetaChanged;
            public bool ShouldSerializeSmoothingBeta()
            {
                return touchMouseAct.ChangedProperties.Contains(TouchpadMouse.PropertyKeyStrings.SMOOTHING_FILTER);
            }

            public GyroMouseAccelCurveChoice AccelCurve
            {
                get => touchMouseAct.AccelCurve;
                set
                {
                    touchMouseAct.AccelCurve = value;
                    AccelCurveChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler AccelCurveChanged;
            public bool ShouldSerializeAccelCurve() =>
                touchMouseAct.ChangedProperties.Contains(
                    TouchpadMouse.PropertyKeyStrings.ACCEL_CURVE);

            public double MinAccelXSens
            {
                get => touchMouseAct.MinAccelXSens;
                set
                {
                    touchMouseAct.MinAccelXSens = value;
                    MinAccelXSensChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler MinAccelXSensChanged;
            public bool ShouldSerializeMinAccelXSens() =>
                touchMouseAct.ChangedProperties.Contains(
                    TouchpadMouse.PropertyKeyStrings.MIN_ACCEL_X_SENS);

            public double MaxAccelXSens
            {
                get => touchMouseAct.MaxAccelXSens;
                set
                {
                    touchMouseAct.MaxAccelXSens = value;
                    MaxAccelXSensChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler MaxAccelXSensChanged;
            public bool ShouldSerializeMaxAccelXSens() =>
                touchMouseAct.ChangedProperties.Contains(
                    TouchpadMouse.PropertyKeyStrings.MAX_ACCEL_X_SENS);

            public double MinAccelYSens
            {
                get => touchMouseAct.MinAccelYSens;
                set
                {
                    touchMouseAct.MinAccelYSens = value;
                    MinAccelYSensChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler MinAccelYSensChanged;
            public bool ShouldSerializeMinAccelYSens() =>
                touchMouseAct.ChangedProperties.Contains(
                    TouchpadMouse.PropertyKeyStrings.MIN_ACCEL_Y_SENS);

            public double MaxAccelYSens
            {
                get => touchMouseAct.MaxAccelYSens;
                set
                {
                    touchMouseAct.MaxAccelYSens = value;
                    MaxAccelYSensChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler MaxAccelYSensChanged;
            public bool ShouldSerializeMaxAccelYSens() =>
                touchMouseAct.ChangedProperties.Contains(
                    TouchpadMouse.PropertyKeyStrings.MAX_ACCEL_Y_SENS);

            public double MinAccelThreshold
            {
                get => touchMouseAct.MinAccelThreshold;
                set
                {
                    touchMouseAct.MinAccelThreshold = value;
                    MinAccelThresholdChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler MinAccelThresholdChanged;
            public bool ShouldSerializeMinAccelThreshold() =>
                touchMouseAct.ChangedProperties.Contains(
                    TouchpadMouse.PropertyKeyStrings.MIN_ACCEL_THRESHOLD);

            public double MaxAccelThreshold
            {
                get => touchMouseAct.MaxAccelThreshold;
                set
                {
                    touchMouseAct.MaxAccelThreshold = value;
                    MaxAccelThresholdChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler MaxAccelThresholdChanged;
            public bool ShouldSerializeMaxAccelThreshold() =>
                touchMouseAct.ChangedProperties.Contains(
                    TouchpadMouse.PropertyKeyStrings.MAX_ACCEL_THRESHOLD);

            public double NaturalVHalf
            {
                get => touchMouseAct.NaturalVHalf;
                set
                {
                    touchMouseAct.NaturalVHalf = value;
                    NaturalVHalfChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler NaturalVHalfChanged;
            public bool ShouldSerializeNaturalVHalf() =>
                touchMouseAct.ChangedProperties.Contains(
                    TouchpadMouse.PropertyKeyStrings.NATURAL_CURVE_VHALF);

            public double PowerVRef
            {
                get => touchMouseAct.PowerVRef;
                set
                {
                    touchMouseAct.PowerVRef = value;
                    PowerVRefChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler PowerVRefChanged;
            public bool ShouldSerializePowerVRef() =>
                touchMouseAct.ChangedProperties.Contains(
                    TouchpadMouse.PropertyKeyStrings.POWER_CURVE_VREF);

            public double PowerExponent
            {
                get => touchMouseAct.PowerExponent;
                set
                {
                    touchMouseAct.PowerExponent = value;
                    PowerExponentChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler PowerExponentChanged;
            public bool ShouldSerializePowerExponent() =>
                touchMouseAct.ChangedProperties.Contains(
                    TouchpadMouse.PropertyKeyStrings.POWER_CURVE_EXPONENT);

            // Stability filter settings. One shared event carries the
            // property group key so the serializer can mark ChangedProperties
            // on deserialization. StabilityMode is declared first so a saved
            // preset is applied before any custom values override it
            public event EventHandler<string> StabilityGroupChanged;

            private bool ShouldSerializeStabilityGroup(string key) =>
                touchMouseAct.ChangedProperties.Contains(key);

            public TouchpadStabilityMode StabilityMode
            {
                get => touchMouseAct.StabilitySettings.Mode;
                set
                {
                    touchMouseAct.StabilitySettings.ApplyPreset(value);
                    StabilityGroupChanged?.Invoke(this,
                        TouchpadMouse.PropertyKeyStrings.STABILITY_MODE);
                }
            }
            public bool ShouldSerializeStabilityMode() =>
                ShouldSerializeStabilityGroup(
                    TouchpadMouse.PropertyKeyStrings.STABILITY_MODE);

            public double StabilityTouchSettleMs
            {
                get => touchMouseAct.StabilitySettings.TouchSettleMs;
                set
                {
                    touchMouseAct.StabilitySettings.TouchSettleMs =
                        Math.Clamp(value, 0.0, 30.0);
                    StabilityGroupChanged?.Invoke(this,
                        TouchpadMouse.PropertyKeyStrings.STABILITY_TOUCH_SETTLE);
                }
            }
            public bool ShouldSerializeStabilityTouchSettleMs() =>
                ShouldSerializeStabilityGroup(
                    TouchpadMouse.PropertyKeyStrings.STABILITY_TOUCH_SETTLE);

            public double StabilityBaseNoiseFloor
            {
                get => touchMouseAct.StabilitySettings.BaseNoiseFloor;
                set
                {
                    touchMouseAct.StabilitySettings.BaseNoiseFloor =
                        Math.Clamp(value, 0.0, 100.0);
                    StabilityGroupChanged?.Invoke(this,
                        TouchpadMouse.PropertyKeyStrings.STABILITY_NOISE);
                }
            }
            public bool ShouldSerializeStabilityBaseNoiseFloor() =>
                ShouldSerializeStabilityGroup(
                    TouchpadMouse.PropertyKeyStrings.STABILITY_NOISE);

            public double StabilityHysteresisExitMultiplier
            {
                get => touchMouseAct.StabilitySettings.HysteresisExitMultiplier;
                set
                {
                    touchMouseAct.StabilitySettings.HysteresisExitMultiplier =
                        Math.Clamp(value, 1.0, 3.0);
                    StabilityGroupChanged?.Invoke(this,
                        TouchpadMouse.PropertyKeyStrings.STABILITY_NOISE);
                }
            }
            public bool ShouldSerializeStabilityHysteresisExitMultiplier() =>
                ShouldSerializeStabilityGroup(
                    TouchpadMouse.PropertyKeyStrings.STABILITY_NOISE);

            public double StabilityFastPassthroughThreshold
            {
                get => touchMouseAct.StabilitySettings.FastPassthroughThreshold;
                set
                {
                    touchMouseAct.StabilitySettings.FastPassthroughThreshold =
                        Math.Clamp(value, 0.0, 200.0);
                    StabilityGroupChanged?.Invoke(this,
                        TouchpadMouse.PropertyKeyStrings.STABILITY_NOISE);
                }
            }
            public bool ShouldSerializeStabilityFastPassthroughThreshold() =>
                ShouldSerializeStabilityGroup(
                    TouchpadMouse.PropertyKeyStrings.STABILITY_NOISE);

            public bool StabilityEdgeGuardEnabled
            {
                get => touchMouseAct.StabilitySettings.EdgeGuardEnabled;
                set
                {
                    touchMouseAct.StabilitySettings.EdgeGuardEnabled = value;
                    StabilityGroupChanged?.Invoke(this,
                        TouchpadMouse.PropertyKeyStrings.STABILITY_EDGE_GUARD);
                }
            }
            public bool ShouldSerializeStabilityEdgeGuardEnabled() =>
                ShouldSerializeStabilityGroup(
                    TouchpadMouse.PropertyKeyStrings.STABILITY_EDGE_GUARD);

            public double StabilityLeftEdgePercent
            {
                get => touchMouseAct.StabilitySettings.LeftEdgePercent;
                set
                {
                    touchMouseAct.StabilitySettings.LeftEdgePercent =
                        Math.Clamp(value, 0.0, 30.0);
                    StabilityGroupChanged?.Invoke(this,
                        TouchpadMouse.PropertyKeyStrings.STABILITY_EDGE_GUARD);
                }
            }
            public bool ShouldSerializeStabilityLeftEdgePercent() =>
                ShouldSerializeStabilityGroup(
                    TouchpadMouse.PropertyKeyStrings.STABILITY_EDGE_GUARD);

            public double StabilityRightEdgePercent
            {
                get => touchMouseAct.StabilitySettings.RightEdgePercent;
                set
                {
                    touchMouseAct.StabilitySettings.RightEdgePercent =
                        Math.Clamp(value, 0.0, 30.0);
                    StabilityGroupChanged?.Invoke(this,
                        TouchpadMouse.PropertyKeyStrings.STABILITY_EDGE_GUARD);
                }
            }
            public bool ShouldSerializeStabilityRightEdgePercent() =>
                ShouldSerializeStabilityGroup(
                    TouchpadMouse.PropertyKeyStrings.STABILITY_EDGE_GUARD);

            public double StabilityTopEdgePercent
            {
                get => touchMouseAct.StabilitySettings.TopEdgePercent;
                set
                {
                    touchMouseAct.StabilitySettings.TopEdgePercent =
                        Math.Clamp(value, 0.0, 30.0);
                    StabilityGroupChanged?.Invoke(this,
                        TouchpadMouse.PropertyKeyStrings.STABILITY_EDGE_GUARD);
                }
            }
            public bool ShouldSerializeStabilityTopEdgePercent() =>
                ShouldSerializeStabilityGroup(
                    TouchpadMouse.PropertyKeyStrings.STABILITY_EDGE_GUARD);

            public double StabilityBottomEdgePercent
            {
                get => touchMouseAct.StabilitySettings.BottomEdgePercent;
                set
                {
                    touchMouseAct.StabilitySettings.BottomEdgePercent =
                        Math.Clamp(value, 0.0, 30.0);
                    StabilityGroupChanged?.Invoke(this,
                        TouchpadMouse.PropertyKeyStrings.STABILITY_EDGE_GUARD);
                }
            }
            public bool ShouldSerializeStabilityBottomEdgePercent() =>
                ShouldSerializeStabilityGroup(
                    TouchpadMouse.PropertyKeyStrings.STABILITY_EDGE_GUARD);

            public double StabilityEdgeJitterMultiplier
            {
                get => touchMouseAct.StabilitySettings.EdgeJitterMultiplier;
                set
                {
                    touchMouseAct.StabilitySettings.EdgeJitterMultiplier =
                        Math.Clamp(value, 1.0, 5.0);
                    StabilityGroupChanged?.Invoke(this,
                        TouchpadMouse.PropertyKeyStrings.STABILITY_EDGE_GUARD);
                }
            }
            public bool ShouldSerializeStabilityEdgeJitterMultiplier() =>
                ShouldSerializeStabilityGroup(
                    TouchpadMouse.PropertyKeyStrings.STABILITY_EDGE_GUARD);

            public double StabilityCornerJitterMultiplier
            {
                get => touchMouseAct.StabilitySettings.CornerJitterMultiplier;
                set
                {
                    touchMouseAct.StabilitySettings.CornerJitterMultiplier =
                        Math.Clamp(value, 1.0, 5.0);
                    StabilityGroupChanged?.Invoke(this,
                        TouchpadMouse.PropertyKeyStrings.STABILITY_EDGE_GUARD);
                }
            }
            public bool ShouldSerializeStabilityCornerJitterMultiplier() =>
                ShouldSerializeStabilityGroup(
                    TouchpadMouse.PropertyKeyStrings.STABILITY_EDGE_GUARD);

            public double StabilityTopLeftCornerMultiplier
            {
                get => touchMouseAct.StabilitySettings.TopLeftCornerMultiplier;
                set
                {
                    touchMouseAct.StabilitySettings.TopLeftCornerMultiplier =
                        Math.Clamp(value, 1.0, 6.0);
                    StabilityGroupChanged?.Invoke(this,
                        TouchpadMouse.PropertyKeyStrings.STABILITY_EDGE_GUARD);
                }
            }
            public bool ShouldSerializeStabilityTopLeftCornerMultiplier() =>
                ShouldSerializeStabilityGroup(
                    TouchpadMouse.PropertyKeyStrings.STABILITY_EDGE_GUARD);

            public double StabilityEdgeHysteresisPercent
            {
                get => touchMouseAct.StabilitySettings.EdgeHysteresisPercent;
                set
                {
                    touchMouseAct.StabilitySettings.EdgeHysteresisPercent =
                        Math.Clamp(value, 0.0, 10.0);
                    StabilityGroupChanged?.Invoke(this,
                        TouchpadMouse.PropertyKeyStrings.STABILITY_EDGE_GUARD);
                }
            }
            public bool ShouldSerializeStabilityEdgeHysteresisPercent() =>
                ShouldSerializeStabilityGroup(
                    TouchpadMouse.PropertyKeyStrings.STABILITY_EDGE_GUARD);

            public bool StabilityEdgeLockEnabled
            {
                get => touchMouseAct.StabilitySettings.EdgeLockEnabled;
                set
                {
                    touchMouseAct.StabilitySettings.EdgeLockEnabled = value;
                    StabilityGroupChanged?.Invoke(this,
                        TouchpadMouse.PropertyKeyStrings.STABILITY_EDGE_GUARD);
                }
            }
            public bool ShouldSerializeStabilityEdgeLockEnabled() =>
                ShouldSerializeStabilityGroup(
                    TouchpadMouse.PropertyKeyStrings.STABILITY_EDGE_GUARD);

            public bool StabilityEdgeStartGateEnabled
            {
                get => touchMouseAct.StabilitySettings.EdgeStartGateEnabled;
                set
                {
                    touchMouseAct.StabilitySettings.EdgeStartGateEnabled = value;
                    StabilityGroupChanged?.Invoke(this,
                        TouchpadMouse.PropertyKeyStrings.STABILITY_EDGE_START_GATE);
                }
            }
            public bool ShouldSerializeStabilityEdgeStartGateEnabled() =>
                ShouldSerializeStabilityGroup(
                    TouchpadMouse.PropertyKeyStrings.STABILITY_EDGE_START_GATE);

            public double StabilityEdgeStartThreshold
            {
                get => touchMouseAct.StabilitySettings.EdgeStartThreshold;
                set
                {
                    touchMouseAct.StabilitySettings.EdgeStartThreshold =
                        Math.Clamp(value, 0.0, 300.0);
                    StabilityGroupChanged?.Invoke(this,
                        TouchpadMouse.PropertyKeyStrings.STABILITY_EDGE_START_GATE);
                }
            }
            public bool ShouldSerializeStabilityEdgeStartThreshold() =>
                ShouldSerializeStabilityGroup(
                    TouchpadMouse.PropertyKeyStrings.STABILITY_EDGE_START_GATE);

            public bool StabilityStationaryHoldEnabled
            {
                get => touchMouseAct.StabilitySettings.StationaryHoldEnabled;
                set
                {
                    touchMouseAct.StabilitySettings.StationaryHoldEnabled = value;
                    StabilityGroupChanged?.Invoke(this,
                        TouchpadMouse.PropertyKeyStrings.STABILITY_STATIONARY);
                }
            }
            public bool ShouldSerializeStabilityStationaryHoldEnabled() =>
                ShouldSerializeStabilityGroup(
                    TouchpadMouse.PropertyKeyStrings.STABILITY_STATIONARY);

            public double StabilityStationaryDetectionMs
            {
                get => touchMouseAct.StabilitySettings.StationaryDetectionMs;
                set
                {
                    touchMouseAct.StabilitySettings.StationaryDetectionMs =
                        Math.Clamp(value, 0.0, 200.0);
                    StabilityGroupChanged?.Invoke(this,
                        TouchpadMouse.PropertyKeyStrings.STABILITY_STATIONARY);
                }
            }
            public bool ShouldSerializeStabilityStationaryDetectionMs() =>
                ShouldSerializeStabilityGroup(
                    TouchpadMouse.PropertyKeyStrings.STABILITY_STATIONARY);

            public double StabilityStationaryNoiseMultiplier
            {
                get => touchMouseAct.StabilitySettings.StationaryNoiseMultiplier;
                set
                {
                    touchMouseAct.StabilitySettings.StationaryNoiseMultiplier =
                        Math.Clamp(value, 1.0, 4.0);
                    StabilityGroupChanged?.Invoke(this,
                        TouchpadMouse.PropertyKeyStrings.STABILITY_STATIONARY);
                }
            }
            public bool ShouldSerializeStabilityStationaryNoiseMultiplier() =>
                ShouldSerializeStabilityGroup(
                    TouchpadMouse.PropertyKeyStrings.STABILITY_STATIONARY);

            public double StabilityStationaryBreakoutThreshold
            {
                get => touchMouseAct.StabilitySettings.StationaryBreakoutThreshold;
                set
                {
                    touchMouseAct.StabilitySettings.StationaryBreakoutThreshold =
                        Math.Clamp(value, 0.0, 200.0);
                    StabilityGroupChanged?.Invoke(this,
                        TouchpadMouse.PropertyKeyStrings.STABILITY_STATIONARY);
                }
            }
            public bool ShouldSerializeStabilityStationaryBreakoutThreshold() =>
                ShouldSerializeStabilityGroup(
                    TouchpadMouse.PropertyKeyStrings.STABILITY_STATIONARY);

            public bool StabilityDeltaClampEnabled
            {
                get => touchMouseAct.StabilitySettings.DeltaClampEnabled;
                set
                {
                    touchMouseAct.StabilitySettings.DeltaClampEnabled = value;
                    StabilityGroupChanged?.Invoke(this,
                        TouchpadMouse.PropertyKeyStrings.STABILITY_DELTA_CLAMP);
                }
            }
            public bool ShouldSerializeStabilityDeltaClampEnabled() =>
                ShouldSerializeStabilityGroup(
                    TouchpadMouse.PropertyKeyStrings.STABILITY_DELTA_CLAMP);

            public double StabilityMaxDeltaPerFrame
            {
                get => touchMouseAct.StabilitySettings.MaxDeltaPerFrame;
                set
                {
                    touchMouseAct.StabilitySettings.MaxDeltaPerFrame =
                        Math.Clamp(value, 10.0, 500.0);
                    StabilityGroupChanged?.Invoke(this,
                        TouchpadMouse.PropertyKeyStrings.STABILITY_DELTA_CLAMP);
                }
            }
            public bool ShouldSerializeStabilityMaxDeltaPerFrame() =>
                ShouldSerializeStabilityGroup(
                    TouchpadMouse.PropertyKeyStrings.STABILITY_DELTA_CLAMP);

            public TouchpadMouseSettings(TouchpadMouse action)
            {
                touchMouseAct = action;
            }
        }

        private TouchpadMouse touchMouseAction = new TouchpadMouse();

        private TouchpadMouseSettings settings;
        public TouchpadMouseSettings Settings
        {
            get => settings;
            set => settings = value;
        }
        public bool ShouldSerializeSettings()
        {
            return touchMouseAction.ChangedProperties.Count > 0;
        }

        // Deserialize
        public TouchpadMouseSerializer() : base()
        {
            mapAction = touchMouseAction;
            settings = new TouchpadMouseSettings(touchMouseAction);

            NameChanged += TouchpadMouseSerializer_NameChanged;
            settings.DeadZoneChanged += Settings_DeadZoneChanged;
            settings.VerticalDeadZoneChanged += Settings_VerticalDeadZoneChanged;
            settings.TrackpadAngleSnapDegreesChanged += Settings_TrackpadAngleSnapDegreesChanged;
            settings.TrackpadSmoothAngleSnapChanged += Settings_TrackpadSmoothAngleSnapChanged;
            settings.TrackballEnabledChanged += Settings_TrackballEnabledChanged;
            settings.TrackballFrictionChanged += Settings_TrackballFrictionChanged;
            settings.SwipesPer360Changed += Settings_SwipesPer360Changed;
            settings.VerticalScaleChanged += Settings_VerticalScaleChanged;
            settings.SmoothingEnabledChanged += Settings_SmoothingEnabledChanged;
            settings.SmoothingMinCutoffChanged += Settings_SmoothingMinCutoffChanged;
            settings.SmoothingBetaChanged += Settings_SmoothingBetaChanged;
            settings.AccelCurveChanged += Settings_AccelCurveChanged;
            settings.MinAccelXSensChanged += Settings_MinAccelXSensChanged;
            settings.MaxAccelXSensChanged += Settings_MaxAccelXSensChanged;
            settings.MinAccelYSensChanged += Settings_MinAccelYSensChanged;
            settings.MaxAccelYSensChanged += Settings_MaxAccelYSensChanged;
            settings.MinAccelThresholdChanged += Settings_MinAccelThresholdChanged;
            settings.MaxAccelThresholdChanged += Settings_MaxAccelThresholdChanged;
            settings.NaturalVHalfChanged += Settings_NaturalVHalfChanged;
            settings.PowerVRefChanged += Settings_PowerVRefChanged;
            settings.PowerExponentChanged += Settings_PowerExponentChanged;
            settings.StabilityGroupChanged += Settings_StabilityGroupChanged;
        }

        private void Settings_StabilityGroupChanged(object sender, string propertyGroup)
        {
            touchMouseAction.ChangedProperties.Add(propertyGroup);
        }

        private void Settings_AccelCurveChanged(object sender, EventArgs e) =>
            touchMouseAction.ChangedProperties.Add(
                TouchpadMouse.PropertyKeyStrings.ACCEL_CURVE);
        private void Settings_MinAccelXSensChanged(object sender, EventArgs e) =>
            touchMouseAction.ChangedProperties.Add(
                TouchpadMouse.PropertyKeyStrings.MIN_ACCEL_X_SENS);
        private void Settings_MaxAccelXSensChanged(object sender, EventArgs e) =>
            touchMouseAction.ChangedProperties.Add(
                TouchpadMouse.PropertyKeyStrings.MAX_ACCEL_X_SENS);
        private void Settings_MinAccelYSensChanged(object sender, EventArgs e) =>
            touchMouseAction.ChangedProperties.Add(
                TouchpadMouse.PropertyKeyStrings.MIN_ACCEL_Y_SENS);
        private void Settings_MaxAccelYSensChanged(object sender, EventArgs e) =>
            touchMouseAction.ChangedProperties.Add(
                TouchpadMouse.PropertyKeyStrings.MAX_ACCEL_Y_SENS);
        private void Settings_MinAccelThresholdChanged(object sender, EventArgs e) =>
            touchMouseAction.ChangedProperties.Add(
                TouchpadMouse.PropertyKeyStrings.MIN_ACCEL_THRESHOLD);
        private void Settings_MaxAccelThresholdChanged(object sender, EventArgs e) =>
            touchMouseAction.ChangedProperties.Add(
                TouchpadMouse.PropertyKeyStrings.MAX_ACCEL_THRESHOLD);
        private void Settings_NaturalVHalfChanged(object sender, EventArgs e) =>
            touchMouseAction.ChangedProperties.Add(
                TouchpadMouse.PropertyKeyStrings.NATURAL_CURVE_VHALF);
        private void Settings_PowerVRefChanged(object sender, EventArgs e) =>
            touchMouseAction.ChangedProperties.Add(
                TouchpadMouse.PropertyKeyStrings.POWER_CURVE_VREF);
        private void Settings_PowerExponentChanged(object sender, EventArgs e) =>
            touchMouseAction.ChangedProperties.Add(
                TouchpadMouse.PropertyKeyStrings.POWER_CURVE_EXPONENT);

        private void Settings_SmoothingBetaChanged(object sender, EventArgs e)
        {
            touchMouseAction.ChangedProperties.Add(TouchpadMouse.PropertyKeyStrings.SMOOTHING_FILTER);
        }

        private void Settings_TrackpadAngleSnapDegreesChanged(object sender, EventArgs e)
        {
            touchMouseAction.ChangedProperties.Add(TouchpadMouse.PropertyKeyStrings.ANGLE_SNAP_DEGREES);
        }

        private void Settings_TrackpadSmoothAngleSnapChanged(object sender, EventArgs e)
        {
            touchMouseAction.ChangedProperties.Add(TouchpadMouse.PropertyKeyStrings.SMOOTH_ANGLE_SNAP);
        }

        private void Settings_SmoothingMinCutoffChanged(object sender, EventArgs e)
        {
            touchMouseAction.ChangedProperties.Add(TouchpadMouse.PropertyKeyStrings.SMOOTHING_FILTER);
        }

        private void Settings_SmoothingEnabledChanged(object sender, EventArgs e)
        {
            touchMouseAction.ChangedProperties.Add(TouchpadMouse.PropertyKeyStrings.SMOOTHING_ENABLED);
        }

        private void Settings_VerticalScaleChanged(object sender, EventArgs e)
        {
            touchMouseAction.ChangedProperties.Add(TouchpadMouse.PropertyKeyStrings.VERTICAL_SCALE);
        }

        private void Settings_SwipesPer360Changed(object sender, EventArgs e)
        {
            touchMouseAction.ChangedProperties.Add(TouchpadMouse.PropertyKeyStrings.SWIPES_PER_360);
        }

        private void Settings_TrackballFrictionChanged(object sender, EventArgs e)
        {
            touchMouseAction.ChangedProperties.Add(TouchpadMouse.PropertyKeyStrings.TRACKBALL_FRICTION);
        }

        private void Settings_TrackballEnabledChanged(object sender, EventArgs e)
        {
            touchMouseAction.ChangedProperties.Add(TouchpadMouse.PropertyKeyStrings.TRACKBALL_MODE);
        }

        private void Settings_DeadZoneChanged(object sender, EventArgs e)
        {
            touchMouseAction.ChangedProperties.Add(TouchpadMouse.PropertyKeyStrings.DEAD_ZONE);
        }

        private void Settings_VerticalDeadZoneChanged(object sender, EventArgs e)
        {
            touchMouseAction.ChangedProperties.Add(TouchpadMouse.PropertyKeyStrings.VERTICAL_DEAD_ZONE);
        }

        private void TouchpadMouseSerializer_NameChanged(object sender, EventArgs e)
        {
            touchMouseAction.ChangedProperties.Add(TouchpadMouse.PropertyKeyStrings.NAME);
        }

        // Pre-serialize
        public TouchpadMouseSerializer(ActionLayer tempLayer, MapAction mapAction) :
            base(tempLayer, mapAction)
        {
            if (mapAction is TouchpadMouse temp)
            {
                touchMouseAction = temp;
                this.mapAction = touchMouseAction;
                settings = new TouchpadMouseSettings(touchMouseAction);
            }
        }
    }

    public class TouchpadMouseJoystickSerializer : MapActionSerializer
    {
        public class TouchpadMouseJoystickSettings
        {
            private TouchpadMouseJoystick touchMouseJoyAction;

            public int DeadZone
            {
                get => touchMouseJoyAction.MStickParams.deadZone;
                set
                {
                    touchMouseJoyAction.MStickParams.deadZone = value;
                    DeadZoneChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler DeadZoneChanged;
            public bool ShouldSerializeDeadZone()
            {
                return touchMouseJoyAction.ChangedProperties.Contains(TouchpadMouseJoystick.PropertyKeyStrings.DEAD_ZONE);
            }

            public int MaxZone
            {
                get => touchMouseJoyAction.MStickParams.maxZone;
                set
                {
                    touchMouseJoyAction.MStickParams.maxZone = value;
                    MaxZoneChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler MaxZoneChanged;
            public bool ShouldSerializeMaxZone()
            {
                return touchMouseJoyAction.ChangedProperties.Contains(TouchpadMouseJoystick.PropertyKeyStrings.MAX_ZONE);
            }

            public double AntiDeadZoneX
            {
                get => touchMouseJoyAction.MStickParams.antiDeadzoneX;
                set
                {
                    touchMouseJoyAction.MStickParams.antiDeadzoneX = value;
                    AntiDeadZoneXChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler AntiDeadZoneXChanged;
            public bool ShouldSerializeAntiDeadZoneX()
            {
                return touchMouseJoyAction.ChangedProperties.Contains(TouchpadMouseJoystick.PropertyKeyStrings.ANTIDEAD_ZONE_X);
            }

            public double AntiDeadZoneY
            {
                get => touchMouseJoyAction.MStickParams.antiDeadzoneY;
                set
                {
                    touchMouseJoyAction.MStickParams.antiDeadzoneY = value;
                    AntiDeadZoneYChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler AntiDeadZoneYChanged;
            public bool ShouldSerializeAntiDeadZoneY()
            {
                return touchMouseJoyAction.ChangedProperties.Contains(TouchpadMouseJoystick.PropertyKeyStrings.ANTIDEAD_ZONE_Y);
            }

            public bool JitterCompensation
            {
                get => touchMouseJoyAction.MStickParams.jitterCompensation;
                set
                {
                    touchMouseJoyAction.MStickParams.jitterCompensation = value;
                    JitterCompensationChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler JitterCompensationChanged;
            public bool ShouldSerializeJitterCompensation()
            {
                return touchMouseJoyAction.ChangedProperties.Contains(TouchpadMouseJoystick.PropertyKeyStrings.JITTER_COMPENSATION);
            }

            //public StickActionCodes OutputStick
            public string OutputStick
            {
                get => StickCodeHelper.Convert(touchMouseJoyAction.MStickParams.OutputStick);
                set
                {
                    if (Enum.TryParse(value, out StickActionCodes temp))
                    {
                        touchMouseJoyAction.MStickParams.OutputStick = temp;
                        OutputStickChanged?.Invoke(this, EventArgs.Empty);
                    }
                }
            }
            public event EventHandler OutputStickChanged;
            public bool ShouldSerializeOutputStick()
            {
                return touchMouseJoyAction.ChangedProperties.Contains(TouchpadMouseJoystick.PropertyKeyStrings.OUTPUT_STICK);
            }

            public StickOutCurve.Curve OutputCurve
            {
                get => touchMouseJoyAction.MStickParams.outputCurve;
                set
                {
                    touchMouseJoyAction.MStickParams.outputCurve = value;
                    OutputCurveChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler OutputCurveChanged;
            public bool ShouldSerializeOutputCurve()
            {
                return touchMouseJoyAction.ChangedProperties.Contains(TouchpadMouseJoystick.PropertyKeyStrings.OUTPUT_CURVE);
            }

            public int Rotation
            {
                get => touchMouseJoyAction.MStickParams.rotation;
                set
                {
                    touchMouseJoyAction.MStickParams.rotation = Math.Clamp(value, -180, 180);
                    RotationChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler RotationChanged;
            public bool ShouldSerializeRotation()
            {
                return touchMouseJoyAction.ChangedProperties.Contains(TouchpadMouseJoystick.PropertyKeyStrings.ROTATION);
            }

            public bool Trackball
            {
                get => touchMouseJoyAction.MStickParams.trackballEnabled;
                set
                {
                    touchMouseJoyAction.MStickParams.trackballEnabled = value;
                    TrackballChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler TrackballChanged;
            public bool ShouldSerializeTrackball()
            {
                return touchMouseJoyAction.ChangedProperties.Contains(TouchpadMouseJoystick.PropertyKeyStrings.TRACKBALL_MODE);
            }

            public int TrackballFriction
            {
                get => touchMouseJoyAction.MStickParams.trackballFriction;
                set
                {
                    touchMouseJoyAction.MStickParams.trackballFriction = value;
                    TrackballFrictionChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler TrackballFrictionChanged;
            public bool ShouldSerializeTrackballFriction()
            {
                return touchMouseJoyAction.ChangedProperties.Contains(TouchpadMouseJoystick.PropertyKeyStrings.TRACKBALL_FRICTION);
            }

            public bool InvertX
            {
                get => touchMouseJoyAction.MStickParams.invertX;
                set
                {
                    touchMouseJoyAction.MStickParams.invertX = value;
                    InvertXChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler InvertXChanged;
            public bool ShouldSerializeInvertX()
            {
                return touchMouseJoyAction.ChangedProperties.Contains(TouchpadMouseJoystick.PropertyKeyStrings.INVERT_X);
            }

            public bool InvertY
            {
                get => touchMouseJoyAction.MStickParams.invertY;
                set
                {
                    touchMouseJoyAction.MStickParams.invertY = value;
                    InvertYChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler InvertYChanged;
            public bool ShouldSerializeInvertY()
            {
                return touchMouseJoyAction.ChangedProperties.Contains(TouchpadMouseJoystick.PropertyKeyStrings.INVERT_Y);
            }

            public double VerticalScale
            {
                get => touchMouseJoyAction.MStickParams.verticalScale;
                set
                {
                    touchMouseJoyAction.MStickParams.verticalScale = Math.Clamp(value, 0.0, 10.0);
                    VerticalScaleChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler VerticalScaleChanged;
            public bool ShouldSerializeVerticalScale()
            {
                return touchMouseJoyAction.ChangedProperties.Contains(TouchpadMouseJoystick.PropertyKeyStrings.VERTICAL_SCALE);
            }

            public bool SmoothingEnabled
            {
                get => touchMouseJoyAction.MStickParams.smoothing;
                set
                {
                    touchMouseJoyAction.MStickParams.smoothing = value;
                    SmoothingEnabledChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler SmoothingEnabledChanged;
            public bool ShouldSerializeSmoothingEnabled()
            {
                return touchMouseJoyAction.ChangedProperties.Contains(TouchpadMouseJoystick.PropertyKeyStrings.SMOOTHING_ENABLED);
            }

            public double SmoothingMinCutoff
            {
                get => touchMouseJoyAction.MStickParams.smoothingFilterSettings.minCutOff;
                set
                {
                    touchMouseJoyAction.MStickParams.smoothingFilterSettings.minCutOff = Math.Clamp(value, 0.0, 10.0);
                    touchMouseJoyAction.MStickParams.smoothingFilterSettings.UpdateSmoothingFilters();
                    SmoothingMinCutoffChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler SmoothingMinCutoffChanged;
            public bool ShouldSerializeSmoothingMinCutoff()
            {
                return touchMouseJoyAction.ChangedProperties.Contains(TouchpadMouseJoystick.PropertyKeyStrings.SMOOTHING_FILTER);
            }

            public double SmoothingBeta
            {
                get => touchMouseJoyAction.MStickParams.smoothingFilterSettings.beta;
                set
                {
                    touchMouseJoyAction.MStickParams.smoothingFilterSettings.beta = Math.Clamp(value, 0.0, 1.0);
                    touchMouseJoyAction.MStickParams.smoothingFilterSettings.UpdateSmoothingFilters();
                    SmoothingBetaChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler SmoothingBetaChanged;
            public bool ShouldSerializeSmoothingBeta()
            {
                return touchMouseJoyAction.ChangedProperties.Contains(TouchpadMouseJoystick.PropertyKeyStrings.SMOOTHING_FILTER);
            }

            public event EventHandler<string> StabilityGroupChanged;

            private bool ShouldSerializeStabilityGroup(string key) =>
                touchMouseJoyAction.ChangedProperties.Contains(key);

            public TouchpadStabilityMode StabilityMode
            {
                get => touchMouseJoyAction.StabilitySettings.Mode;
                set
                {
                    touchMouseJoyAction.StabilitySettings.ApplyPreset(value);
                    StabilityGroupChanged?.Invoke(this,
                        TouchpadMouseJoystick.PropertyKeyStrings.STABILITY_MODE);
                }
            }
            public bool ShouldSerializeStabilityMode() =>
                ShouldSerializeStabilityGroup(
                    TouchpadMouseJoystick.PropertyKeyStrings.STABILITY_MODE);

            public double StabilityTouchSettleMs
            {
                get => touchMouseJoyAction.StabilitySettings.TouchSettleMs;
                set
                {
                    touchMouseJoyAction.StabilitySettings.TouchSettleMs =
                        Math.Clamp(value, 0.0, 30.0);
                    StabilityGroupChanged?.Invoke(this,
                        TouchpadMouseJoystick.PropertyKeyStrings.STABILITY_TOUCH_SETTLE);
                }
            }
            public bool ShouldSerializeStabilityTouchSettleMs() =>
                ShouldSerializeStabilityGroup(
                    TouchpadMouseJoystick.PropertyKeyStrings.STABILITY_TOUCH_SETTLE);

            public double StabilityBaseNoiseFloor
            {
                get => touchMouseJoyAction.StabilitySettings.BaseNoiseFloor;
                set
                {
                    touchMouseJoyAction.StabilitySettings.BaseNoiseFloor =
                        Math.Clamp(value, 0.0, 100.0);
                    StabilityGroupChanged?.Invoke(this,
                        TouchpadMouseJoystick.PropertyKeyStrings.STABILITY_NOISE);
                }
            }
            public bool ShouldSerializeStabilityBaseNoiseFloor() =>
                ShouldSerializeStabilityGroup(
                    TouchpadMouseJoystick.PropertyKeyStrings.STABILITY_NOISE);

            public double StabilityHysteresisExitMultiplier
            {
                get => touchMouseJoyAction.StabilitySettings.HysteresisExitMultiplier;
                set
                {
                    touchMouseJoyAction.StabilitySettings.HysteresisExitMultiplier =
                        Math.Clamp(value, 1.0, 3.0);
                    StabilityGroupChanged?.Invoke(this,
                        TouchpadMouseJoystick.PropertyKeyStrings.STABILITY_NOISE);
                }
            }
            public bool ShouldSerializeStabilityHysteresisExitMultiplier() =>
                ShouldSerializeStabilityGroup(
                    TouchpadMouseJoystick.PropertyKeyStrings.STABILITY_NOISE);

            public double StabilityFastPassthroughThreshold
            {
                get => touchMouseJoyAction.StabilitySettings.FastPassthroughThreshold;
                set
                {
                    touchMouseJoyAction.StabilitySettings.FastPassthroughThreshold =
                        Math.Clamp(value, 0.0, 200.0);
                    StabilityGroupChanged?.Invoke(this,
                        TouchpadMouseJoystick.PropertyKeyStrings.STABILITY_NOISE);
                }
            }
            public bool ShouldSerializeStabilityFastPassthroughThreshold() =>
                ShouldSerializeStabilityGroup(
                    TouchpadMouseJoystick.PropertyKeyStrings.STABILITY_NOISE);

            public bool StabilityEdgeGuardEnabled
            {
                get => touchMouseJoyAction.StabilitySettings.EdgeGuardEnabled;
                set
                {
                    touchMouseJoyAction.StabilitySettings.EdgeGuardEnabled = value;
                    StabilityGroupChanged?.Invoke(this,
                        TouchpadMouseJoystick.PropertyKeyStrings.STABILITY_EDGE_GUARD);
                }
            }
            public bool ShouldSerializeStabilityEdgeGuardEnabled() =>
                ShouldSerializeStabilityGroup(
                    TouchpadMouseJoystick.PropertyKeyStrings.STABILITY_EDGE_GUARD);

            public double StabilityLeftEdgePercent
            {
                get => touchMouseJoyAction.StabilitySettings.LeftEdgePercent;
                set
                {
                    touchMouseJoyAction.StabilitySettings.LeftEdgePercent =
                        Math.Clamp(value, 0.0, 30.0);
                    StabilityGroupChanged?.Invoke(this,
                        TouchpadMouseJoystick.PropertyKeyStrings.STABILITY_EDGE_GUARD);
                }
            }
            public bool ShouldSerializeStabilityLeftEdgePercent() =>
                ShouldSerializeStabilityGroup(
                    TouchpadMouseJoystick.PropertyKeyStrings.STABILITY_EDGE_GUARD);

            public double StabilityRightEdgePercent
            {
                get => touchMouseJoyAction.StabilitySettings.RightEdgePercent;
                set
                {
                    touchMouseJoyAction.StabilitySettings.RightEdgePercent =
                        Math.Clamp(value, 0.0, 30.0);
                    StabilityGroupChanged?.Invoke(this,
                        TouchpadMouseJoystick.PropertyKeyStrings.STABILITY_EDGE_GUARD);
                }
            }
            public bool ShouldSerializeStabilityRightEdgePercent() =>
                ShouldSerializeStabilityGroup(
                    TouchpadMouseJoystick.PropertyKeyStrings.STABILITY_EDGE_GUARD);

            public double StabilityTopEdgePercent
            {
                get => touchMouseJoyAction.StabilitySettings.TopEdgePercent;
                set
                {
                    touchMouseJoyAction.StabilitySettings.TopEdgePercent =
                        Math.Clamp(value, 0.0, 30.0);
                    StabilityGroupChanged?.Invoke(this,
                        TouchpadMouseJoystick.PropertyKeyStrings.STABILITY_EDGE_GUARD);
                }
            }
            public bool ShouldSerializeStabilityTopEdgePercent() =>
                ShouldSerializeStabilityGroup(
                    TouchpadMouseJoystick.PropertyKeyStrings.STABILITY_EDGE_GUARD);

            public double StabilityBottomEdgePercent
            {
                get => touchMouseJoyAction.StabilitySettings.BottomEdgePercent;
                set
                {
                    touchMouseJoyAction.StabilitySettings.BottomEdgePercent =
                        Math.Clamp(value, 0.0, 30.0);
                    StabilityGroupChanged?.Invoke(this,
                        TouchpadMouseJoystick.PropertyKeyStrings.STABILITY_EDGE_GUARD);
                }
            }
            public bool ShouldSerializeStabilityBottomEdgePercent() =>
                ShouldSerializeStabilityGroup(
                    TouchpadMouseJoystick.PropertyKeyStrings.STABILITY_EDGE_GUARD);

            public double StabilityEdgeJitterMultiplier
            {
                get => touchMouseJoyAction.StabilitySettings.EdgeJitterMultiplier;
                set
                {
                    touchMouseJoyAction.StabilitySettings.EdgeJitterMultiplier =
                        Math.Clamp(value, 1.0, 5.0);
                    StabilityGroupChanged?.Invoke(this,
                        TouchpadMouseJoystick.PropertyKeyStrings.STABILITY_EDGE_GUARD);
                }
            }
            public bool ShouldSerializeStabilityEdgeJitterMultiplier() =>
                ShouldSerializeStabilityGroup(
                    TouchpadMouseJoystick.PropertyKeyStrings.STABILITY_EDGE_GUARD);

            public double StabilityCornerJitterMultiplier
            {
                get => touchMouseJoyAction.StabilitySettings.CornerJitterMultiplier;
                set
                {
                    touchMouseJoyAction.StabilitySettings.CornerJitterMultiplier =
                        Math.Clamp(value, 1.0, 5.0);
                    StabilityGroupChanged?.Invoke(this,
                        TouchpadMouseJoystick.PropertyKeyStrings.STABILITY_EDGE_GUARD);
                }
            }
            public bool ShouldSerializeStabilityCornerJitterMultiplier() =>
                ShouldSerializeStabilityGroup(
                    TouchpadMouseJoystick.PropertyKeyStrings.STABILITY_EDGE_GUARD);

            public double StabilityTopLeftCornerMultiplier
            {
                get => touchMouseJoyAction.StabilitySettings.TopLeftCornerMultiplier;
                set
                {
                    touchMouseJoyAction.StabilitySettings.TopLeftCornerMultiplier =
                        Math.Clamp(value, 1.0, 6.0);
                    StabilityGroupChanged?.Invoke(this,
                        TouchpadMouseJoystick.PropertyKeyStrings.STABILITY_EDGE_GUARD);
                }
            }
            public bool ShouldSerializeStabilityTopLeftCornerMultiplier() =>
                ShouldSerializeStabilityGroup(
                    TouchpadMouseJoystick.PropertyKeyStrings.STABILITY_EDGE_GUARD);

            public double StabilityEdgeHysteresisPercent
            {
                get => touchMouseJoyAction.StabilitySettings.EdgeHysteresisPercent;
                set
                {
                    touchMouseJoyAction.StabilitySettings.EdgeHysteresisPercent =
                        Math.Clamp(value, 0.0, 10.0);
                    StabilityGroupChanged?.Invoke(this,
                        TouchpadMouseJoystick.PropertyKeyStrings.STABILITY_EDGE_GUARD);
                }
            }
            public bool ShouldSerializeStabilityEdgeHysteresisPercent() =>
                ShouldSerializeStabilityGroup(
                    TouchpadMouseJoystick.PropertyKeyStrings.STABILITY_EDGE_GUARD);

            public bool StabilityEdgeLockEnabled
            {
                get => touchMouseJoyAction.StabilitySettings.EdgeLockEnabled;
                set
                {
                    touchMouseJoyAction.StabilitySettings.EdgeLockEnabled = value;
                    StabilityGroupChanged?.Invoke(this,
                        TouchpadMouseJoystick.PropertyKeyStrings.STABILITY_EDGE_GUARD);
                }
            }
            public bool ShouldSerializeStabilityEdgeLockEnabled() =>
                ShouldSerializeStabilityGroup(
                    TouchpadMouseJoystick.PropertyKeyStrings.STABILITY_EDGE_GUARD);

            public bool StabilityEdgeStartGateEnabled
            {
                get => touchMouseJoyAction.StabilitySettings.EdgeStartGateEnabled;
                set
                {
                    touchMouseJoyAction.StabilitySettings.EdgeStartGateEnabled = value;
                    StabilityGroupChanged?.Invoke(this,
                        TouchpadMouseJoystick.PropertyKeyStrings.STABILITY_EDGE_START_GATE);
                }
            }
            public bool ShouldSerializeStabilityEdgeStartGateEnabled() =>
                ShouldSerializeStabilityGroup(
                    TouchpadMouseJoystick.PropertyKeyStrings.STABILITY_EDGE_START_GATE);

            public double StabilityEdgeStartThreshold
            {
                get => touchMouseJoyAction.StabilitySettings.EdgeStartThreshold;
                set
                {
                    touchMouseJoyAction.StabilitySettings.EdgeStartThreshold =
                        Math.Clamp(value, 0.0, 300.0);
                    StabilityGroupChanged?.Invoke(this,
                        TouchpadMouseJoystick.PropertyKeyStrings.STABILITY_EDGE_START_GATE);
                }
            }
            public bool ShouldSerializeStabilityEdgeStartThreshold() =>
                ShouldSerializeStabilityGroup(
                    TouchpadMouseJoystick.PropertyKeyStrings.STABILITY_EDGE_START_GATE);

            public bool StabilityStationaryHoldEnabled
            {
                get => touchMouseJoyAction.StabilitySettings.StationaryHoldEnabled;
                set
                {
                    touchMouseJoyAction.StabilitySettings.StationaryHoldEnabled = value;
                    StabilityGroupChanged?.Invoke(this,
                        TouchpadMouseJoystick.PropertyKeyStrings.STABILITY_STATIONARY);
                }
            }
            public bool ShouldSerializeStabilityStationaryHoldEnabled() =>
                ShouldSerializeStabilityGroup(
                    TouchpadMouseJoystick.PropertyKeyStrings.STABILITY_STATIONARY);

            public double StabilityStationaryDetectionMs
            {
                get => touchMouseJoyAction.StabilitySettings.StationaryDetectionMs;
                set
                {
                    touchMouseJoyAction.StabilitySettings.StationaryDetectionMs =
                        Math.Clamp(value, 0.0, 200.0);
                    StabilityGroupChanged?.Invoke(this,
                        TouchpadMouseJoystick.PropertyKeyStrings.STABILITY_STATIONARY);
                }
            }
            public bool ShouldSerializeStabilityStationaryDetectionMs() =>
                ShouldSerializeStabilityGroup(
                    TouchpadMouseJoystick.PropertyKeyStrings.STABILITY_STATIONARY);

            public double StabilityStationaryNoiseMultiplier
            {
                get => touchMouseJoyAction.StabilitySettings.StationaryNoiseMultiplier;
                set
                {
                    touchMouseJoyAction.StabilitySettings.StationaryNoiseMultiplier =
                        Math.Clamp(value, 1.0, 4.0);
                    StabilityGroupChanged?.Invoke(this,
                        TouchpadMouseJoystick.PropertyKeyStrings.STABILITY_STATIONARY);
                }
            }
            public bool ShouldSerializeStabilityStationaryNoiseMultiplier() =>
                ShouldSerializeStabilityGroup(
                    TouchpadMouseJoystick.PropertyKeyStrings.STABILITY_STATIONARY);

            public double StabilityStationaryBreakoutThreshold
            {
                get => touchMouseJoyAction.StabilitySettings.StationaryBreakoutThreshold;
                set
                {
                    touchMouseJoyAction.StabilitySettings.StationaryBreakoutThreshold =
                        Math.Clamp(value, 0.0, 200.0);
                    StabilityGroupChanged?.Invoke(this,
                        TouchpadMouseJoystick.PropertyKeyStrings.STABILITY_STATIONARY);
                }
            }
            public bool ShouldSerializeStabilityStationaryBreakoutThreshold() =>
                ShouldSerializeStabilityGroup(
                    TouchpadMouseJoystick.PropertyKeyStrings.STABILITY_STATIONARY);

            public bool StabilityDeltaClampEnabled
            {
                get => touchMouseJoyAction.StabilitySettings.DeltaClampEnabled;
                set
                {
                    touchMouseJoyAction.StabilitySettings.DeltaClampEnabled = value;
                    StabilityGroupChanged?.Invoke(this,
                        TouchpadMouseJoystick.PropertyKeyStrings.STABILITY_DELTA_CLAMP);
                }
            }
            public bool ShouldSerializeStabilityDeltaClampEnabled() =>
                ShouldSerializeStabilityGroup(
                    TouchpadMouseJoystick.PropertyKeyStrings.STABILITY_DELTA_CLAMP);

            public double StabilityMaxDeltaPerFrame
            {
                get => touchMouseJoyAction.StabilitySettings.MaxDeltaPerFrame;
                set
                {
                    touchMouseJoyAction.StabilitySettings.MaxDeltaPerFrame =
                        Math.Clamp(value, 10.0, 500.0);
                    StabilityGroupChanged?.Invoke(this,
                        TouchpadMouseJoystick.PropertyKeyStrings.STABILITY_DELTA_CLAMP);
                }
            }
            public bool ShouldSerializeStabilityMaxDeltaPerFrame() =>
                ShouldSerializeStabilityGroup(
                    TouchpadMouseJoystick.PropertyKeyStrings.STABILITY_DELTA_CLAMP);

            public TouchpadMouseJoystickSettings(TouchpadMouseJoystick action)
            {
                touchMouseJoyAction = action;
            }
        }

        private TouchpadMouseJoystick touchMouseJoyAction = new TouchpadMouseJoystick();

        private TouchpadMouseJoystickSettings settings;
        public TouchpadMouseJoystickSettings Settings
        {
            get => settings;
            set => settings = value;
        }
        public bool ShouldSerializeSettings()
        {
            return touchMouseJoyAction.ChangedProperties.Count > 0;
        }

        // Deserialize
        public TouchpadMouseJoystickSerializer() : base()
        {
            mapAction = touchMouseJoyAction;
            settings = new TouchpadMouseJoystickSettings(touchMouseJoyAction);

            NameChanged += TouchpadMouseJoystickSerializer_NameChanged;
            settings.DeadZoneChanged += Settings_DeadZoneChanged;
            settings.MaxZoneChanged += Settings_MaxZoneChanged;
            settings.AntiDeadZoneXChanged += Settings_AntiDeadZoneXChanged;
            settings.AntiDeadZoneYChanged += Settings_AntiDeadZoneYChanged;
            settings.RotationChanged += Settings_RotationChanged;
            settings.OutputStickChanged += Settings_OutputStickChanged;
            settings.OutputCurveChanged += Settings_OutputCurveChanged;
            settings.TrackballChanged += Settings_TrackballChanged;
            settings.TrackballFrictionChanged += Settings_TrackballFrictionChanged;
            settings.InvertXChanged += Settings_InvertXChanged;
            settings.InvertYChanged += Settings_InvertYChanged;
            settings.JitterCompensationChanged += Settings_JitterCompensationChanged;
            settings.VerticalScaleChanged += Settings_VerticalScaleChanged;
            settings.SmoothingEnabledChanged += Settings_SmoothingEnabledChanged;
            settings.SmoothingMinCutoffChanged += Settings_SmoothingMinCutoffChanged;
            settings.SmoothingBetaChanged += Settings_SmoothingBetaChanged;
            settings.StabilityGroupChanged += Settings_StabilityGroupChanged;
        }

        private void Settings_StabilityGroupChanged(object sender, string propertyGroup)
        {
            touchMouseJoyAction.ChangedProperties.Add(propertyGroup);
        }

        private void Settings_JitterCompensationChanged(object sender, EventArgs e)
        {
            touchMouseJoyAction.ChangedProperties.Add(TouchpadMouseJoystick.PropertyKeyStrings.JITTER_COMPENSATION);
        }

        private void Settings_SmoothingBetaChanged(object sender, EventArgs e)
        {
            touchMouseJoyAction.ChangedProperties.Add(TouchpadMouseJoystick.PropertyKeyStrings.SMOOTHING_FILTER);
        }

        private void Settings_SmoothingMinCutoffChanged(object sender, EventArgs e)
        {
            touchMouseJoyAction.ChangedProperties.Add(TouchpadMouseJoystick.PropertyKeyStrings.SMOOTHING_FILTER);
        }

        private void Settings_SmoothingEnabledChanged(object sender, EventArgs e)
        {
            touchMouseJoyAction.ChangedProperties.Add(TouchpadMouseJoystick.PropertyKeyStrings.SMOOTHING_ENABLED);
        }

        private void Settings_RotationChanged(object sender, EventArgs e)
        {
            touchMouseJoyAction.ChangedProperties.Add(TouchpadMouseJoystick.PropertyKeyStrings.ROTATION);
        }

        private void Settings_OutputCurveChanged(object sender, EventArgs e)
        {
            touchMouseJoyAction.ChangedProperties.Add(TouchpadMouseJoystick.PropertyKeyStrings.OUTPUT_CURVE);
        }

        private void Settings_TrackballChanged(object sender, EventArgs e)
        {
            touchMouseJoyAction.ChangedProperties.Add(TouchpadMouseJoystick.PropertyKeyStrings.TRACKBALL_MODE);
        }

        private void Settings_VerticalScaleChanged(object sender, EventArgs e)
        {
            touchMouseJoyAction.ChangedProperties.Add(TouchpadMouseJoystick.PropertyKeyStrings.VERTICAL_SCALE);
        }

        private void Settings_InvertYChanged(object sender, EventArgs e)
        {
            touchMouseJoyAction.ChangedProperties.Add(TouchpadMouseJoystick.PropertyKeyStrings.INVERT_Y);
        }

        private void Settings_InvertXChanged(object sender, EventArgs e)
        {
            touchMouseJoyAction.ChangedProperties.Add(TouchpadMouseJoystick.PropertyKeyStrings.INVERT_X);
        }

        private void Settings_TrackballFrictionChanged(object sender, EventArgs e)
        {
            touchMouseJoyAction.ChangedProperties.Add(TouchpadMouseJoystick.PropertyKeyStrings.TRACKBALL_FRICTION);
        }

        private void Settings_AntiDeadZoneYChanged(object sender, EventArgs e)
        {
            touchMouseJoyAction.ChangedProperties.Add(TouchpadMouseJoystick.PropertyKeyStrings.ANTIDEAD_ZONE_Y);
        }

        private void Settings_AntiDeadZoneXChanged(object sender, EventArgs e)
        {
            touchMouseJoyAction.ChangedProperties.Add(TouchpadMouseJoystick.PropertyKeyStrings.ANTIDEAD_ZONE_X);
        }

        private void Settings_MaxZoneChanged(object sender, EventArgs e)
        {
            touchMouseJoyAction.ChangedProperties.Add(TouchpadMouseJoystick.PropertyKeyStrings.MAX_ZONE);
        }

        private void Settings_DeadZoneChanged(object sender, EventArgs e)
        {
            touchMouseJoyAction.ChangedProperties.Add(TouchpadMouseJoystick.PropertyKeyStrings.DEAD_ZONE);
        }

        private void TouchpadMouseJoystickSerializer_NameChanged(object sender, EventArgs e)
        {
            touchMouseJoyAction.ChangedProperties.Add(TouchpadMouseJoystick.PropertyKeyStrings.NAME);
        }

        private void Settings_OutputStickChanged(object sender, EventArgs e)
        {
            touchMouseJoyAction.ChangedProperties.Add(TouchpadMouseJoystick.PropertyKeyStrings.OUTPUT_STICK);
        }

        // Pre-serialize
        public TouchpadMouseJoystickSerializer(ActionLayer tempLayer, MapAction mapAction) :
            base(tempLayer, mapAction)
        {
            if (mapAction is TouchpadMouseJoystick temp)
            {
                touchMouseJoyAction = temp;
                this.mapAction = touchMouseJoyAction;
                settings = new TouchpadMouseJoystickSettings(touchMouseJoyAction);
            }
        }
    }

    public class TouchpadAxesActionSerializer : MapActionSerializer
    {
        public class TouchpadAxesSettings
        {
            private TouchpadAxesAction touchAxesAct;

            public double DeadZone
            {
                get => touchAxesAct.DeadMod.DeadZone;
                set
                {
                    touchAxesAct.DeadMod.DeadZone = Math.Clamp(value, 0.0, 1.0);
                    DeadZoneChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler DeadZoneChanged;

            public TouchpadAxesSettings(TouchpadAxesAction action)
            {
                touchAxesAct = action;
            }
        }

        private TouchpadAxesAction touchAxesAct = new TouchpadAxesAction();
        private TouchpadAxesSettings settings;
        public TouchpadAxesSettings Settings
        {
            get => settings;
            set => settings = value;
        }

        // Deserialize
        public TouchpadAxesActionSerializer() : base()
        {
            mapAction = touchAxesAct;
            settings = new TouchpadAxesSettings(touchAxesAct);
        }

        // Pre-serialize
        public TouchpadAxesActionSerializer(ActionLayer tempLayer, MapAction mapAction) :
            base(tempLayer, mapAction)
        {
            if (mapAction is TouchpadAxesAction temp)
            {
                touchAxesAct = temp;
                this.mapAction = touchAxesAct;
                settings = new TouchpadAxesSettings(touchAxesAct);
            }
        }
    }

    public class TouchpadCircularSerializer : MapActionSerializer
    {
        public class TouchpadCircBtnBinding
        {
            private string actionDirName;
            [JsonProperty("Name", Required = Required.Default)]
            public string ActionDirName
            {
                get => actionDirName;
                set => actionDirName = value;
            }
            public bool ShouldSerializeActionDirName()
            {
                return !string.IsNullOrEmpty(actionDirName);
            }

            private List<ActionFuncSerializer> actionFuncSerializers =
                new List<ActionFuncSerializer>();
            [JsonProperty("Functions", Required = Required.Always)]
            public List<ActionFuncSerializer> ActionFuncSerializers
            {
                get => actionFuncSerializers;
                set
                {
                    actionFuncSerializers = value;
                    ActionFuncSerializersChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler ActionFuncSerializersChanged;
        }

        public class TouchpadCircularSettings
        {
            private TouchpadCircular touchCircAct;

            public double Sensitivity
            {
                get => touchCircAct.Sensitivity;
                set
                {
                    touchCircAct.Sensitivity = Math.Clamp(value, 0.0, 10.0);
                    SensitivityChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler SensitivityChanged;

            public bool ShouldSerializeSensitivity()
            {
                return touchCircAct.ChangedProperties.Contains(TouchpadCircular.PropertyKeyStrings.SENSITIVITY);
            }

            [JsonConverter(typeof(StringEnumConverter))]
            public MapAction.HapticsIntensity HapticsIntensity
            {
                get => touchCircAct.ActionHapticsIntensity;
                set
                {
                    touchCircAct.ActionHapticsIntensity = value;
                    HapticsIntensityChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler HapticsIntensityChanged;
            public bool ShouldSerializeHapticsIntensity()
            {
                return touchCircAct.ChangedProperties.Contains(TouchpadCircular.PropertyKeyStrings.HAPTICS_INTENSITY);
            }

            public TouchpadCircularSettings(TouchpadCircular action)
            {
                touchCircAct = action;
            }
        }

        private TouchpadCircular touchCircAct = new TouchpadCircular();

        private TouchpadCircBtnBinding clockwiseBinding;
        public TouchpadCircBtnBinding Clockwise
        {
            get => clockwiseBinding;
            set
            {
                clockwiseBinding = value;
                ClockwiseChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        private event EventHandler ClockwiseChanged;

        private TouchpadCircBtnBinding counterClockwiseBinding;
        public TouchpadCircBtnBinding CounterClockwise
        {
            get => counterClockwiseBinding;
            set
            {
                counterClockwiseBinding = value;
                CounterClockwiseChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        private event EventHandler CounterClockwiseChanged;

        private TouchpadCircularSettings settings;
        public TouchpadCircularSettings Settings
        {
            get => settings;
            set => settings = value;
        }

        // Deserialize
        public TouchpadCircularSerializer() : base()
        {
            mapAction = touchCircAct;
            settings = new TouchpadCircularSettings(touchCircAct);

            NameChanged += TouchpadCircularSerializer_NameChanged;
            ClockwiseChanged += TouchpadCircularSerializer_ClockwiseChanged;
            CounterClockwiseChanged += TouchpadCircularSerializer_CounterClockwiseChanged;
            settings.SensitivityChanged += Settings_SensitivityChanged;
            settings.HapticsIntensityChanged += Settings_HapticsIntensityChanged;
        }

        private void Settings_HapticsIntensityChanged(object sender, EventArgs e)
        {
            touchCircAct.ChangedProperties.Add(TouchpadCircular.PropertyKeyStrings.HAPTICS_INTENSITY);
        }

        private void Settings_SensitivityChanged(object sender, EventArgs e)
        {
            touchCircAct.ChangedProperties.Add(TouchpadCircular.PropertyKeyStrings.SENSITIVITY);
        }

        // Serialize
        public TouchpadCircularSerializer(ActionLayer tempLayer, MapAction mapAction) :
            base(tempLayer, mapAction)
        {
            if (mapAction is TouchpadCircular temp)
            {
                touchCircAct = temp;
                this.mapAction = touchCircAct;
                settings = new TouchpadCircularSettings(touchCircAct);

                PopulateFuncs();
            }
        }

        // Post-deserialize
        public override void PopulateMap()
        {
            touchCircAct.ClockWiseBtn.ActionFuncs.Clear();
            touchCircAct.CounterClockwiseBtn.ActionFuncs.Clear();

            TouchpadCircularButton tempBtn = null;

            if (clockwiseBinding != null)
            {
                tempBtn = touchCircAct.ClockWiseBtn;
                tempBtn.Name = clockwiseBinding.ActionDirName;
                foreach (ActionFuncSerializer serializer in clockwiseBinding.ActionFuncSerializers)
                {
                    serializer.PopulateFunc();
                    tempBtn.ActionFuncs.Add(serializer.ActionFunc);
                }

                touchCircAct.ChangedProperties.Add(TouchpadCircular.PropertyKeyStrings.SCROLL_BUTTON_1);
                tempBtn = null;
            }

            if (counterClockwiseBinding != null)
            {
                tempBtn = touchCircAct.CounterClockwiseBtn;
                tempBtn.Name = counterClockwiseBinding.ActionDirName;
                foreach (ActionFuncSerializer serializer in counterClockwiseBinding.ActionFuncSerializers)
                {
                    serializer.PopulateFunc();
                    tempBtn.ActionFuncs.Add(serializer.ActionFunc);
                }

                touchCircAct.ChangedProperties.Add(TouchpadCircular.PropertyKeyStrings.SCROLL_BUTTON_2);
                tempBtn = null;
            }
        }

        // Pre-serialize
        public void PopulateFuncs()
        {
            List<ActionFuncSerializer> tempFuncs = new List<ActionFuncSerializer>();

            clockwiseBinding = new TouchpadCircBtnBinding();
            clockwiseBinding.ActionDirName = touchCircAct.ClockWiseBtn.Name;
            foreach(ActionFunc tempFunc in touchCircAct.ClockWiseBtn.ActionFuncs)
            {
                ActionFuncSerializer tempSerializer =
                        ActionFuncSerializerFactory.CreateSerializer(tempFunc);
                tempFuncs.Add(tempSerializer);
            }
            clockwiseBinding.ActionFuncSerializers.AddRange(tempFuncs);

            tempFuncs.Clear();
            counterClockwiseBinding = new TouchpadCircBtnBinding();
            counterClockwiseBinding.ActionDirName = touchCircAct.CounterClockwiseBtn.Name;
            foreach (ActionFunc tempFunc in touchCircAct.CounterClockwiseBtn.ActionFuncs)
            {
                ActionFuncSerializer tempSerializer =
                        ActionFuncSerializerFactory.CreateSerializer(tempFunc);
                tempFuncs.Add(tempSerializer);
            }
            counterClockwiseBinding.ActionFuncSerializers.AddRange(tempFuncs);
        }

        private void TouchpadCircularSerializer_CounterClockwiseChanged(object sender, EventArgs e)
        {
            touchCircAct.ChangedProperties.Add(TouchpadCircular.PropertyKeyStrings.SCROLL_BUTTON_2);
        }

        private void TouchpadCircularSerializer_ClockwiseChanged(object sender, EventArgs e)
        {
            touchCircAct.ChangedProperties.Add(TouchpadCircular.PropertyKeyStrings.SCROLL_BUTTON_1);
        }

        private void TouchpadCircularSerializer_NameChanged(object sender, EventArgs e)
        {
            touchCircAct.ChangedProperties.Add(TouchpadCircular.PropertyKeyStrings.NAME);
        }
    }

    public class TouchpadStickActionSerializer : MapActionSerializer
    {
        public class TouchStickActionSettings
        {
            private TouchpadStickAction touchStickAction;

            [JsonProperty("OutputStick")]
            //public StickActionCodes OutputStick
            public string OutputStick
            {
                //get => touchStickAction.OutputAction.StickCode;
                get
                {
                    return StickCodeHelper.Convert(touchStickAction.OutputAction.StickCode);
                }
                set
                {
                    if (Enum.TryParse(value, out StickActionCodes stickCode))
                    {
                        touchStickAction.OutputAction.StickCode = stickCode;
                        OutputStickChanged?.Invoke(this, EventArgs.Empty);
                    }
                }
            }
            public event EventHandler OutputStickChanged;

            [JsonConverter(typeof(StringEnumConverter))]
            public StickOutCurve.Curve OutputCurve
            {
                get => touchStickAction.OutputCurve;
                set
                {
                    touchStickAction.OutputCurve = value;
                    OutputCurveChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler OutputCurveChanged;
            public bool ShouldSerializeOutputCurve()
            {
                return touchStickAction.ChangedProperties.Contains(TouchpadStickAction.PropertyKeyStrings.OUTPUT_CURVE);
            }

            public double DeadZone
            {
                get => touchStickAction.DeadMod.DeadZone;
                set
                {
                    touchStickAction.DeadMod.DeadZone = Math.Clamp(value, 0.0, 1.0);
                    DeadZoneChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler DeadZoneChanged;
            public bool ShouldSerializeDeadZone()
            {
                return touchStickAction.ChangedProperties.Contains(TouchpadStickAction.PropertyKeyStrings.DEAD_ZONE);
            }

            public double MaxZone
            {
                get => touchStickAction.DeadMod.MaxZone;
                set
                {
                    touchStickAction.DeadMod.MaxZone = Math.Clamp(value, 0.0, 1.0);
                    MaxZoneChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler MaxZoneChanged;
            public bool ShouldSerializeMaxZone()
            {
                return touchStickAction.ChangedProperties.Contains(TouchpadStickAction.PropertyKeyStrings.MAX_ZONE);
            }

            public double AntiDeadZone
            {
                get => touchStickAction.DeadMod.AntiDeadZone;
                set
                {
                    touchStickAction.DeadMod.AntiDeadZone = Math.Clamp(value, 0.0, 1.0);
                    AntiDeadZoneChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler AntiDeadZoneChanged;
            public bool ShouldSerializeAntiDeadZone()
            {
                return touchStickAction.ChangedProperties.Contains(TouchpadStickAction.PropertyKeyStrings.ANTIDEAD_ZONE);
            }

            [JsonConverter(typeof(StringEnumConverter))]
            public StickDeadZone.DeadZoneTypes DeadZoneType
            {
                get => touchStickAction.DeadMod.DeadZoneType;
                set
                {
                    touchStickAction.DeadMod.DeadZoneType = value;
                    DeadZoneTypeChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler DeadZoneTypeChanged;
            public bool ShouldSerializeDeadZoneType()
            {
                return touchStickAction.ChangedProperties.Contains(TouchpadStickAction.PropertyKeyStrings.DEAD_ZONE_TYPE);
            }

            public int Rotation
            {
                get => touchStickAction.Rotation;
                set
                {
                    touchStickAction.Rotation = Math.Clamp(value, -180, 180);
                    RotationChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler RotationChanged;
            public bool ShouldSerializeRotation()
            {
                return touchStickAction.ChangedProperties.Contains(TouchpadStickAction.PropertyKeyStrings.ROTATION);
            }

            public bool InvertX
            {
                get => touchStickAction.InvertX;
                set
                {
                    touchStickAction.InvertX = value;
                    InvertXChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler InvertXChanged;
            public bool ShouldSerializeInvertX()
            {
                return touchStickAction.ChangedProperties.Contains(TouchpadStickAction.PropertyKeyStrings.INVERT_X);
            }

            public bool InvertY
            {
                get => touchStickAction.InvertY;
                set
                {
                    touchStickAction.InvertY = value;
                    InvertYChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler InvertYChanged;
            public bool ShouldSerializeInvertY()
            {
                return touchStickAction.ChangedProperties.Contains(TouchpadStickAction.PropertyKeyStrings.INVERT_Y);
            }

            public double VerticalScale
            {
                get => touchStickAction.VerticalScale;
                set
                {
                    touchStickAction.VerticalScale = Math.Clamp(value, 0.0, 10.0);
                    VerticalScaleChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler VerticalScaleChanged;
            public bool ShouldSerializeVerticalScale()
            {
                return touchStickAction.ChangedProperties.Contains(TouchpadStickAction.PropertyKeyStrings.VERTICAL_SCALE);
            }

            public bool MaxOutputEnabled
            {
                get => touchStickAction.MaxOutputEnabled;
                set
                {
                    touchStickAction.MaxOutputEnabled = value;
                    MaxOutputEnabledChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler MaxOutputEnabledChanged;
            public bool ShouldSerializeMaxOutputEnabled()
            {
                return touchStickAction.ChangedProperties.Contains(TouchpadStickAction.PropertyKeyStrings.MAX_OUTPUT_ENABLED);
            }

            public double MaxOutput
            {
                get => touchStickAction.MaxOutput;
                set
                {
                    touchStickAction.MaxOutput = Math.Clamp(value, 0.0, 1.0);
                    MaxOutputChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler MaxOutputChanged;
            public bool ShouldSerializeMaxOutput()
            {
                return touchStickAction.ChangedProperties.Contains(TouchpadStickAction.PropertyKeyStrings.MAX_OUTPUT);
            }

            public bool SquareStickEnabled
            {
                get => touchStickAction.SquareStickEnabled;
                set
                {
                    touchStickAction.SquareStickEnabled = value;
                    SquareStickEnabledChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler SquareStickEnabledChanged;
            public bool ShouldSerializeSquareStickEnabled()
            {
                return touchStickAction.ChangedProperties.Contains(TouchpadStickAction.PropertyKeyStrings.SQUARE_STICK_ENABLED);
            }

            public double SquareStickRoundness
            {
                get => touchStickAction.SquareStickRoundness;
                set
                {
                    touchStickAction.SquareStickRoundness = Math.Clamp(value, 1.0, 10.0);
                    SquareStickRoundnessChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler SquareStickRoundnessChanged;
            public bool ShouldSerializeSquareStickRoundness()
            {
                return touchStickAction.ChangedProperties.Contains(TouchpadStickAction.PropertyKeyStrings.SQUARE_STICK_ROUNDNESS);
            }

            public bool ForcedCenter
            {
                get => touchStickAction.ForcedCenter;
                set
                {
                    touchStickAction.ForcedCenter = value;
                    ForcedCenterChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler ForcedCenterChanged;
            public bool ShouldSerializeForcedCenter()
            {
                return touchStickAction.ChangedProperties.Contains(TouchpadStickAction.PropertyKeyStrings.FORCED_CENTER);
            }

            [JsonProperty("UseOuterRing")]
            public bool UseOuterRing
            {
                get => touchStickAction.UseRingButton;
                set
                {
                    touchStickAction.UseRingButton = value;
                    UseOuterRingChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler UseOuterRingChanged;
            public bool ShouldSerializeUseOuterRing()
            {
                return touchStickAction.ChangedProperties.Contains(TouchpadStickAction.PropertyKeyStrings.USE_OUTER_RING);
            }

            [JsonProperty("OuterRingDeadZone")]
            public double OuterRingDeadZone
            {
                get => touchStickAction.OuterRingDeadZone;
                set
                {
                    touchStickAction.OuterRingDeadZone = value;
                    OuterRingDeadZoneChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler OuterRingDeadZoneChanged;
            public bool ShouldSerializeOuterRingDeadZone()
            {
                return touchStickAction.ChangedProperties.Contains(TouchpadStickAction.PropertyKeyStrings.OUTER_RING_DEAD_ZONE);
            }

            [JsonProperty("UseAsOuterRing")]
            public bool UseAsOuterRing
            {
                get => touchStickAction.UseAsOuterRing;
                set
                {
                    touchStickAction.UseAsOuterRing = value;
                    UseAsOuterRingChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler UseAsOuterRingChanged;
            public bool ShouldSerializeUseAsOuterRing()
            {
                return touchStickAction.ChangedProperties.Contains(TouchpadStickAction.PropertyKeyStrings.USE_AS_OUTER_RING);
            }

            [JsonConverter(typeof(StringEnumConverter))]
            public OuterRingUseRange OuterRingRange
            {
                get => touchStickAction.UsedOuterRingRange;
                set
                {
                    touchStickAction.UsedOuterRingRange = value;
                    OuterRingRangeChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler OuterRingRangeChanged;
            public bool ShouldSerializeOuterRange()
            {
                return touchStickAction.ChangedProperties.Contains(TouchpadStickAction.PropertyKeyStrings.OUTER_RING_FULL_RANGE);
            }

            public bool SmoothingEnabled
            {
                get => touchStickAction.Smoothing;
                set
                {
                    touchStickAction.Smoothing = value;
                    SmoothingEnabledChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler SmoothingEnabledChanged;
            public bool ShouldSerializeSmoothingEnabled()
            {
                return touchStickAction.ChangedProperties.Contains(TouchpadStickAction.PropertyKeyStrings.SMOOTHING_ENABLED);
            }

            public double SmoothingMinCutoff
            {
                get => touchStickAction.SmoothingFilterSettingsDataRef.minCutOff;
                set
                {
                    touchStickAction.SmoothingFilterSettingsDataRef.minCutOff = Math.Clamp(value, 0.0, 10.0);
                    touchStickAction.SmoothingFilterSettingsDataRef.UpdateSmoothingFilters();
                    SmoothingMinCutoffChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler SmoothingMinCutoffChanged;
            public bool ShouldSerializeSmoothingMinCutoff()
            {
                return touchStickAction.ChangedProperties.Contains(TouchpadStickAction.PropertyKeyStrings.SMOOTHING_FILTER);
            }

            public double SmoothingBeta
            {
                get => touchStickAction.SmoothingFilterSettingsDataRef.beta;
                set
                {
                    touchStickAction.SmoothingFilterSettingsDataRef.beta = Math.Clamp(value, 0.0, 1.0);
                    touchStickAction.SmoothingFilterSettingsDataRef.UpdateSmoothingFilters();
                    SmoothingBetaChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler SmoothingBetaChanged;
            public bool ShouldSerializeSmoothingBeta()
            {
                return touchStickAction.ChangedProperties.Contains(TouchpadStickAction.PropertyKeyStrings.SMOOTHING_FILTER);
            }

            public TouchStickActionSettings(TouchpadStickAction action)
            {
                this.touchStickAction = action;
            }
        }

        private TouchPadDirBinding ringBinding;

        [JsonProperty("OuterRingBinding")]
        public TouchPadDirBinding RingBinding
        {
            get => ringBinding;
            set
            {
                ringBinding = value;
                RingBindingChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        public event EventHandler RingBindingChanged;
        public bool ShouldSerializeRingBinding()
        {
            return ringBinding != null && ringBinding.ActionFuncSerializers.Count > 0;
        }

        private TouchpadStickAction touchStickAction = new TouchpadStickAction();

        private TouchStickActionSettings settings;
        public TouchStickActionSettings Settings
        {
            get => settings;
            set => settings = value;
        }

        // Deserialize
        public TouchpadStickActionSerializer() : base()
        {
            mapAction = touchStickAction;
            settings = new TouchStickActionSettings(touchStickAction);

            NameChanged += TouchpadStickActionSerializer_NameChanged;
            RingBindingChanged += TouchpadStickActionSerializer_RingBindingChanged;
            settings.OutputStickChanged += Settings_OutputStickChanged;
            settings.OutputCurveChanged += Settings_OutputCurveChanged;
            settings.DeadZoneChanged += Settings_DeadZoneChanged;
            settings.MaxZoneChanged += Settings_MaxZoneChanged;
            settings.AntiDeadZoneChanged += Settings_AntiDeadZoneChanged;
            settings.InvertXChanged += Settings_InvertXChanged;
            settings.InvertYChanged += Settings_InvertYChanged;
            settings.VerticalScaleChanged += Settings_VerticalScaleChanged;
            settings.MaxOutputEnabledChanged += Settings_MaxOutputEnabledChanged;
            settings.MaxOutputChanged += Settings_MaxOutputChanged;
            settings.SquareStickEnabledChanged += Settings_SquareStickEnabledChanged;
            settings.SquareStickRoundnessChanged += Settings_SquareStickRoundnessChanged;
            settings.DeadZoneTypeChanged += Settings_DeadZoneTypeChanged;
            settings.ForcedCenterChanged += Settings_ForcedCenterChanged;

            settings.UseOuterRingChanged += Settings_UseOuterRingChanged;
            settings.UseAsOuterRingChanged += Settings_UseAsOuterRingChanged;
            settings.OuterRingDeadZoneChanged += Settings_OuterRingDeadZoneChanged;
            settings.OuterRingRangeChanged += Settings_OuterRingRangeChanged;

            settings.SmoothingEnabledChanged += Settings_SmoothingEnabledChanged;
            settings.SmoothingMinCutoffChanged += Settings_SmoothingMinCutoffChanged;
            settings.SmoothingBetaChanged += Settings_SmoothingBetaChanged;
        }

        private void TouchpadStickActionSerializer_RingBindingChanged(object sender, EventArgs e)
        {
            touchStickAction.ChangedProperties.Add(TouchpadStickAction.PropertyKeyStrings.OUTER_RING_BUTTON);
        }

        private void Settings_OuterRingRangeChanged(object sender, EventArgs e)
        {
            touchStickAction.ChangedProperties.Add(TouchpadStickAction.PropertyKeyStrings.OUTER_RING_FULL_RANGE);
        }

        private void Settings_OuterRingDeadZoneChanged(object sender, EventArgs e)
        {
            touchStickAction.ChangedProperties.Add(TouchpadStickAction.PropertyKeyStrings.OUTER_RING_DEAD_ZONE);
        }

        private void Settings_UseAsOuterRingChanged(object sender, EventArgs e)
        {
            touchStickAction.ChangedProperties.Add(TouchpadStickAction.PropertyKeyStrings.USE_AS_OUTER_RING);
        }

        private void Settings_UseOuterRingChanged(object sender, EventArgs e)
        {
            touchStickAction.ChangedProperties.Add(TouchpadStickAction.PropertyKeyStrings.USE_OUTER_RING);
        }

        private void Settings_SmoothingBetaChanged(object sender, EventArgs e)
        {
            touchStickAction.ChangedProperties.Add(TouchpadStickAction.PropertyKeyStrings.SMOOTHING_FILTER);
        }

        private void Settings_SmoothingMinCutoffChanged(object sender, EventArgs e)
        {
            touchStickAction.ChangedProperties.Add(TouchpadStickAction.PropertyKeyStrings.SMOOTHING_FILTER);
        }

        private void Settings_SmoothingEnabledChanged(object sender, EventArgs e)
        {
            touchStickAction.ChangedProperties.Add(TouchpadStickAction.PropertyKeyStrings.SMOOTHING_ENABLED);
        }

        private void Settings_ForcedCenterChanged(object sender, EventArgs e)
        {
            touchStickAction.ChangedProperties.Add(TouchpadStickAction.PropertyKeyStrings.FORCED_CENTER);
        }

        private void Settings_DeadZoneTypeChanged(object sender, EventArgs e)
        {
            touchStickAction.ChangedProperties.Add(TouchpadStickAction.PropertyKeyStrings.DEAD_ZONE_TYPE);
        }

        private void Settings_SquareStickRoundnessChanged(object sender, EventArgs e)
        {
            touchStickAction.ChangedProperties.Add(TouchpadStickAction.PropertyKeyStrings.SQUARE_STICK_ROUNDNESS);
        }

        private void Settings_SquareStickEnabledChanged(object sender, EventArgs e)
        {
            touchStickAction.ChangedProperties.Add(TouchpadStickAction.PropertyKeyStrings.SQUARE_STICK_ENABLED);
        }

        private void Settings_MaxOutputChanged(object sender, EventArgs e)
        {
            touchStickAction.ChangedProperties.Add(TouchpadStickAction.PropertyKeyStrings.MAX_OUTPUT);
        }

        private void Settings_MaxOutputEnabledChanged(object sender, EventArgs e)
        {
            touchStickAction.ChangedProperties.Add(TouchpadStickAction.PropertyKeyStrings.MAX_OUTPUT_ENABLED);
        }

        private void Settings_VerticalScaleChanged(object sender, EventArgs e)
        {
            touchStickAction.ChangedProperties.Add(TouchpadStickAction.PropertyKeyStrings.VERTICAL_SCALE);
        }

        private void Settings_InvertYChanged(object sender, EventArgs e)
        {
            touchStickAction.ChangedProperties.Add(TouchpadStickAction.PropertyKeyStrings.INVERT_Y);
        }

        private void Settings_InvertXChanged(object sender, EventArgs e)
        {
            touchStickAction.ChangedProperties.Add(TouchpadStickAction.PropertyKeyStrings.INVERT_X);
        }

        private void Settings_OutputCurveChanged(object sender, EventArgs e)
        {
            touchStickAction.ChangedProperties.Add(TouchpadStickAction.PropertyKeyStrings.OUTPUT_CURVE);
        }

        private void TouchpadStickActionSerializer_NameChanged(object sender, EventArgs e)
        {
            touchStickAction.ChangedProperties.Add(TouchpadStickAction.PropertyKeyStrings.NAME);
        }

        // Pre-serialize
        public TouchpadStickActionSerializer(ActionLayer tempLayer, MapAction mapAction) :
            base(tempLayer, mapAction)
        {
            if (mapAction is TouchpadStickAction temp)
            {
                touchStickAction = temp;
                this.mapAction = touchStickAction;
                settings = new TouchStickActionSettings(touchStickAction);
                PopulateFuns();
            }
        }

        // Pre-serialize
        private void PopulateFuns()
        {
            if (touchStickAction.RingButton != null)
            {
                ringBinding = new TouchPadDirBinding();
                ringBinding.ActionDirName = touchStickAction.RingButton.Name;
                foreach (ActionFunc tempFunc in touchStickAction.RingButton.ActionFuncs)
                {
                    ActionFuncSerializer tempSerializer =
                        ActionFuncSerializerFactory.CreateSerializer(tempFunc);
                    if (tempSerializer != null)
                    {
                        ringBinding.ActionFuncSerializers.Add(tempSerializer);
                    }
                }
            }
        }

        // Post-deserialize
        public override void PopulateMap()
        {
            if (ringBinding != null)
            {
                touchStickAction.RingButton.Name = ringBinding.ActionDirName;
                List<ActionFuncSerializer> tempSerializers = ringBinding.ActionFuncSerializers;
                foreach (ActionFuncSerializer serializer in tempSerializers)
                {
                    serializer.PopulateFunc();
                    touchStickAction.RingButton.ActionFuncs.Add(serializer.ActionFunc);
                }

                touchStickAction.ChangedProperties.Add(StickPadAction.PropertyKeyStrings.OUTER_RING_BUTTON);
                //stickPadAct.RingButton.ChangedProperties.Add(StickPadAction.PropertyKeyStrings.OUTER_RING_BUTTON);
            }
        }

        private void Settings_AntiDeadZoneChanged(object sender, EventArgs e)
        {
            touchStickAction.ChangedProperties.Add(TouchpadStickAction.PropertyKeyStrings.ANTIDEAD_ZONE);
        }

        private void Settings_MaxZoneChanged(object sender, EventArgs e)
        {
            touchStickAction.ChangedProperties.Add(TouchpadStickAction.PropertyKeyStrings.MAX_ZONE);
        }

        private void Settings_DeadZoneChanged(object sender, EventArgs e)
        {
            touchStickAction.ChangedProperties.Add(TouchpadStickAction.PropertyKeyStrings.DEAD_ZONE);
        }

        private void Settings_OutputStickChanged(object sender, EventArgs e)
        {
            touchStickAction.ChangedProperties.Add(TouchpadStickAction.PropertyKeyStrings.OUTPUT_STICK);
        }
    }

    public class TouchpadAbsActionSerializer : MapActionSerializer
    {
        public class OuterRingBinding
        {
            private string actionDirName;
            [JsonProperty("Name", Required = Required.Default)]
            public string ActionDirName
            {
                get => actionDirName;
                set => actionDirName = value;
            }
            public bool ShouldSerializeActionDirName()
            {
                return !string.IsNullOrEmpty(actionDirName);
            }

            private List<ActionFuncSerializer> actionFuncSerializers =
                new List<ActionFuncSerializer>();
            [JsonProperty("Functions", Required = Required.Always)]
            public List<ActionFuncSerializer> ActionFuncSerializers
            {
                get => actionFuncSerializers;
                set
                {
                    actionFuncSerializers = value;
                    ActionFuncSerializersChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler ActionFuncSerializersChanged;
        }

        public class TouchpadAbsActionSettings
        {
            private TouchpadAbsAction touchAbsAct;

            public double DeadZone
            {
                get => touchAbsAct.DeadMod.DeadZone;
                set
                {
                    touchAbsAct.DeadMod.DeadZone = Math.Clamp(value, 0.0, 1.0);
                    DeadZoneChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler DeadZoneChanged;

            public double MaxZone
            {
                get => touchAbsAct.DeadMod.MaxZone;
                set
                {
                    touchAbsAct.DeadMod.MaxZone = Math.Clamp(value, 0.0, 1.0);
                    MaxZoneChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler MaxZoneChanged;

            public double AntiRadius
            {
                get => touchAbsAct.AntiRadius;
                set
                {
                    touchAbsAct.AntiRadius = Math.Clamp(value, 0.0, 1.0);
                    AntiRadiusChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler AntiRadiusChanged;

            [JsonProperty("UseOuterRing")]
            public bool UseOuterRing
            {
                get => touchAbsAct.UseRingButton;
                set
                {
                    touchAbsAct.UseRingButton = value;
                    UseOuterRingChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler UseOuterRingChanged;

            [JsonProperty("OuterRingDeadZone")]
            public double OuterRingDeadZone
            {
                get => touchAbsAct.OuterRingDeadZone;
                set
                {
                    touchAbsAct.OuterRingDeadZone = value;
                    OuterRingDeadZoneChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler OuterRingDeadZoneChanged;

            [JsonProperty("UseAsOuterRing")]
            public bool UseAsOuterRing
            {
                get => touchAbsAct.UseAsOuterRing;
                set
                {
                    touchAbsAct.UseAsOuterRing = value;
                    UseAsOuterRingChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler UseAsOuterRingChanged;

            public OuterRingUseRange OuterRingRange
            {
                get => touchAbsAct.UsedOuterRingRange;
                set
                {
                    touchAbsAct.UsedOuterRingRange = value;
                    OuterRingRangeChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler OuterRingRangeChanged;

            public bool SnapToCenterOnRelease
            {
                get => touchAbsAct.SnapToCenterRelease;
                set
                {
                    touchAbsAct.SnapToCenterRelease = value;
                    SnapToCenterOnReleaseChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler SnapToCenterOnReleaseChanged;

            public double Width
            {
                get => touchAbsAct.AbsMouseRange.width;
                set
                {
                    touchAbsAct.AbsMouseRange.width = Math.Clamp(value, 0.0, 1.0);
                    WidthChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler WidthChanged;

            public double Height
            {
                get => touchAbsAct.AbsMouseRange.height;
                set
                {
                    touchAbsAct.AbsMouseRange.height = Math.Clamp(value, 0.0, 1.0);
                    HeightChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler HeightChanged;

            public double XCenter
            {
                get => touchAbsAct.AbsMouseRange.xcenter;
                set
                {
                    touchAbsAct.AbsMouseRange.xcenter = Math.Clamp(value, 0.0, 1.0);
                    XCenterChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler XCenterChanged;

            public double YCenter
            {
                get => touchAbsAct.AbsMouseRange.ycenter;
                set
                {
                    touchAbsAct.AbsMouseRange.ycenter = Math.Clamp(value, 0.0, 1.0);
                    YCenterChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler YCenterChanged;

            public TouchpadAbsActionSettings(TouchpadAbsAction action)
            {
                touchAbsAct = action;
            }
        }

        private OuterRingBinding ringBinding;

        [JsonProperty("OuterRingBinding")]
        public OuterRingBinding RingBinding
        {
            get => ringBinding;
            set
            {
                ringBinding = value;
                RingBindingChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        public event EventHandler RingBindingChanged;
        public bool ShouldSerializeRingBinding()
        {
            return touchAbsAct.ChangedProperties.Contains(TouchpadAbsAction.PropertyKeyStrings.OUTER_RING_BUTTON);
        }

        private TouchpadAbsActionSettings settings;
        public TouchpadAbsActionSettings Settings
        {
            get => settings;
            set => settings = value;
        }

        private TouchpadAbsAction touchAbsAct = new TouchpadAbsAction();

        // Deserialize
        public TouchpadAbsActionSerializer() : base()
        {
            mapAction = touchAbsAct;
            settings = new TouchpadAbsActionSettings(touchAbsAct);

            NameChanged += TouchpadAbsActionSerializer_NameChanged;
            RingBindingChanged += TouchpadAbsActionSerializer_RingBindingChanged;
            settings.DeadZoneChanged += Settings_DeadZoneChanged;
            settings.MaxZoneChanged += Settings_MaxZoneChanged;
            settings.AntiRadiusChanged += Settings_AntiReleaseChanged;
            settings.UseAsOuterRingChanged += Settings_UseAsOuterRingChanged;
            settings.UseOuterRingChanged += Settings_UseOuterRingChanged;
            settings.OuterRingDeadZoneChanged += Settings_OuterRingDeadZoneChanged;
            settings.OuterRingRangeChanged += Settings_OuterRingRangeChanged;
            settings.SnapToCenterOnReleaseChanged += Settings_SnapToCenterOnReleaseChanged;

            settings.WidthChanged += Settings_WidthChanged;
            settings.HeightChanged += Settings_HeightChanged;
            settings.XCenterChanged += Settings_XCenterChanged;
            settings.YCenterChanged += Settings_YCenterChanged;
        }

        private void Settings_AntiReleaseChanged(object sender, EventArgs e)
        {
            touchAbsAct.ChangedProperties.Add(TouchpadAbsAction.PropertyKeyStrings.ANTI_RADIUS);
        }

        private void Settings_MaxZoneChanged(object sender, EventArgs e)
        {
            touchAbsAct.ChangedProperties.Add(TouchpadAbsAction.PropertyKeyStrings.MAX_ZONE);
        }

        private void Settings_OuterRingRangeChanged(object sender, EventArgs e)
        {
            touchAbsAct.ChangedProperties.Add(TouchpadAbsAction.PropertyKeyStrings.OUTER_RING_FULL_RANGE);
        }

        private void Settings_YCenterChanged(object sender, EventArgs e)
        {
            touchAbsAct.ChangedProperties.Add(TouchpadAbsAction.PropertyKeyStrings.BOX_YCENTER);
        }

        private void Settings_XCenterChanged(object sender, EventArgs e)
        {
            touchAbsAct.ChangedProperties.Add(TouchpadAbsAction.PropertyKeyStrings.BOX_XCENTER);
        }

        private void Settings_HeightChanged(object sender, EventArgs e)
        {
            touchAbsAct.ChangedProperties.Add(TouchpadAbsAction.PropertyKeyStrings.BOX_HEIGHT);
        }

        private void Settings_WidthChanged(object sender, EventArgs e)
        {
            touchAbsAct.ChangedProperties.Add(TouchpadAbsAction.PropertyKeyStrings.BOX_WIDTH);
        }

        private void Settings_SnapToCenterOnReleaseChanged(object sender, EventArgs e)
        {
            touchAbsAct.ChangedProperties.Add(TouchpadAbsAction.PropertyKeyStrings.SNAP_TO_CENTER_RELEASE);
        }

        // Serialize ctor
        public TouchpadAbsActionSerializer(ActionLayer tempLayer, MapAction action) :
            base(tempLayer, action)
        {
            if (action is TouchpadAbsAction temp)
            {
                touchAbsAct = temp;
                mapAction = touchAbsAct;
                settings = new TouchpadAbsActionSettings(touchAbsAct);
                PopulateFuncs();
            }
        }

        private void PopulateFuncs()
        {
            if (touchAbsAct.RingButton != null)
            {
                ringBinding = new OuterRingBinding();
                ringBinding.ActionDirName = touchAbsAct.RingButton.Name;
                foreach (ActionFunc tempFunc in touchAbsAct.RingButton.ActionFuncs)
                {
                    ActionFuncSerializer tempSerializer =
                        ActionFuncSerializerFactory.CreateSerializer(tempFunc);
                    if (tempSerializer != null)
                    {
                        ringBinding.ActionFuncSerializers.Add(tempSerializer);
                    }
                }
            }
        }

        private void Settings_DeadZoneChanged(object sender, EventArgs e)
        {
            touchAbsAct.ChangedProperties.Add(TouchpadAbsAction.PropertyKeyStrings.DEAD_ZONE);
        }

        private void TouchpadAbsActionSerializer_NameChanged(object sender, EventArgs e)
        {
            touchAbsAct.ChangedProperties.Add(TouchpadAbsAction.PropertyKeyStrings.NAME);
        }

        private void Settings_OuterRingDeadZoneChanged(object sender, EventArgs e)
        {
            touchAbsAct.ChangedProperties.Add(TouchpadAbsAction.PropertyKeyStrings.OUTER_RING_DEAD_ZONE);
        }

        private void Settings_UseOuterRingChanged(object sender, EventArgs e)
        {
            touchAbsAct.ChangedProperties.Add(TouchpadAbsAction.PropertyKeyStrings.USE_OUTER_RING);
        }

        private void Settings_UseAsOuterRingChanged(object sender, EventArgs e)
        {
            touchAbsAct.ChangedProperties.Add(TouchpadAbsAction.PropertyKeyStrings.USE_AS_OUTER_RING);
        }

        private void TouchpadAbsActionSerializer_RingBindingChanged(object sender, EventArgs e)
        {
            touchAbsAct.ChangedProperties.Add(TouchpadAbsAction.PropertyKeyStrings.OUTER_RING_BUTTON);
        }

        public override void PopulateMap()
        {
            if (ringBinding != null)
            {
                touchAbsAct.RingButton.Name = ringBinding.ActionDirName;
                List<ActionFuncSerializer> tempSerializers = ringBinding.ActionFuncSerializers;
                foreach (ActionFuncSerializer serializer in tempSerializers)
                {
                    serializer.PopulateFunc();
                    touchAbsAct.RingButton.ActionFuncs.Add(serializer.ActionFunc);
                }

                touchAbsAct.ChangedProperties.Add(TouchpadAbsAction.PropertyKeyStrings.OUTER_RING_BUTTON);
                //stickPadAct.RingButton.ChangedProperties.Add(StickPadAction.PropertyKeyStrings.OUTER_RING_BUTTON);
            }
        }
    }



    public class TouchpadDirectionalSwipeSerializer : MapActionSerializer
    {
        public class SwipeDirBinding
        {
            public enum SwipeDir
            {
                Up,
                Down,
                Left,
                Right,
            }

            private string actionDirName;
            [JsonProperty("Name", Required = Required.Default)]
            public string ActionDirName
            {
                get => actionDirName;
                set => actionDirName = value;
            }
            public bool ShouldSerializeActionDirName()
            {
                return !string.IsNullOrEmpty(actionDirName);
            }

            private List<ActionFuncSerializer> actionFuncSerializers =
                new List<ActionFuncSerializer>();
            [JsonProperty("Functions", Required = Required.Always)]
            public List<ActionFuncSerializer> ActionFuncSerializers
            {
                get => actionFuncSerializers;
                set
                {
                    actionFuncSerializers = value;
                    ActionFuncSerializersChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler ActionFuncSerializersChanged;
        }

        public class TouchpadDirSwipeSettings
        {
            private TouchpadDirectionalSwipe touchDirSwipeAction;

            public int DeadZoneX
            {
                get => touchDirSwipeAction.swipeParams.deadzoneX;
                set
                {
                    touchDirSwipeAction.swipeParams.deadzoneX = value;
                    DeadZoneXChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler DeadZoneXChanged;
            public bool ShouldSerializeDeadZoneX()
            {
                return touchDirSwipeAction.ChangedProperties.Contains(TouchpadDirectionalSwipe.PropertyKeyStrings.DEAD_ZONE_X);
            }

            public int DeadZoneY
            {
                get => touchDirSwipeAction.swipeParams.deadzoneY;
                set
                {
                    touchDirSwipeAction.swipeParams.deadzoneY = value;
                    DeadZoneYChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler DeadZoneYChanged;
            public bool ShouldSerializeDeadZoneY()
            {
                return touchDirSwipeAction.ChangedProperties.Contains(TouchpadDirectionalSwipe.PropertyKeyStrings.DEAD_ZONE_Y);
            }

            public int DelayTime
            {
                get => touchDirSwipeAction.swipeParams.delayTime;
                set
                {
                    touchDirSwipeAction.swipeParams.delayTime = value;
                    DelayTimeChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler DelayTimeChanged;
            public bool ShouldSerializeDelayTime()
            {
                return touchDirSwipeAction.ChangedProperties.Contains(TouchpadDirectionalSwipe.PropertyKeyStrings.DELAY_TIME);
            }

            public TouchpadDirSwipeSettings(TouchpadDirectionalSwipe swipeAction)
            {
                touchDirSwipeAction = swipeAction;
            }
        }

        private Dictionary<SwipeDirBinding.SwipeDir, SwipeDirBinding> dictDirBindings =
            new Dictionary<SwipeDirBinding.SwipeDir, SwipeDirBinding>();

        //private Dictionary<TouchpadDirectionalSwipe.SwipeAxisYDir, SwipeDirBinding> dictDirYBindings =
        //    new Dictionary<TouchpadDirectionalSwipe.SwipeAxisYDir, SwipeDirBinding>();

        [JsonProperty("Bindings", Required = Required.Always)]
        public Dictionary<SwipeDirBinding.SwipeDir, SwipeDirBinding> DictDirBindings
        {
            get => dictDirBindings;
            set => dictDirBindings = value;
        }

        //[JsonProperty("YBindings", Required = Required.Always)]
        //public Dictionary<TouchpadDirectionalSwipe.SwipeAxisYDir, SwipeDirBinding> DictDirYBindings
        //{
        //    get => dictDirYBindings;
        //    set => dictDirYBindings = value;
        //}

        private TouchpadDirectionalSwipe touchDirSwipeAction = new TouchpadDirectionalSwipe();
        private TouchpadDirSwipeSettings settings;
        public TouchpadDirSwipeSettings Settings
        {
            get => settings;
            set => settings = value;
        }

        // Deserialize
        public TouchpadDirectionalSwipeSerializer() : base()
        {
            mapAction = touchDirSwipeAction;
            settings = new TouchpadDirSwipeSettings(touchDirSwipeAction);

            NameChanged += TouchDirectionalSwipeSerializer_NameChanged;
            settings.DeadZoneXChanged += Settings_DeadZoneXChanged;
            settings.DeadZoneYChanged += Settings_DeadZoneYChanged;
            settings.DelayTimeChanged += Settings_DelayTimeChanged;
        }

        // Serialize
        public TouchpadDirectionalSwipeSerializer(ActionLayer tempLayer, MapAction action) :
            base(tempLayer, action)
        {
            if (action is TouchpadDirectionalSwipe temp)
            {
                touchDirSwipeAction = temp;
                MapAction = touchDirSwipeAction;
                settings = new TouchpadDirSwipeSettings(touchDirSwipeAction);
                PopulateFuncs();
            }
        }

        // Post-deserialize
        public override void PopulateMap()
        {
            foreach (ButtonAction dirButton in touchDirSwipeAction.UsedEventsButtonsX)
            {
                dirButton?.ActionFuncs.Clear();
            }

            foreach (ButtonAction dirButton in touchDirSwipeAction.UsedEventsButtonsY)
            {
                dirButton?.ActionFuncs.Clear();
            }

            foreach (KeyValuePair<SwipeDirBinding.SwipeDir, SwipeDirBinding> pair in dictDirBindings)
            {
                SwipeDirBinding.SwipeDir dir = pair.Key;
                List<ActionFuncSerializer> tempSerializers = pair.Value.ActionFuncSerializers;
                ButtonAction dirButton = new ButtonAction();
                dirButton.Name = pair.Value.ActionDirName;
                foreach (ActionFuncSerializer serializer in tempSerializers)
                {
                    serializer.PopulateFunc();
                    dirButton.ActionFuncs.Add(serializer.ActionFunc);
                }

                switch (dir)
                {
                    case SwipeDirBinding.SwipeDir.Left:
                        touchDirSwipeAction.UsedEventsButtonsX[(int)TouchpadDirectionalSwipe.SwipeAxisXDir.Left] = dirButton;
                        break;
                    case SwipeDirBinding.SwipeDir.Right:
                        touchDirSwipeAction.UsedEventsButtonsX[(int)TouchpadDirectionalSwipe.SwipeAxisXDir.Right] = dirButton;
                        break;
                    case SwipeDirBinding.SwipeDir.Up:
                        touchDirSwipeAction.UsedEventsButtonsY[(int)TouchpadDirectionalSwipe.SwipeAxisYDir.Up] = dirButton;
                        break;
                    case SwipeDirBinding.SwipeDir.Down:
                        touchDirSwipeAction.UsedEventsButtonsY[(int)TouchpadDirectionalSwipe.SwipeAxisYDir.Down] = dirButton;
                        break;
                    default:
                        break;
                }

                FlagBtnChangedDirection(dir);
            }

            //foreach (KeyValuePair<GyroDirectionalSwipe.SwipeAxisYDir, SwipeDirBinding> pair in dictDirYBindings)
            //{
            //    GyroDirectionalSwipe.SwipeAxisYDir dir = pair.Key;
            //    List<ActionFuncSerializer> tempSerializers = pair.Value.ActionFuncSerializers;
            //    ButtonAction dirButton = new ButtonAction();
            //    dirButton.Name = pair.Value.ActionDirName;
            //    foreach (ActionFuncSerializer serializer in tempSerializers)
            //    {
            //        serializer.PopulateFunc();
            //        dirButton.ActionFuncs.Add(serializer.ActionFunc);
            //    }

            //    gyroDirSwipeAction.UsedEventsButtonsY[(int)dir] = dirButton;
            //    FlagBtnChangedYDirection(dir);
            //}
        }

        private void FlagBtnChangedDirection(SwipeDirBinding.SwipeDir dir)
        {
            switch (dir)
            {
                case SwipeDirBinding.SwipeDir.Left:
                    touchDirSwipeAction.ChangedProperties.Add(TouchpadDirectionalSwipe.PropertyKeyStrings.PAD_DIR_LEFT);
                    break;
                case SwipeDirBinding.SwipeDir.Right:
                    touchDirSwipeAction.ChangedProperties.Add(TouchpadDirectionalSwipe.PropertyKeyStrings.PAD_DIR_RIGHT);
                    break;
                case SwipeDirBinding.SwipeDir.Up:
                    touchDirSwipeAction.ChangedProperties.Add(TouchpadDirectionalSwipe.PropertyKeyStrings.PAD_DIR_UP);
                    break;
                case SwipeDirBinding.SwipeDir.Down:
                    touchDirSwipeAction.ChangedProperties.Add(TouchpadDirectionalSwipe.PropertyKeyStrings.PAD_DIR_DOWN);
                    break;
                default:
                    break;
            }
        }

        // Pre-serialize
        private void PopulateFuncs()
        {
            List<ActionFuncSerializer> tempFuncs = new List<ActionFuncSerializer>();
            TouchpadDirectionalSwipe.SwipeAxisXDir[] tempDirsX = new TouchpadDirectionalSwipe.SwipeAxisXDir[]
            {
                TouchpadDirectionalSwipe.SwipeAxisXDir.Left,
                TouchpadDirectionalSwipe.SwipeAxisXDir.Right,
            };

            TouchpadDirectionalSwipe.SwipeAxisYDir[] tempDirsY = new TouchpadDirectionalSwipe.SwipeAxisYDir[]
            {
                TouchpadDirectionalSwipe.SwipeAxisYDir.Up,
                TouchpadDirectionalSwipe.SwipeAxisYDir.Down,
            };

            for (int i = 0; i < tempDirsX.Length; i++)
            {
                TouchpadDirectionalSwipe.SwipeAxisXDir tempDir = tempDirsX[i];
                ButtonAction dirButton = touchDirSwipeAction.UsedEventsButtonsX[(int)tempDir];

                tempFuncs.Clear();
                foreach (ActionFunc tempFunc in dirButton.ActionFuncs)
                {
                    ActionFuncSerializer tempSerializer =
                        ActionFuncSerializerFactory.CreateSerializer(tempFunc);
                    if (tempSerializer != null)
                    {
                        tempFuncs.Add(tempSerializer);
                    }
                }

                SwipeDirBinding.SwipeDir swipeDir = SwipeDirBinding.SwipeDir.Left;
                if (tempDir == TouchpadDirectionalSwipe.SwipeAxisXDir.Left)
                {
                    swipeDir = SwipeDirBinding.SwipeDir.Left;
                }
                else if (tempDir == TouchpadDirectionalSwipe.SwipeAxisXDir.Right)
                {
                    swipeDir = SwipeDirBinding.SwipeDir.Right;
                }

                dictDirBindings.Add(swipeDir, new SwipeDirBinding()
                {
                    ActionDirName = dirButton.Name,
                    ActionFuncSerializers = new List<ActionFuncSerializer>(tempFuncs),
                });
            }

            for (int i = 0; i < tempDirsY.Length; i++)
            {
                TouchpadDirectionalSwipe.SwipeAxisYDir tempDir = tempDirsY[i];
                ButtonAction dirButton = touchDirSwipeAction.UsedEventsButtonsY[(int)tempDir];

                tempFuncs.Clear();
                foreach (ActionFunc tempFunc in dirButton.ActionFuncs)
                {
                    ActionFuncSerializer tempSerializer =
                        ActionFuncSerializerFactory.CreateSerializer(tempFunc);
                    if (tempSerializer != null)
                    {
                        tempFuncs.Add(tempSerializer);
                    }
                }

                SwipeDirBinding.SwipeDir swipeDir = SwipeDirBinding.SwipeDir.Left;
                if (tempDir == TouchpadDirectionalSwipe.SwipeAxisYDir.Up)
                {
                    swipeDir = SwipeDirBinding.SwipeDir.Up;
                }
                else if (tempDir == TouchpadDirectionalSwipe.SwipeAxisYDir.Down)
                {
                    swipeDir = SwipeDirBinding.SwipeDir.Down;
                }

                dictDirBindings.Add(swipeDir, new SwipeDirBinding()
                {
                    ActionDirName = dirButton.Name,
                    ActionFuncSerializers = new List<ActionFuncSerializer>(tempFuncs),
                });
            }
        }

        private void Settings_DelayTimeChanged(object sender, EventArgs e)
        {
            touchDirSwipeAction.ChangedProperties.Add(TouchpadDirectionalSwipe.PropertyKeyStrings.DELAY_TIME);
        }

        private void Settings_DeadZoneYChanged(object sender, EventArgs e)
        {
            touchDirSwipeAction.ChangedProperties.Add(TouchpadDirectionalSwipe.PropertyKeyStrings.DEAD_ZONE_Y);
        }

        private void Settings_DeadZoneXChanged(object sender, EventArgs e)
        {
            touchDirSwipeAction.ChangedProperties.Add(TouchpadDirectionalSwipe.PropertyKeyStrings.DEAD_ZONE_X);
        }

        private void TouchDirectionalSwipeSerializer_NameChanged(object sender, EventArgs e)
        {
            touchDirSwipeAction.ChangedProperties.Add(TouchpadDirectionalSwipe.PropertyKeyStrings.NAME);
        }
    }

    public class TouchpadSingleButtonSerializer : MapActionSerializer
    {
        public class TouchClickBinding
        {
            private List<ActionFuncSerializer> actionFuncSerializers =
                new List<ActionFuncSerializer>();
            [JsonProperty("Functions")]
            public List<ActionFuncSerializer> ActionFuncSerializers
            {
                get => actionFuncSerializers;
                set
                {
                    actionFuncSerializers = value;
                    ActionFuncSerializersChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler ActionFuncSerializersChanged;
            // TODO: Decide whether Functions property should be always required or not.
            // Conflicting statements here
            public bool ShouldSerializeActionFuncSerializers()
            {
                return actionFuncSerializers != null &&
                    actionFuncSerializers.Count > 0;
            }
        }

        public class TouchpadSingleButtonSettings
        {
            private TouchpadSingleButton touchpadSingleBtnAct;

            public double DeadZone
            {
                get => touchpadSingleBtnAct.DeadMod.DeadZone;
                set
                {
                    touchpadSingleBtnAct.DeadMod.DeadZone = Math.Clamp(value, 0.0, 1.0);
                    DeadZoneChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler DeadZoneChanged;

            public TouchpadSingleButtonSettings(TouchpadSingleButton action)
            {
                touchpadSingleBtnAct = action;
            }
        }

        private TouchpadSingleButton buttonAction = new TouchpadSingleButton();

        private List<ActionFuncSerializer> actionFuncSerializers =
                new List<ActionFuncSerializer>();
        [JsonProperty("Functions", Required = Required.Default)]
        public List<ActionFuncSerializer> ActionFuncSerializers
        {
            get => actionFuncSerializers;
            set
            {
                actionFuncSerializers = value;
                ActionFuncSerializersChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        public event EventHandler ActionFuncSerializersChanged;

        private TouchClickBinding clickBinding = new TouchClickBinding();

        [JsonProperty("Click", Required = Required.Default)]
        public TouchClickBinding ClickBinding
        {
            get => clickBinding;
            set
            {
                clickBinding = value;
            }
        }
        public bool ShouldSerializeClickBinding()
        {
            return buttonAction.ChangedProperties.Contains(TouchpadSingleButton.PropertyKeyStrings.FUNCTIONS_CLICK);
        }


        private TouchpadSingleButtonSettings settings;
        public TouchpadSingleButtonSettings Settings
        {
            get => settings;
            set => settings = value;
        }

        // De-serialize
        public TouchpadSingleButtonSerializer() : base()
        {
            mapAction = buttonAction;
            settings = new TouchpadSingleButtonSettings(buttonAction);

            NameChanged += TouchpadSingleButtonSerializer_NameChanged;
            ActionFuncSerializersChanged += TouchpadSingleButtonSerializer_ActionFuncSerializersChanged;
            clickBinding.ActionFuncSerializersChanged += ClickBinding_ActionFuncSerializersChanged;
            settings.DeadZoneChanged += Settings_DeadZoneChanged;
        }

        // Pre-serialize
        public TouchpadSingleButtonSerializer(ActionLayer tempLayer, MapAction mapAction) :
            base(tempLayer, mapAction)
        {
            if (mapAction is TouchpadSingleButton temp)
            {
                buttonAction = temp;
                this.mapAction = buttonAction;
                settings = new TouchpadSingleButtonSettings(buttonAction);
                PopulateFuncs();
            }
        }

        // Serialize
        private void PopulateFuncs()
        {
            if (!buttonAction.UseParentActions)
            {
                foreach (ActionFunc tempFunc in buttonAction.EventButton.ActionFuncs)
                {
                    ActionFuncSerializer tempSerializer =
                        ActionFuncSerializerFactory.CreateSerializer(tempFunc);
                    if (tempSerializer != null)
                    {
                        actionFuncSerializers.Add(tempSerializer);
                    }
                }
            }

            if (!buttonAction.UseParentClickActions)
            {
                foreach (ActionFunc tempFunc in buttonAction.ClickEventButton.ActionFuncs)
                {
                    ActionFuncSerializer tempSerializer =
                        ActionFuncSerializerFactory.CreateSerializer(tempFunc);
                    if (tempSerializer != null)
                    {
                        clickBinding.ActionFuncSerializers.Add(tempSerializer);
                    }
                }
            }
        }

        // Deserialize
        public override void PopulateMap()
        {
            buttonAction.EventButton.ActionFuncs.Clear();
            foreach (ActionFuncSerializer serializer in actionFuncSerializers)
            {
                serializer.PopulateFunc();
                buttonAction.EventButton.ActionFuncs.Add(serializer.ActionFunc);
            }

            buttonAction.ClickEventButton.ActionFuncs.Clear();
            foreach (ActionFuncSerializer serializer in clickBinding.ActionFuncSerializers)
            {
                serializer.PopulateFunc();
                buttonAction.ClickEventButton.ActionFuncs.Add(serializer.ActionFunc);
            }
        }

        private void ClickBinding_ActionFuncSerializersChanged(object sender, EventArgs e)
        {
            buttonAction.ChangedProperties.Add(TouchpadSingleButton.PropertyKeyStrings.FUNCTIONS_CLICK);
        }

        private void Settings_DeadZoneChanged(object sender, EventArgs e)
        {
            buttonAction.ChangedProperties.Add(TouchpadSingleButton.PropertyKeyStrings.DEAD_ZONE);
        }

        private void TouchpadSingleButtonSerializer_ActionFuncSerializersChanged(object sender, EventArgs e)
        {
            buttonAction.ChangedProperties.Add(TouchpadSingleButton.PropertyKeyStrings.FUNCTIONS);
        }

        private void TouchpadSingleButtonSerializer_NameChanged(object sender, EventArgs e)
        {
            buttonAction.ChangedProperties.Add(TouchpadSingleButton.PropertyKeyStrings.NAME);
        }
    }

    public class TouchpadNoActionSerializer : MapActionSerializer
    {
        private TouchpadNoAction touchNoAction = new TouchpadNoAction();

        public TouchpadNoActionSerializer() : base()
        {
            mapAction = touchNoAction;
        }

        public TouchpadNoActionSerializer(ActionLayer tempLayer, MapAction action) :
            base(tempLayer, action)
        {
            if (action is TouchpadNoAction temp)
            {
                touchNoAction = temp;
                mapAction = touchNoAction;
            }
        }
    }


    public class TouchpadFlickStickActionSerializer : MapActionSerializer
    {
        public class FlickStickSettings
        {
            public double RealWorldCalibration
            {
                get => flickAction.RealWorldCalibration;
                set
                {
                    flickAction.RealWorldCalibration = value;
                    RealWorldCalibrationChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler RealWorldCalibrationChanged;

            public bool ShouldSerializeRealWorldCalibration() => false;

            public double FlickThreshold
            {
                get => flickAction.FlickThreshold;
                set
                {
                    flickAction.FlickThreshold = value;
                    FlickThresholdChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler FlickThresholdChanged;

            public bool ShouldSerializeFlickThreshold()
            {
                return flickAction.ChangedProperties.Contains(TouchpadFlickStick.PropertyKeyStrings.FLICK_THRESHOLD);
            }

            public double FlickTime
            {
                get => flickAction.FlickTime;
                set
                {
                    flickAction.FlickTime = value;
                    FlickTimeChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler FlickTimeChanged;

            public bool ShouldSerializeFlickTime()
            {
                return flickAction.ChangedProperties.Contains(TouchpadFlickStick.PropertyKeyStrings.FLICK_TIME);
            }

            public double FlickTimeExponent
            {
                get => flickAction.FlickTimeExponent;
                set
                {
                    flickAction.FlickTimeExponent = value;
                    FlickTimeExponentChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler FlickTimeExponentChanged;

            public bool ShouldSerializeFlickTimeExponent()
            {
                return flickAction.ChangedProperties.Contains(TouchpadFlickStick.PropertyKeyStrings.FLICK_TIME_EXPONENT);
            }

            public double MinAngleThreshold
            {
                get => flickAction.MinAngleThreshold;
                set
                {
                    flickAction.MinAngleThreshold = value;
                    MinAngleThresholdChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler MinAngleThresholdChanged;

            public bool ShouldSerializeMinAngleThreshold()
            {
                return flickAction.ChangedProperties.Contains(TouchpadFlickStick.PropertyKeyStrings.MIN_ANGLE_THRESHOLD);
            }

            public double InGameSens
            {
                get => flickAction.InGameSens;
                set
                {
                    flickAction.InGameSens = value;
                    InGameSensChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler InGameSensChanged;

            public bool ShouldSerializeInGameSens() => false;

            public double ReleaseDampeningSpeed
            {
                get => flickAction.ReleaseDampeningSpeed;
                set
                {
                    flickAction.ReleaseDampeningSpeed = value;
                    ReleaseDampeningSpeedChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler ReleaseDampeningSpeedChanged;

            public bool ShouldSerializeReleaseDampeningSpeed()
            {
                return flickAction.ChangedProperties.Contains(TouchpadFlickStick.PropertyKeyStrings.RELEASE_DAMPENING_SPEED);
            }

            public bool MultiplierCompensation
            {
                get => flickAction.MultiplierCompensation;
                set
                {
                    flickAction.MultiplierCompensation = value;
                    MultiplierCompensationChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler MultiplierCompensationChanged;

            public bool ShouldSerializeMultiplierCompensation()
            {
                return flickAction.ChangedProperties.Contains(TouchpadFlickStick.PropertyKeyStrings.MULTIPLIER_COMPENSATION);
            }

            public double AccelerationMultiplier
            {
                get => flickAction.AccelerationMultiplier;
                set
                {
                    flickAction.AccelerationMultiplier = value;
                    AccelerationMultiplierChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler AccelerationMultiplierChanged;

            public bool ShouldSerializeAccelerationMultiplier()
            {
                return flickAction.MultiplierCompensation ||
                    flickAction.ChangedProperties.Contains(TouchpadFlickStick.PropertyKeyStrings.ACCELERATION_MULTIPLIER);
            }

            [JsonConverter(typeof(StringEnumConverter))]
            public FlickSnapAngle SnapAngle
            {
                get => flickAction.SnapAngle;
                set
                {
                    flickAction.SnapAngle = value;
                    SnapAngleChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler SnapAngleChanged;

            public bool ShouldSerializeSnapAngle()
            {
                return flickAction.ChangedProperties.Contains(TouchpadFlickStick.PropertyKeyStrings.SNAP_ANGLE);
            }

            public double SnapStrength
            {
                get => flickAction.SnapStrength;
                set
                {
                    flickAction.SnapStrength = value;
                    SnapStrengthChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler SnapStrengthChanged;

            public bool ShouldSerializeSnapStrength()
            {
                return flickAction.ChangedProperties.Contains(TouchpadFlickStick.PropertyKeyStrings.SNAP_STRENGTH);
            }

            public FlickStickSettings(TouchpadFlickStick flickAction)
            {
                this.flickAction = flickAction;
            }

            private TouchpadFlickStick flickAction;
        }

        private TouchpadFlickStick flickAction = new TouchpadFlickStick();
        private FlickStickSettings settings;
        public FlickStickSettings Settings { get => settings; set => settings = value; }

        public TouchpadFlickStickActionSerializer() : base()
        {
            mapAction = flickAction;
            settings = new FlickStickSettings(flickAction);


            NameChanged += TouchpadFlickStickActionSerializer_NameChanged;
            settings.RealWorldCalibrationChanged += Settings_RealWorldCalibrationChanged;
            settings.FlickThresholdChanged += Settings_FlickThresholdChanged;
            settings.FlickTimeChanged += Settings_FlickTimeChanged;
            settings.FlickTimeExponentChanged += Settings_FlickTimeExponentChanged;
            settings.MinAngleThresholdChanged += Settings_MinAngleThresholdChanged;
            settings.InGameSensChanged += Settings_InGameSensChanged;
            settings.ReleaseDampeningSpeedChanged += Settings_ReleaseDampeningSpeedChanged;
            settings.MultiplierCompensationChanged += Settings_MultiplierCompensationChanged;
            settings.AccelerationMultiplierChanged += Settings_AccelerationMultiplierChanged;
            settings.SnapAngleChanged += Settings_SnapAngleChanged;
            settings.SnapStrengthChanged += Settings_SnapStrengthChanged;
        }

        private void Settings_SnapAngleChanged(object sender, EventArgs e)
        {
            flickAction.ChangedProperties.Add(TouchpadFlickStick.PropertyKeyStrings.SNAP_ANGLE);
        }

        private void Settings_SnapStrengthChanged(object sender, EventArgs e)
        {
            flickAction.ChangedProperties.Add(TouchpadFlickStick.PropertyKeyStrings.SNAP_STRENGTH);
        }

        public TouchpadFlickStickActionSerializer(ActionLayer tempLayer, MapAction action) :
            base(tempLayer, action)
        {
            if (action is TouchpadFlickStick temp)
            {
                flickAction = temp;
                mapAction = flickAction;
                settings = new FlickStickSettings(flickAction);
            }
        }

        private void TouchpadFlickStickActionSerializer_NameChanged(object sender, EventArgs e)
        {
            flickAction.ChangedProperties.Add(TouchpadFlickStick.PropertyKeyStrings.NAME);
        }

        private void Settings_RealWorldCalibrationChanged(object sender, EventArgs e)
        {
            flickAction.ChangedProperties.Add(TouchpadFlickStick.PropertyKeyStrings.REAL_WORLD_CALIBRATION);
        }

        private void Settings_InGameSensChanged(object sender, EventArgs e)
        {
            flickAction.ChangedProperties.Add(TouchpadFlickStick.PropertyKeyStrings.IN_GAME_SENS);
        }

        private void Settings_MinAngleThresholdChanged(object sender, EventArgs e)
        {
            flickAction.ChangedProperties.Add(TouchpadFlickStick.PropertyKeyStrings.MIN_ANGLE_THRESHOLD);
        }

        private void Settings_FlickTimeChanged(object sender, EventArgs e)
        {
            flickAction.ChangedProperties.Add(TouchpadFlickStick.PropertyKeyStrings.FLICK_TIME);
        }

        private void Settings_FlickTimeExponentChanged(object sender, EventArgs e)
        {
            flickAction.ChangedProperties.Add(TouchpadFlickStick.PropertyKeyStrings.FLICK_TIME_EXPONENT);
        }

        private void Settings_FlickThresholdChanged(object sender, EventArgs e)
        {
            flickAction.ChangedProperties.Add(TouchpadFlickStick.PropertyKeyStrings.FLICK_THRESHOLD);
        }

        private void Settings_ReleaseDampeningSpeedChanged(object sender, EventArgs e)
        {
            flickAction.ChangedProperties.Add(TouchpadFlickStick.PropertyKeyStrings.RELEASE_DAMPENING_SPEED);
        }

        private void Settings_MultiplierCompensationChanged(object sender, EventArgs e)
        {
            flickAction.ChangedProperties.Add(TouchpadFlickStick.PropertyKeyStrings.MULTIPLIER_COMPENSATION);
        }

        private void Settings_AccelerationMultiplierChanged(object sender, EventArgs e)
        {
            flickAction.ChangedProperties.Add(TouchpadFlickStick.PropertyKeyStrings.ACCELERATION_MULTIPLIER);
        }
    }


    public class AxisDirButtonSerializer : MapActionSerializer
    {
        private ButtonActions.AxisDirButton axisDirButton =
            new ButtonActions.AxisDirButton();

        private List<ActionFuncSerializer> actionFuncSerializers =
           new List<ActionFuncSerializer>();
        [JsonProperty("Functions", Required = Required.Always)]
        public List<ActionFuncSerializer> ActionFuncSerializers
        {
            get => actionFuncSerializers;
            set => actionFuncSerializers = value;
        }

        public AxisDirButtonSerializer() : base()
        {
            mapAction = axisDirButton;
        }

        public AxisDirButtonSerializer(ActionLayer tempLayer, MapAction mapAction) :
            base(tempLayer, mapAction)
        {
            if (mapAction is ButtonActions.AxisDirButton temp)
            {
                axisDirButton = temp;
                this.mapAction = axisDirButton;
                PopulateFuncs();
            }
        }

        private void PopulateFuncs()
        {
            foreach (ActionFunc tempFunc in axisDirButton.ActionFuncs)
            {
                ActionFuncSerializer tempSerializer =
                        ActionFuncSerializerFactory.CreateSerializer(tempFunc);
                if (tempSerializer != null)
                {
                    actionFuncSerializers.Add(tempSerializer);
                }
            }
        }

        public override void PopulateMap()
        {
            axisDirButton.ActionFuncs.Clear();
            foreach (ActionFuncSerializer serializer in actionFuncSerializers)
            {
                serializer.PopulateFunc();
                axisDirButton.ActionFuncs.Add(serializer.ActionFunc);
            }
        }
    }

    public class StickFlickStickActionSerializer : MapActionSerializer
    {
        public class FlickStickSettings
        {
            public FlickStickSubMode SubMode
            {
                get => flickAction.SubMode;
                set
                {
                    flickAction.SubMode = value;
                    SubModeChanged?.Invoke(this, EventArgs.Empty);
                }
            }

            [JsonConverter(typeof(StringEnumConverter))]
            public FlickSnapAngle SnapAngle
            {
                get => flickAction.SnapAngle;
                set
                {
                    flickAction.SnapAngle = value;
                    SnapAngleChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler SnapAngleChanged;

            public bool ShouldSerializeSnapAngle()
            {
                return flickAction.ChangedProperties.Contains(StickFlickStick.PropertyKeyStrings.SNAP_ANGLE);
            }

            public double SnapStrength
            {
                get => flickAction.SnapStrength;
                set
                {
                    flickAction.SnapStrength = value;
                    SnapStrengthChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler SnapStrengthChanged;

            public bool ShouldSerializeSnapStrength()
            {
                return flickAction.ChangedProperties.Contains(StickFlickStick.PropertyKeyStrings.SNAP_STRENGTH);
            }

            public double RotateSmoothOverride
            {
                get => flickAction.RotateSmoothOverride;
                set
                {
                    flickAction.RotateSmoothOverride = value;
                    RotateSmoothOverrideChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler RotateSmoothOverrideChanged;

            public bool ShouldSerializeRotateSmoothOverride()
            {
                return flickAction.ChangedProperties.Contains(StickFlickStick.PropertyKeyStrings.ROTATE_SMOOTH_OVERRIDE);
            }
            public event EventHandler SubModeChanged;

            public bool ShouldSerializeSubMode()
            {
                return flickAction.ChangedProperties.Contains(StickFlickStick.PropertyKeyStrings.SUB_MODE);
            }

            public double RealWorldCalibration
            {
                get => flickAction.RealWorldCalibration;
                set
                {
                    flickAction.RealWorldCalibration = value;
                    RealWorldCalibrationChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler RealWorldCalibrationChanged;

            public bool ShouldSerializeRealWorldCalibration() => false;

            public double FlickThreshold
            {
                get => flickAction.FlickThreshold;
                set
                {
                    flickAction.FlickThreshold = value;
                    FlickThresholdChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler FlickThresholdChanged;

            public bool ShouldSerializeFlickThreshold()
            {
                return flickAction.ChangedProperties.Contains(StickFlickStick.PropertyKeyStrings.FLICK_THRESHOLD);
            }

            public double FlickTime
            {
                get => flickAction.FlickTime;
                set
                {
                    flickAction.FlickTime = value;
                    FlickTimeChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler FlickTimeChanged;

            public bool ShouldSerializeFlickTime()
            {
                return flickAction.ChangedProperties.Contains(StickFlickStick.PropertyKeyStrings.FLICK_TIME);
            }

            public double FlickTimeExponent
            {
                get => flickAction.FlickTimeExponent;
                set
                {
                    flickAction.FlickTimeExponent = value;
                    FlickTimeExponentChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler FlickTimeExponentChanged;

            public bool ShouldSerializeFlickTimeExponent()
            {
                return flickAction.ChangedProperties.Contains(StickFlickStick.PropertyKeyStrings.FLICK_TIME_EXPONENT);
            }

            public double MinAngleThreshold
            {
                get => flickAction.MinAngleThreshold;
                set
                {
                    flickAction.MinAngleThreshold = value;
                    MinAngleThresholdChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler MinAngleThresholdChanged;

            public bool ShouldSerializeMinAngleThreshold()
            {
                return flickAction.ChangedProperties.Contains(StickFlickStick.PropertyKeyStrings.MIN_ANGLE_THRESHOLD);
            }

            public double InGameSens
            {
                get => flickAction.InGameSens;
                set
                {
                    flickAction.InGameSens = value;
                    InGameSensChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler InGameSensChanged;

            public bool ShouldSerializeInGameSens() => false;

            public double ReleaseDampeningSpeed
            {
                get => flickAction.ReleaseDampeningSpeed;
                set
                {
                    flickAction.ReleaseDampeningSpeed = value;
                    ReleaseDampeningSpeedChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler ReleaseDampeningSpeedChanged;

            public bool ShouldSerializeReleaseDampeningSpeed()
            {
                return flickAction.ChangedProperties.Contains(StickFlickStick.PropertyKeyStrings.RELEASE_DAMPENING_SPEED);
            }

            public bool MultiplierCompensation
            {
                get => flickAction.MultiplierCompensation;
                set
                {
                    flickAction.MultiplierCompensation = value;
                    MultiplierCompensationChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler MultiplierCompensationChanged;

            public bool ShouldSerializeMultiplierCompensation()
            {
                return flickAction.ChangedProperties.Contains(StickFlickStick.PropertyKeyStrings.MULTIPLIER_COMPENSATION);
            }

            public double AccelerationMultiplier
            {
                get => flickAction.AccelerationMultiplier;
                set
                {
                    flickAction.AccelerationMultiplier = value;
                    AccelerationMultiplierChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler AccelerationMultiplierChanged;

            public bool ShouldSerializeAccelerationMultiplier()
            {
                return flickAction.MultiplierCompensation ||
                    flickAction.ChangedProperties.Contains(StickFlickStick.PropertyKeyStrings.ACCELERATION_MULTIPLIER);
            }

            public FlickStickSettings(StickFlickStick flickAction)
            {
                this.flickAction = flickAction;
            }

            private StickFlickStick flickAction;
        }

        private StickFlickStick flickAction = new StickFlickStick();
        private FlickStickSettings settings;
        public FlickStickSettings Settings { get => settings; set => settings = value; }

        public StickFlickStickActionSerializer() : base()
        {
            mapAction = flickAction;
            settings = new FlickStickSettings(flickAction);

            NameChanged += StickFlickStickActionSerializer_NameChanged;
            settings.SubModeChanged += Settings_SubModeChanged;
            settings.RealWorldCalibrationChanged += Settings_RealWorldCalibrationChanged;
            settings.FlickThresholdChanged += Settings_FlickThresholdChanged;
            settings.FlickTimeChanged += Settings_FlickTimeChanged;
            settings.FlickTimeExponentChanged += Settings_FlickTimeExponentChanged;
            settings.MinAngleThresholdChanged += Settings_MinAngleThresholdChanged;
            settings.InGameSensChanged += Settings_InGameSensChanged;
            settings.ReleaseDampeningSpeedChanged += Settings_ReleaseDampeningSpeedChanged;
            settings.MultiplierCompensationChanged += Settings_MultiplierCompensationChanged;
            settings.AccelerationMultiplierChanged += Settings_AccelerationMultiplierChanged;
            settings.RotateSmoothOverrideChanged += Settings_RotateSmoothOverrideChanged;
            settings.SnapAngleChanged += Settings_SnapAngleChanged;
            settings.SnapStrengthChanged += Settings_SnapStrengthChanged;
        }

        private void Settings_SnapAngleChanged(object sender, EventArgs e)
        {
            flickAction.ChangedProperties.Add(StickFlickStick.PropertyKeyStrings.SNAP_ANGLE);
        }

        private void Settings_SnapStrengthChanged(object sender, EventArgs e)
        {
            flickAction.ChangedProperties.Add(StickFlickStick.PropertyKeyStrings.SNAP_STRENGTH);
        }

        public StickFlickStickActionSerializer(ActionLayer tempLayer, MapAction action) :
            base(tempLayer, action)
        {
            if (action is StickFlickStick temp)
            {
                flickAction = temp;
                mapAction = flickAction;
                settings = new FlickStickSettings(flickAction);
            }
        }

        private void StickFlickStickActionSerializer_NameChanged(object sender, EventArgs e)
        {
            flickAction.ChangedProperties.Add(StickFlickStick.PropertyKeyStrings.NAME);
        }

        private void Settings_SubModeChanged(object sender, EventArgs e)
        {
            flickAction.ChangedProperties.Add(StickFlickStick.PropertyKeyStrings.SUB_MODE);
        }

        private void Settings_RealWorldCalibrationChanged(object sender, EventArgs e)
        {
            flickAction.ChangedProperties.Add(StickFlickStick.PropertyKeyStrings.REAL_WORLD_CALIBRATION);
        }

        private void Settings_InGameSensChanged(object sender, EventArgs e)
        {
            flickAction.ChangedProperties.Add(StickFlickStick.PropertyKeyStrings.IN_GAME_SENS);
        }

        private void Settings_MinAngleThresholdChanged(object sender, EventArgs e)
        {
            flickAction.ChangedProperties.Add(StickFlickStick.PropertyKeyStrings.MIN_ANGLE_THRESHOLD);
        }

        private void Settings_FlickTimeChanged(object sender, EventArgs e)
        {
            flickAction.ChangedProperties.Add(StickFlickStick.PropertyKeyStrings.FLICK_TIME);
        }

        private void Settings_FlickTimeExponentChanged(object sender, EventArgs e)
        {
            flickAction.ChangedProperties.Add(StickFlickStick.PropertyKeyStrings.FLICK_TIME_EXPONENT);
        }

        private void Settings_FlickThresholdChanged(object sender, EventArgs e)
        {
            flickAction.ChangedProperties.Add(StickFlickStick.PropertyKeyStrings.FLICK_THRESHOLD);
        }

        private void Settings_ReleaseDampeningSpeedChanged(object sender, EventArgs e)
        {
            flickAction.ChangedProperties.Add(StickFlickStick.PropertyKeyStrings.RELEASE_DAMPENING_SPEED);
        }

        private void Settings_MultiplierCompensationChanged(object sender, EventArgs e)
        {
            flickAction.ChangedProperties.Add(StickFlickStick.PropertyKeyStrings.MULTIPLIER_COMPENSATION);
        }

        private void Settings_AccelerationMultiplierChanged(object sender, EventArgs e)
        {
            flickAction.ChangedProperties.Add(StickFlickStick.PropertyKeyStrings.ACCELERATION_MULTIPLIER);
        }

        private void Settings_RotateSmoothOverrideChanged(object sender, EventArgs e)
        {
            flickAction.ChangedProperties.Add(StickFlickStick.PropertyKeyStrings.ROTATE_SMOOTH_OVERRIDE);
        }
    }

    public class StickNoActionSerializer : MapActionSerializer
    {
        private StickNoAction stickNoAction = new StickNoAction();

        public StickNoActionSerializer() : base()
        {
            mapAction = stickNoAction;
        }

        public StickNoActionSerializer(ActionLayer tempLayer, MapAction action) :
            base(tempLayer, action)
        {
            if (action is StickNoAction temp)
            {
                stickNoAction = temp;
                mapAction = stickNoAction;
            }
        }
    }

    public class StickPadActionSerializer : MapActionSerializer
    {
        public class StickPadDirBinding
        {
            //private StickPadAction.DpadDirections direction;
            //[JsonIgnore]
            //public StickPadAction.DpadDirections Direction
            //{
            //    get => direction;
            //}

            private string actionDirName;
            [JsonProperty("Name", Required = Required.Default)]
            public string ActionDirName
            {
                get => actionDirName;
                set => actionDirName = value;
            }
            public bool ShouldSerializeActionDirName()
            {
                return !string.IsNullOrEmpty(actionDirName);
            }

            private List<ActionFuncSerializer> actionFuncSerializers =
                new List<ActionFuncSerializer>();
            [JsonProperty("Functions", Required = Required.Always)]
            public List<ActionFuncSerializer> ActionFuncSerializers
            {
                get => actionFuncSerializers;
                set
                {
                    actionFuncSerializers = value;
                    ActionFuncSerializersChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler ActionFuncSerializersChanged;
        }

        public class StickPadActionSettings
        {
            private StickPadAction padAction;

            public bool SeparateAxisDeadZones
            {
                get => padAction.DeadMod.SeparateAxisDeadZones;
                set
                {
                    padAction.DeadMod.SeparateAxisDeadZones = value;
                    SeparateAxisDeadZonesChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler SeparateAxisDeadZonesChanged;
            public bool ShouldSerializeSeparateAxisDeadZones() =>
                padAction.ChangedProperties.Contains(StickPadAction.PropertyKeyStrings.SEPARATE_AXIS_DEAD_ZONES);

            public double DeadZoneX
            {
                get => padAction.DeadMod.DeadZoneX;
                set
                {
                    padAction.DeadMod.DeadZoneX = Math.Clamp(value, 0.0, 1.0);
                    DeadZoneXChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler DeadZoneXChanged;
            public bool ShouldSerializeDeadZoneX() =>
                padAction.ChangedProperties.Contains(StickPadAction.PropertyKeyStrings.DEAD_ZONE_X);

            public double DeadZoneY
            {
                get => padAction.DeadMod.DeadZoneY;
                set
                {
                    padAction.DeadMod.DeadZoneY = Math.Clamp(value, 0.0, 1.0);
                    DeadZoneYChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler DeadZoneYChanged;
            public bool ShouldSerializeDeadZoneY() =>
                padAction.ChangedProperties.Contains(StickPadAction.PropertyKeyStrings.DEAD_ZONE_Y);

            public double DeadZone
            {
                get => padAction.DeadMod.DeadZone;
                set
                {
                    padAction.DeadMod.DeadZone = Math.Clamp(value, 0.0, 1.0);
                    DeadZoneChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler DeadZoneChanged;
            public bool ShouldSerializeDeadZone()
            {
                return padAction.ChangedProperties.Contains(StickPadAction.PropertyKeyStrings.DEAD_ZONE);
            }

            //public StickPadAction.DPadMode PadMode
            public string PadMode
            {
                get => padAction.CurrentMode.ToString();
                set
                {
                    if (Enum.TryParse(value, out StickPadAction.DPadMode temp))
                    {
                        padAction.CurrentMode = temp;
                        PadModeChanged?.Invoke(this, EventArgs.Empty);
                    }
                }
            }
            public event EventHandler PadModeChanged;
            public bool ShouldSerializePadMode()
            {
                return padAction.ChangedProperties.Contains(StickPadAction.PropertyKeyStrings.PAD_MODE);
            }

            //public StickDeadZone.DeadZoneTypes DeadZoneType
            public string DeadZoneType
            {
                get => padAction.DeadMod.DeadZoneType.ToString();
                set
                {
                    if (Enum.TryParse(value, out StickDeadZone.DeadZoneTypes temp))
                    {
                        padAction.DeadMod.DeadZoneType = temp;
                        DeadZoneTypeChanged?.Invoke(this, EventArgs.Empty);
                    }
                }
            }
            public event EventHandler DeadZoneTypeChanged;
            public bool ShouldSerializeDeadZoneType()
            {
                return padAction.ChangedProperties.Contains(StickPadAction.PropertyKeyStrings.DEAD_ZONE_TYPE);
            }

            [JsonProperty("UseOuterRing")]
            public bool UseOuterRing
            {
                get => padAction.UseRingButton;
                set
                {
                    padAction.UseRingButton = value;
                    UseOuterRingChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler UseOuterRingChanged;
            public bool ShouldSerializeUseOuterRing()
            {
                return padAction.ChangedProperties.Contains(StickPadAction.PropertyKeyStrings.USE_OUTER_RING);
            }

            [JsonProperty("OuterRingDeadZone")]
            public double OuterRingDeadZone
            {
                get => padAction.OuterRingDeadZone;
                set
                {
                    padAction.OuterRingDeadZone = value;
                    OuterRingDeadZoneChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler OuterRingDeadZoneChanged;
            public bool ShouldSerializeOuterRingDeadZone()
            {
                return padAction.ChangedProperties.Contains(StickPadAction.PropertyKeyStrings.OUTER_RING_DEAD_ZONE);
            }

            [JsonProperty("UseAsOuterRing")]
            public bool UseAsOuterRing
            {
                get => padAction.UseAsOuterRing;
                set
                {
                    padAction.UseAsOuterRing = value;
                    UseAsOuterRingChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler UseAsOuterRingChanged;
            public bool ShouldSerializeUseAsOuterRing()
            {
                return padAction.ChangedProperties.Contains(StickPadAction.PropertyKeyStrings.USE_AS_OUTER_RING);
            }

            public int Rotation
            {
                get => padAction.Rotation;
                set
                {
                    padAction.Rotation = value;
                    RotationChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler RotationChanged;
            public bool ShouldSerializeRotation()
            {
                return padAction.ChangedProperties.Contains(StickPadAction.PropertyKeyStrings.ROTATION);
            }

            public int DiagonalRange
            {
                get => padAction.DiagonalRange;
                set
                {
                    padAction.DiagonalRange = value;
                    DiagonalRangeChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler DiagonalRangeChanged;
            public bool ShouldSerializeDiagonalRange()
            {
                return padAction.ChangedProperties.Contains(StickPadAction.PropertyKeyStrings.DIAGONAL_RANGE);
            }

            public bool CounterMovementReleasePressEnabled
            {
                get => padAction.CounterMovementReleasePress.Enabled;
                set
                {
                    padAction.CounterMovementReleasePress.Enabled = value;
                    CounterMovementReleasePressEnabledChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler CounterMovementReleasePressEnabledChanged;
            public bool ShouldSerializeCounterMovementReleasePressEnabled()
            {
                return padAction.ChangedProperties.Contains(StickPadAction.PropertyKeyStrings.COUNTER_MOVEMENT_ENABLED);
            }

            public bool UseArrowKeysForCounterMovementPresses
            {
                get => padAction.CounterMovementReleasePress.UseArrowKeysForCounterMovementPresses;
                set
                {
                    padAction.CounterMovementReleasePress.UseArrowKeysForCounterMovementPresses = value;
                    UseArrowKeysForCounterMovementPressesChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler UseArrowKeysForCounterMovementPressesChanged;
            public bool ShouldSerializeUseArrowKeysForCounterMovementPresses()
            {
                return padAction.ChangedProperties.Contains(StickPadAction.PropertyKeyStrings.COUNTER_MOVEMENT_USE_ARROW_KEYS);
            }

            public string CounterMovementTapLengthPreset
            {
                get => padAction.CounterMovementReleasePress.TapLengthPreset.ToString();
                set
                {
                    if (Enum.TryParse(value, out DS4MapperTest.StickActions.CounterMovementTapLengthPreset temp))
                    {
                        padAction.CounterMovementReleasePress.TapLengthPreset = temp;
                        CounterMovementTapLengthPresetChanged?.Invoke(this, EventArgs.Empty);
                    }
                }
            }
            public event EventHandler CounterMovementTapLengthPresetChanged;
            public bool ShouldSerializeCounterMovementTapLengthPreset()
            {
                return padAction.ChangedProperties.Contains(StickPadAction.PropertyKeyStrings.COUNTER_MOVEMENT_TAP_LENGTH_PRESET);
            }

            [JsonConverter(typeof(StringEnumConverter))]
            public OppositeTapLengthMode OppositeTapLengthMode
            {
                get => padAction.CounterMovementReleasePress.OppositeTapLengthMode;
                set
                {
                    padAction.CounterMovementReleasePress.OppositeTapLengthMode = value;
                    OppositeTapLengthModeChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler OppositeTapLengthModeChanged;
            public bool ShouldSerializeOppositeTapLengthMode()
            {
                return padAction.ChangedProperties.Contains(StickPadAction.PropertyKeyStrings.COUNTER_MOVEMENT_TAP_LENGTH_MODE);
            }

            public int OppositeTapLengthMs
            {
                get => padAction.CounterMovementReleasePress.OppositeTapLengthMs;
                set
                {
                    padAction.CounterMovementReleasePress.OppositeTapLengthMs = value;
                    OppositeTapLengthMsChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler OppositeTapLengthMsChanged;
            public bool ShouldSerializeOppositeTapLengthMs()
            {
                return padAction.ChangedProperties.Contains(StickPadAction.PropertyKeyStrings.COUNTER_MOVEMENT_TAP_LENGTH_FIXED_MS);
            }

            public int OppositeTapLengthVariancePercent
            {
                get => padAction.CounterMovementReleasePress.OppositeTapLengthVariancePercent;
                set
                {
                    padAction.CounterMovementReleasePress.OppositeTapLengthVariancePercent = value;
                    OppositeTapLengthVariancePercentChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler OppositeTapLengthVariancePercentChanged;
            public bool ShouldSerializeOppositeTapLengthVariancePercent()
            {
                return padAction.ChangedProperties.Contains(StickPadAction.PropertyKeyStrings.COUNTER_MOVEMENT_TAP_LENGTH_VARIANCE_PERCENT);
            }

            public int OppositeTapLengthMinimumMs
            {
                get => padAction.CounterMovementReleasePress.OppositeTapLengthMinimumMs;
                set
                {
                    padAction.CounterMovementReleasePress.OppositeTapLengthMinimumMs = value;
                    OppositeTapLengthMinimumMsChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler OppositeTapLengthMinimumMsChanged;
            public bool ShouldSerializeOppositeTapLengthMinimumMs()
            {
                return padAction.ChangedProperties.Contains(StickPadAction.PropertyKeyStrings.COUNTER_MOVEMENT_TAP_LENGTH_MIN_MS);
            }

            public int OppositeTapLengthMaximumMs
            {
                get => padAction.CounterMovementReleasePress.OppositeTapLengthMaximumMs;
                set
                {
                    padAction.CounterMovementReleasePress.OppositeTapLengthMaximumMs = value;
                    OppositeTapLengthMaximumMsChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler OppositeTapLengthMaximumMsChanged;
            public bool ShouldSerializeOppositeTapLengthMaximumMs()
            {
                return padAction.ChangedProperties.Contains(StickPadAction.PropertyKeyStrings.COUNTER_MOVEMENT_TAP_LENGTH_MAX_MS);
            }

            public int OppositeTapStartDelayMinimumMs
            {
                get => padAction.CounterMovementReleasePress.OppositeTapStartDelayMinimumMs;
                set
                {
                    padAction.CounterMovementReleasePress.OppositeTapStartDelayMinimumMs = value;
                    OppositeTapStartDelayMinimumMsChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler OppositeTapStartDelayMinimumMsChanged;
            public bool ShouldSerializeOppositeTapStartDelayMinimumMs()
            {
                return padAction.ChangedProperties.Contains(StickPadAction.PropertyKeyStrings.COUNTER_MOVEMENT_START_DELAY_MIN_MS);
            }

            public int OppositeTapStartDelayMaximumMs
            {
                get => padAction.CounterMovementReleasePress.OppositeTapStartDelayMaximumMs;
                set
                {
                    padAction.CounterMovementReleasePress.OppositeTapStartDelayMaximumMs = value;
                    OppositeTapStartDelayMaximumMsChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler OppositeTapStartDelayMaximumMsChanged;
            public bool ShouldSerializeOppositeTapStartDelayMaximumMs()
            {
                return padAction.ChangedProperties.Contains(StickPadAction.PropertyKeyStrings.COUNTER_MOVEMENT_START_DELAY_MAX_MS);
            }

            public int BrakeMinimumHoldMs
            {
                get => padAction.CounterMovementReleasePress.MinimumHoldMs;
                set
                {
                    padAction.CounterMovementReleasePress.MinimumHoldMs = value;
                    BrakeMinimumHoldMsChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler BrakeMinimumHoldMsChanged;
            public bool ShouldSerializeBrakeMinimumHoldMs()
            {
                return padAction.ChangedProperties.Contains(StickPadAction.PropertyKeyStrings.BRAKE_MIN_HOLD_MS);
            }

            public double BrakeArmingThreshold
            {
                get => padAction.CounterMovementReleasePress.ArmingThreshold;
                set
                {
                    padAction.CounterMovementReleasePress.ArmingThreshold = value;
                    BrakeArmingThresholdChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler BrakeArmingThresholdChanged;
            public bool ShouldSerializeBrakeArmingThreshold()
            {
                return padAction.ChangedProperties.Contains(StickPadAction.PropertyKeyStrings.BRAKE_ARMING_THRESHOLD);
            }

            // --- Legacy compatibility (pre-rename "Digital Release Brake" profiles) ---
            // Old profiles only ever wrote "BrakeEnabled"/"BrakeDurationMs". Captured here,
            // not applied directly, so PopulateMap (which runs after the whole object has
            // deserialized) can apply them using the old immediate-start semantics, and only
            // for whichever new-named fields this same profile did not already specify
            // explicitly under their new names. Never re-serialized.
            private bool? legacyBrakeEnabled;
            internal bool? LegacyBrakeEnabled => legacyBrakeEnabled;
            public bool BrakeEnabled
            {
                get => false;
                set => legacyBrakeEnabled = value;
            }
            public bool ShouldSerializeBrakeEnabled() => false;

            private int? legacyBrakeDurationMs;
            internal int? LegacyBrakeDurationMs => legacyBrakeDurationMs;
            public int BrakeDurationMs
            {
                get => 0;
                set => legacyBrakeDurationMs = value;
            }
            public bool ShouldSerializeBrakeDurationMs() => false;

            public StickPadActionSettings(StickPadAction padAction)
            {
                this.padAction = padAction;
            }
        }

        private StickPadAction stickPadAct =
            new StickPadAction();

        private Dictionary<StickPadAction.DpadDirections, StickPadDirBinding> dictPadBindings =
            new Dictionary<StickPadAction.DpadDirections, StickPadDirBinding>();

        [JsonProperty("Bindings", Required = Required.Always)]
        public Dictionary<StickPadAction.DpadDirections, StickPadDirBinding> DictPadBindings
        {
            get => dictPadBindings;
            set => dictPadBindings = value;
        }
        public bool ShouldSerializeDictPadBindings()
        {
            return dictPadBindings.Count > 0;
        }

        private StickPadDirBinding ringBinding;

        [JsonProperty("OuterRingBinding")]
        public StickPadDirBinding RingBinding
        {
            get => ringBinding;
            set
            {
                ringBinding = value;
                RingBindingChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        public event EventHandler RingBindingChanged;
        public bool ShouldSerializeRingBinding()
        {
            return stickPadAct.ChangedProperties.Contains(StickPadAction.PropertyKeyStrings.OUTER_RING_BUTTON);
        }

        //private List<StickPadBindings> padBindings =
        //    new List<StickPadBindings>()
        //    {
        //        new StickPadBindings(StickPadAction.DpadDirections.Centered),
        //        new StickPadBindings(StickPadAction.DpadDirections.Up),
        //        new StickPadBindings(StickPadAction.DpadDirections.Right),
        //        new StickPadBindings(StickPadAction.DpadDirections.UpRight),
        //        new StickPadBindings(StickPadAction.DpadDirections.Down),
        //        new StickPadBindings(StickPadAction.DpadDirections.Centered),
        //        new StickPadBindings(StickPadAction.DpadDirections.DownRight),
        //        new StickPadBindings(StickPadAction.DpadDirections.Centered),
        //        new StickPadBindings(StickPadAction.DpadDirections.Left),
        //        new StickPadBindings(StickPadAction.DpadDirections.UpLeft),
        //        new StickPadBindings(StickPadAction.DpadDirections.Centered),
        //        new StickPadBindings(StickPadAction.DpadDirections.Centered),
        //        new StickPadBindings(StickPadAction.DpadDirections.DownLeft),
        //    };
        //[JsonProperty("Bindings")]
        //public List<StickPadBindings> PadBindings
        //{
        //    get => padBindings;
        //    set => padBindings = value;
        //}

        //private List<AxisDirButtonSerializer> axisDirButtons =
        //    new List<AxisDirButtonSerializer>()
        //    {
        //        new AxisDirButtonSerializer(), new AxisDirButtonSerializer(),
        //        new AxisDirButtonSerializer(), new AxisDirButtonSerializer(),
        //        new AxisDirButtonSerializer(), new AxisDirButtonSerializer(),
        //        new AxisDirButtonSerializer(), new AxisDirButtonSerializer(),
        //        new AxisDirButtonSerializer(), new AxisDirButtonSerializer(),
        //        new AxisDirButtonSerializer(), new AxisDirButtonSerializer(),
        //        new AxisDirButtonSerializer()
        //    };

        //[JsonProperty("Up")]
        //public AxisDirButtonSerializer UpButton
        //{
        //    get => axisDirButtons[(int)StickPadAction.DpadDirections.Up];
        //}

        private StickPadActionSettings settings;
        public StickPadActionSettings Settings
        {
            get => settings;
            set => settings = value;
        }
        public bool ShouldSerializeSettings()
        {
            return stickPadAct.ChangedProperties.Count > 0;
        }

        // Deserialize
        public StickPadActionSerializer() : base()
        {
            mapAction = stickPadAct;
            settings = new StickPadActionSettings(stickPadAct);

            NameChanged += StickPadActionSerializer_NameChanged;
            RingBindingChanged += StickPadActionSerializer_RingBindingChanged;
            settings.PadModeChanged += Settings_PadModeChanged;
            settings.DeadZoneTypeChanged += Settings_DeadZoneTypeChanged;
            settings.DeadZoneChanged += Settings_DeadZoneChanged;
            settings.SeparateAxisDeadZonesChanged += Settings_SeparateAxisDeadZonesChanged;
            settings.DeadZoneXChanged += Settings_DeadZoneXChanged;
            settings.DeadZoneYChanged += Settings_DeadZoneYChanged;
            settings.UseOuterRingChanged += Settings_UseOuterRingChanged;
            settings.OuterRingDeadZoneChanged += Settings_OuterRingDeadZoneChanged;
            settings.UseAsOuterRingChanged += Settings_UseAsOuterRingChanged;
            settings.RotationChanged += Settings_RotationChanged;
            settings.DiagonalRangeChanged += Settings_DiagonalRangeChanged;
            settings.CounterMovementReleasePressEnabledChanged += Settings_CounterMovementReleasePressEnabledChanged;
            settings.UseArrowKeysForCounterMovementPressesChanged += Settings_UseArrowKeysForCounterMovementPressesChanged;
            settings.CounterMovementTapLengthPresetChanged += Settings_CounterMovementTapLengthPresetChanged;
            settings.OppositeTapLengthModeChanged += Settings_OppositeTapLengthModeChanged;
            settings.OppositeTapLengthMsChanged += Settings_OppositeTapLengthMsChanged;
            settings.OppositeTapLengthVariancePercentChanged += Settings_OppositeTapLengthVariancePercentChanged;
            settings.OppositeTapLengthMinimumMsChanged += Settings_OppositeTapLengthMinimumMsChanged;
            settings.OppositeTapLengthMaximumMsChanged += Settings_OppositeTapLengthMaximumMsChanged;
            settings.OppositeTapStartDelayMinimumMsChanged += Settings_OppositeTapStartDelayMinimumMsChanged;
            settings.OppositeTapStartDelayMaximumMsChanged += Settings_OppositeTapStartDelayMaximumMsChanged;
            settings.BrakeMinimumHoldMsChanged += Settings_BrakeMinimumHoldMsChanged;
            settings.BrakeArmingThresholdChanged += Settings_BrakeArmingThresholdChanged;
        }

        private void Settings_CounterMovementReleasePressEnabledChanged(object sender, EventArgs e)
        {
            stickPadAct.ChangedProperties.Add(StickPadAction.PropertyKeyStrings.COUNTER_MOVEMENT_ENABLED);
        }

        private void Settings_UseArrowKeysForCounterMovementPressesChanged(object sender, EventArgs e)
        {
            stickPadAct.ChangedProperties.Add(StickPadAction.PropertyKeyStrings.COUNTER_MOVEMENT_USE_ARROW_KEYS);
        }

        private void Settings_CounterMovementTapLengthPresetChanged(object sender, EventArgs e)
        {
            stickPadAct.ChangedProperties.Add(StickPadAction.PropertyKeyStrings.COUNTER_MOVEMENT_TAP_LENGTH_PRESET);
        }

        private void Settings_OppositeTapLengthModeChanged(object sender, EventArgs e)
        {
            stickPadAct.ChangedProperties.Add(StickPadAction.PropertyKeyStrings.COUNTER_MOVEMENT_TAP_LENGTH_MODE);
        }

        private void Settings_OppositeTapLengthMsChanged(object sender, EventArgs e)
        {
            stickPadAct.ChangedProperties.Add(StickPadAction.PropertyKeyStrings.COUNTER_MOVEMENT_TAP_LENGTH_FIXED_MS);
        }

        private void Settings_OppositeTapLengthVariancePercentChanged(object sender, EventArgs e)
        {
            stickPadAct.ChangedProperties.Add(StickPadAction.PropertyKeyStrings.COUNTER_MOVEMENT_TAP_LENGTH_VARIANCE_PERCENT);
        }

        private void Settings_OppositeTapLengthMinimumMsChanged(object sender, EventArgs e)
        {
            stickPadAct.ChangedProperties.Add(StickPadAction.PropertyKeyStrings.COUNTER_MOVEMENT_TAP_LENGTH_MIN_MS);
        }

        private void Settings_OppositeTapLengthMaximumMsChanged(object sender, EventArgs e)
        {
            stickPadAct.ChangedProperties.Add(StickPadAction.PropertyKeyStrings.COUNTER_MOVEMENT_TAP_LENGTH_MAX_MS);
        }

        private void Settings_OppositeTapStartDelayMinimumMsChanged(object sender, EventArgs e)
        {
            stickPadAct.ChangedProperties.Add(StickPadAction.PropertyKeyStrings.COUNTER_MOVEMENT_START_DELAY_MIN_MS);
        }

        private void Settings_OppositeTapStartDelayMaximumMsChanged(object sender, EventArgs e)
        {
            stickPadAct.ChangedProperties.Add(StickPadAction.PropertyKeyStrings.COUNTER_MOVEMENT_START_DELAY_MAX_MS);
        }

        private void Settings_BrakeMinimumHoldMsChanged(object sender, EventArgs e)
        {
            stickPadAct.ChangedProperties.Add(StickPadAction.PropertyKeyStrings.BRAKE_MIN_HOLD_MS);
        }

        private void Settings_BrakeArmingThresholdChanged(object sender, EventArgs e)
        {
            stickPadAct.ChangedProperties.Add(StickPadAction.PropertyKeyStrings.BRAKE_ARMING_THRESHOLD);
        }

        private void Settings_DeadZoneTypeChanged(object sender, EventArgs e)
        {
            stickPadAct.ChangedProperties.Add(StickPadAction.PropertyKeyStrings.DEAD_ZONE_TYPE);
        }

        private void Settings_DeadZoneChanged(object sender, EventArgs e)
        {
            stickPadAct.ChangedProperties.Add(StickPadAction.PropertyKeyStrings.DEAD_ZONE);
        }

        private void Settings_SeparateAxisDeadZonesChanged(object sender, EventArgs e)
        {
            stickPadAct.ChangedProperties.Add(StickPadAction.PropertyKeyStrings.SEPARATE_AXIS_DEAD_ZONES);
        }

        private void Settings_DeadZoneXChanged(object sender, EventArgs e)
        {
            stickPadAct.ChangedProperties.Add(StickPadAction.PropertyKeyStrings.DEAD_ZONE_X);
        }

        private void Settings_DeadZoneYChanged(object sender, EventArgs e)
        {
            stickPadAct.ChangedProperties.Add(StickPadAction.PropertyKeyStrings.DEAD_ZONE_Y);
        }

        private void Settings_DiagonalRangeChanged(object sender, EventArgs e)
        {
            stickPadAct.ChangedProperties.Add(StickPadAction.PropertyKeyStrings.DIAGONAL_RANGE);
        }

        private void Settings_RotationChanged(object sender, EventArgs e)
        {
            stickPadAct.ChangedProperties.Add(StickPadAction.PropertyKeyStrings.ROTATION);
        }

        // Pre-serialize
        public StickPadActionSerializer(ActionLayer tempLayer, MapAction mapAction) :
            base(tempLayer, mapAction)
        {
            if (mapAction is StickPadAction temp)
            {
                stickPadAct = temp;
                this.mapAction = stickPadAct;
                settings = new StickPadActionSettings(stickPadAct);
                PopulateFuncs();
            }
        }

        private void StickPadActionSerializer_NameChanged(object sender, EventArgs e)
        {
            stickPadAct.ChangedProperties.Add(StickPadAction.PropertyKeyStrings.NAME);
        }

        private void Settings_UseAsOuterRingChanged(object sender, EventArgs e)
        {
            stickPadAct.ChangedProperties.Add(StickPadAction.PropertyKeyStrings.USE_AS_OUTER_RING);
        }

        private void Settings_OuterRingDeadZoneChanged(object sender, EventArgs e)
        {
            stickPadAct.ChangedProperties.Add(StickPadAction.PropertyKeyStrings.OUTER_RING_DEAD_ZONE);
        }

        private void Settings_UseOuterRingChanged(object sender, EventArgs e)
        {
            stickPadAct.ChangedProperties.Add(StickPadAction.PropertyKeyStrings.USE_OUTER_RING);
        }

        private void StickPadActionSerializer_RingBindingChanged(object sender, EventArgs e)
        {
            stickPadAct.ChangedProperties.Add(StickPadAction.PropertyKeyStrings.OUTER_RING_BUTTON);
        }

        private void Settings_PadModeChanged(object sender, EventArgs e)
        {
            stickPadAct.ChangedProperties.Add(StickPadAction.PropertyKeyStrings.PAD_MODE);
        }

        // Pre-serialize
        private void PopulateFuncs()
        {
            List<ActionFuncSerializer> tempFuncs = new List<ActionFuncSerializer>();
            StickPadAction.DpadDirections[] tempDirs = null;

            if (stickPadAct.CurrentMode == StickPadAction.DPadMode.Standard ||
                stickPadAct.CurrentMode == StickPadAction.DPadMode.FourWayCardinal)
            {
                tempDirs = new StickPadAction.DpadDirections[4]
                {
                    StickPadAction.DpadDirections.Up, StickPadAction.DpadDirections.Down,
                    StickPadAction.DpadDirections.Left, StickPadAction.DpadDirections.Right
                };
            }
            else if (stickPadAct.CurrentMode == StickPadAction.DPadMode.EightWay)
            {
                tempDirs = new StickPadAction.DpadDirections[8]
                {
                    StickPadAction.DpadDirections.Up, StickPadAction.DpadDirections.Down,
                    StickPadAction.DpadDirections.Left, StickPadAction.DpadDirections.Right,
                    StickPadAction.DpadDirections.UpLeft, StickPadAction.DpadDirections.UpRight,
                    StickPadAction.DpadDirections.DownLeft, StickPadAction.DpadDirections.DownRight
                };
            }
            else if (stickPadAct.CurrentMode == StickPadAction.DPadMode.FourWayDiagonal)
            {
                tempDirs = new StickPadAction.DpadDirections[4]
                {
                    StickPadAction.DpadDirections.UpLeft, StickPadAction.DpadDirections.UpRight,
                    StickPadAction.DpadDirections.DownLeft, StickPadAction.DpadDirections.DownRight
                };
            }

            for (int i = 0; i < tempDirs.Length; i++)
            {
                StickPadAction.DpadDirections tempDir = tempDirs[i];
                AxisDirButton dirButton = stickPadAct.EventCodes4[(int)tempDir];

                tempFuncs.Clear();
                foreach (ActionFunc tempFunc in dirButton.ActionFuncs)
                {
                    ActionFuncSerializer tempSerializer =
                        ActionFuncSerializerFactory.CreateSerializer(tempFunc);
                    if (tempSerializer != null)
                    {
                        tempFuncs.Add(tempSerializer);
                    }
                }

                //dictPadBindings.Add(tempDir, tempFuncs);
                dictPadBindings.Add(tempDir,
                    new StickPadDirBinding()
                    {
                        ActionDirName = dirButton.Name,
                        ActionFuncSerializers = new List<ActionFuncSerializer>(tempFuncs),
                    });
            }

            if (stickPadAct.RingButton != null)
            {
                ringBinding = new StickPadDirBinding();
                ringBinding.ActionDirName = stickPadAct.RingButton.Name;
                foreach (ActionFunc tempFunc in stickPadAct.RingButton.ActionFuncs)
                {
                    ActionFuncSerializer tempSerializer =
                        ActionFuncSerializerFactory.CreateSerializer(tempFunc);
                    if (tempSerializer != null)
                    {
                        ringBinding.ActionFuncSerializers.Add(tempSerializer);
                    }
                }
            }
        }

        // Post-deserialize
        public override void PopulateMap()
        {
            foreach(AxisDirButton dirButton in stickPadAct.EventCodes4)
            {
                dirButton.ActionFuncs.Clear();
            }

            foreach (KeyValuePair<StickPadAction.DpadDirections, StickPadDirBinding> tempKeyPair in dictPadBindings)
            //foreach(KeyValuePair<StickPadAction.DpadDirections, List<ActionFuncSerializer>> tempKeyPair in dictPadBindings)
            //foreach(DictionaryEntry entry in dictPadBindings)
            {
                StickPadAction.DpadDirections dir = tempKeyPair.Key;
                List<ActionFuncSerializer> tempSerializers = tempKeyPair.Value.ActionFuncSerializers;
                //List<ActionFuncSerializer> tempSerializers = tempKeyPair.Value;
                //StickPadAction.DpadDirections dir = (StickPadAction.DpadDirections)entry.Key;
                //List<ActionFuncSerializer> tempSerializers = entry.Value as List<ActionFuncSerializer>;

                AxisDirButton tempDirButton = null;
                //foreach (AxisDirButton dirButton in stickPadAct.EventCodes4)
                {
                    tempDirButton = stickPadAct.EventCodes4[(int)dir];
                    if (tempDirButton != null)
                    {
                        tempDirButton.Name = tempKeyPair.Value.ActionDirName;
                    }
                }

                if (tempDirButton != null)
                {
                    foreach (ActionFuncSerializer serializer in tempSerializers)
                    {
                        serializer.PopulateFunc();
                        tempDirButton.ActionFuncs.Add(serializer.ActionFunc);
                    }

                    FlagBtnChangedDirection(dir, stickPadAct);
                    tempDirButton = null;
                }
            }

            if (ringBinding != null)
            {
                stickPadAct.RingButton.Name = ringBinding.ActionDirName;
                List<ActionFuncSerializer> tempSerializers = ringBinding.ActionFuncSerializers;
                foreach (ActionFuncSerializer serializer in tempSerializers)
                {
                    serializer.PopulateFunc();
                    stickPadAct.RingButton.ActionFuncs.Add(serializer.ActionFunc);
                }

                stickPadAct.ChangedProperties.Add(StickPadAction.PropertyKeyStrings.OUTER_RING_BUTTON);
                //stickPadAct.RingButton.ChangedProperties.Add(StickPadAction.PropertyKeyStrings.OUTER_RING_BUTTON);
            }

            MigrateLegacyCounterMovementReleasePressFields();
        }

        // Legacy "Digital Release Brake" profile migration: pre-rename profiles only ever
        // wrote BrakeEnabled/BrakeDurationMs. Applied here (after the whole object has
        // deserialized) using the old immediate-start semantics, i.e. the migrated tap
        // length window becomes min == max == the old single duration, with a 0-0ms start
        // delay so the generated press still begins immediately, matching pre-rename
        // behaviour exactly. Runs only for whichever new-named fields this same profile did
        // not already specify explicitly, so a legacy field can never silently overwrite a
        // value the profile also set directly under its new name.
        // See TouchpadActionPadSerializer.MigrateLegacyCounterMovementReleasePressFields for
        // the three profile generations this handles.
        private void MigrateLegacyCounterMovementReleasePressFields()
        {
            if (settings.LegacyBrakeEnabled.HasValue &&
                !stickPadAct.ChangedProperties.Contains(StickPadAction.PropertyKeyStrings.COUNTER_MOVEMENT_ENABLED))
            {
                stickPadAct.CounterMovementReleasePress.Enabled = settings.LegacyBrakeEnabled.Value;
                stickPadAct.ChangedProperties.Add(StickPadAction.PropertyKeyStrings.COUNTER_MOVEMENT_ENABLED);
            }

            bool modeFieldPresent = stickPadAct.ChangedProperties.Contains(StickPadAction.PropertyKeyStrings.COUNTER_MOVEMENT_TAP_LENGTH_MODE);

            if (settings.LegacyBrakeDurationMs.HasValue)
            {
                int legacyDuration = DigitalReleaseBrakePulse.ClampBrakeDurationMs(settings.LegacyBrakeDurationMs.Value);

                if (!stickPadAct.ChangedProperties.Contains(StickPadAction.PropertyKeyStrings.COUNTER_MOVEMENT_TAP_LENGTH_MIN_MS))
                {
                    stickPadAct.CounterMovementReleasePress.OppositeTapLengthMinimumMs = legacyDuration;
                    stickPadAct.ChangedProperties.Add(StickPadAction.PropertyKeyStrings.COUNTER_MOVEMENT_TAP_LENGTH_MIN_MS);
                }

                if (!stickPadAct.ChangedProperties.Contains(StickPadAction.PropertyKeyStrings.COUNTER_MOVEMENT_TAP_LENGTH_MAX_MS))
                {
                    stickPadAct.CounterMovementReleasePress.OppositeTapLengthMaximumMs = legacyDuration;
                    stickPadAct.ChangedProperties.Add(StickPadAction.PropertyKeyStrings.COUNTER_MOVEMENT_TAP_LENGTH_MAX_MS);
                }

                if (!stickPadAct.ChangedProperties.Contains(StickPadAction.PropertyKeyStrings.COUNTER_MOVEMENT_TAP_LENGTH_FIXED_MS))
                {
                    stickPadAct.CounterMovementReleasePress.OppositeTapLengthMs = legacyDuration;
                    stickPadAct.ChangedProperties.Add(StickPadAction.PropertyKeyStrings.COUNTER_MOVEMENT_TAP_LENGTH_FIXED_MS);
                }

                if (!stickPadAct.ChangedProperties.Contains(StickPadAction.PropertyKeyStrings.COUNTER_MOVEMENT_TAP_LENGTH_VARIANCE_PERCENT))
                {
                    stickPadAct.CounterMovementReleasePress.OppositeTapLengthVariancePercent = 0;
                    stickPadAct.ChangedProperties.Add(StickPadAction.PropertyKeyStrings.COUNTER_MOVEMENT_TAP_LENGTH_VARIANCE_PERCENT);
                }

                if (!modeFieldPresent)
                {
                    stickPadAct.CounterMovementReleasePress.OppositeTapLengthMode = OppositeTapLengthMode.Fixed;
                    stickPadAct.ChangedProperties.Add(StickPadAction.PropertyKeyStrings.COUNTER_MOVEMENT_TAP_LENGTH_MODE);
                }

                if (!stickPadAct.ChangedProperties.Contains(StickPadAction.PropertyKeyStrings.COUNTER_MOVEMENT_START_DELAY_MIN_MS))
                {
                    stickPadAct.CounterMovementReleasePress.OppositeTapStartDelayMinimumMs = 0;
                    stickPadAct.ChangedProperties.Add(StickPadAction.PropertyKeyStrings.COUNTER_MOVEMENT_START_DELAY_MIN_MS);
                }

                if (!stickPadAct.ChangedProperties.Contains(StickPadAction.PropertyKeyStrings.COUNTER_MOVEMENT_START_DELAY_MAX_MS))
                {
                    stickPadAct.CounterMovementReleasePress.OppositeTapStartDelayMaximumMs = 0;
                    stickPadAct.ChangedProperties.Add(StickPadAction.PropertyKeyStrings.COUNTER_MOVEMENT_START_DELAY_MAX_MS);
                }

                if (!stickPadAct.ChangedProperties.Contains(StickPadAction.PropertyKeyStrings.COUNTER_MOVEMENT_START_DELAY_MODE))
                {
                    stickPadAct.CounterMovementReleasePress.OppositeTapStartDelayMode = OppositeTapStartDelayMode.Fixed;
                    stickPadAct.ChangedProperties.Add(StickPadAction.PropertyKeyStrings.COUNTER_MOVEMENT_START_DELAY_MODE);
                }

                if (!stickPadAct.ChangedProperties.Contains(StickPadAction.PropertyKeyStrings.COUNTER_MOVEMENT_START_DELAY_FIXED_MS))
                {
                    stickPadAct.CounterMovementReleasePress.OppositeTapStartDelayMs = 0;
                    stickPadAct.ChangedProperties.Add(StickPadAction.PropertyKeyStrings.COUNTER_MOVEMENT_START_DELAY_FIXED_MS);
                }

                if (!stickPadAct.ChangedProperties.Contains(StickPadAction.PropertyKeyStrings.COUNTER_MOVEMENT_START_DELAY_VARIANCE_PERCENT))
                {
                    stickPadAct.CounterMovementReleasePress.OppositeTapStartDelayVariancePercent = 0;
                    stickPadAct.ChangedProperties.Add(StickPadAction.PropertyKeyStrings.COUNTER_MOVEMENT_START_DELAY_VARIANCE_PERCENT);
                }

                if (!stickPadAct.ChangedProperties.Contains(StickPadAction.PropertyKeyStrings.COUNTER_MOVEMENT_TAP_LENGTH_PRESET))
                {
                    stickPadAct.CounterMovementReleasePress.TapLengthPreset = DS4MapperTest.StickActions.CounterMovementTapLengthPreset.Custom;
                    stickPadAct.ChangedProperties.Add(StickPadAction.PropertyKeyStrings.COUNTER_MOVEMENT_TAP_LENGTH_PRESET);
                }
            }
            else if (!modeFieldPresent &&
                (stickPadAct.ChangedProperties.Contains(StickPadAction.PropertyKeyStrings.COUNTER_MOVEMENT_TAP_LENGTH_MIN_MS) ||
                 stickPadAct.ChangedProperties.Contains(StickPadAction.PropertyKeyStrings.COUNTER_MOVEMENT_TAP_LENGTH_MAX_MS)))
            {
                stickPadAct.CounterMovementReleasePress.OppositeTapLengthMode = OppositeTapLengthMode.MinimumAndMaximum;
                stickPadAct.ChangedProperties.Add(StickPadAction.PropertyKeyStrings.COUNTER_MOVEMENT_TAP_LENGTH_MODE);
                stickPadAct.CounterMovementReleasePress.ApplyMinimumAndMaximum(
                    stickPadAct.CounterMovementReleasePress.OppositeTapLengthMinimumMs,
                    stickPadAct.CounterMovementReleasePress.OppositeTapLengthMaximumMs);
            }

            stickPadAct.CounterMovementReleasePress.NormalizeRanges();
        }

        public void FlagBtnChangedDirection(StickPadAction.DpadDirections dir,
            StickPadAction action)
        {
            switch(dir)
            {
                case StickPadAction.DpadDirections.Up:
                    action.ChangedProperties.Add(StickPadAction.PropertyKeyStrings.PAD_DIR_UP);
                    break;
                case StickPadAction.DpadDirections.Down:
                    action.ChangedProperties.Add(StickPadAction.PropertyKeyStrings.PAD_DIR_DOWN);
                    break;
                case StickPadAction.DpadDirections.Left:
                    action.ChangedProperties.Add(StickPadAction.PropertyKeyStrings.PAD_DIR_LEFT);
                    break;
                case StickPadAction.DpadDirections.Right:
                    action.ChangedProperties.Add(StickPadAction.PropertyKeyStrings.PAD_DIR_RIGHT);
                    break;
                case StickPadAction.DpadDirections.UpLeft:
                    action.ChangedProperties.Add(StickPadAction.PropertyKeyStrings.PAD_DIR_UPLEFT);
                    break;
                case StickPadAction.DpadDirections.UpRight:
                    action.ChangedProperties.Add(StickPadAction.PropertyKeyStrings.PAD_DIR_UPRIGHT);
                    break;
                case StickPadAction.DpadDirections.DownLeft:
                    action.ChangedProperties.Add(StickPadAction.PropertyKeyStrings.PAD_DIR_DOWNLEFT);
                    break;
                case StickPadAction.DpadDirections.DownRight:
                    action.ChangedProperties.Add(StickPadAction.PropertyKeyStrings.PAD_DIR_DOWNRIGHT);
                    break;
                default:
                    break;
            }
        }
    }

    public class AnalogEmulationActionSerializer : MapActionSerializer
    {
        public class AnalogEmulationSettings
        {
            private StickAnalogEmulationAction analogAction;

            public bool SeparateAxisDeadZones
            {
                get => analogAction.DeadMod.SeparateAxisDeadZones;
                set
                {
                    analogAction.DeadMod.SeparateAxisDeadZones = value;
                    SeparateAxisDeadZonesChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler SeparateAxisDeadZonesChanged;
            public bool ShouldSerializeSeparateAxisDeadZones() =>
                analogAction.ChangedProperties.Contains(StickAnalogEmulationAction.PropertyKeyStrings.SEPARATE_AXIS_DEAD_ZONES);

            public double DeadZoneX
            {
                get => analogAction.DeadMod.DeadZoneX;
                set
                {
                    analogAction.DeadMod.DeadZoneX = Math.Clamp(value, 0.0, 1.0);
                    DeadZoneXChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler DeadZoneXChanged;
            public bool ShouldSerializeDeadZoneX() =>
                analogAction.ChangedProperties.Contains(StickAnalogEmulationAction.PropertyKeyStrings.DEAD_ZONE_X);

            public double DeadZoneY
            {
                get => analogAction.DeadMod.DeadZoneY;
                set
                {
                    analogAction.DeadMod.DeadZoneY = Math.Clamp(value, 0.0, 1.0);
                    DeadZoneYChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler DeadZoneYChanged;
            public bool ShouldSerializeDeadZoneY() =>
                analogAction.ChangedProperties.Contains(StickAnalogEmulationAction.PropertyKeyStrings.DEAD_ZONE_Y);

            public double DeadZone
            {
                get => analogAction.DeadMod.DeadZone;
                set
                {
                    analogAction.DeadMod.DeadZone = Math.Clamp(value, 0.0, 1.0);
                    DeadZoneChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler DeadZoneChanged;
            public bool ShouldSerializeDeadZone()
            {
                return analogAction.ChangedProperties.Contains(StickAnalogEmulationAction.PropertyKeyStrings.DEAD_ZONE);
            }

            //public StickDeadZone.DeadZoneTypes DeadZoneType
            public string DeadZoneType
            {
                get => analogAction.DeadMod.DeadZoneType.ToString();
                set
                {
                    if (Enum.TryParse(value, out StickDeadZone.DeadZoneTypes temp))
                    {
                        analogAction.DeadMod.DeadZoneType = temp;
                        DeadZoneTypeChanged?.Invoke(this, EventArgs.Empty);
                    }
                }
            }
            public event EventHandler DeadZoneTypeChanged;
            public bool ShouldSerializeDeadZoneType()
            {
                return analogAction.ChangedProperties.Contains(StickAnalogEmulationAction.PropertyKeyStrings.DEAD_ZONE_TYPE);
            }

            public int Rotation
            {
                get => analogAction.Rotation;
                set
                {
                    analogAction.Rotation = value;
                    RotationChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler RotationChanged;
            public bool ShouldSerializeRotation()
            {
                return analogAction.ChangedProperties.Contains(StickAnalogEmulationAction.PropertyKeyStrings.ROTATION);
            }

            public double MaxZone
            {
                get => analogAction.DeadMod.MaxZone;
                set
                {
                    analogAction.DeadMod.MaxZone = Math.Clamp(value, 0.0, 1.0);
                    MaxZoneChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler MaxZoneChanged;
            public bool ShouldSerializeMaxZone()
            {
                return analogAction.ChangedProperties.Contains(StickAnalogEmulationAction.PropertyKeyStrings.MAX_ZONE);
            }

            public string DirectionMode
            {
                get => analogAction.DirectionMode.ToString();
                set
                {
                    if (Enum.TryParse(value, out AnalogEmulationMath.ResolutionMode temp))
                    {
                        analogAction.DirectionMode = temp;
                        DirectionModeChanged?.Invoke(this, EventArgs.Empty);
                    }
                }
            }
            public event EventHandler DirectionModeChanged;
            public bool ShouldSerializeDirectionMode()
            {
                return analogAction.ChangedProperties.Contains(StickAnalogEmulationAction.PropertyKeyStrings.DIRECTION_MODE);
            }

            public int DiagonalZoneWidth
            {
                get => analogAction.DiagonalZoneWidth;
                set
                {
                    analogAction.DiagonalZoneWidth = value;
                    DiagonalZoneWidthChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler DiagonalZoneWidthChanged;
            public bool ShouldSerializeDiagonalZoneWidth()
            {
                return analogAction.ChangedProperties.Contains(StickAnalogEmulationAction.PropertyKeyStrings.DIAGONAL_ZONE_WIDTH);
            }

            public int DirectionPulseTimeMs
            {
                get => analogAction.DirectionPulseTimeMs;
                set
                {
                    analogAction.DirectionPulseTimeMs = value;
                    DirectionPulseTimeMsChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler DirectionPulseTimeMsChanged;
            public bool ShouldSerializeDirectionPulseTimeMs()
            {
                return analogAction.ChangedProperties.Contains(StickAnalogEmulationAction.PropertyKeyStrings.DIRECTION_PULSE_TIME_MS);
            }

            public bool AnalogSpeedEmulationEnabled
            {
                get => analogAction.SpeedEmulationEnabled;
                set
                {
                    analogAction.SpeedEmulationEnabled = value;
                    AnalogSpeedEmulationEnabledChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler AnalogSpeedEmulationEnabledChanged;
            public bool ShouldSerializeAnalogSpeedEmulationEnabled()
            {
                return analogAction.ChangedProperties.Contains(StickAnalogEmulationAction.PropertyKeyStrings.SPEED_ENABLED);
            }

            public int AnalogEmulationActivePercent
            {
                get => analogAction.SpeedActivePercent;
                set
                {
                    analogAction.SpeedActivePercent = value;
                    AnalogEmulationActivePercentChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler AnalogEmulationActivePercentChanged;
            public bool ShouldSerializeAnalogEmulationActivePercent()
            {
                return analogAction.ChangedProperties.Contains(StickAnalogEmulationAction.PropertyKeyStrings.SPEED_ACTIVE_PERCENT);
            }

            public int AnalogEmulationPulseTimeMs
            {
                get => analogAction.SpeedPulseTimeMs;
                set
                {
                    analogAction.SpeedPulseTimeMs = value;
                    AnalogEmulationPulseTimeMsChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler AnalogEmulationPulseTimeMsChanged;
            public bool ShouldSerializeAnalogEmulationPulseTimeMs()
            {
                return analogAction.ChangedProperties.Contains(StickAnalogEmulationAction.PropertyKeyStrings.SPEED_PULSE_TIME_MS);
            }

            public int FullSpeedThresholdPercent
            {
                get => analogAction.FullSpeedThresholdPercent;
                set
                {
                    analogAction.FullSpeedThresholdPercent = value;
                    FullSpeedThresholdPercentChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler FullSpeedThresholdPercentChanged;
            public bool ShouldSerializeFullSpeedThresholdPercent()
            {
                return analogAction.ChangedProperties.Contains(StickAnalogEmulationAction.PropertyKeyStrings.FULL_SPEED_THRESHOLD_PERCENT);
            }

            public bool CounterMovementReleasePressEnabled
            {
                get => analogAction.CounterMovementReleasePress.Enabled;
                set
                {
                    analogAction.CounterMovementReleasePress.Enabled = value;
                    CounterMovementReleasePressEnabledChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler CounterMovementReleasePressEnabledChanged;
            public bool ShouldSerializeCounterMovementReleasePressEnabled()
            {
                return analogAction.ChangedProperties.Contains(StickAnalogEmulationAction.PropertyKeyStrings.COUNTER_MOVEMENT_ENABLED);
            }

            public bool UseArrowKeysForCounterMovementPresses
            {
                get => analogAction.CounterMovementReleasePress.UseArrowKeysForCounterMovementPresses;
                set
                {
                    analogAction.CounterMovementReleasePress.UseArrowKeysForCounterMovementPresses = value;
                    UseArrowKeysForCounterMovementPressesChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler UseArrowKeysForCounterMovementPressesChanged;
            public bool ShouldSerializeUseArrowKeysForCounterMovementPresses()
            {
                return analogAction.ChangedProperties.Contains(StickAnalogEmulationAction.PropertyKeyStrings.COUNTER_MOVEMENT_USE_ARROW_KEYS);
            }

            public string CounterMovementTapLengthPreset
            {
                get => analogAction.CounterMovementReleasePress.TapLengthPreset.ToString();
                set
                {
                    if (Enum.TryParse(value, out DS4MapperTest.StickActions.CounterMovementTapLengthPreset temp))
                    {
                        analogAction.CounterMovementReleasePress.TapLengthPreset = temp;
                        CounterMovementTapLengthPresetChanged?.Invoke(this, EventArgs.Empty);
                    }
                }
            }
            public event EventHandler CounterMovementTapLengthPresetChanged;
            public bool ShouldSerializeCounterMovementTapLengthPreset()
            {
                return analogAction.ChangedProperties.Contains(StickAnalogEmulationAction.PropertyKeyStrings.COUNTER_MOVEMENT_TAP_LENGTH_PRESET);
            }

            [JsonConverter(typeof(StringEnumConverter))]
            public OppositeTapLengthMode OppositeTapLengthMode
            {
                get => analogAction.CounterMovementReleasePress.OppositeTapLengthMode;
                set
                {
                    analogAction.CounterMovementReleasePress.OppositeTapLengthMode = value;
                    OppositeTapLengthModeChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler OppositeTapLengthModeChanged;
            public bool ShouldSerializeOppositeTapLengthMode()
            {
                return analogAction.ChangedProperties.Contains(StickAnalogEmulationAction.PropertyKeyStrings.COUNTER_MOVEMENT_TAP_LENGTH_MODE);
            }

            public int OppositeTapLengthMs
            {
                get => analogAction.CounterMovementReleasePress.OppositeTapLengthMs;
                set
                {
                    analogAction.CounterMovementReleasePress.OppositeTapLengthMs = value;
                    OppositeTapLengthMsChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler OppositeTapLengthMsChanged;
            public bool ShouldSerializeOppositeTapLengthMs()
            {
                return analogAction.ChangedProperties.Contains(StickAnalogEmulationAction.PropertyKeyStrings.COUNTER_MOVEMENT_TAP_LENGTH_FIXED_MS);
            }

            public int OppositeTapLengthVariancePercent
            {
                get => analogAction.CounterMovementReleasePress.OppositeTapLengthVariancePercent;
                set
                {
                    analogAction.CounterMovementReleasePress.OppositeTapLengthVariancePercent = value;
                    OppositeTapLengthVariancePercentChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler OppositeTapLengthVariancePercentChanged;
            public bool ShouldSerializeOppositeTapLengthVariancePercent()
            {
                return analogAction.ChangedProperties.Contains(StickAnalogEmulationAction.PropertyKeyStrings.COUNTER_MOVEMENT_TAP_LENGTH_VARIANCE_PERCENT);
            }

            public int OppositeTapLengthMinimumMs
            {
                get => analogAction.CounterMovementReleasePress.OppositeTapLengthMinimumMs;
                set
                {
                    analogAction.CounterMovementReleasePress.OppositeTapLengthMinimumMs = value;
                    OppositeTapLengthMinimumMsChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler OppositeTapLengthMinimumMsChanged;
            public bool ShouldSerializeOppositeTapLengthMinimumMs()
            {
                return analogAction.ChangedProperties.Contains(StickAnalogEmulationAction.PropertyKeyStrings.COUNTER_MOVEMENT_TAP_LENGTH_MIN_MS);
            }

            public int OppositeTapLengthMaximumMs
            {
                get => analogAction.CounterMovementReleasePress.OppositeTapLengthMaximumMs;
                set
                {
                    analogAction.CounterMovementReleasePress.OppositeTapLengthMaximumMs = value;
                    OppositeTapLengthMaximumMsChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler OppositeTapLengthMaximumMsChanged;
            public bool ShouldSerializeOppositeTapLengthMaximumMs()
            {
                return analogAction.ChangedProperties.Contains(StickAnalogEmulationAction.PropertyKeyStrings.COUNTER_MOVEMENT_TAP_LENGTH_MAX_MS);
            }

            public int OppositeTapStartDelayMinimumMs
            {
                get => analogAction.CounterMovementReleasePress.OppositeTapStartDelayMinimumMs;
                set
                {
                    analogAction.CounterMovementReleasePress.OppositeTapStartDelayMinimumMs = value;
                    OppositeTapStartDelayMinimumMsChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler OppositeTapStartDelayMinimumMsChanged;
            public bool ShouldSerializeOppositeTapStartDelayMinimumMs()
            {
                return analogAction.ChangedProperties.Contains(StickAnalogEmulationAction.PropertyKeyStrings.COUNTER_MOVEMENT_START_DELAY_MIN_MS);
            }

            public int OppositeTapStartDelayMaximumMs
            {
                get => analogAction.CounterMovementReleasePress.OppositeTapStartDelayMaximumMs;
                set
                {
                    analogAction.CounterMovementReleasePress.OppositeTapStartDelayMaximumMs = value;
                    OppositeTapStartDelayMaximumMsChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler OppositeTapStartDelayMaximumMsChanged;
            public bool ShouldSerializeOppositeTapStartDelayMaximumMs()
            {
                return analogAction.ChangedProperties.Contains(StickAnalogEmulationAction.PropertyKeyStrings.COUNTER_MOVEMENT_START_DELAY_MAX_MS);
            }

            public int BrakeMinimumHoldMs
            {
                get => analogAction.CounterMovementReleasePress.MinimumHoldMs;
                set
                {
                    analogAction.CounterMovementReleasePress.MinimumHoldMs = value;
                    BrakeMinimumHoldMsChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler BrakeMinimumHoldMsChanged;
            public bool ShouldSerializeBrakeMinimumHoldMs()
            {
                return analogAction.ChangedProperties.Contains(StickAnalogEmulationAction.PropertyKeyStrings.BRAKE_MIN_HOLD_MS);
            }

            public double BrakeArmingThreshold
            {
                get => analogAction.CounterMovementReleasePress.ArmingThreshold;
                set
                {
                    analogAction.CounterMovementReleasePress.ArmingThreshold = value;
                    BrakeArmingThresholdChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler BrakeArmingThresholdChanged;
            public bool ShouldSerializeBrakeArmingThreshold()
            {
                return analogAction.ChangedProperties.Contains(StickAnalogEmulationAction.PropertyKeyStrings.BRAKE_ARMING_THRESHOLD);
            }

            // --- Legacy compatibility (pre-rename "Digital Release Brake" profiles) ---
            // See StickPadActionSerializer.StickPadActionSettings for the full rationale;
            // Analog Emulation shares the same CounterMovementReleasePressProcessor type and
            // so needs the same migration shadow properties.
            private bool? legacyBrakeEnabled;
            internal bool? LegacyBrakeEnabled => legacyBrakeEnabled;
            public bool BrakeEnabled
            {
                get => false;
                set => legacyBrakeEnabled = value;
            }
            public bool ShouldSerializeBrakeEnabled() => false;

            private int? legacyBrakeDurationMs;
            internal int? LegacyBrakeDurationMs => legacyBrakeDurationMs;
            public int BrakeDurationMs
            {
                get => 0;
                set => legacyBrakeDurationMs = value;
            }
            public bool ShouldSerializeBrakeDurationMs() => false;

            public AnalogEmulationSettings(StickAnalogEmulationAction analogAction)
            {
                this.analogAction = analogAction;
            }
        }

        private static readonly string[] SlotNames = { "Up", "Down", "Left", "Right" };

        private StickAnalogEmulationAction analogAct = new StickAnalogEmulationAction();

        private Dictionary<string, StickPadActionSerializer.StickPadDirBinding> dictDirBindings =
            new Dictionary<string, StickPadActionSerializer.StickPadDirBinding>();

        [JsonProperty("Bindings", Required = Required.Always)]
        public Dictionary<string, StickPadActionSerializer.StickPadDirBinding> DictDirBindings
        {
            get => dictDirBindings;
            set => dictDirBindings = value;
        }
        public bool ShouldSerializeDictDirBindings()
        {
            return dictDirBindings.Count > 0;
        }

        private AnalogEmulationSettings settings;
        public AnalogEmulationSettings Settings
        {
            get => settings;
            set => settings = value;
        }
        public bool ShouldSerializeSettings()
        {
            return analogAct.ChangedProperties.Count > 0;
        }

        // Deserialize
        public AnalogEmulationActionSerializer() : base()
        {
            mapAction = analogAct;
            settings = new AnalogEmulationSettings(analogAct);

            NameChanged += AnalogEmulationActionSerializer_NameChanged;
            settings.DeadZoneChanged += Settings_DeadZoneChanged;
            settings.DeadZoneTypeChanged += Settings_DeadZoneTypeChanged;
            settings.SeparateAxisDeadZonesChanged += Settings_SeparateAxisDeadZonesChanged;
            settings.DeadZoneXChanged += Settings_DeadZoneXChanged;
            settings.DeadZoneYChanged += Settings_DeadZoneYChanged;
            settings.RotationChanged += Settings_RotationChanged;
            settings.MaxZoneChanged += Settings_MaxZoneChanged;
            settings.DirectionModeChanged += Settings_DirectionModeChanged;
            settings.DiagonalZoneWidthChanged += Settings_DiagonalZoneWidthChanged;
            settings.DirectionPulseTimeMsChanged += Settings_DirectionPulseTimeMsChanged;
            settings.AnalogSpeedEmulationEnabledChanged += Settings_AnalogSpeedEmulationEnabledChanged;
            settings.AnalogEmulationActivePercentChanged += Settings_AnalogEmulationActivePercentChanged;
            settings.AnalogEmulationPulseTimeMsChanged += Settings_AnalogEmulationPulseTimeMsChanged;
            settings.FullSpeedThresholdPercentChanged += Settings_FullSpeedThresholdPercentChanged;
            settings.CounterMovementReleasePressEnabledChanged += Settings_CounterMovementReleasePressEnabledChanged;
            settings.UseArrowKeysForCounterMovementPressesChanged += Settings_UseArrowKeysForCounterMovementPressesChanged;
            settings.CounterMovementTapLengthPresetChanged += Settings_CounterMovementTapLengthPresetChanged;
            settings.OppositeTapLengthModeChanged += Settings_OppositeTapLengthModeChanged;
            settings.OppositeTapLengthMsChanged += Settings_OppositeTapLengthMsChanged;
            settings.OppositeTapLengthVariancePercentChanged += Settings_OppositeTapLengthVariancePercentChanged;
            settings.OppositeTapLengthMinimumMsChanged += Settings_OppositeTapLengthMinimumMsChanged;
            settings.OppositeTapLengthMaximumMsChanged += Settings_OppositeTapLengthMaximumMsChanged;
            settings.OppositeTapStartDelayMinimumMsChanged += Settings_OppositeTapStartDelayMinimumMsChanged;
            settings.OppositeTapStartDelayMaximumMsChanged += Settings_OppositeTapStartDelayMaximumMsChanged;
            settings.BrakeMinimumHoldMsChanged += Settings_BrakeMinimumHoldMsChanged;
            settings.BrakeArmingThresholdChanged += Settings_BrakeArmingThresholdChanged;
        }

        private void Settings_CounterMovementReleasePressEnabledChanged(object sender, EventArgs e)
        {
            analogAct.ChangedProperties.Add(StickAnalogEmulationAction.PropertyKeyStrings.COUNTER_MOVEMENT_ENABLED);
        }

        private void Settings_UseArrowKeysForCounterMovementPressesChanged(object sender, EventArgs e)
        {
            analogAct.ChangedProperties.Add(StickAnalogEmulationAction.PropertyKeyStrings.COUNTER_MOVEMENT_USE_ARROW_KEYS);
        }

        private void Settings_CounterMovementTapLengthPresetChanged(object sender, EventArgs e)
        {
            analogAct.ChangedProperties.Add(StickAnalogEmulationAction.PropertyKeyStrings.COUNTER_MOVEMENT_TAP_LENGTH_PRESET);
        }

        private void Settings_OppositeTapLengthModeChanged(object sender, EventArgs e)
        {
            analogAct.ChangedProperties.Add(StickAnalogEmulationAction.PropertyKeyStrings.COUNTER_MOVEMENT_TAP_LENGTH_MODE);
        }

        private void Settings_OppositeTapLengthMsChanged(object sender, EventArgs e)
        {
            analogAct.ChangedProperties.Add(StickAnalogEmulationAction.PropertyKeyStrings.COUNTER_MOVEMENT_TAP_LENGTH_FIXED_MS);
        }

        private void Settings_OppositeTapLengthVariancePercentChanged(object sender, EventArgs e)
        {
            analogAct.ChangedProperties.Add(StickAnalogEmulationAction.PropertyKeyStrings.COUNTER_MOVEMENT_TAP_LENGTH_VARIANCE_PERCENT);
        }

        private void Settings_OppositeTapLengthMinimumMsChanged(object sender, EventArgs e)
        {
            analogAct.ChangedProperties.Add(StickAnalogEmulationAction.PropertyKeyStrings.COUNTER_MOVEMENT_TAP_LENGTH_MIN_MS);
        }

        private void Settings_OppositeTapLengthMaximumMsChanged(object sender, EventArgs e)
        {
            analogAct.ChangedProperties.Add(StickAnalogEmulationAction.PropertyKeyStrings.COUNTER_MOVEMENT_TAP_LENGTH_MAX_MS);
        }

        private void Settings_OppositeTapStartDelayMinimumMsChanged(object sender, EventArgs e)
        {
            analogAct.ChangedProperties.Add(StickAnalogEmulationAction.PropertyKeyStrings.COUNTER_MOVEMENT_START_DELAY_MIN_MS);
        }

        private void Settings_OppositeTapStartDelayMaximumMsChanged(object sender, EventArgs e)
        {
            analogAct.ChangedProperties.Add(StickAnalogEmulationAction.PropertyKeyStrings.COUNTER_MOVEMENT_START_DELAY_MAX_MS);
        }

        private void Settings_BrakeMinimumHoldMsChanged(object sender, EventArgs e)
        {
            analogAct.ChangedProperties.Add(StickAnalogEmulationAction.PropertyKeyStrings.BRAKE_MIN_HOLD_MS);
        }

        private void Settings_BrakeArmingThresholdChanged(object sender, EventArgs e)
        {
            analogAct.ChangedProperties.Add(StickAnalogEmulationAction.PropertyKeyStrings.BRAKE_ARMING_THRESHOLD);
        }

        private void AnalogEmulationActionSerializer_NameChanged(object sender, EventArgs e)
        {
            analogAct.ChangedProperties.Add(StickAnalogEmulationAction.PropertyKeyStrings.NAME);
        }

        private void Settings_DeadZoneChanged(object sender, EventArgs e)
        {
            analogAct.ChangedProperties.Add(StickAnalogEmulationAction.PropertyKeyStrings.DEAD_ZONE);
        }

        private void Settings_SeparateAxisDeadZonesChanged(object sender, EventArgs e)
        {
            analogAct.ChangedProperties.Add(StickAnalogEmulationAction.PropertyKeyStrings.SEPARATE_AXIS_DEAD_ZONES);
        }

        private void Settings_DeadZoneXChanged(object sender, EventArgs e)
        {
            analogAct.ChangedProperties.Add(StickAnalogEmulationAction.PropertyKeyStrings.DEAD_ZONE_X);
        }

        private void Settings_DeadZoneYChanged(object sender, EventArgs e)
        {
            analogAct.ChangedProperties.Add(StickAnalogEmulationAction.PropertyKeyStrings.DEAD_ZONE_Y);
        }

        private void Settings_DeadZoneTypeChanged(object sender, EventArgs e)
        {
            analogAct.ChangedProperties.Add(StickAnalogEmulationAction.PropertyKeyStrings.DEAD_ZONE_TYPE);
        }

        private void Settings_RotationChanged(object sender, EventArgs e)
        {
            analogAct.ChangedProperties.Add(StickAnalogEmulationAction.PropertyKeyStrings.ROTATION);
        }

        private void Settings_MaxZoneChanged(object sender, EventArgs e)
        {
            analogAct.ChangedProperties.Add(StickAnalogEmulationAction.PropertyKeyStrings.MAX_ZONE);
        }

        private void Settings_DirectionModeChanged(object sender, EventArgs e)
        {
            analogAct.ChangedProperties.Add(StickAnalogEmulationAction.PropertyKeyStrings.DIRECTION_MODE);
        }

        private void Settings_DiagonalZoneWidthChanged(object sender, EventArgs e)
        {
            analogAct.ChangedProperties.Add(StickAnalogEmulationAction.PropertyKeyStrings.DIAGONAL_ZONE_WIDTH);
        }

        private void Settings_DirectionPulseTimeMsChanged(object sender, EventArgs e)
        {
            analogAct.ChangedProperties.Add(StickAnalogEmulationAction.PropertyKeyStrings.DIRECTION_PULSE_TIME_MS);
        }

        private void Settings_AnalogSpeedEmulationEnabledChanged(object sender, EventArgs e)
        {
            analogAct.ChangedProperties.Add(StickAnalogEmulationAction.PropertyKeyStrings.SPEED_ENABLED);
        }

        private void Settings_AnalogEmulationActivePercentChanged(object sender, EventArgs e)
        {
            analogAct.ChangedProperties.Add(StickAnalogEmulationAction.PropertyKeyStrings.SPEED_ACTIVE_PERCENT);
        }

        private void Settings_AnalogEmulationPulseTimeMsChanged(object sender, EventArgs e)
        {
            analogAct.ChangedProperties.Add(StickAnalogEmulationAction.PropertyKeyStrings.SPEED_PULSE_TIME_MS);
        }

        private void Settings_FullSpeedThresholdPercentChanged(object sender, EventArgs e)
        {
            analogAct.ChangedProperties.Add(StickAnalogEmulationAction.PropertyKeyStrings.FULL_SPEED_THRESHOLD_PERCENT);
        }

        // Pre-serialize
        public AnalogEmulationActionSerializer(ActionLayer tempLayer, MapAction action) :
            base(tempLayer, action)
        {
            if (action is StickAnalogEmulationAction temp)
            {
                analogAct = temp;
                mapAction = analogAct;
                settings = new AnalogEmulationSettings(analogAct);
                PopulateFuncs();
            }
        }

        // Pre-serialize
        private void PopulateFuncs()
        {
            List<ActionFuncSerializer> tempFuncs = new List<ActionFuncSerializer>();

            for (int i = 0; i < SlotNames.Length; i++)
            {
                AxisDirButton dirButton = analogAct.DirButtons[i];
                if (dirButton == null) continue;

                tempFuncs.Clear();
                foreach (ActionFunc tempFunc in dirButton.ActionFuncs)
                {
                    ActionFuncSerializer tempSerializer =
                        ActionFuncSerializerFactory.CreateSerializer(tempFunc);
                    if (tempSerializer != null)
                    {
                        tempFuncs.Add(tempSerializer);
                    }
                }

                dictDirBindings.Add(SlotNames[i],
                    new StickPadActionSerializer.StickPadDirBinding()
                    {
                        ActionDirName = dirButton.Name,
                        ActionFuncSerializers = new List<ActionFuncSerializer>(tempFuncs),
                    });
            }
        }

        // Post-deserialize
        public override void PopulateMap()
        {
            foreach (AxisDirButton dirButton in analogAct.DirButtons)
            {
                dirButton?.ActionFuncs.Clear();
            }

            foreach (KeyValuePair<string, StickPadActionSerializer.StickPadDirBinding> tempKeyPair in dictDirBindings)
            {
                int idx = Array.IndexOf(SlotNames, tempKeyPair.Key);
                if (idx < 0) continue;

                AxisDirButton tempDirButton = analogAct.DirButtons[idx];
                if (tempDirButton == null) continue;

                tempDirButton.Name = tempKeyPair.Value.ActionDirName;

                foreach (ActionFuncSerializer serializer in tempKeyPair.Value.ActionFuncSerializers)
                {
                    serializer.PopulateFunc();
                    tempDirButton.ActionFuncs.Add(serializer.ActionFunc);
                }

                FlagBtnChangedDirection(idx, analogAct);
            }

            MigrateLegacyCounterMovementReleasePressFields();
        }

        // See StickPadActionSerializer.MigrateLegacyCounterMovementReleasePressFields for
        // the full rationale; Analog Emulation shares the same
        // CounterMovementReleasePressProcessor type and the same legacy field names.
        // See TouchpadActionPadSerializer.MigrateLegacyCounterMovementReleasePressFields for
        // the three profile generations this handles.
        private void MigrateLegacyCounterMovementReleasePressFields()
        {
            if (settings.LegacyBrakeEnabled.HasValue &&
                !analogAct.ChangedProperties.Contains(StickAnalogEmulationAction.PropertyKeyStrings.COUNTER_MOVEMENT_ENABLED))
            {
                analogAct.CounterMovementReleasePress.Enabled = settings.LegacyBrakeEnabled.Value;
                analogAct.ChangedProperties.Add(StickAnalogEmulationAction.PropertyKeyStrings.COUNTER_MOVEMENT_ENABLED);
            }

            bool modeFieldPresent = analogAct.ChangedProperties.Contains(StickAnalogEmulationAction.PropertyKeyStrings.COUNTER_MOVEMENT_TAP_LENGTH_MODE);

            if (settings.LegacyBrakeDurationMs.HasValue)
            {
                int legacyDuration = DigitalReleaseBrakePulse.ClampBrakeDurationMs(settings.LegacyBrakeDurationMs.Value);

                if (!analogAct.ChangedProperties.Contains(StickAnalogEmulationAction.PropertyKeyStrings.COUNTER_MOVEMENT_TAP_LENGTH_MIN_MS))
                {
                    analogAct.CounterMovementReleasePress.OppositeTapLengthMinimumMs = legacyDuration;
                    analogAct.ChangedProperties.Add(StickAnalogEmulationAction.PropertyKeyStrings.COUNTER_MOVEMENT_TAP_LENGTH_MIN_MS);
                }

                if (!analogAct.ChangedProperties.Contains(StickAnalogEmulationAction.PropertyKeyStrings.COUNTER_MOVEMENT_TAP_LENGTH_MAX_MS))
                {
                    analogAct.CounterMovementReleasePress.OppositeTapLengthMaximumMs = legacyDuration;
                    analogAct.ChangedProperties.Add(StickAnalogEmulationAction.PropertyKeyStrings.COUNTER_MOVEMENT_TAP_LENGTH_MAX_MS);
                }

                if (!analogAct.ChangedProperties.Contains(StickAnalogEmulationAction.PropertyKeyStrings.COUNTER_MOVEMENT_TAP_LENGTH_FIXED_MS))
                {
                    analogAct.CounterMovementReleasePress.OppositeTapLengthMs = legacyDuration;
                    analogAct.ChangedProperties.Add(StickAnalogEmulationAction.PropertyKeyStrings.COUNTER_MOVEMENT_TAP_LENGTH_FIXED_MS);
                }

                if (!analogAct.ChangedProperties.Contains(StickAnalogEmulationAction.PropertyKeyStrings.COUNTER_MOVEMENT_TAP_LENGTH_VARIANCE_PERCENT))
                {
                    analogAct.CounterMovementReleasePress.OppositeTapLengthVariancePercent = 0;
                    analogAct.ChangedProperties.Add(StickAnalogEmulationAction.PropertyKeyStrings.COUNTER_MOVEMENT_TAP_LENGTH_VARIANCE_PERCENT);
                }

                if (!modeFieldPresent)
                {
                    analogAct.CounterMovementReleasePress.OppositeTapLengthMode = OppositeTapLengthMode.Fixed;
                    analogAct.ChangedProperties.Add(StickAnalogEmulationAction.PropertyKeyStrings.COUNTER_MOVEMENT_TAP_LENGTH_MODE);
                }

                if (!analogAct.ChangedProperties.Contains(StickAnalogEmulationAction.PropertyKeyStrings.COUNTER_MOVEMENT_START_DELAY_MIN_MS))
                {
                    analogAct.CounterMovementReleasePress.OppositeTapStartDelayMinimumMs = 0;
                    analogAct.ChangedProperties.Add(StickAnalogEmulationAction.PropertyKeyStrings.COUNTER_MOVEMENT_START_DELAY_MIN_MS);
                }

                if (!analogAct.ChangedProperties.Contains(StickAnalogEmulationAction.PropertyKeyStrings.COUNTER_MOVEMENT_START_DELAY_MAX_MS))
                {
                    analogAct.CounterMovementReleasePress.OppositeTapStartDelayMaximumMs = 0;
                    analogAct.ChangedProperties.Add(StickAnalogEmulationAction.PropertyKeyStrings.COUNTER_MOVEMENT_START_DELAY_MAX_MS);
                }

                if (!analogAct.ChangedProperties.Contains(StickAnalogEmulationAction.PropertyKeyStrings.COUNTER_MOVEMENT_START_DELAY_MODE))
                {
                    analogAct.CounterMovementReleasePress.OppositeTapStartDelayMode = OppositeTapStartDelayMode.Fixed;
                    analogAct.ChangedProperties.Add(StickAnalogEmulationAction.PropertyKeyStrings.COUNTER_MOVEMENT_START_DELAY_MODE);
                }

                if (!analogAct.ChangedProperties.Contains(StickAnalogEmulationAction.PropertyKeyStrings.COUNTER_MOVEMENT_START_DELAY_FIXED_MS))
                {
                    analogAct.CounterMovementReleasePress.OppositeTapStartDelayMs = 0;
                    analogAct.ChangedProperties.Add(StickAnalogEmulationAction.PropertyKeyStrings.COUNTER_MOVEMENT_START_DELAY_FIXED_MS);
                }

                if (!analogAct.ChangedProperties.Contains(StickAnalogEmulationAction.PropertyKeyStrings.COUNTER_MOVEMENT_START_DELAY_VARIANCE_PERCENT))
                {
                    analogAct.CounterMovementReleasePress.OppositeTapStartDelayVariancePercent = 0;
                    analogAct.ChangedProperties.Add(StickAnalogEmulationAction.PropertyKeyStrings.COUNTER_MOVEMENT_START_DELAY_VARIANCE_PERCENT);
                }

                if (!analogAct.ChangedProperties.Contains(StickAnalogEmulationAction.PropertyKeyStrings.COUNTER_MOVEMENT_TAP_LENGTH_PRESET))
                {
                    analogAct.CounterMovementReleasePress.TapLengthPreset = DS4MapperTest.StickActions.CounterMovementTapLengthPreset.Custom;
                    analogAct.ChangedProperties.Add(StickAnalogEmulationAction.PropertyKeyStrings.COUNTER_MOVEMENT_TAP_LENGTH_PRESET);
                }
            }
            else if (!modeFieldPresent &&
                (analogAct.ChangedProperties.Contains(StickAnalogEmulationAction.PropertyKeyStrings.COUNTER_MOVEMENT_TAP_LENGTH_MIN_MS) ||
                 analogAct.ChangedProperties.Contains(StickAnalogEmulationAction.PropertyKeyStrings.COUNTER_MOVEMENT_TAP_LENGTH_MAX_MS)))
            {
                analogAct.CounterMovementReleasePress.OppositeTapLengthMode = OppositeTapLengthMode.MinimumAndMaximum;
                analogAct.ChangedProperties.Add(StickAnalogEmulationAction.PropertyKeyStrings.COUNTER_MOVEMENT_TAP_LENGTH_MODE);
                analogAct.CounterMovementReleasePress.ApplyMinimumAndMaximum(
                    analogAct.CounterMovementReleasePress.OppositeTapLengthMinimumMs,
                    analogAct.CounterMovementReleasePress.OppositeTapLengthMaximumMs);
            }

            analogAct.CounterMovementReleasePress.NormalizeRanges();
        }

        public void FlagBtnChangedDirection(int idx, StickAnalogEmulationAction action)
        {
            switch ((StickAnalogEmulationAction.DirSlot)idx)
            {
                case StickAnalogEmulationAction.DirSlot.Up:
                    action.ChangedProperties.Add(StickAnalogEmulationAction.PropertyKeyStrings.DIR_UP);
                    break;
                case StickAnalogEmulationAction.DirSlot.Down:
                    action.ChangedProperties.Add(StickAnalogEmulationAction.PropertyKeyStrings.DIR_DOWN);
                    break;
                case StickAnalogEmulationAction.DirSlot.Left:
                    action.ChangedProperties.Add(StickAnalogEmulationAction.PropertyKeyStrings.DIR_LEFT);
                    break;
                case StickAnalogEmulationAction.DirSlot.Right:
                    action.ChangedProperties.Add(StickAnalogEmulationAction.PropertyKeyStrings.DIR_RIGHT);
                    break;
                default:
                    break;
            }
        }
    }

    public class StickMouseSerializer : MapActionSerializer
    {
        public class StickMouseSettings
        {
            public class DeltaAccelSettingsSerializer
            {
                private StickMouse stickMouseAction;

                public bool Enabled
                {
                    get => stickMouseAction.MouseDeltaSettings.Enabled;
                    set => stickMouseAction.MouseDeltaSettings.Enabled = value;
                }

                public double Multiplier
                {
                    get => stickMouseAction.MouseDeltaSettings.Multiplier;
                    set => stickMouseAction.MouseDeltaSettings.Multiplier = value;
                }

                public double MaxTravel
                {
                    get => stickMouseAction.MouseDeltaSettings.MaxTravel;
                    set => stickMouseAction.MouseDeltaSettings.MaxTravel = value;
                }

                public double MinTravel
                {
                    get => stickMouseAction.MouseDeltaSettings.MinTravel;
                    set => stickMouseAction.MouseDeltaSettings.MinTravel = value;
                }

                public double EasingDuration
                {
                    get => stickMouseAction.MouseDeltaSettings.EasingDuration;
                    set => stickMouseAction.MouseDeltaSettings.EasingDuration = value;
                }

                public double MinFactor
                {
                    get => stickMouseAction.MouseDeltaSettings.MinFactor;
                    set => stickMouseAction.MouseDeltaSettings.MinFactor = value;
                }

                public DeltaAccelSettingsSerializer(StickMouse mouseAction)
                {
                    this.stickMouseAction = mouseAction;
                }
            }

            private StickMouse stickMouseAction;
            private DeltaAccelSettingsSerializer mouseDeltaSettingsSerializer;

            public int MouseSpeed
            {
                get => stickMouseAction.MouseSpeed;
                set
                {
                    stickMouseAction.MouseSpeed = value;
                    MouseSpeedChanged?.Invoke(this, EventArgs.Empty);
                }
            }

            public event EventHandler MouseSpeedChanged;

            public double DeadZone
            {
                get => stickMouseAction.DeadMod.DeadZone;
                set
                {
                    stickMouseAction.DeadMod.DeadZone = value;
                    DeadZoneChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler DeadZoneChanged;

            public double MaxZone
            {
                get => stickMouseAction.DeadMod.MaxZone;
                set
                {
                    stickMouseAction.DeadMod.MaxZone = value;
                    MaxZoneChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler MaxZoneChanged;

            [JsonConverter(typeof(SafeStringEnumConverter),
                StickOutCurve.Curve.Linear)]
            public StickOutCurve.Curve OutputCurve
            {
                get => stickMouseAction.OutputCurve;
                set
                {
                    stickMouseAction.OutputCurve = value;
                    OutputCurveChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler OutputCurveChanged;

            public DeltaAccelSettingsSerializer DeltaSettings
            {
                get => mouseDeltaSettingsSerializer;
                set
                {
                    mouseDeltaSettingsSerializer = value;
                    DeltaSettingsChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler DeltaSettingsChanged;

            public double VerticalScale
            {
                get => stickMouseAction.VerticalScale;
                set
                {
                    stickMouseAction.VerticalScale = value;
                    VerticalScaleChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler VerticalScaleChanged;

            public StickMouseSettings(StickMouse mouseAction)
            {
                stickMouseAction = mouseAction;
                mouseDeltaSettingsSerializer = new DeltaAccelSettingsSerializer(mouseAction);
            }
        }

        private StickMouse stickMouseAction =
            new StickMouse();

        private StickMouseSettings settings;
        public StickMouseSettings Settings { get => settings; set => settings = value; }

        public StickMouseSerializer() : base()
        {
            mapAction = stickMouseAction;
            settings = new StickMouseSettings(stickMouseAction);

            NameChanged += StickMouseSerializer_NameChanged;
            settings.MouseSpeedChanged += Settings_MouseSpeedChanged;
            settings.DeadZoneChanged += Settings_DeadZoneChanged;
            settings.MaxZoneChanged += Settings_MaxZoneChanged;
            settings.OutputCurveChanged += Settings_OutputCurveChanged;
            settings.DeltaSettingsChanged += Settings_DeltaSettingsChanged;
            settings.VerticalScaleChanged += Settings_VerticalScaleChanged;
        }

        public StickMouseSerializer(ActionLayer tempLayer, MapAction action) :
            base(tempLayer, action)
        {
            if (action is StickMouse temp)
            {
                stickMouseAction = temp;
                mapAction = stickMouseAction;
                settings = new StickMouseSettings(stickMouseAction);
            }
        }

        private void Settings_DeltaSettingsChanged(object sender, EventArgs e)
        {
            stickMouseAction.ChangedProperties.Add(StickMouse.PropertyKeyStrings.DELTA_SETTINGS);
        }

        private void Settings_OutputCurveChanged(object sender, EventArgs e)
        {
            stickMouseAction.ChangedProperties.Add(StickMouse.PropertyKeyStrings.OUTPUT_CURVE);
        }

        private void Settings_MaxZoneChanged(object sender, EventArgs e)
        {
            stickMouseAction.ChangedProperties.Add(StickMouse.PropertyKeyStrings.MAX_ZONE);
        }

        private void StickMouseSerializer_NameChanged(object sender, EventArgs e)
        {
            stickMouseAction.ChangedProperties.Add(StickMouse.PropertyKeyStrings.NAME);
        }

        private void Settings_MouseSpeedChanged(object sender, EventArgs e)
        {
            stickMouseAction.ChangedProperties.Add(StickMouse.PropertyKeyStrings.MOUSE_SPEED);
        }

        private void Settings_DeadZoneChanged(object sender, EventArgs e)
        {
            stickMouseAction.ChangedProperties.Add(StickMouse.PropertyKeyStrings.DEAD_ZONE);
        }

        private void Settings_VerticalScaleChanged(object sender, EventArgs e)
        {
            stickMouseAction.ChangedProperties.Add(StickMouse.PropertyKeyStrings.VERTICAL_SCALE);
        }
    }

    public class StickHybridAimActionSerializer : MapActionSerializer
    {
        public class StickHybridAimSettings
        {
            private StickHybridAim hybridAimAction;

            public int StickSens
            {
                get => hybridAimAction.StickSens;
                set
                {
                    hybridAimAction.StickSens = value;
                    StickSensChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler StickSensChanged;

            public double MouselikeFactor
            {
                get => hybridAimAction.MouselikeFactor;
                set
                {
                    hybridAimAction.MouselikeFactor = value;
                    MouselikeFactorChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler MouselikeFactorChanged;

            public double DeadZone
            {
                get => hybridAimAction.DeadMod.DeadZone;
                set
                {
                    hybridAimAction.DeadMod.DeadZone = value;
                    DeadZoneChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler DeadZoneChanged;

            public double MaxZone
            {
                get => hybridAimAction.DeadMod.MaxZone;
                set
                {
                    hybridAimAction.DeadMod.MaxZone = value;
                    MaxZoneChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler MaxZoneChanged;

            [JsonConverter(typeof(SafeStringEnumConverter),
                StickOutCurve.Curve.Linear)]
            public StickOutCurve.Curve OutputCurve
            {
                get => hybridAimAction.OutputCurve;
                set
                {
                    hybridAimAction.OutputCurve = value;
                    OutputCurveChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler OutputCurveChanged;

            public bool EdgePushEnabled
            {
                get => hybridAimAction.EdgePushEnabled;
                set
                {
                    hybridAimAction.EdgePushEnabled = value;
                    EdgePushEnabledChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler EdgePushEnabledChanged;

            public bool ReturnDeadzoneEnabled
            {
                get => hybridAimAction.ReturnDeadzoneEnabled;
                set
                {
                    hybridAimAction.ReturnDeadzoneEnabled = value;
                    ReturnDeadzoneEnabledChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler ReturnDeadzoneEnabledChanged;

            public double ReturnDeadzoneAngle
            {
                get => hybridAimAction.ReturnDeadzoneAngle;
                set
                {
                    hybridAimAction.ReturnDeadzoneAngle = value;
                    ReturnDeadzoneAngleChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler ReturnDeadzoneAngleChanged;

            public double ReturnDeadzoneCutoffAngle
            {
                get => hybridAimAction.ReturnDeadzoneCutoffAngle;
                set
                {
                    hybridAimAction.ReturnDeadzoneCutoffAngle = value;
                    ReturnDeadzoneCutoffAngleChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler ReturnDeadzoneCutoffAngleChanged;

            public StickHybridAimSettings(StickHybridAim hybridAimAction)
            {
                this.hybridAimAction = hybridAimAction;
            }
        }

        private StickHybridAim hybridAimAction =
            new StickHybridAim();

        private StickHybridAimSettings settings;
        public StickHybridAimSettings Settings { get => settings; set => settings = value; }

        public StickHybridAimActionSerializer() : base()
        {
            mapAction = hybridAimAction;
            settings = new StickHybridAimSettings(hybridAimAction);

            NameChanged += StickHybridAimActionSerializer_NameChanged;
            settings.StickSensChanged += Settings_StickSensChanged;
            settings.MouselikeFactorChanged += Settings_MouselikeFactorChanged;
            settings.DeadZoneChanged += Settings_DeadZoneChanged;
            settings.MaxZoneChanged += Settings_MaxZoneChanged;
            settings.OutputCurveChanged += Settings_OutputCurveChanged;
            settings.EdgePushEnabledChanged += Settings_EdgePushEnabledChanged;
            settings.ReturnDeadzoneEnabledChanged += Settings_ReturnDeadzoneEnabledChanged;
            settings.ReturnDeadzoneAngleChanged += Settings_ReturnDeadzoneAngleChanged;
            settings.ReturnDeadzoneCutoffAngleChanged += Settings_ReturnDeadzoneCutoffAngleChanged;
        }

        public StickHybridAimActionSerializer(ActionLayer tempLayer, MapAction action) :
            base(tempLayer, action)
        {
            if (action is StickHybridAim temp)
            {
                hybridAimAction = temp;
                mapAction = hybridAimAction;
                settings = new StickHybridAimSettings(hybridAimAction);
            }
        }

        private void StickHybridAimActionSerializer_NameChanged(object sender, EventArgs e)
        {
            hybridAimAction.ChangedProperties.Add(StickHybridAim.PropertyKeyStrings.NAME);
        }

        private void Settings_StickSensChanged(object sender, EventArgs e)
        {
            hybridAimAction.ChangedProperties.Add(StickHybridAim.PropertyKeyStrings.STICK_SENS);
        }

        private void Settings_MouselikeFactorChanged(object sender, EventArgs e)
        {
            hybridAimAction.ChangedProperties.Add(StickHybridAim.PropertyKeyStrings.MOUSELIKE_FACTOR);
        }

        private void Settings_DeadZoneChanged(object sender, EventArgs e)
        {
            hybridAimAction.ChangedProperties.Add(StickHybridAim.PropertyKeyStrings.DEAD_ZONE);
        }

        private void Settings_MaxZoneChanged(object sender, EventArgs e)
        {
            hybridAimAction.ChangedProperties.Add(StickHybridAim.PropertyKeyStrings.MAX_ZONE);
        }

        private void Settings_OutputCurveChanged(object sender, EventArgs e)
        {
            hybridAimAction.ChangedProperties.Add(StickHybridAim.PropertyKeyStrings.OUTPUT_CURVE);
        }

        private void Settings_EdgePushEnabledChanged(object sender, EventArgs e)
        {
            hybridAimAction.ChangedProperties.Add(StickHybridAim.PropertyKeyStrings.EDGE_PUSH_ENABLED);
        }

        private void Settings_ReturnDeadzoneEnabledChanged(object sender, EventArgs e)
        {
            hybridAimAction.ChangedProperties.Add(StickHybridAim.PropertyKeyStrings.RETURN_DEADZONE_ENABLED);
        }

        private void Settings_ReturnDeadzoneAngleChanged(object sender, EventArgs e)
        {
            hybridAimAction.ChangedProperties.Add(StickHybridAim.PropertyKeyStrings.RETURN_DEADZONE_ANGLE);
        }

        private void Settings_ReturnDeadzoneCutoffAngleChanged(object sender, EventArgs e)
        {
            hybridAimAction.ChangedProperties.Add(StickHybridAim.PropertyKeyStrings.RETURN_DEADZONE_CUTOFF_ANGLE);
        }
    }

    public class StickMouseRingActionSerializer : MapActionSerializer
    {
        public class StickMouseRingSettings
        {
            private StickMouseRing ringAction;

            public double RingRadius
            {
                get => ringAction.RingRadius;
                set
                {
                    ringAction.RingRadius = value;
                    RingRadiusChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler RingRadiusChanged;

            public double DeadZone
            {
                get => ringAction.DeadMod.DeadZone;
                set
                {
                    ringAction.DeadMod.DeadZone = value;
                    DeadZoneChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler DeadZoneChanged;

            public double MaxZone
            {
                get => ringAction.DeadMod.MaxZone;
                set
                {
                    ringAction.DeadMod.MaxZone = value;
                    MaxZoneChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler MaxZoneChanged;

            public StickMouseRingSettings(StickMouseRing ringAction)
            {
                this.ringAction = ringAction;
            }
        }

        private StickMouseRing ringAction =
            new StickMouseRing();

        private StickMouseRingSettings settings;
        public StickMouseRingSettings Settings { get => settings; set => settings = value; }

        public StickMouseRingActionSerializer() : base()
        {
            mapAction = ringAction;
            settings = new StickMouseRingSettings(ringAction);

            NameChanged += StickMouseRingActionSerializer_NameChanged;
            settings.RingRadiusChanged += Settings_RingRadiusChanged;
            settings.DeadZoneChanged += Settings_DeadZoneChanged;
            settings.MaxZoneChanged += Settings_MaxZoneChanged;
        }

        public StickMouseRingActionSerializer(ActionLayer tempLayer, MapAction action) :
            base(tempLayer, action)
        {
            if (action is StickMouseRing temp)
            {
                ringAction = temp;
                mapAction = ringAction;
                settings = new StickMouseRingSettings(ringAction);
            }
        }

        private void StickMouseRingActionSerializer_NameChanged(object sender, EventArgs e)
        {
            ringAction.ChangedProperties.Add(StickMouseRing.PropertyKeyStrings.NAME);
        }

        private void Settings_RingRadiusChanged(object sender, EventArgs e)
        {
            ringAction.ChangedProperties.Add(StickMouseRing.PropertyKeyStrings.RING_RADIUS);
        }

        private void Settings_DeadZoneChanged(object sender, EventArgs e)
        {
            ringAction.ChangedProperties.Add(StickMouseRing.PropertyKeyStrings.DEAD_ZONE);
        }

        private void Settings_MaxZoneChanged(object sender, EventArgs e)
        {
            ringAction.ChangedProperties.Add(StickMouseRing.PropertyKeyStrings.MAX_ZONE);
        }
    }

    public class StickTranslateSerializer : MapActionSerializer
    {
        public class StickTranslateSettings
        {
            private StickTranslate stickTransAct;

            [JsonConverter(typeof(StringEnumConverter))]
            public StickActionCodes OutputStick
            {
                get => stickTransAct.OutputAction.StickCode;
                set
                {
                    stickTransAct.OutputAction.StickCode = value;
                    OutputStickChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler OutputStickChanged;

            public double DeadZone
            {
                get => stickTransAct.DeadMod.DeadZone;
                set
                {
                    stickTransAct.DeadMod.DeadZone = value;
                    DeadZoneChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler DeadZoneChanged;
            public bool ShouldSerializeDeadZone()
            {
                return stickTransAct.ChangedProperties.Contains(StickTranslate.PropertyKeyStrings.DEAD_ZONE);
            }

            public double MaxZone
            {
                get => stickTransAct.DeadMod.MaxZone;
                set
                {
                    stickTransAct.DeadMod.MaxZone = value;
                    MaxZoneChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler MaxZoneChanged;
            public bool ShouldSerializeMaxZone()
            {
                return stickTransAct.ChangedProperties.Contains(StickTranslate.PropertyKeyStrings.MAX_ZONE);
            }

            public double AntiDeadZone
            {
                get => stickTransAct.DeadMod.AntiDeadZone;
                set
                {
                    stickTransAct.DeadMod.AntiDeadZone = value;
                    AntiZoneChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler AntiZoneChanged;
            public bool ShouldSerializeAntiDeadZone()
            {
                return stickTransAct.ChangedProperties.Contains(StickTranslate.PropertyKeyStrings.ANTIDEAD_ZONE);
            }

            [JsonConverter(typeof(StringEnumConverter))]
            public StickDeadZone.DeadZoneTypes DeadZoneType
            {
                get => stickTransAct.DeadMod.DeadZoneType;
                set
                {
                    stickTransAct.DeadMod.DeadZoneType = value;
                    DeadZoneTypeChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler DeadZoneTypeChanged;
            public bool ShouldSerializeDeadZoneType()
            {
                return stickTransAct.ChangedProperties.Contains(StickTranslate.PropertyKeyStrings.DEAD_ZONE_TYPE);
            }

            [JsonConverter(typeof(StringEnumConverter))]
            public StickOutCurve.Curve OutputCurve
            {
                get => stickTransAct.OutputCurve;
                set
                {
                    stickTransAct.OutputCurve = value;
                    OutputCurveChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler OutputCurveChanged;
            public bool ShouldSerializeOutputCurve()
            {
                return stickTransAct.ChangedProperties.Contains(StickTranslate.PropertyKeyStrings.OUTPUT_CURVE);
            }

            public bool InvertX
            {
                get => stickTransAct.InvertX;
                set
                {
                    stickTransAct.InvertX = value;
                    InvertXChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler InvertXChanged;
            public bool ShouldSerializeInvertX()
            {
                return stickTransAct.ChangedProperties.Contains(StickTranslate.PropertyKeyStrings.INVERT_X);
            }

            public bool InvertY
            {
                get => stickTransAct.InvertY;
                set
                {
                    stickTransAct.InvertY = value;
                    InvertYChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler InvertYChanged;
            public bool ShouldSerializeInvertY()
            {
                return stickTransAct.ChangedProperties.Contains(StickTranslate.PropertyKeyStrings.INVERT_Y);
            }

            public int Rotation
            {
                get => stickTransAct.Rotation;
                set
                {
                    stickTransAct.Rotation = Math.Clamp(value, -180, 180);
                    RotationChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler RotationChanged;
            public bool ShouldSerializeRotation()
            {
                return stickTransAct.ChangedProperties.Contains(StickTranslate.PropertyKeyStrings.ROTATION);
            }

            public double VerticalScale
            {
                get => stickTransAct.VerticalScale;
                set
                {
                    stickTransAct.VerticalScale = Math.Clamp(value, 0.01, 10.0);
                    VerticalScaleChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler VerticalScaleChanged;
            public bool ShouldSerializeVerticalScale()
            {
                return stickTransAct.ChangedProperties.Contains(StickTranslate.PropertyKeyStrings.VERTICAL_SCALE);
            }

            public bool MaxOutputEnabled
            {
                get => stickTransAct.MaxOutputEnabled;
                set
                {
                    stickTransAct.MaxOutputEnabled = value;
                    MaxOutputEnabledChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler MaxOutputEnabledChanged;
            public bool ShouldSerializeMaxOutputEnabled()
            {
                return stickTransAct.ChangedProperties.Contains(StickTranslate.PropertyKeyStrings.MAX_OUTPUT_ENABLED);
            }

            public double MaxOutput
            {
                get => stickTransAct.MaxOutput;
                set
                {
                    stickTransAct.MaxOutput = Math.Clamp(value, 0.0, 1.0);
                    MaxOutputChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler MaxOutputChanged;
            public bool ShouldSerializeMaxOutput()
            {
                return stickTransAct.ChangedProperties.Contains(StickTranslate.PropertyKeyStrings.MAX_OUTPUT);
            }

            public bool SquareStickEnabled
            {
                get => stickTransAct.SquareStickEnabled;
                set
                {
                    stickTransAct.SquareStickEnabled = value;
                    SquareStickEnabledChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler SquareStickEnabledChanged;
            public bool ShouldSerializeSquareStickEnabled()
            {
                return stickTransAct.ChangedProperties.Contains(StickTranslate.PropertyKeyStrings.SQUARE_STICK_ENABLED);
            }

            public double SquareStickRoundness
            {
                get => stickTransAct.SquareStickRoundness;
                set
                {
                    stickTransAct.SquareStickRoundness = Math.Clamp(value, 1.0, 10.0);
                    SquareStickRoundnessChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler SquareStickRoundnessChanged;
            public bool ShouldSerializeSquareStickRoundness()
            {
                return stickTransAct.ChangedProperties.Contains(StickTranslate.PropertyKeyStrings.SQUARE_STICK_ROUNDNESS);
            }

            public StickTranslateSettings(StickTranslate action)
            {
                this.stickTransAct = action;
            }
        }

        private StickTranslate stickTransAct =
            new StickTranslate();

        private StickTranslateSettings settings;
        public StickTranslateSettings Settings
        {
            get => settings;
            set => settings = value;
        }
        public bool ShouldSerializeSettings()
        {
            return stickTransAct.ChangedProperties.Count > 0;
        }

        // Deserialize
        public StickTranslateSerializer() : base()
        {
            mapAction = stickTransAct;
            settings = new StickTranslateSettings(stickTransAct);

            NameChanged += StickTranslateSerializer_NameChanged;
            settings.OutputStickChanged += Settings_OutputStickChanged;
            settings.DeadZoneChanged += Settings_DeadZoneChanged;
            settings.MaxZoneChanged += Settings_MaxZoneChanged;
            settings.AntiZoneChanged += Settings_AntiZoneChanged;
            settings.OutputCurveChanged += Settings_OutputCurveChanged;
            settings.InvertXChanged += Settings_InvertXChanged;
            settings.InvertYChanged += Settings_InvertYChanged;
            settings.RotationChanged += Settings_RotationChanged;
            settings.VerticalScaleChanged += Settings_VerticalScaleChanged;
            settings.MaxOutputEnabledChanged += Settings_MaxOutputEnabledChanged;
            settings.MaxOutputChanged += Settings_MaxOutputChanged;
            settings.SquareStickEnabledChanged += Settings_SquareStickEnabledChanged;
            settings.SquareStickRoundnessChanged += Settings_SquareStickRoundnessChanged;
            settings.DeadZoneTypeChanged += Settings_DeadZoneTypeChanged;
        }

        private void Settings_DeadZoneTypeChanged(object sender, EventArgs e)
        {
            stickTransAct.ChangedProperties.Add(StickTranslate.PropertyKeyStrings.DEAD_ZONE_TYPE);
        }

        private void Settings_SquareStickRoundnessChanged(object sender, EventArgs e)
        {
            stickTransAct.ChangedProperties.Add(StickTranslate.PropertyKeyStrings.SQUARE_STICK_ROUNDNESS);
        }

        private void Settings_SquareStickEnabledChanged(object sender, EventArgs e)
        {
            stickTransAct.ChangedProperties.Add(StickTranslate.PropertyKeyStrings.SQUARE_STICK_ENABLED);
        }

        private void Settings_MaxOutputChanged(object sender, EventArgs e)
        {
            stickTransAct.ChangedProperties.Add(StickTranslate.PropertyKeyStrings.MAX_OUTPUT);
        }

        private void Settings_MaxOutputEnabledChanged(object sender, EventArgs e)
        {
            stickTransAct.ChangedProperties.Add(StickTranslate.PropertyKeyStrings.MAX_OUTPUT_ENABLED);
        }

        private void Settings_VerticalScaleChanged(object sender, EventArgs e)
        {
            stickTransAct.ChangedProperties.Add(StickTranslate.PropertyKeyStrings.VERTICAL_SCALE);
        }

        private void Settings_RotationChanged(object sender, EventArgs e)
        {
            stickTransAct.ChangedProperties.Add(StickTranslate.PropertyKeyStrings.ROTATION);
        }

        private void Settings_InvertYChanged(object sender, EventArgs e)
        {
            stickTransAct.ChangedProperties.Add(StickTranslate.PropertyKeyStrings.INVERT_Y);
        }

        private void Settings_InvertXChanged(object sender, EventArgs e)
        {
            stickTransAct.ChangedProperties.Add(StickTranslate.PropertyKeyStrings.INVERT_X);
        }

        private void Settings_OutputCurveChanged(object sender, EventArgs e)
        {
            stickTransAct.ChangedProperties.Add(StickTranslate.PropertyKeyStrings.OUTPUT_CURVE);
        }

        private void Settings_AntiZoneChanged(object sender, EventArgs e)
        {
            stickTransAct.ChangedProperties.Add(StickTranslate.PropertyKeyStrings.ANTIDEAD_ZONE);
        }

        private void StickTranslateSerializer_NameChanged(object sender, EventArgs e)
        {
            stickTransAct.ChangedProperties.Add(StickTranslate.PropertyKeyStrings.NAME);
        }

        private void Settings_MaxZoneChanged(object sender, EventArgs e)
        {
            stickTransAct.ChangedProperties.Add(StickTranslate.PropertyKeyStrings.MAX_ZONE);
        }

        private void Settings_DeadZoneChanged(object sender, EventArgs e)
        {
            stickTransAct.ChangedProperties.Add(StickTranslate.PropertyKeyStrings.DEAD_ZONE);
        }

        private void Settings_OutputStickChanged(object sender, EventArgs e)
        {
            stickTransAct.ChangedProperties.Add(StickTranslate.PropertyKeyStrings.OUTPUT_STICK);
        }

        // Serialize ctor
        public StickTranslateSerializer(ActionLayer tempLayer, MapAction action) :
            base(tempLayer, action)
        {
            if (action is StickTranslate temp)
            {
                stickTransAct = temp;
                mapAction = stickTransAct;
                settings = new StickTranslateSettings(stickTransAct);
            }
        }
    }

    public class StickAbsMouseActionSerializer : MapActionSerializer
    {
        public class OuterRingBinding
        {
            private string actionDirName;
            [JsonProperty("Name", Required = Required.Default)]
            public string ActionDirName
            {
                get => actionDirName;
                set => actionDirName = value;
            }

            private List<ActionFuncSerializer> actionFuncSerializers =
                new List<ActionFuncSerializer>();
            [JsonProperty("Functions", Required = Required.Always)]
            public List<ActionFuncSerializer> ActionFuncSerializers
            {
                get => actionFuncSerializers;
                set
                {
                    actionFuncSerializers = value;
                    ActionFuncSerializersChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler ActionFuncSerializersChanged;
        }

        public class StickAbsMouseSettings
        {
            private StickAbsMouse stickAbsMouseAction;

            public double DeadZone
            {
                get => stickAbsMouseAction.DeadMod.DeadZone;
                set
                {
                    stickAbsMouseAction.DeadMod.DeadZone = Math.Clamp(value, 0.0, 1.0);
                    DeadZoneChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler DeadZoneChanged;

            public double MaxZone
            {
                get => stickAbsMouseAction.DeadMod.MaxZone;
                set
                {
                    stickAbsMouseAction.DeadMod.MaxZone = Math.Clamp(value, 0.0, 1.0);
                    MaxZoneChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler MaxZoneChanged;

            [JsonProperty("UseOuterRing")]
            public bool UseOuterRing
            {
                get => stickAbsMouseAction.UseRingButton;
                set
                {
                    stickAbsMouseAction.UseRingButton = value;
                    UseOuterRingChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler UseOuterRingChanged;

            [JsonProperty("OuterRingDeadZone")]
            public double OuterRingDeadZone
            {
                get => stickAbsMouseAction.OuterRingDeadZone;
                set
                {
                    stickAbsMouseAction.OuterRingDeadZone = value;
                    OuterRingDeadZoneChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler OuterRingDeadZoneChanged;

            [JsonProperty("UseAsOuterRing")]
            public bool UseAsOuterRing
            {
                get => stickAbsMouseAction.UseAsOuterRing;
                set
                {
                    stickAbsMouseAction.UseAsOuterRing = value;
                    UseAsOuterRingChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler UseAsOuterRingChanged;

            public bool SnapToCenterOnRelease
            {
                get => stickAbsMouseAction.SnapToCenterRelease;
                set
                {
                    stickAbsMouseAction.SnapToCenterRelease = value;
                    SnapToCenterOnReleaseChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler SnapToCenterOnReleaseChanged;

            public double Width
            {
                get => stickAbsMouseAction.AbsMouseRange.width;
                set
                {
                    stickAbsMouseAction.AbsMouseRange.width = Math.Clamp(value, 0.0, 1.0);
                    WidthChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler WidthChanged;

            public double Height
            {
                get => stickAbsMouseAction.AbsMouseRange.height;
                set
                {
                    stickAbsMouseAction.AbsMouseRange.height = Math.Clamp(value, 0.0, 1.0);
                    HeightChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler HeightChanged;

            public double XCenter
            {
                get => stickAbsMouseAction.AbsMouseRange.xcenter;
                set
                {
                    stickAbsMouseAction.AbsMouseRange.xcenter = Math.Clamp(value, 0.0, 1.0);
                    XCenterChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler XCenterChanged;

            public double YCenter
            {
                get => stickAbsMouseAction.AbsMouseRange.ycenter;
                set
                {
                    stickAbsMouseAction.AbsMouseRange.ycenter = Math.Clamp(value, 0.0, 1.0);
                    YCenterChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler YCenterChanged;

            public StickAbsMouseSettings(StickAbsMouse absMouseAction)
            {
                stickAbsMouseAction = absMouseAction;
            }
        }

        private OuterRingBinding ringBinding;

        [JsonProperty("OuterRingBinding")]
        public OuterRingBinding RingBinding
        {
            get => ringBinding;
            set
            {
                ringBinding = value;
                RingBindingChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        public event EventHandler RingBindingChanged;

        private StickAbsMouse stickAbsMouseAction = new StickAbsMouse();
        private StickAbsMouseSettings settings;
        public StickAbsMouseSettings Settings
        {
            get => settings;
            set => settings = value;
        }

        // Deserialize
        public StickAbsMouseActionSerializer() : base()
        {
            mapAction = stickAbsMouseAction;
            settings = new StickAbsMouseSettings(stickAbsMouseAction);

            NameChanged += StickAbsMouseActionSerializer_NameChanged;
            RingBindingChanged += StickAbsMouseActionSerializer_RingBindingChanged;
            settings.DeadZoneChanged += Settings_DeadZoneChanged;
            settings.MaxZoneChanged += Settings_MaxZoneChanged;
            settings.UseAsOuterRingChanged += Settings_UseAsOuterRingChanged;
            settings.UseOuterRingChanged += Settings_UseOuterRingChanged;
            settings.OuterRingDeadZoneChanged += Settings_OuterRingDeadZoneChanged;
            settings.SnapToCenterOnReleaseChanged += Settings_SnapToCenterOnReleaseChanged;

            settings.WidthChanged += Settings_WidthChanged;
            settings.HeightChanged += Settings_HeightChanged;
            settings.XCenterChanged += Settings_XCenterChanged;
            settings.YCenterChanged += Settings_YCenterChanged;
        }

        private void Settings_YCenterChanged(object sender, EventArgs e)
        {
            stickAbsMouseAction.ChangedProperties.Add(StickAbsMouse.PropertyKeyStrings.BOX_YCENTER);
        }

        private void Settings_XCenterChanged(object sender, EventArgs e)
        {
            stickAbsMouseAction.ChangedProperties.Add(StickAbsMouse.PropertyKeyStrings.BOX_XCENTER);
        }

        private void Settings_HeightChanged(object sender, EventArgs e)
        {
            stickAbsMouseAction.ChangedProperties.Add(StickAbsMouse.PropertyKeyStrings.BOX_HEIGHT);
        }

        private void Settings_WidthChanged(object sender, EventArgs e)
        {
            stickAbsMouseAction.ChangedProperties.Add(StickAbsMouse.PropertyKeyStrings.BOX_WIDTH);
        }

        private void Settings_SnapToCenterOnReleaseChanged(object sender, EventArgs e)
        {
            stickAbsMouseAction.ChangedProperties.Add(StickAbsMouse.PropertyKeyStrings.SNAP_TO_CENTER_RELEASE);
        }

        // Serialize
        public StickAbsMouseActionSerializer(ActionLayer tempLayer, MapAction action) :
            base(tempLayer, action)
        {
            if (action is StickAbsMouse temp)
            {
                stickAbsMouseAction = temp;
                mapAction = stickAbsMouseAction;
                settings = new StickAbsMouseSettings(stickAbsMouseAction);
            }
        }

        private void Settings_DeadZoneChanged(object sender, EventArgs e)
        {
            stickAbsMouseAction.ChangedProperties.Add(StickAbsMouse.PropertyKeyStrings.DEAD_ZONE);
        }

        private void Settings_MaxZoneChanged(object sender, EventArgs e)
        {
            stickAbsMouseAction.ChangedProperties.Add(StickAbsMouse.PropertyKeyStrings.MAX_ZONE);
        }

        private void StickAbsMouseActionSerializer_NameChanged(object sender, EventArgs e)
        {
            stickAbsMouseAction.ChangedProperties.Add(StickAbsMouse.PropertyKeyStrings.NAME);
        }

        private void Settings_OuterRingDeadZoneChanged(object sender, EventArgs e)
        {
            stickAbsMouseAction.ChangedProperties.Add(StickAbsMouse.PropertyKeyStrings.OUTER_RING_DEAD_ZONE);
        }

        private void Settings_UseOuterRingChanged(object sender, EventArgs e)
        {
            stickAbsMouseAction.ChangedProperties.Add(StickAbsMouse.PropertyKeyStrings.USE_OUTER_RING);
        }

        private void Settings_UseAsOuterRingChanged(object sender, EventArgs e)
        {
            stickAbsMouseAction.ChangedProperties.Add(StickAbsMouse.PropertyKeyStrings.USE_AS_OUTER_RING);
        }

        private void StickAbsMouseActionSerializer_RingBindingChanged(object sender, EventArgs e)
        {
            stickAbsMouseAction.ChangedProperties.Add(StickAbsMouse.PropertyKeyStrings.OUTER_RING_BUTTON);

            stickAbsMouseAction.RingButton.Name = ringBinding.ActionDirName;
            stickAbsMouseAction.RingButton.ActionFuncs.Clear();
            List<ActionFuncSerializer> tempSerializers = ringBinding.ActionFuncSerializers;
            foreach (ActionFuncSerializer serializer in tempSerializers)
            {
                serializer.PopulateFunc();
                stickAbsMouseAction.RingButton.ActionFuncs.Add(serializer.ActionFunc);
            }
        }
    }

    public class StickCircularSerializer : MapActionSerializer
    {
        public class StickCircBtnBinding
        {
            private string actionDirName;
            [JsonProperty("Name", Required = Required.Default)]
            public string ActionDirName
            {
                get => actionDirName;
                set => actionDirName = value;
            }
            public bool ShouldSerializeActionDirName()
            {
                return !string.IsNullOrEmpty(actionDirName);
            }

            private List<ActionFuncSerializer> actionFuncSerializers =
                new List<ActionFuncSerializer>();
            [JsonProperty("Functions", Required = Required.Always)]
            public List<ActionFuncSerializer> ActionFuncSerializers
            {
                get => actionFuncSerializers;
                set
                {
                    actionFuncSerializers = value;
                    ActionFuncSerializersChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler ActionFuncSerializersChanged;
        }

        public class StickCircularSettings
        {
            private StickCircular stickCircAct;

            public double Sensitivity
            {
                get => stickCircAct.Sensitivity;
                set
                {
                    stickCircAct.Sensitivity = Math.Clamp(value, 0.0, 10.0);
                    SensitivityChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler SensitivityChanged;

            public bool ShouldSerializeSensitivity()
            {
                return stickCircAct.ChangedProperties.Contains(StickCircular.PropertyKeyStrings.SENSITIVITY);
            }

            [JsonConverter(typeof(StringEnumConverter))]
            public MapAction.HapticsIntensity HapticsIntensity
            {
                get => stickCircAct.ActionHapticsIntensity;
                set
                {
                    stickCircAct.ActionHapticsIntensity = value;
                    HapticsIntensityChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler HapticsIntensityChanged;
            public bool ShouldSerializeHapticsIntensity()
            {
                return stickCircAct.ChangedProperties.Contains(StickCircular.PropertyKeyStrings.HAPTICS_INTENSITY);
            }

            public StickCircularSettings(StickCircular action)
            {
                stickCircAct = action;
            }
        }

        private StickCircular stickCircAct = new StickCircular();

        private StickCircBtnBinding clockwiseBinding;
        public StickCircBtnBinding Clockwise
        {
            get => clockwiseBinding;
            set
            {
                clockwiseBinding = value;
                ClockwiseChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        private event EventHandler ClockwiseChanged;

        private StickCircBtnBinding counterClockwiseBinding;
        public StickCircBtnBinding CounterClockwise
        {
            get => counterClockwiseBinding;
            set
            {
                counterClockwiseBinding = value;
                CounterClockwiseChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        private event EventHandler CounterClockwiseChanged;

        private StickCircularSettings settings;
        public StickCircularSettings Settings
        {
            get => settings;
            set => settings = value;
        }

        // Deserialize
        public StickCircularSerializer() : base()
        {
            mapAction = stickCircAct;
            settings = new StickCircularSettings(stickCircAct);

            NameChanged += StickpadCircularSerializer_NameChanged;
            ClockwiseChanged += StickCircularSerializer_ClockwiseChanged;
            CounterClockwiseChanged += StickCircularSerializer_CounterClockwiseChanged;
            settings.SensitivityChanged += Settings_SensitivityChanged;
            settings.HapticsIntensityChanged += Settings_HapticIntensityChanged;
        }

        private void Settings_HapticIntensityChanged(object sender, EventArgs e)
        {
            stickCircAct.ChangedProperties.Add(StickCircular.PropertyKeyStrings.HAPTICS_INTENSITY);
        }

        private void Settings_SensitivityChanged(object sender, EventArgs e)
        {
            stickCircAct.ChangedProperties.Add(StickCircular.PropertyKeyStrings.SENSITIVITY);
        }

        // Serialize
        public StickCircularSerializer(ActionLayer tempLayer, MapAction mapAction) :
            base(tempLayer, mapAction)
        {
            if (mapAction is StickCircular temp)
            {
                stickCircAct = temp;
                this.mapAction = stickCircAct;
                settings = new StickCircularSettings(stickCircAct);

                PopulateFuncs();
            }
        }

        // Post-deserialize
        public override void PopulateMap()
        {
            stickCircAct.ClockWiseBtn.ActionFuncs.Clear();
            stickCircAct.CounterClockwiseBtn.ActionFuncs.Clear();

            TouchpadCircularButton tempBtn = null;

            if (clockwiseBinding != null)
            {
                tempBtn = stickCircAct.ClockWiseBtn;
                tempBtn.Name = clockwiseBinding.ActionDirName;
                foreach (ActionFuncSerializer serializer in clockwiseBinding.ActionFuncSerializers)
                {
                    serializer.PopulateFunc();
                    tempBtn.ActionFuncs.Add(serializer.ActionFunc);
                }

                stickCircAct.ChangedProperties.Add(StickCircular.PropertyKeyStrings.SCROLL_BUTTON_1);
                tempBtn = null;
            }

            if (counterClockwiseBinding != null)
            {
                tempBtn = stickCircAct.CounterClockwiseBtn;
                tempBtn.Name = counterClockwiseBinding.ActionDirName;
                foreach (ActionFuncSerializer serializer in counterClockwiseBinding.ActionFuncSerializers)
                {
                    serializer.PopulateFunc();
                    tempBtn.ActionFuncs.Add(serializer.ActionFunc);
                }

                stickCircAct.ChangedProperties.Add(StickCircular.PropertyKeyStrings.SCROLL_BUTTON_2);
                tempBtn = null;
            }
        }

        // Pre-serialize
        public void PopulateFuncs()
        {
            List<ActionFuncSerializer> tempFuncs = new List<ActionFuncSerializer>();

            clockwiseBinding = new StickCircBtnBinding();
            clockwiseBinding.ActionDirName = stickCircAct.ClockWiseBtn.Name;
            foreach (ActionFunc tempFunc in stickCircAct.ClockWiseBtn.ActionFuncs)
            {
                ActionFuncSerializer tempSerializer =
                        ActionFuncSerializerFactory.CreateSerializer(tempFunc);
                tempFuncs.Add(tempSerializer);
            }
            clockwiseBinding.ActionFuncSerializers.AddRange(tempFuncs);

            tempFuncs.Clear();
            counterClockwiseBinding = new StickCircBtnBinding();
            counterClockwiseBinding.ActionDirName = stickCircAct.CounterClockwiseBtn.Name;
            foreach (ActionFunc tempFunc in stickCircAct.CounterClockwiseBtn.ActionFuncs)
            {
                ActionFuncSerializer tempSerializer =
                        ActionFuncSerializerFactory.CreateSerializer(tempFunc);
                tempFuncs.Add(tempSerializer);
            }
            counterClockwiseBinding.ActionFuncSerializers.AddRange(tempFuncs);
        }

        private void StickCircularSerializer_CounterClockwiseChanged(object sender, EventArgs e)
        {
            stickCircAct.ChangedProperties.Add(StickCircular.PropertyKeyStrings.SCROLL_BUTTON_2);
        }

        private void StickCircularSerializer_ClockwiseChanged(object sender, EventArgs e)
        {
            stickCircAct.ChangedProperties.Add(StickCircular.PropertyKeyStrings.SCROLL_BUTTON_1);
        }

        private void StickpadCircularSerializer_NameChanged(object sender, EventArgs e)
        {
            stickCircAct.ChangedProperties.Add(StickCircular.PropertyKeyStrings.NAME);
        }
    }

    public static class GyroActionsUtils
    {
        public enum GyroTriggerEvalCond
        {
            And,
            Or,
        }
    }

    public class GyroMouseSerializer : MapActionSerializer
    {
        public class GyroMouseSettings
        {
            private GyroMouse gyroMouseAction;

            public double DeadZone
            {
                get => gyroMouseAction.mouseParams.deadzone;
                set
                {
                    gyroMouseAction.mouseParams.deadzone = value;
                    DeadZoneChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler DeadZoneChanged;

            public double VerticalDeadZone
            {
                get => gyroMouseAction.mouseParams.verticalDeadZone;
                set
                {
                    gyroMouseAction.mouseParams.verticalDeadZone = Math.Clamp(value, 0.0, 1000.0);
                    VerticalDeadZoneChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler VerticalDeadZoneChanged;
            public bool ShouldSerializeVerticalDeadZone()
            {
                return gyroMouseAction.ChangedProperties.Contains(GyroMouse.PropertyKeyStrings.VERTICAL_DEAD_ZONE);
            }

            public double GyroAngleSnapDegrees
            {
                get => gyroMouseAction.mouseParams.gyroAngleSnapDegrees;
                set
                {
                    gyroMouseAction.mouseParams.gyroAngleSnapDegrees = Math.Clamp(value, 0.0, 45.0);
                    GyroAngleSnapDegreesChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler GyroAngleSnapDegreesChanged;
            public bool ShouldSerializeGyroAngleSnapDegrees()
            {
                return gyroMouseAction.ChangedProperties.Contains(GyroMouse.PropertyKeyStrings.ANGLE_SNAP_DEGREES);
            }

            public bool GyroSmoothAngleSnap
            {
                get => gyroMouseAction.mouseParams.gyroSmoothAngleSnap;
                set
                {
                    gyroMouseAction.mouseParams.gyroSmoothAngleSnap = value;
                    GyroSmoothAngleSnapChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler GyroSmoothAngleSnapChanged;
            public bool ShouldSerializeGyroSmoothAngleSnap()
            {
                return gyroMouseAction.ChangedProperties.Contains(GyroMouse.PropertyKeyStrings.SMOOTH_ANGLE_SNAP);
            }

            public double RealWorlCalibration
            {
                get => gyroMouseAction.mouseParams.realWorldCalibration;
                set
                {
                    gyroMouseAction.mouseParams.realWorldCalibration = value;
                    RealWorldCalibrationChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler RealWorldCalibrationChanged;
            public bool ShouldSerializeRealWorlCalibration() => false;

            public double InGameSens
            {
                get => gyroMouseAction.mouseParams.inGameSens;
                set
                {
                    gyroMouseAction.mouseParams.inGameSens = value;
                    InGameSensChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler InGameSensChanged;
            public bool ShouldSerializeInGameSens() => false;

            [JsonConverter(typeof(StringEnumConverter))]
            public GyroMouseAccelCurveChoice AccelCurve
            {
                get => gyroMouseAction.mouseParams.accelCurve;
                set
                {
                    gyroMouseAction.mouseParams.accelCurve = value;
                    AccelCurveChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler AccelCurveChanged;
            public bool ShouldSerializeAccelCurve()
            {
                //return gyroMouseAction.ChangedProperties.Contains(GyroMouse.PropertyKeyStrings.ACCEL_CURVE);
                return gyroMouseAction.mouseParams.accelCurve !=
                    GyroMouseParams.ACCEL_CURVE_DEFAULT;
            }

            public double MinAccelXSens
            {
                get => gyroMouseAction.mouseParams.minAccelXSens;
                set
                {
                    gyroMouseAction.mouseParams.minAccelXSens = Math.Clamp(value, 0.0, 100.0);
                    MinAccelXSensChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler MinAccelXSensChanged;

            public bool ShouldSerializeMinAccelXSens()
            {
                return gyroMouseAction.ChangedProperties.Contains(GyroMouse.PropertyKeyStrings.MIN_ACCEL_X_SENS);
            }

            public double MaxAccelXSens
            {
                get => gyroMouseAction.mouseParams.maxAccelXSens;
                set
                {
                    gyroMouseAction.mouseParams.maxAccelXSens = Math.Clamp(value, 0.0, 100.0);
                    MaxAccelXSensChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler MaxAccelXSensChanged;

            public bool ShouldSerializeMaxAccelXSens()
            {
                return gyroMouseAction.ChangedProperties.Contains(GyroMouse.PropertyKeyStrings.MAX_ACCEL_X_SENS);
            }


            public double MinAccelYSens
            {
                get => gyroMouseAction.mouseParams.minAccelYSens;
                set
                {
                    gyroMouseAction.mouseParams.minAccelYSens = Math.Clamp(value, 0.0, 100.0);
                    MinAccelYSensChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler MinAccelYSensChanged;

            public bool ShouldSerializeMinAccelYSens()
            {
                return gyroMouseAction.ChangedProperties.Contains(GyroMouse.PropertyKeyStrings.MIN_ACCEL_Y_SENS);
            }

            public double MaxAccelYSens
            {
                get => gyroMouseAction.mouseParams.maxAccelYSens;
                set
                {
                    gyroMouseAction.mouseParams.maxAccelYSens = Math.Clamp(value, 0.0, 100.0);
                    MaxAccelYSensChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler MaxAccelYSensChanged;

            public bool ShouldSerializeMaxAccelYSens()
            {
                return gyroMouseAction.ChangedProperties.Contains(GyroMouse.PropertyKeyStrings.MAX_ACCEL_Y_SENS);
            }

            public double MinGyroThreshold
            {
                get => gyroMouseAction.mouseParams.minGyroThreshold;
                set
                {
                    gyroMouseAction.mouseParams.minGyroThreshold = Math.Clamp(value, 0.0, 500.0);
                    MinGyroThresholdChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler MinGyroThresholdChanged;

            public bool ShouldSerializeMinGyroThreshold()
            {
                return gyroMouseAction.ChangedProperties.Contains(GyroMouse.PropertyKeyStrings.MIN_GYRO_THRESHOLD);
            }

            public double MaxGyroThreshold
            {
                get => gyroMouseAction.mouseParams.maxGyroThreshold;
                set
                {
                    gyroMouseAction.mouseParams.maxGyroThreshold = Math.Clamp(value, 0.0, 500.0);
                    MaxGyroThresholdChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler MaxGyroThresholdChanged;

            public bool ShouldSerializeMaxGyroThreshold()
            {
                return gyroMouseAction.ChangedProperties.Contains(GyroMouse.PropertyKeyStrings.MAX_GYRO_THRESHOLD);
            }

            public double PowerVRef
            {
                get => gyroMouseAction.mouseParams.powerVRef;
                set
                {
                    gyroMouseAction.mouseParams.powerVRef = Math.Clamp(value, 0.1, 500.0);
                    PowerVRefChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler PowerVRefChanged;

            public bool ShouldSerializePowerVRef()
            {
                return gyroMouseAction.ChangedProperties.Contains(GyroMouse.PropertyKeyStrings.POWER_CURVE_VREF);
            }

            public double PowerExponent
            {
                get => gyroMouseAction.mouseParams.powerExponent;
                set
                {
                    gyroMouseAction.mouseParams.powerExponent = Math.Clamp(value, 0.1, 500.0);
                    PowerExponentChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler PowerExponentChanged;

            public bool ShouldSerializePowerExponent()
            {
                return gyroMouseAction.ChangedProperties.Contains(GyroMouse.PropertyKeyStrings.POWER_CURVE_EXPONENT);
            }

            public double NaturalVHalf
            {
                get => gyroMouseAction.mouseParams.naturalVHalf;
                set
                {
                    gyroMouseAction.mouseParams.naturalVHalf = Math.Clamp(value, 0.1, 500.0);
                    NaturalVHalfChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler NaturalVHalfChanged;
            public bool ShouldSerializeNaturalVHalf()
            {
                return gyroMouseAction.ChangedProperties.Contains(GyroMouse.PropertyKeyStrings.NATURAL_CURVE_VHALF);
            }

            public double Sensitivity
            {
                get => gyroMouseAction.mouseParams.sensitivity;
                set
                {
                    gyroMouseAction.mouseParams.sensitivity = value;
                    SensitivityChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler SensitivityChanged;
            public bool ShouldSerializeSensitivityd()
            {
                return gyroMouseAction.ChangedProperties.Contains(GyroMouse.PropertyKeyStrings.SENSITIVITY);
            }

            public double VerticalScale
            {
                get => gyroMouseAction.mouseParams.verticalScale;
                set
                {
                    gyroMouseAction.mouseParams.verticalScale = Math.Clamp(value, 0.0, 10.0);
                    VerticalScaleChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler VerticalScaleChanged;

            public bool InvertX
            {
                get => gyroMouseAction.mouseParams.invertX;
                set
                {
                    gyroMouseAction.mouseParams.invertX = value;
                    InvertXChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler InvertXChanged;

            public bool InvertY
            {
                get => gyroMouseAction.mouseParams.invertY;
                set
                {
                    gyroMouseAction.mouseParams.invertY = value;
                    InvertYChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler InvertYChanged;

            [JsonConverter(typeof(TriggerButtonsConverter))]
            public JoypadActionCodes[] TriggerButtons
            {
                get => gyroMouseAction.mouseParams.gyroTriggerButtons;
                set
                {
                    gyroMouseAction.mouseParams.gyroTriggerButtons = value;
                    TriggersButtonChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler TriggersButtonChanged;

            public bool TriggerActivates
            {
                get => gyroMouseAction.mouseParams.triggerActivates;
                set
                {
                    gyroMouseAction.mouseParams.triggerActivates = value;
                    TriggerActivatesChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler TriggerActivatesChanged;

            public int ActivationHoldMs
            {
                get => gyroMouseAction.mouseParams.activationHoldMs;
                set => gyroMouseAction.mouseParams.activationHoldMs = Math.Clamp(value, 0, 60000);
            }
            public bool ShouldSerializeActivationHoldMs() =>
                gyroMouseAction.ChangedProperties.Contains(GyroMouse.PropertyKeyStrings.ACTIVATION_HOLD_MS);

            [JsonConverter(typeof(StringEnumConverter))]
            public GyroActionsUtils.GyroTriggerEvalCond EvalCond
            {
                get => gyroMouseAction.mouseParams.andCond ?
                    GyroActionsUtils.GyroTriggerEvalCond.And : GyroActionsUtils.GyroTriggerEvalCond.Or;
                set
                {
                    gyroMouseAction.mouseParams.andCond =
                        value == GyroActionsUtils.GyroTriggerEvalCond.And ? true : false;

                    EvalCondChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler EvalCondChanged;

            [JsonConverter(typeof(StringEnumConverter))]
            public GyroMouseXAxisChoice UseForXAxis
            {
                get => gyroMouseAction.mouseParams.useForXAxis;
                set
                {
                    gyroMouseAction.mouseParams.useForXAxis = value;
                    UseForXAxisChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler UseForXAxisChanged;

            [JsonConverter(typeof(StringEnumConverter))]
            public GyroSpaceChoice GyroSpace
            {
                get => gyroMouseAction.mouseParams.orientation.gyroSpace;
                set
                {
                    gyroMouseAction.mouseParams.orientation.gyroSpace = value;
                    GyroSpaceChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler GyroSpaceChanged;
            public bool ShouldSerializeGyroSpace() =>
                gyroMouseAction.ChangedProperties.Contains(GyroMouse.PropertyKeyStrings.GYRO_SPACE);

            [JsonConverter(typeof(StringEnumConverter))]
            public GyroLocalAxisSource HorizontalControl
            {
                get => gyroMouseAction.mouseParams.orientation.horizontal.source;
                set
                {
                    gyroMouseAction.mouseParams.orientation.horizontal.source = value;
                    HorizontalControlChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler HorizontalControlChanged;
            public bool ShouldSerializeHorizontalControl() =>
                gyroMouseAction.ChangedProperties.Contains(GyroMouse.PropertyKeyStrings.HORIZONTAL_CONTROL);

            [JsonConverter(typeof(StringEnumConverter))]
            public GyroLocalAxisSource VerticalControl
            {
                get => gyroMouseAction.mouseParams.orientation.vertical.source;
                set
                {
                    gyroMouseAction.mouseParams.orientation.vertical.source = value;
                    VerticalControlChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler VerticalControlChanged;
            public bool ShouldSerializeVerticalControl() =>
                gyroMouseAction.ChangedProperties.Contains(GyroMouse.PropertyKeyStrings.VERTICAL_CONTROL);

            public bool HorizontalInvert
            {
                get => gyroMouseAction.mouseParams.orientation.horizontal.invertSingle;
                set
                {
                    gyroMouseAction.mouseParams.orientation.horizontal.invertSingle = value;
                    HorizontalInvertChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler HorizontalInvertChanged;
            public bool ShouldSerializeHorizontalInvert() =>
                gyroMouseAction.ChangedProperties.Contains(GyroMouse.PropertyKeyStrings.HORIZONTAL_INVERT);

            public bool VerticalInvert
            {
                get => gyroMouseAction.mouseParams.orientation.vertical.invertSingle;
                set
                {
                    gyroMouseAction.mouseParams.orientation.vertical.invertSingle = value;
                    VerticalInvertChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler VerticalInvertChanged;
            public bool ShouldSerializeVerticalInvert() =>
                gyroMouseAction.ChangedProperties.Contains(GyroMouse.PropertyKeyStrings.VERTICAL_INVERT);

            public double HorizontalYawContribution
            {
                get => gyroMouseAction.mouseParams.orientation.horizontal.yawContribution;
                set
                {
                    gyroMouseAction.mouseParams.orientation.horizontal.yawContribution =
                        Math.Clamp(value, GyroLocalAxisMapping.CONTRIBUTION_MIN, GyroLocalAxisMapping.CONTRIBUTION_MAX);
                    HorizontalYawContributionChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler HorizontalYawContributionChanged;
            public bool ShouldSerializeHorizontalYawContribution() =>
                gyroMouseAction.ChangedProperties.Contains(GyroMouse.PropertyKeyStrings.HORIZONTAL_YAW_CONTRIBUTION);

            public double HorizontalRollContribution
            {
                get => gyroMouseAction.mouseParams.orientation.horizontal.rollContribution;
                set
                {
                    gyroMouseAction.mouseParams.orientation.horizontal.rollContribution =
                        Math.Clamp(value, GyroLocalAxisMapping.CONTRIBUTION_MIN, GyroLocalAxisMapping.CONTRIBUTION_MAX);
                    HorizontalRollContributionChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler HorizontalRollContributionChanged;
            public bool ShouldSerializeHorizontalRollContribution() =>
                gyroMouseAction.ChangedProperties.Contains(GyroMouse.PropertyKeyStrings.HORIZONTAL_ROLL_CONTRIBUTION);

            public double VerticalYawContribution
            {
                get => gyroMouseAction.mouseParams.orientation.vertical.yawContribution;
                set
                {
                    gyroMouseAction.mouseParams.orientation.vertical.yawContribution =
                        Math.Clamp(value, GyroLocalAxisMapping.CONTRIBUTION_MIN, GyroLocalAxisMapping.CONTRIBUTION_MAX);
                    VerticalYawContributionChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler VerticalYawContributionChanged;
            public bool ShouldSerializeVerticalYawContribution() =>
                gyroMouseAction.ChangedProperties.Contains(GyroMouse.PropertyKeyStrings.VERTICAL_YAW_CONTRIBUTION);

            public double VerticalRollContribution
            {
                get => gyroMouseAction.mouseParams.orientation.vertical.rollContribution;
                set
                {
                    gyroMouseAction.mouseParams.orientation.vertical.rollContribution =
                        Math.Clamp(value, GyroLocalAxisMapping.CONTRIBUTION_MIN, GyroLocalAxisMapping.CONTRIBUTION_MAX);
                    VerticalRollContributionChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler VerticalRollContributionChanged;
            public bool ShouldSerializeVerticalRollContribution() =>
                gyroMouseAction.ChangedProperties.Contains(GyroMouse.PropertyKeyStrings.VERTICAL_ROLL_CONTRIBUTION);

            public bool InvertGyroEnabled
            {
                get => gyroMouseAction.mouseParams.invert.enabled;
                set
                {
                    gyroMouseAction.mouseParams.invert.enabled = value;
                    InvertGyroEnabledChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler InvertGyroEnabledChanged;
            public bool ShouldSerializeInvertGyroEnabled() =>
                gyroMouseAction.ChangedProperties.Contains(GyroMouse.PropertyKeyStrings.INVERT_GYRO_ENABLED);

            [JsonConverter(typeof(StringEnumConverter))]
            public GyroInvertAxisChoice InvertGyroAxis
            {
                get => gyroMouseAction.mouseParams.invert.axisChoice;
                set
                {
                    gyroMouseAction.mouseParams.invert.axisChoice = value;
                    InvertGyroAxisChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler InvertGyroAxisChanged;
            public bool ShouldSerializeInvertGyroAxis() =>
                gyroMouseAction.ChangedProperties.Contains(GyroMouse.PropertyKeyStrings.INVERT_GYRO_AXIS);

            [JsonConverter(typeof(TriggerButtonsConverter))]
            public JoypadActionCodes[] InvertGyroTriggerButtons
            {
                get => gyroMouseAction.mouseParams.invert.triggerButtons;
                set
                {
                    gyroMouseAction.mouseParams.invert.triggerButtons = value;
                    InvertGyroTriggerButtonsChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler InvertGyroTriggerButtonsChanged;
            public bool ShouldSerializeInvertGyroTriggerButtons() =>
                gyroMouseAction.ChangedProperties.Contains(GyroMouse.PropertyKeyStrings.INVERT_GYRO_TRIGGER_BUTTONS);

            public bool InvertGyroTriggerActivates
            {
                get => gyroMouseAction.mouseParams.invert.triggerActivates;
                set
                {
                    gyroMouseAction.mouseParams.invert.triggerActivates = value;
                    InvertGyroTriggerActivatesChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler InvertGyroTriggerActivatesChanged;
            public bool ShouldSerializeInvertGyroTriggerActivates() =>
                gyroMouseAction.ChangedProperties.Contains(GyroMouse.PropertyKeyStrings.INVERT_GYRO_TRIGGER_ACTIVATES);

            [JsonConverter(typeof(StringEnumConverter))]
            public GyroActionsUtils.GyroTriggerEvalCond InvertGyroTriggerEvalCond
            {
                get => gyroMouseAction.mouseParams.invert.andCond ?
                    GyroActionsUtils.GyroTriggerEvalCond.And : GyroActionsUtils.GyroTriggerEvalCond.Or;
                set
                {
                    gyroMouseAction.mouseParams.invert.andCond =
                        value == GyroActionsUtils.GyroTriggerEvalCond.And;
                    InvertGyroTriggerEvalCondChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler InvertGyroTriggerEvalCondChanged;
            public bool ShouldSerializeInvertGyroTriggerEvalCond() =>
                gyroMouseAction.ChangedProperties.Contains(GyroMouse.PropertyKeyStrings.INVERT_GYRO_TRIGGER_EVAL_COND);

            public int InvertGyroActivationHoldMs
            {
                get => gyroMouseAction.mouseParams.invert.activationHoldMs;
                set
                {
                    gyroMouseAction.mouseParams.invert.activationHoldMs = Math.Clamp(value, 0, 60000);
                    InvertGyroActivationHoldMsChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler InvertGyroActivationHoldMsChanged;
            public bool ShouldSerializeInvertGyroActivationHoldMs() =>
                gyroMouseAction.ChangedProperties.Contains(GyroMouse.PropertyKeyStrings.INVERT_GYRO_ACTIVATION_HOLD_MS);

            public double MinThreshold
            {
                get => gyroMouseAction.mouseParams.minThreshold;
                set
                {
                    gyroMouseAction.mouseParams.minThreshold = value;
                    MinThresholdChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler MinThresholdChanged;

            public bool Toggle
            {
                get => gyroMouseAction.mouseParams.toggleAction;
                set
                {
                    gyroMouseAction.mouseParams.toggleAction = value;
                    ToggleChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler ToggleChanged;

            public bool JitterCompensation
            {
                get => gyroMouseAction.mouseParams.jitterCompensation;
                set
                {
                    gyroMouseAction.mouseParams.jitterCompensation = value;
                    JitterCompensationChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler JitterCompensationChanged;

            public bool ShouldSerializeJitterCompensation()
            {
                return gyroMouseAction.ChangedProperties.Contains(GyroMouse.PropertyKeyStrings.JITTER_COMPENSATION);
            }

            public bool MultiplierCompensation
            {
                get => gyroMouseAction.mouseParams.multiplierCompensation;
                set
                {
                    gyroMouseAction.mouseParams.multiplierCompensation = value;
                    MultiplierCompensationChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler MultiplierCompensationChanged;

            public bool ShouldSerializeMultiplierCompensation()
            {
                return gyroMouseAction.ChangedProperties.Contains(GyroMouse.PropertyKeyStrings.MULTIPLIER_COMPENSATION);
            }

            public double AccelerationMultiplier
            {
                get => gyroMouseAction.mouseParams.accelerationMultiplier;
                set
                {
                    gyroMouseAction.mouseParams.accelerationMultiplier = Math.Clamp(value, 0.01, 100.0);
                    AccelerationMultiplierChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler AccelerationMultiplierChanged;

            public bool ShouldSerializeAccelerationMultiplier()
            {
                return gyroMouseAction.mouseParams.multiplierCompensation ||
                    gyroMouseAction.ChangedProperties.Contains(GyroMouse.PropertyKeyStrings.ACCELERATION_MULTIPLIER);
            }

            public double VerticalAccelerationMultiplier
            {
                get => gyroMouseAction.mouseParams.verticalAccelerationMultiplier;
                set
                {
                    gyroMouseAction.mouseParams.verticalAccelerationMultiplier = Math.Clamp(value, 0.01, 100.0);
                    VerticalAccelerationMultiplierChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler VerticalAccelerationMultiplierChanged;

            public bool ShouldSerializeVerticalAccelerationMultiplier()
            {
                return gyroMouseAction.mouseParams.multiplierCompensation ||
                    gyroMouseAction.ChangedProperties.Contains(GyroMouse.PropertyKeyStrings.VERTICAL_ACCELERATION_MULTIPLIER);
            }

            public bool VerticalAccelerationScaleMode
            {
                get => gyroMouseAction.mouseParams.verticalAccelerationScaleMode;
                set
                {
                    gyroMouseAction.mouseParams.verticalAccelerationScaleMode = value;
                    VerticalAccelerationScaleModeChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler VerticalAccelerationScaleModeChanged;

            public bool ShouldSerializeVerticalAccelerationScaleMode()
            {
                return gyroMouseAction.mouseParams.multiplierCompensation ||
                    gyroMouseAction.ChangedProperties.Contains(GyroMouse.PropertyKeyStrings.VERTICAL_ACCELERATION_SCALE_MODE);
            }

            public bool SmoothingEnabled
            {
                get => gyroMouseAction.mouseParams.smoothing;
                set
                {
                    gyroMouseAction.mouseParams.smoothing = value;
                    SmoothingEnabledChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler SmoothingEnabledChanged;
            public bool ShouldSerializeSmoothingEnabled()
            {
                return gyroMouseAction.ChangedProperties.Contains(GyroMouse.PropertyKeyStrings.SMOOTHING_ENABLED);
            }

            public double SmoothingMinCutoff
            {
                get => gyroMouseAction.mouseParams.smoothingFilterSettings.minCutOff;
                set
                {
                    gyroMouseAction.mouseParams.smoothingFilterSettings.minCutOff = Math.Clamp(value, 0.0, 10.0);
                    gyroMouseAction.mouseParams.smoothingFilterSettings.UpdateSmoothingFilters();
                    SmoothingMinCutoffChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler SmoothingMinCutoffChanged;
            public bool ShouldSerializeSmoothingMinCutoff()
            {
                return gyroMouseAction.ChangedProperties.Contains(GyroMouse.PropertyKeyStrings.SMOOTHING_FILTER);
            }

            public double SmoothingBeta
            {
                get => gyroMouseAction.mouseParams.smoothingFilterSettings.beta;
                set
                {
                    gyroMouseAction.mouseParams.smoothingFilterSettings.beta = Math.Clamp(value, 0.0, 1.0);
                    gyroMouseAction.mouseParams.smoothingFilterSettings.UpdateSmoothingFilters();
                    SmoothingBetaChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler SmoothingBetaChanged;
            public bool ShouldSerializeSmoothingBeta()
            {
                return gyroMouseAction.ChangedProperties.Contains(GyroMouse.PropertyKeyStrings.SMOOTHING_FILTER);
            }

            public bool TriggerSensitivityModifierEnabled
            {
                get => gyroMouseAction.mouseParams.triggerSensitivityModifier.enabled;
                set => gyroMouseAction.mouseParams.triggerSensitivityModifier.enabled = value;
            }
            [JsonConverter(typeof(StringEnumConverter))]
            public TriggerSensitivityModifierTrigger TriggerSensitivityModifierTrigger
            {
                get => gyroMouseAction.mouseParams.triggerSensitivityModifier.trigger;
                set => gyroMouseAction.mouseParams.triggerSensitivityModifier.trigger = value;
            }
            [JsonConverter(typeof(StringEnumConverter))]
            public TriggerSensitivityModifierBehaviour TriggerSensitivityModifierBehaviour
            {
                get => gyroMouseAction.mouseParams.triggerSensitivityModifier.behaviour;
                set => gyroMouseAction.mouseParams.triggerSensitivityModifier.behaviour = value;
            }
            [JsonConverter(typeof(StringEnumConverter))]
            public TriggerSensitivityModifierConfigureUsing TriggerSensitivityModifierConfigureUsing
            {
                get => gyroMouseAction.mouseParams.triggerSensitivityModifier.configureUsing;
                set => gyroMouseAction.mouseParams.triggerSensitivityModifier.configureUsing = value;
            }
            [JsonConverter(typeof(StringEnumConverter))]
            public TriggerSensitivityModifierResponseCurve TriggerSensitivityModifierResponseCurve
            {
                get => gyroMouseAction.mouseParams.triggerSensitivityModifier.responseCurve;
                set => gyroMouseAction.mouseParams.triggerSensitivityModifier.responseCurve = value;
            }
            public double TriggerSensitivityModifierTargetSensitivity
            {
                get => gyroMouseAction.mouseParams.triggerSensitivityModifier.targetSensitivity;
                set => gyroMouseAction.mouseParams.triggerSensitivityModifier.targetSensitivity = Math.Clamp(value, 0.0, 100.0);
            }
            public double TriggerSensitivityModifierMultiplier
            {
                get => gyroMouseAction.mouseParams.triggerSensitivityModifier.multiplier;
                set => gyroMouseAction.mouseParams.triggerSensitivityModifier.multiplier = Math.Clamp(value, 0.0, 100.0);
            }
            public bool TriggerSensitivityModifierModifyVerticalSensitivity
            {
                get => gyroMouseAction.mouseParams.triggerSensitivityModifier.modifyVerticalSensitivity;
                set => gyroMouseAction.mouseParams.triggerSensitivityModifier.modifyVerticalSensitivity = value;
            }
            private bool ShouldSerializeTriggerSensitivityModifier() =>
                gyroMouseAction.mouseParams.triggerSensitivityModifier.enabled ||
                gyroMouseAction.ChangedProperties.Contains(GyroMouse.PropertyKeyStrings.TRIGGER_SENSITIVITY_MODIFIER);
            public bool ShouldSerializeTriggerSensitivityModifierEnabled() =>
                ShouldSerializeTriggerSensitivityModifier();
            public bool ShouldSerializeTriggerSensitivityModifierTrigger() =>
                ShouldSerializeTriggerSensitivityModifier();
            public bool ShouldSerializeTriggerSensitivityModifierBehaviour() =>
                ShouldSerializeTriggerSensitivityModifier();
            public bool ShouldSerializeTriggerSensitivityModifierConfigureUsing() =>
                ShouldSerializeTriggerSensitivityModifier();
            public bool ShouldSerializeTriggerSensitivityModifierResponseCurve() =>
                ShouldSerializeTriggerSensitivityModifier();
            public bool ShouldSerializeTriggerSensitivityModifierTargetSensitivity() =>
                ShouldSerializeTriggerSensitivityModifier();
            public bool ShouldSerializeTriggerSensitivityModifierMultiplier() =>
                ShouldSerializeTriggerSensitivityModifier();
            public bool ShouldSerializeTriggerSensitivityModifierModifyVerticalSensitivity() =>
                ShouldSerializeTriggerSensitivityModifier();

            public GyroMouseSettings(GyroMouse mouseAction)
            {
                gyroMouseAction = mouseAction;
            }
        }

        private GyroMouse gyroMouseAction = new GyroMouse();
        private GyroMouseSettings settings;
        public GyroMouseSettings Settings
        {
            get => settings;
            set => settings = value;
        }

        // Deserialize
        public GyroMouseSerializer(): base()
        {
            mapAction = gyroMouseAction;
            settings = new GyroMouseSettings(gyroMouseAction);

            NameChanged += GyroMouseSerializer_NameChanged;
            settings.DeadZoneChanged += Settings_DeadZoneChanged;
            settings.VerticalDeadZoneChanged += Settings_VerticalDeadZoneChanged;
            settings.GyroAngleSnapDegreesChanged += Settings_GyroAngleSnapDegreesChanged;
            settings.GyroSmoothAngleSnapChanged += Settings_GyroSmoothAngleSnapChanged;
            settings.RealWorldCalibrationChanged += Settings_RealWorldCalibrationChanged;
            settings.InGameSensChanged += Settings_InGameSensChanged;
            settings.AccelCurveChanged += Settings_AccelCurveChanged;
            settings.MinAccelXSensChanged += Settings_MinAccelXSensChanged;
            settings.MaxAccelXSensChanged += Settings_MaxAccelXSensChanged;
            settings.MinAccelYSensChanged += Settings_MinAccelYSensChanged;
            settings.MaxAccelYSensChanged += Settings_MaxAccelYSensChanged;
            settings.MinGyroThresholdChanged += Settings_MinGyroThresholdChanged;
            settings.MaxGyroThresholdChanged += Settings_MaxGyroThresholdChanged;
            settings.PowerVRefChanged += Settings_PowerVRefChanged;
            settings.PowerExponentChanged += Settings_PowerExponentChanged;
            settings.NaturalVHalfChanged += Settings_NaturalVHalfChanged;
            settings.SensitivityChanged += Settings_SensitivityChanged;
            settings.VerticalScaleChanged += Settings_VerticalScaleChanged;
            settings.InvertXChanged += Settings_InvertXChanged;
            settings.InvertYChanged += Settings_InvertYChanged;
            settings.TriggersButtonChanged += Settings_TriggerButtonsChanged;
            settings.TriggerActivatesChanged += Settings_TriggerActivatesChanged;
            settings.EvalCondChanged += Settings_EvalCondChanged;
            settings.UseForXAxisChanged += Settings_UseForXAxisChanged;
            settings.GyroSpaceChanged += Settings_GyroSpaceChanged;
            settings.HorizontalControlChanged += Settings_HorizontalControlChanged;
            settings.VerticalControlChanged += Settings_VerticalControlChanged;
            settings.HorizontalInvertChanged += Settings_HorizontalInvertChanged;
            settings.VerticalInvertChanged += Settings_VerticalInvertChanged;
            settings.HorizontalYawContributionChanged += Settings_HorizontalYawContributionChanged;
            settings.HorizontalRollContributionChanged += Settings_HorizontalRollContributionChanged;
            settings.VerticalYawContributionChanged += Settings_VerticalYawContributionChanged;
            settings.VerticalRollContributionChanged += Settings_VerticalRollContributionChanged;
            settings.InvertGyroEnabledChanged += Settings_InvertGyroEnabledChanged;
            settings.InvertGyroAxisChanged += Settings_InvertGyroAxisChanged;
            settings.InvertGyroTriggerButtonsChanged += Settings_InvertGyroTriggerButtonsChanged;
            settings.InvertGyroTriggerActivatesChanged += Settings_InvertGyroTriggerActivatesChanged;
            settings.InvertGyroTriggerEvalCondChanged += Settings_InvertGyroTriggerEvalCondChanged;
            settings.InvertGyroActivationHoldMsChanged += Settings_InvertGyroActivationHoldMsChanged;
            settings.MinThresholdChanged += Settings_MinThresholdChanged;
            settings.ToggleChanged += Settings_ToggleChanged;
            settings.JitterCompensationChanged += Settings_JitterCompensationChanged;
            settings.MultiplierCompensationChanged += Settings_MultiplierCompensationChanged;
            settings.AccelerationMultiplierChanged += Settings_AccelerationMultiplierChanged;
            settings.VerticalAccelerationMultiplierChanged += Settings_VerticalAccelerationMultiplierChanged;
            settings.VerticalAccelerationScaleModeChanged += Settings_VerticalAccelerationScaleModeChanged;
            settings.SmoothingEnabledChanged += Settings_SmoothingEnabledChanged;
            settings.SmoothingMinCutoffChanged += Settings_SmoothingMinCutoffChanged;
            settings.SmoothingBetaChanged += Settings_SmoothingMinBetaChanged;
        }

        private void Settings_GyroSpaceChanged(object sender, EventArgs e)
        {
            gyroMouseAction.ChangedProperties.Add(GyroMouse.PropertyKeyStrings.GYRO_SPACE);
        }

        private void Settings_HorizontalControlChanged(object sender, EventArgs e)
        {
            gyroMouseAction.ChangedProperties.Add(GyroMouse.PropertyKeyStrings.HORIZONTAL_CONTROL);
        }

        private void Settings_VerticalControlChanged(object sender, EventArgs e)
        {
            gyroMouseAction.ChangedProperties.Add(GyroMouse.PropertyKeyStrings.VERTICAL_CONTROL);
        }

        private void Settings_HorizontalInvertChanged(object sender, EventArgs e)
        {
            gyroMouseAction.ChangedProperties.Add(GyroMouse.PropertyKeyStrings.HORIZONTAL_INVERT);
        }

        private void Settings_VerticalInvertChanged(object sender, EventArgs e)
        {
            gyroMouseAction.ChangedProperties.Add(GyroMouse.PropertyKeyStrings.VERTICAL_INVERT);
        }

        private void Settings_HorizontalYawContributionChanged(object sender, EventArgs e)
        {
            gyroMouseAction.ChangedProperties.Add(GyroMouse.PropertyKeyStrings.HORIZONTAL_YAW_CONTRIBUTION);
        }

        private void Settings_HorizontalRollContributionChanged(object sender, EventArgs e)
        {
            gyroMouseAction.ChangedProperties.Add(GyroMouse.PropertyKeyStrings.HORIZONTAL_ROLL_CONTRIBUTION);
        }

        private void Settings_VerticalYawContributionChanged(object sender, EventArgs e)
        {
            gyroMouseAction.ChangedProperties.Add(GyroMouse.PropertyKeyStrings.VERTICAL_YAW_CONTRIBUTION);
        }

        private void Settings_VerticalRollContributionChanged(object sender, EventArgs e)
        {
            gyroMouseAction.ChangedProperties.Add(GyroMouse.PropertyKeyStrings.VERTICAL_ROLL_CONTRIBUTION);
        }

        private void Settings_InvertGyroEnabledChanged(object sender, EventArgs e)
        {
            gyroMouseAction.ChangedProperties.Add(GyroMouse.PropertyKeyStrings.INVERT_GYRO_ENABLED);
        }

        private void Settings_InvertGyroAxisChanged(object sender, EventArgs e)
        {
            gyroMouseAction.ChangedProperties.Add(GyroMouse.PropertyKeyStrings.INVERT_GYRO_AXIS);
        }

        private void Settings_InvertGyroTriggerButtonsChanged(object sender, EventArgs e)
        {
            gyroMouseAction.ChangedProperties.Add(GyroMouse.PropertyKeyStrings.INVERT_GYRO_TRIGGER_BUTTONS);
        }

        private void Settings_InvertGyroTriggerActivatesChanged(object sender, EventArgs e)
        {
            gyroMouseAction.ChangedProperties.Add(GyroMouse.PropertyKeyStrings.INVERT_GYRO_TRIGGER_ACTIVATES);
        }

        private void Settings_InvertGyroTriggerEvalCondChanged(object sender, EventArgs e)
        {
            gyroMouseAction.ChangedProperties.Add(GyroMouse.PropertyKeyStrings.INVERT_GYRO_TRIGGER_EVAL_COND);
        }

        private void Settings_InvertGyroActivationHoldMsChanged(object sender, EventArgs e)
        {
            gyroMouseAction.ChangedProperties.Add(GyroMouse.PropertyKeyStrings.INVERT_GYRO_ACTIVATION_HOLD_MS);
        }

        // Post-deserialize: migrate legacy InvertX/InvertY/UseForXAxis into the new
        // orientation model when a loaded profile predates this feature. Gated on
        // ChangedProperties so an explicit new-format value is never overwritten.
        public override void PopulateMap()
        {
            MigrateLegacyOrientation();
        }

        private void MigrateLegacyOrientation()
        {
            if (!gyroMouseAction.ChangedProperties.Contains(GyroMouse.PropertyKeyStrings.HORIZONTAL_CONTROL))
            {
                gyroMouseAction.mouseParams.orientation.horizontal.source =
                    gyroMouseAction.mouseParams.useForXAxis == GyroMouseXAxisChoice.Roll
                        ? GyroLocalAxisSource.Roll : GyroLocalAxisSource.Yaw;
                gyroMouseAction.mouseParams.orientation.horizontal.invertSingle = gyroMouseAction.mouseParams.invertX;
                gyroMouseAction.ChangedProperties.Add(GyroMouse.PropertyKeyStrings.HORIZONTAL_CONTROL);
                gyroMouseAction.ChangedProperties.Add(GyroMouse.PropertyKeyStrings.HORIZONTAL_INVERT);
            }

            if (!gyroMouseAction.ChangedProperties.Contains(GyroMouse.PropertyKeyStrings.VERTICAL_CONTROL))
            {
                // Vertical was always hardcoded to Pitch prior to this feature.
                gyroMouseAction.mouseParams.orientation.vertical.source = GyroLocalAxisSource.Pitch;
                gyroMouseAction.mouseParams.orientation.vertical.invertSingle = gyroMouseAction.mouseParams.invertY;
                gyroMouseAction.ChangedProperties.Add(GyroMouse.PropertyKeyStrings.VERTICAL_CONTROL);
                gyroMouseAction.ChangedProperties.Add(GyroMouse.PropertyKeyStrings.VERTICAL_INVERT);
            }
        }

        private void Settings_NaturalVHalfChanged(object sender, EventArgs e)
        {
            gyroMouseAction.ChangedProperties.Add(GyroMouse.PropertyKeyStrings.NATURAL_CURVE_VHALF);
        }

        private void Settings_GyroAngleSnapDegreesChanged(object sender, EventArgs e)
        {
            gyroMouseAction.ChangedProperties.Add(GyroMouse.PropertyKeyStrings.ANGLE_SNAP_DEGREES);
        }

        private void Settings_GyroSmoothAngleSnapChanged(object sender, EventArgs e)
        {
            gyroMouseAction.ChangedProperties.Add(GyroMouse.PropertyKeyStrings.SMOOTH_ANGLE_SNAP);
        }

        private void Settings_MaxAccelYSensChanged(object sender, EventArgs e)
        {
            gyroMouseAction.ChangedProperties.Add(GyroMouse.PropertyKeyStrings.MAX_ACCEL_Y_SENS);
        }

        private void Settings_MinAccelYSensChanged(object sender, EventArgs e)
        {
            gyroMouseAction.ChangedProperties.Add(GyroMouse.PropertyKeyStrings.MIN_ACCEL_Y_SENS);
        }

        private void Settings_PowerExponentChanged(object sender, EventArgs e)
        {
            gyroMouseAction.ChangedProperties.Add(GyroMouse.PropertyKeyStrings.POWER_CURVE_EXPONENT);
        }

        private void Settings_PowerVRefChanged(object sender, EventArgs e)
        {
            gyroMouseAction.ChangedProperties.Add(GyroMouse.PropertyKeyStrings.POWER_CURVE_VREF);
        }

        private void Settings_MaxGyroThresholdChanged(object sender, EventArgs e)
        {
            gyroMouseAction.ChangedProperties.Add(GyroMouse.PropertyKeyStrings.MAX_GYRO_THRESHOLD);
        }

        private void Settings_MinGyroThresholdChanged(object sender, EventArgs e)
        {
            gyroMouseAction.ChangedProperties.Add(GyroMouse.PropertyKeyStrings.MIN_GYRO_THRESHOLD);
        }

        private void Settings_MaxAccelXSensChanged(object sender, EventArgs e)
        {
            gyroMouseAction.ChangedProperties.Add(GyroMouse.PropertyKeyStrings.MAX_ACCEL_X_SENS);
        }

        private void Settings_MinAccelXSensChanged(object sender, EventArgs e)
        {
            gyroMouseAction.ChangedProperties.Add(GyroMouse.PropertyKeyStrings.MIN_ACCEL_X_SENS);
        }

        private void Settings_AccelCurveChanged(object sender, EventArgs e)
        {
            gyroMouseAction.ChangedProperties.Add(GyroMouse.PropertyKeyStrings.ACCEL_CURVE);
        }

        private void Settings_InGameSensChanged(object sender, EventArgs e)
        {
            gyroMouseAction.ChangedProperties.Add(GyroMouse.PropertyKeyStrings.IN_GAME_SENS);
        }

        private void Settings_RealWorldCalibrationChanged(object sender, EventArgs e)
        {
            gyroMouseAction.ChangedProperties.Add(GyroMouse.PropertyKeyStrings.REAL_WORLD_CALIBRATION);
        }

        private void Settings_JitterCompensationChanged(object sender, EventArgs e)
        {
            gyroMouseAction.ChangedProperties.Add(GyroMouse.PropertyKeyStrings.JITTER_COMPENSATION);
        }

        private void Settings_MultiplierCompensationChanged(object sender, EventArgs e)
        {
            gyroMouseAction.ChangedProperties.Add(GyroMouse.PropertyKeyStrings.MULTIPLIER_COMPENSATION);
        }

        private void Settings_AccelerationMultiplierChanged(object sender, EventArgs e)
        {
            gyroMouseAction.ChangedProperties.Add(GyroMouse.PropertyKeyStrings.ACCELERATION_MULTIPLIER);
        }

        private void Settings_VerticalAccelerationMultiplierChanged(object sender, EventArgs e)
        {
            gyroMouseAction.ChangedProperties.Add(GyroMouse.PropertyKeyStrings.VERTICAL_ACCELERATION_MULTIPLIER);
        }

        private void Settings_VerticalAccelerationScaleModeChanged(object sender, EventArgs e)
        {
            gyroMouseAction.ChangedProperties.Add(GyroMouse.PropertyKeyStrings.VERTICAL_ACCELERATION_SCALE_MODE);
        }

        private void Settings_EvalCondChanged(object sender, EventArgs e)
        {
            gyroMouseAction.ChangedProperties.Add(GyroMouse.PropertyKeyStrings.TRIGGER_EVAL_COND);
        }

        private void Settings_SmoothingMinBetaChanged(object sender, EventArgs e)
        {
            gyroMouseAction.ChangedProperties.Add(GyroMouse.PropertyKeyStrings.SMOOTHING_FILTER);
        }

        private void Settings_SmoothingMinCutoffChanged(object sender, EventArgs e)
        {
            gyroMouseAction.ChangedProperties.Add(GyroMouse.PropertyKeyStrings.SMOOTHING_FILTER);
        }

        private void Settings_SmoothingEnabledChanged(object sender, EventArgs e)
        {
            gyroMouseAction.ChangedProperties.Add(GyroMouse.PropertyKeyStrings.SMOOTHING_ENABLED);
        }

        private void Settings_ToggleChanged(object sender, EventArgs e)
        {
            gyroMouseAction.ChangedProperties.Add(GyroMouse.PropertyKeyStrings.TOGGLE_ACTION);
        }

        private void Settings_MinThresholdChanged(object sender, EventArgs e)
        {
            gyroMouseAction.ChangedProperties.Add(GyroMouse.PropertyKeyStrings.MIN_THRESHOLD);
        }

        private void Settings_UseForXAxisChanged(object sender, EventArgs e)
        {
            gyroMouseAction.ChangedProperties.Add(GyroMouse.PropertyKeyStrings.X_AXIS);
        }

        private void Settings_InvertXChanged(object sender, EventArgs e)
        {
            gyroMouseAction.ChangedProperties.Add(GyroMouse.PropertyKeyStrings.INVERT_X);
        }

        private void Settings_InvertYChanged(object sender, EventArgs e)
        {
            gyroMouseAction.ChangedProperties.Add(GyroMouse.PropertyKeyStrings.INVERT_Y);
        }

        private void Settings_VerticalScaleChanged(object sender, EventArgs e)
        {
            gyroMouseAction.ChangedProperties.Add(GyroMouse.PropertyKeyStrings.VERTICAL_SCALE);
        }

        private void Settings_SensitivityChanged(object sender, EventArgs e)
        {
            gyroMouseAction.ChangedProperties.Add(GyroMouse.PropertyKeyStrings.SENSITIVITY);
        }

        private void Settings_TriggerActivatesChanged(object sender, EventArgs e)
        {
            gyroMouseAction.ChangedProperties.Add(GyroMouse.PropertyKeyStrings.TRIGGER_ACTIVATE);
        }

        private void Settings_TriggerButtonsChanged(object sender, EventArgs e)
        {
            gyroMouseAction.ChangedProperties.Add(GyroMouse.PropertyKeyStrings.TRIGGER_BUTTONS);
        }

        // Serialize
        public GyroMouseSerializer(ActionLayer tempLayer, MapAction action) :
            base(tempLayer, action)
        {
            if (action is GyroMouse temp)
            {
                gyroMouseAction = temp;
                mapAction = gyroMouseAction;
                settings = new GyroMouseSettings(gyroMouseAction);
            }
        }

        private void Settings_DeadZoneChanged(object sender, EventArgs e)
        {
            gyroMouseAction.ChangedProperties.Add(GyroMouse.PropertyKeyStrings.DEAD_ZONE);
        }

        private void Settings_VerticalDeadZoneChanged(object sender, EventArgs e)
        {
            gyroMouseAction.ChangedProperties.Add(GyroMouse.PropertyKeyStrings.VERTICAL_DEAD_ZONE);
        }

        private void GyroMouseSerializer_NameChanged(object sender, EventArgs e)
        {
            gyroMouseAction.ChangedProperties.Add(GyroMouse.PropertyKeyStrings.NAME);
        }
    }

    public class GyroDirectionalSwipeSerializer : MapActionSerializer
    {
        public class SwipeDirBinding
        {
            public enum SwipeDir
            {
                Up,
                Down,
                Left,
                Right,
            }

            private string actionDirName;
            [JsonProperty("Name", Required = Required.Default)]
            public string ActionDirName
            {
                get => actionDirName;
                set => actionDirName = value;
            }

            private List<ActionFuncSerializer> actionFuncSerializers =
                new List<ActionFuncSerializer>();
            [JsonProperty("Functions", Required = Required.Always)]
            public List<ActionFuncSerializer> ActionFuncSerializers
            {
                get => actionFuncSerializers;
                set
                {
                    actionFuncSerializers = value;
                    ActionFuncSerializersChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler ActionFuncSerializersChanged;
        }

        public class GyroDirSwipeSettings
        {
            private GyroDirectionalSwipe gyroDirSwipeAction;

            public double DeadZoneX
            {
                get => gyroDirSwipeAction.swipeParams.deadzoneX;
                set
                {
                    gyroDirSwipeAction.swipeParams.deadzoneX = value;
                    DeadZoneXChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler DeadZoneXChanged;

            public double DeadZoneY
            {
                get => gyroDirSwipeAction.swipeParams.deadzoneY;
                set
                {
                    gyroDirSwipeAction.swipeParams.deadzoneY = value;
                    DeadZoneYChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler DeadZoneYChanged;

            public int DelayTime
            {
                get => gyroDirSwipeAction.swipeParams.delayTime;
                set
                {
                    gyroDirSwipeAction.swipeParams.delayTime = value;
                    DelayTimeChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler DelayTimeChanged;

            [JsonConverter(typeof(TriggerButtonsConverter))]
            public JoypadActionCodes[] TriggerButtons
            {
                get => gyroDirSwipeAction.swipeParams.gyroTriggerButtons;
                set
                {
                    gyroDirSwipeAction.swipeParams.gyroTriggerButtons = value;
                    TriggerButtonsChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler TriggerButtonsChanged;

            public bool TriggerActivates
            {
                get => gyroDirSwipeAction.swipeParams.triggerActivates;
                set
                {
                    gyroDirSwipeAction.swipeParams.triggerActivates = value;
                    TriggerActivatesChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler TriggerActivatesChanged;

            public int ActivationHoldMs
            {
                get => gyroDirSwipeAction.swipeParams.activationHoldMs;
                set => gyroDirSwipeAction.swipeParams.activationHoldMs = Math.Clamp(value, 0, 60000);
            }
            public bool ShouldSerializeActivationHoldMs() =>
                gyroDirSwipeAction.ChangedProperties.Contains(GyroDirectionalSwipe.PropertyKeyStrings.ACTIVATION_HOLD_MS);

            [JsonConverter(typeof(StringEnumConverter))]
            public GyroActionsUtils.GyroTriggerEvalCond EvalCond
            {
                get => gyroDirSwipeAction.swipeParams.andCond ?
                    GyroActionsUtils.GyroTriggerEvalCond.And : GyroActionsUtils.GyroTriggerEvalCond.Or;
                set
                {
                    gyroDirSwipeAction.swipeParams.andCond =
                        value == GyroActionsUtils.GyroTriggerEvalCond.And ? true : false;

                    EvalCondChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler EvalCondChanged;

            public GyroDirSwipeSettings(GyroDirectionalSwipe swipeAction)
            {
                gyroDirSwipeAction = swipeAction;
            }
        }

        private Dictionary<SwipeDirBinding.SwipeDir, SwipeDirBinding> dictDirBindings =
            new Dictionary<SwipeDirBinding.SwipeDir, SwipeDirBinding>();

        //private Dictionary<GyroDirectionalSwipe.SwipeAxisYDir, SwipeDirBinding> dictDirYBindings =
        //    new Dictionary<GyroDirectionalSwipe.SwipeAxisYDir, SwipeDirBinding>();

        [JsonProperty("Bindings", Required = Required.Always)]
        public Dictionary<SwipeDirBinding.SwipeDir, SwipeDirBinding> DictDirBindings
        {
            get => dictDirBindings;
            set => dictDirBindings = value;
        }

        //[JsonProperty("YBindings", Required = Required.Always)]
        //public Dictionary<GyroDirectionalSwipe.SwipeAxisYDir, SwipeDirBinding> DictDirYBindings
        //{
        //    get => dictDirYBindings;
        //    set => dictDirYBindings = value;
        //}

        private GyroDirectionalSwipe gyroDirSwipeAction = new GyroDirectionalSwipe();
        private GyroDirSwipeSettings settings;
        public GyroDirSwipeSettings Settings
        {
            get => settings;
            set => settings = value;
        }

        // Deserialize
        public GyroDirectionalSwipeSerializer(): base()
        {
            mapAction = gyroDirSwipeAction;
            settings = new GyroDirSwipeSettings(gyroDirSwipeAction);

            NameChanged += GyroDirectionalSwipeSerializer_NameChanged;
            settings.DeadZoneXChanged += Settings_DeadZoneXChanged;
            settings.DeadZoneYChanged += Settings_DeadZoneYChanged;
            settings.DelayTimeChanged += Settings_DelayTimeChanged;
            settings.TriggerActivatesChanged += Settings_TriggerActivatesChanged;
            settings.TriggerButtonsChanged += Settings_TriggerButtonsChanged;
            settings.EvalCondChanged += Settings_EvalCondChanged;
        }

        private void Settings_EvalCondChanged(object sender, EventArgs e)
        {
            gyroDirSwipeAction.ChangedProperties.Add(GyroDirectionalSwipe.PropertyKeyStrings.TRIGGER_EVAL_COND);
        }

        // Serialize
        public GyroDirectionalSwipeSerializer(ActionLayer tempLayer, MapAction action) :
            base(tempLayer, action)
        {
            if (action is GyroDirectionalSwipe temp)
            {
                gyroDirSwipeAction = temp;
                MapAction = gyroDirSwipeAction;
                settings = new GyroDirSwipeSettings(gyroDirSwipeAction);
                PopulateFuncs();
            }
        }

        // Post-deserialize
        public override void PopulateMap()
        {
            foreach (ButtonAction dirButton in gyroDirSwipeAction.UsedEventsButtonsX)
            {
                dirButton?.ActionFuncs.Clear();
            }

            foreach (ButtonAction dirButton in gyroDirSwipeAction.UsedEventsButtonsY)
            {
                dirButton?.ActionFuncs.Clear();
            }

            foreach (KeyValuePair<SwipeDirBinding.SwipeDir, SwipeDirBinding> pair in dictDirBindings)
            {
                SwipeDirBinding.SwipeDir dir = pair.Key;
                List<ActionFuncSerializer> tempSerializers = pair.Value.ActionFuncSerializers;
                ButtonAction dirButton = new ButtonAction();
                dirButton.Name = pair.Value.ActionDirName;
                foreach (ActionFuncSerializer serializer in tempSerializers)
                {
                    serializer.PopulateFunc();
                    dirButton.ActionFuncs.Add(serializer.ActionFunc);
                }

                switch(dir)
                {
                    case SwipeDirBinding.SwipeDir.Left:
                        gyroDirSwipeAction.UsedEventsButtonsX[(int)GyroDirectionalSwipe.SwipeAxisXDir.Left] = dirButton;
                        break;
                    case SwipeDirBinding.SwipeDir.Right:
                        gyroDirSwipeAction.UsedEventsButtonsX[(int)GyroDirectionalSwipe.SwipeAxisXDir.Right] = dirButton;
                        break;
                    case SwipeDirBinding.SwipeDir.Up:
                        gyroDirSwipeAction.UsedEventsButtonsY[(int)GyroDirectionalSwipe.SwipeAxisYDir.Up] = dirButton;
                        break;
                    case SwipeDirBinding.SwipeDir.Down:
                        gyroDirSwipeAction.UsedEventsButtonsY[(int)GyroDirectionalSwipe.SwipeAxisYDir.Down] = dirButton;
                        break;
                    default:
                        break;
                }

                FlagBtnChangedDirection(dir);
            }

            //foreach (KeyValuePair<GyroDirectionalSwipe.SwipeAxisYDir, SwipeDirBinding> pair in dictDirYBindings)
            //{
            //    GyroDirectionalSwipe.SwipeAxisYDir dir = pair.Key;
            //    List<ActionFuncSerializer> tempSerializers = pair.Value.ActionFuncSerializers;
            //    ButtonAction dirButton = new ButtonAction();
            //    dirButton.Name = pair.Value.ActionDirName;
            //    foreach (ActionFuncSerializer serializer in tempSerializers)
            //    {
            //        serializer.PopulateFunc();
            //        dirButton.ActionFuncs.Add(serializer.ActionFunc);
            //    }

            //    gyroDirSwipeAction.UsedEventsButtonsY[(int)dir] = dirButton;
            //    FlagBtnChangedYDirection(dir);
            //}
        }

        private void FlagBtnChangedDirection(SwipeDirBinding.SwipeDir dir)
        {
            switch(dir)
            {
                case SwipeDirBinding.SwipeDir.Left:
                    gyroDirSwipeAction.ChangedProperties.Add(GyroDirectionalSwipe.PropertyKeyStrings.PAD_DIR_LEFT);
                    break;
                case SwipeDirBinding.SwipeDir.Right:
                    gyroDirSwipeAction.ChangedProperties.Add(GyroDirectionalSwipe.PropertyKeyStrings.PAD_DIR_RIGHT);
                    break;
                case SwipeDirBinding.SwipeDir.Up:
                    gyroDirSwipeAction.ChangedProperties.Add(GyroDirectionalSwipe.PropertyKeyStrings.PAD_DIR_UP);
                    break;
                case SwipeDirBinding.SwipeDir.Down:
                    gyroDirSwipeAction.ChangedProperties.Add(GyroDirectionalSwipe.PropertyKeyStrings.PAD_DIR_DOWN);
                    break;
                default:
                    break;
            }
        }

        //private void FlagBtnChangedYDirection(GyroDirectionalSwipe.SwipeAxisYDir dir)
        //{
        //    switch(dir)
        //    {
        //        case GyroDirectionalSwipe.SwipeAxisYDir.Up:
        //            //gyroDirSwipeAction.ChangedProperties.Add(GyroDirectionalSwipe.PropertyKeyStrings.)
        //            break;
        //        case GyroDirectionalSwipe.SwipeAxisYDir.Down:
        //            //gyroDirSwipeAction.ChangedProperties.Add(GyroDirectionalSwipe.PropertyKeyStrings.)
        //            break;
        //        default:
        //            break;
        //    }
        //}

        // Pre-serialize
        private void PopulateFuncs()
        {
            List<ActionFuncSerializer> tempFuncs = new List<ActionFuncSerializer>();
            GyroDirectionalSwipe.SwipeAxisXDir[] tempDirsX = new GyroDirectionalSwipe.SwipeAxisXDir[]
            {
                GyroDirectionalSwipe.SwipeAxisXDir.Left,
                GyroDirectionalSwipe.SwipeAxisXDir.Right,
            };

            GyroDirectionalSwipe.SwipeAxisYDir[] tempDirsY = new GyroDirectionalSwipe.SwipeAxisYDir[]
            {
                GyroDirectionalSwipe.SwipeAxisYDir.Up,
                GyroDirectionalSwipe.SwipeAxisYDir.Down,
            };

            for (int i = 0; i < tempDirsX.Length; i++)
            {
                GyroDirectionalSwipe.SwipeAxisXDir tempDir = tempDirsX[i];
                ButtonAction dirButton = gyroDirSwipeAction.UsedEventsButtonsX[(int)tempDir];

                tempFuncs.Clear();
                foreach (ActionFunc tempFunc in dirButton.ActionFuncs)
                {
                    ActionFuncSerializer tempSerializer =
                        ActionFuncSerializerFactory.CreateSerializer(tempFunc);
                    if (tempSerializer != null)
                    {
                        tempFuncs.Add(tempSerializer);
                    }
                }

                SwipeDirBinding.SwipeDir swipeDir = SwipeDirBinding.SwipeDir.Left;
                if (tempDir == GyroDirectionalSwipe.SwipeAxisXDir.Left)
                {
                    swipeDir = SwipeDirBinding.SwipeDir.Left;
                }
                else if (tempDir == GyroDirectionalSwipe.SwipeAxisXDir.Right)
                {
                    swipeDir = SwipeDirBinding.SwipeDir.Right;
                }

                dictDirBindings.Add(swipeDir, new SwipeDirBinding()
                {
                    ActionDirName = dirButton.Name,
                    ActionFuncSerializers = new List<ActionFuncSerializer>(tempFuncs),
                });
            }

            for (int i = 0; i < tempDirsY.Length; i++)
            {
                GyroDirectionalSwipe.SwipeAxisYDir tempDir = tempDirsY[i];
                ButtonAction dirButton = gyroDirSwipeAction.UsedEventsButtonsY[(int)tempDir];

                tempFuncs.Clear();
                foreach (ActionFunc tempFunc in dirButton.ActionFuncs)
                {
                    ActionFuncSerializer tempSerializer =
                        ActionFuncSerializerFactory.CreateSerializer(tempFunc);
                    if (tempSerializer != null)
                    {
                        tempFuncs.Add(tempSerializer);
                    }
                }

                SwipeDirBinding.SwipeDir swipeDir = SwipeDirBinding.SwipeDir.Left;
                if (tempDir == GyroDirectionalSwipe.SwipeAxisYDir.Up)
                {
                    swipeDir = SwipeDirBinding.SwipeDir.Up;
                }
                else if (tempDir == GyroDirectionalSwipe.SwipeAxisYDir.Down)
                {
                    swipeDir = SwipeDirBinding.SwipeDir.Down;
                }

                dictDirBindings.Add(swipeDir, new SwipeDirBinding()
                {
                    ActionDirName = dirButton.Name,
                    ActionFuncSerializers = new List<ActionFuncSerializer>(tempFuncs),
                });
            }
        }

        private void Settings_TriggerButtonsChanged(object sender, EventArgs e)
        {
            gyroDirSwipeAction.ChangedProperties.Add(GyroDirectionalSwipe.PropertyKeyStrings.TRIGGER_BUTTONS);
        }

        private void Settings_TriggerActivatesChanged(object sender, EventArgs e)
        {
            gyroDirSwipeAction.ChangedProperties.Add(GyroDirectionalSwipe.PropertyKeyStrings.TRIGGER_ACTIVATE);
        }

        private void Settings_DelayTimeChanged(object sender, EventArgs e)
        {
            gyroDirSwipeAction.ChangedProperties.Add(GyroDirectionalSwipe.PropertyKeyStrings.DELAY_TIME);
        }

        private void Settings_DeadZoneYChanged(object sender, EventArgs e)
        {
            gyroDirSwipeAction.ChangedProperties.Add(GyroDirectionalSwipe.PropertyKeyStrings.DEAD_ZONE_Y);
        }

        private void Settings_DeadZoneXChanged(object sender, EventArgs e)
        {
            gyroDirSwipeAction.ChangedProperties.Add(GyroDirectionalSwipe.PropertyKeyStrings.DEAD_ZONE_X);
        }

        private void GyroDirectionalSwipeSerializer_NameChanged(object sender, EventArgs e)
        {
            gyroDirSwipeAction.ChangedProperties.Add(GyroDirectionalSwipe.PropertyKeyStrings.NAME);
        }
    }

    public class GyroMouseJoystickSerializer : MapActionSerializer
    {
        public class GyroMouseJoystickSettings
        {
            private GyroMouseJoystick gyroMouseStickAction;

            public double DeadZone
            {
                get => gyroMouseStickAction.mStickParams.deadZone;
                set
                {
                    gyroMouseStickAction.mStickParams.deadZone = value;
                    DeadZoneChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler DeadZoneChanged;

            public double MaxZone
            {
                get => gyroMouseStickAction.mStickParams.maxZone;
                set
                {
                    gyroMouseStickAction.mStickParams.maxZone = value;
                    MaxZoneChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler MaxZoneChanged;

            [JsonConverter(typeof(TriggerButtonsConverter))]
            public JoypadActionCodes[] TriggerButtons
            {
                get => gyroMouseStickAction.mStickParams.gyroTriggerButtons;
                set
                {
                    gyroMouseStickAction.mStickParams.gyroTriggerButtons = value;
                    TriggerButtonsChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler TriggerButtonsChanged;

            public bool TriggerActivates
            {
                get => gyroMouseStickAction.mStickParams.triggerActivates;
                set
                {
                    gyroMouseStickAction.mStickParams.triggerActivates = value;
                    TriggerActivatesChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler TriggerActivatesChanged;

            public int ActivationHoldMs
            {
                get => gyroMouseStickAction.mStickParams.activationHoldMs;
                set => gyroMouseStickAction.mStickParams.activationHoldMs = Math.Clamp(value, 0, 60000);
            }
            public bool ShouldSerializeActivationHoldMs() =>
                gyroMouseStickAction.ChangedProperties.Contains(GyroMouseJoystick.PropertyKeyStrings.ACTIVATION_HOLD_MS);

            [JsonConverter(typeof(StringEnumConverter))]
            public GyroActionsUtils.GyroTriggerEvalCond EvalCond
            {
                get => gyroMouseStickAction.mStickParams.andCond ?
                    GyroActionsUtils.GyroTriggerEvalCond.And : GyroActionsUtils.GyroTriggerEvalCond.Or;
                set
                {
                    gyroMouseStickAction.mStickParams.andCond =
                        value == GyroActionsUtils.GyroTriggerEvalCond.And ? true : false;

                    EvalCondChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler EvalCondChanged;

            public GyroMouseXAxisChoice UseForXAxis
            {
                get => gyroMouseStickAction.mStickParams.useForXAxis;
                set
                {
                    gyroMouseStickAction.mStickParams.useForXAxis = value;
                    UseForXAxisChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler UseForXAxisChanged;

            public double AntiDeadZoneX
            {
                get => gyroMouseStickAction.mStickParams.antiDeadzoneX;
                set
                {
                    gyroMouseStickAction.mStickParams.antiDeadzoneX = value;
                    AntiDeadZoneXChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler AntiDeadZoneXChanged;

            public double AntiDeadZoneY
            {
                get => gyroMouseStickAction.mStickParams.antiDeadzoneY;
                set
                {
                    gyroMouseStickAction.mStickParams.antiDeadzoneY = value;
                    AntiDeadZoneYChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler AntiDeadZoneYChanged;

            public bool InvertX
            {
                get => gyroMouseStickAction.mStickParams.invertX;
                set
                {
                    gyroMouseStickAction.mStickParams.invertX = value;
                    InvertXChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler InvertXChanged;

            public bool InvertY
            {
                get => gyroMouseStickAction.mStickParams.invertY;
                set
                {
                    gyroMouseStickAction.mStickParams.invertY = value;
                    InvertYChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler InvertYChanged;

            public double VerticalScale
            {
                get => gyroMouseStickAction.mStickParams.verticalScale;
                set
                {
                    gyroMouseStickAction.mStickParams.verticalScale = Math.Clamp(value, 0.0, 10.0);
                    VerticalScaleChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler VerticalScaleChanged;

            public bool JitterCompensation
            {
                get => gyroMouseStickAction.mStickParams.jitterCompensation;
                set
                {
                    gyroMouseStickAction.mStickParams.jitterCompensation = value;
                    JitterCompensationChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler JitterCompensationChanged;

            public bool ShouldSerializeJitterCompensation()
            {
                return gyroMouseStickAction.ChangedProperties.Add(GyroMouseJoystick.PropertyKeyStrings.JITTER_COMPENSATION);
            }

            public GyroMouseJoystickOuputAxes OutputAxes
            {
                get => gyroMouseStickAction.mStickParams.outputAxes;
                set
                {
                    gyroMouseStickAction.mStickParams.outputAxes = value;
                    OutputAxesChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler OutputAxesChanged;

            [JsonConverter(typeof(StringEnumConverter))]
            public StickActionCodes OutputStick
            {
                get => gyroMouseStickAction.mStickParams.outputStick;
                set
                {
                    gyroMouseStickAction.mStickParams.OutputStick = value;
                    OutputStickChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler OutputStickChanged;

            public bool MaxOutputEnabled
            {
                get => gyroMouseStickAction.mStickParams.maxOutputEnabled;
                set
                {
                    gyroMouseStickAction.mStickParams.maxOutputEnabled = value;
                    MaxOutputEnabledChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler MaxOutputEnabledChanged;

            public double MaxOutput
            {
                get => gyroMouseStickAction.mStickParams.maxOutput;
                set
                {
                    gyroMouseStickAction.mStickParams.maxOutput = value;
                    MaxOutputChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler MaxOutputChanged;

            public bool Toggle
            {
                get => gyroMouseStickAction.mStickParams.toggleAction;
                set
                {
                    gyroMouseStickAction.mStickParams.toggleAction = value;
                    ToggleChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler ToggleChanged;

            public bool SmoothingEnabled
            {
                get => gyroMouseStickAction.mStickParams.smoothing;
                set
                {
                    gyroMouseStickAction.mStickParams.smoothing = value;
                    SmoothingEnabledChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler SmoothingEnabledChanged;

            public double SmoothingMinCutoff
            {
                get => gyroMouseStickAction.mStickParams.smoothingFilterSettings.minCutOff;
                set
                {
                    gyroMouseStickAction.mStickParams.smoothingFilterSettings.minCutOff = Math.Clamp(value, 0.0, 10.0);
                    gyroMouseStickAction.mStickParams.smoothingFilterSettings.UpdateSmoothingFilters();
                    SmoothingMinCutoffChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler SmoothingMinCutoffChanged;

            public double SmoothingBeta
            {
                get => gyroMouseStickAction.mStickParams.smoothingFilterSettings.beta;
                set
                {
                    gyroMouseStickAction.mStickParams.smoothingFilterSettings.beta = Math.Clamp(value, 0.0, 1.0);
                    gyroMouseStickAction.mStickParams.smoothingFilterSettings.UpdateSmoothingFilters();
                    SmoothingBetaChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler SmoothingBetaChanged;

            public GyroMouseJoystickSettings(GyroMouseJoystick mouseStickAction)
            {
                gyroMouseStickAction = mouseStickAction;
            }
        }

        private GyroMouseJoystick gyroMouseJoystickAction = new GyroMouseJoystick();
        private GyroMouseJoystickSettings settings;
        public GyroMouseJoystickSettings Settings
        {
            get => settings;
            set => settings = value;
        }

        // Deserialize
        public GyroMouseJoystickSerializer() : base()
        {
            mapAction = gyroMouseJoystickAction;
            settings = new GyroMouseJoystickSettings(gyroMouseJoystickAction);

            NameChanged += GyroMouseJoystickSerializer_NameChanged;
            settings.DeadZoneChanged += Settings_DeadZoneChanged;
            settings.MaxZoneChanged += Settings_MaxZoneChanged;
            settings.TriggerButtonsChanged += Settings_TriggerButtonsChanged;
            settings.TriggerActivatesChanged += Settings_TriggerActivatesChanged;
            settings.EvalCondChanged += Settings_EvalCondChanged;
            settings.UseForXAxisChanged += Settings_UseForXAxisChanged;
            settings.AntiDeadZoneXChanged += Settings_AntiDeadZoneXChanged;
            settings.AntiDeadZoneYChanged += Settings_AntiDeadZoneYChanged;
            settings.InvertXChanged += Settings_InvertXChanged;
            settings.InvertYChanged += Settings_InvertYChanged;
            settings.VerticalScaleChanged += Settings_VerticalScaleChanged;
            settings.OutputAxesChanged += Settings_OutputAxesChanged;
            settings.OutputStickChanged += Settings_OutputStickChanged;
            settings.MaxOutputEnabledChanged += Settings_MaxOutputEnabledChanged;
            settings.MaxOutputChanged += Settings_MaxOutputChanged;
            settings.ToggleChanged += Settings_ToggleChanged;
            settings.JitterCompensationChanged += Settings_JitterCompensationChanged;
            settings.SmoothingEnabledChanged += Settings_SmoothingEnabledChanged;
            settings.SmoothingMinCutoffChanged += Settings_SmoothingMinCutoffChanged;
            settings.SmoothingBetaChanged += Settings_SmoothingBetaChanged;
        }

        private void Settings_JitterCompensationChanged(object sender, EventArgs e)
        {
            gyroMouseJoystickAction.ChangedProperties.Add(GyroMouseJoystick.PropertyKeyStrings.JITTER_COMPENSATION);
        }

        private void Settings_EvalCondChanged(object sender, EventArgs e)
        {
            gyroMouseJoystickAction.ChangedProperties.Add(GyroMouseJoystick.PropertyKeyStrings.TRIGGER_EVAL_COND);
        }

        private void Settings_SmoothingBetaChanged(object sender, EventArgs e)
        {
            gyroMouseJoystickAction.ChangedProperties.Add(GyroMouseJoystick.PropertyKeyStrings.SMOOTHING_FILTER);
        }

        private void Settings_SmoothingMinCutoffChanged(object sender, EventArgs e)
        {
            gyroMouseJoystickAction.ChangedProperties.Add(GyroMouseJoystick.PropertyKeyStrings.SMOOTHING_FILTER);
        }

        private void Settings_SmoothingEnabledChanged(object sender, EventArgs e)
        {
            gyroMouseJoystickAction.ChangedProperties.Add(GyroMouseJoystick.PropertyKeyStrings.SMOOTHING_ENABLED);
        }

        private void Settings_ToggleChanged(object sender, EventArgs e)
        {
            gyroMouseJoystickAction.ChangedProperties.Add(GyroMouseJoystick.PropertyKeyStrings.TOGGLE_ACTION);
        }

        private void Settings_MaxOutputChanged(object sender, EventArgs e)
        {
            gyroMouseJoystickAction.ChangedProperties.Add(GyroMouseJoystick.PropertyKeyStrings.MAX_OUTPUT);
        }

        private void Settings_MaxOutputEnabledChanged(object sender, EventArgs e)
        {
            gyroMouseJoystickAction.ChangedProperties.Add(GyroMouseJoystick.PropertyKeyStrings.MAX_OUTPUT_ENABLED);
        }

        private void Settings_OutputStickChanged(object sender, EventArgs e)
        {
            gyroMouseJoystickAction.ChangedProperties.Add(GyroMouseJoystick.PropertyKeyStrings.OUTPUT_STICK);
        }

        private void Settings_OutputAxesChanged(object sender, EventArgs e)
        {
            gyroMouseJoystickAction.ChangedProperties.Add(GyroMouseJoystick.PropertyKeyStrings.OUTPUT_AXES);
        }

        private void Settings_VerticalScaleChanged(object sender, EventArgs e)
        {
            gyroMouseJoystickAction.ChangedProperties.Add(GyroMouseJoystick.PropertyKeyStrings.VERTICAL_SCALE);
        }

        private void Settings_InvertYChanged(object sender, EventArgs e)
        {
            gyroMouseJoystickAction.ChangedProperties.Add(GyroMouseJoystick.PropertyKeyStrings.INVERT_Y);
        }

        private void Settings_InvertXChanged(object sender, EventArgs e)
        {
            gyroMouseJoystickAction.ChangedProperties.Add(GyroMouseJoystick.PropertyKeyStrings.INVERT_X);
        }

        private void Settings_MaxZoneChanged(object sender, EventArgs e)
        {
            gyroMouseJoystickAction.ChangedProperties.Add(GyroMouseJoystick.PropertyKeyStrings.MAX_ZONE);
        }

        private void Settings_AntiDeadZoneYChanged(object sender, EventArgs e)
        {
            gyroMouseJoystickAction.ChangedProperties.Add(GyroMouseJoystick.PropertyKeyStrings.ANTIDEAD_ZONE_Y);
        }

        private void Settings_AntiDeadZoneXChanged(object sender, EventArgs e)
        {
            gyroMouseJoystickAction.ChangedProperties.Add(GyroMouseJoystick.PropertyKeyStrings.ANTIDEAD_ZONE_X);
        }

        private void Settings_UseForXAxisChanged(object sender, EventArgs e)
        {
            gyroMouseJoystickAction.ChangedProperties.Add(GyroMouseJoystick.PropertyKeyStrings.X_AXIS);
        }

        private void Settings_TriggerActivatesChanged(object sender, EventArgs e)
        {
            gyroMouseJoystickAction.ChangedProperties.Add(GyroMouseJoystick.PropertyKeyStrings.TRIGGER_ACTIVATE);
        }

        private void Settings_TriggerButtonsChanged(object sender, EventArgs e)
        {
            gyroMouseJoystickAction.ChangedProperties.Add(GyroMouseJoystick.PropertyKeyStrings.TRIGGER_BUTTONS);
        }

        public GyroMouseJoystickSerializer(ActionLayer tempLayer, MapAction action) :
            base(tempLayer, action)
        {
            if (action is GyroMouseJoystick temp)
            {
                gyroMouseJoystickAction = temp;
                mapAction = gyroMouseJoystickAction;
                settings = new GyroMouseJoystickSettings(gyroMouseJoystickAction);
            }
        }

        private void Settings_DeadZoneChanged(object sender, EventArgs e)
        {
            gyroMouseJoystickAction.ChangedProperties.Add(GyroMouseJoystick.PropertyKeyStrings.DEAD_ZONE);
        }

        private void GyroMouseJoystickSerializer_NameChanged(object sender, EventArgs e)
        {
            gyroMouseJoystickAction.ChangedProperties.Add(GyroMouseJoystick.PropertyKeyStrings.NAME);
        }
    }

    public class GyroNoMapActionSerializer : MapActionSerializer
    {
        private GyroNoMapAction gyroNoAction = new GyroNoMapAction();

        public GyroNoMapActionSerializer() : base()
        {
            mapAction = gyroNoAction;
        }

        public GyroNoMapActionSerializer(ActionLayer tempLayer, MapAction action) :
            base(tempLayer, action)
        {
            if (action is GyroNoMapAction temp)
            {
                gyroNoAction = temp;
                mapAction = gyroNoAction;
            }
        }
    }

    public class GyroPadActionSerializer : MapActionSerializer
    {
        public class GyroPadDirBinding
        {
            private string actionDirName;
            [JsonProperty("Name", Required = Required.Default)]
            public string ActionDirName
            {
                get => actionDirName;
                set => actionDirName = value;
            }

            private List<ActionFuncSerializer> actionFuncSerializers =
                new List<ActionFuncSerializer>();
            [JsonProperty("Functions", Required = Required.Always)]
            public List<ActionFuncSerializer> ActionFuncSerializers
            {
                get => actionFuncSerializers;
                set
                {
                    actionFuncSerializers = value;
                    ActionFuncSerializersChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler ActionFuncSerializersChanged;
        }

        public class GyroPadActionSettings
        {
            private GyroPadAction padAction;

            public GyroPadAction.DPadMode PadMode
            {
                get => padAction.CurrentMode;
                set
                {
                    padAction.CurrentMode = value;
                    PadModeChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler PadModeChanged;

            [JsonConverter(typeof(TriggerButtonsConverter))]
            public JoypadActionCodes[] TriggerButtons
            {
                get => padAction.padParams.gyroTriggerButtons;
                set
                {
                    padAction.padParams.gyroTriggerButtons = value;
                    TriggersButtonChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler TriggersButtonChanged;

            public bool TriggerActivates
            {
                get => padAction.padParams.triggerActivates;
                set
                {
                    padAction.padParams.triggerActivates = value;
                    TriggerActivatesChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler TriggerActivatesChanged;

            public GyroActionsUtils.GyroTriggerEvalCond EvalCond
            {
                get => padAction.padParams.andCond ?
                    GyroActionsUtils.GyroTriggerEvalCond.And : GyroActionsUtils.GyroTriggerEvalCond.Or;
                set
                {
                    padAction.padParams.andCond =
                        value == GyroActionsUtils.GyroTriggerEvalCond.And ? true : false;

                    EvalCondChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            public event EventHandler EvalCondChanged;

            public GyroPadActionSettings(GyroPadAction padAction)
            {
                this.padAction = padAction;
            }
        }

        private GyroPadAction gyroPadAction = new GyroPadAction();

        private Dictionary<GyroPadAction.DpadDirections, GyroPadDirBinding> dictPadBindings =
            new Dictionary<GyroPadAction.DpadDirections, GyroPadDirBinding>();

        [JsonProperty("Bindings", Required = Required.Always)]
        public Dictionary<GyroPadAction.DpadDirections, GyroPadDirBinding> DictPadBindings
        {
            get => dictPadBindings;
            set => dictPadBindings = value;
        }

        private GyroPadActionSettings settings;
        public GyroPadActionSettings Settings
        {
            get => settings;
            set => settings = value;
        }

        public GyroPadActionSerializer() : base()
        {
            mapAction = gyroPadAction;
            settings = new GyroPadActionSettings(gyroPadAction);

            settings.PadModeChanged += Settings_PadModeChanged;
            settings.TriggersButtonChanged += Settings_TriggersButtonChanged;
            settings.TriggerActivatesChanged += Settings_TriggerActivatesChanged;
            settings.EvalCondChanged += Settings_EvalCondChanged;
        }

        private void Settings_EvalCondChanged(object sender, EventArgs e)
        {
            gyroPadAction.ChangedProperties.Add(GyroPadAction.PropertyKeyStrings.TRIGGER_EVAL_COND);
        }

        private void Settings_TriggerActivatesChanged(object sender, EventArgs e)
        {
            gyroPadAction.ChangedProperties.Add(GyroPadAction.PropertyKeyStrings.TRIGGER_ACTIVATE);
        }

        private void Settings_TriggersButtonChanged(object sender, EventArgs e)
        {
            gyroPadAction.ChangedProperties.Add(GyroPadAction.PropertyKeyStrings.TRIGGER_BUTTONS);
        }

        private void Settings_PadModeChanged(object sender, EventArgs e)
        {
            //if (mapAction.ParentAction != null)
            {
                gyroPadAction.ChangedProperties.Add(GyroPadAction.PropertyKeyStrings.PAD_MODE);
            }
        }

        public GyroPadActionSerializer(ActionLayer tempLayer, MapAction action) :
            base(tempLayer, action)
        {
            if (action is GyroPadAction temp)
            {
                gyroPadAction = temp;
                mapAction = gyroPadAction;
                settings = new GyroPadActionSettings(gyroPadAction);
                PopulateFuncs();
            }
        }

        // Post-deserialize
        public override void PopulateMap()
        {
            foreach (AxisDirButton dirButton in gyroPadAction.EventCodes4)
            {
                dirButton?.ActionFuncs.Clear();
            }

            //foreach (KeyValuePair<GyroPadAction.DpadDirections, List<ActionFuncSerializer>> tempKeyPair in dictPadBindings)
            foreach (KeyValuePair<GyroPadAction.DpadDirections, GyroPadDirBinding> tempKeyPair in dictPadBindings)
            {
                GyroPadAction.DpadDirections dir = tempKeyPair.Key;
                List<ActionFuncSerializer> tempSerializers = tempKeyPair.Value.ActionFuncSerializers;
                //List<ActionFuncSerializer> tempSerializers = tempKeyPair.Value;

                //ButtonAction tempDirButton = dPadAction.EventCodes4[(int)dir];
                //if (tempDirButton != null)
                {
                    //tempDirButton.Name = tempKeyPair.Value.ActionDirName;
                    AxisDirButton dirButton = new AxisDirButton();
                    dirButton.Name = tempKeyPair.Value.ActionDirName;
                    foreach (ActionFuncSerializer serializer in tempSerializers)
                    {
                        //ButtonAction dirButton = dPadAction.EventCodes4[(int)dir];
                        serializer.PopulateFunc();
                        dirButton.ActionFuncs.Add(serializer.ActionFunc);
                        //dPadAction.EventCodes4[(int)dir] = dirButton;
                    }

                    gyroPadAction.EventCodes4[(int)dir] = dirButton;
                    FlagBtnChangedDirection(dir, gyroPadAction);
                }
            }
        }

        // Pre-serialize
        private void PopulateFuncs()
        {
            List<ActionFuncSerializer> tempFuncs = new List<ActionFuncSerializer>();
            GyroPadAction.DpadDirections[] tempDirs = null;

            if (gyroPadAction.CurrentMode == GyroPadAction.DPadMode.Standard ||
                gyroPadAction.CurrentMode == GyroPadAction.DPadMode.FourWayCardinal)
            {
                tempDirs = new GyroPadAction.DpadDirections[4]
                {
                    GyroPadAction.DpadDirections.Up, GyroPadAction.DpadDirections.Down,
                    GyroPadAction.DpadDirections.Left, GyroPadAction.DpadDirections.Right
                };
            }
            else if (gyroPadAction.CurrentMode == GyroPadAction.DPadMode.EightWay)
            {
                tempDirs = new GyroPadAction.DpadDirections[8]
                {
                    GyroPadAction.DpadDirections.Up, GyroPadAction.DpadDirections.Down,
                    GyroPadAction.DpadDirections.Left, GyroPadAction.DpadDirections.Right,
                    GyroPadAction.DpadDirections.UpLeft, GyroPadAction.DpadDirections.UpRight,
                    GyroPadAction.DpadDirections.DownLeft, GyroPadAction.DpadDirections.DownRight
                };
            }
            else if (gyroPadAction.CurrentMode == GyroPadAction.DPadMode.FourWayDiagonal)
            {
                tempDirs = new GyroPadAction.DpadDirections[4]
                {
                    GyroPadAction.DpadDirections.Up, GyroPadAction.DpadDirections.Down,
                    GyroPadAction.DpadDirections.Left, GyroPadAction.DpadDirections.Right
                };
            }

            for (int i = 0; i < tempDirs.Length; i++)
            {
                GyroPadAction.DpadDirections tempDir = tempDirs[i];
                AxisDirButton dirButton = gyroPadAction.EventCodes4[(int)tempDir];

                tempFuncs.Clear();
                foreach (ActionFunc tempFunc in dirButton.ActionFuncs)
                {
                    ActionFuncSerializer tempSerializer =
                        ActionFuncSerializerFactory.CreateSerializer(tempFunc);
                    if (tempSerializer != null)
                    {
                        tempFuncs.Add(tempSerializer);
                    }
                }

                //dictPadBindings.Add(tempDir, tempFuncs);
                dictPadBindings.Add(tempDir, new GyroPadDirBinding()
                {
                    ActionDirName = dirButton.Name,
                    ActionFuncSerializers = new List<ActionFuncSerializer>(tempFuncs),
                });
            }
        }

        public void FlagBtnChangedDirection(GyroPadAction.DpadDirections dir,
            GyroPadAction action)
        {
            switch (dir)
            {
                case GyroPadAction.DpadDirections.Up:
                    action.ChangedProperties.Add(GyroPadAction.PropertyKeyStrings.PAD_DIR_UP);
                    break;
                case GyroPadAction.DpadDirections.Down:
                    action.ChangedProperties.Add(GyroPadAction.PropertyKeyStrings.PAD_DIR_DOWN);
                    break;
                case GyroPadAction.DpadDirections.Left:
                    action.ChangedProperties.Add(GyroPadAction.PropertyKeyStrings.PAD_DIR_LEFT);
                    break;
                case GyroPadAction.DpadDirections.Right:
                    action.ChangedProperties.Add(GyroPadAction.PropertyKeyStrings.PAD_DIR_RIGHT);
                    break;
                case GyroPadAction.DpadDirections.UpLeft:
                    action.ChangedProperties.Add(GyroPadAction.PropertyKeyStrings.PAD_DIR_UPLEFT);
                    break;
                case GyroPadAction.DpadDirections.UpRight:
                    action.ChangedProperties.Add(GyroPadAction.PropertyKeyStrings.PAD_DIR_UPRIGHT);
                    break;
                case GyroPadAction.DpadDirections.DownLeft:
                    action.ChangedProperties.Add(GyroPadAction.PropertyKeyStrings.PAD_DIR_DOWNLEFT);
                    break;
                case GyroPadAction.DpadDirections.DownRight:
                    action.ChangedProperties.Add(GyroPadAction.PropertyKeyStrings.PAD_DIR_DOWNRIGHT);
                    break;
                default:
                    break;
            }
        }
    }

    // Only use JsonConverter for deserializing JSON to MapActionSerializer
    // instance. Use base serializer to handle serializing an instance
    // back to JSON
    public class MapActionTypeConverter : JsonConverter
    {
        public override bool CanWrite => false;
        public override bool CanRead => true;

        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(MapActionSerializer);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            JObject j = JObject.Load(reader);
            Trace.WriteLine("HUP RIDE");
            MapActionSerializer current = existingValue as MapActionSerializer;

            string actionOutput = j["ActionMode"]?.ToString();
            //bool status = int.TryParse(j["Index"]?.ToString(), out int ind);
            object resultInstance = null;

            // If action parsing fails, just skip it
            try
            {
                switch (actionOutput)
                {
                    case "ButtonAction":
                        ButtonActionSerializer instance = new ButtonActionSerializer();
                        ButtonAction tempAction = instance.ButtonAction;
                        /*if (ActionSetSerializer.TopActionLayer != null)
                        {
                            //int ind = ActionLayerSerializer.CurrentActionIndex;
                            bool status = int.TryParse(j["Id"]?.ToString(), out int parentId);
                            int ind = status ? ActionSetSerializer.TopActionLayer.LayerActions.FindIndex((item) => item.Id == parentId) : -1;
                            if (status && ind >= 0)
                            //if (ind > 0 &&
                            //    ind < ActionSetSerializer.TopActionLayer.LayerActions.Count)
                            {
                                MapAction tempMap =
                                    ActionSetSerializer.TopActionLayer.LayerActions[ind];
                                if (tempMap.IsSameType(tempMap, tempAction))
                                {
                                    instance.ButtonAction =
                                        ((tempMap as ButtonAction).DuplicateAction() as ButtonAction);
                                }
                            }
                        }
                        */

                        JsonConvert.PopulateObject(j.ToString(), instance);
                        instance.ActionFuncSerializers.RemoveAll((item) => item == null);
                        instance.RaiseActionFuncSerializersChanged();
                        resultInstance = instance;
                        break;
                    case "ButtonNoAction":
                        ButtonNoActionSerializer btnNoActinstance = new ButtonNoActionSerializer();
                        JsonConvert.PopulateObject(j.ToString(), btnNoActinstance);
                        resultInstance = btnNoActinstance;
                        break;
                    case "StickPadAction":
                        StickPadActionSerializer stickPadInstance = new StickPadActionSerializer();
                        JsonConvert.PopulateObject(j.ToString(), stickPadInstance);
                        foreach (StickPadAction.DpadDirections dir in stickPadInstance.DictPadBindings.Keys)
                        {
                            //stickPadInstance.DictPadBindings[dir].RemoveAll((item) => item == null);
                            //stickPadInstance.DictPadBindings[dir].RemoveAll((item) => item == null);
                            stickPadInstance.DictPadBindings[dir].ActionFuncSerializers.RemoveAll((item) => item == null);
                        }

                        resultInstance = stickPadInstance;
                        break;
                    case "StickMouseAction":
                        StickMouseSerializer stickMouseInstance = new StickMouseSerializer();
                        JsonConvert.PopulateObject(j.ToString(), stickMouseInstance);
                        resultInstance = stickMouseInstance;
                        break;
                    case "StickTranslateAction":
                        StickTranslateSerializer stickTransActInstance = new StickTranslateSerializer();
                        JsonConvert.PopulateObject(j.ToString(), stickTransActInstance);
                        resultInstance = stickTransActInstance;
                        break;
                    case "StickAbsMouseAction":
                        StickAbsMouseActionSerializer stickAbsMouseInstance = new StickAbsMouseActionSerializer();
                        JsonConvert.PopulateObject(j.ToString(), stickAbsMouseInstance);
                        resultInstance = stickAbsMouseInstance;
                        break;
                    case "StickCircularAction":
                        StickCircularSerializer stickCircActInstance = new StickCircularSerializer();
                        JsonConvert.PopulateObject(j.ToString(), stickCircActInstance);
                        resultInstance = stickCircActInstance;
                        break;
                    case "StickFlickStickAction":
                        StickFlickStickActionSerializer flickInstance = new StickFlickStickActionSerializer();
                        JsonConvert.PopulateObject(j.ToString(), flickInstance);
                        resultInstance = flickInstance;
                        break;
                    case "StickHybridAimAction":
                        StickHybridAimActionSerializer hybridAimInstance = new StickHybridAimActionSerializer();
                        JsonConvert.PopulateObject(j.ToString(), hybridAimInstance);
                        resultInstance = hybridAimInstance;
                        break;
                    case "StickMouseRingAction":
                        StickMouseRingActionSerializer mouseRingInstance = new StickMouseRingActionSerializer();
                        JsonConvert.PopulateObject(j.ToString(), mouseRingInstance);
                        resultInstance = mouseRingInstance;
                        break;
                    case "StickAnalogEmulationAction":
                        AnalogEmulationActionSerializer analogEmuInstance = new AnalogEmulationActionSerializer();
                        JsonConvert.PopulateObject(j.ToString(), analogEmuInstance);
                        resultInstance = analogEmuInstance;
                        break;
                    case "StickNoAction":
                        StickNoActionSerializer stickNoActinstance = new StickNoActionSerializer();
                        JsonConvert.PopulateObject(j.ToString(), stickNoActinstance);
                        resultInstance = stickNoActinstance;
                        break;
                    case "TriggerTranslateAction":
                        TriggerTranslateActionSerializer triggerActInstance = new TriggerTranslateActionSerializer();
                        JsonConvert.PopulateObject(j.ToString(), triggerActInstance);
                        resultInstance = triggerActInstance;
                        break;
                    case "TriggerButtonAction":
                        TriggerButtonActionSerializer triggerButtonActInstance = new TriggerButtonActionSerializer();
                        JsonConvert.PopulateObject(j.ToString(), triggerButtonActInstance);
                        resultInstance = triggerButtonActInstance;
                        break;
                    case "TriggerDualStageAction":
                        TriggerDualStageActionSerializer triggerDualActInstance = new TriggerDualStageActionSerializer();
                        JsonConvert.PopulateObject(j.ToString(), triggerDualActInstance);
                        resultInstance = triggerDualActInstance;
                        break;
                    case "TriggerMouseAction":
                        TriggerMouseActionSerializer triggerMouseActInstance = new TriggerMouseActionSerializer();
                        JsonConvert.PopulateObject(j.ToString(), triggerMouseActInstance);
                        resultInstance = triggerMouseActInstance;
                        break;
                    case "TouchStickTranslateAction":
                        TouchpadStickActionSerializer touchStickActInstance = new TouchpadStickActionSerializer();
                        JsonConvert.PopulateObject(j.ToString(), touchStickActInstance);
                        resultInstance = touchStickActInstance;
                        break;
                    case "TouchMouseAction":
                        TouchpadMouseSerializer touchMouseActInstance = new TouchpadMouseSerializer();
                        JsonConvert.PopulateObject(j.ToString(), touchMouseActInstance);
                        resultInstance = touchMouseActInstance;
                        break;
                    case "TouchMouseJoystickAction":
                        TouchpadMouseJoystickSerializer touchMouseJoyActInstance = new TouchpadMouseJoystickSerializer();
                        JsonConvert.PopulateObject(j.ToString(), touchMouseJoyActInstance);
                        resultInstance = touchMouseJoyActInstance;
                        break;
                    case "TouchActionPadAction":
                        TouchpadActionPadSerializer touchActionPadInstance = new TouchpadActionPadSerializer();
                        JsonConvert.PopulateObject(j.ToString(), touchActionPadInstance);
                        resultInstance = touchActionPadInstance;
                        break;
                    case "TouchAbsPadAction":
                        TouchpadAbsActionSerializer touchAbsActionInstance = new TouchpadAbsActionSerializer();
                        JsonConvert.PopulateObject(j.ToString(), touchAbsActionInstance);
                        resultInstance = touchAbsActionInstance;
                        break;
                    case "TouchCircularAction":
                        TouchpadCircularSerializer touchCircActInstance = new TouchpadCircularSerializer();
                        JsonConvert.PopulateObject(j.ToString(), touchCircActInstance);
                        resultInstance = touchCircActInstance;
                        break;
                    case "TouchDirSwipeAction":
                        TouchpadDirectionalSwipeSerializer touchDirSwipeInstance = new TouchpadDirectionalSwipeSerializer();
                        JsonConvert.PopulateObject(j.ToString(), touchDirSwipeInstance);
                        resultInstance = touchDirSwipeInstance;
                        break;
                    case "TouchAxesAction":
                        TouchpadAxesActionSerializer touchAxesActInstance = new TouchpadAxesActionSerializer();
                        JsonConvert.PopulateObject(j.ToString(), touchAxesActInstance);
                        resultInstance = touchAxesActInstance;
                        break;
                    case "TouchSingleButtonAction":
                        TouchpadSingleButtonSerializer touchSingleBtnActInstance = new TouchpadSingleButtonSerializer();
                        JsonConvert.PopulateObject(j.ToString(), touchSingleBtnActInstance);
                        resultInstance = touchSingleBtnActInstance;
                        break;
                    case "TouchFlickStickAction":
                        TouchpadFlickStickActionSerializer touchFlickInstance = new TouchpadFlickStickActionSerializer();
                        JsonConvert.PopulateObject(j.ToString(), touchFlickInstance);
                        resultInstance = touchFlickInstance;
                        break;
                    case "TouchNoAction":
                        TouchpadNoActionSerializer touchNoActinstance = new TouchpadNoActionSerializer();
                        JsonConvert.PopulateObject(j.ToString(), touchNoActinstance);
                        resultInstance = touchNoActinstance;
                        break;
                    case "DPadAction":
                        DpadActionSerializer dpadActSerializer = new DpadActionSerializer();
                        JsonConvert.PopulateObject(j.ToString(), dpadActSerializer);
                        foreach (DPadActions.DpadDirections dir in dpadActSerializer.DictPadBindings.Keys)
                        {
                            //dpadActSerializer.DictPadBindings[dir].RemoveAll((item) => item == null);
                            dpadActSerializer.DictPadBindings[dir].ActionFuncSerializers.RemoveAll((item) => item == null);
                        }
                        resultInstance = dpadActSerializer;
                        break;
                    case "DPadNoAction":
                        DpadNoActionSerializer dpadNoActSerializer = new DpadNoActionSerializer();
                        JsonConvert.PopulateObject(j.ToString(), dpadNoActSerializer);
                        resultInstance = dpadNoActSerializer;
                        break;
                    case "DPadTranslateAction":
                        DpadTranslateSerializer dpadTransActSerializer = new DpadTranslateSerializer();
                        JsonConvert.PopulateObject(j.ToString(), dpadTransActSerializer);
                        resultInstance = dpadTransActSerializer;
                        break;
                    case "GyroMouseAction":
                        GyroMouseSerializer gyroMouseInstance = new GyroMouseSerializer();
                        JsonConvert.PopulateObject(j.ToString(), gyroMouseInstance);
                        resultInstance = gyroMouseInstance;
                        break;
                    case "GyroMouseJoystickAction":
                        GyroMouseJoystickSerializer gyroMouseStickInstance = new GyroMouseJoystickSerializer();
                        JsonConvert.PopulateObject(j.ToString(), gyroMouseStickInstance);
                        resultInstance = gyroMouseStickInstance;
                        break;
                    case "GyroDirSwipeAction":
                        GyroDirectionalSwipeSerializer gyroDirSwipeInstance = new GyroDirectionalSwipeSerializer();
                        JsonConvert.PopulateObject(j.ToString(), gyroDirSwipeInstance);
                        resultInstance = gyroDirSwipeInstance;
                        break;
                    case "GyroNoAction":
                        GyroNoMapActionSerializer gyroNoActinstance = new GyroNoMapActionSerializer();
                        JsonConvert.PopulateObject(j.ToString(), gyroNoActinstance);
                        resultInstance = gyroNoActinstance;
                        break;
                    default:
                        break;
                }
            }
            catch(JsonReaderException)
            {
            }

            // No longer throw exception here if action type is unknown.
            // Leaving old code here temporarily(?)
            //if (resultInstance == null)
            //{
            //    throw new JsonException($"Failed to read invalid type of ({actionOutput})");
            //}

            return resultInstance;
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
        }
    }

    [JsonConverter(typeof(OutputActionDataTypeConverter))]
    public class OutputActionDataSerializer
    {
        private OutputActionData outputData;
        [JsonIgnore]
        public OutputActionData OutputData { get => outputData; }

        [JsonProperty(PropertyName = "Type", Required = Required.Always)]
        public ActionType ActionType
        {
            get => outputData.OutputType;
            set => outputData.OutputType = value;
        }

        public int Code
        {
            get => outputData.OutputCode;
            set => outputData.OutputCode = value;
        }

        public JoypadActionCodes PadCode
        {
            get => outputData.JoypadCode;
            set => outputData.JoypadCode = value;
        }

        [JsonConstructor]
        public OutputActionDataSerializer()
        {
            this.outputData = new OutputActionData(ActionType.Empty, 0);
        }

        public OutputActionDataSerializer(OutputActionData data)
        {
            this.outputData = data;
        }

        public static uint ParseKeyboardCodeString(string key)
        {
            uint result = 0;
            switch (key)
            {
                case "A":
                    result = (uint)VirtualKeys.A;
                    break;
                case "B":
                    result = (uint)VirtualKeys.B;
                    break;
                case "C":
                    result = (uint)VirtualKeys.C;
                    break;
                case "D":
                    result = (uint)VirtualKeys.D;
                    break;
                case "E":
                    result = (uint)VirtualKeys.E;
                    break;
                case "F":
                    result = (uint)VirtualKeys.F;
                    break;
                case "G":
                    result = (uint)VirtualKeys.G;
                    break;
                case "H":
                    result = (uint)VirtualKeys.H;
                    break;
                case "I":
                    result = (uint)VirtualKeys.I;
                    break;
                case "J":
                    result = (uint)VirtualKeys.J;
                    break;
                case "K":
                    result = (uint)VirtualKeys.K;
                    break;
                case "L":
                    result = (uint)VirtualKeys.L;
                    break;
                case "M":
                    result = (uint)VirtualKeys.M;
                    break;
                case "N":
                    result = (uint)VirtualKeys.N;
                    break;
                case "O":
                    result = (uint)VirtualKeys.O;
                    break;
                case "P":
                    result = (uint)VirtualKeys.P;
                    break;
                case "Q":
                    result = (uint)VirtualKeys.Q;
                    break;
                case "R":
                    result = (uint)VirtualKeys.R;
                    break;
                case "S":
                    result = (uint)VirtualKeys.S;
                    break;
                case "T":
                    result = (uint)VirtualKeys.T;
                    break;
                case "U":
                    result = (uint)VirtualKeys.U;
                    break;
                case "V":
                    result = (uint)VirtualKeys.V;
                    break;
                case "W":
                    result = (uint)VirtualKeys.W;
                    break;
                case "X":
                    result = (uint)VirtualKeys.X;
                    break;
                case "Y":
                    result = (uint)VirtualKeys.Y;
                    break;
                case "Z":
                    result = (uint)VirtualKeys.Z;
                    break;
                case "N1":
                    result = (uint)VirtualKeys.N1;
                    break;
                case "N2":
                    result = (uint)VirtualKeys.N2;
                    break;
                case "N3":
                    result = (uint)VirtualKeys.N3;
                    break;
                case "N4":
                    result = (uint)VirtualKeys.N4;
                    break;
                case "N5":
                    result = (uint)VirtualKeys.N5;
                    break;
                case "N6":
                    result = (uint)VirtualKeys.N6;
                    break;
                case "N7":
                    result = (uint)VirtualKeys.N7;
                    break;
                case "N8":
                    result = (uint)VirtualKeys.N8;
                    break;
                case "N9":
                    result = (uint)VirtualKeys.N9;
                    break;
                case "N0":
                    result = (uint)VirtualKeys.N0;
                    break;
                case "Minus":
                    result = (uint)VirtualKeys.OEMMinus;
                    break;
                case "Equal":
                    result = (uint)VirtualKeys.NEC_Equal;
                    break;
                case "LeftBracket":
                    result = (uint)VirtualKeys.OEM4;
                    break;
                case "RightBracket":
                    result = (uint)VirtualKeys.OEM6;
                    break;
                case "Backslash":
                    result = (uint)VirtualKeys.OEM5;
                    break;
                case "Semicolor":
                    result = (uint)VirtualKeys.OEM1;
                    break;
                case "Quote":
                    result = (uint)VirtualKeys.OEM7;
                    break;
                case "Comma":
                    result = (uint)VirtualKeys.OEMComma;
                    break;
                case "Period":
                    result = (uint)VirtualKeys.OEMPeriod;
                    break;
                case "Slash":
                    result = (uint)VirtualKeys.OEM2;
                    break;
                case "Space":
                case "Spacebar":
                    result = (uint)VirtualKeys.Space;
                    break;
                case "Backspace":
                    result = (uint)VirtualKeys.Back;
                    break;
                case "CapsLock":
                    result = (uint)VirtualKeys.CapsLock;
                    break;
                case "LeftAlt":
                    result = (uint)VirtualKeys.LeftMenu;
                    break;
                case "RightAlt":
                    result = (uint)VirtualKeys.RightMenu;
                    break;
                case "Windows":
                case "LeftWindows":
                    result = (uint)VirtualKeys.LeftWindows;
                    break;
                case "RightWindows":
                    result = (uint)VirtualKeys.RightWindows;
                    break;
                case "LeftControl":
                    result = (uint)VirtualKeys.LeftControl;
                    break;
                case "RightControl":
                    result = (uint)VirtualKeys.RightControl;
                    break;
                case "Esc":
                case "Escape":
                    result = (uint)VirtualKeys.Escape;
                    break;
                case "LeftShift":
                    result = (uint)VirtualKeys.LeftShift;
                    break;
                case "RightShift":
                    result = (uint)VirtualKeys.RightShift;
                    break;
                case "Enter":
                case "Return":
                    result = (uint)VirtualKeys.Return;
                    break;
                case "Tab":
                    result = (uint)VirtualKeys.Tab;
                    break;
                case "F1":
                    result = (uint)VirtualKeys.F1;
                    break;
                case "F2":
                    result = (uint)VirtualKeys.F2;
                    break;
                case "F3":
                    result = (uint)VirtualKeys.F3;
                    break;
                case "F4":
                    result = (uint)VirtualKeys.F4;
                    break;
                case "F5":
                    result = (uint)VirtualKeys.F5;
                    break;
                case "F6":
                    result = (uint)VirtualKeys.F6;
                    break;
                case "F7":
                    result = (uint)VirtualKeys.F7;
                    break;
                case "F8":
                    result = (uint)VirtualKeys.F8;
                    break;
                case "F9":
                    result = (uint)VirtualKeys.F9;
                    break;
                case "F10":
                    result = (uint)VirtualKeys.F10;
                    break;
                case "F11":
                    result = (uint)VirtualKeys.F11;
                    break;
                case "F12":
                    result = (uint)VirtualKeys.F12;
                    break;
                case "Up":
                    result = (uint)VirtualKeys.Up;
                    break;
                case "Down":
                    result = (uint)VirtualKeys.Down;
                    break;
                case "Left":
                    result = (uint)VirtualKeys.Left;
                    break;
                case "Right":
                    result = (uint)VirtualKeys.Right;
                    break;
                case "Insert":
                    result = (uint)VirtualKeys.Insert;
                    break;
                case "Delete":
                    result = (uint)VirtualKeys.Delete;
                    break;
                case "Home":
                    result = (uint)VirtualKeys.Home;
                    break;
                case "End":
                    result = (uint)VirtualKeys.End;
                    break;
                case "PageUp":
                    result = (uint)VirtualKeys.Prior;
                    break;
                case "PageDown":
                    result = (uint)VirtualKeys.Next;
                    break;
                case "PrintScreen":
                    result = (uint)VirtualKeys.Print;
                    break;
                case "ScrollLock":
                    result = (uint)VirtualKeys.ScrollLock;
                    break;
                case "Pause":
                case "Break":
                    result = (uint)VirtualKeys.Pause;
                    break;
                case "VolumeUp":
                    result = (uint)VirtualKeys.VolumeUp;
                    break;
                case "VolumeDown":
                    result = (uint)VirtualKeys.VolumeDown;
                    break;
                case "VolumeMute":
                    result = (uint)VirtualKeys.VolumeMute;
                    break;
                case "MediaPlayPause":
                    result = (uint)VirtualKeys.MediaPlayPause;
                    break;
                case "Grave":
                case "Tilde":
                    result = (uint)VirtualKeys.OEM3;
                    break;
                default:
                    break;
            }
            return result;
        }

        //public static int ParseMouseButtonCodeString(string key)
        //{
        //    int result = 0;
        //    switch(key)
        //    {
        //        case "LeftButton":
        //        case "Left":
        //            result = MouseButtonCodes.MOUSE_LEFT_BUTTON;
        //            break;
        //        case "RightButton":
        //        case "Right":
        //            result = MouseButtonCodes.MOUSE_RIGHT_BUTTON;
        //            break;
        //        case "MiddleButton":
        //        case "Middle":
        //            result = MouseButtonCodes.MOUSE_MIDDLE_BUTTON;
        //            break;
        //        case "XButton1":
        //            result = MouseButtonCodes.MOUSE_XBUTTON1;
        //            break;
        //        case "XButton2":
        //            result = MouseButtonCodes.MOUSE_XBUTTON2;
        //            break;
        //        default:
        //            break;
        //    }
        //    return result;
        //}

        public enum MouseButtonAliases : int
        {
            None,
            LeftButton = MouseButtonCodes.MOUSE_LEFT_BUTTON,
            Left = LeftButton,
            RightButton = MouseButtonCodes.MOUSE_RIGHT_BUTTON,
            Right = RightButton,
            MiddleButton = MouseButtonCodes.MOUSE_MIDDLE_BUTTON,
            Middle = MiddleButton,
            XButton1 = MouseButtonCodes.MOUSE_XBUTTON1,
            XButton2 = MouseButtonCodes.MOUSE_XBUTTON2,
        }

        public enum MouseButtonOutputAliases : int
        {
            None,
            LeftButton = MouseButtonCodes.MOUSE_LEFT_BUTTON,
            RightButton = MouseButtonCodes.MOUSE_RIGHT_BUTTON,
            MiddleButton = MouseButtonCodes.MOUSE_MIDDLE_BUTTON,
            XButton1 = MouseButtonCodes.MOUSE_XBUTTON1,
            XButton2 = MouseButtonCodes.MOUSE_XBUTTON2,
        }

        public enum MouseWheelAliases : uint
        {
            None = 0,
            WheelUp,
            WheelDown,
            WheelLeft,
            WheelRight,
        }
    }

    public class OutputActionDataTypeConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(OutputActionDataSerializer);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            JObject j = JObject.Load(reader);

            string outputType = j["Type"]?.ToString();
            if (!Enum.TryParse(outputType, out ActionType checkType))
            {
                return null;
            }

            OutputActionDataSerializer resultInstance = null;
            OutputActionData tempInstance = new OutputActionData(ActionType.Empty, 0);
            switch (checkType)
            {
                case ActionType.Keyboard:
                    {
                        tempInstance.OutputType = checkType;
                        string tempKeyAlias = j["Code"]?.ToString() ?? string.Empty;
                        if (uint.TryParse(tempKeyAlias, out uint temp))
                        {
                            // String parses to a uint. Assume native code
                            tempInstance.OutputCode = (int)temp;
                            tempInstance.OutputCodeAlias = ProfileSerializer.EventInputMapper.GetRealEventKey(temp);
                            tempInstance.OutputCodeStr = tempKeyAlias;
                            //tempInstance.OutputCode = temp;
                        }
                        else
                        {
                            // Check mapping aliases
                            if (!string.IsNullOrEmpty(tempKeyAlias))
                            {
                                if (tempKeyAlias.StartsWith("0x") &&
                                    uint.TryParse(tempKeyAlias.Remove(0, 2), System.Globalization.NumberStyles.HexNumber, null, out temp))
                                {
                                    // alias is a hex number (copied from MS docs?)
                                    tempInstance.OutputCode = (int)temp;
                                    tempInstance.OutputCodeAlias = ProfileSerializer.EventInputMapper.GetRealEventKey(temp);
                                    tempInstance.OutputCodeStr = tempKeyAlias;
                                }
                                else
                                {
                                    // Check alias for known mapping
                                    temp = OutputActionDataSerializer.ParseKeyboardCodeString(tempKeyAlias);
                                    if (temp > 0)
                                    {
                                        uint realKey = ProfileSerializer.EventInputMapper.GetRealEventKey(temp);
                                        tempInstance.OutputCode = (int)temp;
                                        tempInstance.OutputCodeAlias = realKey;
                                        tempInstance.OutputCodeStr = tempKeyAlias;
                                    }
                                    else
                                    {
                                        tempInstance.OutputCode = 0;
                                        tempInstance.OutputCodeAlias = 0;
                                        tempInstance.OutputCodeStr = tempKeyAlias;
                                    }
                                }
                            }
                        }

                        DeserializeExtraJSONProperties(tempInstance, j);
                        resultInstance = new OutputActionDataSerializer(tempInstance);
                        break;
                    }
                case ActionType.RelativeMouse:
                    {
                        tempInstance.OutputType = checkType;
                        //if (int.TryParse(j["Code"]?.ToString(), out int temp))
                        //{
                        //    tempInstance.OutputCode = temp;
                        //    tempInstance.OutputCodeAlias = temp;
                        //}
                        if (Enum.TryParse(j["Dir"]?.ToString(), out RelativeMouseDir temp))
                        {
                            tempInstance.mouseDir = temp;
                            tempInstance.OutputCodeStr = j["Dir"]?.ToString();
                        }

                        DeserializeExtraJSONProperties(tempInstance, j);
                        resultInstance = new OutputActionDataSerializer(tempInstance);
                        break;
                    }
                case ActionType.MouseButton:
                    {
                        tempInstance.OutputType = checkType;
                        string tempAlias = j["Code"]?.ToString() ?? string.Empty;
                        if (int.TryParse(tempAlias, out int temp))
                        {
                            // String parses to a int. Assume native code
                            tempInstance.OutputCode = temp;
                            tempInstance.OutputCodeStr = temp.ToString();
                        }
                        else
                        {
                            // Check mapping aliases
                            if (Enum.TryParse(tempAlias,
                                out OutputActionDataSerializer.MouseButtonAliases mButtonAlias))
                            {
                                //temp = OutputActionDataSerializer.ParseMouseButtonCodeString(tempAlias);
                                temp = (int)mButtonAlias;
                                tempInstance.OutputCode = (int)temp;
                                tempInstance.OutputCodeStr = tempAlias;
                            }
                        }

                        DeserializeExtraJSONProperties(tempInstance, j);
                        resultInstance = new OutputActionDataSerializer(tempInstance);
                        break;
                    }
                case ActionType.MouseWheel:
                    tempInstance.OutputType = checkType;
                    //tempInstance.OutputCode = 128;
                    string tempWheelAlias = j["Code"]?.ToString() ?? string.Empty;
                    int wheelTemp = 0;
                    if (int.TryParse(tempWheelAlias, out wheelTemp))
                    {
                        tempInstance.OutputCode = wheelTemp;
                        tempInstance.OutputCodeStr = tempWheelAlias;
                    }
                    else if (Enum.TryParse(tempWheelAlias,
                        out OutputActionDataSerializer.MouseWheelAliases buttonAlias))
                    {
                        wheelTemp = (int)buttonAlias;
                        tempInstance.OutputCode = wheelTemp;
                        tempInstance.OutputCodeStr = tempWheelAlias;
                    }

                    DeserializeExtraJSONProperties(tempInstance, j);
                    resultInstance = new OutputActionDataSerializer(tempInstance);
                    break;
                case ActionType.GamepadControl:
                    {
                        tempInstance.OutputType = checkType;
                        if (Enum.TryParse(j["PadOutput"]?.ToString(), out JoypadActionCodes temp))
                        {
                            tempInstance.JoypadCode = temp;
                        }

                        DeserializeExtraJSONProperties(tempInstance, j);
                        resultInstance = new OutputActionDataSerializer(tempInstance);
                        break;
                    }
                case ActionType.SwitchSet:
                    tempInstance.OutputType = checkType;
                    if (int.TryParse(j["Set"]?.ToString(), out int setTemp))
                    {
                        tempInstance.ChangeToSet = setTemp;
                    }

                    DeserializeExtraJSONProperties(tempInstance, j);
                    resultInstance = new OutputActionDataSerializer(tempInstance);
                    break;
                case ActionType.SwitchActionLayer:
                    tempInstance.OutputType = checkType;
                    if (int.TryParse(j["Layer"]?.ToString(), out int layerTemp))
                    {
                        tempInstance.ChangeToLayer = layerTemp;
                    }

                    DeserializeExtraJSONProperties(tempInstance, j);
                    resultInstance = new OutputActionDataSerializer(tempInstance);
                    break;
                case ActionType.ApplyActionLayer:
                    tempInstance.OutputType = checkType;
                    if (int.TryParse(j["Layer"]?.ToString(), out int applyLayerNumTemp))
                    {
                        tempInstance.ChangeToLayer = applyLayerNumTemp;
                    }

                    DeserializeExtraJSONProperties(tempInstance, j);
                    resultInstance = new OutputActionDataSerializer(tempInstance);
                    break;
                case ActionType.RemoveActionLayer:
                    tempInstance.OutputType = checkType;
                    if (int.TryParse(j["Layer"]?.ToString(), out int removeLayerNumTemp))
                    {
                        tempInstance.FromProfileChangeLayer = removeLayerNumTemp;
                        tempInstance.ChangeToLayer = removeLayerNumTemp;
                    }

                    DeserializeExtraJSONProperties(tempInstance, j);
                    resultInstance = new OutputActionDataSerializer(tempInstance);
                    break;
                case ActionType.HoldActionLayer:
                    tempInstance.OutputType = checkType;
                    if (int.TryParse(j["Layer"]?.ToString(), out int holdLayerNumTemp))
                    {
                        tempInstance.ChangeToLayer = holdLayerNumTemp;
                    }

                    DeserializeExtraJSONProperties(tempInstance, j);
                    resultInstance = new OutputActionDataSerializer(tempInstance);
                    break;
                case ActionType.Wait:
                    tempInstance.OutputType = checkType;
                    if (int.TryParse(j["Period"]?.ToString(), out int periodTemp))
                    {
                        tempInstance.DurationMs = periodTemp;
                    }

                    DeserializeExtraJSONProperties(tempInstance, j);
                    tempInstance.ComputeActionFlags();
                    resultInstance = new OutputActionDataSerializer(tempInstance);
                    break;
                case ActionType.CycleStep:
                    string tempCycleOp = j["Op"]?.ToString() ?? string.Empty;
                    tempInstance.OutputType = checkType;
                    if (Enum.TryParse(tempCycleOp,
                        out CycleStepActionType cycleStepType))
                    {
                        tempInstance.cycleStepAct.stepActionType = cycleStepType;

                        string tempId = j["Cycle"]?.ToString() ?? string.Empty;
                        if (!string.IsNullOrWhiteSpace(tempId))
                        {
                            tempInstance.cycleStepAct.cycleId = tempId;

                            if (cycleStepType == CycleStepActionType.MoveToStep &&
                                int.TryParse(j["Step"]?.ToString() ?? string.Empty, out int tempStepNum))
                            {
                                tempInstance.cycleStepAct.stepNum = tempStepNum;
                            }
                        }
                    }

                    resultInstance = new OutputActionDataSerializer(tempInstance);
                    break;
                case ActionType.CameraTurn:
                    tempInstance.OutputType = checkType;
                    if (double.TryParse(j["Angle"]?.ToString(),
                        System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out double camAngle))
                        tempInstance.cameraTurnAngle = camAngle;
                    if (double.TryParse(j["Duration"]?.ToString(),
                        System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out double camDuration))
                        tempInstance.cameraTurnDurationMs = camDuration;
                    if (double.TryParse(j["Counts360"]?.ToString(),
                        System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out double camCounts360))
                        tempInstance.cameraTurnCounts360 = camCounts360;
                    resultInstance = new OutputActionDataSerializer(tempInstance);
                    break;
                case ActionType.Empty:
                    tempInstance.OutputType = ActionType.Empty;
                    resultInstance = new OutputActionDataSerializer(tempInstance);
                    break;
                default:
                    //throw new JsonException();
                    tempInstance.OutputType = ActionType.Empty;
                    resultInstance = new OutputActionDataSerializer(tempInstance);
                    break;
            }

            return resultInstance;
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            OutputActionDataSerializer current = value as OutputActionDataSerializer;

            JObject tempJ = new JObject();

            //writer.WriteStartObject();
            //writer.WritePropertyName("type");
            //writer.WriteValue(current.ActionType.ToString());

            tempJ.Add("Type", current.ActionType.ToString());
            switch (current.ActionType)
            {
                case ActionType.Keyboard:
                    if (!string.IsNullOrEmpty(current.OutputData.OutputCodeStr))
                    {
                        tempJ.Add("Code", current.OutputData.OutputCodeStr);
                    }
                    else
                    {
                        tempJ.Add("Code", current.OutputData.OutputCodeAlias);
                    }

                    SerializeExtraJSONProperties(current.OutputData, tempJ);
                    break;
                case ActionType.RelativeMouse:
                    tempJ.Add("Dir", current.OutputData.OutputCodeStr);
                    SerializeExtraJSONProperties(current.OutputData, tempJ);
                    break;
                case ActionType.MouseButton:
                    if (!string.IsNullOrEmpty(current.OutputData.OutputCodeStr))
                    {
                        tempJ.Add("Code", current.OutputData.OutputCodeStr);
                    }
                    else
                    {
                        tempJ.Add("Code", current.OutputData.OutputCode);
                    }

                    SerializeExtraJSONProperties(current.OutputData, tempJ);
                    break;
                case ActionType.MouseWheel:
                    if (!string.IsNullOrEmpty(current.OutputData.OutputCodeStr))
                    {
                        tempJ.Add("Code", current.OutputData.OutputCodeStr);
                    }
                    else
                    {
                        tempJ.Add("Code", current.OutputData.OutputCode);
                    }

                    SerializeExtraJSONProperties(current.OutputData, tempJ);
                    break;
                case ActionType.GamepadControl:
                    //tempJ.Add("PadOutput", current.OutputData.JoypadCode.ToString());
                    tempJ.Add("PadOutput", OutputJoypadActionCodeHelper.Convert(current.OutputData.JoypadCode));
                    SerializeExtraJSONProperties(current.OutputData, tempJ);
                    break;
                case ActionType.SwitchSet:
                    tempJ.Add("Set", current.OutputData.ChangeToSet);
                    SerializeExtraJSONProperties(current.OutputData, tempJ);
                    break;
                case ActionType.SwitchActionLayer:
                    tempJ.Add("Layer", current.OutputData.ChangeToLayer);
                    //SerializeExtraJSONProperties(current.OutputData, tempJ);
                    break;
                case ActionType.ApplyActionLayer:
                    tempJ.Add("Layer", current.OutputData.ChangeToLayer);
                    SerializeExtraJSONProperties(current.OutputData, tempJ);
                    break;
                case ActionType.RemoveActionLayer:
                    if (current.OutputData.FromProfileChangeLayer != -1)
                    {
                        tempJ.Add("Layer", current.OutputData.FromProfileChangeLayer);
                    }

                    SerializeExtraJSONProperties(current.OutputData, tempJ);
                    break;
                case ActionType.HoldActionLayer:
                    tempJ.Add("Layer", current.OutputData.ChangeToLayer);
                    SerializeExtraJSONProperties(current.OutputData, tempJ);
                    break;
                case ActionType.Wait:
                    tempJ.Add("Period", current.OutputData.DurationMs);
                    SerializeExtraJSONProperties(current.OutputData, tempJ);
                    break;
                case ActionType.CycleStep:
                    {
                        CycleStepAction tempStepAct = current.OutputData.cycleStepAct;
                        tempJ.Add("Cycle", tempStepAct.cycleId);
                        tempJ.Add("Op", tempStepAct.stepActionType.ToString());
                        if (tempStepAct.stepActionType == CycleStepActionType.MoveToStep)
                        {
                            tempJ.Add("Step", tempStepAct.stepNum);
                        }

                        SerializeExtraJSONProperties(current.OutputData, tempJ);
                    }

                    break;
                case ActionType.CameraTurn:
                    tempJ.Add("Angle", current.OutputData.cameraTurnAngle);
                    tempJ.Add("Duration", current.OutputData.cameraTurnDurationMs);
                    break;
                default:
                    break;
            }

            //serializer.Serialize(new JTokenWriter(tempJ), value);
            serializer.Serialize(writer,tempJ);

            //writer.WriteEndObject();
            //JObject j = JObject.FromObject(value);
            //j.WriteTo(writer);
            //serializer.Serialize(writer, value);
        }

        private void DeserializeExtraJSONProperties(OutputActionData actionData, JObject jsonObject)
        {
            switch (actionData.OutputType)
            {
                case ActionType.Keyboard:
                    break;
                case ActionType.RelativeMouse:
                    {
                        if (int.TryParse(jsonObject["Settings"]?["MouseXSpeed"]?.ToString(), out int tempX) &&
                            tempX > 0)
                        {
                            actionData.extraSettings.mouseXSpeed = tempX;
                        }

                        if (int.TryParse(jsonObject["Settings"]?["MouseYSpeed"]?.ToString(), out int tempY) &&
                            tempY > 0)
                        {
                            actionData.extraSettings.mouseYSpeed = tempY;
                        }
                    }

                    break;
                case ActionType.MouseWheel:
                    if (int.TryParse(jsonObject["Settings"]?["TickTime"]?.ToString(), out int temp) &&
                        temp > 0)
                    {
                        actionData.CheckTick = true;
                        actionData.DurationMs = temp;
                    }

                    break;
                case ActionType.GamepadControl:
                    if (bool.TryParse(jsonObject["Settings"]?["Negative"]?.ToString(), out bool tempNeg) &&
                        tempNeg)
                    {
                        actionData.Negative = tempNeg;
                    }

                    break;
                case ActionType.SwitchSet:
                    if (Enum.TryParse(jsonObject["Settings"]?["ChangeCondition"]?.ToString(), out SetChangeCondition tempCond))
                    {
                        actionData.ChangeCondition = tempCond;
                    }

                    break;
                case ActionType.SwitchActionLayer:
                    if (Enum.TryParse(jsonObject["Settings"]?["ChangeCondition"]?.ToString(), out ActionLayerChangeCondition tempSwitchLayerCond))
                    {
                        actionData.LayerChangeCondition = tempSwitchLayerCond;
                    }

                    break;
                case ActionType.ApplyActionLayer:
                    if (Enum.TryParse(jsonObject["Settings"]?["ChangeCondition"]?.ToString(), out ActionLayerChangeCondition applyLayerCond))
                    {
                        actionData.LayerChangeCondition = applyLayerCond;
                    }

                    break;
                case ActionType.RemoveActionLayer:
                    if (Enum.TryParse(jsonObject["Settings"]?["ChangeCondition"]?.ToString(), out ActionLayerChangeCondition removeLayerCond))
                    {
                        actionData.LayerChangeCondition = removeLayerCond;
                    }

                    break;
                case ActionType.HoldActionLayer:
                    //if (Enum.TryParse(jsonObject["Settings"]?["ChangeCondition"]?.ToString(), out ActionLayerChangeCondition removeLayerCond))
                    //{
                    //    actionData.LayerChangeCondition = removeLayerCond;
                    //}

                    break;
                default:
                    break;
            }
        }

        private void SerializeExtraJSONProperties(OutputActionData actionData, JObject jsonObject)
        {
            switch (actionData.OutputType)
            {
                case ActionType.Keyboard:
                    break;
                case ActionType.RelativeMouse:
                    {
                        JObject settingsDirJ = new JObject();
                        if (actionData.extraSettings.mouseXSpeed > 0)
                        {
                            settingsDirJ.Add("MouseXSpeed",
                                actionData.extraSettings.mouseXSpeed);
                        }

                        if (actionData.extraSettings.mouseYSpeed > 0)
                        {
                            settingsDirJ.Add("MouseYSpeed",
                                actionData.extraSettings.mouseYSpeed);
                        }

                        if (settingsDirJ.Count > 0)
                        {
                            jsonObject.Add("Settings", settingsDirJ);
                        }
                    }

                    break;
                case ActionType.MouseWheel:
                    JObject settingsJ = new JObject();
                    if (actionData.checkTick && actionData.DurationMs != 0)
                    {
                        settingsJ.Add("TickTime", actionData.DurationMs);
                    }

                    if (settingsJ.Count > 0)
                    {
                        jsonObject.Add("Settings", settingsJ);
                    }

                    break;
                case ActionType.GamepadControl:
                    JObject settingsPadControlJ = new JObject();
                    if (actionData.Negative)
                    {
                        settingsPadControlJ.Add("Negative", actionData.Negative);
                    }

                    if (settingsPadControlJ.Count > 0)
                    {
                        jsonObject.Add("Settings", settingsPadControlJ);
                    }

                    break;
                case ActionType.SwitchSet:
                    JObject settingsSetJ = new JObject();
                    if (actionData.ChangeCondition != SetChangeCondition.None)
                    {
                        settingsSetJ.Add("ChangeCondition", actionData.ChangeCondition.ToString());
                    }

                    if (settingsSetJ.Count > 0)
                    {
                        jsonObject.Add("Settings", settingsSetJ);
                    }

                    break;
                case ActionType.ApplyActionLayer:
                case ActionType.RemoveActionLayer:
                case ActionType.SwitchActionLayer:
                    {
                        JObject settingsLayerJ = new JObject();
                        if (actionData.LayerChangeCondition != ActionLayerChangeCondition.None)
                        {
                            settingsLayerJ.Add("ChangeCondition", actionData.LayerChangeCondition.ToString());
                        }

                        if (settingsLayerJ.Count > 0)
                        {
                            jsonObject.Add("Settings", settingsLayerJ);
                        }
                    }

                    break;
                default:
                    break;
            }
        }
    }

    public class TriggerButtonsConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(JoypadActionCodes[]);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            List<JoypadActionCodes> tempList = new List<JoypadActionCodes>();

            if (reader.TokenType == JsonToken.String)
            {
                tempList.Add(Enum.Parse<JoypadActionCodes>(reader.Value.ToString()));
            }
            else if (reader.TokenType == JsonToken.StartArray)
            {
                JArray array = JArray.Load(reader);
                JsonConvert.PopulateObject(array.ToString(), tempList);
            }

            return tempList.ToArray();
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            JoypadActionCodes[] joypadActionCodes = (JoypadActionCodes[])value;
            if (joypadActionCodes.Length == 1)
            {
                JoypadActionCodes tempCode = joypadActionCodes[0];
                string tempStr = tempCode == JoypadActionCodes.AlwaysOn ? OutputJoypadActionCodeHelper.ALWAYS_ON_TEXT :
                    OutputJoypadActionCodeHelper.Convert(tempCode);

                serializer.Serialize(writer, tempStr);
            }
            else
            {
                List<string> tempList = new List<string>();
                foreach(JoypadActionCodes code in joypadActionCodes)
                {
                    if (code == JoypadActionCodes.AlwaysOn)
                    {
                        tempList.Add(OutputJoypadActionCodeHelper.ALWAYS_ON_TEXT);
                    }
                    else
                    {
                        tempList.Add(OutputJoypadActionCodeHelper.Convert(code));
                    }
                }

                serializer.Serialize(writer, tempList);
                //serializer.Serialize(writer, value);
            }
        }
    }

    public class ActionFuncsListConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(List<ActionFuncSerializer>);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            JArray array = JArray.Load(reader);
            List<ActionFuncSerializer> funcsList = new List<ActionFuncSerializer>();
            foreach(JToken token in array.Children())
            {
                if (token.Type == JTokenType.Object)
                {
                    ActionFuncSerializer temp = token.ToObject<ActionFuncSerializer>();
                    if (temp != null)
                    {
                        funcsList.Add(temp);
                    }
                }
            }

            return funcsList;
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            serializer.Serialize(writer, value);
        }
    }

    public class SafeStringEnumConverter : StringEnumConverter
    {
        public Enum DefaultValue { get; }

        public SafeStringEnumConverter(Enum defaultValue)
        {
            DefaultValue = defaultValue;
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            try
            {
                return base.ReadJson(reader, objectType, existingValue, serializer);
            }
            catch
            {
                return DefaultValue;
            }
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            base.WriteJson(writer, value, serializer);
        }
    }
}
