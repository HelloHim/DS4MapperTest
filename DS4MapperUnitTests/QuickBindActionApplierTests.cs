using System;
using System.Linq;
using DS4MapperTest;
using DS4MapperTest.ActionUtil;
using DS4MapperTest.ButtonActions;
using DS4MapperTest.MapperUtil;
using DS4MapperTest.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;

namespace DS4MapperUnitTests
{
    // Minimal reader that lets Mapper.ProcessMappingChangeAction run its
    // action immediately, without a real input thread. HaltReportingRunAction
    // waits on readWaitEv before invoking the action, so it must already be
    // signalled.
    internal class NoOpDeviceReader : DeviceReaderBase
    {
        public NoOpDeviceReader()
        {
            ReadWaitEv.Set();
        }

        public override void StartUpdate() { }
        public override void StopUpdate() { }
        public override void WriteRumbleReport() { }
    }

    internal class QuickBindTestMapper : TestMapper
    {
        private readonly DeviceReaderBase reader = new NoOpDeviceReader();
        public override DeviceReaderBase BaseReader => reader;
    }

    // ProfileSerializer.EventInputMapper is normally wired up by the app's
    // backend manager at startup; QuickBindActionApplier.ApplyKeyboard reads
    // it to translate a VirtualKeys value into the active output backend's
    // native event code, so tests need a stand-in.
    internal class IdentityVirtualKBMMapping : VirtualKBMMapping
    {
        public override void PopulateConstants() { }
        public override void PopulateMappings() { }
        public override uint GetRealEventKey(uint winVkKey) => winVkKey;
    }

    [TestClass]
    public class QuickBindActionApplierTests
    {
        [TestInitialize]
        public void TestInitialize()
        {
            ProfileSerializer.EventInputMapper = new IdentityVirtualKBMMapping();
        }

        private static EditFaceBindingContext MakeContext(out ActionFunc func)
        {
            Mapper mapper = new QuickBindTestMapper();
            ButtonAction action = new ButtonAction();
            func = new NormalPressFunc(new OutputActionData(OutputActionData.ActionType.Empty, 0));
            action.ActionFuncs.Add(func);

            return new EditFaceBindingContext(mapper, action, func);
        }

        [TestMethod]
        public void IsSimpleFunc_NullFunc_IsSimple()
        {
            Assert.IsTrue(QuickBindActionApplier.IsSimpleFunc(null));
        }

        [TestMethod]
        public void IsSimpleFunc_UnboundFunc_IsSimple()
        {
            ActionFunc func = new NormalPressFunc(new OutputActionData(OutputActionData.ActionType.Empty, 0));
            Assert.IsTrue(QuickBindActionApplier.IsSimpleFunc(func));
        }

        [TestMethod]
        public void IsSimpleFunc_SingleKeyboardOutput_IsSimple()
        {
            ActionFunc func = new NormalPressFunc(
                new OutputActionData(OutputActionData.ActionType.Keyboard, (int)VirtualKeys.A));
            Assert.IsTrue(QuickBindActionApplier.IsSimpleFunc(func));
        }

        [TestMethod]
        public void IsSimpleFunc_SingleMouseButtonOutput_IsSimple()
        {
            ActionFunc func = new NormalPressFunc(
                new OutputActionData(OutputActionData.ActionType.MouseButton, MouseButtonCodes.MOUSE_LEFT_BUTTON));
            Assert.IsTrue(QuickBindActionApplier.IsSimpleFunc(func));
        }

        [TestMethod]
        public void IsSimpleFunc_SingleMouseWheelOutput_IsSimple()
        {
            ActionFunc func = new NormalPressFunc(
                new OutputActionData(OutputActionData.ActionType.MouseWheel, (int)MouseWheelCodes.WheelUp));
            Assert.IsTrue(QuickBindActionApplier.IsSimpleFunc(func));
        }

        [TestMethod]
        public void IsSimpleFunc_SingleGamepadOutput_IsComplex()
        {
            ActionFunc func = new NormalPressFunc(
                new OutputActionData(OutputActionData.ActionType.GamepadControl, JoypadActionCodes.X360_A));
            Assert.IsFalse(QuickBindActionApplier.IsSimpleFunc(func));
        }

