using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Markup;
using System.Windows.Media;
using System.Xml.Linq;
using StarPie.Plugin;
using WinPieGestures;
using WinPieGestures.Plugins;

namespace StarPie.GestureLayout.Tests;

public class Program
{
    private static int s_passed = 0;
    private static int s_failed = 0;

    private static void Assert(bool condition, string testName, string detail = "")
    {
        if (condition)
        {
            Console.WriteLine($"[PASS] {testName}");
            s_passed++;
        }
        else
        {
            Console.WriteLine($"[FAIL] {testName}: {detail}");
            s_failed++;
        }
    }

    [STAThread]
    public static int Main(string[] args)
    {
        Console.WriteLine("==================================================");
        Console.WriteLine("SP-GESTURE-EDITOR-001 R5 Layout & Production Style Regression");
        Console.WriteLine("==================================================");

        try
        {
            // Parse CLI options: support --xaml <path> to test specific XAML file (e.g. input-snapshot vs candidate)
            string? customXamlPath = null;
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--xaml" && i + 1 < args.Length)
                {
                    customXamlPath = args[++i];
                }
            }

            string repoRoot = FindRepoRoot();
            string xamlPath = customXamlPath ?? Path.Combine(repoRoot, "WinPieGestures", "SettingsWindow.xaml");
            Console.WriteLine($"Testing XAML: {xamlPath}");
            Assert(File.Exists(xamlPath), "XamlFileExists", $"Found at {xamlPath}");

            // 1. Initialize WPF Application in STA with real production styles extracted from tested XAML
            var doc = XDocument.Load(xamlPath);
            EnsureWpfApplication(doc);

            // 2. Extract and verify production DataTemplate from tested XAML
            DataTemplate? gestureTemplate = ExtractGestureRowTemplate(doc);
            Assert(gestureTemplate != null, "ExtractGestureRowTemplate", "Extracted production DataTemplate");

            // 3. Extract and verify production PluginListBox style & template
            (Style? itemContainerStyle, DataTemplate? itemTemplate) = ExtractPluginListBoxTemplates(doc);
            Assert(itemContainerStyle != null && itemTemplate != null, "ExtractPluginListBoxTemplates", "Extracted production ListBox templates");

            if (gestureTemplate != null)
            {
                // 4. Test production FlatComboBoxStyle text measurement across all 4 languages
                TestProductionStyle_LabelFittingAcrossFourLanguages(gestureTemplate);

                // 5. Test 600, 900, 1400 DIP container widths and bounded parameter container
                TestTwoTierResponsiveLayout_BoundsAndSpacing(gestureTemplate);

                // 6. Test all 10 action types: layout, button visibility, bounds and non-overlapping reachability
                TestMeasureArrange_AllWidthsAndTypes_AndButtonReachability(gestureTemplate);

                // 7. Test panel exclusivity and mutation guard with a genuinely registered plugin fixture
                TestPanelExclusivity_AndMutationGuardWithRegisteredFixture(gestureTemplate);
            }

            if (itemContainerStyle != null && itemTemplate != null)
            {
                // 8. Test Plugin card selection visual styling and dark/light themes
                TestPluginCardSelection_AndRoundedVisuals(itemContainerStyle, itemTemplate);
                TestDarkAndLightThemeResources(itemContainerStyle, itemTemplate);
            }

            // 9. Test KeyMap DataGrid Table Theme Adaptation & WCAG Contrast
            TestKeyMapTableTheme_ContrastAndBrushes(repoRoot);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[FATAL] Unhandled exception in layout tests: {ex}");
            s_failed++;
        }

        Console.WriteLine("==================================================");
        Console.WriteLine($"Results: {s_passed} passed, {s_failed} failed.");
        Console.WriteLine("==================================================");

