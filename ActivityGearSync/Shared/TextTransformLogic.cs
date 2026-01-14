namespace ActivityGearSync.Shared;

public static class TextTransformLogic
{
    public static class Operations
    {
        public const string Set = "Set new value";
        public const string AddPrefix = "Add prefix";
        public const string AddSuffix = "Add suffix";
        public const string FindReplace = "Find & Replace";

        public static readonly string[] All = [Set, AddPrefix, AddSuffix, FindReplace];
    }

    public static string ApplyOperation(
        string original,
        string operation,
        string? newValue = null,
        string? prefix = null,
        string? suffix = null,
        string? findText = null,
        string? replaceText = null)
    {
        return operation switch
        {
            Operations.Set => newValue ?? "",
            Operations.AddPrefix => (prefix ?? "") + original,
            Operations.AddSuffix => original + (suffix ?? ""),
            Operations.FindReplace => original.Replace(
                findText ?? "",
                replaceText ?? "",
                StringComparison.OrdinalIgnoreCase),
            _ => original
        };
    }
}
