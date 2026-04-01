using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Microsoft.Win32;

namespace proxifyre_ui
{
    public static class EnvironmentDetector
    {
        public static List<DependencyInfo> CheckMissingDependencies()
        {
            var missing = new List<DependencyInfo>();

            // 1. Check ProxiFyre.exe
            if (!File.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, Constants.ProgramName)))
            {
                missing.Add(new DependencyInfo
                {
                    Name = "ProxiFyre 核心程序",
                    Url = Constants.ProxifyreDownloadUrl,
                    Type = DependencyType.Zip
                });
            }

            // 2. Check NDISAPI (Windows Packet Filter)
            if (!IsNdisapiInstalled())
            {
                missing.Add(new DependencyInfo
                {
                    Name = "Windows Packet Filter (NDISAPI)",
                    Url = Constants.NdisapiDownloadUrl,
                    Type = DependencyType.Msi
                });
            }

            // 3. Check VC++ Redistributable
            if (!IsVcRedistInstalled())
            {
                missing.Add(new DependencyInfo
                {
                    Name = "Visual C++ Redistributable 2015-2022 (x64)",
                    Url = Constants.VcRedistDownloadUrl,
                    Type = DependencyType.Exe
                });
            }

            return missing;
        }

        private static bool IsNdisapiInstalled()
        {
            // Windows Packet Filter (NDISAPI) installs a system driver.
            // The most accurate way to check without relying solely on system32 dlls (which might be 32/64 bit routed or moved)
            // is to check the Windows Registry for the Ndisapi service or the WinpkFilter software key.
            try
            {
                // Check 1: NDISAPI Service Registry Key (most reliable for the actual driver)
                using (var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\ndisrd"))
                {
                    if (key != null)
                    {
                        return true;
                    }
                }

                // Check 2: 64-bit software registry for WinpkFilter
                using (var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
                using (var key = baseKey.OpenSubKey(@"SOFTWARE\NT Kernel Resources\WinpkFilter"))
                {
                    if (key != null)
                    {
                        return true;
                    }
                }
                
                // Fallback: Check if ndisapi.dll exists in System32
                string sys32 = Environment.GetFolderPath(Environment.SpecialFolder.System);
                if (File.Exists(Path.Combine(sys32, "ndisapi.dll")))
                {
                    return true;
                }
            }
            catch { }

            return false;
        }

        private static bool IsVcRedistInstalled()
        {
            // Check VC++ Redist registry keys
            try
            {
                using (RegistryKey key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\x64"))
                {
                    if (key != null)
                    {
                        var installed = key.GetValue("Installed");
                        if (installed != null && (int)installed == 1)
                        {
                            return true;
                        }
                    }
                }
            }
            catch { }
            return false;
        }
    }

    public class DependencyInfo
    {
        public string Name { get; set; }
        public string Url { get; set; }
        public DependencyType Type { get; set; }
    }

    public enum DependencyType
    {
        Zip,
        Msi,
        Exe
    }
}
