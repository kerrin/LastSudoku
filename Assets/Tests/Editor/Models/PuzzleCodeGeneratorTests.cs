using NUnit.Framework;
using Sudoku.Models;

namespace Sudoku.Tests.Models
{
    /**
     * Unit tests for the PuzzleCodeGenerator encoding and decoding functionality.
     * Tests verify that boards can be encoded to codes and decoded back consistently.
     */
    public class PuzzleCodeGeneratorTests
    {
        /**
         * Test encoding and decoding an empty board (all zeros).
         */
        [Test]
        public void EncodeDecode_EmptyBoard_RoundTrips()
        {
            // Arrange: Create an empty 9x9 board
            var emptyBoard = CreateEmptyBoard();

            // Act: Encode the board
            string code = PuzzleCodeGenerator.EncodeBoardToCode(emptyBoard);

            // Assert: Code should not be empty
            Assert.IsNotEmpty(code, "Code should not be empty for an empty board");

            // Act: Decode the code back
            var decodedBoard = PuzzleCodeGenerator.DecodeBoardFromCode(code);

            // Assert: Decoded board should match the original
            AssertBoardsEqual(emptyBoard, decodedBoard);
        }

        /**
         * Test encoding and decoding a board with some filled cells.
         */
        [Test]
        public void EncodeDecode_PartiallyFilledBoard_RoundTrips()
        {
            // Arrange: Create a board with some values
            var board = CreateEmptyBoard();
            board.Cells[0, 0].Value = 1;
            board.Cells[0, 1].Value = 2;
            board.Cells[0, 2].Value = 3;
            board.Cells[1, 0].Value = 4;
            board.Cells[1, 1].Value = 5;
            board.Cells[1, 2].Value = 6;
            board.Cells[8, 8].Value = 9;

            // Act: Encode the board
            string code = PuzzleCodeGenerator.EncodeBoardToCode(board);

            // Assert: Code should not be empty
            Assert.IsNotEmpty(code, "Code should not be empty");

            // Act: Decode the code back
            var decodedBoard = PuzzleCodeGenerator.DecodeBoardFromCode(code);

            // Assert: Decoded board should match the original
            AssertBoardsEqual(board, decodedBoard);
        }

        /**
         * Test encoding and decoding a completely filled board.
         */
        [Test]
        public void EncodeDecode_CompletelyFilledBoard_RoundTrips()
        {
            // Arrange: Create a board with all cells filled (with values 1-9 repeating)
            var board = CreateEmptyBoard();
            int cellIndex = 0;
            for (int row = 0; row < 9; row++)
            {
                for (int col = 0; col < 9; col++)
                {
                    board.Cells[row, col].Value = (cellIndex % 9) + 1; // 1-9
                    cellIndex++;
                }
            }

            // Act: Encode the board
            string code = PuzzleCodeGenerator.EncodeBoardToCode(board);

            // Assert: Code should not be empty
            Assert.IsNotEmpty(code, "Code should not be empty");

            // Act: Decode the code back
            var decodedBoard = PuzzleCodeGenerator.DecodeBoardFromCode(code);

            // Assert: Decoded board should match the original
            AssertBoardsEqual(board, decodedBoard);
        }

        /**
         * Test that ambiguous characters are auto-corrected during decoding.
         */
        [Test]
        public void DecodeBoardFromCode_WithAmbiguousCharacters_CorrectsThem()
        {
            // Arrange: Create a simple board and encode it
            var originalBoard = CreateEmptyBoard();
            originalBoard.Cells[0, 0].Value = 1;
            string correctCode = PuzzleCodeGenerator.EncodeBoardToCode(originalBoard);

            // Act: Manually replace safe characters with ambiguous ones
            // (This test assumes the code contains '0' and '1')
            string ambiguousCode = correctCode;
            if (correctCode.Contains("0"))
            {
                ambiguousCode = ambiguousCode.Replace("0", "O"); // O should convert to 0
            }
            if (correctCode.Contains("1"))
            {
                ambiguousCode = ambiguousCode.Replace("1", "I"); // I should convert to 1
            }

            // Act: Decode with ambiguous characters
            var decodedBoard = PuzzleCodeGenerator.DecodeBoardFromCode(ambiguousCode);

            // Assert: Should decode successfully despite ambiguous input
            Assert.IsNotNull(decodedBoard, "Should successfully decode with ambiguous characters");
            AssertBoardsEqual(originalBoard, decodedBoard);
        }

