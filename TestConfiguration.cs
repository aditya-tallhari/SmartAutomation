namespace SmartAutomation;

public sealed record TestConfiguration(
    string Url,
    string FirstName,
    string Gender,
    string State)
{
    public static TestConfiguration FromArguments(string[] args)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < args.Length; index++)
        {
            if (!args[index].StartsWith("--", StringComparison.Ordinal) || index + 1 >= args.Length)
            {
                continue;
            }

            values[args[index][2..]] = args[index + 1];
            index++;
        }

        return new TestConfiguration(
            Get(values, "url", "SMART_AUTOMATION_URL",
                "https://app.cloudqa.io/home/AutomationPracticeForm"),
            Get(values, "first-name", "SMART_AUTOMATION_FIRST_NAME", "Aditya"),
            Get(values, "gender", "SMART_AUTOMATION_GENDER", "Male"),
            Get(values, "state", "SMART_AUTOMATION_STATE", "India"));
    }

    private static string Get(
        IReadOnlyDictionary<string, string> arguments,
        string argumentName,
        string environmentName,
        string defaultValue)
    {
        if (arguments.TryGetValue(argumentName, out var argumentValue) &&
            !string.IsNullOrWhiteSpace(argumentValue))
        {
            return argumentValue.Trim();
        }

        return Environment.GetEnvironmentVariable(environmentName)?.Trim() is { Length: > 0 } environmentValue
            ? environmentValue
            : defaultValue;
    }
}
