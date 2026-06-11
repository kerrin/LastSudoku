using System;
using System.Collections.Generic;
using System.IO;
using Sudoku.Solver;
using Sudoku.Solver.Rules;
using UnityEngine;

namespace Sudoku.UI.Config
{
    /**
     * Serialized runtime configuration model for config-panel options.
     *
     * Adding new options is safe: add fields to section classes with defaults,
     * then keep EnsureInitialized updated so old files keep working.
     */
    [Serializable]
    public sealed class RuntimeConfigData
    {
        public int SchemaVersion = 1;
        public AssistanceConfigData Assistance = new AssistanceConfigData();
        public GenerationConfigData Generation = new GenerationConfigData();
        public ColourConfigData Colours = new ColourConfigData();
        public List<RuleConfigEntry> Rules = new List<RuleConfigEntry>();
    }

    [Serializable]
    public sealed class AssistanceConfigData
    {
        public bool HideApplyRules;
        public bool AutoCandidateOnSetValue = true;
    }

    [Serializable]
    public sealed class GenerationConfigData
    {
        public bool UseRotationalSymmetry = true;
    }

    [Serializable]
    public sealed class ColourConfigData
    {
        // Default: green, amber, and red enabled; blue disabled.
        public bool GreenEnabled = true;
        public bool AmberEnabled = true;
        public bool RedEnabled   = true;
        public bool BlueEnabled  = false;
    }

    [Serializable]
    public sealed class RuleConfigEntry
    {
        public string RuleTypeName;
        public bool Enabled = true;
    }

    /**
     * Defines default config values used when no config file exists.
     *
     * Consumers can replace Factory to provide project-specific defaults.
     */
    public static class RuntimeConfigDefaults
    {
        public static Func<RuntimeConfigData> Factory { get; set; } = CreateBuiltInDefaults;

        /**
         * Build default runtime config values.
         *
         * @returns A fully initialized default config object.
         */
        public static RuntimeConfigData Create()
        {
            var config = (Factory?.Invoke()) ?? CreateBuiltInDefaults();
            RuntimeConfigRepository.EnsureInitialized(config);
            return config;
        }

        private static RuntimeConfigData CreateBuiltInDefaults()
        {
            return new RuntimeConfigData
            {
                SchemaVersion = 1,
                Assistance = new AssistanceConfigData
                {
                    // Show Apply Rules by default.
                    HideApplyRules = false,
                    // Enable auto-candidate behavior on set-value by default.
                    AutoCandidateOnSetValue = true
                },
                Generation = new GenerationConfigData
                {
                    // Keep rotational symmetry enabled for generated puzzles.
                    UseRotationalSymmetry = true
                },
                // Default colours: green, amber, red enabled; blue disabled.
                Colours = new ColourConfigData
                {
                    GreenEnabled = true,
                    AmberEnabled = true,
                    RedEnabled   = true,
                    BlueEnabled  = false
                },
                // Default rule policy: enable all Easy rules, disable all non-Easy rules.
                Rules = CreateBuiltInRuleDefaults()
            };
        }

        /**
         * Create built-in rule defaults mapped by known rule type names.
         *
         * @returns Rule defaults where Easy rules are enabled and all others disabled.
         */
        private static List<RuleConfigEntry> CreateBuiltInRuleDefaults()
        {
            var registry = new RuleRegistry();
            registry.RegisterDefaults();

            var defaults = new List<RuleConfigEntry>();
            var rules = registry.Rules;
            for (int i = 0; i < rules.Count; i++)
            {
                defaults.Add(CreateRuleDefault(rules[i]));
            }

            return defaults;
        }

        /**
         * Build a single rule config default from its difficulty.
         *
         * @param rule Rule instance used to derive type-name and difficulty.
         * @returns A config entry with default enabled state.
         */
        private static RuleConfigEntry CreateRuleDefault(ISudokuRule rule)
        {
            if (rule == null)
            {
                return new RuleConfigEntry();
            }

            return new RuleConfigEntry
            {
                RuleTypeName = rule.GetType().Name,
                Enabled = rule.Difficulty == Difficulty.Easy
            };
        }
    }

    /**
     * Handles disk IO for runtime config JSON.
     */
    public static class RuntimeConfigRepository
    {
        private const string FileName = "RuntimeConfig.json";

        /**
         * Optional path override used by tests.
         */
        public static string OverrideFilePath { get; set; }

        private static string FilePath =>
            OverrideFilePath ?? GetDefaultFilePath();

        private static string GetDefaultFilePath()
        {
    #if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            string documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            return Path.Combine(documentsPath, "My Games", "Last Sudoku", FileName);
    #else
            return Path.Combine(Application.persistentDataPath, FileName);
    #endif
        }

        /**
         * Load config from disk, falling back to defaults when missing/corrupt.
         *
         * @returns Initialized config data.
         */
        public static RuntimeConfigData LoadOrDefault()
        {
            var config = RuntimeConfigDefaults.Create();

            try
            {
                if (!File.Exists(FilePath))
                {
                    return config;
                }

                var json = File.ReadAllText(FilePath);
                if (string.IsNullOrWhiteSpace(json))
                {
                    return config;
                }

                // Overwrite only fields present in json; missing fields keep defaults.
                JsonUtility.FromJsonOverwrite(json, config);
                EnsureInitialized(config);
                return config;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"RuntimeConfigRepository: Failed to load config. {ex.Message}");
                return RuntimeConfigDefaults.Create();
            }
        }