        [TestMethod]
        public void IsSimpleFunc_MultipleOutputs_IsComplex()
        {
            ActionFunc func = new NormalPressFunc(new[]
            {
                new OutputActionData(OutputActionData.ActionType.Keyboard, (int)VirtualKeys.A),
                new OutputActionData(OutputActionData.ActionType.Keyboard, (int)VirtualKeys.B),
            });
            Assert.IsFalse(QuickBindActionApplier.IsSimpleFunc(func));
        }

        [TestMethod]
        public void IsSimpleFunc_ChordedPressWithSingleKeyboardOutput_IsSimple()
        {
            ChordedPressFunc func = new ChordedPressFunc
            {
                TriggerButton = JoypadActionCodes.BtnNorth,
            };
            func.OutputActions.Add(new OutputActionData(OutputActionData.ActionType.Keyboard, (int)VirtualKeys.A));

            Assert.IsTrue(QuickBindActionApplier.IsSimpleFunc(func));
        }

        [TestMethod]
        public void ApplyKeyboard_ChordedPress_PreservesChordTrigger()
        {
            Mapper mapper = new QuickBindTestMapper();
            ButtonAction action = new ButtonAction();
            ChordedPressFunc func = new ChordedPressFunc
            {
                TriggerButton = JoypadActionCodes.BtnEast,
            };
            func.OutputActions.Add(new OutputActionData(OutputActionData.ActionType.Keyboard, (int)VirtualKeys.A));
            action.ActionFuncs.Add(func);

            QuickBindActionApplier.ApplyKeyboard(
                new EditFaceBindingContext(mapper, action, func), VirtualKeys.B, "B");

            Assert.AreEqual(JoypadActionCodes.BtnEast, func.TriggerButton);
            Assert.AreEqual((int)VirtualKeys.B, func.OutputActions.Single().OutputCode);
        }

        [TestMethod]
        public void ButtonFuncSelector_ChordedPress_UsesChordedIndex()
        {
            ButtonActionFuncSelectViewModel vm = new ButtonActionFuncSelectViewModel(new ChordedPressFunc());

            Assert.AreEqual(5, vm.SelectedIndex);
        }

        [TestMethod]
        public void FuncBindingControl_ChangeToChordedPress_CreatesChordedFunc()
        {
            Mapper mapper = new QuickBindTestMapper();
            ButtonAction action = new ButtonAction();
            action.ActionFuncs.Add(new NormalPressFunc(
                new OutputActionData(OutputActionData.ActionType.Empty, 0)));
            FuncBindingControlViewModel vm = new FuncBindingControlViewModel(mapper, action, null);

            vm.ChangeFunc(0, 5);

            Assert.IsInstanceOfType(action.ActionFuncs[0], typeof(ChordedPressFunc));
            Assert.AreEqual(1, action.ActionFuncs[0].OutputActions.Count);
            Assert.AreEqual(OutputActionData.ActionType.Empty, action.ActionFuncs[0].OutputActions[0].OutputType);
        }

        [TestMethod]
        public void CopyFunc_ChordedPress_PreservesTriggerAndOutput()
        {
            ChordedPressFunc source = new ChordedPressFunc
            {
                TriggerButton = JoypadActionCodes.BtnNorth,
            };
            source.OutputActions.Add(new OutputActionData(OutputActionData.ActionType.Keyboard, (int)VirtualKeys.X));

            ChordedPressFunc copy = ActionFuncCopyFactory.CopyFunc(source) as ChordedPressFunc;

            Assert.IsNotNull(copy);
            Assert.AreNotSame(source, copy);
            Assert.AreEqual(JoypadActionCodes.BtnNorth, copy.TriggerButton);
            Assert.AreEqual((int)VirtualKeys.X, copy.OutputActions.Single().OutputCode);
        }

        [TestMethod]
        public void ChordedPressSerializer_RoundTripsTrigger()
        {
            ChordedPressFunc source = new ChordedPressFunc
            {
                TriggerButton = JoypadActionCodes.BtnSouth,
            };
            source.OutputActions.Add(new OutputActionData(OutputActionData.ActionType.Keyboard, (int)VirtualKeys.C));
            ChordedPressFuncSerializer serializer = new ChordedPressFuncSerializer(source);

            string json = JsonConvert.SerializeObject(serializer);
            ChordedPressFuncSerializer loaded = JsonConvert.DeserializeObject<ChordedPressFuncSerializer>(json);
            loaded.PopulateFunc();

            Assert.IsTrue(json.Contains("\"Trigger\":\"BtnSouth\""));
            Assert.AreEqual(JoypadActionCodes.BtnSouth, loaded.ChorededPressFunc.TriggerButton);
            Assert.AreEqual((int)VirtualKeys.C, loaded.ChorededPressFunc.OutputActions.Single().OutputCode);
        }

