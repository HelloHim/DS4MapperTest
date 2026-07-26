using DS4MapperTest;
using DS4MapperTest.ViewModels;

namespace DS4MapperUnitTests
{
    [TestClass]
    public class ProfileDirtyStateTests
    {
        [TestMethod]
        public void DirtyStateTracksTheSavedProfileRepresentation()
        {
            Profile profile = new Profile { Name = "Original" };
            profile.ActionSets[0].ActionLayers[0].Name = "Layer 1";
            TestMapper mapper = new TestMapper(profile);
            var viewModel = new ProfileEditorTestViewModel(mapper,
                new ProfileEntity(string.Empty, "Original", InputDeviceType.None), profile);

            Assert.IsFalse(viewModel.IsProfileDirty);

            viewModel.ProfileName = "Changed";
            Assert.IsTrue(viewModel.IsProfileDirty);
            Assert.IsTrue(profile.Dirty);

            viewModel.ProfileName = "Original";
            Assert.IsFalse(viewModel.IsProfileDirty);
            Assert.IsFalse(profile.Dirty);

            viewModel.ProfileName = "Saved";
            viewModel.MarkProfileClean();
            Assert.IsFalse(viewModel.IsProfileDirty);
            Assert.IsFalse(profile.Dirty);

            profile.Name = "Changed outside the editor";
            profile.Dirty = true;
            Assert.IsTrue(viewModel.IsProfileDirty);

            profile.Name = "Saved";
            profile.Dirty = true;
            Assert.IsFalse(viewModel.IsProfileDirty);
            Assert.IsFalse(profile.Dirty);
        }

        [TestMethod]
        public void StaleProfileDirtyFlagDoesNotMarkANewEditorSessionUnsaved()
        {
            Profile profile = new Profile { Name = "Original", Dirty = true };
            profile.ActionSets[0].ActionLayers[0].Name = "Layer 1";
            TestMapper mapper = new TestMapper(profile);

            var viewModel = new ProfileEditorTestViewModel(mapper,
                new ProfileEntity(string.Empty, "Original", InputDeviceType.None), profile);

            Assert.IsFalse(viewModel.IsProfileDirty);
            Assert.IsFalse(profile.Dirty);
        }
    }
}