        /**
         * Save config to disk as JSON.
         *
         * @param config Config payload to persist.
         */
        public static void Save(RuntimeConfigData config)
        {
            if (config == null)
            {
                return;
            }

            try
            {
                EnsureInitialized(config);

                var directory = Path.GetDirectoryName(FilePath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var json = JsonUtility.ToJson(config, prettyPrint: true);
                File.WriteAllText(FilePath, json);
            }
            catch (Exception ex)
            {
                Debug.LogError($"RuntimeConfigRepository: Failed to save config. {ex.Message}");
            }
        }

        /**
         * Ensure config and nested sections are always non-null.
         *
         * @param config Config to normalize.
         */
        public static void EnsureInitialized(RuntimeConfigData config)
        {
            if (config == null)
            {
                return;
            }

            if (config.SchemaVersion <= 0)
            {
                config.SchemaVersion = 1;
            }

            config.Assistance ??= new AssistanceConfigData();
            config.Generation ??= new GenerationConfigData();
            config.Colours ??= new ColourConfigData();
            config.Rules ??= new List<RuleConfigEntry>();
        }
    }

    /**
     * Applies runtime config to live settings and persists user changes.
     */
    public static class RuntimeConfigService
    {
        private static RuntimeConfigData _current;

        /**
         * Ensure config is loaded and non-rule settings are applied.
         */
        public static void EnsureLoaded()
        {
            if (_current != null)
            {
                return;
            }

            _current = RuntimeConfigRepository.LoadOrDefault();
            ApplyNonRuleSettings(_current);
        }

        /**
         * Persist current runtime settings, including rule states when registry is available.
         *
         * @param registry Optional rule registry to snapshot current enabled states.
         */
        public static void SaveCurrent(RuleRegistry registry = null)
        {
            EnsureLoaded();

            _current.Assistance.HideApplyRules = AssistanceSettings.HideApplyRules;
            _current.Assistance.AutoCandidateOnSetValue = AssistanceSettings.AutoCandidateOnSetValue;
            _current.Generation.UseRotationalSymmetry = GenerationSettings.UseRotationalSymmetry;

            _current.Colours ??= new ColourConfigData();
            _current.Colours.GreenEnabled = ColourSettings.GreenEnabled;
            _current.Colours.AmberEnabled = ColourSettings.AmberEnabled;
            _current.Colours.RedEnabled   = ColourSettings.RedEnabled;
            _current.Colours.BlueEnabled  = ColourSettings.BlueEnabled;

            if (registry != null)
            {
                _current.Rules.Clear();
                var rules = registry.GetRulesWithStatus();
                for (int i = 0; i < rules.Count; i++)
                {
                    var (rule, enabled) = rules[i];
                    if (rule == null)
                    {
                        continue;
                    }

                    _current.Rules.Add(new RuleConfigEntry
                    {
                        RuleTypeName = rule.GetType().Name,
                        Enabled = enabled
                    });
                }
            }

            RuntimeConfigRepository.Save(_current);
        }

        /**
         * Apply saved rule states to a live registry when available.
         *
         * @param registry Registry to apply saved enabled states to.
         * @param runner Optional runner to notify for UI refresh behavior.
         */
        public static void ApplySavedRuleStates(RuleRegistry registry, SolverRunner runner = null)
        {
            if (registry == null)
            {
                return;
            }

            EnsureLoaded();

            if (_current.Rules == null || _current.Rules.Count == 0)
            {
                return;
            }

            var savedStates = new Dictionary<string, bool>(StringComparer.Ordinal);
            for (int i = 0; i < _current.Rules.Count; i++)
            {
                var entry = _current.Rules[i];
                if (entry == null || string.IsNullOrWhiteSpace(entry.RuleTypeName))
                {
                    continue;
                }

                savedStates[entry.RuleTypeName] = entry.Enabled;
            }

            if (savedStates.Count == 0)
            {
                return;
            }

            var currentStates = registry.GetRulesWithStatus();
            for (int i = 0; i < currentStates.Count; i++)
            {
                var (rule, isEnabled) = currentStates[i];
                if (rule == null)
                {
                    continue;
                }

                var typeName = rule.GetType().Name;
                if (!savedStates.TryGetValue(typeName, out var desiredEnabled))
                {
                    continue;
                }

                if (desiredEnabled == isEnabled)
                {
                    continue;
                }

                registry.SetEnabled(typeName, desiredEnabled);
                runner?.HandleRuleToggleChanged(typeName, desiredEnabled);
            }
        }

        private static void ApplyNonRuleSettings(RuntimeConfigData config)
        {
            if (config == null)
            {
                return;
            }

            AssistanceSettings.HideApplyRules = config.Assistance.HideApplyRules;
            AssistanceSettings.AutoCandidateOnSetValue = config.Assistance.AutoCandidateOnSetValue;
            GenerationSettings.UseRotationalSymmetry = config.Generation.UseRotationalSymmetry;

            // Apply colour settings with safe fallback when the section is absent.
            var colours = config.Colours ?? new ColourConfigData();
            ColourSettings.GreenEnabled = colours.GreenEnabled;
            ColourSettings.AmberEnabled = colours.AmberEnabled;
            ColourSettings.RedEnabled   = colours.RedEnabled;
            ColourSettings.BlueEnabled  = colours.BlueEnabled;
        }
    }
}