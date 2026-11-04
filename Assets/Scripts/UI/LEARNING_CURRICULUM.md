# Learning hub curriculum

## Implementation sequence

1. **Learning hub foundation** - a runtime entry point, reusable lesson/wiki presentation, persistent completion state, and answerable example interactions.
2. **Beginner onboarding** - Sudoku's row/column/box restrictions, candidates, and app controls for cell edits, help-panel visibility, and change history.
3. **Easy techniques** - Naked Single and Hidden Single, each with a guided example that asks the player to place a value.
4. **Medium techniques** - Naked Pair, pointing/box-line interactions, then hidden subsets and triples.
5. **Advanced techniques** - wings, fish, colouring, uniqueness, and forcing chains. Generate examples from solver fixtures where reliable; keep curated static diagrams for chain-heavy techniques.
6. **In-game wiki** - reference articles for the foundations, app controls, and every registered solving technique; link articles to corresponding tutorial lessons as those lessons are added.
7. **Validation and polish** - test answer validation and lesson ordering, verify input at different resolutions, and refine content against actual in-game controls.

## Current milestone

The shared runtime hub is available from the main menu and from the solve-view side panel. In solve view, both Learn and Wiki render through the board's final IMGUI pass so the board stays visible behind an opaque, interactive learning window. Sudoku Basics keeps its example answer unselected and unrevealed; subsequent lessons use the game's radial menu with unrelated actions faded and require the player to hold the left mouse button while moving to an action, then release to apply it. The wiki is grouped into sections and subsections, with cross-references styled as inline links in article explanations. Interactive examples cover Sudoku basics, app controls, candidates, Naked Single, Hidden Single, Naked Pair, and Pointing Pair. Remaining work is to add interactive lessons for the remaining rules, improve example generation, and link each wiki entry to its lesson.
