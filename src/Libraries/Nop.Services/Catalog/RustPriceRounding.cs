using System.Globalization;
using System.Runtime.InteropServices;
using Nop.Core.Domain.Directory;

namespace Nop.Services.Catalog;

/// <summary>
/// In-process call into libprice_rounding. Load failure, a non-zero status, or a thrown exception
/// is a failed call. The caller then uses the C# rounding body.
/// </summary>
internal static class RustPriceRounding
{
    private const int Ready = 1;
    private const int Failed = 2;
    private const int ResultCapacity = 128;

    private static readonly object Gate = new();
    private static int _state;
    private static RoundDelegate _round;

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int RoundDelegate(IntPtr amount, int roundingType, IntPtr result, int resultCapacity);

    /// <summary>
    /// Round <paramref name="value"/> with the cdylib.
    /// </summary>
    /// <returns>True when the library returned a decimal string. False when it is missing or failed.</returns>
    internal static bool TryRound(decimal value, RoundingType roundingType, out decimal rounded)
    {
        rounded = 0m;
        IntPtr amount = IntPtr.Zero;
        IntPtr result = IntPtr.Zero;

        try
        {
            var round = Resolve();
            if (round == null)
                return false;

            amount = Marshal.StringToCoTaskMemUTF8(value.ToString(CultureInfo.InvariantCulture));
            result = Marshal.AllocHGlobal(ResultCapacity);
            var zeros = new byte[ResultCapacity];
            Marshal.Copy(zeros, 0, result, ResultCapacity);

            if (round(amount, (int)roundingType, result, ResultCapacity) != 0)
                return false;

            var text = Marshal.PtrToStringUTF8(result);
            if (string.IsNullOrEmpty(text))
                return false;

            rounded = decimal.Parse(text, NumberStyles.Number, CultureInfo.InvariantCulture);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
        finally
        {
            if (amount != IntPtr.Zero)
                Marshal.FreeCoTaskMem(amount);
            if (result != IntPtr.Zero)
                Marshal.FreeHGlobal(result);
        }
    }

    private static RoundDelegate Resolve()
    {
        if (Volatile.Read(ref _state) == Ready)
            return _round;
        if (Volatile.Read(ref _state) == Failed)
            return null;

        lock (Gate)
        {
            if (_state == Ready)
                return _round;
            if (_state == Failed)
                return null;

            var library = IntPtr.Zero;
            try
            {
                var path = LibraryPath();
                if (path == null || !NativeLibrary.TryLoad(path, out library))
                {
                    Volatile.Write(ref _state, Failed);
                    return null;
                }

                if (!NativeLibrary.TryGetExport(library, "price_rounding_round", out var export))
                {
                    NativeLibrary.Free(library);
                    library = IntPtr.Zero;
                    Volatile.Write(ref _state, Failed);
                    return null;
                }

                _round = Marshal.GetDelegateForFunctionPointer<RoundDelegate>(export);
                // Keep the mapping. Freeing it would invalidate _round. Clearing the local stops the catch from unloading it.
                library = IntPtr.Zero;
                Volatile.Write(ref _state, Ready);
                return _round;
            }
            catch (Exception)
            {
                if (library != IntPtr.Zero)
                    NativeLibrary.Free(library);

                Volatile.Write(ref _state, Failed);
                return null;
            }
        }
    }

    /// <summary>
    /// The Alpine image copies libprice_rounding.so next to the app. Anywhere else, including a glibc build, stays unloaded.
    /// </summary>
    private static string LibraryPath()
    {
        var fileName = OperatingSystem.IsWindows()
            ? "price_rounding.dll"
            : OperatingSystem.IsMacOS()
                ? "libprice_rounding.dylib"
                : "libprice_rounding.so";

        var nextToApp = Path.Combine(AppContext.BaseDirectory, fileName);
        return File.Exists(nextToApp) ? nextToApp : null;
    }
}
