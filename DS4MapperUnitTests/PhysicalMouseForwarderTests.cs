using System.Collections.Generic;
using System.Linq;
using DS4MapperTest;
using DS4MapperTest.MapperUtil;
using DS4MapperTest.PhysicalMouse;

namespace DS4MapperUnitTests
{
    [TestClass]
    public class PhysicalMouseForwarderTests
    {
        private sealed class RecordingPhysicalMouseRouter : IPhysicalMouseOutputRouter
        {
            public readonly List<(MouseOutputProducerId ProducerId, MouseOutputRoute Route, int X, int Y)> Relative =
                new List<(MouseOutputProducerId ProducerId, MouseOutputRoute Route, int X, int Y)>();
            public readonly List<(MouseOutputProducerId ProducerId, MouseOutputRoute Route, int Vertical, int Horizontal)> Wheel =
                new List<(MouseOutputProducerId ProducerId, MouseOutputRoute Route, int Vertical, int Horizontal)>();
            public readonly List<(MouseOutputProducerId ProducerId, MouseOutputRoute Route, int MouseCode, bool Pressed)> Buttons =
                new List<(MouseOutputProducerId ProducerId, MouseOutputRoute Route, int MouseCode, bool Pressed)>();
            public readonly List<(MouseOutputProducerId ProducerId, bool FlushSharedFakerInput)> Flushes =
                new List<(MouseOutputProducerId ProducerId, bool FlushSharedFakerInput)>();

            private long nextProducerId = 1;

            public MouseOutputProducerId? RegisteredProducer { get; private set; }
            public MouseOutputProducerId? UnregisteredProducer { get; private set; }

            public MouseOutputProducerId RegisterProducer()
            {
                RegisteredProducer = new MouseOutputProducerId(nextProducerId++);
                return RegisteredProducer.Value;
            }

            public void UnregisterProducer(MouseOutputProducerId producerId)
            {
                UnregisteredProducer = producerId;
            }

            public void QueueRelative(MouseOutputProducerId producerId, MouseOutputRoute route, int x, int y)
            {
                Relative.Add((producerId, route, x, y));
            }

            public void QueueWheel(MouseOutputProducerId producerId, MouseOutputRoute route, int vertical, int horizontal)
            {
                Wheel.Add((producerId, route, vertical, horizontal));
            }

            public void SetButton(MouseOutputProducerId producerId, MouseOutputRoute route, int mouseCode, bool pressed)
            {
                Buttons.Add((producerId, route, mouseCode, pressed));
            }

            public void FlushProducer(MouseOutputProducerId producerId, bool flushSharedFakerInput)
            {
                Flushes.Add((producerId, flushSharedFakerInput));
            }
        }

        private static PhysicalMouseForwarder CreateAttachedForwarder(
            out RecordingPhysicalMouseRouter router)
        {
            router = new RecordingPhysicalMouseRouter();
            RawMouseCaptureDevice capture = new RawMouseCaptureDevice();
            PhysicalMouseForwarder forwarder = new PhysicalMouseForwarder(capture);
            forwarder.AttachOutput(router);
            return forwarder;
        }

        [TestMethod]
        public void MovementUsesUnifiedVirtualMouseRouteAndFlushesImmediately()
        {
            PhysicalMouseForwarder forwarder = CreateAttachedForwarder(out RecordingPhysicalMouseRouter router);

            forwarder.HandleMouseMove(12, -7);

            Assert.AreEqual(1, router.Relative.Count);
            Assert.AreEqual(MouseOutputRoute.UnifiedVirtualMouse, router.Relative[0].Route);
            Assert.AreEqual(12, router.Relative[0].X);
            Assert.AreEqual(-7, router.Relative[0].Y);
            Assert.AreEqual(1, router.Flushes.Count);
            Assert.IsTrue(router.Flushes[0].FlushSharedFakerInput);
        }

        [TestMethod]
        public void ZeroMovementDoesNotTriggerARouteSubmission()
        {
            PhysicalMouseForwarder forwarder = CreateAttachedForwarder(out RecordingPhysicalMouseRouter router);

            forwarder.HandleMouseMove(0, 0);

            Assert.AreEqual(0, router.Relative.Count);
            Assert.AreEqual(0, router.Flushes.Count);
        }

        [TestMethod]
        public void WheelConvertsWholeNotchImmediately()
        {
            PhysicalMouseForwarder forwarder = CreateAttachedForwarder(out RecordingPhysicalMouseRouter router);

            forwarder.HandleMouseWheel(120, horizontal: false);

            Assert.AreEqual(1, router.Wheel.Count);
            Assert.AreEqual(MouseOutputRoute.UnifiedVirtualMouse, router.Wheel[0].Route);
            Assert.AreEqual(120, router.Wheel[0].Vertical);
            Assert.AreEqual(0, router.Wheel[0].Horizontal);
            Assert.AreEqual(1, router.Flushes.Count);
        }

