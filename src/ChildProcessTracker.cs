using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace proxifyre_ui
{
    /// <summary>
    /// 封装 Windows Job Object (作业对象) API。
    /// 用于将子进程与当前父进程的生命周期绑定，确保在父进程异常崩溃或被强杀时，子进程（及其派生进程）能被内核无条件清理。
    /// </summary>
    public sealed class ChildProcessTracker : IDisposable
    {
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr CreateJobObject(IntPtr lpJobAttributes, string lpName);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetInformationJobObject(IntPtr hJob, JobObjectInfoType infoType, IntPtr lpJobObjectInfo, uint cbJobObjectInfoLength);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool AssignProcessToJobObject(IntPtr job, IntPtr process);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CloseHandle(IntPtr hObject);

        private IntPtr _handle;
        private bool _disposed;

        public ChildProcessTracker()
        {
            _handle = CreateJobObject(IntPtr.Zero, null);
            if (_handle == IntPtr.Zero)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "无法创建 Job Object");
            }

            var info = new JOBOBJECT_BASIC_LIMIT_INFORMATION
            {
                // 核心标志：当拥有此 Job Object 的所有句柄被关闭时（通常是父进程退出时），杀掉该 Job 下的所有进程
                LimitFlags = 0x2000 // JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE
            };

            var extendedInfo = new JOBOBJECT_EXTENDED_LIMIT_INFORMATION
            {
                BasicLimitInformation = info
            };

            int length = Marshal.SizeOf(typeof(JOBOBJECT_EXTENDED_LIMIT_INFORMATION));
            IntPtr extendedInfoPtr = Marshal.AllocHGlobal(length);
            try
            {
                Marshal.StructureToPtr(extendedInfo, extendedInfoPtr, false);
                if (!SetInformationJobObject(_handle, JobObjectInfoType.ExtendedLimitInformation, extendedInfoPtr, (uint)length))
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "无法设置 Job Object 的属性");
                }
            }
            finally
            {
                Marshal.FreeHGlobal(extendedInfoPtr);
            }
        }

        /// <summary>
        /// 将指定的进程加入到当前 Job Object 中进行生命周期管理。
        /// </summary>
        /// <param name="process">需要被管理的子进程</param>
        public void AddProcess(Process process)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(ChildProcessTracker));
            if (process == null) throw new ArgumentNullException(nameof(process));

            bool success = AssignProcessToJobObject(_handle, process.Handle);
            if (!success && !process.HasExited)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "无法将进程分配给 Job Object");
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                if (_handle != IntPtr.Zero)
                {
                    CloseHandle(_handle);
                    _handle = IntPtr.Zero;
                }
                _disposed = true;
            }
            GC.SuppressFinalize(this);
        }

        ~ChildProcessTracker()
        {
            Dispose();
        }

        #region Native Structures 结构定义

        private enum JobObjectInfoType
        {
            ExtendedLimitInformation = 9
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct IO_COUNTERS
        {
            public ulong ReadOperationCount;
            public ulong WriteOperationCount;
            public ulong OtherOperationCount;
            public ulong ReadTransferCount;
            public ulong WriteTransferCount;
            public ulong OtherTransferCount;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct JOBOBJECT_BASIC_LIMIT_INFORMATION
        {
            public long PerProcessUserTimeLimit;
            public long PerJobUserTimeLimit;
            public uint LimitFlags;
            public UIntPtr MinimumWorkingSetSize;
            public UIntPtr MaximumWorkingSetSize;
            public uint ActiveProcessLimit;
            public UIntPtr Affinity;
            public uint PriorityClass;
            public uint SchedulingClass;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct JOBOBJECT_EXTENDED_LIMIT_INFORMATION
        {
            public JOBOBJECT_BASIC_LIMIT_INFORMATION BasicLimitInformation;
            public IO_COUNTERS IoInfo;
            public UIntPtr ProcessMemoryLimit;
            public UIntPtr JobMemoryLimit;
            public UIntPtr PeakProcessMemoryUsed;
            public UIntPtr PeakJobMemoryUsed;
        }

        #endregion
    }
}