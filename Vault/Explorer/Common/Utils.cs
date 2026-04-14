// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace Microsoft.Vault.Explorer.Common
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Security.Cryptography;
    using System.Security.Cryptography.X509Certificates;
    using System.Text;
    using System.Text.RegularExpressions;
    using System.Windows.Forms;
    using Microsoft.Vault.Core;
    using Microsoft.Vault.Explorer.Model.ContentTypes;
    using Microsoft.Vault.Library;
    using Microsoft.Win32;
    using Newtonsoft.Json;

    public static class Utils
    {
        /// <summary>
        ///     Space with black down triangle char
        /// </summary>
        public const string DropDownSuffix = " \u25BC";

        public static string NullableDateTimeToString(DateTime? dt) => dt == null ? "(none)" : dt.Value.ToLocalTime().ToString();

        public static string NullableIntToString(int? x) => x == null ? "(none)" : x.ToString();

        public static string ExpirationToString(DateTime? dt)
        {
            if (dt == null) return "";
            return ExpirationToString(dt.Value - DateTime.UtcNow);
        }

        public static string ExpirationToString(TimeSpan ts)
        {
            if (ts == TimeSpan.MaxValue) return "Never";
            if (ts.TotalDays < 0) return "Expired";
            if (ts.TotalDays >= 2) return $"{ts.TotalDays:N0} days";
            if (ts.TotalDays >= 1) return $"{ts.TotalDays:N0} day and {ts.Hours} hours";
            return $"{ts.Hours} hours";
        }

        public static string ByteArrayToHex(byte[] arr)
        {
            Guard.ArgumentNotNull(arr, nameof(arr));
            return BitConverter.ToString(arr).Replace("-", "");
        }

        public static string FullPathToJsonFile(string filename)
        {
            filename = Environment.ExpandEnvironmentVariables(filename);
            if (Path.IsPathRooted(filename)) return filename;
            filename = Path.Combine(AppSettings.Default.JsonConfigurationFilesRoot, filename);
            if (Path.IsPathRooted(filename)) return filename;
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, filename);
        }

        public static T LoadFromJsonFile<T>(string filename, bool isOptional = false) where T : new()
        {
            string path = FullPathToJsonFile(filename);
            if (File.Exists(path))
            {
                var x = JsonConvert.DeserializeObject<T>(File.ReadAllText(path));
                return x;
            }

            if (isOptional) return new T();
            throw new FileNotFoundException("Mandatory .json configuration file is not found", path);
        }

        /// <summary>
        /// Returns a WinForms Cursor loaded from a raw .cur byte buffer.
        /// Still used by MainForm during Phase 1-3. Deleted in Phase 5 with MainForm.
        /// </summary>
        public static Cursor LoadCursorFromResource(byte[] buffer)
        {
            using var ms = new MemoryStream(buffer);
            return new Cursor(ms);
        }

        public static string ConvertToValidSecretName(string name)
        {
            var result = new Regex("[^0-9a-zA-Z-]", RegexOptions.Singleline).Replace(name, "-");
            return string.IsNullOrEmpty(result) ? "unknown" : result;
        }

        public static string ConvertToValidTagValue(string value)
        {
            if (string.IsNullOrEmpty(value)) return value;
            if (value.Length <= Consts.MaxTagNameLength) return value;
            return value.Substring(0, Consts.MaxTagNameLength - 3) + "...";
        }

        public static string GetRtfUnicodeEscapedString(string s)
        {
            var sb = new StringBuilder();
            foreach (var c in s)
            {
                if (c == '\\' || c == '{' || c == '}') sb.Append(@"\" + c);
                else if (c <= 0x7f) sb.Append(c);
                else sb.Append("\\u" + Convert.ToUInt32(c) + "?");
            }

            return sb.ToString();
        }

        /// <summary>
        /// Returns a version string for the given PE file.
        /// Used by the WinForms SettingsDialog — removed in Phase 3 when SettingsView.axaml replaces it.
        /// </summary>
        public static string GetFileVersionString(string title, string peFilename, string optionalPrefix = "")
        {
            var filepath = Path.Combine(Path.GetDirectoryName(Application.ExecutablePath), peFilename);
            string version = "Unknown";
            try
            {
                var verInfo = FileVersionInfo.GetVersionInfo(filepath);
                version = $"{verInfo.FileMajorPart}.{verInfo.FileMinorPart}.{verInfo.FileBuildPart}.{verInfo.FilePrivatePart}";
            }
            catch { }

            return $"{title}{version}{optionalPrefix}";
        }

        public static string NewSecurePassword()
        {
            var UpperCharsSet = Enumerable.Range(65, 26).Select(i => (byte)i).ToArray();
            var LowerCharsSet = Enumerable.Range(97, 26).Select(i => (byte)i).ToArray();
            var NumbersSet = Enumerable.Range(48, 10).Select(i => (byte)i).ToArray();
            var SpecialCharsSet = new byte[] { 33, 35, 40, 41, 64 };
            var All = UpperCharsSet.Concat(LowerCharsSet).Concat(NumbersSet).Concat(SpecialCharsSet).ToArray();

            using var r = new CryptoRandomGenerator();
            int length = r.Next(32, 41);
            var u = Enumerable.Range(0, 5).Select(i => UpperCharsSet[r.Next(0, UpperCharsSet.Length)]);
            var l = Enumerable.Range(0, 1).Select(i => LowerCharsSet[r.Next(0, LowerCharsSet.Length)]);
            var n = Enumerable.Range(0, 1).Select(i => NumbersSet[r.Next(0, NumbersSet.Length)]);
            var s = Enumerable.Range(0, 4).Select(i => SpecialCharsSet[r.Next(0, SpecialCharsSet.Length)]);
            var a = Enumerable.Range(0, length - 11).Select(i => All[r.Next(0, All.Length)]);
            return Encoding.ASCII.GetString(Microsoft.Vault.Library.Utils.Shuffle(u.Concat(l).Concat(n).Concat(s).Concat(a)).ToArray());
        }

        public static string NewApiKey(int length = 64)
        {
            byte[] buff = new byte[length];
            RandomNumberGenerator.Fill(buff);
            return Convert.ToBase64String(buff);
        }

        /// <summary>
        /// Sets ClickOnce Add/Remove Programs icon. Windows + ClickOnce only.
        /// Kept for Phase 1-3 compatibility; removed when ClickOnce is retired (Phase 5).
        /// </summary>
        public static void ClickOnce_SetAddRemoveProgramsIcon()
        {
            if (ApplicationDeployment.IsNetworkDeployed && ApplicationDeployment.CurrentDeployment.IsFirstRun)
            {
                try
                {
                    using var myUninstallKey = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall");
                    foreach (var subKeyName in myUninstallKey.GetSubKeyNames())
                    {
                        using var myKey = myUninstallKey.OpenSubKey(subKeyName, true);
                        object myValue = myKey.GetValue("DisplayName");
                        if (myValue != null && myValue.ToString() == Globals.ProductName)
                        {
                            myKey.SetValue("DisplayIcon", Application.ExecutablePath);
                            break;
                        }
                    }
                }
                catch { }
            }
        }
    }
}
