using OpenQA.Selenium;

namespace SmartAutomation;

public sealed class SmartElementFinder
{
    private const int MinimumScore = 70;
    private const int AmbiguityGap = 15;

    private readonly IWebDriver _driver;

    public SmartElementFinder(IWebDriver driver)
    {
        _driver = driver;
    }

    public IWebElement FindFirstName()
    {
        return FindBest(
            "First Name",
            _driver.FindElements(By.CssSelector("input:not([type='hidden']):not([type='radio']):not([type='checkbox'])")),
            element => ScoreTextCandidate(element, ["first name", "firstname"], 25));
    }

    public IWebElement FindGender(string requestedGender)
    {
        var radios = _driver.FindElements(By.CssSelector("input[type='radio']"));
        return FindBest(
            $"Gender option '{requestedGender}'",
            radios,
            element => ScoreRadioCandidate(element, requestedGender));
    }

    public IWebElement FindState(string requestedState)
    {
        return FindBest(
            "State dropdown",
            _driver.FindElements(By.TagName("select")),
            element => ScoreSelectCandidate(element, requestedState));
    }

    private IWebElement FindBest(
        string fieldName,
        IReadOnlyCollection<IWebElement> candidates,
        Func<IWebElement, CandidateScore> scorer)
    {
        var scored = candidates
            .Where(element => element.Displayed && element.Enabled)
            .GroupBy(element => new
            {
                Tag = element.TagName,
                Id = element.GetAttribute("id"),
                Name = element.GetAttribute("name"),
                Value = element.GetAttribute("value")
            })
            .Select(group => group.First())
            .Select(element => (Element: element, Score: scorer(element)))
            .Where(item => item.Score.IsValid)
            .OrderByDescending(item => item.Score.Value)
            .ToList();

        if (scored.Count == 0)
        {
            throw new SmartElementException(
                $"{fieldName} was not found. Add a meaningful label, accessible name, placeholder, or nearby field context.");
        }

        if (scored.Count > 1 && scored[0].Score.Value - scored[1].Score.Value < AmbiguityGap)
        {
            throw new SmartElementException(
                $"{fieldName} is ambiguous. Top candidates: {Describe(scored[0].Element)} and " +
                $"{Describe(scored[1].Element)}.");
        }

        return scored[0].Element;
    }

    private CandidateScore ScoreTextCandidate(
        IWebElement element,
        IReadOnlyCollection<string> labels,
        int contextWeight)
    {
        var score = ScoreSemanticSignals(element, labels, contextWeight);
        if (!IsTextInput(element))
        {
            return CandidateScore.Invalid;
        }

        return new CandidateScore(score + 20, true);
    }

    private CandidateScore ScoreRadioCandidate(IWebElement element, string requestedGender)
    {
        var optionLabels = GetLabels(element);
        var context = GetContextText(element);
        var score = 20;
        var radioValue = element.GetAttribute("value");
        var matchesLabel = optionLabels.Any(label => EqualsNormalized(label, requestedGender));
        var matchesValue = EqualsNormalized(radioValue, requestedGender);

        if (matchesLabel)
        {
            score += 100;
        }
        else if (matchesValue)
        {
            score += 90;
        }
        else
        {
            return CandidateScore.Invalid;
        }

        if (ContainsNormalized(context, "gender"))
        {
            score += 60;
        }

        if (string.Equals(element.GetAttribute("name"), "gender", StringComparison.OrdinalIgnoreCase))
        {
            score += 20;
        }

        return new CandidateScore(score, true);
    }

    private CandidateScore ScoreSelectCandidate(IWebElement element, string requestedState)
    {
        var score = ScoreSemanticSignals(element, ["state"], 30);
        var options = element.FindElements(By.TagName("option"));
        if (options.Any(option => ContainsNormalized(option.Text, requestedState)))
        {
            score += 40;
        }

        return new CandidateScore(score + 20, score >= MinimumScore);
    }

