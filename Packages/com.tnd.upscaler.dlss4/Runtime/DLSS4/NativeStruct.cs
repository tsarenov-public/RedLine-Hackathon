using System;
using System.Runtime.InteropServices;
using Unity.Collections.LowLevel.Unsafe;

namespace TND.Upscaling.DLSS4
{
    public class NativeStruct<TData>: IDisposable
        where TData: struct
    {
        private IntPtr _nativePtr = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(TData)));

        public void SetData(ref TData data)
        {
            unsafe { UnsafeUtility.CopyStructureToPtr(ref data, _nativePtr.ToPointer()); }
        }

        public IntPtr GetPointer() => _nativePtr;

        public void Dispose()
        {
            if (_nativePtr == IntPtr.Zero)
                return;
            
            Marshal.FreeHGlobal(_nativePtr);
            _nativePtr = IntPtr.Zero;
        }
    }
}
