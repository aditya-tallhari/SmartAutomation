# SmartAutomation

## Assignment overview

SmartAutomation is a small C# console application that fills the First Name, Gender, and State fields on a Selenium web page. It uses semantic clues and weighted scoring instead of depending on fixed IDs, classes, field positions, or absolute XPath expressions.

The program fails safely when a field is missing or when two candidates are too similar to identify confidently.

## Requirements

- .NET 10 SDK (the project can also be retargeted to a supported .NET version)
- Google Chrome
- Internet access for the original CloudQA page and first package restore
- VS Code with the C# Dev Kit extension recommended

## Technology stack

- C# console application
- .NET 10
- Selenium WebDriver 4.38
- Selenium Support 4.38
- ChromeDriver managed by Selenium Manager

## Installation

```powershell
cd SmartAutomation
dotnet restore
dotnet build
```

Selenium Manager downloads a compatible ChromeDriver when the application starts. No driver path is hardcoded.

## Configuration and running

All values have defaults for the CloudQA assignment page:

```powershell
dotnet run -- --url "https://app.cloudqa.io/home/AutomationPracticeForm" --first-name "Ada" --gender "Female" --state "India"
```

The URL is passed to Selenium at runtime and is not embedded in the locator logic. The same values can be supplied through environment variables:

```powershell
$env:SMART_AUTOMATION_URL = "https://example.test/form"
$env:SMART_AUTOMATION_FIRST_NAME = "Ada"
$env:SMART_AUTOMATION_GENDER = "Female"
$env:SMART_AUTOMATION_STATE = "India"
dotnet run
```

Command-line arguments take precedence over environment variables. The supported names are `--url`, `--first-name`, `--gender`, and `--state`.

## How the smart locator works

`SmartElementFinder` first gathers candidates by element type:

- text inputs and textareas for First Name
- radio inputs for Gender
- native `select` elements for State

Each candidate receives points for independent evidence:

| Signal | Weight |
|---|---:|
| Exact associated label | 100 |
| Accessible name (`aria-label`, `aria-labelledby`, or title) | 90 |
| Placeholder | 80 |
| Relevant fieldset/container context | 25-30 |
| Correct element type | 20 |
| Requested radio option text | 100 |
| Requested option exists in a select | 40 |

The highest scoring candidate must reach the confidence threshold and beat the runner-up by at least 15 points. Otherwise the program reports a missing or ambiguous field instead of guessing. Matching is case-insensitive and whitespace-normalized.

## Example output

```text
SMART AUTOMATION TEST

Target URL: https://app.cloudqa.io/home/AutomationPracticeForm
Browser: Chrome

[PASS] First Name identified
[PASS] First Name entered
[PASS] First Name verified
[PASS] Gender option identified: Female
[PASS] Gender selected
[PASS] Gender verified
[PASS] State dropdown identified
[PASS] State selected
[PASS] State verified

Overall Result: PASS
```

The process exits with `0` for success, `2` for a smart-locator failure, `3` for a WebDriver failure, and `1` for another unexpected failure.

## Testing strategy

### Executed checks

The project has been restored, compiled, and run successfully against the live CloudQA page with:

```powershell
dotnet build
dotnet run -- --url "https://app.cloudqa.io/home/AutomationPracticeForm" --first-name "Ada" --gender "Female" --state "India"
```

The observed live result was `Overall Result: PASS`. The live test requires Chrome and network access, so its result depends on the current CloudQA page.

### Recommended resilience checks

Use small local HTML copies of the form and pass a `file:///...` URL:

1. Change every `id`, `name`, and `class`; keep meaningful labels. The semantic labels should still locate the fields.
2. Reorder the fields. The finder searches by element meaning, not index.
3. Add unrelated fields. They should score lower than the labeled targets.
4. Remove one required field. The program should return a clear missing-field failure.
5. Add two equally labeled fields. The program should return an ambiguity failure.
6. Change the labels to unrelated text. The confidence threshold should prevent an accidental interaction.

These are manual fixture scenarios and are not claimed as executed by this repository until you run them.

## File guide

- `Program.cs`: starts Chrome, waits for the page, performs the three interactions, verifies values, prints the result, and closes Chrome.
- `SmartElementFinder.cs`: reusable candidate discovery, semantic signal extraction, scoring, confidence checks, and safe failure messages.
- `TestConfiguration.cs`: command-line, environment-variable, and default configuration.
- `SmartAutomation.csproj`: .NET target and Selenium package references.
- `screenshots/`: reserved for optional debugging screenshots.

## Limitations and future improvements

Scoring is a heuristic. A completely unlabeled field, a major redesign, or two genuinely identical fields may be impossible to identify reliably. Dynamic pages may need an additional page-specific readiness condition. CAPTCHA, authentication, cross-origin frames, and Shadow DOM controls may require separate handling; this assignment intentionally does not add that complexity because the three target controls are expected to be in the normal document.

Possible future improvements include saving a screenshot and HTML snapshot on failure, adding a structured JSON report, and adding automated fixture tests with a local test server.

## Beginner explanation

Selenium WebDriver controls a real Chrome browser through a driver. The program opens the configured URL, finds candidates by element type, scores their labels and nearby context, and interacts only when the best candidate is sufficiently more likely than the alternatives. This means a changed ID or class usually does not break the test because those unstable attributes are not the primary signal.

To debug a failure, read the field-specific message, inspect the page for a meaningful label or accessible name, and run with a local HTML fixture. Do not solve an ambiguity by adding an element index; improve the semantic information or adjust the documented scoring rule.

### Interview-ready explanation

“I built a small Selenium console application with a reusable smart finder. Instead of selecting the third input or relying on a fixed ID, it collects candidates by type and ranks them using labels, accessible names, placeholders, and local form context. It checks confidence and ambiguity before interacting, verifies every value afterward, accepts a URL and field values through configuration, and always closes Chrome. This makes the test more resilient while still keeping the implementation understandable and safe.”
