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

        public ControllerCapabilities(
            ControllerDisplayInfo displayInfo,
            IEnumerable<ControllerInputDescriptor> descriptors)
        {
            DisplayInfo = displayInfo ?? ControllerDisplayInfo.Unknown();

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
