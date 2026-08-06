using System.IO;
using System.Xml.Serialization;
using NUnit.Framework;
using Sudoku.Models;
using Sudoku.Solver;

namespace Sudoku.Tests.Editor
{
    public class SolvedPuzzleStateExportTests
    {
        [Test]
        public void FromBoard_ExportsDirectionalLinks_AndChangeLogLinkSnapshots()
        {
            var board = TestHelpers.CreateEmptyBoard();

            var addExecution = ManualCellEditCore.ApplyAddDirectionalLink(
                board,
                startRow: 0,
                startColumn: 0,
                startDigit: 1,
                endRow: 0,
                endColumn: 1,
                endDigit: 1,
                kind: DirectionalLinkKind.Strong);
            Assert.IsTrue(addExecution.Applied);

            var export = SolvedPuzzleStateExport.FromBoard(board, "CODE", "INITIAL", elapsedSeconds: 12.5);

            Assert.IsNotNull(export.DirectionalLinks);
            Assert.AreEqual(1, export.DirectionalLinks.Count);
            Assert.AreEqual((int)DirectionalLinkKind.Strong, export.DirectionalLinks[0].Kind);
            Assert.AreEqual(0, export.DirectionalLinks[0].StartRow);
            Assert.AreEqual(1, export.DirectionalLinks[0].EndColumn);

            Assert.IsNotNull(export.ChangeLog);
            Assert.Greater(export.ChangeLog.Count, 0);
            Assert.IsNotNull(export.ChangeLog[0].OldDirectionalLinks);
            Assert.IsNotNull(export.ChangeLog[0].NewDirectionalLinks);
            Assert.AreEqual(0, export.ChangeLog[0].OldDirectionalLinks.Count);
            Assert.AreEqual(1, export.ChangeLog[0].NewDirectionalLinks.Count);
        }

        [Test]
        public void XmlDeserialize_WithoutDirectionalLinksField_KeepsDefaultCollectionsInitialised()
        {
            const string xml = "<SolvedPuzzleStateExport><PuzzleCode>ABC</PuzzleCode><InitialPuzzleCode>ABC</InitialPuzzleCode><Size>9</Size><BoxWidth>3</BoxWidth><BoxHeight>3</BoxHeight><SavedAtUtcTicks>0</SavedAtUtcTicks><ChangeLogIndex>0</ChangeLogIndex><NextChangeGroupId>1</NextChangeGroupId><ElapsedSeconds>0</ElapsedSeconds></SolvedPuzzleStateExport>";

            var serializer = new XmlSerializer(typeof(SolvedPuzzleStateExport));
            SolvedPuzzleStateExport deserialized;
            using (var reader = new StringReader(xml))
            {
                deserialized = serializer.Deserialize(reader) as SolvedPuzzleStateExport;
            }

            Assert.IsNotNull(deserialized);
            Assert.IsNotNull(deserialized.DirectionalLinks);
            Assert.IsNotNull(deserialized.ChangeLog);
            Assert.IsNotNull(deserialized.Cells);
        }
    }
}