        /**
         * Test that null board returns empty code.
         */
        [Test]
        public void EncodeBoardToCode_WithNullBoard_ReturnsEmpty()
        {
            // Act
            string code = PuzzleCodeGenerator.EncodeBoardToCode(null);

            // Assert
            Assert.IsEmpty(code);
        }

        /**
         * Test that empty code returns null board.
         */
        [Test]
        public void DecodeBoardFromCode_WithEmptyCode_ReturnsNull()
        {
            // Act
            var board = PuzzleCodeGenerator.DecodeBoardFromCode("");

            // Assert
            Assert.IsNull(board);
        }

        /**
         * Test that codes without an explicit mode header are rejected.
         */
        [Test]
        public void DecodeBoardFromCode_WithoutModeHeader_ReturnsNull()
        {
            // Act
            var board = PuzzleCodeGenerator.DecodeBoardFromCode("12345abc");

            // Assert
            Assert.IsNull(board, "Decoder should require explicit D: or P: header.");
        }

        /**
         * Test that invalid base-alphabet characters are rejected.
         */
        [Test]
        public void DecodeBoardFromCode_WithInvalidCharacters_ReturnsNull()
        {
            // Act: Try to decode with invalid character (e.g., '#' is not in the base alphabet)
            var board = PuzzleCodeGenerator.DecodeBoardFromCode("invalid#code");

            // Assert
            Assert.IsNull(board, "Should return null for invalid base-alphabet characters");
        }

        /**
         * Test that code is deterministic (same board always produces same code).
         */
        [Test]
        public void EncodeBoardToCode_Deterministic_ProducesSameCodeForSameBoard()
        {
            // Arrange: Create a board
            var board = CreateEmptyBoard();
            board.Cells[0, 0].Value = 5;
            board.Cells[4, 4].Value = 9;
            board.Cells[8, 8].Value = 1;

            // Act: Encode multiple times
            string code1 = PuzzleCodeGenerator.EncodeBoardToCode(board);
            string code2 = PuzzleCodeGenerator.EncodeBoardToCode(board);
            string code3 = PuzzleCodeGenerator.EncodeBoardToCode(board);

            // Assert: All codes should be identical
            Assert.AreEqual(code1, code2, "Encoding should be deterministic");
            Assert.AreEqual(code2, code3, "Encoding should be deterministic");
        }

        /**
         * Test code length is reasonable and uses the larger mixed-case base efficiently.
         */
        [Test]
        public void EncodeBoardToCode_CodeLength_IsReasonablyCompact()
        {
            // Arrange: Create a full board
            var board = CreateEmptyBoard();
            for (int row = 0; row < 9; row++)
            {
                for (int col = 0; col < 9; col++)
                {
                    board.Cells[row, col].Value = ((row + col) % 9) + 1;
                }
            }

            // Act
            string code = PuzzleCodeGenerator.EncodeBoardToCode(board);

                // Assert: Code should be much shorter than 81 characters (which would be direct decimal)
                Assert.Less(code.Length, 55, $"Code should be reasonably compact, got {code.Length} chars");
                Assert.Greater(code.Length, 30, "Code should contain meaningful data");
        }

        /**
            * Test that encoded codes contain only valid base-53 characters.
         */
        [Test]
        public void EncodeBoardToCode_ContainsOnlyValidCharacters()
        {
            // Arrange: Create a full board
            var board = CreateEmptyBoard();
            for (int row = 0; row < 9; row++)
            {
                for (int col = 0; col < 9; col++)
                {
                    board.Cells[row, col].Value = ((row + col) % 9) + 1;
                }
            }

            // Act
            string code = PuzzleCodeGenerator.EncodeBoardToCode(board);

            // Assert: Every character must be from the allowed alphabet.
            const string allowedChars = "DP0123456789abcdefghjkmnpqrtuvwxyzABCDEFGHJKMNPQRTUVWXY";
            foreach (char symbol in code)
            {
                Assert.True(allowedChars.Contains(symbol.ToString()), $"Invalid symbol '{symbol}' in code: {code}");
            }
        }

