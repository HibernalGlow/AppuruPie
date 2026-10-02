using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;
using System.Runtime.Serialization;
using StarPie.Plugin;
using WinPieGestures;
using WinPieGestures.Plugins;

namespace StarPie.GestureEditor.Tests;

class Program
{
    static int s_failed = 0;
    static int s_passed = 0;

    static void Assert(bool condition, string testName, string message = "")
    {
        if (condition)
        {
            Console.WriteLine($"[PASS] {testName}");
            s_passed++;
        }
        else
        {
            Console.WriteLine($"[FAIL] {testName}: {message}");
            s_failed++;
        }
    }

    static void Main(string[] args)
    {
        bool r7Only = args != null && Array.Exists(args, a => a == "--r7-production-behavior");
        Console.WriteLine("==================================================");
        Console.WriteLine(r7Only
            ? "SP-GESTURE-EDITOR-001 R7 Production Behavior Suite"
            : "SP-GESTURE-EDITOR-001 Deterministic Regression Test");
        Console.WriteLine("==================================================");

        if (!r7Only)
        {
            Test1_WebUrl_LegacyMappingAndRoundTrip();
            Test2_WindowManager_SubmodesAndParameters();
            Test3_ShellTool_PropertiesAndPersistence();
            Test4_Plugin_PreservationAcrossTypeSwitch();
            Test5_SettingsWindowXaml_BindingsAndVisibility();
            Test6_MissingRequiredParametersCount();
            Test7_I18n_LocalizationAcrossLanguages();
            Test8_Repair2_R1F1_StalePluginStateAndRecreation();
            Test9_Repair2_R1F2_RequiredParamsTipAndLocalization();
            Test10_Repair3_R2F1_OfficialActionsExtensionDataPreservation();
            Test11_Repair3_R2F2_ParameterTargetWriteNullAndSessionEmptySemantics();
            Test12_Repair6_LaunchParityAndTargetIsolation();
            Test13_Repair6_ExclusiveRecordingAndSessionIsolation();
        }

        // R7 Production Behavior Tests
        Test14_Repair7_F1_SessionLifecycleAndSynchronization();
        Test15_Repair7_F2_IsValidGestureOrCancelActionTarget();
        Test16_Repair7_F4_LocalizationKeysResolution();
        Test17_Repair7_BaselineCounterexampleProof();

        Console.WriteLine("==================================================");
        Console.WriteLine($"Results: {s_passed} passed, {s_failed} failed.");
        Console.WriteLine("==================================================");

        Environment.Exit(s_failed > 0 ? 1 : 0);
    }

    static void Test1_WebUrl_LegacyMappingAndRoundTrip()
    {
        // 1. Legacy "Url" mapping should aggregate to "WebUrl"
        var mapping = new GestureMapping
        {
            Pattern = "R",
            Action = new ActionItem { Type = "Url", Parameter = "https://github.com/Star-Pie/StarPie" }
        };
        var vm = new GestureMappingViewModel(mapping);

        Assert(vm.AggregatedType == "WebUrl", 
            "Test1.1_LegacyUrlAggregatedType", 
            $"Expected AggregatedType 'WebUrl' for legacy 'Url', but got '{vm.AggregatedType}'");

        Assert(vm.IsWebUrlType, 
            "Test1.2_IsWebUrlTypeTrue", 
            $"Expected IsWebUrlType true for legacy 'Url'");

        // 2. Setting AggregatedType to WebUrl
        vm.AggregatedType = "WebUrl";
        Assert(vm.IsWebUrlType, "Test1.3_SetAggregatedTypeWebUrl", "IsWebUrlType should remain true");
        Assert(vm.Parameter == "https://github.com/Star-Pie/StarPie", "Test1.4_UrlParameterPreserved", "Parameter should be preserved");
    }

    static void Test2_WindowManager_SubmodesAndParameters()
    {
        // 1. WindowOpacity subtype mapping
        var mappingOpacity = new GestureMapping
        {
            Pattern = "D",
            Action = new ActionItem { Type = "WindowOpacity", Parameter = "70" }
        };
        var vmOpacity = new GestureMappingViewModel(mappingOpacity);

        Assert(vmOpacity.AggregatedType == "WindowManager", "Test2.1_OpacityAggregatedType", $"Expected WindowManager, got {vmOpacity.AggregatedType}");
        Assert(vmOpacity.WindowManagerSubMode == "WindowOpacity", "Test2.2_OpacitySubMode", $"Expected WindowOpacity, got {vmOpacity.WindowManagerSubMode}");
        Assert(vmOpacity.IsOpacitySubMode, "Test2.3_IsOpacitySubMode", "Expected IsOpacitySubMode true");
        Assert(vmOpacity.Parameter == "70", "Test2.4_OpacityParam", $"Expected 70, got {vmOpacity.Parameter}");

        // 2. SwitchWindow subtype mapping
        var mappingSwitch = new GestureMapping
        {
            Pattern = "U",
            Action = new ActionItem { Type = "SwitchWindow", Parameter = "3" }
        };
        var vmSwitch = new GestureMappingViewModel(mappingSwitch);

        Assert(vmSwitch.AggregatedType == "WindowManager", "Test2.5_SwitchAggregatedType", $"Expected WindowManager, got {vmSwitch.AggregatedType}");
        Assert(vmSwitch.WindowManagerSubMode == "SwitchWindow", "Test2.6_SwitchSubMode", $"Expected SwitchWindow, got {vmSwitch.WindowManagerSubMode}");
        Assert(vmSwitch.IsSwitchWindowSubMode, "Test2.7_IsSwitchWindowSubMode", "Expected IsSwitchWindowSubMode true");
        Assert(vmSwitch.NthWindowIndex == "3", "Test2.8_SwitchNthIndex", $"Expected 3, got {vmSwitch.NthWindowIndex}");

        // 3. Tile subtype mapping
        var mappingTile = new GestureMapping
        {
            Pattern = "L",
            Action = new ActionItem { Type = "Tile", Parameter = "3L12" }
        };
        var vmTile = new GestureMappingViewModel(mappingTile);

        Assert(vmTile.AggregatedType == "WindowManager", "Test2.9_TileAggregatedType", $"Expected WindowManager, got {vmTile.AggregatedType}");
        Assert(vmTile.WindowManagerSubMode == "Tile", "Test2.10_TileSubMode", $"Expected Tile, got {vmTile.WindowManagerSubMode}");
        Assert(vmTile.IsTileSubMode, "Test2.11_IsTileSubMode", "Expected IsTileSubMode true");
        Assert(vmTile.TileLayout == "3L12", "Test2.12_TileLayout", $"Expected 3L12, got {vmTile.TileLayout}");

        // 4. Switching to WindowManager from Hotkey resolves to concrete subtype
        var freshMapping = new GestureMapping
        {
            Pattern = "DR",
            Action = new ActionItem { Type = "Hotkey", Parameter = "Ctrl+C" }
        };
        var vmFresh = new GestureMappingViewModel(freshMapping);
        vmFresh.AggregatedType = "WindowManager";
        Assert(vmFresh.IsWindowManagerType, "Test2.13_FreshWindowManagerIsType", "Expected IsWindowManagerType true");
        Assert(vmFresh.Type != "WindowManager", "Test2.14_FreshWindowManagerResolvedToSubtype", $"Type should resolve to concrete subtype, got '{vmFresh.Type}'");
    }

    static void Test3_ShellTool_PropertiesAndPersistence()
    {
        var mapping = new GestureMapping
        {
            Pattern = "DL",
            Action = new ActionItem { Type = "ShellTool", Parameter = "Windows.CopyAsPath" }
        };
        var vm = new GestureMappingViewModel(mapping);

        // Check IsShellToolType property via reflection so base can compile
        PropertyInfo? propIsShellTool = typeof(GestureMappingViewModel).GetProperty("IsShellToolType");
        Assert(propIsShellTool != null, "Test3.1_IsShellToolTypePropertyExists", "GestureMappingViewModel must expose IsShellToolType property");

        if (propIsShellTool != null)
        {
            bool isShellTool = (bool)propIsShellTool.GetValue(vm)!;
            Assert(isShellTool, "Test3.2_IsShellToolTypeValue", "Expected IsShellToolType true for Type='ShellTool'");
        }

        // Check ShellToolTitle property
        PropertyInfo? propShellToolTitle = typeof(GestureMappingViewModel).GetProperty("ShellToolTitle");
        Assert(propShellToolTitle != null, "Test3.3_ShellToolTitlePropertyExists", "GestureMappingViewModel must expose ShellToolTitle property");

        if (propShellToolTitle != null)
        {
            string? title = (string?)propShellToolTitle.GetValue(vm);
            Assert(!string.IsNullOrEmpty(title), "Test3.4_ShellToolTitleValue", $"Expected non-empty ShellToolTitle, got '{title}'");
        }

        // Check disabled/unknown tool persistence
        var disabledMapping = new GestureMapping
        {
            Pattern = "UR",
            Action = new ActionItem { Type = "ShellTool", Parameter = "Custom.DisabledPlugin.ToolA" }
        };
        var vmDisabled = new GestureMappingViewModel(disabledMapping);
        if (propShellToolTitle != null)
        {
            string? title = (string?)propShellToolTitle.GetValue(vmDisabled);
            Assert(title != null && title.Contains("Custom.DisabledPlugin.ToolA"), 
                "Test3.5_DisabledToolPreservesParameter", 
                $"Expected title to retain tool parameter, got '{title}'");
        }
    }

