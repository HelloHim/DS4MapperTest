using DS4MapperTest.Universal.Profiles;
using System;
using System.Collections.Generic;

namespace DS4MapperTest.Universal.Editor
{
    public sealed class UniversalProfileEditorSaveResult
    {
        public UniversalProfileEditorSaveResult(
            bool success,
            UniversalProfile profile,
            IReadOnlyList<UniversalProfileValidationIssue> issues)
        {
            Success = success;
            Profile = profile;
            Issues = issues ?? Array.Empty<UniversalProfileValidationIssue>();
        }

        public bool Success { get; }
        public UniversalProfile Profile { get; }
        public IReadOnlyList<UniversalProfileValidationIssue> Issues { get; }
    }

    // Merges an editor model's projection back into a full universal profile
    // and persists it. Validation runs before the store is ever touched, so
    // a malformed or partially-edited profile can never replace the last
    // valid file on disk - the atomic store write (and any runtime reload)
    // only happens once validation passes.
    public sealed class UniversalProfileEditorSaveCoordinator
    {
        private readonly UniversalProfileStore store;
        private readonly Action<Guid, UniversalProfile> reloadHook;

        public UniversalProfileEditorSaveCoordinator(
            UniversalProfileStore store,
            Action<Guid, UniversalProfile> reloadHook = null)
        {
            this.store = store ?? throw new ArgumentNullException(nameof(store));
            this.reloadHook = reloadHook;
        }

        public UniversalProfileEditorSaveResult Save(
            UniversalProfileEditorModel model,
            Guid? logicalControllerIdToReload = null)
        {
            if (model == null) throw new ArgumentNullException(nameof(model));

            UniversalProfile candidate = model.BuildUpdatedProfile();
            return SaveProfile(candidate, logicalControllerIdToReload);
        }

        public UniversalProfileEditorSaveResult SaveProfile(
            UniversalProfile candidate,
            Guid? logicalControllerIdToReload = null,
            string previousPath = null)
        {
            if (candidate == null) throw new ArgumentNullException(nameof(candidate));

            UniversalProfileValidationResult validation = UniversalProfileValidator.Validate(candidate);
            if (!validation.IsValid)
            {
                return new UniversalProfileEditorSaveResult(false, candidate, validation.Issues);
            }

            if (string.IsNullOrWhiteSpace(previousPath))
            {
                store.Save(candidate);
            }
            else
            {
                store.SaveNamed(candidate, previousPath);
            }

            if (logicalControllerIdToReload.HasValue)
            {
                reloadHook?.Invoke(logicalControllerIdToReload.Value, candidate);
            }

            return new UniversalProfileEditorSaveResult(true, candidate, validation.Issues);
        }
    }
}
