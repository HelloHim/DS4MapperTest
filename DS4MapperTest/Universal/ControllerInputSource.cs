namespace DS4MapperTest.Universal
{
    public sealed class ControllerInputSource
    {
        public static readonly ControllerInputSource None = new ControllerInputSource(string.Empty, string.Empty, string.Empty);

        public string BackendName { get; }
        public string NativeIdentifier { get; }
        public string NativeElement { get; }

        public ControllerInputSource(string backendName, string nativeIdentifier, string nativeElement)
        {
            BackendName = backendName ?? string.Empty;
            NativeIdentifier = nativeIdentifier ?? string.Empty;
            NativeElement = nativeElement ?? string.Empty;
        }
    }
}
