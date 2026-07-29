using System.Text;

using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;

using NUnit.Framework;

namespace GitPulse.UITests;

/// <summary>
///   Walk Control / Content / Raw UIA trees — Shell page bodies may only appear
///   in Raw view on MAUI Windows.
/// </summary>
public sealed class AccessibilityDumpTests : BaseTest
{
    [Test]
    [Order(-1)]
    public void DumpTree_AfterSelectingSettings()
    {
        SelectShellTab("Settings");
        Thread.Sleep(2000);

        string outDir = Path.Combine(FlaUISetup.FindRepoRoot(), "artifacts", "uitest-diagnostics");
        Directory.CreateDirectory(outDir);

        string controlDump = DescribeTree(250);
        File.WriteAllText(Path.Combine(outDir, "flaui-settings-control.txt"), controlDump);

        string rawDump = DumpWalker("Raw", FlaUISetup.Automation.TreeWalkerFactory.GetRawViewWalker());
        File.WriteAllText(Path.Combine(outDir, "flaui-settings-raw.txt"), rawDump);

        string contentDump = DumpWalker("Content", FlaUISetup.Automation.TreeWalkerFactory.GetContentViewWalker());
        File.WriteAllText(Path.Combine(outDir, "flaui-settings-content.txt"), contentDump);

        TestContext.Out.WriteLine("--- CONTROL ---");
        TestContext.Out.WriteLine(controlDump);
        TestContext.Out.WriteLine("--- RAW (first 200 lines) ---");
        TestContext.Out.WriteLine(string.Join(Environment.NewLine, rawDump.Split('\n').Take(200)));
        TestContext.Out.WriteLine("--- CONTENT (first 200 lines) ---");
        TestContext.Out.WriteLine(string.Join(Environment.NewLine, contentDump.Split('\n').Take(200)));

        bool hasTokenEntry =
            MainWindow.FindFirstDescendant(cf => cf.ByAutomationId(AutomationIds.SettingsTokenEntry)) is not null
            || RawContainsAutomationId(AutomationIds.SettingsTokenEntry);
        bool hasSave =
            MainWindow.FindFirstDescendant(cf => cf.ByAutomationId(AutomationIds.SettingsSaveTokenButton)) is not null
            || MainWindow.FindFirstDescendant(cf => cf.ByName("Save Token")) is not null
            || RawContainsName("Save Token")
            || RawContainsAutomationId(AutomationIds.SettingsSaveTokenButton);

        Assert.That(
            hasTokenEntry || hasSave,
            Is.True,
            "Settings controls missing from Control/Content/Raw trees. See artifacts/uitest-diagnostics/flaui-settings-*.txt");
    }

    string DumpWalker(string label, ITreeWalker walker)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"{label} view walk from MainWindow:");
        int count = 0;
        Walk(walker, MainWindow, depth: 0, sb, ref count, maxNodes: 400);
        sb.AppendLine($"Total nodes visited: {count}");
        return sb.ToString();
    }

    void Walk(ITreeWalker walker, AutomationElement node, int depth, StringBuilder sb, ref int count, int maxNodes)
    {
        if (count >= maxNodes)
        {
            return;
        }

        count++;
        string id = Safe(() => node.Properties.AutomationId.ValueOrDefault) ?? string.Empty;
        string name = Safe(() => node.Properties.Name.ValueOrDefault) ?? string.Empty;
        ControlType type = Safe(() => node.Properties.ControlType.ValueOrDefault);
        string className = Safe(() => node.Properties.ClassName.ValueOrDefault) ?? string.Empty;
        sb.AppendLine($"{new string(' ', depth * 2)}[{type}] class='{className}' id='{id}' name='{name}'");

        AutomationElement? child = Safe(() => walker.GetFirstChild(node));
        while (child is not null && count < maxNodes)
        {
            Walk(walker, child, depth + 1, sb, ref count, maxNodes);
            AutomationElement current = child;
            child = Safe(() => walker.GetNextSibling(current));
        }
    }

    bool RawContainsAutomationId(string automationId)
    {
        ITreeWalker walker = FlaUISetup.Automation.TreeWalkerFactory.GetRawViewWalker();
        return FindInWalker(walker, MainWindow, e =>
            string.Equals(
                Safe(() => e.Properties.AutomationId.ValueOrDefault),
                automationId,
                StringComparison.Ordinal));
    }

    bool RawContainsName(string name)
    {
        ITreeWalker walker = FlaUISetup.Automation.TreeWalkerFactory.GetRawViewWalker();
        return FindInWalker(walker, MainWindow, e =>
            string.Equals(
                Safe(() => e.Properties.Name.ValueOrDefault),
                name,
                StringComparison.Ordinal));
    }

    bool FindInWalker(ITreeWalker walker, AutomationElement root, Func<AutomationElement, bool> predicate, int max = 2000)
    {
        int visited = 0;
        return FindRecursive(walker, root, predicate, ref visited, max);
    }

    bool FindRecursive(
        ITreeWalker walker,
        AutomationElement node,
        Func<AutomationElement, bool> predicate,
        ref int visited,
        int max)
    {
        if (visited++ >= max)
        {
            return false;
        }

        if (predicate(node))
        {
            return true;
        }

        AutomationElement? child = Safe(() => walker.GetFirstChild(node));
        while (child is not null)
        {
            if (FindRecursive(walker, child, predicate, ref visited, max))
            {
                return true;
            }

            AutomationElement current = child;
            child = Safe(() => walker.GetNextSibling(current));
        }

        return false;
    }

    static T? Safe<T>(Func<T> read)
    {
        try
        {
            return read();
        }
        catch
        {
            return default;
        }
    }
}
