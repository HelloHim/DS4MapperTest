using DS4MapperTest.MapperUtil;

namespace DS4MapperTest.ActionUtil
{
    // Temporary compatibility stand-in for the legacy "Release Press" action-function
    // while that feature is being reimplemented from scratch. Profiles saved by older
    // versions may contain a function with "Type": "Release"; loading it as this inert
    // placeholder lets the rest of the profile load and run normally instead of crashing
    // or having the old data silently reinterpreted as a different press type. This
    // placeholder produces no output and is not reachable from the UI.
    public class LegacyReleaseFuncPlaceholder : ActionFunc
    {
        public LegacyReleaseFuncPlaceholder()
        {
        }

        public LegacyReleaseFuncPlaceholder(LegacyReleaseFuncPlaceholder srcFunc)
        {
            srcFunc.CopyTo(this);
        }

        public override void Prepare(Mapper mapper, bool state, ActionFuncStateData stateData)
        {
        }

        public override void Event(Mapper mapper, ActionFuncStateData stateData)
        {
        }

        public override void Release(Mapper mapper)
        {
        }

        public override string Describe(Mapper mapper)
        {
            return "Release Press (removed - profile not yet re-saved)";
        }
    }
}
