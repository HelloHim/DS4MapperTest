using DS4MapperTest.ActionUtil;
using DS4MapperTest.ButtonActions;

namespace DS4MapperTest.ViewModels
{
    public class ReleaseFuncPropViewModel
    {
        private Mapper mapper;
        private ButtonAction action;
        private ReleaseFunc func;

        public string Name
        {
            get => func.Name;
            set
            {
                func.Name = value;
            }
        }

        public string DisplayBind
        {
            get
            {
                string result = "";
                result = func.DescribeOutputActions(mapper);
                return result;
            }
        }

        public int DelayMs
        {
            get => func.DelayDurationMs;
            set
            {
                func.DelayDurationMs = value;
            }
        }

        public bool ToggleEnabled
        {
            get => func.toggleEnabled;
            set
            {
                func.toggleEnabled = value;
            }
        }

        public bool MaxHoldTimeEnabled
        {
            get => func.MaxHoldTimeEnabled;
            set
            {
                func.MaxHoldTimeEnabled = value;
            }
        }

        public int MaxHoldTimeMs
        {
            get => func.MaxHoldTimeMs;
            set
            {
                func.MaxHoldTimeMs = value;
            }
        }

        public ReleaseFuncPropViewModel(Mapper mapper, ButtonAction action,
            ReleaseFunc func)
        {
            this.mapper = mapper;
            this.action = action;
            this.func = func;
        }
    }
}
