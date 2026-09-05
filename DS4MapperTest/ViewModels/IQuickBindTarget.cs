using System;
using System.ComponentModel;
using DS4MapperTest.ActionUtil;
using DS4MapperTest.MapperUtil;

namespace DS4MapperTest.ViewModels
{
    /// <summary>
    /// Implemented by the per-press-type row view models (FaceButtonFuncItem,
    /// DPadDirectionFuncItem, TriggerButtonFuncItem, StickExtraFuncItem) so the
    /// reusable QuickBindControl can capture/replace/clear a binding without
    /// each Keybinds tab reimplementing the same logic.
    /// </summary>
    public interface IQuickBindTarget
    {
        Mapper Mapper { get; }

        // e.g. "Cross", "D-Pad Up", "Left Trigger"
        string RowLabel { get; }

        // e.g. "Normal Press", "Hold Press"
        string SlotLabel { get; }

        // Current binding text as already surfaced by the advanced editor's
        string DisplayBind { get; }

        // True when the current slot holds something Quick Bind cannot safely
        // replace/clear without confirmation (macro, multiple outputs, gamepad
        // control, layer/set switch, etc).
        bool IsComplexBinding { get; }

        // Ensures the owning action is a local, editable copy (creating a layer
        // override the same way the advanced editor already does) and returns
        // the context needed to mutate the specific ActionFunc slot. Used for
        // both capture replacement and clearing (via QuickBindActionApplier),
        // and for opening the advanced editor on the correct slot.
        EditFaceBindingContext GetEditContext();

        // Refreshes the owning row/control so bound UI (DisplayBind, tooltips,
        // etc) reflects the change just applied.
        void NotifyBindingChanged();
    }

    public interface IQuickBindRemovableTarget
    {
        bool CanRemoveTarget { get; }
        void RemoveTarget();
    }

    public interface IActionOutputListOwner
    {
        Mapper Mapper { get; }
        string RowLabel { get; }
        string SlotLabel { get; }
        ActionFunc Func { get; }
        EditFaceBindingContext PrepareEdit(ActionOutputItem item);
        void AddOutputAction();
        void RemoveOutputAction(ActionOutputItem item);
        void NotifyBindingChanged();
    }

    public class ActionOutputItem : INotifyPropertyChanged, IQuickBindTarget,
        IQuickBindRemovableTarget
    {
        private readonly IActionOutputListOwner owner;

        public event PropertyChangedEventHandler PropertyChanged;

        public IActionOutputListOwner Owner => owner;
        public int Index { get; }
        public bool CanRemove => Index > 0;
        public bool ShowAddSubCommand =>
            Index == Math.Max(1, owner.Func?.OutputActions.Count ?? 0) - 1;

        public string DisplayBind
        {
            get
            {
                OutputActionData data = CurrentOutput;
                string result = data?.Describe(owner.Mapper);
                return string.IsNullOrWhiteSpace(result) ? "Unbound" : result;
            }
        }

        public ActionOutputItem(IActionOutputListOwner owner, int index)
        {
            this.owner = owner;
            Index = index;
        }

        Mapper IQuickBindTarget.Mapper => owner.Mapper;
        string IQuickBindTarget.RowLabel => owner.RowLabel;
        string IQuickBindTarget.SlotLabel => $"{owner.SlotLabel} Action {Index + 1}";
        bool IQuickBindTarget.IsComplexBinding => !QuickBindActionApplier.IsSimpleOutput(CurrentOutput);
        EditFaceBindingContext IQuickBindTarget.GetEditContext() => owner.PrepareEdit(this);
        void IQuickBindTarget.NotifyBindingChanged() => owner.NotifyBindingChanged();
        bool IQuickBindRemovableTarget.CanRemoveTarget => CanRemove;
        void IQuickBindRemovableTarget.RemoveTarget() => owner.RemoveOutputAction(this);

        private OutputActionData CurrentOutput =>
            Index >= 0 && Index < (owner.Func?.OutputActions.Count ?? 0)
                ? owner.Func.OutputActions[Index]
                : null;

        public void Refresh()
        {
            OnPropertyChanged(nameof(DisplayBind));
            OnPropertyChanged(nameof(CanRemove));
            OnPropertyChanged(nameof(ShowAddSubCommand));
        }

        private void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
