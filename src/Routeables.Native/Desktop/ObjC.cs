#if !ANDROID && !IOS && !MACCATALYST && !WINDOWS

using System.Runtime.InteropServices;

namespace Routeables.Native.Desktop;

/// <summary>
/// The Objective-C runtime of macOS, called straight (<c>libobjc</c>), for a build that has no macOS target: classes and selectors by name, messages, the
/// <c>NSString</c>s, the blocks the system calls back (a global block with its state after it), and a class made at run time (the delegate of the notification center).
/// </summary>
#pragma warning disable SYSLIB1054 // A few signatures, called when a notification is made: no source-generated marshalling needed.
#pragma warning disable CA5392 // The libraries are those of the system.
internal static unsafe class ObjC
{
    private const string LibObjC = "/usr/lib/libobjc.A.dylib";
    private const string LibSystem = "/usr/lib/libSystem.B.dylib";
    private static readonly IntPtr RtldDefault = -2;

    [DllImport(LibSystem)]
    private static extern IntPtr dlopen([MarshalAs(UnmanagedType.LPUTF8Str)] string path, int mode);

    [DllImport(LibSystem)]
    private static extern IntPtr dlsym(IntPtr handle, [MarshalAs(UnmanagedType.LPUTF8Str)] string name);

    [DllImport(LibObjC)]
    private static extern IntPtr objc_getClass([MarshalAs(UnmanagedType.LPUTF8Str)] string name);

    [DllImport(LibObjC)]
    private static extern IntPtr sel_registerName([MarshalAs(UnmanagedType.LPUTF8Str)] string name);

    [DllImport(LibObjC)]
    private static extern IntPtr objc_allocateClassPair(IntPtr superclass, [MarshalAs(UnmanagedType.LPUTF8Str)] string name, nuint extraBytes);

    [DllImport(LibObjC)]
    private static extern void objc_registerClassPair(IntPtr cls);

    [DllImport(LibObjC)]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool class_addMethod(IntPtr cls, IntPtr sel, IntPtr imp, [MarshalAs(UnmanagedType.LPUTF8Str)] string types);

    [DllImport(LibObjC, EntryPoint = "objc_msgSend")]
    public static extern IntPtr Send(IntPtr receiver, IntPtr selector);

    [DllImport(LibObjC, EntryPoint = "objc_msgSend")]
    public static extern IntPtr Send(IntPtr receiver, IntPtr selector, IntPtr a);

    [DllImport(LibObjC, EntryPoint = "objc_msgSend")]
    public static extern IntPtr Send(IntPtr receiver, IntPtr selector, IntPtr a, IntPtr b);

    [DllImport(LibObjC, EntryPoint = "objc_msgSend")]
    public static extern IntPtr Send(IntPtr receiver, IntPtr selector, IntPtr a, IntPtr b, IntPtr c);

    // categoryWithIdentifier:actions:intentIdentifiers:options: (the last one is an NSUInteger)
    [DllImport(LibObjC, EntryPoint = "objc_msgSend")]
    public static extern IntPtr Send(IntPtr receiver, IntPtr selector, IntPtr a, IntPtr b, IntPtr c, nuint options);

    // requestAuthorizationWithOptions:completionHandler: (an NSUInteger, a block)
    [DllImport(LibObjC, EntryPoint = "objc_msgSend")]
    public static extern IntPtr Send(IntPtr receiver, IntPtr selector, nuint options, IntPtr block);

    private static bool _loaded;

    /// <summary>Loads the frameworks the classes are in (nothing loads them in a console or an Avalonia application before the first use).</summary>
    public static void Load()
    {
        if (_loaded)
            return;

        dlopen("/System/Library/Frameworks/Foundation.framework/Foundation", 1);
        dlopen("/System/Library/Frameworks/UserNotifications.framework/UserNotifications", 1);
        _loaded = true;
    }

    public static IntPtr Class(string name) => objc_getClass(name);

    public static IntPtr Sel(string name) => sel_registerName(name);

    /// <summary>An autoreleased <c>NSString</c>.</summary>
    public static IntPtr NSString(string? text)
    {
        var utf8 = Marshal.StringToCoTaskMemUTF8(text ?? string.Empty);
        try { return Send(Class("NSString"), Sel("stringWithUTF8String:"), utf8); }
        finally { Marshal.FreeCoTaskMem(utf8); }
    }

    public static string? String(IntPtr nsString)
        => nsString == IntPtr.Zero ? null : Marshal.PtrToStringUTF8(Send(nsString, Sel("UTF8String")));

    public static IntPtr New(string className) => Send(Send(Class(className), Sel("alloc")), Sel("init"));

    /// <summary>A subclass of <c>NSObject</c> with these methods (selector, implementation, type encoding), registered once.</summary>
    public static IntPtr NewClass(string name, params (string selector, IntPtr implementation, string types)[] methods)
    {
        var cls = objc_allocateClassPair(Class("NSObject"), name, 0);
        if (cls == IntPtr.Zero)
            return Class(name);

        foreach (var (selector, implementation, types) in methods)
            class_addMethod(cls, Sel(selector), implementation, types);

        objc_registerClassPair(cls);
        return cls;
    }

    // A block is {isa, flags, reserved, invoke, descriptor}; the state is put after it (it is a global block: nothing is copied or released, it stays).
    private const int BlockIsGlobal = 1 << 28;
    private const int StateOffset = 32;

    /// <summary>A block that calls <paramref name="invoke"/> (an unmanaged function whose first argument is the block) and carries <paramref name="state"/>.</summary>
    public static IntPtr NewBlock(IntPtr invoke, IntPtr state)
    {
        var descriptor = (ulong*)NativeMemory.Alloc(16);
        descriptor[0] = 0;
        descriptor[1] = StateOffset + (ulong)IntPtr.Size;

        var block = (byte*)NativeMemory.Alloc(StateOffset + (nuint)IntPtr.Size);
        *(IntPtr*)block = dlsym(RtldDefault, "_NSConcreteGlobalBlock");
        *(int*)(block + 8) = BlockIsGlobal;
        *(int*)(block + 12) = 0;
        *(IntPtr*)(block + 16) = invoke;
        *(IntPtr*)(block + 24) = (IntPtr)descriptor;
        *(IntPtr*)(block + StateOffset) = state;
        return (IntPtr)block;
    }

    public static IntPtr BlockState(IntPtr block) => *(IntPtr*)((byte*)block + StateOffset);

    /// <summary>Calls a block the system gave, <c>void (^)(void)</c>.</summary>
    public static void CallBlock(IntPtr block)
    {
        if (block != IntPtr.Zero)
            ((delegate* unmanaged[Cdecl]<IntPtr, void>)*(IntPtr*)((byte*)block + 16))(block);
    }

    /// <summary>Calls a block the system gave, <c>void (^)(NSUInteger)</c>.</summary>
    public static void CallBlock(IntPtr block, nuint argument)
    {
        if (block != IntPtr.Zero)
            ((delegate* unmanaged[Cdecl]<IntPtr, nuint, void>)*(IntPtr*)((byte*)block + 16))(block, argument);
    }
}
#endif
