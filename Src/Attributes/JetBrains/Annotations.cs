/* MIT License

Copyright (c) 2016 JetBrains http://www.jetbrains.com

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE. */

#nullable disable

using System.Runtime.CompilerServices;
using System.Diagnostics;

// ReSharper disable UnusedType.Global

#pragma warning disable 1591

// ReSharper disable UnusedMember.Global
// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable UnusedAutoPropertyAccessor.Global
// ReSharper disable IntroduceOptionalParameters.Global
// ReSharper disable MemberCanBeProtected.Global
// ReSharper disable InconsistentNaming

[assembly: InternalsVisibleTo("FastEndpoints")]

namespace JetBrains.Annotations;

/// <summary>
/// Indicates that the marked parameter, field, or property is a route template.
/// </summary>
[AttributeUsage(AttributeTargets.Parameter), Conditional("JETBRAINS_ANNOTATIONS")]
sealed class RouteTemplateAttribute : Attribute;

/// <summary>
/// Indicates that the marked symbol is used implicitly (e.g. via reflection, in external library), so this symbol will be ignored by usage-checking inspections. You can use
/// <see cref="ImplicitUseKindFlags" /> and <see cref="ImplicitUseTargetFlags" /> to configure how this attribute is applied.
/// </summary>
[AttributeUsage(AttributeTargets.All), Conditional("JETBRAINS_ANNOTATIONS")]
sealed class UsedImplicitlyAttribute(ImplicitUseKindFlags useKindFlags, ImplicitUseTargetFlags targetFlags) : Attribute
{
    public ImplicitUseKindFlags UseKindFlags { get; } = useKindFlags;
    public ImplicitUseTargetFlags TargetFlags { get; } = targetFlags;

    public UsedImplicitlyAttribute() : this(ImplicitUseKindFlags.Default, ImplicitUseTargetFlags.Default) { }
    public UsedImplicitlyAttribute(ImplicitUseKindFlags useKindFlags) : this(useKindFlags, ImplicitUseTargetFlags.Default) { }
    public UsedImplicitlyAttribute(ImplicitUseTargetFlags targetFlags) : this(ImplicitUseKindFlags.Default, targetFlags) { }
}

/// <summary>
/// Specifies the details of an implicitly used symbol when it is marked with <see cref="UsedImplicitlyAttribute" />.
/// </summary>
[Flags]
enum ImplicitUseKindFlags
{
    Default = Access | Assign | InstantiatedWithFixedConstructorSignature,

    /// <summary>Only entity marked with attribute considered used.</summary>
    Access = 1,

    /// <summary>Indicates implicit assignment to a member.</summary>
    Assign = 2,

    /// <summary>
    /// Indicates implicit instantiation of a type with fixed constructor signature.
    /// That means any unused constructor parameters won't be reported as such.
    /// </summary>
    InstantiatedWithFixedConstructorSignature = 4,

    /// <summary>Indicates implicit instantiation of a type.</summary>
    InstantiatedNoFixedConstructorSignature = 8
}

/// <summary>
/// Specifies what is considered to be used implicitly when marked with <see cref="UsedImplicitlyAttribute" />.
/// </summary>
[Flags]
enum ImplicitUseTargetFlags
{
    Default = Itself,
    Itself = 1,

    /// <summary>Members of the type marked with the attribute are considered used.</summary>
    Members = 2,

    /// <summary> Inherited entities are considered used. </summary>
    WithInheritors = 4,

    /// <summary>Entity marked with the attribute and all its members considered used.</summary>
    WithMembers = Itself | Members
}