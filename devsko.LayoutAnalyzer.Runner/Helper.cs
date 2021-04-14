using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Win32.SafeHandles;
using Microsoft.Windows.Sdk;

namespace devsko.LayoutAnalyzer.Runner
{
    public static class Helper
    {
        public unsafe static string GetCaseSensitivePath(string path)
        {
            using SafeFileHandle hFile = PInvoke.CreateFile(
                lpFileName: path,
                dwDesiredAccess: (FILE_ACCESS_FLAGS)0,
                dwShareMode: FILE_SHARE_FLAGS.FILE_SHARE_READ | FILE_SHARE_FLAGS.FILE_SHARE_WRITE | FILE_SHARE_FLAGS.FILE_SHARE_DELETE,
                lpSecurityAttributes: null,
                dwCreationDisposition: FILE_CREATE_FLAGS.OPEN_EXISTING,
                dwFlagsAndAttributes: FILE_FLAGS_AND_ATTRIBUTES.FILE_FLAG_BACKUP_SEMANTICS,
                hTemplateFile: null);

            if (hFile.IsInvalid)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            Span<char> name = stackalloc char[1024];
            uint size = PInvoke.GetFinalPathNameByHandle(
                hFile: hFile,
                lpszFilePath: (char*)Unsafe.AsPointer(ref name[0]),
                cchFilePath: (uint)name.Length,
                dwFlags: FILE_NAME.FILE_NAME_NORMALIZED);

            if (size == 0 || size > name.Length)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            if (name.StartsWith(@"\\?\"))
            {
                name = name.Slice(4);
            }

            return MemoryMarshal.CreateReadOnlySpanFromNullTerminated((char*)Unsafe.AsPointer(ref name[0])).ToString();
        }
    }
}
