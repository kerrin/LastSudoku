using System;
using System.Collections.Generic;

namespace Sudoku.Models
{
    /**
     * Link strength used by user-authored directional links in solve mode.
     */
    public enum DirectionalLinkKind
    {
        Strong = 1,
        Weak = 2,
    }

    /**
     * One endpoint of a directional candidate/value link.
     */
    [Serializable]
    public class DirectionalLinkEndpoint : IEquatable<DirectionalLinkEndpoint>
    {
        public int Row;
        public int Column;
        public int Digit;

        /**
         * Create a detached copy of this endpoint.
         *
         * @returns Cloned endpoint instance.
         */
        public DirectionalLinkEndpoint Clone()
        {
            return new DirectionalLinkEndpoint
            {
                Row = Row,
                Column = Column,
                Digit = Digit,
            };
        }

        /**
         * Compare two endpoints for value equality.
         *
         * @param other Endpoint to compare against.
         * @returns True when row, column, and digit are all equal.
         */
        public bool Equals(DirectionalLinkEndpoint other)
        {
            if (other == null)
            {
                return false;
            }

            return Row == other.Row && Column == other.Column && Digit == other.Digit;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as DirectionalLinkEndpoint);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = (hash * 31) + Row;
                hash = (hash * 31) + Column;
                hash = (hash * 31) + Digit;
                return hash;
            }
        }
    }

    /**
     * Directed relationship from one candidate/value endpoint to another.
     */
    [Serializable]
    public class DirectionalCellLink : IEquatable<DirectionalCellLink>
    {
        public DirectionalLinkKind Kind = DirectionalLinkKind.Strong;
        public DirectionalLinkEndpoint Start = new DirectionalLinkEndpoint();
        public DirectionalLinkEndpoint End = new DirectionalLinkEndpoint();

        /**
         * Create a detached copy of this directional link.
         *
         * @returns Cloned directional link.
         */
        public DirectionalCellLink Clone()
        {
            return new DirectionalCellLink
            {
                Kind = Kind,
                Start = Start != null ? Start.Clone() : null,
                End = End != null ? End.Clone() : null,
            };
        }

        /**
         * Compare two links for value equality.
         *
         * @param other Link to compare against.
         * @returns True when kind and both endpoints are equal.
         */
        public bool Equals(DirectionalCellLink other)
        {
            if (other == null)
            {
                return false;
            }

            if (Kind != other.Kind)
            {
                return false;
            }

            bool startsEqual = Start == null ? other.Start == null : Start.Equals(other.Start);
            if (!startsEqual)
            {
                return false;
            }

            bool endsEqual = End == null ? other.End == null : End.Equals(other.End);
            return endsEqual;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as DirectionalCellLink);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = (hash * 31) + (int)Kind;
                hash = (hash * 31) + (Start != null ? Start.GetHashCode() : 0);
                hash = (hash * 31) + (End != null ? End.GetHashCode() : 0);
                return hash;
            }
        }

        /**
         * Clone a directional-link list.
         *
         * @param source Source list, which may be null.
         * @returns Deep clone list or null when source is null.
         */
        public static List<DirectionalCellLink> CloneList(List<DirectionalCellLink> source)
        {
            if (source == null)
            {
                return null;
            }

            var clone = new List<DirectionalCellLink>(source.Count);
            for (int i = 0; i < source.Count; i++)
            {
                var link = source[i];
                if (link != null)
                {
                    clone.Add(link.Clone());
                }
            }

            return clone;
        }
    }
}
