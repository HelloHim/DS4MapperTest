using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using DS4MapperTest.ButtonActions;
using DS4MapperTest.MapperUtil;
using DS4MapperTest.StickActions;

namespace DS4MapperTest.ViewModels
{
    public class StickBindEditViewModel
    {
        private Mapper mapper;
        public Mapper Mapper
        {
            get => mapper;
        }

        private StickMapAction action;
        public StickMapAction Action
        {
            get => action;
        }

        private UserControl displayControl;
        public UserControl DisplayControl
        {
            get => displayControl;
            set
            {
                displayControl = value;
                DisplayControlChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        public event EventHandler DisplayControlChanged;

        public string InputControlName
        {
            get
            {
                string result = "";
                if (mapper.BindingDict.TryGetValue(action.MappingId,
                    out InputBindingMeta tempMeta))
                {
                    result = tempMeta.displayName;
                }

                return result;
            }
        }

        public StickBindEditViewModel(Mapper mapper, StickMapAction action)
        {
            this.mapper = mapper;
            this.action = action;
        }

        public StickMapAction PrepareNewAction(int ind)
        {
            StickMapAction result = null;
            switch (ind)
            {
                case 0:
                    result = new StickNoAction();
                    break;
                case 1:
                    {
                        StickTranslate tempAction = new StickTranslate();
                        var joyDefaults = mapper.DeviceActionDefaults.GrabStickTranslateActionDefaults();
                        joyDefaults.Process(tempAction);
                        result = tempAction;
                    }

                    break;
                case 2:
                    {
                        StickPadAction tempAction = new StickPadAction();
                        var joyDefaults = mapper.DeviceActionDefaults.GrabStickPadActionActionDefaults();
                        joyDefaults.Process(tempAction);

                        AxisDirButton[] wasd = CreateWasdButtons(mapper);
                        tempAction.EventCodes4[(int)StickPadAction.DpadDirections.Up] = wasd[0];
                        tempAction.EventCodes4[(int)StickPadAction.DpadDirections.Down] = wasd[1];
                        tempAction.EventCodes4[(int)StickPadAction.DpadDirections.Left] = wasd[2];
                        tempAction.EventCodes4[(int)StickPadAction.DpadDirections.Right] = wasd[3];

                        result = tempAction;
                    }

                    break;
                case 3:
                    {
                        StickMouse tempAction = new StickMouse();
                        var joyDefaults = mapper.DeviceActionDefaults.GrabStickMouseActionDefaults();
                        joyDefaults.Process(tempAction);
                        result = tempAction;
                    }

                    break;
                case 4:
                    {
                        StickCircular tempAction = new StickCircular();
                        var joyDefaults = mapper.DeviceActionDefaults.GrabStickCircularActionDefaults();
                        joyDefaults.Process(tempAction);
                        result = tempAction;
                    }

                    break;
                case 5:
                    {
                        StickAbsMouse tempAction = new StickAbsMouse();
                        //var joyDefaults = mapper.DeviceActionDefaults.GrabTouchActionPadDefaults();
                        //joyDefaults.Process(tempAction);
                        result = tempAction;
                    }

                    break;
                case 6:
                    {
                        StickFlickStick tempAction = new StickFlickStick();
                        // Flick stick uses profile-wide calibration at runtime. Keep a
                        // newly selected stick action in step with gyro, touchpad flick
                        // stick, and camera-turn calibration from its first frame.
                        tempAction.RealWorldCalibration = mapper.ActionProfile.CalibRwc;
                        tempAction.InGameSens = mapper.ActionProfile.CalibInGameSens;
                        result = tempAction;
                    }

                    break;
                case 7:
                    {
                        StickAnalogEmulationAction tempAction = new StickAnalogEmulationAction();

                        AxisDirButton[] wasd = CreateWasdButtons(mapper);
                        tempAction.DirButtons[(int)StickAnalogEmulationAction.DirSlot.Up] = wasd[0];
                        tempAction.DirButtons[(int)StickAnalogEmulationAction.DirSlot.Down] = wasd[1];
                        tempAction.DirButtons[(int)StickAnalogEmulationAction.DirSlot.Left] = wasd[2];
                        tempAction.DirButtons[(int)StickAnalogEmulationAction.DirSlot.Right] = wasd[3];

                        result = tempAction;
                    }

                    break;
                case 8:
                    {
                        StickHybridAim tempAction = new StickHybridAim();
                        result = tempAction;
                    }

                    break;
                default:
                    break;
            }

            return result;
        }

        // Default Up/Down/Left/Right binding for any stick mode using the four cardinal
        // directional slots (DPad, Analog Emulation). Returns [Up, Down, Left, Right].
        private static AxisDirButton[] CreateWasdButtons(Mapper mapper)
        {
            return new AxisDirButton[]
            {
                CreateKeyButton(mapper, VirtualKeys.W),
                CreateKeyButton(mapper, VirtualKeys.S),
                CreateKeyButton(mapper, VirtualKeys.A),
                CreateKeyButton(mapper, VirtualKeys.D),
            };
        }

        private static AxisDirButton CreateKeyButton(Mapper mapper, VirtualKeys key)
        {
            OutputActionData data = new OutputActionData(OutputActionData.ActionType.Keyboard,
                (int)key, (int)mapper.EventInputMapping.GetRealEventKey((uint)key));
            data.OutputCodeStr = OutputDataAliasUtil.KeyboardStringAliasDict[key];
            return new AxisDirButton(data);
        }

        public void SwitchAction(StickMapAction action)
        {
            StickMapAction oldAction = this.action;
            StickMapAction newAction = action;

            mapper.ProcessMappingChangeAction(() =>
            {
                oldAction.Release(mapper, ignoreReleaseActions: true);
                //int tempInd = mapper.ActionProfile.CurrentActionSet.CurrentActionLayer.LayerActions.FindIndex((item) => item == tempAction);
                //if (tempInd >= 0)
                {
                    //mapper.ActionProfile.CurrentActionSet.CurrentActionLayer.LayerActions.RemoveAt(tempInd);
                    //mapper.ActionProfile.CurrentActionSet.CurrentActionLayer.LayerActions.Insert(tempInd, newAction);

                    //oldAction.Release(mapper, ignoreReleaseActions: true);

                    //mapper.ActionProfile.CurrentActionSet.RecentAppliedLayer.AddTouchpadAction(this.action);
                    if (oldAction.Id != MapAction.DEFAULT_UNBOUND_ID)
                    {
                        mapper.ActionProfile.CurrentActionSet.RecentAppliedLayer.ReplaceStickAction(oldAction, newAction);
                    }
                    else
                    {
                        mapper.ActionProfile.CurrentActionSet.RecentAppliedLayer.AddStickAction(newAction);
                    }

                    if (mapper.ActionProfile.CurrentActionSet.UsingCompositeLayer)
                    {
                        MapAction baseLayerAction = mapper.ActionProfile.CurrentActionSet.DefaultActionLayer.normalActionDict[oldAction.MappingId];
                        if (MapAction.IsSameType(baseLayerAction, newAction))
                        {
                            newAction.SoftCopyFromParent(baseLayerAction as StickMapAction);
                        }

                        mapper.ActionProfile.CurrentActionSet.RecompileCompositeLayer(mapper);
                    }
                    else
                    {
                        mapper.ActionProfile.CurrentActionSet.DefaultActionLayer.SyncActions();
                        mapper.ActionProfile.CurrentActionSet.ClearCompositeLayerActions();
                        mapper.ActionProfile.CurrentActionSet.PrepareCompositeLayer();
                    }
                }
            });

            this.action = action;
        }

        public void MigrateActionId(StickMapAction newAction)
        {
            if (action.Id == MapAction.DEFAULT_UNBOUND_ID)
            {
                // Need to create new ID for action
                newAction.Id = mapper.EditLayer.FindNextAvailableId();
            }
            else
            {
                // Can re-use existing ID
                newAction.Id = action.Id;
            }
        }

        public void UpdateAction(StickMapAction newAction)
        {
            this.action = newAction;
        }
    }
}
