namespace MyHttpServer.Framework;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = true)]
public sealed class AuthorizeAttribute : Attribute;
