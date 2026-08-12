namespace DS4MapperTest.Universal.Editor
{
    // A lightweight, content-free summary of one stored action entry in an
    // action layer: just enough to let the editor offer "bind this input to
    // action #N" without deserialising or reinterpreting the action payload.
    public sealed class UniversalActionSummary
    {
        public int ActionId { get; }
        public string ActionType { get; }

        public UniversalActionSummary(int actionId, string actionType)
        {
            ActionId = actionId;
            ActionType = actionType ?? string.Empty;
        }
    }
}
