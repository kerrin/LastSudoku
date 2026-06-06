using System;
using System.Numerics;
using System.Text;

namespace Sudoku.Models
{
    /**
     * Encodes and decodes Sudoku boards into compact alphanumeric codes.
     * Uses a mixed-case base-53 alphabet to keep codes short while remaining readable.
     * 
     * Encoding strategy:
     * - Extract all 81 cell values (0-9, where 0 represents empty)
     * - Treat as a base-10 number with 81 digits
     * - Convert to base-53 with a safe alphabet that excludes ambiguous symbols
     * - Normalize common ambiguous input during decode (e.g., i/l/I/L -> 1, o/O -> 0, Z -> z)
     */
    public static class PuzzleCodeGenerator
    {
        private const int BoardCellCount = 81;
        private const int MaxBoardDigit = 9;
        private const int SparsePresenceBase = 2;
        private const int SparseValueBase = 9;

        private const char DenseModePrefix = 'D';
        private const char SparseModePrefix = 'P';

        // Base-53 character set:
        // - Digits: 0-9
        // - Lowercase: a-h, j-k, m-n, p-r, t-z (excludes i, l, o, s)
        // - Uppercase: A-H, J-K, M-N, P-R, T-Y (excludes I, L, O, S, Z)
        // Total: 10 + 22 + 21 = 53
        private const string Base53Chars = "0123456789abcdefghjkmnpqrtuvwxyzABCDEFGHJKMNPQRTUVWXY";
        
        // Characters that might be confused with valid characters.
        // Normalize user input to the canonical symbols used by the decoder.
        private static readonly char[] AmbiguousInputs = { 'O', 'o', 'I', 'i', 'L', 'l', 'S', 's', 'Z' };
        private static readonly char[] CorrectedChars = { '0', '0', '1', '1', '1', '1', '5', '5', 'z' };

        /**
         * Encode a Sudoku board into a compact alphanumeric code.
         * 
         * @param board The board to encode.
         * @returns A compact string code representing the board state, or empty string on error.
         */
        public static string EncodeBoardToCode(Board board)
        {
            if (board == null)
            {
                return string.Empty;
            }

            // Extract all 81 cell values (0-9, null becomes 0)
            byte[] cellValues = new byte[BoardCellCount];
            int index = 0;
            
            for (int row = 0; row < board.Size; row++)
            {
                for (int col = 0; col < board.Size; col++)
                {
                    var cell = board.Cells[row, col];
                    if (cell == null)
                    {
                        cellValues[index] = 0;
                    }
                    else
                    {
                        // Convert value (nullable int? to byte, null becomes 0), clamp invalid values.
                        byte value = cell.Value.HasValue ? (byte)cell.Value.Value : (byte)0;
                        cellValues[index] = value <= MaxBoardDigit ? value : (byte)0;
                    }
                    index++;
                }
            }

            string denseCode = string.Concat(DenseModePrefix, EncodeDense(cellValues));
            string sparseCode = string.Concat(SparseModePrefix, EncodeSparse(cellValues));

            return sparseCode.Length < denseCode.Length ? sparseCode : denseCode;
        }

        /**
         * Decode a puzzle code back into a board state.
         * Auto-corrects common input mistakes (O→0, o→0, I→1, l→1).
         * 
         * @param code The puzzle code to decode.
         * @param boardSize The size of the board to create (default 9).
         * @returns A decoded board, or null if the code is invalid.
         */
        public static Board DecodeBoardFromCode(string code, int boardSize = 9)
        {
            if (string.IsNullOrEmpty(code))
            {
                return null;
            }

            // Auto-correct ambiguous input characters
            code = CorrectAmbiguousCharacters(code);

            bool hasExplicitHeader = code.Length >= 2;
            if (!hasExplicitHeader)
            {
                return null;
            }

            byte[] cellValues;
            if (code[0] == SparseModePrefix)
            {
                cellValues = DecodeSparse(code.Substring(1));
            }
            else if (code[0] == DenseModePrefix)
            {
                cellValues = DecodeDense(code.Substring(1));
            }
            else
            {
                return null;
            }

            if (cellValues == null || cellValues.Length != BoardCellCount)
            {
                return null;
            }

            // Create board and populate cells
            var board = new Board(boardSize, 3, 3);
            int index = 0;
            
            for (int row = 0; row < board.Size; row++)
            {
                for (int col = 0; col < board.Size; col++)
                {
                    var cell = new Cell(row, col);
                    byte value = cellValues[index];
                    
                    // 0 means empty, 1-9 are valid values
                    if (value >= 1 && value <= 9)
                    {
                        cell.Value = value;
                    }
                    else if (value == 0)
                    {
                        cell.Value = null;
                    }
                    else
                    {
                        // Invalid cell value
                        return null;
                    }

                    board.Cells[row, col] = cell;
                    index++;
                }
            }

            return board;
        }

