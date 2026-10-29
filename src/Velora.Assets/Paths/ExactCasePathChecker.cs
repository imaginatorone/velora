namespace Velora.Assets.Paths;

internal enum ExactCaseStatus
{
    Exact,
    Missing,
    Mismatch,
}

internal sealed record ExactCaseResult(
    ExactCaseStatus Status,
    string ActualRelativePath,
    string? SuggestedRelativePath = null);

internal static class ExactCasePathChecker
{
    public static ExactCaseResult Check(string canonicalRoot, string portableRelativePath)
    {
        var requestedComponents = portableRelativePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var actualComponents = new List<string>(requestedComponents.Length);
        var currentDirectory = canonicalRoot;
        var foundMismatch = false;

        for (var index = 0; index < requestedComponents.Length; index++)
        {
            if (!Directory.Exists(currentDirectory))
            {
                return new ExactCaseResult(
                    ExactCaseStatus.Missing,
                    string.Join('/', actualComponents));
            }

            var entries = Directory
                .EnumerateFileSystemEntries(currentDirectory)
                .Select(static entry => new
                {
                    FullPath = entry,
                    Name = Path.GetFileName(entry),
                })
                .OrderBy(static entry => entry.Name, StringComparer.Ordinal)
                .ToArray();

            var requested = requestedComponents[index];
            var exact = entries.FirstOrDefault(entry =>
                string.Equals(entry.Name, requested, StringComparison.Ordinal));
            if (exact is null)
            {
                var mismatched = entries.FirstOrDefault(entry =>
                    string.Equals(entry.Name, requested, StringComparison.OrdinalIgnoreCase));
                if (mismatched is null)
                {
                    return new ExactCaseResult(
                        ExactCaseStatus.Missing,
                        string.Join('/', actualComponents));
                }

                exact = mismatched;
                foundMismatch = true;
            }

            actualComponents.Add(exact.Name);
            currentDirectory = exact.FullPath;
        }

        var actualRelativePath = string.Join('/', actualComponents);
        return foundMismatch
            ? new ExactCaseResult(ExactCaseStatus.Mismatch, actualRelativePath, actualRelativePath)
            : new ExactCaseResult(ExactCaseStatus.Exact, actualRelativePath);
    }
}
