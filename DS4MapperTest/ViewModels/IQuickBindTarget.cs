using DS4MapperTest.ActionUtil;

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
        // Describe()/DescribeOutputActions() helpers.
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
}
