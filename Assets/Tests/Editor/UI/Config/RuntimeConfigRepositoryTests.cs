using System;
using System.IO;
using NUnit.Framework;
using Sudoku.UI.Config;

namespace Sudoku.Tests.Editor.UI.Config
{
    /**
     * Unit tests for runtime config JSON persistence.
     */
    [TestFixture]
    public class RuntimeConfigRepositoryTests
    {
        private string _tempFile;

        [SetUp]
        public void SetUp()
        {
            _tempFile = Path.Combine(Path.GetTempPath(), $"test_runtime_config_{Guid.NewGuid():N}.json");
            RuntimeConfigRepository.OverrideFilePath = _tempFile;
            RuntimeConfigDefaults.Factory = CreateCustomDefaults;
        }

        [TearDown]
        public void TearDown()
        {
            RuntimeConfigRepository.OverrideFilePath = null;
            RuntimeConfigDefaults.Factory = CreateBuiltInStyleDefaults;

            if (File.Exists(_tempFile))
            {
                File.Delete(_tempFile);
            }
        }

        [Test]
        public void LoadOrDefault_WhenNoFileExists_UsesConfiguredDefaults()
        {
            var config = RuntimeConfigRepository.LoadOrDefault();

            Assert.IsNotNull(config);
            Assert.IsNotNull(config.Assistance);
            Assert.IsNotNull(config.Generation);
            Assert.IsNotNull(config.Rules);
            Assert.IsTrue(config.Assistance.AutoFillAllCandidatesOnPuzzleStart);
            Assert.IsTrue(config.Assistance.AutoInitialiseCandidatesOnPuzzleStart);
            Assert.IsTrue(config.Assistance.HideApplyRules);
            Assert.IsFalse(config.Assistance.AutoCandidateOnSetValue);
            Assert.IsFalse(config.Generation.UseRotationalSymmetry);
            Assert.AreEqual(0, config.Rules.Count);
        }

        [Test]
        public void SaveAndLoad_RoundTripsKnownFields()
        {
            var config = RuntimeConfigDefaults.Create();
            config.Assistance.AutoFillAllCandidatesOnPuzzleStart = true;
            config.Assistance.AutoInitialiseCandidatesOnPuzzleStart = false;
            config.Assistance.HideApplyRules = false;
            config.Assistance.AutoCandidateOnSetValue = true;
            config.Generation.UseRotationalSymmetry = true;
            config.Rules.Add(new RuleConfigEntry { RuleTypeName = "NakedSingleRule", Enabled = false });

            RuntimeConfigRepository.Save(config);
            var loaded = RuntimeConfigRepository.LoadOrDefault();

            Assert.IsNotNull(loaded);
            Assert.IsTrue(loaded.Assistance.AutoFillAllCandidatesOnPuzzleStart);
            Assert.IsFalse(loaded.Assistance.AutoInitialiseCandidatesOnPuzzleStart);
            Assert.IsFalse(loaded.Assistance.HideApplyRules);
            Assert.IsTrue(loaded.Assistance.AutoCandidateOnSetValue);
            Assert.IsTrue(loaded.Generation.UseRotationalSymmetry);
            Assert.AreEqual(1, loaded.Rules.Count);
            Assert.AreEqual("NakedSingleRule", loaded.Rules[0].RuleTypeName);
            Assert.IsFalse(loaded.Rules[0].Enabled);
        }

        [Test]
        public void LoadOrDefault_WithOlderJson_KeepsDefaultsForMissingFields()
        {
            var oldJson = "{\n" +
                          "  \"SchemaVersion\": 1,\n" +
                          "  \"Assistance\": { \"HideApplyRules\": false },\n" +
                          "  \"Rules\": [ { \"RuleTypeName\": \"RightAngleRule\", \"Enabled\": true } ]\n" +
                          "}";

            File.WriteAllText(_tempFile, oldJson);

            var loaded = RuntimeConfigRepository.LoadOrDefault();

            // Present in old json.
            Assert.IsFalse(loaded.Assistance.HideApplyRules);

            // Missing from old json: should keep configured defaults.
            Assert.IsTrue(loaded.Assistance.AutoFillAllCandidatesOnPuzzleStart);
            Assert.IsTrue(loaded.Assistance.AutoInitialiseCandidatesOnPuzzleStart);
            Assert.IsFalse(loaded.Assistance.AutoCandidateOnSetValue);
            Assert.IsFalse(loaded.Generation.UseRotationalSymmetry);

            Assert.AreEqual(1, loaded.Rules.Count);
            Assert.AreEqual("RightAngleRule", loaded.Rules[0].RuleTypeName);
            Assert.IsTrue(loaded.Rules[0].Enabled);
        }

        /**
         * Custom defaults used to verify the repository keeps missing fields initialized.
         *
         * @returns Runtime config defaults for tests.
         */
        private static RuntimeConfigData CreateCustomDefaults()
        {
            return new RuntimeConfigData
            {
                SchemaVersion = 1,
                Assistance = new AssistanceConfigData
                {
                    AutoFillAllCandidatesOnPuzzleStart = true,
                    AutoInitialiseCandidatesOnPuzzleStart = true,
                    HideApplyRules = true,
                    AutoCandidateOnSetValue = false
                },
                Generation = new GenerationConfigData
                {
                    UseRotationalSymmetry = false
                },
                Rules = new System.Collections.Generic.List<RuleConfigEntry>()
            };
        }

        /**
         * Restore default behavior after tests.
         *
         * @returns Runtime config defaults matching production defaults.
         */
        private static RuntimeConfigData CreateBuiltInStyleDefaults()
        {
            return new RuntimeConfigData
            {
                SchemaVersion = 1,
                Assistance = new AssistanceConfigData
                {
                    AutoFillAllCandidatesOnPuzzleStart = false,
                    AutoInitialiseCandidatesOnPuzzleStart = false,
                    HideApplyRules = false,
                    AutoCandidateOnSetValue = true
                },
                Generation = new GenerationConfigData
                {
                    UseRotationalSymmetry = true
                },
                Rules = new System.Collections.Generic.List<RuleConfigEntry>()
            };
        }
    }
}
