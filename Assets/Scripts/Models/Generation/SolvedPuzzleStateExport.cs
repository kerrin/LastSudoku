using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using System.Xml.Serialization;
using Sudoku.Solver.Rules;

namespace Sudoku.Models
{
    /**
     * Serializable snapshot of a Sudoku board's current solved-state progress.
     */
    [Serializable]
    public class SolvedPuzzleStateExport
    {
        public string PuzzleCode;
        public string InitialPuzzleCode;
        public int Size;
        public int BoxWidth;
        public int BoxHeight;
        public long SavedAtUtcTicks;
        public int ChangeLogIndex;
        public int NextChangeGroupId;
        /** Accumulated solve time in seconds, persisted so the timer continues after loading. */
        public double ElapsedSeconds;
        public List<SolvedPuzzleCellExport> Cells = new List<SolvedPuzzleCellExport>();
        public List<SolvedPuzzleDirectionalLinkExport> DirectionalLinks = new List<SolvedPuzzleDirectionalLinkExport>();
        public List<SolvedPuzzleChangeLogEntryExport> ChangeLog = new List<SolvedPuzzleChangeLogEntryExport>();

        /** Parameterless constructor required for XML deserialization. */
        public SolvedPuzzleStateExport() { }

        /**
         * Build a serializable export model from the current board.
         *
         * @param board Source board to snapshot.
         * @param puzzleCode Encoded puzzle code representing the current values.
         * @param initialPuzzleCode Encoded puzzle code captured at solve start.
         * @param elapsedSeconds Accumulated solve time in seconds to persist.
         * @returns A new export model containing values, candidates, and changelog.
         */
        public static SolvedPuzzleStateExport FromBoard(Board board, string puzzleCode, string initialPuzzleCode, double elapsedSeconds = 0.0)
        {
            if (board == null)
            {
                throw new ArgumentNullException(nameof(board));
            }

            var export = new SolvedPuzzleStateExport
            {
                PuzzleCode = puzzleCode ?? string.Empty,
                InitialPuzzleCode = initialPuzzleCode ?? string.Empty,
                Size = board.Size,
                BoxWidth = board.BoxWidth,
                BoxHeight = board.BoxHeight,
                SavedAtUtcTicks = DateTime.UtcNow.Ticks,
                ChangeLogIndex = board.ChangeLogIndex,
                NextChangeGroupId = board.NextChangeGroupId,
                ElapsedSeconds = elapsedSeconds >= 0.0 ? elapsedSeconds : 0.0,
            };

            if (board.Cells != null)
            {
                for (int row = 0; row < board.Size; row++)
                {
                    for (int col = 0; col < board.Size; col++)
                    {
                        var cell = board.Cells[row, col];
                        if (cell == null)
                        {
                            continue;
                        }

                        var cellExport = new SolvedPuzzleCellExport
                        {
                            Row = cell.Row,
                            Column = cell.Column,
                            Box = cell.Box,
                            Value = cell.Value,
                            IsGiven = cell.IsGiven,
                            Color = cell.Color.ToString(),
                        };

                        if (cell.Candidates != null)
                        {
                            foreach (int candidate in cell.Candidates)
                            {
                                cellExport.Candidates.Add(candidate);
                            }

                            cellExport.Candidates.Sort();
                        }

                        if (cell.DigitColors != null)
                        {
                            foreach (var digitEntry in cell.DigitColors)
                            {
                                int digit = digitEntry.Key;
                                if (digit < 1 || digit > board.Size)
                                {
                                    continue;
                                }

                                if (digitEntry.Value == null || digitEntry.Value.Count == 0)
                                {
                                    continue;
                                }

                                var colourExport = new SolvedPuzzleDigitColorExport
                                {
                                    Digit = digit,
                                };

                                foreach (HighlightColor colour in digitEntry.Value)
                                {
                                    if (colour == HighlightColor.None)
                                    {
                                        continue;
                                    }

                                    colourExport.Colours.Add((int)colour);
                                }

                                colourExport.Colours.Sort();
                                if (colourExport.Colours.Count > 0)
                                {
                                    cellExport.DigitColors.Add(colourExport);
                                }
                            }

                            cellExport.DigitColors.Sort((a, b) => a.Digit.CompareTo(b.Digit));
                        }

                        export.Cells.Add(cellExport);
                    }
                }
            }

            if (board.ChangeLog != null)
            {
                foreach (CellChange change in board.ChangeLog)
                {
                    if (change == null)
                    {
                        continue;
                    }

                    var changeExport = new SolvedPuzzleChangeLogEntryExport
                    {
                        Row = change.Row,
                        Column = change.Column,
                        OldValue = change.OldValue,
                        NewValue = change.NewValue,
                        ClearValue = change.ClearValue,
                        ForceSetValue = change.ForceSetValue,
                        ValueOnlySet = change.ValueOnlySet,
                        GroupId = change.GroupId,
                        SourceRuleName = change.SourceRuleName ?? string.Empty,
                        SourceRuleDescription = change.SourceRuleDescription ?? string.Empty,
                    };

                    if (change.RemovedCandidates != null)
                    {
                        changeExport.RemovedCandidates.AddRange(change.RemovedCandidates);
                    }

                    if (change.AddedCandidates != null)
                    {
                        changeExport.AddedCandidates.AddRange(change.AddedCandidates);
                    }

                    if (change.OldDirectionalLinks != null)
                    {
                        changeExport.OldDirectionalLinks = ConvertDirectionalLinks(change.OldDirectionalLinks);
                    }

                    if (change.NewDirectionalLinks != null)
                    {
                        changeExport.NewDirectionalLinks = ConvertDirectionalLinks(change.NewDirectionalLinks);
                    }

                    export.ChangeLog.Add(changeExport);
                }
            }

            if (board.DirectionalLinks != null)
            {
                export.DirectionalLinks = ConvertDirectionalLinks(board.DirectionalLinks);
            }

            return export;
        }