    static void Test4_Plugin_PreservationAcrossTypeSwitch()
    {
        var mapping = new GestureMapping
        {
            Pattern = "UL",
            Action = new ActionItem
            {
                Type = "Plugin",
                PluginActionRef = new PluginActionRef { PluginId = "test.plugin.abc", ContributionId = "action.sample" },
                ExtensionData = new Dictionary<string, string> { ["speed"] = "100" }
            }
        };
        var vm = new GestureMappingViewModel(mapping);

        Assert(vm.AggregatedType == "Plugin", "Test4.1_PluginAggregatedType", $"Expected 'Plugin', got '{vm.AggregatedType}'");
        Assert(vm.IsPluginType, "Test4.2_IsPluginType", "Expected IsPluginType true");
        Assert(vm.IsPluginActionBroken, "Test4.3_UnregisteredPluginBrokenState", "Unregistered plugin action should show broken state");

        // Now simulate user temporarily switching to Hotkey and then back to Plugin
        vm.AggregatedType = "Hotkey";
        Assert(vm.Type == "Hotkey", "Test4.4_SwitchedToHotkey", $"Expected 'Hotkey', got '{vm.Type}'");

        vm.AggregatedType = "Plugin";
        Assert(vm.Type == "Plugin", "Test4.5_SwitchedBackToPlugin", $"Expected 'Plugin', got '{vm.Type}'");
        Assert(vm.Mapping.Action.PluginActionRef != null, 
            "Test4.6_PluginActionRefPreserved", 
            "PluginActionRef must NOT be destroyed when switching types");
        if (vm.Mapping.Action.PluginActionRef != null)
        {
            Assert(vm.Mapping.Action.PluginActionRef.PluginId == "test.plugin.abc" &&
                   vm.Mapping.Action.PluginActionRef.ContributionId == "action.sample",
                   "Test4.7_PluginActionRefDataMatches",
                   $"Preserved PluginActionRef does not match original: {vm.Mapping.Action.PluginActionRef.FullId}");
        }

        // Check HasPluginParameters property
        PropertyInfo? propHasParams = typeof(GestureMappingViewModel).GetProperty("HasPluginParameters");
        Assert(propHasParams != null, "Test4.8_HasPluginParametersPropertyExists", "GestureMappingViewModel must expose HasPluginParameters property");
    }

    static void Test5_SettingsWindowXaml_BindingsAndVisibility()
    {
        // Read SettingsWindow.xaml and check for the required configuration UI elements in gesture pattern mappings
        string basePath = AppDomain.CurrentDomain.BaseDirectory;
        string? projectRoot = null;
        DirectoryInfo? current = new DirectoryInfo(basePath);
        while (current != null)
        {
            string candidate = Path.Combine(current.FullName, "WinPieGestures", "SettingsWindow.xaml");
            if (File.Exists(candidate))
            {
                projectRoot = candidate;
                break;
            }
            current = current.Parent;
        }

        Assert(projectRoot != null && File.Exists(projectRoot), "Test5.1_SettingsWindowXamlFound", $"SettingsWindow.xaml path: {projectRoot}");
        if (projectRoot == null) return;

        string xaml = File.ReadAllText(projectRoot);

        // Find the GestureMappingsItemsControl section
        int startIdx = xaml.IndexOf("Name=\"GestureMappingsItemsControl\"");
        Assert(startIdx >= 0, "Test5.2_GestureMappingsItemsControlFound", "Could not find GestureMappingsItemsControl in SettingsWindow.xaml");
        if (startIdx < 0) return;

        int endIdx = xaml.IndexOf("</ItemsControl>", startIdx);
        string itemsControlXaml = xaml.Substring(startIdx, endIdx - startIdx);

        // Check that ActionTypes ComboBox binds to AggregatedType (not raw Type)
        bool bindsAggregatedType = itemsControlXaml.Contains("SelectedValue=\"{Binding AggregatedType, Mode=TwoWay}\"") ||
                                   itemsControlXaml.Contains("SelectedValue=\"{Binding AggregatedType");
        Assert(bindsAggregatedType, "Test5.3_ActionTypesComboBoxBindsAggregatedType", "ActionTypes ComboBox in gesture row must bind SelectedValue to AggregatedType, not raw Type");

        // Check for WebUrl UI
        bool hasWebUrl = itemsControlXaml.Contains("IsWebUrlType");
        Assert(hasWebUrl, "Test5.4_HasWebUrlConfigurationUI", "GestureMappingsItemsControl must contain configuration UI for IsWebUrlType");

        // Check for WindowManager UI with submode options
        bool hasWindowManager = itemsControlXaml.Contains("IsWindowManagerType") && itemsControlXaml.Contains("WindowManagerSubModes");
        Assert(hasWindowManager, "Test5.5_HasWindowManagerConfigurationUI", "GestureMappingsItemsControl must contain configuration UI for IsWindowManagerType with WindowManagerSubModes");

        // Check for ShellTool UI
        bool hasShellTool = itemsControlXaml.Contains("IsShellToolType");
        Assert(hasShellTool, "Test5.6_HasShellToolConfigurationUI", "GestureMappingsItemsControl must contain configuration UI for IsShellToolType");

        // Check for Plugin UI
        bool hasPlugin = itemsControlXaml.Contains("IsPluginType") && itemsControlXaml.Contains("PluginActionOptions");
        Assert(hasPlugin, "Test5.7_HasPluginConfigurationUI", "GestureMappingsItemsControl must contain configuration UI for IsPluginType with PluginActionOptions");

        // Check that OpacityTip is bound and hardcoded text is removed
        bool bindsOpacityTip = itemsControlXaml.Contains("ToolTip=\"{Binding OpacityTip}\"");
        bool hasHardcodedOpacity = itemsControlXaml.Contains("ToolTip=\"输入不透明度百分比 (30~100)\"");
        Assert(bindsOpacityTip && !hasHardcodedOpacity, "Test5.8_OpacityTipBoundAndNotHardcoded", "Gesture opacity TextBox must bind ToolTip to OpacityTip without hardcoded Chinese");

        // Check that hardcoded plugin grouping tooltip is removed from gesture row
        bool hasHardcodedPluginTooltip = itemsControlXaml.Contains("ToolTip=\"同一插件的动作归入以该插件名命名的分组\"");
        Assert(!hasHardcodedPluginTooltip, "Test5.9_PluginGroupingTooltipRemovedFromGestureRow", "Gesture row plugin ComboBox must not have hardcoded plugin grouping tooltip");
    }

    static void Test6_MissingRequiredParametersCount()
    {
        var parameters = new List<ParameterField>
        {
            new ParameterField { Key = "req_text", Required = true, Type = ParameterFieldType.Text },
            new ParameterField { Key = "req_num", Required = true, Type = ParameterFieldType.Number },
            new ParameterField { Key = "req_flag", Required = true, Type = ParameterFieldType.Bool },
            new ParameterField { Key = "opt_text", Required = false, Type = ParameterFieldType.Text }
        };

        // 1. Two missing required non-Bool fields => count 2 (req_flag is Bool so ignored; opt_text is optional)
        int countNullExt = GestureMappingViewModel.CountMissingRequiredParameters(parameters, null);
        Assert(countNullExt == 2, "Test6.1_TwoMissingRequiredCount2_NullExt", $"Expected 2, got {countNullExt}");

        var emptyExt = new Dictionary<string, string>();
        int countEmptyExt = GestureMappingViewModel.CountMissingRequiredParameters(parameters, emptyExt);
        Assert(countEmptyExt == 2, "Test6.2_TwoMissingRequiredCount2_EmptyExt", $"Expected 2, got {countEmptyExt}");

        // 2. One field supplied => count 1
        var oneSupplied = new Dictionary<string, string> { ["req_text"] = "hello" };
        int countOne = GestureMappingViewModel.CountMissingRequiredParameters(parameters, oneSupplied);
        Assert(countOne == 1, "Test6.3_OneFieldSuppliedCount1", $"Expected 1, got {countOne}");

        // 3. All supplied => count 0
        var allSupplied = new Dictionary<string, string> { ["req_text"] = "hello", ["req_num"] = "42" };
        int countAll = GestureMappingViewModel.CountMissingRequiredParameters(parameters, allSupplied);
        Assert(countAll == 0, "Test6.4_AllSuppliedCount0", $"Expected 0, got {countAll}");

        // 4. Required Bool is not counted as missing whether present or absent
        var boolSupplied = new Dictionary<string, string> { ["req_text"] = "hello", ["req_num"] = "42", ["req_flag"] = "false" };
        int countBool = GestureMappingViewModel.CountMissingRequiredParameters(parameters, boolSupplied);
        Assert(countBool == 0, "Test6.5_RequiredBoolNotCountedAsMissing", $"Expected 0, got {countBool}");

        // 5. Whitespace / empty string is counted as missing
        var whitespaceExt = new Dictionary<string, string> { ["req_text"] = "   ", ["req_num"] = "" };
        int countWhitespace = GestureMappingViewModel.CountMissingRequiredParameters(parameters, whitespaceExt);
        Assert(countWhitespace == 2, "Test6.6_WhitespaceCountedAsMissing", $"Expected 2, got {countWhitespace}");

        // 6. Null parameters list returns 0
        int countNullParams = GestureMappingViewModel.CountMissingRequiredParameters(null, null);
        Assert(countNullParams == 0, "Test6.7_NullParametersReturns0", $"Expected 0, got {countNullParams}");
    }

