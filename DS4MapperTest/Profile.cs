using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using DS4MapperTest.ButtonActions;
using DS4MapperTest.Common;
using DS4MapperTest.GyroActions;
using static DS4MapperTest.Mapper;
using DS4MapperTest.DS4Library;

namespace DS4MapperTest
{
    public enum CalibMode
    {
        RwcMode,
        CountsMode,
    }

    public class Profile
    {
        protected List<ActionSet> actionSets = new List<ActionSet>(8);
        public List<ActionSet> ActionSets
        {
            get => actionSets; set => actionSets = value;
        }

        private ActionSet currentActionSet;
        public ActionSet CurrentActionSet { get => currentActionSet; }

        private ActionSet defaultActionSet;
        public ActionSet DefaultActionSet { get => defaultActionSet; }

        private int currentActionSetIndex = 0;
        public int CurrentActionSetIndex
        {
            get => currentActionSetIndex;
        }

        protected Dictionary<string, CycleButton> cycleBindings =
            new Dictionary<string, CycleButton>();
        public Dictionary<string, CycleButton> CycleBindings => cycleBindings;

        protected string name;
        public string Name { get => name; set => name = value; }

        protected string description;
        public string Description { get => description; set => description = value; }

        protected DateTime creationDate;
        public DateTime CreationDate { get => creationDate; set => creationDate = value; }

        protected string controllerType;
        public string ControllerType { get => controllerType; set => controllerType = value; }

        //protected int leftStickRotation;
        //public int LeftStickRotation { get => leftStickRotation; set => leftStickRotation = value; }

        //protected int rightStickRotation;
        //public int RightStickRotation { get => rightStickRotation; set => rightStickRotation = value; }

        //protected int leftTouchpadRotation;
        //public int LeftTouchpadRotation { get => leftTouchpadRotation; set => leftTouchpadRotation = value; }

        //protected int rightTouchpadRotation;
        //public int RightTouchpadRotation { get => rightTouchpadRotation; set => rightTouchpadRotation = value; }

        private EmulatedControllerSettings outputGamepadSettings = new EmulatedControllerSettings();
        public EmulatedControllerSettings OutputGamepadSettings
        {
            get => outputGamepadSettings;
            set => outputGamepadSettings = value;
        }

        private LightbarSettings lightbarSettings = new LightbarSettings();
        public LightbarSettings LightbarSettings
        {
            get => lightbarSettings;
            set => lightbarSettings = value;
        }

        // Default calibration matches the VALORANT preset at an In-Game
        // Sensitivity of 1.0, so a brand-new profile shows that preset selected
        // with no further setup needed (see GameCalibPreset.Valorant).
        private double calibRwc = GameCalibPreset.Valorant.RWC;
        public double CalibRwc
        {
            get => calibRwc;
            set
            {
                if (calibRwc == value) return;
                calibRwc = value;
                CalibRwcChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        public event EventHandler CalibRwcChanged;

        private double calibInGameSens = 1.0;
        public double CalibInGameSens
        {
            get => calibInGameSens;
            set
            {
                if (calibInGameSens == value) return;
                calibInGameSens = value;
                CalibInGameSensChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        public event EventHandler CalibInGameSensChanged;

        private double calibCounts = GameCalibPreset.Valorant.RWC * 360.0;
        public double CalibCounts
        {
            get => calibCounts;
            set
            {
                if (calibCounts == value) return;
                calibCounts = value;
                CalibCountsChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        public event EventHandler CalibCountsChanged;

        private CalibMode calibMode = CalibMode.RwcMode;
        public CalibMode CalibMode
        {
            get => calibMode;
            set
            {
                if (calibMode == value) return;
                calibMode = value;
                CalibModeChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        public event EventHandler CalibModeChanged;

        public bool dirty;
        public bool Dirty
        {
            get => dirty;
            set
            {
                dirty = value;
                DirtyChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        public event EventHandler DirtyChanged;

        public Profile()
        {
            // Add one empty ActionSet by default
            ActionSet actionSet = new ActionSet(0, "Set 1");
            actionSets.Add(actionSet);

            currentActionSet = actionSet;
            defaultActionSet = currentActionSet;
        }

        public void ResetAliases()
        {
            currentActionSet = null;
            currentActionSetIndex = 0;
            defaultActionSet = null;
            if (actionSets.Count > 0)
            {
                currentActionSet = actionSets[0];
                defaultActionSet = currentActionSet;

                foreach(ActionSet set in actionSets)
                {
                    set.ResetAliases();
                }
            }
        }

        public void SwitchSets(int index, Mapper mapper)
        {
            if (index >= 0 && index < actionSets.Count)
            {
                currentActionSet.ReleaseActions(mapper);
                currentActionSet = actionSets[index];
                currentActionSetIndex = index;
            }
        }
    }

    public class EmulatedControllerSettings
    {
        public enum OutputControllerTypeTest : ushort
        {
            Xbox360,
            //DS4,
        }

        // Default to making a virtual X360 controller after loading a profile
        [JsonIgnore]
        public bool enabled = true;
        [JsonIgnore]
        public OutputContType outputGamepad = OutputContType.Xbox360;
        [JsonIgnore]
        public bool forceFeedbackEnabled = false;

        public bool Enabled
        {
            get => enabled;
            set => enabled = value;
        }

        [JsonConverter(typeof(StringEnumConverter))]
        public OutputContType OutputGamepad
        {
            get => outputGamepad;
            set
            {
                if (outputGamepad == value) return;
                outputGamepad = value;
                OutputGamepadChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        public event EventHandler OutputGamepadChanged;

        public bool ShouldSerializeOutputGamepad()
        {
            return enabled;
        }

        public bool ForceFeedbackEnabled
        {
            get => forceFeedbackEnabled;
            set => forceFeedbackEnabled = value;
        }
        public bool ShouldSerializeForceFeedbackEnabled()
        {
            return enabled;
        }
    }

    public class InputBindingMeta
    {
        public enum InputControlType : uint
        {
            None,
            Button,
            Axis,
            Stick,
            DPad,
            Trigger,
            Touchpad,
            TouchpadRegion,
            Gyro
        }

        public InputControlType controlType;
        public string id;
        public string displayName;

        public InputBindingMeta(string id, string displayName, InputControlType type)
        {
            this.id = id;
            this.displayName = displayName;
            this.controlType = type;
        }
    }
}
