public static class StringHelper
{
    /**
     * Convert a string class name to a more human-readable format.
     * For example, "ManualCellEditCore" becomes "Manual Cell Edit Core".
     */
    public static string ClassNameToDisplayString(string className)
    {
        if (string.IsNullOrEmpty(className))
        {
            return string.Empty;
        }

        return System.Text.RegularExpressions.Regex.Replace(className, "(\\B[A-Z])", " $1");
    }
}