        [TestMethod]
        public void LegacyReleaseProfileEntry_LoadsIgnoringOldDurationAndInterruptable()
        {
            string json = "{\"Type\":\"Release\",\"OutputActions\":[{\"Type\":\"Keyboard\",\"Code\":\"A\"}]," +
                "\"Settings\":{\"Duration\":250,\"Interruptable\":true,\"DelayDuration\":150}}";

            ActionFuncSerializer loaded = JsonConvert.DeserializeObject<ActionFuncSerializer>(json);
            loaded.PopulateFunc();

            Assert.IsInstanceOfType(loaded, typeof(ReleaseFuncSerializer));
            Assert.IsInstanceOfType(loaded.ActionFunc, typeof(ReleaseFunc));

            ReleaseFunc releaseFunc = (ReleaseFunc)loaded.ActionFunc;
            Assert.AreEqual(150, releaseFunc.DelayDurationMs);
            Assert.AreEqual((int)VirtualKeys.A, releaseFunc.OutputActions.Single().OutputCode);
            Assert.IsFalse(releaseFunc.toggleEnabled);

            string resaved = JsonConvert.SerializeObject(loaded);
            Assert.IsFalse(resaved.Contains("Duration\":250"));
            Assert.IsFalse(resaved.Contains("Interruptable"));
        }

        [TestMethod]
        public void ApplyKeyboard_UnboundSlot_BecomesKeyboardAction()
        {
            EditFaceBindingContext ctx = MakeContext(out ActionFunc func);

            QuickBindActionApplier.ApplyKeyboard(ctx, VirtualKeys.E, "E");

            Assert.AreEqual(1, func.OutputActions.Count);
            OutputActionData data = func.OutputActions[0];
            Assert.AreEqual(OutputActionData.ActionType.Keyboard, data.OutputType);
            Assert.AreEqual((int)VirtualKeys.E, data.OutputCode);
            Assert.AreEqual("E", data.OutputCodeStr);
        }

        [TestMethod]
        public void ApplyKeyboard_MarksActionFunctionsChanged()
        {
            EditFaceBindingContext ctx = MakeContext(out _);

            QuickBindActionApplier.ApplyKeyboard(ctx, VirtualKeys.Space, "Space");

            Assert.IsTrue(ctx.Action.ChangedProperties.Contains(ButtonAction.PropertyKeyStrings.FUNCTIONS));
        }

        [TestMethod]
        public void ApplyMouseButton_UnboundSlot_BecomesMouseButtonAction()
        {
            EditFaceBindingContext ctx = MakeContext(out ActionFunc func);

            QuickBindActionApplier.ApplyMouseButton(ctx, MouseButtonCodes.MOUSE_RIGHT_BUTTON, "RightButton");

            OutputActionData data = func.OutputActions.Single();
            Assert.AreEqual(OutputActionData.ActionType.MouseButton, data.OutputType);
            Assert.AreEqual(MouseButtonCodes.MOUSE_RIGHT_BUTTON, data.OutputCode);
            Assert.AreEqual("RightButton", data.OutputCodeStr);
        }

        [TestMethod]
        public void ApplyMouseWheel_UnboundSlot_BecomesMouseWheelAction()
        {
            EditFaceBindingContext ctx = MakeContext(out ActionFunc func);

            QuickBindActionApplier.ApplyMouseWheel(ctx, MouseWheelCodes.WheelDown, "WheelDown");

            OutputActionData data = func.OutputActions.Single();
            Assert.AreEqual(OutputActionData.ActionType.MouseWheel, data.OutputType);
            Assert.AreEqual((int)MouseWheelCodes.WheelDown, data.OutputCode);
        }

        [TestMethod]
        public void ApplyUnbound_ClearsToEmpty()
        {
            EditFaceBindingContext ctx = MakeContext(out ActionFunc func);
            QuickBindActionApplier.ApplyKeyboard(ctx, VirtualKeys.A, "A");

            QuickBindActionApplier.ApplyUnbound(ctx);

            OutputActionData data = func.OutputActions.Single();
            Assert.AreEqual(OutputActionData.ActionType.Empty, data.OutputType);
        }

