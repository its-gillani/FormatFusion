using System;
using System.IO;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using Microsoft.Toolkit.Uwp.Notifications;

namespace FormatFusion.UI.Services
{
    public static class NotificationService
    {
        private const string AppUserModelId = "FormatFusion.DesktopApp";

        public static void Initialize()
        {
            try
            {
                EnsureStartMenuShortcut();
                ToastNotificationManagerCompat.OnActivated += toastArgs =>
                {
                    // Handle activation if needed
                };
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Failed to initialize notifications: " + ex.Message);
            }
        }

        public static void ShowToast(string title, string content)
        {
            try
            {
                new ToastContentBuilder()
                    .AddText(title)
                    .AddText(content)
                    .Show();
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Failed to show toast: " + ex.Message);
            }
        }

        private static void EnsureStartMenuShortcut()
        {
            try
            {
                string exePath = Process.GetCurrentProcess().MainModule?.FileName;
                if (string.IsNullOrEmpty(exePath)) return;

                string shortcutDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "FormatFusion");
                if (!Directory.Exists(shortcutDir))
                    Directory.CreateDirectory(shortcutDir);

                string shortcutPath = Path.Combine(shortcutDir, "FormatFusion.lnk");

                if (File.Exists(shortcutPath))
                    return; // Shortcut already exists

                InstallShortcut(shortcutPath, exePath);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Failed to ensure Start Menu shortcut: " + ex.Message);
            }
        }

        private static void InstallShortcut(string shortcutPath, string exePath)
        {
            IShellLinkW newShortcut = (IShellLinkW)new CShellLink();
            newShortcut.SetPath(exePath);
            newShortcut.SetWorkingDirectory(Path.GetDirectoryName(exePath));
            newShortcut.SetIconLocation(exePath, 0);

            IPropertyStore newShortcutProperties = (IPropertyStore)newShortcut;

            using (PropVariant appId = new PropVariant(AppUserModelId))
            {
                Guid propertyKeyGuid = new Guid("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3");
                PropertyKey propertyKey = new PropertyKey(propertyKeyGuid, 5); // System.AppUserModel.ID
                newShortcutProperties.SetValue(ref propertyKey, appId);
                newShortcutProperties.Commit();
            }

            IPersistFile newShortcutSave = (IPersistFile)newShortcut;
            newShortcutSave.Save(shortcutPath, true);
        }

        #region COM Interfaces for ShellLink

        [ComImport, Guid("000214F9-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        internal interface IShellLinkW
        {
            void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszFile, int cchMaxPath, out IntPtr pfd, uint fFlags);
            void GetIDList(out IntPtr ppidl);
            void SetIDList(IntPtr pidl);
            void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszName, int cchMaxName);
            void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);
            void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszDir, int cchMaxPath);
            void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);
            void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszArgs, int cchMaxPath);
            void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);
            void GetHotkey(out short pwHotkey);
            void SetHotkey(short wHotkey);
            void GetShowCmd(out int piShowCmd);
            void SetShowCmd(int iShowCmd);
            void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszIconPath, int cchIconPath, out int piIcon);
            void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);
            void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pszPathRel, uint dwReserved);
            void Resolve(IntPtr hwnd, uint fFlags);
            void SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
        }

        [ComImport, Guid("00021401-0000-0000-C000-000000000046"), ClassInterface(ClassInterfaceType.None)]
        internal class CShellLink { }

        [ComImport, Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        internal interface IPropertyStore
        {
            void GetCount(out uint cProps);
            void GetAt(uint iProp, out PropertyKey pkey);
            void GetValue(ref PropertyKey key, out PropVariant pv);
            void SetValue(ref PropertyKey key, PropVariant propvar);
            void Commit();
        }

        [StructLayout(LayoutKind.Sequential, Pack = 4)]
        internal struct PropertyKey
        {
            public Guid fmtid;
            public uint pid;

            public PropertyKey(Guid guid, uint id)
            {
                fmtid = guid;
                pid = id;
            }
        }

        [StructLayout(LayoutKind.Explicit)]
        internal struct PropVariant : IDisposable
        {
            [FieldOffset(0)] ushort vt;
            [FieldOffset(8)] IntPtr pointerValue;
            [FieldOffset(8)] byte byteValue;
            [FieldOffset(8)] long longValue;
            [FieldOffset(8)] short boolValue;

            public PropVariant(string value)
            {
                vt = 31; // VT_LPWSTR
                pointerValue = Marshal.StringToCoTaskMemUni(value);
                byteValue = 0;
                longValue = 0;
                boolValue = 0;
            }

            public void Dispose()
            {
                if (vt == 31 && pointerValue != IntPtr.Zero)
                {
                    Marshal.FreeCoTaskMem(pointerValue);
                    pointerValue = IntPtr.Zero;
                    vt = 0;
                }
            }
        }
        #endregion
    }
}