        /**
         * Test that encoded output includes a valid mode prefix for adaptive decoding.
         */
        [Test]
        public void EncodeBoardToCode_ContainsValidModePrefix()
        {
            // Arrange
            var board = CreateEmptyBoard();
            board.Cells[0, 0].Value = 7;

            // Act
            string code = PuzzleCodeGenerator.EncodeBoardToCode(board);

            // Assert
            Assert.IsNotEmpty(code, "Code should not be empty.");
            Assert.True(code[0] == 'D' || code[0] == 'P', $"Unexpected mode prefix '{code[0]}' in code: {code}");
            Assert.Greater(code.Length, 1, $"Expected payload after mode prefix in code: {code}");
        }

        /**
         * Sparse boards should benefit from sparse encoding and remain short for manual entry.
         */
        [Test]
        public void EncodeBoardToCode_SparseBoard_StaysCompact()
        {
            // Arrange: Typical sparse puzzle with a handful of givens.
            var board = CreateEmptyBoard();
            board.Cells[0, 0].Value = 5;
            board.Cells[0, 4].Value = 3;
            board.Cells[1, 3].Value = 8;
            board.Cells[3, 2].Value = 6;
            board.Cells[4, 4].Value = 7;
            board.Cells[6, 1].Value = 2;
            board.Cells[8, 8].Value = 9;

            // Act
            string code = PuzzleCodeGenerator.EncodeBoardToCode(board);
            var decoded = PuzzleCodeGenerator.DecodeBoardFromCode(code);

            // Assert
            AssertBoardsEqual(board, decoded);
            Assert.Less(code.Length, 30, $"Sparse board code should be compact, got {code.Length}: {code}");
        }

        /**
         * Test that ambiguous input characters are normalized to valid canonical symbols.
         */
        [Test]
        public void DecodeBoardFromCode_NormalizesAmbiguousCharacters()
        {
            // Arrange
            var originalBoard = CreateEmptyBoard();
            originalBoard.Cells[0, 0].Value = 1;
            originalBoard.Cells[8, 8].Value = 9;

            string code = PuzzleCodeGenerator.EncodeBoardToCode(originalBoard);

            // Act
            string normalizedCode = code
                .Replace('1', 'I')
                .Replace('0', 'O')
                .Replace('z', 'Z');

            var decodedBoard = PuzzleCodeGenerator.DecodeBoardFromCode(normalizedCode);

            // Assert
            Assert.IsNotNull(decodedBoard, "Decoder should normalize ambiguous symbols and decode successfully.");
            AssertBoardsEqual(originalBoard, decodedBoard);
        }

        // ============================================
        // Helper methods
        // ============================================

        /**
         * Create an empty 9x9 board with all cells uninitialized.
         */
        private static Board CreateEmptyBoard()
        {
            var board = new Board(9, 3, 3);
            for (int row = 0; row < 9; row++)
            {
                for (int col = 0; col < 9; col++)
                {
                    board.Cells[row, col] = new Cell(row, col, value: null);
                }
            }
            return board;
        }

        /**
         * Assert that two boards have identical cell values.
         */
        private static void AssertBoardsEqual(Board expected, Board actual)
        {
            Assert.IsNotNull(actual, "Decoded board should not be null");
            Assert.AreEqual(expected.Size, actual.Size, "Board sizes should match");

            for (int row = 0; row < expected.Size; row++)
            {
                for (int col = 0; col < expected.Size; col++)
                {
                    int? expectedValue = expected.Cells[row, col].Value;
                    int? actualValue = actual.Cells[row, col].Value;

                    Assert.AreEqual(
                        expectedValue,
                        actualValue,
                        $"Cell [{row}, {col}] should have value {expectedValue} but got {actualValue}"
                    );
                }
            }
        }
    }
}