        [TestMethod]
        public void WheelSubNotchDeltaIsCarriedNotDropped()
        {
            PhysicalMouseForwarder forwarder = CreateAttachedForwarder(out RecordingPhysicalMouseRouter router);

            forwarder.HandleMouseWheel(40, horizontal: false);
            forwarder.HandleMouseWheel(40, horizontal: false);
            Assert.AreEqual(0, router.Wheel.Count);

            forwarder.HandleMouseWheel(40, horizontal: false);

            Assert.AreEqual(1, router.Wheel.Count);
            Assert.AreEqual(120, router.Wheel[0].Vertical);
        }

        [TestMethod]
        public void HorizontalWheelUsesHorizontalAxis()
        {
            PhysicalMouseForwarder forwarder = CreateAttachedForwarder(out RecordingPhysicalMouseRouter router);

            forwarder.HandleMouseWheel(-120, horizontal: true);

            Assert.AreEqual(1, router.Wheel.Count);
            Assert.AreEqual(0, router.Wheel[0].Vertical);
            Assert.AreEqual(-120, router.Wheel[0].Horizontal);
        }

        [TestMethod]
        public void DuplicateDownFromSameSourceDoesNotEmitDuplicateButtonPress()
        {
            PhysicalMouseForwarder forwarder = CreateAttachedForwarder(out RecordingPhysicalMouseRouter router);

            forwarder.HandleMouseButton(RawMouseButton.Left, true);
            forwarder.HandleMouseButton(RawMouseButton.Left, true);
            forwarder.HandleMouseButton(RawMouseButton.Left, false);

            Assert.AreEqual(2, router.Buttons.Count);
            Assert.AreEqual((MouseButtonCodes.MOUSE_LEFT_BUTTON, true),
                (router.Buttons[0].MouseCode, router.Buttons[0].Pressed));
            Assert.AreEqual((MouseButtonCodes.MOUSE_LEFT_BUTTON, false),
                (router.Buttons[1].MouseCode, router.Buttons[1].Pressed));
        }

        [TestMethod]
        public void Button4And5MapToDistinctMouseCodes()
        {
            PhysicalMouseForwarder forwarder = CreateAttachedForwarder(out RecordingPhysicalMouseRouter router);

            forwarder.HandleMouseButton(RawMouseButton.Button4, true);
            forwarder.HandleMouseButton(RawMouseButton.Button5, true);

            CollectionAssert.AreEqual(
                new[] { MouseButtonCodes.MOUSE_XBUTTON1, MouseButtonCodes.MOUSE_XBUTTON2 },
                router.Buttons.Select(item => item.MouseCode).ToArray());
            Assert.IsTrue(router.Buttons.All(item => item.Route == MouseOutputRoute.UnifiedVirtualMouse));
        }

        [TestMethod]
        public void UnknownMouseButtonIsIgnored()
        {
            PhysicalMouseForwarder forwarder = CreateAttachedForwarder(out RecordingPhysicalMouseRouter router);

            forwarder.HandleMouseButton((RawMouseButton)999, true);

            Assert.AreEqual(0, router.Buttons.Count);
            Assert.AreEqual(0, router.Flushes.Count);
        }

        [TestMethod]
        public void DeviceRemovedReleasesOnlyPhysicalMouseHeldButtons()
        {
            PhysicalMouseForwarder forwarder = CreateAttachedForwarder(out RecordingPhysicalMouseRouter router);

            forwarder.HandleMouseButton(RawMouseButton.Left, true);
            forwarder.HandleMouseButton(RawMouseButton.Right, true);
            forwarder.HandleDeviceRemoved();

            CollectionAssert.AreEqual(
                new[]
                {
                    (MouseButtonCodes.MOUSE_LEFT_BUTTON, true),
                    (MouseButtonCodes.MOUSE_RIGHT_BUTTON, true),
                    (MouseButtonCodes.MOUSE_LEFT_BUTTON, false),
                    (MouseButtonCodes.MOUSE_RIGHT_BUTTON, false),
                },
                router.Buttons.Select(item => (item.MouseCode, item.Pressed)).ToArray());
        }

        [TestMethod]
        public void DetachOutputReleasesHeldButtonsBeforeUnregisteringProducer()
        {
            PhysicalMouseForwarder forwarder = CreateAttachedForwarder(out RecordingPhysicalMouseRouter router);

            forwarder.HandleMouseButton(RawMouseButton.Left, true);
            MouseOutputProducerId registeredProducer = router.RegisteredProducer.Value;

            forwarder.DetachOutput();

            Assert.AreEqual((MouseButtonCodes.MOUSE_LEFT_BUTTON, false),
                (router.Buttons.Last().MouseCode, router.Buttons.Last().Pressed));
            Assert.AreEqual(registeredProducer, router.UnregisteredProducer);
        }
    }
}