        [TestMethod]
        public void ApplyUnbound_ClearingLastOutput_DisablesTurbo()
        {
            EditFaceBindingContext ctx = MakeContext(out ActionFunc func);
            QuickBindActionApplier.ApplyKeyboard(ctx, VirtualKeys.A, "A");
            ((NormalPressFunc)func).TurboEnabled = true;

            QuickBindActionApplier.ApplyUnbound(ctx);

            Assert.IsFalse(((NormalPressFunc)func).TurboEnabled);
        }

        [TestMethod]
        public void ApplyUnbound_RowContext_KeepsTurboWhileAnotherOutputRemains()
        {
            EditFaceBindingContext ctx = MakeContext(out ActionFunc func);
            func.OutputActions.Clear();
            func.OutputActions.Add(new OutputActionData(OutputActionData.ActionType.Keyboard, (int)VirtualKeys.H));
            func.OutputActions.Add(new OutputActionData(OutputActionData.ActionType.Keyboard, (int)VirtualKeys.J));
            ((NormalPressFunc)func).TurboEnabled = true;

            QuickBindActionApplier.ApplyUnbound(
                new EditFaceBindingContext(ctx.Mapper, ctx.Action, func, 0));

            Assert.IsTrue(((NormalPressFunc)func).TurboEnabled);
        }

        [TestMethod]
        public void DisableTurboIfUnbound_NoOutputsLeftBound_DisablesTurbo()
        {
            NormalPressFunc func = new NormalPressFunc(
                new OutputActionData(OutputActionData.ActionType.Empty, 0));
            func.TurboEnabled = true;

            QuickBindActionApplier.DisableTurboIfUnbound(func);

            Assert.IsFalse(func.TurboEnabled);
        }

        [TestMethod]
        public void DisableTurboIfUnbound_HoldPressWithBoundOutput_KeepsTurbo()
        {
            HoldPressFunc func = new HoldPressFunc();
            func.OutputActions.Add(
                new OutputActionData(OutputActionData.ActionType.Keyboard, (int)VirtualKeys.H));
            func.TurboEnabled = true;

            QuickBindActionApplier.DisableTurboIfUnbound(func);

            Assert.IsTrue(func.TurboEnabled);
        }

        [TestMethod]
        public void ApplyKeyboard_ReplacingComplexFunc_CollapsesToSingleSlot()
        {
            EditFaceBindingContext ctx = MakeContext(out ActionFunc func);
            func.OutputActions.Clear();
            func.OutputActions.Add(new OutputActionData(OutputActionData.ActionType.Keyboard, (int)VirtualKeys.A));
            func.OutputActions.Add(new OutputActionData(OutputActionData.ActionType.Keyboard, (int)VirtualKeys.B));
            Assert.IsFalse(QuickBindActionApplier.IsSimpleFunc(func));

            QuickBindActionApplier.ApplyKeyboard(ctx, VirtualKeys.C, "C");

            OutputActionData data = func.OutputActions.Single();
            Assert.AreEqual((int)VirtualKeys.C, data.OutputCode);
        }

        [TestMethod]
        public void ApplyKeyboard_RowContext_ReplacesOnlyTargetSlot()
        {
            EditFaceBindingContext ctx = MakeContext(out ActionFunc func);
            func.OutputActions.Clear();
            func.OutputActions.Add(new OutputActionData(OutputActionData.ActionType.Keyboard, (int)VirtualKeys.A));
            func.OutputActions.Add(new OutputActionData(OutputActionData.ActionType.Keyboard, (int)VirtualKeys.B));
            func.OutputActions.Add(new OutputActionData(OutputActionData.ActionType.MouseButton, MouseButtonCodes.MOUSE_LEFT_BUTTON));

            QuickBindActionApplier.ApplyKeyboard(
                new EditFaceBindingContext(ctx.Mapper, ctx.Action, func, 1),
                VirtualKeys.J,
                "J");

            Assert.AreEqual(3, func.OutputActions.Count);
            Assert.AreEqual((int)VirtualKeys.A, func.OutputActions[0].OutputCode);
            Assert.AreEqual(OutputActionData.ActionType.Keyboard, func.OutputActions[1].OutputType);
            Assert.AreEqual((int)VirtualKeys.J, func.OutputActions[1].OutputCode);
            Assert.AreEqual(OutputActionData.ActionType.MouseButton, func.OutputActions[2].OutputType);
        }

