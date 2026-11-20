namespace MyHttpServer.Framework.Attributes;

[AttributeUsage(AttributeTargets.Method, Inherited = false)]
public sealed class HttpPostAttribute : Attribute;

[AttributeUsage(AttributeTargets.Method, Inherited = false)]
public sealed class HttpPutAttribute : Attribute;
