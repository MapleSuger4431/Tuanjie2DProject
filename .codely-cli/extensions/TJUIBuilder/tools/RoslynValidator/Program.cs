// <copyright file="Program.cs" company="Codely">
// Copyright (c) Codely. All rights reserved.
// </copyright>

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using RoslynValidator.Analyzers;

namespace RoslynValidator
{
    /// <summary>
    /// Represents a diagnostic result.
    /// </summary>
    public class DiagnosticResult
    {
        [JsonPropertyName("severity")]
        public string Severity { get; set; } = "info";

        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("message")]
        public string Message { get; set; } = string.Empty;

        [JsonPropertyName("file")]
        public string? File { get; set; }

        [JsonPropertyName("line")]
        public int Line { get; set; }

        [JsonPropertyName("column")]
        public int Column { get; set; }

        [JsonPropertyName("endLine")]
        public int EndLine { get; set; }

        [JsonPropertyName("endColumn")]
        public int EndColumn { get; set; }
    }

    /// <summary>
    /// Represents the input parameters for validation.
    /// </summary>
    public class InputParameters
    {
        [JsonPropertyName("gameName")]
        public string GameName { get; set; } = string.Empty;

        [JsonPropertyName("sourceFiles")]
        public List<string> SourceFiles { get; set; } = new();

        [JsonPropertyName("baseDirectory")]
        public string BaseDirectory { get; set; } = string.Empty;

        [JsonPropertyName("inputContract")]
        public string? InputContract { get; set; }
    }

    /// <summary>
    /// Represents the validation result.
    /// </summary>
    public class ValidationResult
    {
        [JsonPropertyName("inputParameters")]
        public InputParameters InputParameters { get; set; } = new();

        [JsonPropertyName("success")]
        public bool Success { get; set; }

        [JsonPropertyName("errorCount")]
        public int ErrorCount { get; set; }

        [JsonPropertyName("warningCount")]
        public int WarningCount { get; set; }

        [JsonPropertyName("filesValidated")]
        public int FilesValidated { get; set; }

        [JsonPropertyName("referencesLoaded")]
        public int ReferencesLoaded { get; set; }

        /// <summary>
        /// Total elapsed time in milliseconds from start to output.
        /// </summary>
        [JsonPropertyName("elapsedMs")]
        public long ElapsedMs { get; set; }

        [JsonPropertyName("diagnostics")]
        public List<DiagnosticResult> Diagnostics { get; set; } = new();

        [JsonPropertyName("hardGate")]
        public HardGateResult HardGate { get; set; } = new();
    }

    public class HardGateResult
    {
        [JsonPropertyName("profile")]
        public string? Profile { get; set; }

        [JsonPropertyName("passed")]
        public bool Passed { get; set; } = true;

        [JsonPropertyName("blockingDiagnosticIds")]
        public List<string> BlockingDiagnosticIds { get; set; } = new();
    }

    class Program
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };

        // Track loaded DLL paths to avoid duplicates
        private static readonly HashSet<string> LoadedDllPaths = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Reads Unity project path from .com-unity-codely.json and removes "Assets" suffix.
        /// </summary>
        static string? ReadUnityProjectPath()
        {
            try
            {
                var codelyJsonPath = Path.Combine(Directory.GetCurrentDirectory(), ".com-unity-codely.json");
                if (!File.Exists(codelyJsonPath))
                {
                    return null;
                }

                var json = File.ReadAllText(codelyJsonPath);
                using var doc = JsonDocument.Parse(json);
                var projectPath = doc.RootElement.GetProperty("project_path").GetString();

                if (!string.IsNullOrEmpty(projectPath))
                {
                    // Remove "Assets" suffix if present
                    projectPath = projectPath.TrimEnd('/', '\\');
                    if (projectPath.EndsWith("Assets", StringComparison.OrdinalIgnoreCase))
                    {
                        projectPath = Path.GetDirectoryName(projectPath);
                    }
                    return projectPath;
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Warning: Failed to read Unity project path: {ex.Message}");
            }
            return null;
        }

        /// <summary>
        /// Reads the Unity/Tuanjie contents directory from Library/EditorInstance.json.
        /// </summary>
        static string? ReadUnityEditorPath()
        {
            try
            {
                var editorInstancePath = Path.Combine(Directory.GetCurrentDirectory(), "Library", "EditorInstance.json");
                if (!File.Exists(editorInstancePath))
                {
                    return null;
                }

                var json = File.ReadAllText(editorInstancePath);
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("app_contents_path", out var contentsElement))
                {
                    var contentsPath = contentsElement.GetString();
                    if (!string.IsNullOrEmpty(contentsPath) && Directory.Exists(contentsPath))
                    {
                        return contentsPath;
                    }
                }

                var appPath = doc.RootElement.GetProperty("app_path").GetString();
                if (!string.IsNullOrEmpty(appPath))
                {
                    if (Directory.Exists(appPath))
                    {
                        var contentsPath = Path.Combine(appPath, "Contents");
                        return Directory.Exists(contentsPath) ? contentsPath : appPath;
                    }

                    return Path.GetDirectoryName(appPath);
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Warning: Failed to read Unity Editor path: {ex.Message}");
            }
            return null;
        }

        static string? FindUnityMonoProfile(string? editorPath)
        {
            if (string.IsNullOrEmpty(editorPath)) return null;

            var monoRootCandidates = new[]
            {
                Path.Combine(editorPath, "MonoBleedingEdge", "lib", "mono"),
                Path.Combine(editorPath, "Data", "MonoBleedingEdge", "lib", "mono"),
            };
            var profileNames = new[]
            {
                OperatingSystem.IsMacOS() ? "unityjit-macos" :
                    OperatingSystem.IsLinux() ? "unityjit-linux" : "unityjit-win32",
                "unity",
                "4.8-api",
            };
            foreach (var root in monoRootCandidates)
            foreach (var profile in profileNames)
            {
                var candidate = Path.Combine(root, profile);
                if (File.Exists(Path.Combine(candidate, "mscorlib.dll"))) return candidate;
            }

            return null;
        }

        /// <summary>
        /// Gets default define symbols when Bee artifacts are not available.
        /// These are minimal symbols required for basic Unity script validation.
        /// </summary>
        static List<string> GetDefaultDefineSymbols()
        {
            return new List<string>
            {
                "UNITY_EDITOR",
                "UNITY_EDITOR_64",
                "UNITY_EDITOR_WIN",
                "UNITY_2022_3_OR_NEWER",
                "UNITY_2022_2_OR_NEWER",
                "UNITY_2022_1_OR_NEWER",
                "UNITY_2021_3_OR_NEWER",
                "UNITY_2021_2_OR_NEWER",
                "UNITY_2021_1_OR_NEWER",
                "UNITY_2020_3_OR_NEWER",
                "UNITY_2020_2_OR_NEWER",
                "UNITY_2020_1_OR_NEWER",
                "UNITY_2019_4_OR_NEWER",
                "UNITY_2019_3_OR_NEWER",
                "UNITY_2019_2_OR_NEWER",
                "UNITY_2019_1_OR_NEWER",
                "UNITY_2018_4_OR_NEWER",
                "UNITY_2018_3_OR_NEWER",
                "UNITY_2018_2_OR_NEWER",
                "UNITY_2018_1_OR_NEWER",
                "UNITY_2017_4_OR_NEWER",
                "UNITY_2017_3_OR_NEWER",
                "UNITY_2017_2_OR_NEWER",
                "UNITY_2017_1_OR_NEWER",
                "UNITY_5_6_OR_NEWER",
                "UNITY_5_5_OR_NEWER",
                "UNITY_5_4_OR_NEWER",
                "UNITY_5_3_OR_NEWER",
                "PLATFORM_STANDALONE",
                "PLATFORM_STANDALONE_WIN",
                "UNITY_STANDALONE_WIN",
                "UNITY_STANDALONE",
                "ENABLE_MONO",
                "NET_STANDARD_2_1",
                "NETSTANDARD2_1",
                "DEBUG",
                "TRACE"
            };
        }

        /// <summary>
        /// Reads define symbols from Bee compilation artifacts (.rsp files).
        /// These files contain the complete set of preprocessor symbols used by Unity's build system.
        /// Priority: Assembly-CSharp.rsp (user assembly) > System assembly .rsp files
        /// Falls back to default symbols if Bee artifacts are not available.
        /// </summary>
        static List<string> ReadDefineSymbols()
        {
            try
            {
                var beeArtifactsPath = Path.Combine(Directory.GetCurrentDirectory(), "Library", "Bee", "artifacts");
                if (!Directory.Exists(beeArtifactsPath))
                {
                    Console.WriteLine("Info: Bee artifacts not found, using default define symbols");
                    return GetDefaultDefineSymbols();
                }

                // Find all .dag directories (each represents a compilation target)
                var dagDirectories = Directory.GetDirectories(beeArtifactsPath, "*.dag", SearchOption.TopDirectoryOnly);
                if (dagDirectories.Length == 0)
                {
                    Console.WriteLine("Info: No .dag directories found, using default define symbols");
                    return GetDefaultDefineSymbols();
                }

                // Use the first .dag directory (usually the most recent)
                var dagPath = dagDirectories[0];

                // Priority 1: Try to find Assembly-CSharp.rsp (main user assembly)
                var rspFiles = Directory.GetFiles(dagPath, "Assembly-CSharp.rsp", SearchOption.TopDirectoryOnly);

                // Priority 2: Fallback to system assemblies (Unity.Collections, Unity.Mathematics, etc.)
                if (rspFiles.Length == 0)
                {
                    rspFiles = Directory.GetFiles(dagPath, "Unity.*.rsp", SearchOption.TopDirectoryOnly);
                    // Exclude .mvfrm.rsp files (view model format - no defines)
                    rspFiles = rspFiles.Where(f => !f.EndsWith(".mvfrm.rsp")).ToArray();
                    if (rspFiles.Length == 0)
                    {
                        // Priority 3: Last resort - use any .rsp file (excluding .mvfrm.rsp)
                        rspFiles = Directory.GetFiles(dagPath, "*.rsp", SearchOption.TopDirectoryOnly);
                        rspFiles = rspFiles.Where(f => !f.EndsWith(".mvfrm.rsp")).ToArray();
                        if (rspFiles.Length == 0)
                        {
                            Console.WriteLine("Info: No .rsp files found, using default define symbols");
                            return GetDefaultDefineSymbols();
                        }
                    }
                }

                // Read the first .rsp file found
                var rspContent = File.ReadAllLines(rspFiles[0]);
                var symbols = new List<string>();

                foreach (var line in rspContent)
                {
                    var trimmedLine = line.Trim();
                    if (trimmedLine.StartsWith("-define:"))
                    {
                        var symbol = trimmedLine.Substring("-define:".Length).Trim();
                        if (!string.IsNullOrEmpty(symbol))
                        {
                            symbols.Add(symbol);
                        }
                    }
                }

                Console.WriteLine($"Info: Loaded {symbols.Count} define symbols from {Path.GetFileName(rspFiles[0])}");
                return symbols;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Warning: Failed to read define symbols from Bee artifacts: {ex.Message}");
                Console.WriteLine("Info: Falling back to default define symbols");
                return GetDefaultDefineSymbols();
            }
        }

        static int Main(string[] args)
        {
            // Piped consumers (test harnesses, CI) decode UTF-8; an attached console keeps its
            // native codepage so localized diagnostics stay readable in interactive terminals.
            try
            {
                if (Console.IsOutputRedirected) Console.OutputEncoding = System.Text.Encoding.UTF8;
            }
            catch { /* best-effort; encoding must never block validation */ }

            // Start timing
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                // Parse arguments
                string? gameName = null;
                List<string>? sourceFiles = null;
                string? baseDirectory = null;
                bool enableRules = false;
                bool showWarnings = false;
                bool scriptMode = false;
                bool recursive = false;
                string? profile = null;
                string? inputContract = null;
                string? resultPath = null;
                bool summaryJson = false;

                for (int i = 0; i < args.Length; i++)
                {
                    switch (args[i])
                    {
                        case "-g":
                        case "--game-name":
                            if (i + 1 < args.Length)
                            {
                                gameName = args[++i];
                            }
                            break;
                        case "-s":
                        case "--source-files":
                            if (i + 1 < args.Length)
                            {
                                var files = args[++i].Split(',');
                                sourceFiles = files.Select(f => f.Trim()).ToList();
                            }
                            break;
                        case "-b":
                        case "--base-directory":
                            if (i + 1 < args.Length)
                            {
                                baseDirectory = args[++i];
                            }
                            break;
                        case "-r":
                        case "--rules":
                            enableRules = true;
                            break;
                        case "-w":
                        case "--warnings":
                            showWarnings = true;
                            break;
                        case "--script":
                            scriptMode = true;
                            break;
                        case "--recursive":
                            recursive = true;
                            break;
                        case "--profile":
                            if (i + 1 < args.Length)
                            {
                                profile = args[++i];
                            }
                            break;
                        case "--input-contract":
                            if (i + 1 < args.Length)
                            {
                                inputContract = args[++i];
                            }
                            break;
                        case "--result-path":
                            if (i + 1 < args.Length)
                            {
                                resultPath = args[++i];
                            }
                            break;
                        case "--summary-json":
                            summaryJson = true;
                            break;
                        case "-h":
                        case "--help":
                            PrintHelp();
                            return 0;
                    }
                }

                // Validate required parameters
                if (gameName == null || baseDirectory == null || (!recursive && sourceFiles == null))
                {
                    Console.Error.WriteLine("Error: Missing required parameters.");
                    Console.Error.WriteLine("Usage: RoslynValidator -g <game-name> -b <base-directory> (-s <source-files> | --recursive)");
                    Console.Error.WriteLine("Example: RoslynValidator -g FlappyBird3D -s Player.cs,GameManager.cs -b /path/to/scripts");
                    return 1;
                }

                if (profile != null && !string.Equals(profile, "game-scripts", StringComparison.OrdinalIgnoreCase))
                {
                    Console.Error.WriteLine($"Error: Unknown profile '{profile}'. Supported profile: game-scripts");
                    return 1;
                }

                if (recursive)
                {
                    if (!Directory.Exists(baseDirectory))
                    {
                        Console.Error.WriteLine($"Error: Base directory not found: {baseDirectory}");
                        return 1;
                    }
                    sourceFiles = Directory.GetFiles(baseDirectory, "*.cs", SearchOption.AllDirectories)
                        .Select(path => Path.GetRelativePath(baseDirectory, path).Replace('\\', '/'))
                        .OrderBy(path => path, StringComparer.Ordinal)
                        .ToList();
                    if (sourceFiles.Count == 0)
                    {
                        Console.Error.WriteLine($"Error: No .cs files found recursively below: {baseDirectory}");
                        return 1;
                    }
                }

                if (inputContract != null && !recursive)
                {
                    Console.Error.WriteLine("Error: --input-contract requires --recursive so call-site diagnostics use the full game-script set.");
                    return 1;
                }

                // Perform validation
                return ValidateWithDirectParameters(
                    gameName, sourceFiles!, baseDirectory, stopwatch, enableRules, showWarnings, scriptMode, profile,
                    inputContract, resultPath, summaryJson);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error: {ex.Message}");
                return 1;
            }
        }

        static void PrintHelp()
        {
            Console.WriteLine(@"
RoslynValidator - C# Script Validation Tool for Unity Projects (Codely)

Usage: RoslynValidator -g <game-name> -s <source-files> -b <base-directory>

Parameters:
  -g, --game-name <name>          Game name (for result file naming)
  -s, --source-files <files>       C# files to validate (comma-separated)
  -b, --base-directory <path>     Base directory (absolute path)
  -r, --rules                     Enable custom Unity anti-pattern analyzers
  -w, --warnings                  Show warnings in addition to errors
  --script                        Parse and compile Roslyn top-level EditorScript syntax
  --recursive                     Recursively enumerate all .cs files below -b (alternative to -s)
  --profile game-scripts          Enable game-script hard gates (UR032-UR036, UR038, UR039, UR048-UR050)
  --input-contract <path>         Validate compiled input_contract entrypoints; requires --recursive
  --result-path <path>            Write the machine-readable result to this path
  --summary-json                  Print a compact machine-readable summary
  -h, --help                     Show this help message

Examples:
  RoslynValidator -g FlappyBird3D -s Player.cs,GameManager.cs -b D:/project/scripts
  RoslynValidator -g FlappyBird3D -s Player.cs,GameManager.cs -b D:/project/scripts -r

Notes:
  - Unity Editor path is automatically read from Library/EditorInstance.json
  - Unity project path is automatically read from .com-unity-codely.json
  - Define symbols are automatically read from Bee artifacts or use defaults
  - Validation always uses full level (syntax + semantic analysis)
  - Console output shows only errors by default; add -w to include warnings
  - Result file is saved to Temp/result_config_<game-name>.json

Unity DLL Discovery:
  - UnityEngine.dll and all UnityEngine.*.dll modules
  - UnityEditor.dll and all UnityEditor.*.dll modules
  - Library/ScriptAssemblies/*.dll (compiled project assemblies)

Exit codes:
  0 - Validation successful (no errors)
  1 - Validation failed (errors found) or runtime error
");
        }

        static int ValidateWithDirectParameters(string gameName, List<string> sourceFiles, string baseDirectory, System.Diagnostics.Stopwatch stopwatch, bool enableRules, bool showWarnings, bool scriptMode, string? profile, string? inputContract, string? resultPath, bool summaryJson)
        {
            // Auto-detect Unity paths
            var unityProjectPath = ReadUnityProjectPath();
            var unityEditorPath = ReadUnityEditorPath();
            var defineSymbols = ReadDefineSymbols();

            // Resolve source file paths
            var resolvedSourceFiles = ResolvePaths(sourceFiles, baseDirectory);

            if (resolvedSourceFiles.Count == 0)
            {
                Console.Error.WriteLine("Error: No valid source files found.");
                return 1;
            }

            // Console.WriteLine($"Validating {resolvedSourceFiles.Count} source files...");

            var inputEntrypoints = LoadInputEntrypoints(inputContract);

            // Perform validation with full diagnostics (including warnings)
            var result = Validate(
                resolvedSourceFiles, unityProjectPath, unityEditorPath, defineSymbols,
                baseDirectory, true, enableRules, scriptMode, profile, inputEntrypoints);

            // Stop timing and record elapsed time
            stopwatch.Stop();
            result.ElapsedMs = stopwatch.ElapsedMilliseconds;

            // Set input parameters
            result.InputParameters = new InputParameters
            {
                GameName = gameName,
                SourceFiles = sourceFiles,
                BaseDirectory = baseDirectory,
                InputContract = inputContract
            };

            // Serialize result to JSON (for file output)
            var resultJson = JsonSerializer.Serialize(result, JsonOptions);

            // Save result to file
            try
            {
                var tempDir = string.IsNullOrWhiteSpace(resultPath)
                    ? Path.Combine(Directory.GetCurrentDirectory(), "Temp")
                    : Path.GetDirectoryName(Path.GetFullPath(resultPath)) ?? Directory.GetCurrentDirectory();

                // Ensure directory exists
                if (!Directory.Exists(tempDir))
                {
                    Directory.CreateDirectory(tempDir);
                }

                var resultFileName = $"result_{gameName}.json";
                var resultFilePath = string.IsNullOrWhiteSpace(resultPath)
                    ? Path.Combine(tempDir, resultFileName)
                    : Path.GetFullPath(resultPath);

                File.WriteAllText(resultFilePath, resultJson);
                // Console.WriteLine($"Result saved to: {resultFilePath}");
            }
            catch (Exception)
            {
                // Result-file failure is surfaced by the caller when it requires machine-readable output.
            }

            // Output result to console
            PrintResult(result, showWarnings);
            if (summaryJson)
            {
                Console.WriteLine(JsonSerializer.Serialize(new
                {
                    success = result.Success,
                    filesValidated = result.FilesValidated,
                    referencesLoaded = result.ReferencesLoaded,
                    errors = result.ErrorCount,
                    warnings = result.WarningCount,
                    resultPath = string.IsNullOrWhiteSpace(resultPath) ? null : Path.GetFullPath(resultPath)
                }, JsonOptions));
            }

            return result.Success ? 0 : 1;
        }

        static IReadOnlyList<string> LoadInputEntrypoints(string? contractPath)
        {
            if (string.IsNullOrWhiteSpace(contractPath)) return Array.Empty<string>();
            string fullPath = Path.GetFullPath(contractPath);
            if (!File.Exists(fullPath)) throw new FileNotFoundException("Input contract not found", fullPath);
            using var document = JsonDocument.Parse(File.ReadAllText(fullPath));
            JsonElement root = document.RootElement;
            JsonElement contract = root;
            if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("input_contract", out var nested))
                contract = nested;
            if (contract.ValueKind == JsonValueKind.Null) return Array.Empty<string>();
            if (contract.ValueKind != JsonValueKind.Object
                || !contract.TryGetProperty("entry_points", out var entrypoints)
                || entrypoints.ValueKind != JsonValueKind.Array)
                throw new InvalidDataException("Input contract must contain input_contract.entry_points or entry_points array");
            return entrypoints.EnumerateArray().Select(value => value.GetString())
                .Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value!.Trim())
                .Distinct(StringComparer.Ordinal).ToArray();
        }

        static List<string> ResolvePaths(List<string> paths, string baseDir)
        {
            var resolved = new List<string>();
            foreach (var path in paths)
            {
                if (Path.IsPathRooted(path))
                {
                    if (File.Exists(path))
                    {
                        resolved.Add(path);
                    }
                }
                else
                {
                    var fullPath = Path.GetFullPath(Path.Combine(baseDir, path));
                    if (File.Exists(fullPath))
                    {
                        resolved.Add(fullPath);
                    }
                }
            }
            return resolved;
        }

        static ValidationResult Validate(
            List<string> sourceFiles,
            string? unityProjectPath,
            string? unityEditorPath,
            List<string> defineSymbols,
            string baseDir,
            bool includeWarnings = true,
            bool enableRules = false,
            bool scriptMode = false,
            string? profile = null,
            IReadOnlyList<string>? inputEntrypoints = null)
        {
            var result = new ValidationResult
            {
                FilesValidated = sourceFiles.Count
            };

            var allDiagnostics = new List<DiagnosticResult>();
            var syntaxTrees = new List<SyntaxTree>();
            var fileContents = new Dictionary<string, string>();

            // Parse all source files
            foreach (var file in sourceFiles)
            {
                try
                {
                    var content = File.ReadAllText(file);
                    fileContents[file] = content;

                    // Parse with preprocessor directives
                    var preprocessorSymbols = defineSymbols.ToArray();
                    var options = CSharpParseOptions.Default
                        .WithPreprocessorSymbols(preprocessorSymbols)
                        .WithLanguageVersion(LanguageVersion.Latest)
                        .WithKind(scriptMode ? SourceCodeKind.Script : SourceCodeKind.Regular);

                    var syntaxTree = CSharpSyntaxTree.ParseText(content, options, file);
                    syntaxTrees.Add(syntaxTree);

                    // Get syntax diagnostics
                    var syntaxDiagnostics = syntaxTree.GetDiagnostics();
                    foreach (var diag in syntaxDiagnostics)
                    {
                        var diagResult = CreateDiagnosticResult(diag, file);
                        allDiagnostics.Add(diagResult);
                    }
                }
                catch (Exception ex)
                {
                    var errorResult = new DiagnosticResult
                    {
                        Severity = "error",
                        Message = $"Failed to read file: {ex.Message}",
                        File = file
                    };
                    allDiagnostics.Add(errorResult);
                }
            }

            // Always perform semantic analysis (full validation)
            var (semanticDiagnostics, refCount) = PerformSemanticAnalysis(
                syntaxTrees, unityProjectPath, unityEditorPath, baseDir, enableRules, scriptMode, profile,
                inputEntrypoints ?? Array.Empty<string>());
            allDiagnostics.AddRange(semanticDiagnostics);
            result.ReferencesLoaded = refCount;

            // Count errors and warnings from full list
            foreach (var diag in allDiagnostics)
            {
                if (diag.Severity == "error")
                {
                    result.ErrorCount++;
                }
                else if (diag.Severity == "warning")
                {
                    result.WarningCount++;
                }
            }

            // Filter diagnostics based on includeWarnings parameter
            result.Diagnostics = includeWarnings
                ? allDiagnostics
                : allDiagnostics.Where(d => d.Severity == "error").ToList();

            result.Success = result.ErrorCount == 0;
            result.HardGate = new HardGateResult
            {
                Profile = profile,
                Passed = profile == null || result.ErrorCount == 0,
                BlockingDiagnosticIds = string.Equals(profile, "game-scripts", StringComparison.OrdinalIgnoreCase)
                    ? allDiagnostics.Where(d => d.Severity == "error" && !string.IsNullOrEmpty(d.Id))
                        .Select(d => d.Id!).Distinct(StringComparer.Ordinal).OrderBy(id => id, StringComparer.Ordinal).ToList()
                    : new List<string>()
            };
            return result;
        }

        /// <summary>
        /// Loads all built-in custom analyzers.
        /// </summary>
        static ImmutableArray<DiagnosticAnalyzer> LoadCustomAnalyzers(
            string? profile, IReadOnlyList<string> inputEntrypoints)
        {
            var analyzers = new List<DiagnosticAnalyzer>
            {
                new Analyzers.PlaceholderReturnAnalyzer(),      // UR025
                new Analyzers.PropertyDataFlowAnalyzer(),       // UR026 / UR027
                new Analyzers.UnreachableBranchAnalyzer(),      // UR028
                new Analyzers.ResourcesLoadAnalyzer(),          // UR029
                new Analyzers.FindSingletonAnalyzer(),          // UR030
            };

            if (string.Equals(profile, "game-scripts", StringComparison.OrdinalIgnoreCase))
            {
                analyzers.Add(new Analyzers.GameScriptCompletenessAnalyzer()); // UR032-UR036
                analyzers.Add(new Analyzers.FrameworkEventShadowAnalyzer());  // UR038
                analyzers.Add(new Analyzers.UnityScriptFilenameAnalyzer());    // UR039
                analyzers.Add(new Analyzers.ComponentLookupNullAnalyzer());     // UR048-UR050
            }

            return ImmutableArray.CreateRange(analyzers);
        }

        static (List<DiagnosticResult>, int) PerformSemanticAnalysis(
            List<SyntaxTree> syntaxTrees,
            string? unityProjectPath,
            string? unityEditorPath,
            string baseDir,
            bool enableRules = false,
            bool scriptMode = false,
            string? profile = null,
            IReadOnlyList<string>? inputEntrypoints = null)
        {
            var diagnostics = new List<DiagnosticResult>();
            var references = new List<MetadataReference>();

            try
            {
                // Clear loaded DLL tracking
                LoadedDllPaths.Clear();

                // 1. Add core .NET assemblies
                AddCoreReferences(references);

                // 2. Add Unity Editor assemblies (if path provided)
                if (!string.IsNullOrEmpty(unityEditorPath))
                {
                    AddUnityEditorReferences(references, unityEditorPath, diagnostics);
                }
                else
                {
                    Console.Error.WriteLine("Warning: Unity Editor path not found (Library/EditorInstance.json missing). UnityEngine DLLs will not be loaded. Run with Unity Editor open or from a Unity project directory.");
                }

                // 3. Add Unity project assemblies (if path provided)
                if (!string.IsNullOrEmpty(unityProjectPath))
                {
                    AddUnityProjectReferences(references, unityProjectPath, diagnostics);
                }

                // 3.5 Add precompiled DLLs shipped inside packages.
                // These plugin DLLs live under Packages/<pkg>/ and are NOT copied into
                // Library/ScriptAssemblies, so without this step any script using their
                // types fails semantic analysis with CS0012.
                if (!string.IsNullOrEmpty(unityProjectPath))
                {
                    AddPackagePrecompiledReferences(references, unityProjectPath, diagnostics);
                }

                // 4. Add NUnit framework (if project path provided)
                if (!string.IsNullOrEmpty(unityProjectPath))
                {
                    AddNUnitReference(references, unityProjectPath, diagnostics);
                }

                if (references.Count == 0)
                {
                    diagnostics.Add(new DiagnosticResult
                    {
                        Severity = "warning",
                        Message = "No valid references found for semantic analysis"
                    });
                    return (diagnostics, 0);
                }

                // Create compilation
                var compilationOptions = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
                    .WithOverflowChecks(true)
                    .WithDeterministic(true);
                var compilation = scriptMode
                    ? CSharpCompilation.CreateScriptCompilation(
                        "ValidationScript", syntaxTrees.First(), references, compilationOptions)
                    : CSharpCompilation.Create(
                        "ValidationAssembly", syntaxTrees, references, compilationOptions);

                // Get all diagnostics (including semantic and custom analyzers)
                ImmutableArray<Diagnostic> allDiagnostics;

                if (enableRules)
                {
                    var analyzers = LoadCustomAnalyzers(profile, inputEntrypoints ?? Array.Empty<string>());
                    if (analyzers.Length > 0)
                    {
                        var compilationWithAnalyzers = compilation.WithAnalyzers(analyzers);
                        allDiagnostics = compilationWithAnalyzers.GetAllDiagnosticsAsync().Result;
                    }
                    else
                    {
                        allDiagnostics = compilation.GetDiagnostics();
                    }
                }
                else
                {
                    allDiagnostics = compilation.GetDiagnostics();
                }

                foreach (var diag in allDiagnostics)
                {
                    // Skip hidden diagnostics
                    if (diag.Severity == DiagnosticSeverity.Hidden)
                    {
                        continue;
                    }

                    // Get file path from location
                    string? filePath = null;
                    if (diag.Location != null && diag.Location != Location.None)
                    {
                        filePath = diag.Location.SourceTree?.FilePath;
                    }

                    var diagResult = CreateDiagnosticResult(diag, filePath);
                    diagnostics.Add(diagResult);
                }
            }
            catch (Exception ex)
            {
                diagnostics.Add(new DiagnosticResult
                {
                    Severity = "error",
                    Message = $"Semantic analysis failed: {ex.Message}"
                });
            }

            return (diagnostics, references.Count);
        }

        /// <summary>
        /// Adds core .NET runtime assemblies as references.
        /// </summary>
        static void AddCoreReferences(List<MetadataReference> references)
        {
            try
            {
                // A Unity project assembly targets Unity Mono. Mixing its mscorlib with .NET's
                // System.* facade assemblies produces duplicate framework types (for example
                // CS0433 for System.Console). AddUnityEditorReferences supplies one coherent
                // Mono profile when an Editor installation is available.
                if (FindUnityMonoProfile(ReadUnityEditorPath()) != null) return;

                // Use TRUSTED_PLATFORM_ASSEMBLIES which works reliably in both
                // 'dotnet run' and 'PublishSingleFile' modes. It contains the full
                // list of assembly paths that the runtime actually uses.
                var trustedAssemblies = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string;
                if (string.IsNullOrEmpty(trustedAssemblies))
                {
                    // TRUSTED_PLATFORM_ASSEMBLIES may not be available in PublishSingleFile mode.
                    // In that case, core types will be provided by Unity Mono's mscorlib.dll
                    // if the Unity Editor is running. Otherwise, fall back to the runtime directory.
                    var runtimeDir = System.Runtime.InteropServices.RuntimeEnvironment.GetRuntimeDirectory();
                    if (Directory.Exists(runtimeDir))
                    {
                        var coreLibPath = Path.Combine(runtimeDir, "System.Private.CoreLib.dll");
                        if (File.Exists(coreLibPath))
                        {
                            TryAddReference(references, coreLibPath);
                        }
                    }
                    return;
                }

                var assemblyPaths = trustedAssemblies.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);

                // Build a lookup: filename -> full path (take first match)
                var assemblyLookup = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var path in assemblyPaths)
                {
                    var fileName = Path.GetFileName(path);
                    if (!assemblyLookup.ContainsKey(fileName))
                    {
                        assemblyLookup[fileName] = path;
                    }
                }

                // Core assemblies needed for Roslyn compilation.
                var coreAssemblyFiles = new List<string>
                {
                    "System.Private.CoreLib.dll",
                    "System.Runtime.dll",
                    "System.Collections.dll",
                    "System.Linq.dll",
                    "System.Console.dll",
                    "System.Threading.dll",
                    "System.Threading.Tasks.dll",
                    "System.Globalization.dll",
                    "System.IO.dll",
                    "System.Reflection.dll",
                    "System.Text.RegularExpressions.dll",
                    "System.Linq.Expressions.dll",
                    "System.ObjectModel.dll",
                    "System.ComponentModel.dll",
                    "System.Collections.Concurrent.dll",
                    "netstandard.dll",
                };

                foreach (var asmFile in coreAssemblyFiles)
                {
                    if (assemblyLookup.TryGetValue(asmFile, out var fullPath))
                    {
                        TryAddReference(references, fullPath);
                    }
                }
            }
            catch
            {
                // Ignore errors adding core references
            }
        }

        /// <summary>
        /// Adds Unity Editor assemblies (UnityEngine, UnityEditor and their modules).
        /// </summary>
        static void AddUnityEditorReferences(
            List<MetadataReference> references,
            string unityEditorPath,
            List<DiagnosticResult> diagnostics)
        {
            try
            {
                // Resolve path (handle both Editor folder and parent folder)
                var editorPath = unityEditorPath;
                if (!Directory.Exists(Path.Combine(editorPath, "Data")))
                {
                    // Check if it's the root Editor folder
                    if (Directory.Exists(Path.Combine(editorPath, "Editor")))
                    {
                        editorPath = Path.Combine(editorPath, "Editor");
                    }
                }

                // Managed folder contains UnityEngine and UnityEditor DLLs
                var managedPath = Path.Combine(editorPath, "Data", "Managed");
                if (!Directory.Exists(managedPath))
                {
                    // Try alternative path structure
                    managedPath = Path.Combine(editorPath, "Managed");
                }

                if (Directory.Exists(managedPath))
                {
                    // Load UnityEngine.dll (if it exists in root Managed folder)
                    TryAddReference(references, Path.Combine(managedPath, "UnityEngine.dll"));

                    // Load UnityEditor.dll (if it exists in root Managed folder)
                    TryAddReference(references, Path.Combine(managedPath, "UnityEditor.dll"));

                    // Load all UnityEngine.*.dll modules from root Managed folder
                    foreach (var dll in Directory.GetFiles(managedPath, "UnityEngine.*.dll"))
                    {
                        TryAddReference(references, dll);
                    }

                    // Load all UnityEditor.*.dll modules from root Managed folder
                    foreach (var dll in Directory.GetFiles(managedPath, "UnityEditor.*.dll"))
                    {
                        TryAddReference(references, dll);
                    }

                    // Load modules from UnityEngine/ subfolder (68 modules)
                    var engineModulePath = Path.Combine(managedPath, "UnityEngine");
                    if (Directory.Exists(engineModulePath))
                    {
                        foreach (var dll in Directory.GetFiles(engineModulePath, "*.dll"))
                        {
                            TryAddReference(references, dll);
                        }
                    }

                    // Load modules from UnityEditor/ subfolder (16 modules)
                    var editorModulePath = Path.Combine(managedPath, "UnityEditor");
                    if (Directory.Exists(editorModulePath))
                    {
                        foreach (var dll in Directory.GetFiles(editorModulePath, "*.dll"))
                        {
                            TryAddReference(references, dll);
                        }
                    }
                }
                else
                {
                    diagnostics.Add(new DiagnosticResult
                    {
                        Severity = "warning",
                        Message = $"Unity Managed folder not found at: {managedPath}"
                    });
                }

                // Use exactly one platform-appropriate Unity Mono profile. Do not combine it
                // with .NET runtime facade assemblies; both define overlapping framework types.
                var monoLibPath = FindUnityMonoProfile(editorPath);
                if (!string.IsNullOrEmpty(monoLibPath))
                {
                    var monoAssemblies = new[]
                    {
                        "mscorlib.dll",
                        "System.dll",
                        "System.Core.dll",
                        "System.Xml.dll",
                        "System.Xml.Linq.dll",
                        "System.Net.Http.dll",
                        "System.Runtime.Serialization.dll",
                        "System.Json.dll",
                        "Microsoft.CSharp.dll"
                    };

                    foreach (var asm in monoAssemblies)
                    {
                        TryAddReference(references, Path.Combine(monoLibPath, asm));
                    }
                }

                // Load netstandard.dll from Mono Facades (for Unity compatibility)
                var unityJitFacadesPath = string.IsNullOrEmpty(monoLibPath)
                    ? string.Empty : Path.Combine(monoLibPath, "Facades");
                if (Directory.Exists(unityJitFacadesPath))
                {
                    var netstandardPath = Path.Combine(unityJitFacadesPath, "netstandard.dll");
                    // Only add if not already loaded (LoadedDllPaths tracks duplicates)
                    TryAddReference(references, netstandardPath);
                }
            }
            catch (Exception ex)
            {
                diagnostics.Add(new DiagnosticResult
                {
                    Severity = "warning",
                    Message = $"Failed to load Unity Editor references: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// Adds NUnit framework from Unity Test Framework package.
        /// </summary>
        static void AddNUnitReference(
            List<MetadataReference> references,
            string unityProjectPath,
            List<DiagnosticResult> diagnostics)
        {
            try
            {
                var packageCachePath = Path.Combine(unityProjectPath, "Library", "PackageCache");

                // Priority 1: Load NUnit from com.unity.ext.nunit package (Unity's custom NUnit implementation)
                // Use wildcard to match versioned directory names (e.g., com.unity.ext.nunit@1.0.6)
                var extNunitDirs = Directory.GetDirectories(packageCachePath, "com.unity.ext.nunit*");
                foreach (var dir in extNunitDirs)
                {
                    // Search in all subdirectories for nunit.framework.dll
                    foreach (var dll in Directory.GetFiles(dir, "nunit.framework.dll", SearchOption.AllDirectories))
                    {
                        TryAddReference(references, dll, diagnostics);
                    }
                }

                // Priority 2: Load NUnit from Unity Test Framework package
                var testFrameworkDirs = Directory.GetDirectories(packageCachePath, "com.unity.test-framework*");
                foreach (var dir in testFrameworkDirs)
                {
                    foreach (var dll in Directory.GetFiles(dir, "nunit.framework.dll", SearchOption.AllDirectories))
                    {
                        TryAddReference(references, dll, diagnostics);
                    }
                }

                // Priority 3: Check Packages folder (for local packages)
                var packagesNunitDirs = Directory.GetDirectories(Path.Combine(unityProjectPath, "Packages"), "com.unity.ext.nunit*");
                foreach (var dir in packagesNunitDirs)
                {
                    foreach (var dll in Directory.GetFiles(dir, "nunit.framework.dll", SearchOption.AllDirectories))
                    {
                        TryAddReference(references, dll, diagnostics);
                    }
                }
            }
            catch (Exception ex)
            {
                diagnostics.Add(new DiagnosticResult
                {
                    Severity = "warning",
                    Message = $"Failed to load NUnit reference: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// Adds Unity project compiled assemblies from Library/ScriptAssemblies.
        /// </summary>
        static void AddUnityProjectReferences(
            List<MetadataReference> references,
            string unityProjectPath,
            List<DiagnosticResult> diagnostics)
        {
            try
            {
                var scriptAssembliesPath = Path.Combine(unityProjectPath, "Library", "ScriptAssemblies");

                if (Directory.Exists(scriptAssembliesPath))
                {
                    // Load all DLLs from ScriptAssemblies folder
                    foreach (var dll in Directory.GetFiles(scriptAssembliesPath, "*.dll"))
                    {
                        // Skip Editor-only assemblies for runtime validation
                        // (but include them for comprehensive validation)
                        TryAddReference(references, dll);
                    }
                }
                else
                {
                    diagnostics.Add(new DiagnosticResult
                    {
                        Severity = "warning",
                        Message = $"Unity ScriptAssemblies folder not found at: {scriptAssembliesPath}. " +
                                  "Make sure the project has been compiled in Unity at least once."
                    });
                }

                // Also check for PlayerScriptAssemblies (for runtime scripts)
                var playerScriptAssembliesPath = Path.Combine(unityProjectPath, "Library", "PlayerScriptAssemblies");
                if (Directory.Exists(playerScriptAssembliesPath))
                {
                    foreach (var dll in Directory.GetFiles(playerScriptAssembliesPath, "*.dll"))
                    {
                        TryAddReference(references, dll);
                    }
                }
            }
            catch (Exception ex)
            {
                diagnostics.Add(new DiagnosticResult
                {
                    Severity = "warning",
                    Message = $"Failed to load Unity project references: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// Adds precompiled plugin DLLs that ship inside embedded/local packages under Packages/.
        /// Such plugin DLLs are referenced by user scripts but are NOT copied into
        /// Library/ScriptAssemblies, so without loading them here, any script using their types
        /// fails semantic analysis with CS0012 ("defined in an assembly that is not
        /// referenced"). We scan Packages/ for managed DLLs and add them as references.
        /// Editor-only/native/test DLLs are skipped defensively; duplicates are ignored and
        /// individual managed-DLL load failures are reported as warnings.
        /// 
        /// In addition to embedded packages under Packages/, this method also resolves local
        /// packages declared via "file:" dependencies in Packages/manifest.json. These packages
        /// may live outside the project's Packages/ directory (e.g. in .codely-cli/extensions/),
        /// so scanning only Packages/ would miss their DLLs.
        /// </summary>
        static void AddPackagePrecompiledReferences(
            List<MetadataReference> references,
            string unityProjectPath,
            List<DiagnosticResult> diagnostics)
        {
            try
            {
                var packagesPath = Path.Combine(unityProjectPath, "Packages");
                if (!Directory.Exists(packagesPath))
                {
                    return;
                }

                // Collect all directories to scan for managed DLLs.
                var scanRoots = new List<string> { packagesPath };

                // Parse manifest.json for file: dependencies that point outside Packages/.
                // Relative paths in manifest.json are resolved against <project>/Packages.
                var manifestPath = Path.Combine(packagesPath, "manifest.json");
                if (File.Exists(manifestPath))
                {
                    var packagesDir = new DirectoryInfo(packagesPath);
                    using var doc = JsonDocument.Parse(File.ReadAllText(manifestPath));
                    if (doc.RootElement.TryGetProperty("dependencies", out var deps))
                    {
                        foreach (var dep in deps.EnumerateObject())
                        {
                            var value = dep.Value.GetString();
                            if (string.IsNullOrEmpty(value) || !value.StartsWith("file:"))
                                continue;

                            var rawPath = value.Substring(5);
                            var depPath = Path.IsPathRooted(rawPath)
                                ? rawPath
                                : Path.GetFullPath(Path.Combine(packagesPath, rawPath));

                            if (!Directory.Exists(depPath))
                            {
                                diagnostics.Add(new DiagnosticResult
                                {
                                    Severity = "warning",
                                    Message = $"manifest.json file: dependency directory not found: {dep.Name} -> {depPath}"
                                });
                                continue;
                            }

                            // Normalize and skip if it's already inside Packages/ (covered by the
                            // recursive scan above).
                            var normalizedDep = Path.GetFullPath(depPath);
                            var normalizedPackages = Path.GetFullPath(packagesPath);
                            if (normalizedDep.StartsWith(normalizedPackages, StringComparison.OrdinalIgnoreCase))
                                continue;

                            scanRoots.Add(normalizedDep);
                        }
                    }
                }

                foreach (var scanRoot in scanRoots)
                {
                    foreach (var dll in Directory.GetFiles(scanRoot, "*.dll", SearchOption.AllDirectories))
                    {
                        var fileName = Path.GetFileNameWithoutExtension(dll);

                        // Skip obvious non-runtime-reference DLLs to keep references lean and avoid
                        // loading native libs (which MetadataReference can't read anyway -> ignored).
                        if (fileName.EndsWith("Editor", StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        if (!IsManagedAssembly(dll))
                        {
                            continue;
                        }

                        TryAddReference(references, dll, diagnostics);
                    }
                }
            }
            catch (Exception ex)
            {
                diagnostics.Add(new DiagnosticResult
                {
                    Severity = "warning",
                    Message = $"Failed to load package precompiled references: {ex.Message}"
                });
            }
        }

        static bool IsManagedAssembly(string path)
        {
            try
            {
                using var stream = File.OpenRead(path);
                using var reader = new PEReader(stream);
                return reader.HasMetadata && reader.GetMetadataReader().IsAssembly;
            }
            catch (BadImageFormatException)
            {
                return false;
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
        }

        /// <summary>
        /// Tries to add a metadata reference, ignoring duplicates and missing files.
        /// </summary>
        static void TryAddReference(List<MetadataReference> references, string dllPath)
        {
            try
            {
                // Normalize path
                var normalizedPath = Path.GetFullPath(dllPath);

                // Check for duplicates
                if (LoadedDllPaths.Contains(normalizedPath))
                {
                    return;
                }

                if (!File.Exists(normalizedPath))
                {
                    return;
                }

                var reference = MetadataReference.CreateFromFile(normalizedPath);
                references.Add(reference);
                LoadedDllPaths.Add(normalizedPath);
            }
            catch
            {
                // Ignore errors for individual DLLs
            }
        }

        /// <summary>
        /// Tries to add a metadata reference with error reporting.
        /// </summary>
        static void TryAddReference(
            List<MetadataReference> references,
            string dllPath,
            List<DiagnosticResult> diagnostics)
        {
            try
            {
                // Normalize path
                var normalizedPath = Path.GetFullPath(dllPath);

                // Check for duplicates
                if (LoadedDllPaths.Contains(normalizedPath))
                {
                    return;
                }

                if (!File.Exists(normalizedPath))
                {
                    diagnostics.Add(new DiagnosticResult
                    {
                        Severity = "warning",
                        Message = $"DLL not found: {dllPath}"
                    });
                    return;
                }

                var reference = MetadataReference.CreateFromFile(normalizedPath);
                references.Add(reference);
                LoadedDllPaths.Add(normalizedPath);
            }
            catch (Exception ex)
            {
                diagnostics.Add(new DiagnosticResult
                {
                    Severity = "warning",
                    Message = $"Could not load reference: {dllPath} - {ex.Message}"
                });
            }
        }

        static DiagnosticResult CreateDiagnosticResult(Diagnostic diagnostic, string? filePath)
        {
            var result = new DiagnosticResult
            {
                Severity = diagnostic.Severity switch
                {
                    DiagnosticSeverity.Error => "error",
                    DiagnosticSeverity.Warning => "warning",
                    DiagnosticSeverity.Info => "info",
                    _ => "hidden"
                },
                Id = diagnostic.Id,
                Message = diagnostic.GetMessage(),
                File = filePath
            };

            var location = diagnostic.Location;
            if (location != null && location != Location.None)
            {
                var lineSpan = location.GetLineSpan();
                if (lineSpan.IsValid)
                {
                    result.Line = lineSpan.StartLinePosition.Line + 1;
                    result.Column = lineSpan.StartLinePosition.Character + 1;
                    result.EndLine = lineSpan.EndLinePosition.Line + 1;
                    result.EndColumn = lineSpan.EndLinePosition.Character + 1;
                }
            }

            return result;
        }

        static void PrintResult(ValidationResult result, bool showWarnings = false)
        {
            if (result.ErrorCount > 0 || (showWarnings && result.WarningCount > 0))
            {
                var parts = new List<string>();
                if (result.ErrorCount > 0) parts.Add($"Error Count: {result.ErrorCount}");
                if (showWarnings && result.WarningCount > 0) parts.Add($"Warning Count: {result.WarningCount}");
                // FAILED only when errors actually block validation; a warnings-only run passes
                // (exit code 0, success=true) and must not claim failure.
                Console.WriteLine(result.ErrorCount > 0
                    ? $"FAILED - {string.Join(", ", parts)}"
                    : $"SUCCESS - {string.Join(", ", parts)}");
                Console.WriteLine();

                foreach (var diag in result.Diagnostics)
                {
                    if (diag.Severity == "error")
                    {
                        var location = diag.File != null
                            ? $"{DisplayPath(diag.File, result.InputParameters.BaseDirectory)}({diag.Line},{diag.Column})"
                            : "";

                        var id = !string.IsNullOrEmpty(diag.Id) ? $" [{diag.Id}]" : "";
                        Console.WriteLine($"ERROR{id}: {diag.Message}");
                        if (!string.IsNullOrEmpty(location))
                        {
                            Console.WriteLine($"  at {location}");
                        }
                    }
                    else if (showWarnings && diag.Severity == "warning")
                    {
                        var location = diag.File != null
                            ? $"{DisplayPath(diag.File, result.InputParameters.BaseDirectory)}({diag.Line},{diag.Column})"
                            : "";

                        var id = !string.IsNullOrEmpty(diag.Id) ? $" [{diag.Id}]" : "";
                        Console.WriteLine($"WARNING{id}: {diag.Message}");
                        if (!string.IsNullOrEmpty(location))
                        {
                            Console.WriteLine($"  at {location}");
                        }
                    }
                }
            }
            else
            {
                Console.WriteLine("SUCCESS");
            }
        }

        static string DisplayPath(string file, string baseDirectory)
        {
            try
            {
                var relative = Path.GetRelativePath(Path.GetFullPath(baseDirectory), Path.GetFullPath(file));
                return relative.Replace('\\', '/');
            }
            catch
            {
                return file.Replace('\\', '/');
            }
        }
    }
}