using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Ampheck.RockchipToolkit.Generators;

[Generator(LanguageNames.CSharp)]
public sealed class JsonRpcServiceGenerator : IIncrementalGenerator {
    private const string ServiceAttributeName =
        "Ampheck.RockchipToolkit.Rpc.JsonRpc.JsonRpcServiceAttribute";
    private const string MethodAttributeName =
        "Ampheck.RockchipToolkit.Rpc.JsonRpc.JsonRpcMethodAttribute";
    private const string ContextTypeName =
        "Ampheck.RockchipToolkit.Rpc.JsonRpc.JsonRpcCallContext";
    private const string SerializerContextName =
        "Ampheck.RockchipToolkit.Rpc.JsonRpc.ToolkitJsonSerializerContext";

    private static readonly DiagnosticDescriptor InvalidService = new(
        "ARKT101",
        "JSON-RPC service is invalid",
        "Type '{0}' must be a non-abstract, non-generic class that is not private",
        "Ampheck.RockchipToolkit.Generators",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor InvalidMethod = new(
        "ARKT102",
        "JSON-RPC method signature is invalid",
        "Method '{0}' must be an instance ValueTask<TResult> or " +
        "Task<TResult> method with parameters " +
        "(TParams, JsonRpcCallContext)",
        "Ampheck.RockchipToolkit.Generators",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor MissingService = new(
        "ARKT103",
        "JSON-RPC method has no service registration",
        "Type '{0}' declares a JSON-RPC method but has no [JsonRpcService] attribute",
        "Ampheck.RockchipToolkit.Generators",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor DuplicateMethod = new(
        "ARKT104",
        "JSON-RPC method name is duplicated",
        "JSON-RPC method name '{0}' is declared more than once",
        "Ampheck.RockchipToolkit.Generators",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly SymbolDisplayFormat TypeFormat =
        SymbolDisplayFormat.FullyQualifiedFormat
            .WithGlobalNamespaceStyle(
                SymbolDisplayGlobalNamespaceStyle.Included)
            .WithMiscellaneousOptions(
                SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers |
                SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);

    public void Initialize(
        IncrementalGeneratorInitializationContext context) {
        var services = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                ServiceAttributeName,
                static (node, _) =>
                    node is ClassDeclarationSyntax,
                static (syntaxContext, _) =>
                    ReadService(syntaxContext));

        var methods = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                MethodAttributeName,
                static (node, _) =>
                    node is MethodDeclarationSyntax,
                static (syntaxContext, _) =>
                    ReadMethod(syntaxContext));

        context.RegisterSourceOutput(
            services.Collect().Combine(methods.Collect()),
            static (output, value) =>
                Emit(output, value.Left, value.Right));
    }

    private static ServiceResult ReadService(
        GeneratorAttributeSyntaxContext context) {
        if (context.TargetSymbol is not INamedTypeSymbol type ||
            !IsValidService(type)) {
            return new ServiceResult(
                null,
                Diagnostic.Create(
                    InvalidService,
                    context.TargetNode.GetLocation(),
                    context.TargetSymbol.ToDisplayString()));
        }

        var lifetime = JsonRpcServiceLifetime.Scoped;
        if (context.Attributes[0].ConstructorArguments.Length == 1 &&
            context.Attributes[0].ConstructorArguments[0].Value is int value) {
            lifetime = value switch {
                1 => JsonRpcServiceLifetime.Singleton,
                2 => JsonRpcServiceLifetime.Transient,
                _ => JsonRpcServiceLifetime.Scoped
            };
        }

        return new ServiceResult(
            new ServiceSpec(
                type.ToDisplayString(TypeFormat),
                lifetime),
            null);
    }

    private static MethodResult ReadMethod(
        GeneratorAttributeSyntaxContext context) {
        if (context.TargetSymbol is not IMethodSymbol method) {
            return new MethodResult(
                null,
                ImmutableArray<Diagnostic>.Empty);
        }

        var diagnostics = ImmutableArray.CreateBuilder<Diagnostic>();
        var location = context.TargetNode.GetLocation();
        var attribute = context.Attributes[0];
        var name = attribute.ConstructorArguments.Length > 0
            ? attribute.ConstructorArguments[0].Value as string
            : null;
        var longRunning = attribute.ConstructorArguments.Length > 1 &&
            attribute.ConstructorArguments[1].Value is true;
        if (!HasAttribute(
                method.ContainingType,
                ServiceAttributeName)) {
            diagnostics.Add(Diagnostic.Create(
                MissingService,
                location,
                method.ContainingType.ToDisplayString()));
        }

        if (string.IsNullOrWhiteSpace(name) ||
            method.IsStatic ||
            method.IsGenericMethod ||
            method.MethodKind != MethodKind.Ordinary ||
            method.DeclaredAccessibility == Accessibility.Private ||
            method.Parameters.Length != 2 ||
            !IsType(
                method.Parameters[1].Type,
                ContextTypeName) ||
            !TryGetResultType(
                method.ReturnType,
                out var resultType)) {
            diagnostics.Add(Diagnostic.Create(
                InvalidMethod,
                location,
                method.ToDisplayString()));
        }

        if (string.IsNullOrWhiteSpace(name) ||
            diagnostics.Count != 0 ||
            !TryGetResultType(
                method.ReturnType,
                out var resolvedResultType)) {
            return new MethodResult(
                null,
                diagnostics.ToImmutable());
        }

        return new MethodResult(
            new MethodSpec(
                name!,
                method.ContainingType.ToDisplayString(TypeFormat),
                method.Name,
                method.Parameters[0].Type.ToDisplayString(TypeFormat),
                resolvedResultType.ToDisplayString(TypeFormat),
                longRunning,
                location),
            diagnostics.ToImmutable());
    }

    private static bool TryGetResultType(
        ITypeSymbol type,
        out ITypeSymbol result) {
        result = null!;
        if (type is not INamedTypeSymbol named ||
            named.TypeArguments.Length != 1 ||
            named.ContainingNamespace.ToDisplayString() !=
                "System.Threading.Tasks" ||
            named.Name is not ("Task" or "ValueTask")) {
            return false;
        }

        result = named.TypeArguments[0];
        return true;
    }

    private static bool IsValidService(INamedTypeSymbol type) {
        return type.TypeKind == TypeKind.Class &&
            !type.IsAbstract &&
            !type.IsGenericType &&
            type.DeclaredAccessibility is not Accessibility.Private &&
            !HasPrivateContainer(type);
    }

    private static bool HasPrivateContainer(INamedTypeSymbol type) {
        for (var current = type.ContainingType;
            current is not null;
            current = current.ContainingType) {
            if (current.DeclaredAccessibility == Accessibility.Private) {
                return true;
            }
        }

        return false;
    }

    private static bool HasAttribute(
        ISymbol symbol,
        string metadataName) {
        return symbol.GetAttributes().Any(attribute =>
            string.Equals(
                attribute.AttributeClass?.ToDisplayString(),
                metadataName,
                StringComparison.Ordinal));
    }

    private static bool IsType(
        ITypeSymbol type,
        string metadataName) {
        return type is INamedTypeSymbol named &&
            string.Equals(
                named.ToDisplayString(),
                metadataName,
                StringComparison.Ordinal);
    }

    private static void Emit(
        SourceProductionContext context,
        ImmutableArray<ServiceResult> serviceResults,
        ImmutableArray<MethodResult> methodResults) {
        foreach (var diagnostic in serviceResults
            .Where(value => value.Diagnostic is not null)
            .Select(value => value.Diagnostic!)) {
            context.ReportDiagnostic(diagnostic);
        }

        foreach (var diagnostic in methodResults.SelectMany(
            value => value.Diagnostics)) {
            context.ReportDiagnostic(diagnostic);
        }

        var services = serviceResults
            .Where(value => value.Service is not null)
            .Select(value => value.Service!)
            .GroupBy(
                value => value.TypeName,
                StringComparer.Ordinal)
            .Select(group => group.First())
            .OrderBy(
                value => value.TypeName,
                StringComparer.Ordinal)
            .ToArray();
        var methods = methodResults
            .Where(value => value.Method is not null)
            .Select(value => value.Method!)
            .ToArray();
        foreach (var group in methods.GroupBy(
            value => value.Name,
            StringComparer.Ordinal)) {
            if (group.Count() <= 1) {
                continue;
            }

            foreach (var method in group) {
                context.ReportDiagnostic(Diagnostic.Create(
                    DuplicateMethod,
                    method.Location,
                    method.Name));
            }
        }

        var uniqueMethods = methods
            .GroupBy(
                value => value.Name,
                StringComparer.Ordinal)
            .Where(group => group.Count() == 1)
            .Select(group => group.Single())
            .OrderBy(
                value => value.Name,
                StringComparer.Ordinal)
            .ToArray();
        if (services.Length == 0 && uniqueMethods.Length == 0) {
            return;
        }

        var source = new StringBuilder();
        source.AppendLine("// <auto-generated/>");
        source.AppendLine("#nullable enable");
        source.AppendLine();
        source.AppendLine(
            "namespace Ampheck.RockchipToolkit.Server.Generated;");
        source.AppendLine();
        AppendRegistrations(source, services);
        source.AppendLine();
        AppendCatalog(source, uniqueMethods);
        context.AddSource(
            "GeneratedJsonRpcServices.g.cs",
            source.ToString());
    }

    private static void AppendRegistrations(
        StringBuilder source,
        IReadOnlyList<ServiceSpec> services) {
        source.AppendLine(
            "internal static class GeneratedServiceRegistrations {");
        source.AppendLine(
            "    internal static " +
            "global::Microsoft.Extensions.DependencyInjection." +
            "IServiceCollection AddGeneratedToolkitServices(");
        source.AppendLine(
            "        this global::Microsoft.Extensions." +
            "DependencyInjection.IServiceCollection services) {");
        foreach (var service in services) {
            var registration = service.Lifetime switch {
                JsonRpcServiceLifetime.Singleton => "TryAddSingleton",
                JsonRpcServiceLifetime.Transient => "TryAddTransient",
                _ => "TryAddScoped"
            };
            source.Append(
                "        global::Microsoft.Extensions.DependencyInjection." +
                "Extensions.ServiceCollectionDescriptorExtensions.");
            source.Append(registration);
            source.Append('<');
            source.Append(service.TypeName);
            source.AppendLine(">(services);");
        }

        source.AppendLine("        return services;");
        source.AppendLine("    }");
        source.AppendLine("}");
    }

    private static void AppendCatalog(
        StringBuilder source,
        IReadOnlyList<MethodSpec> methods) {
        source.AppendLine(
            "internal static class GeneratedJsonRpcCatalog {");
        source.AppendLine(
            "    internal static global::System.Collections.Generic.IReadOnlyList<string> Methods { get; } = [");
        foreach (var method in methods) {
            source.Append("        ");
            AppendLiteral(source, method.Name);
            source.AppendLine(",");
        }

        source.AppendLine("    ];");
        source.AppendLine();
        source.AppendLine(
            "    internal static bool IsLongRunning(string method) {");
        source.AppendLine("        return method switch {");
        foreach (var method in methods.Where(value => value.LongRunning)) {
            source.Append("            ");
            AppendLiteral(source, method.Name);
            source.AppendLine(" => true,");
        }

        source.AppendLine("            _ => false");
        source.AppendLine("        };");
        source.AppendLine("    }");
        source.AppendLine();
        source.AppendLine(
            "    internal static async global::System.Threading.Tasks." +
            "ValueTask<global::System.Text.Json.JsonElement> " +
            "InvokeAsync(");
        source.AppendLine(
            "        global::System.IServiceProvider services,");
        source.AppendLine("        string method,");
        source.AppendLine(
            "        global::System.Text.Json.JsonElement parameters,");
        source.AppendLine(
            "        global::Ampheck.RockchipToolkit.Rpc.JsonRpc.JsonRpcCallContext context) {");
        source.AppendLine("        switch (method) {");
        foreach (var method in methods) {
            source.Append("            case ");
            AppendLiteral(source, method.Name);
            source.AppendLine(":");
            source.AppendLine("            {");
            source.Append(
                "                var methodParameters = " +
                "global::System.Text.Json.JsonSerializer." +
                "Deserialize(parameters, typeof(");
            source.Append(method.ParameterType);
            source.AppendLine(
                "), global::Ampheck.RockchipToolkit.Rpc.JsonRpc.ToolkitJsonSerializerContext.Default);");
            source.Append(
                "                if (methodParameters is not ");
            source.Append(method.ParameterType);
            source.AppendLine(" typedParameters) {");
            source.AppendLine(
                "                    throw new global::Ampheck.RockchipToolkit.Rpc.JsonRpc.ToolkitRpcException(");
            source.AppendLine(
                "                        global::Ampheck.RockchipToolkit.Rpc.JsonRpc.JsonRpcErrorCodes.InvalidParams,");
            source.AppendLine(
                "                        \"Invalid method parameters.\");");
            source.AppendLine("                }");
            source.Append(
                "                var service = global::Microsoft.Extensions." +
                "DependencyInjection.ServiceProviderServiceExtensions." +
                "GetRequiredService<");
            source.Append(method.ServiceType);
            source.AppendLine(">(services);");
            source.Append(
                "                var result = await service.");
            source.Append(method.MethodName);
            source.AppendLine(
                "(typedParameters, context).ConfigureAwait(false);");
            source.Append(
                "                return global::System.Text.Json.JsonSerializer.SerializeToElement(result, typeof(");
            source.Append(method.ResultType);
            source.AppendLine(
                "), global::Ampheck.RockchipToolkit.Rpc.JsonRpc.ToolkitJsonSerializerContext.Default);");
            source.AppendLine("            }");
        }

        source.AppendLine("            default:");
        source.AppendLine(
            "                throw new global::Ampheck.RockchipToolkit.Rpc.JsonRpc.ToolkitRpcException(");
        source.AppendLine(
            "                    global::Ampheck.RockchipToolkit.Rpc.JsonRpc.JsonRpcErrorCodes.MethodNotFound,");
        source.AppendLine("                    $\"Unknown JSON-RPC method '{method}'.\");");
        source.AppendLine("        }");
        source.AppendLine("    }");
        source.AppendLine("}");
    }

    private static void AppendLiteral(
        StringBuilder source,
        string value) {
        source.Append(SymbolDisplay.FormatLiteral(value, quote: true));
    }

    private sealed class ServiceResult(
        ServiceSpec? service,
        Diagnostic? diagnostic) {
        public ServiceSpec? Service { get; } = service;
        public Diagnostic? Diagnostic { get; } = diagnostic;
    }

    private sealed class MethodResult(
        MethodSpec? method,
        ImmutableArray<Diagnostic> diagnostics) {
        public MethodSpec? Method { get; } = method;
        public ImmutableArray<Diagnostic> Diagnostics { get; } =
            diagnostics;
    }

    private sealed class ServiceSpec(
        string typeName,
        JsonRpcServiceLifetime lifetime) {
        public string TypeName { get; } = typeName;
        public JsonRpcServiceLifetime Lifetime { get; } = lifetime;
    }

    private sealed class MethodSpec(
        string name,
        string serviceType,
        string methodName,
        string parameterType,
        string resultType,
        bool longRunning,
        Location location) {
        public string Name { get; } = name;
        public string ServiceType { get; } = serviceType;
        public string MethodName { get; } = methodName;
        public string ParameterType { get; } = parameterType;
        public string ResultType { get; } = resultType;
        public bool LongRunning { get; } = longRunning;
        public Location Location { get; } = location;
    }

    private enum JsonRpcServiceLifetime {
        Scoped,
        Singleton,
        Transient
    }
}
