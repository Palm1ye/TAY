using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace TAY.Services
{
    public class ContextMenuItem
    {
        public string Name { get; set; } = "";
        public string RegistryPath { get; set; } = ""; // E.g. HKCR\*\...
        public string FullKeyPath { get; set; } = ""; // Full HKLM/HKCU path
        public string DefaultValue { get; set; } = "";
        public bool IsEnabled { get; set; } = true;
        public string SourceType { get; set; } = "*"; // '*' or 'Directory'
    }

    public class ContextMenuService
    {
        private static readonly string AsteriskPath = @"*\shellex\ContextMenuHandlers";
        private static readonly string DirectoryPath = @"Directory\shellex\ContextMenuHandlers";

        public static async Task<List<ContextMenuItem>> GetContextMenuHandlersAsync()
        {
            return await Task.Run(() =>
            {
                var list = new List<ContextMenuItem>();
                RealTimeLogService.Instance.Log("Scanning Windows Context Menu shell extensions...");

                try
                {
                    ScanRegistryPath(Registry.ClassesRoot, AsteriskPath, "*", list);
                    ScanRegistryPath(Registry.ClassesRoot, DirectoryPath, "Directory", list);
                    RealTimeLogService.Instance.Log($"Scan completed. Detected {list.Count} shell extensions.");
                }
                catch (Exception ex)
                {
                    RealTimeLogService.Instance.Log($"[ERROR] Failed to scan context menu handlers: {ex.Message}");
                }

                return list;
            });
        }

        private static void ScanRegistryPath(RegistryKey rootKey, string relativePath, string sourceType, List<ContextMenuItem> list)
        {
            using var key = rootKey.OpenSubKey(relativePath);
            if (key == null) return;

            foreach (var subKeyName in key.GetSubKeyNames())
            {
                try
                {
                    using var subKey = key.OpenSubKey(subKeyName);
                    if (subKey == null) continue;

                    string defaultVal = subKey.GetValue("")?.ToString() ?? "";
                    
                    bool isEnabled = true;
                    string displayName = subKeyName;

                    // Detect disabled state
                    // Case A: Default value starts with -
                    if (defaultVal.StartsWith("-"))
                    {
                        isEnabled = false;
                    }
                    // Case B: Key name starts with -
                    else if (subKeyName.StartsWith("-"))
                    {
                        isEnabled = false;
                        displayName = subKeyName.Substring(1); // strip the '-' for display
                    }

                    // Standard built-in Windows ones to ignore or group
                    var lowerName = displayName.ToLowerInvariant();
                    if (lowerName.Equals("new") || lowerName.Equals("sharing") || lowerName.Equals("workfolders") || lowerName.Equals("openwith"))
                    {
                        continue; // Skip standard system essentials to keep list clean
                    }

                    list.Add(new ContextMenuItem
                    {
                        Name = displayName,
                        RegistryPath = $@"HKCR\{relativePath}\{subKeyName}",
                        FullKeyPath = relativePath,
                        DefaultValue = defaultVal,
                        IsEnabled = isEnabled,
                        SourceType = sourceType
                    });
                }
                catch { }
            }
        }

        public static async Task<bool> ToggleHandlerAsync(ContextMenuItem item, bool enable)
        {
            return await Task.Run(() =>
            {
                string actionStr = enable ? "Enabling" : "Disabling";
                RealTimeLogService.Instance.Log($"{actionStr} shell extension: {item.Name}...");

                try
                {
                    using var parentKey = Registry.ClassesRoot.OpenSubKey(item.FullKeyPath, true);
                    if (parentKey == null)
                    {
                        RealTimeLogService.Instance.Log($"[ERROR] Registry error: Could not open {item.FullKeyPath} with write permissions.");
                        return false;
                    }

                    string keyName = item.IsEnabled ? item.Name : "-" + item.Name;
                    
                    if (enable)
                    {
                        EnableKey(parentKey, keyName);
                        RealTimeLogService.Instance.Log($"[SUCCESS] Enabled shell extension: {item.Name}");
                    }
                    else
                    {
                        DisableKey(parentKey, keyName);
                        RealTimeLogService.Instance.Log($"[SUCCESS] Disabled shell extension: {item.Name}");
                    }

                    return true;
                }
                catch (UnauthorizedAccessException)
                {
                    RealTimeLogService.Instance.Log("[ERROR] Access Denied: Administrator privileges are required to modify HKCR shell extensions.");
                    return false;
                }
                catch (Exception ex)
                {
                    RealTimeLogService.Instance.Log($"[ERROR] Toggle failed: {ex.Message}");
                    return false;
                }
            });
        }

        private static void DisableKey(RegistryKey parentKey, string subKeyName)
        {
            using var subKey = parentKey.OpenSubKey(subKeyName, true);
            if (subKey == null) return;

            var defaultVal = subKey.GetValue("")?.ToString() ?? "";
            if (!string.IsNullOrEmpty(defaultVal) && defaultVal.StartsWith("{"))
            {
                // Case A: Prepend '-' to default value GUID
                subKey.SetValue("", "-" + defaultVal);
            }
            else
            {
                // Case B: Rename key by prefixing '-'
                if (!subKeyName.StartsWith("-"))
                {
                    RenameSubKey(parentKey, subKeyName, "-" + subKeyName);
                }
            }
        }

        private static void EnableKey(RegistryKey parentKey, string subKeyName)
        {
            if (subKeyName.StartsWith("-"))
            {
                // Case B: Rename back
                string originalName = subKeyName.Substring(1);
                RenameSubKey(parentKey, subKeyName, originalName);
            }
            else
            {
                // Case A: Remove '-' prefix from default value GUID
                using var subKey = parentKey.OpenSubKey(subKeyName, true);
                if (subKey == null) return;
                var defaultVal = subKey.GetValue("")?.ToString() ?? "";
                if (defaultVal.StartsWith("-{"))
                {
                    subKey.SetValue("", defaultVal.Substring(1));
                }
            }
        }

        private static void RenameSubKey(RegistryKey parentKey, string oldName, string newName)
        {
            using var oldKey = parentKey.OpenSubKey(oldName);
            if (oldKey == null) return;

            using var newKey = parentKey.CreateSubKey(newName, true);
            CopyRegistryKey(oldKey, newKey);
            oldKey.Close();
            parentKey.DeleteSubKeyTree(oldName);
        }

        private static void CopyRegistryKey(RegistryKey src, RegistryKey dst)
        {
            foreach (var valueName in src.GetValueNames())
            {
                dst.SetValue(valueName, src.GetValue(valueName)!, src.GetValueKind(valueName));
            }
            foreach (var subKeyName in src.GetSubKeyNames())
            {
                using var srcSubKey = src.OpenSubKey(subKeyName);
                using var dstSubKey = dst.CreateSubKey(subKeyName, true);
                CopyRegistryKey(srcSubKey!, dstSubKey);
            }
        }
    }
}