        return s_failed > 0 ? 1 : 0;
    }

    private static void EnsureWpfApplication(XDocument doc)
    {
        var app = Application.Current ?? new Application();

        // 1. Populate required theme brushes (matching production XAML resources)
        app.Resources["WindowBackgroundBrush"] = new SolidColorBrush(Color.FromRgb(0xF8, 0xFA, 0xFC));
        app.Resources["SidebarBackgroundBrush"] = Brushes.White;
        app.Resources["CardBackgroundBrush"] = Brushes.White;
        app.Resources["CardBorderBrush"] = new SolidColorBrush(Color.FromRgb(0xE2, 0xE8, 0xF0));
        app.Resources["TextPrimaryBrush"] = new SolidColorBrush(Color.FromRgb(0x0F, 0x17, 0x2A));
        app.Resources["TextSecondaryBrush"] = new SolidColorBrush(Color.FromRgb(0x47, 0x55, 0x69));
        app.Resources["TextMutedBrush"] = new SolidColorBrush(Color.FromRgb(0x64, 0x74, 0x8B));
        app.Resources["InputBackgroundBrush"] = Brushes.White;
        app.Resources["InputBorderBrush"] = new SolidColorBrush(Color.FromRgb(0xCB, 0xD5, 0xE1));
        app.Resources["ItemHoverBrush"] = new SolidColorBrush(Color.FromRgb(0xF1, 0xF5, 0xF9));
        app.Resources["SubtleCardBrush"] = new SolidColorBrush(Color.FromRgb(0xF8, 0xFA, 0xFC));
        app.Resources["NavTabDefaultBgBrush"] = Brushes.Transparent;
        app.Resources["NavTabActiveBgBrush"] = new SolidColorBrush(Color.FromRgb(0xEF, 0xF6, 0xFF));
        app.Resources["AccentPrimaryBrush"] = new SolidColorBrush(Color.FromRgb(0x25, 0x63, 0xEB));
        app.Resources["AccentHoverBrush"] = new SolidColorBrush(Color.FromRgb(0x1D, 0x4E, 0xD8));
        app.Resources["AccentTextBrush"] = Brushes.White;
        app.Resources["ButtonDefaultBgBrush"] = Brushes.White;
        app.Resources["ButtonDefaultFgBrush"] = new SolidColorBrush(Color.FromRgb(0x33, 0x41, 0x55));
        app.Resources["ButtonDefaultBorderBrush"] = new SolidColorBrush(Color.FromRgb(0xCB, 0xD5, 0xE1));
        app.Resources["ButtonHoverBgBrush"] = new SolidColorBrush(Color.FromRgb(0xF1, 0xF5, 0xF9));

        app.Resources["BoolToVis"] = new BooleanToVisibilityConverter();

        // 2. Load the REAL production FlatComboBoxStyle, ComboBoxToggleButtonTemplate, and ModernButtonStyle from the XAML
        XNamespace ns = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";

        var resDictElem = new XElement(ns + "ResourceDictionary",
            new XAttribute("xmlns", ns.NamespaceName),
            new XAttribute(XNamespace.Xmlns + "x", x.NamespaceName));

        foreach (string key in new[] { "ComboBoxToggleButtonTemplate", "FlatComboBoxStyle", "ModernButtonStyle", "ToggleSwitchStyle" })
        {
            var elem = doc.Descendants().FirstOrDefault(e => (string?)e.Attribute(x + "Key") == key);
            if (elem != null)
            {
                resDictElem.Add(new XElement(elem));
            }
        }

        // DangerButtonStyle based on ModernButtonStyle
        string dangerStyleXaml = @"
<Style xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
       xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml""
       x:Key=""DangerButtonStyle"" TargetType=""Button"" BasedOn=""{StaticResource ModernButtonStyle}"">
  <Setter Property=""Background"" Value=""#1AE11D48"" />
  <Setter Property=""BorderBrush"" Value=""#40E11D48"" />
  <Setter Property=""Foreground"" Value=""#E11D48"" />
</Style>";
        resDictElem.Add(XElement.Parse(dangerStyleXaml));

        var parsedResDict = (ResourceDictionary)XamlReader.Parse(resDictElem.ToString());
        app.Resources.MergedDictionaries.Add(parsedResDict);
    }

    private static string FindRepoRoot()
    {
        string? dir = AppDomain.CurrentDomain.BaseDirectory;
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir, "WinPieGestures", "SettingsWindow.xaml")))
                return dir;
            dir = Directory.GetParent(dir)?.FullName;
        }
        throw new FileNotFoundException("Cannot find repository root containing WinPieGestures/SettingsWindow.xaml");
    }

    private static DataTemplate ExtractGestureRowTemplate(XDocument doc)
    {
        XNamespace ns = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        var itemsControl = doc.Descendants(ns + "ItemsControl")
            .FirstOrDefault(e => (string?)e.Attribute("Name") == "GestureMappingsItemsControl");
        if (itemsControl == null) throw new InvalidOperationException("GestureMappingsItemsControl not found in XAML");

        var itemTemplateElem = itemsControl.Element(ns + "ItemsControl.ItemTemplate");
        if (itemTemplateElem == null) throw new InvalidOperationException("ItemsControl.ItemTemplate not found");

        var dataTemplateElem = itemTemplateElem.Element(ns + "DataTemplate");
        if (dataTemplateElem == null) throw new InvalidOperationException("DataTemplate not found inside ItemTemplate");

        XNamespace defNs = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XNamespace xNs = "http://schemas.microsoft.com/winfx/2006/xaml";
        XNamespace localNs = "clr-namespace:WinPieGestures;assembly=StarPie";
        XNamespace pluginsNs = "clr-namespace:WinPieGestures.Plugins;assembly=StarPie";

        _ = typeof(HotkeyRecorderBox).Assembly;

        var cleanTemplate = CleanXamlNode(dataTemplateElem, defNs, localNs, pluginsNs);
        cleanTemplate.SetAttributeValue("xmlns", defNs.NamespaceName);
        cleanTemplate.SetAttributeValue(XNamespace.Xmlns + "x", xNs.NamespaceName);
        cleanTemplate.SetAttributeValue(XNamespace.Xmlns + "local", localNs.NamespaceName);
        cleanTemplate.SetAttributeValue(XNamespace.Xmlns + "plugins", pluginsNs.NamespaceName);

        string xamlString = cleanTemplate.ToString();
        return (DataTemplate)XamlReader.Parse(xamlString);
    }

    private static (Style, DataTemplate) ExtractPluginListBoxTemplates(XDocument doc)
    {
        XNamespace ns = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        var listBox = doc.Descendants(ns + "ListBox")
            .FirstOrDefault(e => (string?)e.Attribute("Name") == "PluginListBox");
        if (listBox == null) throw new InvalidOperationException("PluginListBox not found in XAML");

        var containerStyleElem = listBox.Element(ns + "ListBox.ItemContainerStyle")?.Element(ns + "Style");
        if (containerStyleElem == null) throw new InvalidOperationException("ListBox.ItemContainerStyle not found");

        var itemTemplateElem = listBox.Element(ns + "ListBox.ItemTemplate")?.Element(ns + "DataTemplate");
        if (itemTemplateElem == null) throw new InvalidOperationException("ListBox.ItemTemplate not found");

        XNamespace defNs = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XNamespace xNs = "http://schemas.microsoft.com/winfx/2006/xaml";
        XNamespace localNs = "clr-namespace:WinPieGestures;assembly=StarPie";
        XNamespace pluginsNs = "clr-namespace:WinPieGestures.Plugins;assembly=StarPie";

        var cleanStyle = CleanXamlNode(containerStyleElem, defNs, localNs, pluginsNs);
        cleanStyle.SetAttributeValue("xmlns", defNs.NamespaceName);
        cleanStyle.SetAttributeValue(XNamespace.Xmlns + "x", xNs.NamespaceName);
        cleanStyle.SetAttributeValue(XNamespace.Xmlns + "local", localNs.NamespaceName);

        var cleanTemplate = CleanXamlNode(itemTemplateElem, defNs, localNs, pluginsNs);
        cleanTemplate.SetAttributeValue("xmlns", defNs.NamespaceName);
        cleanTemplate.SetAttributeValue(XNamespace.Xmlns + "x", xNs.NamespaceName);
        cleanTemplate.SetAttributeValue(XNamespace.Xmlns + "local", localNs.NamespaceName);

        var style = (Style)XamlReader.Parse(cleanStyle.ToString());
        var template = (DataTemplate)XamlReader.Parse(cleanTemplate.ToString());
        return (style, template);
    }

    private static XElement CleanXamlNode(XElement element, XNamespace defNs, XNamespace localNs, XNamespace pluginsNs)
    {
        XName name;
        if (element.Name.NamespaceName == "clr-namespace:WinPieGestures" || element.Name.NamespaceName.StartsWith("clr-namespace:WinPieGestures;"))
        {
            name = localNs + element.Name.LocalName;
        }
        else if (element.Name.NamespaceName == "clr-namespace:WinPieGestures.Plugins" || element.Name.NamespaceName.StartsWith("clr-namespace:WinPieGestures.Plugins;"))
        {
            name = pluginsNs + element.Name.LocalName;
        }
        else
        {
            name = defNs + element.Name.LocalName;
        }

        var clone = new XElement(name);
        foreach (var attr in element.Attributes())
        {
            string attrName = attr.Name.LocalName;
            if (attrName.EndsWith("Click") || attrName.EndsWith("Changed") || attrName.EndsWith("Wheel") || attrName.EndsWith("Down") || attrName.EndsWith("Up"))
                continue;
            if (attr.IsNamespaceDeclaration)
                continue;

            clone.Add(new XAttribute(attr));
        }

        foreach (var child in element.Nodes())
        {
            if (child is XElement childElem)
                clone.Add(CleanXamlNode(childElem, defNs, localNs, pluginsNs));
            else
                clone.Add(child);
        }
        return clone;
    }

    private static void TestProductionStyle_LabelFittingAcrossFourLanguages(DataTemplate template)
    {
        Console.WriteLine("\n--- Testing ActionTypes ComboBox Production Style Across 4 Languages ---");

        string[] languages = { "zh-CN", "zh-TW", "en-US", "ja-JP" };

        var mapping = new GestureMapping
        {
            Pattern = "R",
            Action = new ActionItem { Type = "ShellTool", Parameter = "recyclebin" }
        };

        foreach (string lang in languages)
        {
            I18n.SetLanguage(lang);
            var vm = new GestureMappingViewModel(mapping);

            var cp = new ContentPresenter { ContentTemplate = template, Content = vm };
            cp.Measure(new Size(900, double.PositiveInfinity));
            cp.Arrange(new Rect(0, 0, 900, cp.DesiredSize.Height));
            cp.UpdateLayout();

            var border = (Border)VisualTreeHelper.GetChild(cp, 0);
            var rootGrid = (Grid)border.Child;
            var row0 = (Grid)rootGrid.Children[0];
            var typeCombo = (ComboBox)row0.Children[1];

            // Under production FlatComboBoxStyle, the right arrow column is 26 DIP and left/right padding is 10+10=20 DIP.
            double comboWidth = typeCombo.Width;
            double paddingLeft = typeCombo.Padding.Left;
            double paddingRight = typeCombo.Padding.Right;
            if (paddingLeft == 0 && paddingRight == 0)
            {
                // Fallback to FlatComboBoxStyle default padding (10, 4)
                paddingLeft = 10;
                paddingRight = 10;
            }
            double usableTextWidth = comboWidth - 26.0 - paddingLeft - paddingRight;

            // Longest label in ActionTypes across languages is "⚡ " + ActionTypeShellToolShort
            string label = "⚡ " + I18n.T("ActionTypeShellToolShort");

            var text = new TextBlock
            {
                Text = label,
                FontSize = typeCombo.FontSize,
                FontFamily = typeCombo.FontFamily
            };
            text.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            double desiredWidth = text.DesiredSize.Width;

            bool fits = desiredWidth <= usableTextWidth + 0.5;
            Assert(fits, $"{lang}_LabelFitsUnderProductionStyle",
                $"label='{label}', desired={desiredWidth:F2} DIP, usable={usableTextWidth:F2} DIP (combo width={comboWidth} DIP, arrow=26 DIP, padding={paddingLeft + paddingRight} DIP)");

            // Also check all ActionType items in the combo
            foreach (var choice in vm.ActionTypes)
            {
                var choiceText = new TextBlock
                {
                    Text = choice.DisplayText,
                    FontSize = typeCombo.FontSize,
                    FontFamily = typeCombo.FontFamily
                };
                choiceText.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                bool choiceFits = choiceText.DesiredSize.Width <= usableTextWidth + 0.5;
                Assert(choiceFits, $"{lang}_{choice.Tag}_FitsInCombo",
                    $"text='{choice.DisplayText}', desired={choiceText.DesiredSize.Width:F2}, usable={usableTextWidth:F2}");
            }
        }
    }

    private static void TestTwoTierResponsiveLayout_BoundsAndSpacing(DataTemplate template)
    {
        Console.WriteLine("\n--- Testing Two-Tier Responsive Layout (600 / 900 / 1400 DIP) ---");

        var mapping = new GestureMapping
        {
            Pattern = "R",
            Action = new ActionItem { Type = "ShellTool", Parameter = "recyclebin" }
        };
        var vm = new GestureMappingViewModel(mapping);

        // 1. Measure at 600 DIP (minimum supported content width)
        var cp600 = new ContentPresenter { ContentTemplate = template, Content = vm };
        cp600.Measure(new Size(600, double.PositiveInfinity));
        cp600.Arrange(new Rect(0, 0, 600, cp600.DesiredSize.Height));
        cp600.UpdateLayout();

        var border600 = (Border)VisualTreeHelper.GetChild(cp600, 0);
        var rootGrid600 = (Grid)border600.Child;
        var row0_600 = (Grid)rootGrid600.Children[0];
        var row1_600 = (Grid)rootGrid600.Children[1];

        var typeCombo600 = (ComboBox)row0_600.Children[1];
        Assert(typeCombo600.Width >= 190.0, "TypeCombo_WidthSufficientForCjkAndLatin", $"Type ComboBox width is {typeCombo600.Width} DIP (>= 190 DIP)");

        // Row 1 parameter container at 600 DIP has useful space >= 450 DIP
        Assert(row1_600.ActualWidth >= 450.0, "ParamContainer_UsefulWidthAt600DIP", $"Row 1 actual width at 600 DIP is {row1_600.ActualWidth:F1} DIP (>= 450 DIP)");

        // 2. Measure at 1400 DIP (large content width)
        var cp1400 = new ContentPresenter { ContentTemplate = template, Content = vm };
        cp1400.Measure(new Size(1400, double.PositiveInfinity));
        cp1400.Arrange(new Rect(0, 0, 1400, cp1400.DesiredSize.Height));
        cp1400.UpdateLayout();

        var border1400 = (Border)VisualTreeHelper.GetChild(cp1400, 0);
        var rootGrid1400 = (Grid)border1400.Child;
        var row1_1400 = (Grid)rootGrid1400.Children[1];

        // Row 1 parameter container MaxWidth is 760 and bounded at 1400 DIP
        Assert(row1_1400.MaxWidth == 760.0, "ParamContainer_HasMaxWidth760", $"Row 1 MaxWidth is {row1_1400.MaxWidth}");
        Assert(row1_1400.ActualWidth <= 760.01, "ParamContainer_BoundedAt1400DIP", $"Row 1 actual width at 1400 DIP is {row1_1400.ActualWidth:F1} DIP (<= 760 DIP)");
    }

    private static void FindVisibleButtons(DependencyObject parent, List<Button> list)
    {
        int count = VisualTreeHelper.GetChildrenCount(parent);
        for (int i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is UIElement uie && uie.Visibility != Visibility.Visible)
            {
                continue;
            }
            if (child is Button btn)
            {
                list.Add(btn);
            }
            FindVisibleButtons(child, list);
        }
    }

    private static void TestMeasureArrange_AllWidthsAndTypes_AndButtonReachability(DataTemplate template)
    {
        Console.WriteLine("\n--- Testing All Action Types and Button Viewport Reachability Across 4 Languages ---");

        var testCases = new (string Name, GestureMapping Mapping)[]
        {
            ("Hotkey", new GestureMapping { Pattern = "U", Action = new ActionItem { Type = "Hotkey", Parameter = "Ctrl+Shift+T" } }),
            ("Hotkey_ActiveRecording", new GestureMapping { Pattern = "U", Action = new ActionItem { Type = "Hotkey", Parameter = "Ctrl+Shift+T" } }),
            ("Launch", new GestureMapping { Pattern = "D", Action = new ActionItem { Type = "Launch", Parameter = @"C:\Program Files\Test\app.exe" } }),
            ("WebUrl", new GestureMapping { Pattern = "D", Action = new ActionItem { Type = "WebUrl", Parameter = "https://example.com/very/long/url/path/test" } }),
            ("Folder", new GestureMapping { Pattern = "L", Action = new ActionItem { Type = "Folder", Parameter = @"C:\Users\Public\Documents\WorkFolder" } }),
            ("System", new GestureMapping { Pattern = "R", Action = new ActionItem { Type = "System", Parameter = "LockWorkstation" } }),
            ("Command", new GestureMapping { Pattern = "UL", Action = new ActionItem { Type = "Command", Parameter = "git status" } }),
            ("WindowManager_Switch", new GestureMapping { Pattern = "UR", Action = new ActionItem { Type = "SwitchWindow", Parameter = "2" } }),
            ("WindowManager_Tile", new GestureMapping { Pattern = "DL", Action = new ActionItem { Type = "Tile", Parameter = "Left" } }),
            ("WindowManager_Opacity", new GestureMapping { Pattern = "DR", Action = new ActionItem { Type = "WindowOpacity", Parameter = "80" } }),
            ("ShellTool", new GestureMapping { Pattern = "LR", Action = new ActionItem { Type = "ShellTool", Parameter = "recyclebin" } }),
            ("Plugin", new GestureMapping { Pattern = "UD", Action = new ActionItem { Type = "Plugin", PluginActionRef = new PluginActionRef { PluginId = "test.plugin", ContributionId = "action1" } } })
        };

        double[] testWidths = { 600.0, 900.0, 1400.0 };
        string[] testLanguages = { "zh-CN", "zh-TW", "en-US", "ja-JP" };

        foreach (string lang in testLanguages)
        {
            I18n.SetLanguage(lang);

            foreach (var (name, mapping) in testCases)
            {
                var vm = new GestureMappingViewModel(mapping);
                if (name == "Hotkey_ActiveRecording")
                {
                    vm.IsExclusiveRecording = true;
                }
                foreach (double w in testWidths)
                {
                    var cp = new ContentPresenter { ContentTemplate = template, Content = vm };
                    cp.Measure(new Size(w, double.PositiveInfinity));
                    cp.Arrange(new Rect(0, 0, w, cp.DesiredSize.Height));
                    cp.UpdateLayout();

                    Assert(Math.Abs(cp.ActualWidth - w) < 0.1, $"{lang}_{name}_ActualWidth_{w}DIP", $"Expected {w}, got {cp.ActualWidth}");
                    Assert(cp.ActualHeight > 0 && !double.IsNaN(cp.ActualHeight) && !double.IsInfinity(cp.ActualHeight),
                        $"{lang}_{name}_ValidHeight_{w}DIP", $"Height is {cp.ActualHeight:F1} DIP");

                    var border = (Border)VisualTreeHelper.GetChild(cp, 0);
                    var rootGrid = (Grid)border.Child;
                    var row0 = (Grid)rootGrid.Children[0];
                    var row1 = (Grid)rootGrid.Children[1];

                    // Row 1 width bound check
                    Assert(row1.ActualWidth <= 760.01, $"{lang}_{name}_Row1BoundedMaxWidth_{w}DIP", $"Row 1 width is {row1.ActualWidth:F1} DIP (<= 760 DIP)");

                    // Collect all visible buttons in row 0 and row 1
                    var visibleButtons = new List<Button>();
                    FindVisibleButtons(rootGrid, visibleButtons);

                    // Assert expected button counts
                    if (name == "Launch")
                    {
                        Assert(visibleButtons.Count >= 5, $"{lang}_{name}_HasExpectedButtons_{w}DIP", $"Expected >= 5 buttons, found {visibleButtons.Count}");
                    }
                    else if (name == "Hotkey" || name == "Hotkey_ActiveRecording")
                    {
                        Assert(visibleButtons.Count >= 4, $"{lang}_{name}_HasExpectedButtons_{w}DIP", $"Expected >= 4 buttons, found {visibleButtons.Count}");
                    }
                    else
                    {
                        Assert(visibleButtons.Count >= 2, $"{lang}_{name}_HasExpectedButtons_{w}DIP", $"Expected >= 2 buttons, found {visibleButtons.Count}");
                    }

                    // Check viewport containment and non-overlap for all visible buttons
                    var buttonBounds = new List<(Button Btn, Rect Bounds)>();
                    for (int i = 0; i < visibleButtons.Count; i++)
                    {
                        var btn = visibleButtons[i];
                        Point pos = btn.TranslatePoint(new Point(0, 0), cp);
                        Rect bounds = new Rect(pos, btn.RenderSize);
                        buttonBounds.Add((btn, bounds));

                        bool inViewport = bounds.Left >= -0.5 && bounds.Right <= w + 1.0 &&
                                          bounds.Top >= -0.5 && bounds.Bottom <= cp.ActualHeight + 1.0;
                        Assert(inViewport, $"{lang}_{name}_Btn{i}_WithinViewport_{w}DIP",
                            $"btnContent='{btn.Content}', bounds={bounds}, container=({w}x{cp.ActualHeight})");
                    }

                    // Check positive-area overlap among all visible buttons
                    for (int i = 0; i < buttonBounds.Count; i++)
                    {
                        for (int j = i + 1; j < buttonBounds.Count; j++)
                        {
                            Rect overlap = Rect.Intersect(buttonBounds[i].Bounds, buttonBounds[j].Bounds);
                            bool hasPositiveOverlap = !overlap.IsEmpty && overlap.Width > 0.01 && overlap.Height > 0.01;
                            Assert(!hasPositiveOverlap, $"{lang}_{name}_NoOverlap_Btn{i}_Btn{j}_{w}DIP",
                                $"Buttons '{buttonBounds[i].Btn.Content}' and '{buttonBounds[j].Btn.Content}' must not overlap. Overlap={overlap}");
                        }
                    }
                }
            }
        }
    }

    private static void TestPanelExclusivity_AndMutationGuardWithRegisteredFixture(DataTemplate template)
    {
        Console.WriteLine("\n--- Testing Panel Exclusivity & Mutation Guard with Registered Fixture ---");

        const string fixturePluginId = "test.registered.cad";
        const string fixtureShortId = "draw_circle";
        const string fixtureFullId = $"{fixturePluginId}.{fixtureShortId}";

        var assembly = typeof(GestureMappingViewModel).Assembly;
        var hostType = assembly.GetType("WinPieGestures.Plugins.PluginHost", true)!;
        var catalog = hostType.GetField("Catalog", BindingFlags.Public | BindingFlags.Static)!.GetValue(null)!;
        var actions = (System.Collections.IDictionary)catalog.GetType().GetField("_actions", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(catalog)!;
        var registrationType = assembly.GetType("WinPieGestures.Plugins.PluginActionRegistration", true)!;
        var registration = Activator.CreateInstance(registrationType, true)!;
        registrationType.GetProperty("PluginId")!.SetValue(registration, fixturePluginId);
        registrationType.GetProperty("ShortId")!.SetValue(registration, fixtureShortId);
        registrationType.GetProperty("FullId")!.SetValue(registration, fixtureFullId);
        registrationType.GetProperty("DisplayName")!.SetValue(registration, "Draw Circle");
        registrationType.GetProperty("Parameters")!.SetValue(registration, Array.Empty<ParameterField>());

        try
        {
            actions[fixtureFullId] = registration;

            // Verify the fixture is genuinely registered and resolvable via PluginHost.TryGetAction
            var tryGetActionMethod = hostType.GetMethod("TryGetAction", BindingFlags.Public | BindingFlags.Static);
            object[] tryGetArgs = new object[] { fixtureFullId, null! };
            bool isRegistered = (bool)tryGetActionMethod!.Invoke(null, tryGetArgs)!;
            Assert(isRegistered, "FixtureGenuinelyRegistered", $"Fixture {fixtureFullId} is registered in PluginHost");

            var mapping = new GestureMapping
            {
                Pattern = "R",
                Action = new ActionItem { Type = "Hotkey", Parameter = "Ctrl+Alt+A" }
            };
            var vm = new GestureMappingViewModel(mapping);

            Assert(!vm.IsPluginType, "Hotkey_IsPluginType_False", "Hotkey mapping should have IsPluginType == false");

            var cp = new ContentPresenter { ContentTemplate = template, Content = vm };
            cp.Measure(new Size(800, double.PositiveInfinity));
            cp.Arrange(new Rect(0, 0, 800, cp.DesiredSize.Height));
            cp.UpdateLayout();

            var border = (Border)VisualTreeHelper.GetChild(cp, 0);
            var rootGrid = (Grid)border.Child;
            var row1 = (Grid)rootGrid.Children[1];

            // Verify Hotkey panel is visible and Plugin panel is collapsed
            var hotkeyRecorder = row1.Children[0];
            Assert(hotkeyRecorder.Visibility == Visibility.Visible, "HotkeyPanel_IsVisible", "Hotkey recorder should be Visible");

            var pluginPanel = row1.Children[row1.Children.Count - 1];
            Assert(pluginPanel.Visibility == Visibility.Collapsed, "PluginPanel_IsCollapsed", "Plugin panel should be Collapsed for Hotkey");

            // CRITICAL BEHAVIORAL GUARD:
            // Setting SelectedPluginActionFullId while Type != 'Plugin' must be rejected by the guard.
            // Because fixtureFullId IS registered, if the guard did not exist, PluginActionBinding.Apply
            // WOULD have executed and mutated mapping.Action.Type to 'Plugin'!
            vm.SelectedPluginActionFullId = fixtureFullId;

            Assert(vm.Type == "Hotkey", "MutationGuard_TypePreserved", $"Type should remain 'Hotkey', got '{vm.Type}'");
            Assert(vm.AggregatedType == "Hotkey", "MutationGuard_AggregatedTypePreserved", $"AggregatedType should remain 'Hotkey', got '{vm.AggregatedType}'");
            Assert(mapping.Action.Type == "Hotkey", "MutationGuard_MappingActionTypePreserved", $"ActionItem.Type should remain 'Hotkey', got '{mapping.Action.Type}'");

            // Switch to ShellTool
            vm.AggregatedType = "ShellTool";
            Assert(vm.IsShellToolType, "ShellTool_IsShellToolType_True", "ShellTool type active");
            Assert(!vm.IsPluginType, "ShellTool_IsPluginType_False", "Plugin type false");

            cp.UpdateLayout();
            Assert(pluginPanel.Visibility == Visibility.Collapsed, "PluginPanel_CollapsedForShellTool", "Plugin panel remains Collapsed for ShellTool");

            // Try mutating again via SelectedPluginActionFullId
            vm.SelectedPluginActionFullId = fixtureFullId;
            Assert(vm.Type == "ShellTool", "MutationGuard_ShellToolPreserved", "ShellTool preserved against registered action write");
            Assert(mapping.Action.Type == "ShellTool", "MutationGuard_ActionTypeShellToolPreserved", "Action.Type preserved as ShellTool");

            // Now explicitly switch to Plugin: writing SelectedPluginActionFullId should now succeed
            vm.AggregatedType = "Plugin";
            Assert(vm.IsPluginType, "Plugin_IsPluginType_True", "Plugin type is now active");
            vm.SelectedPluginActionFullId = fixtureFullId;
            Assert(mapping.Action.Type == "Plugin", "PluginMode_MutationAllowedWhenActive", "Action.Type is Plugin when in Plugin mode");
            Assert(mapping.Action.PluginActionRef?.FullId == fixtureFullId, "PluginMode_ActionRefAssigned", "PluginActionRef assigned correctly");
        }
        finally
        {
            actions.Remove(fixtureFullId);
        }
    }

    private static void TestPluginCardSelection_AndRoundedVisuals(Style itemContainerStyle, DataTemplate itemTemplate)
    {
        Console.WriteLine("\n--- Testing Plugin Card Selection & Rounded Visuals (INV-PLUGIN-SELECTION) ---");

        var itemData = new DummyPluginItem
        {
            DisplayName = "Test Plugin",
            Description = "Test Plugin Description",
            Version = "1.0.0",
            Author = "Author"
        };

        var lbi = new ListBoxItem
        {
            Style = itemContainerStyle,
            ContentTemplate = itemTemplate,
            Content = itemData,
            Width = 500
        };

        lbi.ApplyTemplate();
        lbi.Measure(new Size(500, 300));
        lbi.Arrange(new Rect(0, 0, 500, 300));
        lbi.UpdateLayout();

        Assert(lbi != null, "ListBoxItemGenerated", "ListBoxItem was instantiated");
        if (lbi == null) return;

        // 1. Verify ListBoxItem has Transparent background and no border thickness (removes Aero blue rectangle)
        Assert(lbi.Background == Brushes.Transparent, "ListBoxItem_BackgroundTransparent", "ListBoxItem background is Transparent");
        Assert(lbi.BorderThickness == new Thickness(0), "ListBoxItem_BorderThicknessZero", "ListBoxItem border thickness is 0");
        Assert(lbi.FocusVisualStyle == null, "ListBoxItem_FocusVisualStyleNull", "FocusVisualStyle is null (no dotted rectangle)");

        // 2. Inspect inner PluginCardBorder
        var cardBorder = FindVisualChild<Border>(lbi);
        Assert(cardBorder != null, "PluginCardBorderFound", "PluginCardBorder found in ItemTemplate");
        if (cardBorder == null) return;

        Assert(cardBorder.CornerRadius == new CornerRadius(10), "PluginCardBorder_CornerRadius10", $"CornerRadius is {cardBorder.CornerRadius} (expected 10 on all corners)");

        // 3. Selection visual test: Select item
        lbi.IsSelected = true;
        lbi.UpdateLayout();
        Assert(lbi.IsSelected, "ListBoxItem_IsSelected", "Item is selected");

        // With item selected, container border remains transparent with 0 thickness
        Assert(lbi.Background == Brushes.Transparent, "SelectedItem_ContainerBackgroundTransparent", "Container border remains Transparent when selected (no rectangular blue overlay)");
        Assert(lbi.BorderThickness == new Thickness(0), "SelectedItem_ContainerBorderThicknessZero", "Container border thickness remains 0 when selected");

        // CardBorder maintains CornerRadius='10' in selected state
        Assert(cardBorder.CornerRadius == new CornerRadius(10), "SelectedItem_CardBorderRetainsRadius10", "CardBorder maintains CornerRadius 10 when selected");
    }

    private static void TestDarkAndLightThemeResources(Style itemContainerStyle, DataTemplate itemTemplate)
    {
        Console.WriteLine("\n--- Testing Theme Resources Switching (Dark / Light) ---");

        var app = Application.Current;
        if (app == null) return;

        // Switch to Dark Theme brushes
        app.Resources["CardBackgroundBrush"] = new SolidColorBrush(Color.FromRgb(0x1E, 0x29, 0x3B));
        app.Resources["CardBorderBrush"] = new SolidColorBrush(Color.FromRgb(0x33, 0x41, 0x55));
        app.Resources["AccentPrimaryBrush"] = new SolidColorBrush(Color.FromRgb(0x3B, 0x82, 0xF6));
        app.Resources["NavTabActiveBgBrush"] = new SolidColorBrush(Color.FromRgb(0x1E, 0x3A, 0x8A));
        app.Resources["TextPrimaryBrush"] = new SolidColorBrush(Color.FromRgb(0xF8, 0xFA, 0xFC));

        var lbi = new ListBoxItem
        {
            Style = itemContainerStyle,
            ContentTemplate = itemTemplate,
            Content = new DummyPluginItem { DisplayName = "Dark Theme Item" },
            Width = 500
        };

        lbi.ApplyTemplate();
        lbi.Measure(new Size(500, 200));
        lbi.Arrange(new Rect(0, 0, 500, 200));
        lbi.UpdateLayout();

        Assert(lbi != null, "DarkTheme_ListBoxItemGenerated", "ListBoxItem generated in Dark Theme");
        if (lbi == null) return;

        Assert(lbi.Background == Brushes.Transparent, "DarkTheme_ContainerTransparent", "Dark Theme ListBoxItem container is Transparent");

        var cardBorder = FindVisualChild<Border>(lbi);
        Assert(cardBorder != null && cardBorder.CornerRadius == new CornerRadius(10), "DarkTheme_CornerRadius10", "Dark Theme card maintains CornerRadius 10");

        // Restore Light Theme brushes
        app.Resources["CardBackgroundBrush"] = Brushes.White;
        app.Resources["CardBorderBrush"] = new SolidColorBrush(Color.FromRgb(0xE2, 0xE8, 0xF0));
        app.Resources["AccentPrimaryBrush"] = new SolidColorBrush(Color.FromRgb(0x25, 0x63, 0xEB));
        app.Resources["NavTabActiveBgBrush"] = new SolidColorBrush(Color.FromRgb(0xEF, 0xF6, 0xFF));
        app.Resources["TextPrimaryBrush"] = new SolidColorBrush(Color.FromRgb(0x0F, 0x17, 0x2A));
    }

    private static T? FindVisualChild<T>(DependencyObject? parent) where T : DependencyObject
    {
        if (parent == null) return null;
        int count = VisualTreeHelper.GetChildrenCount(parent);
        for (int i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T typedChild) return typedChild;
            var subChild = FindVisualChild<T>(child);
            if (subChild != null) return subChild;
        }
        return null;
    }

    private static void TestKeyMapTableTheme_ContrastAndBrushes(string repoRoot)
    {
        Console.WriteLine("\n--- Testing KeyMap DataGrid Table Theme Adaptation & WCAG Contrast ---");

        string keyMapXamlPath = Path.Combine(repoRoot, "WinPieGestures", "KeyMapEditorWindow.xaml");
        Assert(File.Exists(keyMapXamlPath), "KeyMapXamlExists", $"Found at {keyMapXamlPath}");

        string xamlText = File.ReadAllText(keyMapXamlPath);

        // 1. Verify Styles and DynamicResource usage in KeyMapEditorWindow.xaml
        Assert(xamlText.Contains(@"x:Key=""KeyMapDataGridColumnHeaderStyle"""), "KeyMap_HasHeaderStyleDef");
        Assert(xamlText.Contains(@"x:Key=""KeyMapDataGridCellStyle"""), "KeyMap_HasCellStyleDef");
        Assert(xamlText.Contains(@"x:Key=""KeyMapDataGridRowStyle"""), "KeyMap_HasRowStyleDef");

        Assert(xamlText.Contains(@"ColumnHeaderStyle=""{StaticResource KeyMapDataGridColumnHeaderStyle}"""), "KeyMap_DataGridBindsHeaderStyle");
        Assert(xamlText.Contains(@"CellStyle=""{StaticResource KeyMapDataGridCellStyle}"""), "KeyMap_DataGridBindsCellStyle");
        Assert(xamlText.Contains(@"RowStyle=""{StaticResource KeyMapDataGridRowStyle}"""), "KeyMap_DataGridBindsRowStyle");

        Assert(xamlText.Contains(@"RowBackground=""{DynamicResource CardBackgroundBrush}"""), "KeyMap_DataGridRowBackgroundDynamic");
        Assert(xamlText.Contains(@"AlternatingRowBackground=""{DynamicResource SubtleCardBrush}"""), "KeyMap_DataGridAlternatingBackgroundDynamic");
        Assert(xamlText.Contains(@"AlternationCount=""2"""), "KeyMap_DataGridAlternationCount2");

        // 2. Extract production DataGrid styles and instantiate actual DataGrid
        XNamespace ns = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XNamespace xNs = "http://schemas.microsoft.com/winfx/2006/xaml";

        var resDictElem = new XElement(ns + "ResourceDictionary",
            new XAttribute("xmlns", ns.NamespaceName),
            new XAttribute(XNamespace.Xmlns + "x", xNs.NamespaceName));

        var keyMapDoc = XDocument.Load(keyMapXamlPath);
        foreach (string key in new[] { "KeyMapDataGridColumnHeaderStyle", "KeyMapDataGridCellStyle", "KeyMapDataGridRowStyle" })
        {
            var elem = keyMapDoc.Descendants().FirstOrDefault(e => (string?)e.Attribute(xNs + "Key") == key);
            if (elem != null)
            {
                resDictElem.Add(new XElement(elem));
            }
        }
        var parsedResDict = (ResourceDictionary)XamlReader.Parse(resDictElem.ToString());
        var headerStyle = (Style)parsedResDict["KeyMapDataGridColumnHeaderStyle"];
        var cellStyle = (Style)parsedResDict["KeyMapDataGridCellStyle"];
        var rowStyle = (Style)parsedResDict["KeyMapDataGridRowStyle"];

        Assert(headerStyle != null, "KeyMap_HeaderStyleInstantiated");
        Assert(cellStyle != null, "KeyMap_CellStyleInstantiated");
        Assert(rowStyle != null, "KeyMap_RowStyleInstantiated");

        var testItems = new List<KeyMapTableTestItem>
        {
            new KeyMapTableTestItem { FromKey = "Q", ToKey = "Num7" },
            new KeyMapTableTestItem { FromKey = "W", ToKey = "Num8" },
            new KeyMapTableTestItem { FromKey = "E", ToKey = "Num9" }
        };

        var dataGrid = new DataGrid
        {
            AutoGenerateColumns = false,
            CanUserAddRows = false,
            IsReadOnly = true,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            HeadersVisibility = DataGridHeadersVisibility.Column,
            ColumnHeaderStyle = headerStyle,
            RowStyle = rowStyle,
            CellStyle = cellStyle,
            RowBackground = (Brush)Application.Current!.Resources["CardBackgroundBrush"],
            AlternatingRowBackground = (Brush)Application.Current.Resources["SubtleCardBrush"],
            AlternationCount = 2,
            SelectionMode = DataGridSelectionMode.Single,
            SelectionUnit = DataGridSelectionUnit.FullRow,
            Width = 600,
            Height = 400
        };

        dataGrid.Columns.Add(new DataGridTextColumn { Header = "源按键", Binding = new System.Windows.Data.Binding("FromKey"), Width = 250 });
        dataGrid.Columns.Add(new DataGridTextColumn { Header = "目标按键", Binding = new System.Windows.Data.Binding("ToKey"), Width = 250 });
        dataGrid.ItemsSource = testItems;

        // 3. Test contrast ratio across Light, ObsidianDark, and TitaniumGray
        string[] themes = { "Light", "ObsidianDark", "TitaniumGray" };
        var dummy = new Grid();

        foreach (string theme in themes)
        {
            AppThemeManager.ApplyTheme(dummy, theme);

            // Synchronize theme brushes to Application.Current.Resources for DynamicResource resolution
            if (Application.Current != null)
            {
                foreach (var k in new[] { "CardBackgroundBrush", "SubtleCardBrush", "ItemHoverBrush", "NavTabActiveBgBrush", "TextPrimaryBrush", "TextSecondaryBrush", "TextMutedBrush", "CardBorderBrush", "AccentPrimaryBrush", "WindowBackgroundBrush" })
                {
                    if (dummy.Resources.Contains(k))
                    {
                        Application.Current.Resources[k] = dummy.Resources[k];
                    }
                }
            }

            dataGrid.Measure(new Size(600, 400));
            dataGrid.Arrange(new Rect(0, 0, 600, 400));
            dataGrid.UpdateLayout();

            Assert(dataGrid.Items.Count == 3, $"{theme}_DataGridHasExpectedItems", "DataGrid contains 3 items");
            Assert(dataGrid.Columns.Count == 2, $"{theme}_DataGridHasExpectedColumns", "DataGrid contains 2 columns");

            Color cardBg = GetBrushColor("CardBackgroundBrush");
            Color subtleBg = GetBrushColor("SubtleCardBrush");
            Color hoverBg = GetBrushColor("ItemHoverBrush");
            Color navTabActiveBg = GetBrushColor("NavTabActiveBgBrush");
            Color textPrimary = GetBrushColor("TextPrimaryBrush");
            Color textSecondary = GetBrushColor("TextSecondaryBrush");
            Color textMuted = GetBrushColor("TextMutedBrush");

            // Header text: TextSecondaryBrush on SubtleCardBrush
            double headerContrast = CalculateContrastRatio(textSecondary, subtleBg);
            Assert(headerContrast >= 4.5, $"{theme}_HeaderContrast_Ge4_5",
                $"Header contrast is {headerContrast:F2}:1 (fg={textSecondary}, bg={subtleBg})");

            // Normal row text: TextPrimaryBrush on CardBackgroundBrush
            double normalRowContrast = CalculateContrastRatio(textPrimary, cardBg);
            Assert(normalRowContrast >= 4.5, $"{theme}_NormalRowContrast_Ge4_5",
                $"Normal row contrast is {normalRowContrast:F2}:1 (fg={textPrimary}, bg={cardBg})");

            // Alternating row text: TextPrimaryBrush on SubtleCardBrush
            double altRowContrast = CalculateContrastRatio(textPrimary, subtleBg);
            Assert(altRowContrast >= 4.5, $"{theme}_AlternatingRowContrast_Ge4_5",
                $"Alternating row contrast is {altRowContrast:F2}:1 (fg={textPrimary}, bg={subtleBg})");

            // Selected row active text: TextPrimaryBrush on NavTabActiveBgBrush
            double selectedContrast = CalculateContrastRatio(textPrimary, navTabActiveBg);
            Assert(selectedContrast >= 4.5, $"{theme}_SelectedRowContrast_Ge4_5",
                $"Selected row contrast is {selectedContrast:F2}:1 (fg={textPrimary}, bg={navTabActiveBg})");

            // Selected row inactive text: TextPrimaryBrush on SubtleCardBrush
            double selectedInactiveContrast = CalculateContrastRatio(textPrimary, subtleBg);
            Assert(selectedInactiveContrast >= 4.5, $"{theme}_SelectedInactiveRowContrast_Ge4_5",
                $"Selected inactive row contrast is {selectedInactiveContrast:F2}:1 (fg={textPrimary}, bg={subtleBg})");

            // Hover row text: TextPrimaryBrush on ItemHoverBrush
            double hoverContrast = CalculateContrastRatio(textPrimary, hoverBg);
            Assert(hoverContrast >= 4.5, $"{theme}_HoverRowContrast_Ge4_5",
                $"Hover row contrast is {hoverContrast:F2}:1 (fg={textPrimary}, bg={hoverBg})");

            // Disabled text: TextMutedBrush on CardBackgroundBrush
            double disabledContrast = CalculateContrastRatio(textMuted, cardBg);
            Assert(disabledContrast >= 4.5, $"{theme}_DisabledTextContrast_Ge4_5",
                $"Disabled text contrast is {disabledContrast:F2}:1 (fg={textMuted}, bg={cardBg})");

            // Empty state handling
            dataGrid.ItemsSource = new List<KeyMapTableTestItem>();
            dataGrid.UpdateLayout();
            Assert(dataGrid.Items.Count == 0, $"{theme}_DataGridEmptyStateHandled", "DataGrid handles empty items source without exception");
            dataGrid.ItemsSource = testItems;
        }
    }

    private static Color GetBrushColor(string resourceKey)
    {
        if (Application.Current?.Resources[resourceKey] is SolidColorBrush scb)
        {
            return scb.Color;
        }
        throw new InvalidOperationException($"Resource '{resourceKey}' not found as SolidColorBrush");
    }

    private static double CalculateLuminance(Color color)
    {
        double r = color.R / 255.0;
        double g = color.G / 255.0;
        double b = color.B / 255.0;
        double rL = r <= 0.04045 ? r / 12.92 : Math.Pow((r + 0.055) / 1.055, 2.4);
        double gL = g <= 0.04045 ? g / 12.92 : Math.Pow((g + 0.055) / 1.055, 2.4);
        double bL = b <= 0.04045 ? b / 12.92 : Math.Pow((b + 0.055) / 1.055, 2.4);
        return 0.2126 * rL + 0.7152 * gL + 0.0722 * bL;
    }

    private static double CalculateContrastRatio(Color c1, Color c2)
    {
        double l1 = CalculateLuminance(c1);
        double l2 = CalculateLuminance(c2);
        if (l1 < l2)
        {
            double tmp = l1;
            l1 = l2;
            l2 = tmp;
        }
        return (l1 + 0.05) / (l2 + 0.05);
    }
}

public class DummyPluginItem
{
    public string DisplayName { get; set; } = "";
    public string Description { get; set; } = "";
    public string Version { get; set; } = "";
    public string Author { get; set; } = "";
    public bool IsEnabled { get; set; } = true;
    public bool HasSettings { get; set; } = false;
    public string SettingsTooltip { get; set; } = "设置";
    public string StatusBadgeText { get; set; } = "已启用";
    public Brush StatusBadgeForeground { get; set; } = Brushes.Green;
    public Brush StatusBadgeBackground { get; set; } = Brushes.LightGreen;
}

public class KeyMapTableTestItem
{
    public string FromKey { get; set; } = "";
    public string ToKey { get; set; } = "";
}

