using System.Collections.Generic;

namespace Sudoku.Models
{
    /** Summary of a single change entry for display purposes. */
    public class ChangeLogEntrySummary
    {
        public int Row;
        public int Column;
        public int? OldValue;
        public int? NewValue;
        public int RemovedCandidatesCount;
        public int AddedCandidatesCount;
    }

    /** Group-level summary for an atomic rule application recorded in the ChangeLog. */
    public class ChangeLogGroupSummary
    {
        public int GroupId;
        public string RuleName;
        public string Description;
        public int ChangesCount;
        public int ValuesAddedCount;
        public int CandidatesRemovedCount;
        public int CandidatesAddedCount;
        public int StartIndex;
        public int EndIndex; // exclusive
        public List<ChangeLogEntrySummary> Entries = new List<ChangeLogEntrySummary>();
    }
}
