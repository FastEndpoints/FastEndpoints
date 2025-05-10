using System.Reflection;

#pragma warning disable CS8618

// ReSharper disable UnassignedGetOnlyAutoProperty

namespace FastEndpoints.OData;

sealed class ODataParamInfo : ParameterInfo
{
    public override IList<CustomAttributeData> GetCustomAttributesData()
        => [];

    public override object[] GetCustomAttributes(bool inherit)
        => [];

    public override MemberInfo Member { get; } = new ODataMemberInfo();

    class ODataMemberInfo : MemberInfo
    {
        public override Attribute[] GetCustomAttributes(bool inherit)
            => throw new NotImplementedException();

        public override Attribute[] GetCustomAttributes(Type attributeType, bool inherit)
            => throw new NotImplementedException();

        public override bool IsDefined(Type attributeType, bool inherit)
            => throw new NotImplementedException();

        public override Type? DeclaringType { get; }
        public override MemberTypes MemberType { get; } = MemberTypes.Method;
        public override string Name { get; }
        public override Type? ReflectedType { get; }
    }
}