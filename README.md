# LastSudoku
Sudoku game and generator

Local hinting can be provided to co=pilot by editing local.instructions.md and the asking co-pilot:
Refresh your presistent user memory for the changes in the local.instructions.md


Specification:

Inputs
------
Size, e.g. 3 for 3x3
Grid of numbers (of correct size), with required values, and colours, and if part of starting state
Rule set that user is expected to know to solve. Flag rules that must be used to solve?
E.g. 
Each row and column has all numbers once and only once
Each cell has all possible values once

Outputs
-------
Grid of numbers for initial state, with colours
Grid of numbers for completed state, with colours
Difficulty expected
Generation:
One possible method:
Start by creating a grid where numbers are shifted in each new sub-group. E.g.
12|34
34|12
--|--
21|43
43|21
Or
123|456|789
456|789|123
789|123|456
---|---|---
312|645|978
645|978|312
978|312|645
---|---|---
231|564|897
564|897|231
897|231|564
Etc

Then randomly move entire columns left or right, or rows up and down within the bounds of a cell
e.g. 
---|---|---
312|645|978
645|978|312
978|312|645
---|---|---
Moving middle row up
---|---|---
645|978|312
312|645|978
978|312|645
---|---|---


Then randomly move entire cell columns left or right, or rows up and down
E.g.
12|34
34|12
--|--
21|43
43|21
Becomes
21|43
43|21
--|--
12|34
34|12

Do that a random number of times, to create a random layout.
Next do the same to get the required numbers in position, by searching for rows and columns with the numbers in the correct place and swapping them.

Firstly:
Remove 1 number from each row and column
Next:
Pick a rule
Apply it to remove a value (that is not required on that starting grid)
Repeat until no rule can be applied
Lastly:
Solve the sudoku with the available rules (use the simplest rules first)
Track which rules were used
Return puzzle and result

Solution Rules
--------------
1 empty in row, column, 3by3 square
Candidates (revisit affected after new number)
1 Missing Single in 3 width row/column
Naked Singles, 2 x Pairs, 3 x Triples
    e.g, only 2 digits in just two places, remove from other locations
Hidden Pairs/Triples
    e.g. same 2 digits in just two places, remove other options to make naked pairs
Corners
Skyscrapers
Right angles
X-wing
    E.g. pairs of the same number in 2 rows and column, you can remove other occurrences in that row/column
Y-wings
    3 cells in right triangle with pairs of 3 numbers in all 3 combinations
    That means that any cell that see (same row/column/3by3) both squares cannot contain the number in those squares
Swordfish
    Combines X/Y wings to have 3x3 nine-cells patterns
Phistomefel Ring
Uniqueness tests (avoid deadly patterns with multiple solutions)
(Check for errors)
X-Cycles
XY-Chain
3D Medusa  
Jellyfish
Unique Rectangles
Fireworks
SK Loops
Extended Unique Rectangle
Hidden Unique Rectangles
WXYZ Wing
Aligned Pair Exclusion
Exocet
Grouped X-Cycles
Empty Rectangles
Finned X-Wing
Finned Swordfish
Alternative Inference Chains
Digit Forcing Chains
Nishio Forcing Chains
Cell Forcing Chains
Unit Forcing Chains
Quad Forcing Chains
Almost Locked Sets

Advanced Sudoku Puzzle Types to implement later
-----------------------------------------------
Killer Sudoku
Sandwich Sudoku