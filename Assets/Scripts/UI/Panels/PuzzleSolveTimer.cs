using UnityEngine;

namespace Sudoku.UI.Panels
{
    /**
     * Tracks elapsed time for a Solve Puzzle session.
     * Time counts up from zero when a new puzzle starts.
     * The timer stops automatically once the puzzle is validated as complete and correct.
     * Elapsed seconds can be saved to and restored from a save-state export.
     */
    [DisallowMultipleComponent]
    public class PuzzleSolveTimer : MonoBehaviour
    {
        /** Accumulated seconds elapsed since the puzzle started. */
        private double _elapsedSeconds;

        /** Whether the timer is currently counting up. */
        private bool _isRunning;

        /** Whether the puzzle has been completed and the timer permanently stopped. */
        private bool _isStopped;

        /** The last formatted time string, cached to reduce garbage. */
        private string _cachedDisplayString = "0:00:00";

        /** The last elapsed seconds value that was formatted, used to avoid redundant formatting. */
        private double _lastFormattedSeconds = -1.0;

        /** Whether the timer is currently active (running or paused but not completed). */
        public bool IsRunning => _isRunning;

        /** Whether the puzzle is completed and the timer has been permanently stopped. */
        public bool IsStopped => _isStopped;

        /** Current elapsed seconds (read-only). */
        public double ElapsedSeconds => _elapsedSeconds;

        /**
         * Begin counting from zero for a fresh puzzle.
         * Resets any previously stopped or paused state.
         */
        public void StartFresh()
        {
            _elapsedSeconds = 0.0;
            _isRunning = true;
            _isStopped = false;
            _lastFormattedSeconds = -1.0;
        }

        /**
         * Restore the timer from a previously saved elapsed time and resume counting.
         * Use this when loading a saved solve state.
         *
         * @param savedSeconds The elapsed seconds from the save state.
         */
        public void RestoreAndResume(double savedSeconds)
        {
            _elapsedSeconds = savedSeconds >= 0.0 ? savedSeconds : 0.0;
            _isRunning = true;
            _isStopped = false;
            _lastFormattedSeconds = -1.0;
        }

        /**
         * Permanently stop the timer when the puzzle is solved and validated.
         * The timer will not accumulate further time after this call.
         */
        public void StopOnCompletion()
        {
            _isRunning = false;
            _isStopped = true;
        }

        /**
         * Pause the timer (e.g., when returning to menu) without marking it as solved.
         */
        public void Pause()
        {
            _isRunning = false;
        }

        /**
         * Resume the timer after a pause.
         */
        public void Resume()
        {
            if (!_isStopped)
            {
                _isRunning = true;
            }
        }

        /**
         * Get the current elapsed time formatted as H:MM:SS.
         *
         * @returns Formatted time string, e.g. "0:04:37".
         */
        public string GetDisplayString()
        {
            // Only reformat when the displayed integer-second value has changed.
            double floored = System.Math.Floor(_elapsedSeconds);
            if (!Mathf.Approximately((float)(floored - _lastFormattedSeconds), 0f))
            {
                int total = (int)floored;
                int hours = total / 3600;
                int minutes = (total % 3600) / 60;
                int seconds = total % 60;
                _cachedDisplayString = $"{hours}:{minutes:D2}:{seconds:D2}";
                _lastFormattedSeconds = floored;
            }

            return _cachedDisplayString;
        }

        private void Update()
        {
            if (!_isRunning)
            {
                return;
            }

            _elapsedSeconds += Time.unscaledDeltaTime;
        }
    }
}
