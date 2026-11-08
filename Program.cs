using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;

namespace SmartAutomation;

internal static class Program
{
    private static int Main(string[] args)
    {
        var configuration = TestConfiguration.FromArguments(args);
        IWebDriver? driver = null;

        Console.WriteLine("SMART AUTOMATION TEST");
        Console.WriteLine();
        Console.WriteLine($"Target URL: {configuration.Url}");
        Console.WriteLine("Browser: Chrome");
        Console.WriteLine();

        try
        {
            var options = new ChromeOptions();
            options.AddArgument("--start-maximized");
            driver = new ChromeDriver(options);
            driver.Navigate().GoToUrl(configuration.Url);

            var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(15));
            wait.Until(current => ((IJavaScriptExecutor)current).ExecuteScript("return document.readyState")?.ToString() == "complete");
            var finder = new SmartElementFinder(driver);

            var firstName = finder.FindFirstName();
            Report("First Name identified", true);
            firstName.Clear();
            firstName.SendKeys(configuration.FirstName);
            Report("First Name entered", true);
            Report("First Name verified", firstName.GetAttribute("value") == configuration.FirstName);

            var gender = finder.FindGender(configuration.Gender);
            Report($"Gender option identified: {configuration.Gender}", true);
            gender.Click();
            Report("Gender selected", true);
            Report("Gender verified", gender.Selected);

            var state = finder.FindState(configuration.State);
            Report("State dropdown identified", true);
            new SelectElement(state).SelectByText(configuration.State);
            Report("State selected", true);
            var selectedState = new SelectElement(state).SelectedOption.Text.Trim();
            Report("State verified", string.Equals(selectedState, configuration.State, StringComparison.OrdinalIgnoreCase));

            Console.WriteLine();
            Console.WriteLine("Overall Result: PASS");
            return 0;
        }
        catch (SmartElementException exception)
        {
            Console.WriteLine($"[FAIL] Smart locator: {exception.Message}");
            Console.WriteLine();
            Console.WriteLine("Overall Result: FAIL");
            return 2;
        }
        catch (WebDriverException exception)
        {
            Console.WriteLine($"[FAIL] Browser automation: {exception.Message}");
            Console.WriteLine();
            Console.WriteLine("Overall Result: FAIL");
            return 3;
        }
        catch (Exception exception)
        {
            Console.WriteLine($"[FAIL] Unexpected error: {exception.Message}");
            Console.WriteLine();
            Console.WriteLine("Overall Result: FAIL");
            return 1;
        }
        finally
        {
            driver?.Quit();
        }
    }

    private static void Report(string action, bool passed)
    {
        Console.WriteLine($"[{(passed ? "PASS" : "FAIL")}] {action}");
        if (!passed)
        {
            throw new SmartElementException($"{action} did not produce the expected result.");
        }
    }
}