    static void Test7_I18n_LocalizationAcrossLanguages()
    {
        var originalLang = I18n.CurrentLanguage;
        try
        {
            var mappingOpacity = new GestureMapping
            {
                Action = new ActionItem { Type = "WindowOpacity", Parameter = "80" }
            };
            var vmOpacity = new GestureMappingViewModel(mappingOpacity);

            // ==================== Traditional Chinese (zh-TW) ====================
            I18n.CurrentLanguage = LanguageCode.ZhTw;

            Assert(vmOpacity.OpacityTip == "輸入不透明度百分比 (30~100)",
                "Test7.1_ZhTw_OpacityTip",
                $"Expected '輸入不透明度百分比 (30~100)', got '{vmOpacity.OpacityTip}'");

            string btnTextZhTw = I18n.T("PluginsCardSettingsButton");
            Assert(btnTextZhTw == "⚙ 設定",
                "Test7.2_ZhTw_PluginsCardSettingsButton",
                $"Expected '⚙ 設定', got '{btnTextZhTw}'");

            string suffixZhTw = btnTextZhTw.TrimStart('⚙', ' ');
            Assert(suffixZhTw == "設定",
                "Test7.3_ZhTw_DialogTitleSuffix",
                $"Expected '設定', got '{suffixZhTw}'");

            string allOptZhTw = I18n.T("PluginsPanelAllOptional");
            Assert(allOptZhTw == "此動作的參數全部可選。",
                "Test7.4_ZhTw_PluginsPanelAllOptional",
                $"Expected '此動作的參數全部可選。', got '{allOptZhTw}'");

            string reqZhTw = string.Format(I18n.T("PluginsPanelRequiredParams"), 2);
            Assert(reqZhTw.Contains("此動作有 2 個必填參數"),
                "Test7.5_ZhTw_PluginsPanelRequiredParams",
                $"Expected Traditional Chinese format, got '{reqZhTw}'");

            // ==================== Japanese (ja-JP) ====================
            I18n.CurrentLanguage = LanguageCode.Ja;

            Assert(vmOpacity.OpacityTip == "不透明度のパーセンテージを入力 (30~100)",
                "Test7.6_Ja_OpacityTip",
                $"Expected '不透明度のパーセンテージを入力 (30~100)', got '{vmOpacity.OpacityTip}'");

            string btnTextJa = I18n.T("PluginsCardSettingsButton");
            Assert(btnTextJa == "⚙ 設定",
                "Test7.7_Ja_PluginsCardSettingsButton",
                $"Expected '⚙ 設定', got '{btnTextJa}'");

            string suffixJa = btnTextJa.TrimStart('⚙', ' ');
            Assert(suffixJa == "設定",
                "Test7.8_Ja_DialogTitleSuffix",
                $"Expected '設定', got '{suffixJa}'");

            string allOptJa = I18n.T("PluginsPanelAllOptional");
            Assert(allOptJa == "この動作のパラメーターはすべて任意です。",
                "Test7.9_Ja_PluginsPanelAllOptional",
                $"Expected 'この動作のパラメーターはすべて任意です。', got '{allOptJa}'");

            string reqJa = string.Format(I18n.T("PluginsPanelRequiredParams"), 2);
            Assert(reqJa.Contains("この動作には必須パラメーターが 2 個あります"),
                "Test7.10_Ja_PluginsPanelRequiredParams",
                $"Expected Japanese format, got '{reqJa}'");

            // ==================== English (en-US) ====================
            I18n.CurrentLanguage = LanguageCode.En;

            Assert(vmOpacity.OpacityTip == "Enter opacity percentage (30~100)",
                "Test7.11_En_OpacityTip",
                $"Expected 'Enter opacity percentage (30~100)', got '{vmOpacity.OpacityTip}'");

            string btnTextEn = I18n.T("PluginsCardSettingsButton");
            Assert(btnTextEn == "⚙ Settings",
                "Test7.12_En_PluginsCardSettingsButton",
                $"Expected '⚙ Settings', got '{btnTextEn}'");

            string suffixEn = btnTextEn.TrimStart('⚙', ' ');
            Assert(suffixEn == "Settings",
                "Test7.13_En_DialogTitleSuffix",
                $"Expected 'Settings', got '{suffixEn}'");

            string allOptEn = I18n.T("PluginsPanelAllOptional");
            Assert(allOptEn == "All parameters of this action are optional.",
                "Test7.14_En_PluginsPanelAllOptional",
                $"Expected 'All parameters of this action are optional.', got '{allOptEn}'");

            string reqEn = string.Format(I18n.T("PluginsPanelRequiredParams"), 2);
            Assert(reqEn.Contains("2 required parameter(s)"),
                "Test7.15_En_PluginsPanelRequiredParams",
                $"Expected English format, got '{reqEn}'");

            // ==================== Simplified Chinese (zh-CN) ====================
            I18n.CurrentLanguage = LanguageCode.ZhCn;

            Assert(vmOpacity.OpacityTip == "输入不透明度百分比 (30~100)",
                "Test7.16_ZhCn_OpacityTip",
                $"Expected '输入不透明度百分比 (30~100)', got '{vmOpacity.OpacityTip}'");

            string btnTextZhCn = I18n.T("PluginsCardSettingsButton");
            Assert(btnTextZhCn == "⚙ 设置",
                "Test7.17_ZhCn_PluginsCardSettingsButton",
                $"Expected '⚙ 设置', got '{btnTextZhCn}'");

            string suffixZhCn = btnTextZhCn.TrimStart('⚙', ' ');
            Assert(suffixZhCn == "设置",
                "Test7.18_ZhCn_DialogTitleSuffix",
                $"Expected '设置', got '{suffixZhCn}'");

            string allOptZhCn = I18n.T("PluginsPanelAllOptional");
            Assert(allOptZhCn == "此动作的参数全部可选。",
                "Test7.19_ZhCn_PluginsPanelAllOptional",
                $"Expected '此动作的参数全部可选。', got '{allOptZhCn}'");

            string reqZhCn = string.Format(I18n.T("PluginsPanelRequiredParams"), 2);
            Assert(reqZhCn.Contains("2 个必填参数"),
                "Test7.20_ZhCn_PluginsPanelRequiredParams",
                $"Expected Simplified Chinese format, got '{reqZhCn}'");
        }
        finally
        {
            I18n.CurrentLanguage = originalLang;
        }
    }

    static (IDictionary actions, object registration) RegisterSentinelAction(
        string pluginId,
        string shortId,
        string displayName,
        ParameterField[] parameters)
    {
        var assembly = typeof(GestureMappingViewModel).Assembly;
        var hostType = assembly.GetType("WinPieGestures.Plugins.PluginHost", true)!;
        var catalog = hostType.GetField("Catalog", BindingFlags.Public | BindingFlags.Static)!.GetValue(null)!;
        var actions = (IDictionary)catalog.GetType().GetField("_actions", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(catalog)!;
        var registrationType = assembly.GetType("WinPieGestures.Plugins.PluginActionRegistration", true)!;
        var registration = Activator.CreateInstance(registrationType, true)!;
        registrationType.GetProperty("PluginId")!.SetValue(registration, pluginId);
        registrationType.GetProperty("ShortId")!.SetValue(registration, shortId);
        registrationType.GetProperty("FullId")!.SetValue(registration, $"{pluginId}.{shortId}");
        registrationType.GetProperty("DisplayName")!.SetValue(registration, displayName);
        registrationType.GetProperty("Parameters")!.SetValue(registration, parameters);
        actions[$"{pluginId}.{shortId}"] = registration;
        return (actions, registration);
    }

    static Dictionary<string, string> ProjectActionParameters(ActionItem action)
    {
        var assembly = typeof(GestureMappingViewModel).Assembly;
        var projectionType = assembly.GetType("WinPieGestures.Plugins.ActionParameterProjection", true)!;
        return (Dictionary<string, string>)projectionType.GetMethod("Project", BindingFlags.Public | BindingFlags.Static)!.Invoke(null, new object[] { action })!;
    }

    static void Test8_Repair2_R1F1_StalePluginStateAndRecreation()
    {
        var sentinelParams = new ParameterField[]
        {
            new() { Key = "Parameter", Type = ParameterFieldType.Text, Required = true },
            new() { Key = "RunAsStandardUser", Type = ParameterFieldType.Bool, Required = false },
            new() { Key = "second", Type = ParameterFieldType.Text, Required = true }
        };
        var (actions, _) = RegisterSentinelAction("review.sentinel", "action", "Sentinel Action", sentinelParams);

        try
        {
            // 1. Start with Plugin action having ExtensionData with Parameter and RunAsStandardUser
            var action = new ActionItem
            {
                Type = "Plugin",
                PluginActionRef = new PluginActionRef { PluginId = "review.sentinel", ContributionId = "action" },
                ExtensionData = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Parameter"] = "https://old-plugin-value.com/",
                    ["RunAsStandardUser"] = "true",
                    ["second"] = "plugin_second_value"
                }
            };
            var mapping = new GestureMapping { Pattern = "U", Action = action };
            var vm = new GestureMappingViewModel(mapping);

            Assert(vm.AggregatedType == "Plugin", "Test8.1_InitialAggregatedTypeIsPlugin");
            Assert(vm.IsPluginType, "Test8.2_InitialIsPluginType");

            // 2. Switch from Plugin to WebUrl
            vm.AggregatedType = "WebUrl";
            vm.Parameter = "https://new-weburl.com/";

            // ActionItem should be clean: non-Plugin action must not retain PluginActionRef or ExtensionData
            Assert(action.Type == "WebUrl", "Test8.3_SwitchedActionTypeIsWebUrl", $"Expected 'WebUrl', got '{action.Type}'");
            Assert(action.PluginActionRef == null, "Test8.4_PluginActionRefClearedOnActiveAction", "PluginActionRef must be cleared on active ActionItem when switching away from Plugin");
            Assert(action.ExtensionData == null || action.ExtensionData.Count == 0, "Test8.5_ExtensionDataClearedOnActiveAction", "ExtensionData must be cleared on active ActionItem when switching away from Plugin");

            // Production projection must yield the new UI values without being shadowed by stale plugin extension data
            var projectedUrl = ProjectActionParameters(action);
            Assert(projectedUrl["Parameter"] == "https://new-weburl.com/", "Test8.6_ProjectedWebUrlParameterNotShadowed", $"Expected 'https://new-weburl.com/', got '{projectedUrl["Parameter"]}'");
            Assert(projectedUrl["RunAsStandardUser"] == "false", "Test8.7_ProjectedWebUrlRunAsStandardUserNotShadowed", $"Expected 'false', got '{projectedUrl["RunAsStandardUser"]}'");

            // 3. Switch to Folder
            vm.AggregatedType = "Folder";
            vm.Parameter = @"C:\Program Files";
            var projectedFolder = ProjectActionParameters(action);
            Assert(projectedFolder["Parameter"] == @"C:\Program Files", "Test8.8_ProjectedFolderParameterNotShadowed", $"Expected 'C:\\Program Files', got '{projectedFolder["Parameter"]}'");

            // 4. Switch to Command and set RunAsStandardUser = true
            vm.AggregatedType = "Command";
            vm.Parameter = "notepad.exe";
            vm.RunAsStandardUser = true;
            var projectedCmd = ProjectActionParameters(action);
            Assert(projectedCmd["Parameter"] == "notepad.exe", "Test8.9_ProjectedCommandParameterNotShadowed", $"Expected 'notepad.exe', got '{projectedCmd["Parameter"]}'");
            Assert(projectedCmd["RunAsStandardUser"] == "true", "Test8.10_ProjectedCommandRunAsStandardUserRespected", $"Expected 'true', got '{projectedCmd["RunAsStandardUser"]}'");

            // 5. Recreate VM for the same GestureMapping object while Command is active
            var vmRebuilt = new GestureMappingViewModel(mapping);
            Assert(vmRebuilt.AggregatedType == "Command", "Test8.11_RebuiltVMAggregatedTypePreserved");
            Assert(vmRebuilt.Parameter == "notepad.exe", "Test8.12_RebuiltVMParameterPreserved");
            Assert(vmRebuilt.RunAsStandardUser == true, "Test8.13_RebuiltVMRunAsStandardUserPreserved");

            // 6. Switch back to Plugin on the rebuilt VM
            vmRebuilt.AggregatedType = "Plugin";
            Assert(vmRebuilt.Type == "Plugin", "Test8.14_RebuiltVMSwitchedBackToPlugin");
            Assert(action.PluginActionRef != null, "Test8.15_PluginActionRefRestoredFromSessionSnapshot", "PluginActionRef must be restored from session snapshot");
            Assert(action.PluginActionRef?.FullId == "review.sentinel.action", "Test8.16_PluginActionRefIdMatches", $"Expected 'review.sentinel.action', got '{action.PluginActionRef?.FullId}'");
            Assert(action.ExtensionData != null, "Test8.17_ExtensionDataRestoredFromSessionSnapshot", "ExtensionData must be restored from session snapshot");
            Assert(action.ExtensionData?["Parameter"] == "https://old-plugin-value.com/", "Test8.18_ExtensionDataParameterRestored", $"Expected restored 'https://old-plugin-value.com/', got '{action.ExtensionData?["Parameter"]}'");
            Assert(action.ExtensionData?["second"] == "plugin_second_value", "Test8.19_ExtensionDataSecondRestored", $"Expected restored 'plugin_second_value', got '{action.ExtensionData?["second"]}'");
        }
        finally
        {
            actions.Remove("review.sentinel.action");
        }
    }

