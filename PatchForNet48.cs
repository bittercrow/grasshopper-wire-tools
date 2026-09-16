
#if NET48

namespace System.Runtime.CompilerServices
{
    // Workaround to support C# 9.0 'init' properties on .NET Framework 4.8 (Rhino 7).
    public static class IsExternalInit { }
}

#endif