        [TestMethod]
        public void ApplyUnbound_RowContext_ClearsOnlyTargetSlot()
        {
            EditFaceBindingContext ctx = MakeContext(out ActionFunc func);
            func.OutputActions.Clear();
            func.OutputActions.Add(new OutputActionData(OutputActionData.ActionType.Keyboard, (int)VirtualKeys.H));
            func.OutputActions.Add(new OutputActionData(OutputActionData.ActionType.Keyboard, (int)VirtualKeys.J));

            QuickBindActionApplier.ApplyUnbound(
                new EditFaceBindingContext(ctx.Mapper, ctx.Action, func, 0));

            Assert.AreEqual(2, func.OutputActions.Count);
            Assert.AreEqual(OutputActionData.ActionType.Empty, func.OutputActions[0].OutputType);
            Assert.AreEqual((int)VirtualKeys.J, func.OutputActions[1].OutputCode);
        }

        [TestMethod]
        public void NormalPressSerializer_RoundTripsMultipleOutputsInOrder()
        {
            NormalPressFunc source = new NormalPressFunc(new[]
            {
                new OutputActionData(OutputActionData.ActionType.Keyboard, (int)VirtualKeys.H),
                new OutputActionData(OutputActionData.ActionType.Keyboard, (int)VirtualKeys.J),
                new OutputActionData(OutputActionData.ActionType.MouseButton, MouseButtonCodes.MOUSE_LEFT_BUTTON),
            });
            NormalPressFuncSerializer serializer = new NormalPressFuncSerializer(source);

            string json = JsonConvert.SerializeObject(serializer);
            ActionFuncSerializer loaded = JsonConvert.DeserializeObject<ActionFuncSerializer>(json);
            loaded.PopulateFunc();

            Assert.IsInstanceOfType(loaded.ActionFunc, typeof(NormalPressFunc));
            Assert.AreEqual(3, loaded.ActionFunc.OutputActions.Count);
            Assert.AreEqual((int)VirtualKeys.H, loaded.ActionFunc.OutputActions[0].OutputCode);
            Assert.AreEqual((int)VirtualKeys.J, loaded.ActionFunc.OutputActions[1].OutputCode);
            Assert.AreEqual(OutputActionData.ActionType.MouseButton, loaded.ActionFunc.OutputActions[2].OutputType);
        }

        [TestMethod]
        public void CopyFunc_MultipleOutputs_CreatesIndependentOutputCopies()
        {
            NormalPressFunc source = new NormalPressFunc(new[]
            {
                new OutputActionData(OutputActionData.ActionType.Keyboard, (int)VirtualKeys.H),
                new OutputActionData(OutputActionData.ActionType.Keyboard, (int)VirtualKeys.J),
            });

            NormalPressFunc copy = ActionFuncCopyFactory.CopyFunc(source) as NormalPressFunc;

            Assert.IsNotNull(copy);
            Assert.AreEqual(2, copy.OutputActions.Count);
            Assert.AreNotSame(source.OutputActions[0], copy.OutputActions[0]);
            copy.OutputActions[0].Prepare(OutputActionData.ActionType.Keyboard, (int)VirtualKeys.K);
            Assert.AreEqual((int)VirtualKeys.H, source.OutputActions[0].OutputCode);
            Assert.AreEqual((int)VirtualKeys.K, copy.OutputActions[0].OutputCode);
        }

        [TestMethod]
        public void ApplyKeyboard_ReplacingNormalPress_LeavesOtherFuncsOnActionUntouched()
        {
            Mapper mapper = new QuickBindTestMapper();
            ButtonAction action = new ButtonAction();
            NormalPressFunc normalFunc = new NormalPressFunc(
                new OutputActionData(OutputActionData.ActionType.Keyboard, (int)VirtualKeys.W));
            HoldPressFunc holdFunc = new HoldPressFunc();
            holdFunc.OutputActions.Add(new OutputActionData(OutputActionData.ActionType.Keyboard, (int)VirtualKeys.S));
            action.ActionFuncs.Add(normalFunc);
            action.ActionFuncs.Add(holdFunc);

            EditFaceBindingContext ctx = new EditFaceBindingContext(mapper, action, normalFunc);
            QuickBindActionApplier.ApplyKeyboard(ctx, VirtualKeys.E, "E");

            Assert.AreEqual((int)VirtualKeys.E, normalFunc.OutputActions.Single().OutputCode);
            Assert.AreEqual((int)VirtualKeys.S, holdFunc.OutputActions.Single().OutputCode);
        }
    }
}
