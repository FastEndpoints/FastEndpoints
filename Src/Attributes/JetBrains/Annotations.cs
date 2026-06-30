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

#pragma warning disable 1591

// ReSharper disable UnusedType.Global
// ReSharper disable UnusedMember.Global
// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable UnusedAutoPropertyAccessor.Global
// ReSharper disable IntroduceOptionalParameters.Global
// ReSharper disable MemberCanBeProtected.Global
// ReSharper disable InconsistentNaming

[assembly: InternalsVisibleTo("FastEndpoints, PublicKey=002400000480000094000000060200000024000052534131000400000100010051c42d27b58576d0e7a31a419b1a07b3cfefcece51c94ebb3ac9309423361041fd6521dae4ea609d6d6bc1d23402413b13bb1a10e3f05ca0740f41a524a418a1e596b0eeb14facde12bbc1c07e0e5a5f1220a12821dfeb1ba070c8eae1f857508faa6ad00cfa7da3f57252e210be8900dd53b180ef319d4adab21f266ccf06a1")]

namespace JetBrains.Annotations;

/// <summary>
/// Indicates that the marked parameter, field, or property is a route template.
/// </summary>
[AttributeUsage(AttributeTargets.Parameter)]
sealed class RouteTemplateAttribute : Attribute;

/// <summary>
/// Indicates that the marked symbol is used implicitly (e.g. via reflection, in external library), so this symbol will be ignored by usage-checking inspections. You can use
/// <see cref="ImplicitUseKindFlags" /> and <see cref="ImplicitUseTargetFlags" /> to configure how this attribute is applied.
/// </summary>
[AttributeUsage(AttributeTargets.All)]
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