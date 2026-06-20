// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// This is a polyfill for System.Runtime.CompilerServices.IsExternalInit, which is
// required to use `record` and `init` accessors on .NET Standard 2.0 targets.
// On .NET 5+ and .NET Core 3.0+ this type is provided by the runtime.
// See: https://learn.microsoft.com/dotnet/api/system.runtime.compilerservices.isexternalinit

namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit
    {
    }
}
