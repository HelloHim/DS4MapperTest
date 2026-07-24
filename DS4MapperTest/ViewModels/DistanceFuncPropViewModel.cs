using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DS4MapperTest.ActionUtil;
using DS4MapperTest.ButtonActions;

namespace DS4MapperTest.ViewModels
{
    public class DistanceFuncPropViewModel
    {
        private Mapper mapper;
        private ButtonAction action;
        private DistanceFunc func;

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

        public double Distance
        {
            get => func.distance;
            set
            {
                if (double.IsNaN(value)) return;
                double clampedValue = Math.Clamp(value, 0.0, 1.0);
                if (func.distance == clampedValue) return;
                func.distance = clampedValue;
                DistanceChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        public event EventHandler DistanceChanged;

        public DistanceFuncPropViewModel(Mapper mapper, ButtonAction action,
            DistanceFunc func)
        {
            this.mapper = mapper;
            this.action = action;
            this.func = func;
        }
    }
}