        /**
         * Encode all cells directly as a base-10 digit stream then convert to base-53.
         *
         * @param cellValues Flat cell array where 0 means empty and 1..9 are filled values.
         * @returns Dense base-53 payload.
         */
        private static string EncodeDense(byte[] cellValues)
        {
            BigInteger boardNumber = DigitsToBigInteger(cellValues, 10);
            return ConvertToBase53(boardNumber);
        }

        /**
         * Decode dense base-53 payload back to 81 board cell values.
         *
         * @param payload Dense payload without prefix.
         * @returns Decoded 81 values, or null on invalid input.
         */
        private static byte[] DecodeDense(string payload)
        {
            BigInteger boardNumber = ConvertFromBase53(payload);
            if (boardNumber == BigInteger.Zero && payload != "0")
            {
                return null;
            }

            byte[] cellValues = BigIntegerToDigits(boardNumber, BoardCellCount, 10);
            if (cellValues == null)
            {
                return null;
            }

            for (int i = 0; i < cellValues.Length; i++)
            {
                if (cellValues[i] > MaxBoardDigit)
                {
                    return null;
                }
            }

            return cellValues;
        }

        /**
         * Encode sparsely by splitting into an occupancy bitstream and non-zero values.
         * This is typically shorter for boards with many empty cells.
         *
         * @param cellValues Flat cell array where 0 means empty and 1..9 are filled values.
         * @returns Sparse payload without prefix.
         */
        private static string EncodeSparse(byte[] cellValues)
        {
            byte[] occupiedDigits = new byte[BoardCellCount];
            var nonZeroDigits = new System.Collections.Generic.List<byte>(BoardCellCount);

            for (int i = 0; i < BoardCellCount; i++)
            {
                byte value = cellValues[i];
                bool hasValue = value >= 1 && value <= MaxBoardDigit;
                occupiedDigits[i] = hasValue ? (byte)1 : (byte)0;
                if (hasValue)
                {
                    // Convert values 1..9 into base-9 digits 0..8.
                    nonZeroDigits.Add((byte)(value - 1));
                }
            }

            string occupiedCode = ConvertToBase53(DigitsToBigInteger(occupiedDigits, SparsePresenceBase));
            string valuesCode = ConvertToBase53(DigitsToBigInteger(nonZeroDigits.ToArray(), SparseValueBase));

            // Two length characters allow exact split during decode.
            char occupiedLenChar = ToBase53LengthChar(occupiedCode.Length);
            char valuesLenChar = ToBase53LengthChar(valuesCode.Length);

            return string.Concat(occupiedLenChar, valuesLenChar, occupiedCode, valuesCode);
        }

        /**
         * Decode sparse payload back into 81 cell values.
         *
         * @param payload Sparse payload without prefix.
         * @returns Decoded 81 values, or null on invalid input.
         */
        private static byte[] DecodeSparse(string payload)
        {
            if (string.IsNullOrEmpty(payload) || payload.Length < 2)
            {
                return null;
            }

            int occupiedLen = FromBase53LengthChar(payload[0]);
            int valuesLen = FromBase53LengthChar(payload[1]);
            if (occupiedLen <= 0 || valuesLen <= 0)
            {
                return null;
            }

            if (payload.Length != 2 + occupiedLen + valuesLen)
            {
                return null;
            }

            string occupiedCode = payload.Substring(2, occupiedLen);
            string valuesCode = payload.Substring(2 + occupiedLen, valuesLen);

            BigInteger occupiedNumber = ConvertFromBase53(occupiedCode);
            BigInteger valuesNumber = ConvertFromBase53(valuesCode);
            if ((occupiedNumber == BigInteger.Zero && occupiedCode != "0") ||
                (valuesNumber == BigInteger.Zero && valuesCode != "0"))
            {
                return null;
            }

            byte[] occupiedDigits = BigIntegerToDigits(occupiedNumber, BoardCellCount, SparsePresenceBase);
            if (occupiedDigits == null)
            {
                return null;
            }

            int nonZeroCount = 0;
            for (int i = 0; i < occupiedDigits.Length; i++)
            {
                if (occupiedDigits[i] > 1)
                {
                    return null;
                }

                if (occupiedDigits[i] == 1)
                {
                    nonZeroCount++;
                }
            }

            byte[] valueDigits = BigIntegerToDigits(valuesNumber, nonZeroCount, SparseValueBase);
            if (valueDigits == null)
            {
                return null;
            }

            var output = new byte[BoardCellCount];
            int valueIndex = 0;
            for (int i = 0; i < BoardCellCount; i++)
            {
                if (occupiedDigits[i] == 0)
                {
                    output[i] = 0;
                    continue;
                }

                byte base9Digit = valueDigits[valueIndex++];
                if (base9Digit > 8)
                {
                    return null;
                }

                output[i] = (byte)(base9Digit + 1);
            }

            return output;
        }

