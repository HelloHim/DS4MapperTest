using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace DS4MapperTest.Universal
{
    public sealed class ControllerCapabilities
    {
        private readonly IReadOnlyDictionary<UniversalInputId, ControllerInputDescriptor> descriptorsByInput;

        public ControllerDisplayInfo DisplayInfo { get; }

        public IEnumerable<ControllerInputDescriptor> Descriptors => descriptorsByInput.Values;

        public IEnumerable<UniversalInputId> SupportedInputs =>
            descriptorsByInput.Values
                .Where(descriptor => descriptor.IsSupported)
                .Select(descriptor => descriptor.InputId);

        /// <summary>
        /// How many motion samples per second this device actually produces,
        /// when the backend can tell. Null when unknown or when the device has
        /// no motion sensor.
        /// </summary>
        /// <remarks>
        /// The mapping loop polls at a fixed rate, and a device that reports
        /// faster than that has samples read twice and others never read at
        /// all. Reading the device's own rate lets the loop keep up with the
        /// hardware instead of assuming every controller is a 125 Hz one.
        /// </remarks>
        public double? MotionSampleRateHz { get; }

        public ControllerCapabilities(
            ControllerDisplayInfo displayInfo,
            IEnumerable<ControllerInputDescriptor> descriptors,
            double? motionSampleRateHz = null)
        {
            DisplayInfo = displayInfo ?? ControllerDisplayInfo.Unknown();
            MotionSampleRateHz = motionSampleRateHz > 0.0 ? motionSampleRateHz : null;

            Dictionary<UniversalInputId, ControllerInputDescriptor> temp =
                new Dictionary<UniversalInputId, ControllerInputDescriptor>();

            foreach (ControllerInputDescriptor descriptor in descriptors ?? Enumerable.Empty<ControllerInputDescriptor>())
            {
                if (descriptor == null)
                {
                    throw new ArgumentException("Controller input descriptors cannot contain null entries.", nameof(descriptors));
                }

                if (temp.ContainsKey(descriptor.InputId))
                {
                    throw new ArgumentException(
                        $"Controller capabilities contain duplicate descriptors for {descriptor.InputId}.",
                        nameof(descriptors));
                }

                temp.Add(descriptor.InputId, descriptor);
            }

            descriptorsByInput =
                new ReadOnlyDictionary<UniversalInputId, ControllerInputDescriptor>(temp);
        }

        public bool Supports(UniversalInputId inputId)
        {
            return descriptorsByInput.TryGetValue(inputId, out ControllerInputDescriptor descriptor) &&
                descriptor.IsSupported;
        }

        public bool TryGetDescriptor(
            UniversalInputId inputId,
            out ControllerInputDescriptor descriptor)
        {
            return descriptorsByInput.TryGetValue(inputId, out descriptor);
        }

        public string GetDisplayLabel(UniversalInputId inputId)
        {
            if (descriptorsByInput.TryGetValue(inputId, out ControllerInputDescriptor descriptor) &&
                !string.IsNullOrWhiteSpace(descriptor.NativeDisplayLabel))
            {
                return descriptor.NativeDisplayLabel;
            }

            return UniversalInputCatalog.TryGetMetadata(inputId, out UniversalInputMetadata metadata)
                ? metadata.DisplayName
                : inputId.ToString();
        }

        public string GetGlyphKey(UniversalInputId inputId)
        {
            if (descriptorsByInput.TryGetValue(inputId, out ControllerInputDescriptor descriptor) &&
                !string.IsNullOrWhiteSpace(descriptor.GlyphKey))
            {
                return descriptor.GlyphKey;
            }

            return DisplayInfo.GetFallbackGlyphKey(inputId);
        }
    }
}
