using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DS4MapperTest.ViewModels.Common;
using DS4MapperTest.GyroActions;
using DS4MapperTest.MapperUtil;

namespace DS4MapperTest.ViewModels.GyroActionPropViewModels
{
    public enum InvertChocies
    {
        None,
        InvertX,
        InvertY,
        InvertXY,
    }

    public class InvertChoiceItem
    {
        private string displayName;
        public string DisplayName => displayName;

        private InvertChocies choice;
        public InvertChocies Choice => choice;

        public InvertChoiceItem(string displayName, InvertChocies choice)
        {
            this.displayName = displayName;
            this.choice = choice;
        }
    }

    public enum SmoothPresetChoices
    {
        None,
        Stiff,
        Normie,
        Loose,
    }

    public enum GyroActivationModeChoice
    {
        AlwaysOn,
        HoldToEnable,
        HoldToDisable,
    }

    public class SmoothPresetChoiceItem
    {
        private string displayName;
        public string DisplayName => displayName;

        private SmoothPresetChoices choice;
        public SmoothPresetChoices Choice => choice;

        private double minCutoffValue = 1.0;
        public double MinCutoffValue => minCutoffValue;

        private double betaValue = 1.0;
        public double BetaValue => betaValue;

        public SmoothPresetChoiceItem(string displayName, SmoothPresetChoices choice,
            double minCutoff, double beta)
        {
            this.displayName = displayName;
            this.choice = choice;
            this.minCutoffValue = minCutoff;
            this.betaValue = beta;
        }
    }

    public class AccelCurveChoiceItem
    {
        private string displayName;
        public string DisplayName => displayName;

        private GyroMouseAccelCurveChoice choice;
        public GyroMouseAccelCurveChoice Choice => choice;

        public AccelCurveChoiceItem(string displayName, GyroMouseAccelCurveChoice choice)
        {
            this.displayName = displayName;
            this.choice = choice;
        }
    }

    public class GyroActionPropVMBase
    {
        protected const string DEFAULT_EMPTY_TRIGGER_STR = "None";

        protected Mapper mapper;
        public Mapper Mapper
        {
            get => mapper;
        }

        protected GyroMapAction baseAction;
        public GyroMapAction BaseAction
        {
            get => baseAction;
        }

        public string Name
        {
            get => baseAction.Name;
            set
            {
                if (baseAction.Name == value) return;
                baseAction.Name = value;
                NameChanged?.Invoke(this, EventArgs.Empty);
                ActionPropertyChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        public event EventHandler NameChanged;

        protected List<EnumChoiceSelection<GyroActivationModeChoice>> gyroActivationModeItems =
            new List<EnumChoiceSelection<GyroActivationModeChoice>>()
        {
            new EnumChoiceSelection<GyroActivationModeChoice>("Always On", GyroActivationModeChoice.AlwaysOn),
            new EnumChoiceSelection<GyroActivationModeChoice>("Hold to Enable", GyroActivationModeChoice.HoldToEnable),
            new EnumChoiceSelection<GyroActivationModeChoice>("Hold to Disable", GyroActivationModeChoice.HoldToDisable),
        };

        public List<EnumChoiceSelection<GyroActivationModeChoice>> GyroActivationModeItems => gyroActivationModeItems;

        protected List<EnumChoiceSelection<bool>> gyroTriggerCondItems =
            new List<EnumChoiceSelection<bool>>()
        {
            new EnumChoiceSelection<bool>("Any selected", false),
            new EnumChoiceSelection<bool>("All selected", true),
        };

        public List<EnumChoiceSelection<bool>> GyroTriggerCondItems => gyroTriggerCondItems;

        protected static GyroActivationModeChoice GetGyroActivationMode(
            IEnumerable<JoypadActionCodes> gyroTriggerButtons, bool triggerActivates)
        {
            return gyroTriggerButtons.Contains(JoypadActionCodes.AlwaysOn) && triggerActivates
                ? GyroActivationModeChoice.AlwaysOn
                : triggerActivates
                    ? GyroActivationModeChoice.HoldToEnable
                    : GyroActivationModeChoice.HoldToDisable;
        }

        protected static void SetTriggerItemEnabled(
            IEnumerable<GyroTriggerButtonItem> triggerItems,
            JoypadActionCodes code,
            bool enabled)
        {
            GyroTriggerButtonItem item = triggerItems.FirstOrDefault((candidate) => candidate.Code == code);
            if (item != null && item.Enabled != enabled)
            {
                item.Enabled = enabled;
            }
        }

        public virtual event EventHandler ActionPropertyChanged;
        public event EventHandler<GyroMapAction> ActionChanged;

        protected bool usingRealAction = true;

        protected void ReplaceExistingLayerAction(object sender, EventArgs e)
        {
            if (!usingRealAction)
            {
                mapper.ProcessMappingChangeAction(() =>
                {
                    this.baseAction.ParentAction.Release(mapper, ignoreReleaseActions: true);

                    mapper.EditLayer.AddGyroAction(this.baseAction);
                    if (mapper.EditActionSet.UsingCompositeLayer)
                    {
                        mapper.EditActionSet.RecompileCompositeLayer(mapper);
                    }
                    else
                    {
                        mapper.EditLayer.SyncActions();
                    }
                });

                usingRealAction = true;

                ActionChanged?.Invoke(this, baseAction);
            }
        }

        protected void ExecuteInMapperThread(Action tempAction)
        {
            mapper.ProcessMappingChangeAction(() =>
            {
                tempAction?.Invoke();
            });
        }
    }
}