        /**
         * Convert an array of bytes (cell values) to a BigInteger.
         * Treats the bytes as digits of a base-10 number.
         * 
         * @param cellValues Array of bytes representing cell values (0-9).
         * @returns A BigInteger representation of the board state.
         */
        private static BigInteger DigitsToBigInteger(byte[] cellValues, int numberBase)
        {
            BigInteger result = new BigInteger(0);
            
            foreach (byte b in cellValues)
            {
                result = result * numberBase + b;
            }

            return result;
        }

        /**
         * Convert a BigInteger back to an array of bytes.
         * Extracts digits from the BigInteger as a base-10 number.
         * 
         * @param number The BigInteger to convert.
         * @param expectedLength The expected length of the result array.
         * @returns A byte array of the specified length, or null if conversion fails.
         */
        private static byte[] BigIntegerToDigits(BigInteger number, int expectedLength, int numberBase)
        {
            byte[] result = new byte[expectedLength];
            
            for (int i = expectedLength - 1; i >= 0; i--)
            {
                byte digit = (byte)(number % numberBase);
                result[i] = digit;
                number /= numberBase;
            }

            // If there are remaining digits, the code was invalid (too many cells)
            if (number > 0)
            {
                return null;
            }

            return result;
        }

        /**
         * Convert a small positive length to a single base-53 length character.
         *
         * @param length Length to encode; must be between 1 and 52.
         * @returns Encoded single-character length marker.
         */
        private static char ToBase53LengthChar(int length)
        {
            return (length > 0 && length < Base53Chars.Length) ? Base53Chars[length] : '\0';
        }

        /**
         * Decode a single base-53 length character.
         *
         * @param c Encoded length character.
         * @returns Decoded length, or -1 when invalid.
         */
        private static int FromBase53LengthChar(char c)
        {
            int value = Base53Chars.IndexOf(c);
            return value > 0 ? value : -1;
        }

        /**
         * Convert a BigInteger to a base-53 string using the safe mixed-case alphabet.
         * 
         * @param number The BigInteger to convert.
         * @returns A base-53 string representation.
         */
        private static string ConvertToBase53(BigInteger number)
        {
            if (number == 0)
            {
                return "0";
            }

            var sb = new StringBuilder();
            
            while (number > 0)
            {
                int digit = (int)(number % 53);
                sb.Insert(0, Base53Chars[digit]);
                number /= 53;
            }

            return sb.ToString();
        }

        /**
         * Convert a base-53 string to a BigInteger.
         * 
         * @param code The base-53 string to convert.
         * @returns A BigInteger representation, or BigInteger.Zero if the code is invalid.
         */
        private static BigInteger ConvertFromBase53(string code)
        {
            try
            {
                BigInteger result = new BigInteger(0);
                
                foreach (char c in code)
                {
                    int digit = Base53Chars.IndexOf(c);
                    if (digit < 0)
                    {
                        return BigInteger.Zero;
                    }

                    result = result * 53 + digit;
                }

                return result;
            }
            catch
            {
                return BigInteger.Zero;
            }
        }

        /**
         * Auto-correct common input mistakes where users type confusing characters.
         * O/o -> 0, I/i/L/l -> 1, S/s -> 5, Z -> z.
         * 
         * @param code The input code that may contain ambiguous characters.
         * @returns The corrected code.
         */
        private static string CorrectAmbiguousCharacters(string code)
        {
            var sb = new StringBuilder(code);
            
            for (int i = 0; i < AmbiguousInputs.Length; i++)
            {
                char ambiguous = AmbiguousInputs[i];
                char correct = CorrectedChars[i];
                
                for (int j = 0; j < sb.Length; j++)
                {
                    if (sb[j] == ambiguous)
                    {
                        sb[j] = correct;
                    }
                }
            }

            return sb.ToString();
        }
    }
}