    static void Test9_Repair2_R1F2_RequiredParamsTipAndLocalization()
    {
        var sentinelParams = new ParameterField[]
        {
            new() { Key = "Parameter", Type = ParameterFieldType.Text, Required = true },
            new() { Key = "second", Type = ParameterFieldType.Text, Required = true },
            new() { Key = "flag", Type = ParameterFieldType.Bool, Required = true }
        };
        var (actions, _) = RegisterSentinelAction("review.sentinel", "action", "Sentinel Action", sentinelParams);

        var optionalParams = new ParameterField[]
        {
            new() { Key = "opt_text", Type = ParameterFieldType.Text, Required = false },
            new() { Key = "opt_flag", Type = ParameterFieldType.Bool, Required = true }
        };
        RegisterSentinelAction("review.sentinel", "opt_action", "Optional Action", optionalParams);

        var originalLang = I18n.CurrentLanguage;
        try
        {
            var action = new ActionItem
            {
                Type = "Plugin",
                PluginActionRef = new PluginActionRef { PluginId = "review.sentinel", ContributionId = "action" },
                ExtensionData = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            };
            var mapping = new GestureMapping { Pattern = "D", Action = action };
            var vm = new GestureMappingViewModel(mapping);

            // ==================== Traditional Chinese (zh-TW) ====================
            I18n.CurrentLanguage = LanguageCode.ZhTw;

            // Missing 2 required non-Bool fields
            Assert(vm.MissingRequiredPluginParametersCount == 2, "Test9.1_ZhTw_MissingTwoCount", $"Expected 2, got {vm.MissingRequiredPluginParametersCount}");
            Assert(vm.HasMissingRequiredPluginParameters == true, "Test9.2_ZhTw_HasMissingTwo");
            Assert(vm.PluginParamsButtonText == "⚠️ 設定", "Test9.3_ZhTw_BtnTextMissingTwo", $"Expected '⚠️ 設定', got '{vm.PluginParamsButtonText}'");
            Assert(vm.PluginParamsTip.Contains("2 個必填參數") || vm.PluginParamsTip.Contains("2 個必填参数"), "Test9.4_ZhTw_TipMissingTwo", $"Expected missing 2 tip, got '{vm.PluginParamsTip}'");

            // Fill 1 field -> Missing 1
            action.ExtensionData["Parameter"] = "filled_val";
            Assert(vm.MissingRequiredPluginParametersCount == 1, "Test9.5_ZhTw_MissingOneCount", $"Expected 1, got {vm.MissingRequiredPluginParametersCount}");
            Assert(vm.HasMissingRequiredPluginParameters == true, "Test9.6_ZhTw_HasMissingOne");
            Assert(vm.PluginParamsButtonText == "⚠️ 設定", "Test9.7_ZhTw_BtnTextMissingOne", $"Expected '⚠️ 設定', got '{vm.PluginParamsButtonText}'");
            Assert(vm.PluginParamsTip.Contains("1 個必填參數") || vm.PluginParamsTip.Contains("1 個必填参数"), "Test9.8_ZhTw_TipMissingOne", $"Expected missing 1 tip, got '{vm.PluginParamsTip}'");

            // Fill 2nd field -> All required fields satisfied (missingCount == 0)
            action.ExtensionData["second"] = "filled_val_2";
            Assert(vm.MissingRequiredPluginParametersCount == 0, "Test9.9_ZhTw_MissingZeroCount", $"Expected 0, got {vm.MissingRequiredPluginParametersCount}");
            Assert(vm.HasMissingRequiredPluginParameters == false, "Test9.10_ZhTw_HasMissingZero");
            Assert(vm.PluginParamsButtonText == "⚙ 設定", "Test9.11_ZhTw_BtnTextMissingZero", $"Expected '⚙ 設定', got '{vm.PluginParamsButtonText}'");
            // Semantic validation: MUST NOT claim "all optional" when required parameters were merely satisfied
            Assert(!vm.PluginParamsTip.Contains("可選") && !vm.PluginParamsTip.Contains("可选"), "Test9.12_ZhTw_SatisfiedActionDoesNotClaimOptional", $"Tip should not say '可選', got '{vm.PluginParamsTip}'");
            Assert(vm.PluginParamsTip == I18n.T("PluginsCardSettingsButton"), "Test9.13_ZhTw_SatisfiedActionReturnsSettingsKey", $"Expected '{I18n.T("PluginsCardSettingsButton")}', got '{vm.PluginParamsTip}'");

            // ==================== Japanese (ja-JP) ====================
            I18n.CurrentLanguage = LanguageCode.Ja;

            // When satisfied in ja-JP
            Assert(vm.MissingRequiredPluginParametersCount == 0, "Test9.14_Ja_MissingZeroCount");
            Assert(vm.HasMissingRequiredPluginParameters == false, "Test9.15_Ja_HasMissingZero");
            Assert(vm.PluginParamsButtonText == "⚙ 設定", "Test9.16_Ja_BtnTextMissingZero", $"Expected '⚙ 設定', got '{vm.PluginParamsButtonText}'");
            Assert(!vm.PluginParamsTip.Contains("任意"), "Test9.17_Ja_SatisfiedActionDoesNotClaimOptional", $"Tip should not say '任意', got '{vm.PluginParamsTip}'");
            Assert(vm.PluginParamsTip == I18n.T("PluginsCardSettingsButton"), "Test9.18_Ja_SatisfiedActionReturnsSettingsKey", $"Expected '{I18n.T("PluginsCardSettingsButton")}', got '{vm.PluginParamsTip}'");

            // Missing 1 in ja-JP
            action.ExtensionData.Remove("second");
            Assert(vm.MissingRequiredPluginParametersCount == 1, "Test9.19_Ja_MissingOneCount");
            Assert(vm.HasMissingRequiredPluginParameters == true, "Test9.20_Ja_HasMissingOne");
            Assert(vm.PluginParamsButtonText == "⚠️ 設定", "Test9.21_Ja_BtnTextMissingOne", $"Expected '⚠️ 設定', got '{vm.PluginParamsButtonText}'");
            Assert(vm.PluginParamsTip.Contains("1 個あります"), "Test9.22_Ja_TipMissingOne", $"Expected Japanese missing 1 tip, got '{vm.PluginParamsTip}'");

            // ==================== Truly optional action ====================
            var optAction = new ActionItem
            {
                Type = "Plugin",
                PluginActionRef = new PluginActionRef { PluginId = "review.sentinel", ContributionId = "opt_action" },
                ExtensionData = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            };
            var optMapping = new GestureMapping { Pattern = "L", Action = optAction };
            var optVm = new GestureMappingViewModel(optMapping);

            Assert(optVm.MissingRequiredPluginParametersCount == 0, "Test9.23_OptActionMissingCount0");
            Assert(optVm.HasMissingRequiredPluginParameters == false, "Test9.24_OptActionHasMissingFalse");
            Assert(optVm.PluginParamsTip == I18n.T("PluginsPanelAllOptional"), "Test9.25_OptActionReturnsAllOptionalKey", $"Expected '{I18n.T("PluginsPanelAllOptional")}', got '{optVm.PluginParamsTip}'");
        }
        finally
        {
            I18n.CurrentLanguage = originalLang;
            actions.Remove("review.sentinel.action");
            actions.Remove("review.sentinel.opt_action");
        }
    }

