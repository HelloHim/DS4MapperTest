using DS4MapperTest.ActionUtil;
using DS4MapperTest.ButtonActions;
using DS4MapperTest.MapperUtil;

namespace DS4MapperTest.ViewModels
{
    /// <summary>
    /// Applies a captured keyboard/mouse input to an existing ActionFunc slot
    /// using the same OutputActionData mutation path as ButtonActionEditViewModel
    /// (Prepare + OutputCodeStr + OutputCodeAlias, wrapped in
    /// Mapper.ProcessMappingChangeAction), so Quick Bind produces a result that
    /// is indistinguishable from one created through the advanced editor.
    /// </summary>
    public static class QuickBindActionApplier
    {
        // A func slot is "simple" when Quick Bind can replace/clear it without a
        // confirmation prompt: unbound, or exactly one keyboard/mouse-button/
        // mouse-wheel output. Anything else (multiple outputs, macros, gamepad
        // control, layer/set switches, waits, camera turns, ...) is complex.
        public static bool IsSimpleFunc(ActionFunc func)
        {
            if (func == null) return true;
            if (func.OutputActions.Count == 0) return true;
            if (func.OutputActions.Count > 1) return false;

            return IsSimpleOutput(func.OutputActions[0]);
        }

        public static bool IsSimpleOutput(OutputActionData data)
        {
            if (data == null) return true;

            OutputActionData.ActionType type = data.OutputType;
            return type == OutputActionData.ActionType.Empty ||
                type == OutputActionData.ActionType.Keyboard ||
                type == OutputActionData.ActionType.MouseButton ||
                type == OutputActionData.ActionType.MouseWheel;
        }

        public static void ApplyKeyboard(EditFaceBindingContext ctx, VirtualKeys key, string codeAlias)
        {
            if (ctx?.Func == null || ctx.Action == null || ctx.Mapper == null) return;

            ctx.Mapper.ProcessMappingChangeAction(() =>
            {
                ctx.Action.Release(ctx.Mapper, ignoreReleaseActions: true);

                OutputActionData data = EnsureTargetSlot(ctx);
                data.Reset();

                uint eventCode = ProfileSerializer.EventInputMapper.GetRealEventKey((uint)key);
                data.Prepare(OutputActionData.ActionType.Keyboard, (int)key);
                data.OutputCodeStr = codeAlias;
                data.OutputCodeAlias = eventCode;

                FaceButtonBindingItem.MarkFunctionsChanged(ctx.Action);
            });
        }

        public static void ApplyMouseButton(EditFaceBindingContext ctx, int mouseButtonCode, string codeAlias)
        {
            if (ctx?.Func == null || ctx.Action == null || ctx.Mapper == null) return;

            ctx.Mapper.ProcessMappingChangeAction(() =>
            {
                ctx.Action.Release(ctx.Mapper, ignoreReleaseActions: true);

                OutputActionData data = EnsureTargetSlot(ctx);
                data.Reset();

                data.Prepare(OutputActionData.ActionType.MouseButton, mouseButtonCode);
                data.OutputCodeStr = codeAlias;

                FaceButtonBindingItem.MarkFunctionsChanged(ctx.Action);
            });
        }

        public static void ApplyMouseWheel(EditFaceBindingContext ctx, MouseWheelCodes wheelCode, string codeAlias)
        {
            if (ctx?.Func == null || ctx.Action == null || ctx.Mapper == null) return;

            ctx.Mapper.ProcessMappingChangeAction(() =>
            {
                ctx.Action.Release(ctx.Mapper, ignoreReleaseActions: true);

                OutputActionData data = EnsureTargetSlot(ctx);
                data.Reset();

                data.Prepare(OutputActionData.ActionType.MouseWheel, (int)wheelCode);
                data.OutputCodeStr = codeAlias;
                data.checkTick = false;

                FaceButtonBindingItem.MarkFunctionsChanged(ctx.Action);
            });
        }

        public static void ApplyUnbound(EditFaceBindingContext ctx)
        {
            if (ctx?.Func == null || ctx.Action == null || ctx.Mapper == null) return;

            ctx.Mapper.ProcessMappingChangeAction(() =>
            {
                ctx.Action.Release(ctx.Mapper, ignoreReleaseActions: true);

                OutputActionData data = EnsureTargetSlot(ctx);
                data.Reset();

                data.Prepare(OutputActionData.ActionType.Empty, 0);
                data.OutputCodeStr = OutputActionData.ActionType.Empty.ToString();

                FaceButtonBindingItem.MarkFunctionsChanged(ctx.Action);
            });
        }

        // Whole-function quick bind keeps the old simple workflow by replacing the
        // single visible slot. Row-level quick bind targets only the requested slot.
        private static OutputActionData EnsureTargetSlot(EditFaceBindingContext ctx)
        {
            if (ctx.OutputIndex.HasValue)
            {
                int index = ctx.OutputIndex.Value;
                while (ctx.Func.OutputActions.Count <= index)
                {
                    ctx.Func.OutputActions.Add(new OutputActionData(OutputActionData.ActionType.Empty, 0));
                }

                return ctx.Func.OutputActions[index];
            }

            return EnsureSingleSlot(ctx.Func);
        }

        private static OutputActionData EnsureSingleSlot(ActionFunc func)
        {
            OutputActionData first = func.OutputActions.Count > 0
                ? func.OutputActions[0]
                : new OutputActionData(OutputActionData.ActionType.Empty, 0);

            if (func.OutputActions.Count != 1)
            {
                func.OutputActions.Clear();
                func.OutputActions.Add(first);
            }

            return first;
        }
    }
}