    private int ScoreSemanticSignals(
        IWebElement element,
        IReadOnlyCollection<string> expectedLabels,
        int contextWeight)
    {
        var score = 0;
        var labelText = string.Join(" ", GetLabels(element));
        var accessibleName = string.Join(" ", new[]
        {
            element.GetAttribute("aria-label"),
            GetReferencedLabelText(element),
            element.GetAttribute("title"),
            element.GetAttribute("name")
        }.Where(value => !string.IsNullOrWhiteSpace(value)));
        var placeholder = element.GetAttribute("placeholder");
        var context = GetContextText(element);

        if (expectedLabels.Any(label => ContainsNormalized(labelText, label)))
        {
            score += 100;
        }

        if (expectedLabels.Any(label => ContainsNormalized(accessibleName, label)))
        {
            score += 90;
        }

        if (expectedLabels.Any(label => ContainsNormalized(placeholder, label)))
        {
            score += 80;
        }

        if (expectedLabels.Any(label => ContainsNormalized(context, label)))
        {
            score += contextWeight;
        }

        return score;
    }

    private static bool IsTextInput(IWebElement element)
    {
        var tagName = element.TagName;
        var type = element.GetAttribute("type");
        return string.Equals(tagName, "textarea", StringComparison.OrdinalIgnoreCase) ||
            (string.Equals(tagName, "input", StringComparison.OrdinalIgnoreCase) &&
             !new[] { "hidden", "radio", "checkbox", "file", "submit", "button" }
                 .Contains(type, StringComparer.OrdinalIgnoreCase));
    }

    private string GetReferencedLabelText(IWebElement element)
    {
        var labelledBy = element.GetAttribute("aria-labelledby");
        if (string.IsNullOrWhiteSpace(labelledBy))
        {
            return string.Empty;
        }

        return string.Join(" ", labelledBy.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(id => _driver.FindElements(By.Id(id)).FirstOrDefault()?.Text ?? string.Empty));
    }

    private IReadOnlyCollection<string> GetLabels(IWebElement element)
    {
        var labels = new List<string>();
        var id = element.GetAttribute("id");
        if (!string.IsNullOrWhiteSpace(id))
        {
            labels.AddRange(element.FindElements(By.XPath(
                $"./preceding::*[@for='{id}'][1] | ./following::*[@for='{id}'][1]"))
                .Select(label => label.Text));
        }

        labels.AddRange(element.FindElements(By.XPath("./ancestor::label[1]")).Select(label => label.Text));
        labels.AddRange(element.FindElements(By.XPath("./preceding-sibling::label[1]")).Select(label => label.Text));
        return labels.Where(text => !string.IsNullOrWhiteSpace(text)).ToArray();
    }

    private static string GetContextText(IWebElement element)
    {
        var container = element.FindElements(By.XPath(
            "./ancestor::fieldset[1] | ./ancestor::div[1] | ./ancestor::section[1]")).FirstOrDefault();
        return container?.Text ?? string.Empty;
    }

    private static bool ContainsNormalized(string? source, string? expected)
    {
        return Normalize(source).Contains(Normalize(expected), StringComparison.OrdinalIgnoreCase);
    }

    private static bool EqualsNormalized(string? source, string? expected)
    {
        return string.Equals(Normalize(source), Normalize(expected), StringComparison.OrdinalIgnoreCase);
    }

    private static string Normalize(string? value)
    {
        return string.Join(" ", (value ?? string.Empty)
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
            .Trim();
    }

    private static string Describe(IWebElement element)
    {
        var id = element.GetAttribute("id");
        var name = element.GetAttribute("name");
        var type = element.GetAttribute("type");
        return $"id='{id}', name='{name}', type='{type}'";
    }

    private readonly record struct CandidateScore(int Value, bool IsValid)
    {
        public static CandidateScore Invalid => new(0, false);
    }
}

public sealed class SmartElementException : Exception
{
    public SmartElementException(string message) : base(message)
    {
    }
}