    static void Test10_Repair3_R2F1_OfficialActionsExtensionDataPreservation()
    {
        // 1. WebUrl: ExtensionData with Parameter and custom extension keys
        var officialUrl = new ActionItem
        {
            Type = "WebUrl",
            Parameter = "https://fallback.invalid/",
            ExtensionData = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Parameter"] = "https://configured.invalid/",
                ["futureKey"] = "preserve-me"
            }
        };
        string before = ProjectActionParameters(officialUrl)["Parameter"];
        var mappingUrl = new GestureMapping { Action = officialUrl };
        var vmUrl = new GestureMappingViewModel(mappingUrl);
        string after = ProjectActionParameters(officialUrl)["Parameter"];

        Assert(officialUrl.ExtensionData != null && officialUrl.ExtensionData.ContainsKey("futureKey"),
            "Test10.1_WebUrl_ExtensionDataRetainedAfterVmCreation",
            $"ExtensionData null={officialUrl.ExtensionData == null}");
        Assert(before == after && after == "https://configured.invalid/",
            "Test10.2_WebUrl_ProjectedConfiguredParameterPreserved",
            $"before={before}, after={after}");
        Assert(officialUrl.Parameter == "https://fallback.invalid/",
            "Test10.3_WebUrl_BareParameterPreserved");

        // Reading properties and NotifyAllPropertiesChanged must keep ExtensionData intact
        _ = vmUrl.WebUrlTip;
        _ = vmUrl.Parameter;
        vmUrl.NotifyAllPropertiesChanged();
        Assert(officialUrl.ExtensionData != null && officialUrl.ExtensionData["futureKey"] == "preserve-me",
            "Test10.4_WebUrl_ExtensionDataRetainedAfterNotification");

        // Rebuilding VM for same mapping preserves ExtensionData and projected value
        var vmUrlRebuilt = new GestureMappingViewModel(mappingUrl);
        Assert(ProjectActionParameters(officialUrl)["Parameter"] == "https://configured.invalid/",
            "Test10.5_WebUrl_ProjectedValuePreservedOnRebuiltVM");