        /**
         * Convert runtime directional links into an XML-safe export payload.
         *
         * @param links Runtime directional links.
         * @returns Export payload list.
         */
        private static List<SolvedPuzzleDirectionalLinkExport> ConvertDirectionalLinks(List<DirectionalCellLink> links)
        {
            var converted = new List<SolvedPuzzleDirectionalLinkExport>();
            if (links == null)
            {
                return converted;
            }

            for (int i = 0; i < links.Count; i++)
            {
                var link = links[i];
                if (link == null || link.Start == null || link.End == null)
                {
                    continue;
                }

                converted.Add(new SolvedPuzzleDirectionalLinkExport
                {
                    Kind = (int)link.Kind,
                    StartRow = link.Start.Row,
                    StartColumn = link.Start.Column,
                    StartDigit = link.Start.Digit,
                    EndRow = link.End.Row,
                    EndColumn = link.End.Column,
                    EndDigit = link.End.Digit,
                });
            }

            return converted;
        }
    }

    /**
     * Serializable snapshot for one cell in the solved-state export.
     */
    [Serializable]
    public class SolvedPuzzleCellExport
    {
        public int Row;
        public int Column;
        public int Box;
        public int? Value;
        public bool IsGiven;
        public string Color;
        public List<int> Candidates = new List<int>();
        public List<SolvedPuzzleDigitColorExport> DigitColors = new List<SolvedPuzzleDigitColorExport>();
    }

    /**
     * Serializable colour annotations for a single digit in a cell.
     */
    [Serializable]
    public class SolvedPuzzleDigitColorExport
    {
        public int Digit;
        public List<int> Colours = new List<int>();
    }

    /**
     * Serializable directional link export for solve-mode annotations.
     */
    [Serializable]
    public class SolvedPuzzleDirectionalLinkExport
    {
        public int Kind;
        public int StartRow;
        public int StartColumn;
        public int StartDigit;
        public int EndRow;
        public int EndColumn;
        public int EndDigit;
    }

    /**
     * Serializable snapshot for one changelog entry in the solved-state export.
     */
    [Serializable]
    public class SolvedPuzzleChangeLogEntryExport
    {
        public int Row;
        public int Column;
        public int? OldValue;
        public int? NewValue;
        public bool ClearValue;
        public bool ForceSetValue;
        public bool ValueOnlySet;
        public List<int> RemovedCandidates = new List<int>();
        public List<int> AddedCandidates = new List<int>();
        public List<SolvedPuzzleDirectionalLinkExport> OldDirectionalLinks = new List<SolvedPuzzleDirectionalLinkExport>();
        public List<SolvedPuzzleDirectionalLinkExport> NewDirectionalLinks = new List<SolvedPuzzleDirectionalLinkExport>();
        public int GroupId;
        public string SourceRuleName;
        public string SourceRuleDescription;
    }

    /**
     * Persists solved-state exports to XML under a deterministic location.
     */
    public static class SolvedPuzzleStateXmlExporter
    {
        /**
         * Save a solved puzzle-state export to XML using puzzle code as filename.
         *
         * @param state Export model to serialize.
         * @returns Absolute path to the written XML file.
         */
        public static string Save(SolvedPuzzleStateExport state)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            string outputDirectory = GetOutputDirectory();
            Directory.CreateDirectory(outputDirectory);

            string safeFileName = BuildSafeFileName(state.PuzzleCode);
            string fullPath = Path.Combine(outputDirectory, safeFileName + ".xml");

            var serializer = new XmlSerializer(typeof(SolvedPuzzleStateExport));
            var settings = new XmlWriterSettings
            {
                Indent = true,
                NewLineHandling = NewLineHandling.Entitize,
            };

            using (var stream = File.Create(fullPath))
            using (var writer = XmlWriter.Create(stream, settings))
            {
                serializer.Serialize(writer, state);
            }

            return fullPath;
        }

        /**
         * Resolve the solved-state export directory.
         *
         * @returns Path to the export directory for the current platform.
         */
        public static string GetOutputDirectory()
        {
            string documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

            if (!string.IsNullOrEmpty(documents))
            {
                return Path.Combine(documents, "My Games", "Last Sudoku");
            }

            return Path.Combine(UnityEngine.Application.persistentDataPath, "Last Sudoku");
        }

        /**
         * Sanitize puzzle code to a filename-safe token.
         *
         * @param puzzleCode Code used to derive the filename.
         * @returns Safe filename token without extension.
         */
        public static string BuildSafeFileName(string puzzleCode)
        {
            string raw = string.IsNullOrWhiteSpace(puzzleCode)
                ? DateTime.UtcNow.ToString("yyyyMMdd_HHmmss")
                : puzzleCode.Trim();

            char[] invalidChars = Path.GetInvalidFileNameChars();
            foreach (char invalid in invalidChars)
            {
                raw = raw.Replace(invalid, '_');
            }

            return string.IsNullOrWhiteSpace(raw) ? "sudoku_state" : raw;
        }
    }
}