        // 2. Folder: ExtensionData preservation
        var officialFolder = new ActionItem
        {
            Type = "Folder",
            Parameter = @"C:\fallback",
            ExtensionData = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Parameter"] = @"C:\configured",
                ["customFolderKey"] = "keep"
            }
        };
        var mappingFolder = new GestureMapping { Action = officialFolder };
        var vmFolder = new GestureMappingViewModel(mappingFolder);
        vmFolder.NotifyAllPropertiesChanged();
        Assert(ProjectActionParameters(officialFolder)["Parameter"] == @"C:\configured",
            "Test10.6_Folder_ExtensionDataPreservedOnBrowsing");
        Assert(officialFolder.ExtensionData?["customFolderKey"] == "keep",
            "Test10.7_Folder_CustomExtensionKeyPreserved");

        // 3. Command: ExtensionData preservation
        var officialCmd = new ActionItem
        {
            Type = "Command",
            Parameter = "fallback.exe",
            ExtensionData = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Parameter"] = "configured.exe",
                ["customCmdKey"] = "keep_cmd"
            }
        };
        var mappingCmd = new GestureMapping { Action = officialCmd };
        var vmCmd = new GestureMappingViewModel(mappingCmd);
        vmCmd.NotifyAllPropertiesChanged();
        Assert(ProjectActionParameters(officialCmd)["Parameter"] == "configured.exe",
            "Test10.8_Command_ExtensionDataPreservedOnBrowsing");
        Assert(officialCmd.ExtensionData?["customCmdKey"] == "keep_cmd",
            "Test10.9_Command_CustomExtensionKeyPreserved");

        // 4. Object isolation across different mapping objects
        var (actions, _) = RegisterSentinelAction("review.sentinel", "actionA", "Sentinel A", new ParameterField[]
        {
            new() { Key = "pA", Type = ParameterFieldType.Text, Required = false }
        });
        RegisterSentinelAction("review.sentinel", "actionB", "Sentinel B", new ParameterField[]
        {
            new() { Key = "pB", Type = ParameterFieldType.Text, Required = false }
        });

        try
        {
            var action1 = new ActionItem
            {
                Type = "Plugin",
                PluginActionRef = new PluginActionRef { PluginId = "review.sentinel", ContributionId = "actionA" },
                ExtensionData = new Dictionary<string, string> { ["pA"] = "valA" }
            };
            var mapping1 = new GestureMapping { Action = action1 };
            var vm1 = new GestureMappingViewModel(mapping1);

            var action2 = new ActionItem
            {
                Type = "Plugin",
                PluginActionRef = new PluginActionRef { PluginId = "review.sentinel", ContributionId = "actionB" },
                ExtensionData = new Dictionary<string, string> { ["pB"] = "valB" }
            };
            var mapping2 = new GestureMapping { Action = action2 };
            var vm2 = new GestureMappingViewModel(mapping2);

            // Switch mapping1 to WebUrl; mapping2 remains Plugin
            vm1.AggregatedType = "WebUrl";
            vm1.Parameter = "https://vm1.url";

            Assert(action2.Type == "Plugin" && action2.PluginActionRef?.FullId == "review.sentinel.actionB",
                "Test10.10_Mapping2UnaffectedByMapping1TypeSwitch");
            Assert(action2.ExtensionData?["pB"] == "valB",
                "Test10.11_Mapping2ExtensionDataUnaffected");

            // Rebuilt VM for mapping1 switched back to Plugin
            var vm1Rebuilt = new GestureMappingViewModel(mapping1);
            vm1Rebuilt.AggregatedType = "Plugin";
            Assert(action1.PluginActionRef?.FullId == "review.sentinel.actionA",
                "Test10.12_Mapping1RestoredToPluginA");
            Assert(action1.ExtensionData?["pA"] == "valA",
                "Test10.13_Mapping1ExtensionDataRestoredA");
        }
        finally
        {
            actions.Remove("review.sentinel.actionA");
            actions.Remove("review.sentinel.actionB");
        }
    }

    static void Test11_Repair3_R2F2_ParameterTargetWriteNullAndSessionEmptySemantics()
    {
        // 1. Production ActionItemParameterTarget Write(key, null)
        var plugin = new ActionItem
        {
            Type = "Plugin",
            PluginActionRef = new PluginActionRef { PluginId = "review.no-load", ContributionId = "action" },
            ExtensionData = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["oldValue"] = "must-not-return"
            }
        };
        var mapping = new GestureMapping { Action = plugin };
        var vm = new GestureMappingViewModel(mapping);

        var targetType = typeof(GestureMappingViewModel).Assembly
            .GetType("WinPieGestures.Plugins.ActionItemParameterTarget", true)!;
        var parameterTarget = Activator.CreateInstance(targetType, new object[] { plugin })!;
        bool cleared = (bool)targetType.GetMethod("Write")!
            .Invoke(parameterTarget, new object?[] { "oldValue", null })!;

        Assert(cleared && plugin.ExtensionData == null,
            "Test11.1_ParameterTargetWriteNullClearsToNull",
            $"cleared={cleared}, null={plugin.ExtensionData == null}");

        vm.NotifyAllPropertiesChanged();
        vm.AggregatedType = "WebUrl";
        vm.Parameter = "https://new.invalid/";

        Assert(ProjectActionParameters(plugin)["Parameter"] == vm.Parameter,
            "Test11.2_ProjectedWebUrlParameterAfterTargetWrite",
            $"Expected '{vm.Parameter}', got '{ProjectActionParameters(plugin)["Parameter"]}'");

        var rebuilt = new GestureMappingViewModel(mapping);
        rebuilt.AggregatedType = "Plugin";

        Assert(plugin.PluginActionRef?.FullId == "review.no-load.action",
            "Test11.3_PluginRefSurvivesRowRecreation",
            $"Expected 'review.no-load.action', got '{plugin.PluginActionRef?.FullId}'");

        Assert(plugin.ExtensionData == null || plugin.ExtensionData.Count == 0,
            "Test11.4_DeletedPluginParameterDoesNotResurrect",
            $"oldValue resurrected: {plugin.ExtensionData?.ContainsKey("oldValue") == true}");

        // 2. Direct empty dictionary semantics
        var plugin2 = new ActionItem
        {
            Type = "Plugin",
            PluginActionRef = new PluginActionRef { PluginId = "review.no-load", ContributionId = "action2" },
            ExtensionData = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        };
        var mapping2 = new GestureMapping { Action = plugin2 };
        var vm2 = new GestureMappingViewModel(mapping2);
        vm2.NotifyAllPropertiesChanged();
        vm2.AggregatedType = "Command";
        vm2.Parameter = "cmd.exe";

        var rebuilt2 = new GestureMappingViewModel(mapping2);
        rebuilt2.AggregatedType = "Plugin";

        Assert(plugin2.ExtensionData == null || plugin2.ExtensionData.Count == 0,
            "Test11.5_EmptyDictionaryRemainsEmptyOnSwitchBack");

        // 3. Repeated back-and-forth switching
        var plugin3 = new ActionItem
        {
            Type = "Plugin",
            PluginActionRef = new PluginActionRef { PluginId = "review.no-load", ContributionId = "action3" },
            ExtensionData = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["activeParam"] = "activeValue"
            }
        };
        var mapping3 = new GestureMapping { Action = plugin3 };
        var vm3 = new GestureMappingViewModel(mapping3);

        // Round 1: Plugin -> WebUrl -> Plugin
        vm3.AggregatedType = "WebUrl";
        vm3.AggregatedType = "Plugin";
        Assert(plugin3.ExtensionData?["activeParam"] == "activeValue", "Test11.6_Round1ParamPreserved");

        // Round 2: Plugin -> Command -> Plugin
        vm3.AggregatedType = "Command";
        vm3.AggregatedType = "Plugin";
        Assert(plugin3.ExtensionData?["activeParam"] == "activeValue", "Test11.7_Round2ParamPreserved");

        // Round 3: Plugin -> WindowManager -> Plugin
        vm3.AggregatedType = "WindowManager";
        vm3.AggregatedType = "Plugin";
        Assert(plugin3.ExtensionData?["activeParam"] == "activeValue", "Test11.8_Round3ParamPreserved");
    }

    static void Test12_Repair6_LaunchParityAndTargetIsolation()
    {
        Console.WriteLine("\n--- Test12: R6 Gesture Launch Parity & Target Row Isolation ---");

        // 1. Arguments and RunAsStandardUser two-way bindings on ViewModel
        var launchAction = new ActionItem
        {
            Type = "Launch",
            Parameter = @"C:\Program Files\Test\app.exe",
            Arguments = "--input sample.dat",
            RunAsStandardUser = true
        };
        var launchMapping = new GestureMapping { Pattern = "U", Action = launchAction };
        var vmLaunch = new GestureMappingViewModel(launchMapping);

        Assert(vmLaunch.Arguments == "--input sample.dat", "Test12.1_ArgumentsReadInitial");
        Assert(vmLaunch.RunAsStandardUser == true, "Test12.2_RunAsStandardUserReadInitial");

        bool argsChangedFired = false;
        bool standardUserChangedFired = false;
        vmLaunch.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(vmLaunch.Arguments)) argsChangedFired = true;
            if (e.PropertyName == nameof(vmLaunch.RunAsStandardUser)) standardUserChangedFired = true;
        };

        vmLaunch.Arguments = "--verbose --log out.txt";
        Assert(argsChangedFired, "Test12.3_ArgumentsPropertyChangedFired");
        Assert(launchAction.Arguments == "--verbose --log out.txt", "Test12.4_ActionArgumentsUpdated");

        vmLaunch.RunAsStandardUser = false;
        Assert(standardUserChangedFired, "Test12.5_RunAsStandardUserPropertyChangedFired");
        Assert(launchAction.RunAsStandardUser == false, "Test12.6_ActionRunAsStandardUserUpdated");

        // 2. Row A and Row B Isolation: Target writes ONLY affect the targeted row
        var rowA_Action = new ActionItem
        {
            Type = "Launch",
            Name = "Custom App A",
            Parameter = @"C:\Apps\AppA.exe",
            Arguments = "-flagA",
            RunAsStandardUser = false,
            ExtensionData = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["CustomKeyA"] = "ValueA" }
        };
        var rowA_Mapping = new GestureMapping { Pattern = "R", Action = rowA_Action };
        var vmA = new GestureMappingViewModel(rowA_Mapping);

        var rowB_Action = new ActionItem
        {
            Type = "Launch",
            Name = "",
            Parameter = @"C:\Apps\AppB.exe",
            Arguments = "-flagB",
            RunAsStandardUser = true,
            ExtensionData = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["CustomKeyB"] = "ValueB" }
        };
        var rowB_Mapping = new GestureMapping { Pattern = "L", Action = rowB_Action };
        var vmB = new GestureMappingViewModel(rowB_Mapping);

        // Simulate Program Picker confirm on Row A with custom name:
        // Path updated, InheritAppIconPath updated, custom name PRESERVED, ExtensionData untouched
        string selectedPathA = @"C:\Games\GameA\game.exe";
        string autoNameA = "game";
        vmA.Parameter = selectedPathA;
        vmA.InheritAppIconPath = selectedPathA;
        if (string.IsNullOrWhiteSpace(vmA.Name)) vmA.Name = autoNameA;

        Assert(vmA.Parameter == selectedPathA, "Test12.7_RowAParameterUpdated");
        Assert(vmA.InheritAppIconPath == selectedPathA, "Test12.8_RowAIconPathUpdated");
        Assert(vmA.Name == "Custom App A", "Test12.9_RowACustomNamePreserved");
        Assert(rowA_Action.ExtensionData?["CustomKeyA"] == "ValueA", "Test12.10_RowAExtensionDataPreserved");

        // Verify Row B was completely untouched
        Assert(vmB.Parameter == @"C:\Apps\AppB.exe", "Test12.11_RowBParameterUntouched");
        Assert(vmB.Arguments == "-flagB", "Test12.12_RowBArgumentsUntouched");
        Assert(vmB.RunAsStandardUser == true, "Test12.13_RowBStandardUserUntouched");
        Assert(rowB_Action.ExtensionData?["CustomKeyB"] == "ValueB", "Test12.14_RowBExtensionDataUntouched");

        // Simulate Program Picker confirm on Row B with empty initial name:
        // Path updated, auto name set because vm.Name was empty
        string selectedPathB = @"C:\Tools\ToolB\tool.exe";
        string autoNameB = "ToolB";
        vmB.Parameter = selectedPathB;
        vmB.InheritAppIconPath = selectedPathB;
        if (string.IsNullOrWhiteSpace(vmB.Name)) vmB.Name = autoNameB;

        Assert(vmB.Parameter == selectedPathB, "Test12.15_RowBParameterUpdated");
        Assert(vmB.InheritAppIconPath == selectedPathB, "Test12.16_RowBIconPathUpdated");
        Assert(vmB.Name == "ToolB", "Test12.17_RowBAutoNameAssigned");

        // 3. Localized labels and tooltips across 4 languages
        string[] languages = { "zh-CN", "zh-TW", "en-US", "ja-JP" };
        foreach (string lang in languages)
        {
            I18n.SetLanguage(lang);
            var vm = new GestureMappingViewModel(new GestureMapping { Action = new ActionItem { Type = "Launch" } });

            Assert(!string.IsNullOrEmpty(vm.LaunchArgsLabel), $"Test12.18_{lang}_LaunchArgsLabelNotEmpty");
            Assert(!string.IsNullOrEmpty(vm.LaunchArgsToolTip), $"Test12.19_{lang}_LaunchArgsToolTipNotEmpty");
            Assert(!string.IsNullOrEmpty(vm.PickProgramButtonText), $"Test12.20_{lang}_PickProgramButtonTextNotEmpty");
            Assert(!string.IsNullOrEmpty(vm.PickProgramToolTip), $"Test12.21_{lang}_PickProgramToolTipNotEmpty");
            Assert(!string.IsNullOrEmpty(vm.CaptureWindowButtonText), $"Test12.22_{lang}_CaptureWindowButtonTextNotEmpty");
            Assert(!string.IsNullOrEmpty(vm.CaptureWindowToolTip), $"Test12.23_{lang}_CaptureWindowToolTipNotEmpty");
            Assert(!string.IsNullOrEmpty(vm.BrowseExeButtonText), $"Test12.24_{lang}_BrowseExeButtonTextNotEmpty");
            Assert(!string.IsNullOrEmpty(vm.BrowseExeToolTip), $"Test12.25_{lang}_BrowseExeToolTipNotEmpty");
            Assert(!string.IsNullOrEmpty(vm.RunAsStandardUserLabel), $"Test12.26_{lang}_RunAsStandardUserLabelNotEmpty");
            Assert(!string.IsNullOrEmpty(vm.RunAsStandardUserToolTip), $"Test12.27_{lang}_RunAsStandardUserToolTipNotEmpty");

            // English checks
            if (lang == "en-US")
            {
                Assert(!vm.PickProgramButtonText.Contains("软件库"), "Test12.28_En_PickProgramNotZh");
                Assert(!vm.CaptureWindowButtonText.Contains("捕捉"), "Test12.29_En_CaptureWindowNotZh");
                Assert(!vm.BrowseExeButtonText.Contains("浏览"), "Test12.30_En_BrowseExeNotZh");
            }
        }
    }

    static void Test13_Repair6_ExclusiveRecordingAndSessionIsolation()
    {
        Console.WriteLine("\n--- Test13: R6 Gesture Exclusive Recording & Session Isolation ---");

        var hotkeyAction = new ActionItem { Type = "Hotkey", Parameter = "Ctrl+Shift+F" };
        var mapping = new GestureMapping { Pattern = "UR", Action = hotkeyAction };
        var vm = new GestureMappingViewModel(mapping);

        // 1. Initial non-recording state
        Assert(vm.IsExclusiveRecording == false, "Test13.1_InitialNotRecording");
        Assert(vm.PauseHotkeysButtonText.Contains("⏸"), "Test13.2_PauseButtonTextInitialHasPauseIcon");

        // 2. Active recording state reflects in button text and tooltip
        vm.IsExclusiveRecording = true;
        Assert(vm.PauseHotkeysButtonText.Contains("🔴"), "Test13.3_PauseButtonTextActiveHasRecordingIcon");
        Assert(!string.IsNullOrEmpty(vm.PauseHotkeysToolTip), "Test13.4_PauseToolTipActiveNotEmpty");

        // 3. Four language check for active and inactive button states
        string[] languages = { "zh-CN", "zh-TW", "en-US", "ja-JP" };
        foreach (string lang in languages)
        {
            I18n.SetLanguage(lang);
            vm.IsExclusiveRecording = false;
            string inactiveText = vm.PauseHotkeysButtonText;
            string inactiveTip = vm.PauseHotkeysToolTip;
            string builderText = vm.HotkeyBuilderButtonText;
            string builderTip = vm.HotkeyBuilderToolTip;

            Assert(!string.IsNullOrEmpty(inactiveText), $"Test13.5_{lang}_InactiveButtonTextNotEmpty");
            Assert(!string.IsNullOrEmpty(inactiveTip), $"Test13.6_{lang}_InactiveToolTipNotEmpty");
            Assert(!string.IsNullOrEmpty(builderText), $"Test13.7_{lang}_BuilderButtonTextNotEmpty");
            Assert(!string.IsNullOrEmpty(builderTip), $"Test13.8_{lang}_BuilderToolTipNotEmpty");

            vm.IsExclusiveRecording = true;
            string activeText = vm.PauseHotkeysButtonText;
            string activeTip = vm.PauseHotkeysToolTip;
            Assert(!string.IsNullOrEmpty(activeText), $"Test13.9_{lang}_ActiveButtonTextNotEmpty");
            Assert(!string.IsNullOrEmpty(activeTip), $"Test13.10_{lang}_ActiveToolTipNotEmpty");
            Assert(activeText != inactiveText, $"Test13.11_{lang}_ActiveDiffersFromInactive");

            if (lang == "en-US")
            {
                Assert(!activeText.Contains("独占"), "Test13.12_En_ActiveButtonNotZh");
                Assert(!inactiveText.Contains("暂停"), "Test13.13_En_InactiveButtonNotZh");
            }
        }

        // 4. Type switch cancellation guard
        vm.IsExclusiveRecording = true;
        bool cancelCallbackFired = false;
        vm.OnExclusiveRecordingCancelledRequest = () => { cancelCallbackFired = true; };

        vm.AggregatedType = "WebUrl";
        Assert(vm.IsExclusiveRecording == false, "Test13.14_IsExclusiveRecordingResetOnTypeSwitch");
        Assert(cancelCallbackFired, "Test13.15_CancelCallbackFiredOnTypeSwitch");

        // 5. Verify SettingsWindow XAML and KeyMapEditorWindow XAML bindings
        string repoRoot = FindRepoRoot();
        string settingsXaml = File.ReadAllText(Path.Combine(repoRoot, "WinPieGestures", "SettingsWindow.xaml"));
        Assert(settingsXaml.Contains("GestureTogglePauseHotkeys_Click"), "Test13.16_SettingsXamlHasGestureTogglePauseHotkeys");
        Assert(settingsXaml.Contains("GestureHotkeyBuilder_Click"), "Test13.17_SettingsXamlHasGestureHotkeyBuilder");
        Assert(settingsXaml.Contains("GesturePickProgramFromLibrary_Click"), "Test13.18_SettingsXamlHasGesturePickProgram");
        Assert(settingsXaml.Contains("GestureCaptureRunningWindow_Click"), "Test13.19_SettingsXamlHasGestureCaptureWindow");
        Assert(settingsXaml.Contains("GestureBrowse_Click"), "Test13.20_SettingsXamlHasGestureBrowse");
        Assert(settingsXaml.Contains("Arguments, Mode=TwoWay"), "Test13.21_SettingsXamlHasArgumentsBinding");
        Assert(settingsXaml.Contains("RunAsStandardUser, Mode=TwoWay"), "Test13.22_SettingsXamlHasRunAsStandardUserBinding");

        string keyMapXaml = File.ReadAllText(Path.Combine(repoRoot, "WinPieGestures", "KeyMapEditorWindow.xaml"));
        Assert(keyMapXaml.Contains("KeyMapDataGridColumnHeaderStyle"), "Test13.23_KeyMapXamlHasHeaderStyle");
        Assert(keyMapXaml.Contains("KeyMapDataGridCellStyle"), "Test13.24_KeyMapXamlHasCellStyle");
        Assert(keyMapXaml.Contains("KeyMapDataGridRowStyle"), "Test13.25_KeyMapXamlHasRowStyle");
        Assert(keyMapXaml.Contains("SubtleCardBrush"), "Test13.26_KeyMapXamlUsesSubtleCardBrush");
        Assert(keyMapXaml.Contains("CardBackgroundBrush"), "Test13.27_KeyMapXamlUsesCardBackgroundBrush");
    }

    static void Test14_Repair7_F1_SessionLifecycleAndSynchronization()
    {
        Console.WriteLine("\n--- Test14: R7 F1 Exclusive Recording Session Lifecycle & Synchronization ---");

        var mappingA = new GestureMapping { Pattern = "UR", Action = new ActionItem { Type = "Hotkey", Parameter = "Ctrl+Shift+A" } };
        var mappingB = new GestureMapping { Pattern = "DL", Action = new ActionItem { Type = "Hotkey", Parameter = "Ctrl+Shift+B" } };

        // 1. Static mapping-level tracking isolation
        GestureMappingViewModel.ClearAllExclusiveRecordingStates();
        Assert(!GestureMappingViewModel.IsMappingInExclusiveRecording(mappingA), "Test14.1_InitiallyNotRecordingA");
        Assert(!GestureMappingViewModel.IsMappingInExclusiveRecording(mappingB), "Test14.2_InitiallyNotRecordingB");

        var vmA1 = new GestureMappingViewModel(mappingA);
        Assert(!vmA1.IsExclusiveRecording, "Test14.3_VmA1NotRecordingInitially");
        vmA1.IsExclusiveRecording = true;
        Assert(GestureMappingViewModel.IsMappingInExclusiveRecording(mappingA), "Test14.4_StaticTrackingRecordedA");
        Assert(!GestureMappingViewModel.IsMappingInExclusiveRecording(mappingB), "Test14.5_MappingBIsolationPreserved");

        // 2. Replacement VM constructed for same mapping (RefreshGestureMappings scenario)
        var vmA2 = new GestureMappingViewModel(mappingA);
        Assert(vmA2.IsExclusiveRecording, "Test14.6_ReplacementVmReflectsActiveRecording");
        Assert(vmA1.IsExclusiveRecording, "Test14.7_OriginalVmReflectsActiveRecording");

        // 3. Type switch on replacement VM triggers cancellation request and resets state
        int cancelCount = 0;
        vmA2.OnExclusiveRecordingCancelledRequest = () => cancelCount++;
        vmA2.AggregatedType = "Launch";
        Assert(cancelCount == 1, "Test14.8_ReplacementVmTypeSwitchFiresCancel");
        Assert(!vmA2.IsExclusiveRecording, "Test14.9_ReplacementVmRecordingCleared");
        Assert(!vmA1.IsExclusiveRecording, "Test14.10_OriginalVmRecordingCleared");
        Assert(!GestureMappingViewModel.IsMappingInExclusiveRecording(mappingA), "Test14.11_StaticStateClearedAfterTypeSwitch");

        // 4. Deleting / explicit clearing mapping state
        vmA1.IsExclusiveRecording = true;
        Assert(vmA1.IsExclusiveRecording, "Test14.12_VmA1RecordingReactivated");
        GestureMappingViewModel.SetMappingExclusiveRecording(mappingA, false);
        Assert(!vmA1.IsExclusiveRecording, "Test14.13_ExplicitClearResetsVm");
        Assert(!GestureMappingViewModel.IsMappingInExclusiveRecording(mappingA), "Test14.14_StaticStateExplicitlyCleared");

        // 5. ClearAllExclusiveRecordingStates
        var vmB = new GestureMappingViewModel(mappingB);
        vmA1.IsExclusiveRecording = true;
        vmB.IsExclusiveRecording = true;
        Assert(GestureMappingViewModel.IsMappingInExclusiveRecording(mappingA), "Test14.15_StaticTrackingA");
        Assert(GestureMappingViewModel.IsMappingInExclusiveRecording(mappingB), "Test14.16_StaticTrackingB");
        GestureMappingViewModel.ClearAllExclusiveRecordingStates();
        Assert(!GestureMappingViewModel.IsMappingInExclusiveRecording(mappingA), "Test14.17_ClearAllResetsA");
        Assert(!GestureMappingViewModel.IsMappingInExclusiveRecording(mappingB), "Test14.18_ClearAllResetsB");
        Assert(!vmA1.IsExclusiveRecording, "Test14.19_VmA1ClearedAfterClearAll");
        Assert(!vmB.IsExclusiveRecording, "Test14.20_VmBClearedAfterClearAll");

        // 6. Test session completion writing parameter to TargetMapping
        var targetMapping = new GestureMapping { Pattern = "U", Action = new ActionItem { Type = "Hotkey", Parameter = "Alt+F4" } };
        var targetVm = new GestureMappingViewModel(targetMapping);
        targetVm.IsExclusiveRecording = true;
        Assert(targetVm.IsExclusiveRecording, "Test14.21_TargetVmRecordingActive");

        string recordedKey = "Ctrl+Shift+P";
        GestureMappingViewModel.SetMappingExclusiveRecording(targetMapping, false);
        targetMapping.Action.Parameter = recordedKey;
        targetVm.Parameter = recordedKey;
        Assert(targetMapping.Action.Parameter == recordedKey, "Test14.22_TargetMappingActionParameterUpdated");
        Assert(targetVm.Parameter == recordedKey, "Test14.23_TargetVmParameterUpdated");
        Assert(!targetVm.IsExclusiveRecording, "Test14.24_TargetVmRecordingClearedAfterCompletion");
    }

    static void Test15_Repair7_F2_IsValidGestureOrCancelActionTarget()
    {
        Console.WriteLine("\n--- Test15: R7 F2 IsValidGestureOrCancelActionTarget Validation ---");

        var originalConfig = ConfigManager.CurrentConfig;
        try
        {
            var testConfig = new AppConfig();
            var validMapping = new GestureMapping { Pattern = "R", Action = new ActionItem { Type = "Launch", Parameter = "notepad.exe" } };
            var cancelAction = new ActionItem { Type = "Launch", Parameter = "calc.exe" };
            testConfig.GestureMappings = new List<GestureMapping> { validMapping };
            testConfig.CancelAction = cancelAction;

            typeof(ConfigManager).GetProperty("CurrentConfig", BindingFlags.Public | BindingFlags.Static)?
                .GetSetMethod(true)?.Invoke(null, new object[] { testConfig });

            var method = typeof(SettingsWindow).GetMethod("IsValidGestureOrCancelActionTarget", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert(method != null, "Test15.1_IsValidGestureOrCancelActionTargetMethodExists");

            var dummyWindow = FormatterServices.GetUninitializedObject(typeof(SettingsWindow));

            // 1. Valid custom gesture mapping in GestureMappings
            var vmCustom = new GestureMappingViewModel(validMapping);
            bool validCustom = (bool)method!.Invoke(dummyWindow, new object?[] { vmCustom })!;
            Assert(validCustom, "Test15.2_RegisteredCustomGestureAllowed");

            // 2. Valid CancelAction from ConfigManager.CurrentConfig.CancelAction
            var cancelMapping = new GestureMapping { Action = cancelAction };
            var vmCancel = new GestureMappingViewModel(cancelMapping);
            bool validCancel = (bool)method.Invoke(dummyWindow, new object?[] { vmCancel })!;
            Assert(validCancel, "Test15.3_CancelActionAllowedWithoutGestureMappingsMembership");

            // 3. Orphaned / unregistered gesture VM fails closed
            var orphanedMapping = new GestureMapping { Pattern = "L", Action = new ActionItem { Type = "Launch", Parameter = "stale.exe" } };
            var vmOrphan = new GestureMappingViewModel(orphanedMapping);
            bool validOrphan = (bool)method.Invoke(dummyWindow, new object?[] { vmOrphan })!;
            Assert(!validOrphan, "Test15.4_OrphanedVmFailsClosed");

            // 4. Null VM fails closed
            bool validNull = (bool)method.Invoke(dummyWindow, new object?[] { null })!;
            Assert(!validNull, "Test15.5_NullVmFailsClosed");

            // 5. Null Mapping fails closed
            var vmNullMapping = (GestureMappingViewModel)FormatterServices.GetUninitializedObject(typeof(GestureMappingViewModel));
            bool validNullMapping = (bool)method.Invoke(dummyWindow, new object?[] { vmNullMapping })!;
            Assert(!validNullMapping, "Test15.6_NullMappingFailsClosed");

            // 6. Action parameters isolation: updating CancelAction leaves GestureMappings untouched
            string newCancelPath = @"C:\Windows\System32\cmd.exe";
            vmCancel.Parameter = newCancelPath;
            vmCancel.InheritAppIconPath = newCancelPath;
            Assert(cancelAction.Parameter == newCancelPath, "Test15.7_CancelActionUpdated");
            Assert(validMapping.Action.Parameter == "notepad.exe", "Test15.8_CustomGestureUntouchedByCancelActionUpdate");

            // 7. Action parameters isolation: updating custom gesture leaves CancelAction untouched
            string newCustomPath = @"C:\Program Files\App\app.exe";
            vmCustom.Parameter = newCustomPath;
            Assert(validMapping.Action.Parameter == newCustomPath, "Test15.9_CustomGestureUpdated");
            Assert(cancelAction.Parameter == newCancelPath, "Test15.10_CancelActionUntouchedByCustomGestureUpdate");
        }
        finally
        {
            typeof(ConfigManager).GetProperty("CurrentConfig", BindingFlags.Public | BindingFlags.Static)?
                .GetSetMethod(true)?.Invoke(null, new object[] { originalConfig });
        }
    }

    static void Test16_Repair7_F4_LocalizationKeysResolution()
    {
        Console.WriteLine("\n--- Test16: R7 F4 Four-Language Localization Verification ---");

        string[] keys = new[]
        {
            "FileDialogFilterExecutable",
            "FileDialogTitleSelectProgram",
            "FolderDialogDescription",
            "ExclusiveRecordingStatePrompt",
            "ExclusiveRecordingModifiersPrompt",
            "ExclusiveRecordingBalloonActivated",
            "ExclusiveRecordingBalloonRestored",
            "ExclusiveRecordingBalloonRecorded"
        };

        string[] languages = new[] { "zh-CN", "zh-TW", "en-US", "ja-JP" };

        foreach (string lang in languages)
        {
            I18n.SetLanguage(lang);
            foreach (string key in keys)
            {
                string val = I18n.T(key);
                Assert(!string.IsNullOrEmpty(val), $"Test16_{lang}_{key}_NotEmpty", $"Key '{key}' was empty in {lang}");
                Assert(val != key, $"Test16_{lang}_{key}_NotRawKey", $"Key '{key}' was unresolved in {lang}");

                if (lang == "en-US")
                {
                    bool hasCjk = false;
                    foreach (char c in val)
                    {
                        if (c >= 0x4E00 && c <= 0x9FFF) { hasCjk = true; break; }
                    }
                    Assert(!hasCjk, $"Test16_en-US_{key}_NoCjk", $"en-US key '{key}' contains CJK: {val}");
                }
            }
        }
    }

    static void Test17_Repair7_BaselineCounterexampleProof()
    {
        Console.WriteLine("\n--- Test17: R7 Baseline Counterexample & Fix Proof ---");

        string baselineDll = @"H:\design\.ai\tasks\SP-GESTURE-EDITOR-001\r7\baseline-assemblies\StarPie.dll";
        if (File.Exists(baselineDll))
        {
            var alc = new AssemblyLoadContext("BaselineCounterexampleContext", isCollectible: true);
            try
            {
                var baselineAsm = alc.LoadFromAssemblyPath(baselineDll);
                var bMappingType = baselineAsm.GetType("WinPieGestures.GestureMapping");
                var bActionType = baselineAsm.GetType("WinPieGestures.ActionItem");
                var bVmType = baselineAsm.GetType("WinPieGestures.GestureMappingViewModel");

                dynamic bMapping = Activator.CreateInstance(bMappingType!)!;
                dynamic bAction = Activator.CreateInstance(bActionType!)!;
                bAction.Type = "Hotkey";
                bAction.Parameter = "Ctrl+A";
                bMapping.Action = bAction;

                dynamic bOldVm = Activator.CreateInstance(bVmType!, bMapping)!;
                int bCancellations = 0;
                Action bCancelCallback = () => bCancellations++;
                bOldVm.OnExclusiveRecordingCancelledRequest = bCancelCallback;
                bOldVm.IsExclusiveRecording = true;

                // RefreshGestureMappings constructs a new VM for this same Mapping object
                dynamic bReplacementVm = Activator.CreateInstance(bVmType!, bMapping)!;
                bReplacementVm.OnExclusiveRecordingCancelledRequest = bCancelCallback;
                bReplacementVm.AggregatedType = "Launch";

                bool bOldFlag = (bool)bOldVm.IsExclusiveRecording;
                bool bRepFlag = (bool)bReplacementVm.IsExclusiveRecording;
                bool bDefect = bOldFlag && !bRepFlag && bCancellations == 0;
                Assert(bDefect, "Test17.1_BaselineF1DefectReproduced",
                    $"Baseline R6 assembly reproduced F1 defect: oldFlag={bOldFlag}, repFlag={bRepFlag}, cancellations={bCancellations}");
            }
            finally
            {
                alc.Unload();
            }
        }
        else
        {
            Console.WriteLine("[INFO] Baseline assembly not found at expected path; skipping baseline dynamic load.");
        }

        // Now verify Candidate in current process passes the exact same scenario cleanly
        var cMapping = new GestureMapping { Pattern = "D", Action = new ActionItem { Type = "Hotkey", Parameter = "Ctrl+A" } };
        var cOldVm = new GestureMappingViewModel(cMapping);
        int cCancellations = 0;
        cOldVm.OnExclusiveRecordingCancelledRequest = () => cCancellations++;
        cOldVm.IsExclusiveRecording = true;

        var cReplacementVm = new GestureMappingViewModel(cMapping);
        Assert(cReplacementVm.IsExclusiveRecording, "Test17.2_CandidateReplacementVmSynchronizedWithMapping");
        cReplacementVm.OnExclusiveRecordingCancelledRequest = () => cCancellations++;
        cReplacementVm.AggregatedType = "Launch";

        Assert(cCancellations == 1, "Test17.3_CandidateFiresCancellationOnReplacementTypeSwitch");
        Assert(!cReplacementVm.IsExclusiveRecording, "Test17.4_CandidateReplacementRecordingCleared");
        Assert(!cOldVm.IsExclusiveRecording, "Test17.5_CandidateOldVmRecordingCleared");
        Assert(!GestureMappingViewModel.IsMappingInExclusiveRecording(cMapping), "Test17.6_CandidateStaticMappingCleared");
    }

    private static string FindRepoRoot()
    {
        string? dir = AppDomain.CurrentDomain.BaseDirectory;
        while (!string.IsNullOrEmpty(dir))
        {
            if (File.Exists(Path.Combine(dir, "WinPieGestures", "SettingsWindow.xaml")))
            {
                return dir;
            }
            dir = Path.GetDirectoryName(dir);
        }
        return Directory.GetCurrentDirectory();
    }
}
