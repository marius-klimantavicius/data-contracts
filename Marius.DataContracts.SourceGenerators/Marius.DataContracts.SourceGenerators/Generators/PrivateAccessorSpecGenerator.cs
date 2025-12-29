using System.Text;
using Marius.DataContracts.SourceGenerators.Specs;
using Microsoft.CodeAnalysis.CSharp;

namespace Marius.DataContracts.SourceGenerators.Generators;

internal class PrivateAccessorSpecGenerator
{
    public static void GeneratePrivateAccessors(CodeWriter writer, DataContractSetSpec contractSet)
    {
        if (contractSet.PrivateAccessors.Length == 0)
            return;

        var nonGenericAccessors = new List<PrivateAccessorSpec>();
        var genericAccessorGroups = new Dictionary<string, (List<PrivateAccessorSpec> Accessors, string ClassName)>();
        foreach (var accessor in contractSet.PrivateAccessors)
        {
            if (!accessor.ContainingType.IsGenericType)
            {
                nonGenericAccessors.Add(accessor);
            }
            else
            {
                var key = GetGenericAccessorClassKey(accessor);
                if (!genericAccessorGroups.TryGetValue(key, out var group))
                {
                    var accessorClassName = writer.LocalName("PrivateAccessor");
                    group = (new List<PrivateAccessorSpec>(), accessorClassName);
                    genericAccessorGroups[key] = group;
                }

                group.Accessors.Add(accessor);
            }
        }

        writer.AppendLine();
        writer.AppendLine("private static class PrivateAccessor");
        using (writer.Block())
        {
            var needLine = false;
            foreach (var accessor in nonGenericAccessors)
            {
                if (needLine)
                    writer.AppendLine();
                needLine = true;

                switch (accessor.Kind)
                {
                    case PrivateAccessorKind.Constructor:
                    case PrivateAccessorKind.Method:
                        GenerateMethodAccessor(writer, accessor);
                        break;
                    case PrivateAccessorKind.Field:
                        GenerateFieldAccessor(writer, accessor);
                        break;
                }
            }

            foreach (var (accessors, className) in genericAccessorGroups.Values)
            {
                foreach (var accessor in accessors)
                {
                    if (needLine)
                        writer.AppendLine();
                    needLine = true;

                    GenerateWrapperAccessor(writer, accessor, className);
                }
            }
        }

        foreach (var (accessors, className) in genericAccessorGroups.Values)
        {
            writer.AppendLine();
            GenerateGenericAccessorClass(writer, className, accessors);
        }
    }

    private static string GetGenericAccessorClassKey(PrivateAccessorSpec accessor)
    {
        var sb = new ValueStringBuilder(stackalloc char[256]);
        var constructedFrom = accessor.ContainingType.ConstructedFrom ?? accessor.ContainingType;
        if (constructedFrom.TypeParameters.Length > 0)
        {
            sb.Append(constructedFrom.TypeParameters.Length);
            sb.Append('_');
            foreach (var tp in constructedFrom.TypeParameters)
            {
                sb.Append('_');
                sb.Append(tp.Ordinal);
                sb.Append('_');
                sb.Append(tp.Name);
                if (tp.HasValueTypeConstraint) sb.Append("_struct");
                if (tp.HasReferenceTypeConstraint) sb.Append("_class");
                if (tp.HasUnmanagedConstraint) sb.Append("_unmanaged");
                if (tp.HasNotNullConstraint) sb.Append("_notnull");
                if (tp.HasConstructorConstraint) sb.Append("_new");
                foreach (var c in tp.Constraints)
                {
                    sb.Append('_');
                    sb.Append(c.ConstraintTypeFullName ?? "null");
                }
            }
        }

        return sb.ToString();
    }

    private static void GenerateWrapperAccessor(CodeWriter writer, PrivateAccessorSpec accessor, string genericAccessorClassName)
    {
        var typeParamList = "";
        var constraintsClauses = "";

        var typeParameters = accessor.ContainingType.TypeParameters;
        if (typeParameters.Length > 0)
        {
            var sb = new ValueStringBuilder(stackalloc char[256]);
            foreach (var item in typeParameters)
            {
                if (sb.Length == 0)
                    sb.Append('<');
                else
                    sb.Append(", ");

                sb.Append(item.Name);
            }

            sb.Append('>');

            typeParamList = sb.ToString();
            constraintsClauses = GenerateConstraintsClauses(typeParameters);
        }

        var className = $"{genericAccessorClassName}{typeParamList}";
        switch (accessor.Kind)
        {
            case PrivateAccessorKind.Constructor:
            case PrivateAccessorKind.Method:
                GenerateMethodWrapper(writer, accessor, typeParamList, constraintsClauses, className);
                break;
            case PrivateAccessorKind.Field:
                GenerateFieldWrapper(writer, accessor, typeParamList, constraintsClauses, className);
                break;
        }
    }

    private static void GenerateMethodWrapper(CodeWriter writer, PrivateAccessorSpec accessor, string typeParamList, string constraintsClauses, string className)
    {
        var args = "";
        var callArgs = "";
        if (accessor.Parameters.Length > 0)
        {
            args = string.Join(", ", accessor.Parameters.AsArray().Select(p =>
            {
                var modifier = p.RefKind switch
                {
                    ParameterRefKind.Ref => "ref ",
                    ParameterRefKind.Out => "out ",
                    ParameterRefKind.In => "in ",
                    _ => "",
                };
                return $"{modifier}{p.Type.FullyQualifiedName} {p.Name}";
            }));
            callArgs = string.Join(", ", accessor.Parameters.AsArray().Select(p =>
            {
                var modifier = p.RefKind switch
                {
                    ParameterRefKind.Ref => "ref ",
                    ParameterRefKind.Out => "out ",
                    ParameterRefKind.In => "in ",
                    _ => "",
                };
                return $"{modifier}{p.Name}";
            }));
        }

        if (accessor.IsRegularConstructor)
        {
            writer.AppendLine($"[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
            writer.AppendLine($"public static {accessor.ContainingType.FullyQualifiedName} {accessor.Name}{typeParamList}({args}){constraintsClauses}");
            writer.AppendLine($"    => {className}.{accessor.Name}({callArgs});");
        }
        else
        {
            var selfArg = $"{accessor.ContainingType.MaybeRef()}{accessor.ContainingType.FullyQualifiedName} self";
            var selfCallArg = accessor.ContainingType.IsValueType ? "ref self" : "self";

            if (!string.IsNullOrEmpty(args))
            {
                args = ", " + args;
                callArgs = ", " + callArgs;
            }

            writer.AppendLine($"[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
            if (accessor.Kind == PrivateAccessorKind.Constructor)
            {
                writer.AppendLine($"public static void {accessor.Name}{typeParamList}({selfArg}{args}){constraintsClauses}");
                writer.AppendLine($"    => {className}.{accessor.Name}({selfCallArg}{callArgs});");
            }
            else
            {
                writer.AppendLine($"public static {accessor.ReturnType?.FullyQualifiedName} {accessor.Name}{typeParamList}({selfArg}{args}){constraintsClauses}");
                writer.AppendLine($"    => {className}.{accessor.Name}({selfCallArg}{callArgs});");
            }
        }
    }

    private static void GenerateFieldWrapper(CodeWriter writer, PrivateAccessorSpec accessor, string typeParamList, string constraintsClauses, string className)
    {
        var selfArg = $"{accessor.ContainingType.MaybeRef()}{accessor.ContainingType.FullyQualifiedName} self";
        var selfCallArg = accessor.ContainingType.IsValueType ? "ref self" : "self";

        writer.AppendLine($"[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
        writer.AppendLine($"public static ref {accessor.ReturnType!.FullyQualifiedName} {accessor.Name}{typeParamList}({selfArg}){constraintsClauses}");
        writer.AppendLine($"    => ref {className}.{accessor.Name}({selfCallArg});");
    }

    private static void GenerateGenericAccessorClass(CodeWriter writer, string baseName, List<PrivateAccessorSpec> accessors)
    {
        var firstAccessor = accessors[0];
        var typeParams = firstAccessor.ContainingType.TypeParameters;
        if (typeParams.Length == 0)
        {
            writer.AppendLine($"private static class {baseName}");
        }
        else
        {
            var typeParamList = "<" + string.Join(", ", typeParams.Select(tp => tp.Name)) + ">";
            var constraints = GenerateConstraintsClauses(typeParams);
            writer.AppendLine($"private static class {baseName}{typeParamList}{constraints}");
        }

        using (writer.Block())
        {
            var needLine = false;
            foreach (var accessor in accessors)
            {
                if (needLine)
                    writer.AppendLine();

                needLine = true;

                switch (accessor.Kind)
                {
                    case PrivateAccessorKind.Constructor:
                    case PrivateAccessorKind.Method:
                        GenerateMethodAccessor(writer, accessor);
                        break;
                    case PrivateAccessorKind.Field:
                        GenerateFieldAccessor(writer, accessor);
                        break;
                }
            }
        }
    }

    private static string GenerateConstraintsClauses(IEnumerable<TypeParameterSpec> typeParameters)
    {
        var clauses = new ValueStringBuilder();
        var constraints = new ValueStringBuilder();
        foreach (var tp in typeParameters)
        {
            var hasConstraint = false;

            constraints.Append("where ");
            constraints.Append(tp.Name);
            constraints.Append(" : ");

            // Special constraints must come in specific order: class/struct/unmanaged first, then types, then new()
            if (tp.HasUnmanagedConstraint)
                Append(ref constraints, "unmanaged", ref hasConstraint);
            else if (tp.HasValueTypeConstraint)
                Append(ref constraints, "struct", ref hasConstraint);
            else if (tp.HasReferenceTypeConstraint)
                Append(ref constraints, tp.HasReferenceTypeConstraintNullable ? "class?" : "class", ref hasConstraint);

            if (tp.HasNotNullConstraint && !tp.HasValueTypeConstraint && !tp.HasReferenceTypeConstraint)
                Append(ref constraints, "notnull", ref hasConstraint);

            // Type constraints
            foreach (var c in tp.Constraints) 
                Append(ref constraints, c.ConstraintTypeFullName!, ref hasConstraint);

            // new() must come last
            if (tp.HasConstructorConstraint && !tp.HasValueTypeConstraint && !tp.HasUnmanagedConstraint)
                Append(ref constraints, "new()", ref hasConstraint);

            if (hasConstraint)
            {
                clauses.Append(' ');
                clauses.Append(constraints.GetStringAndClear());
            }
        }

        constraints.Dispose();
        if (clauses.Length == 0)
            return "";

        return clauses.ToString();

        static void Append(ref ValueStringBuilder sb, string value, ref bool needComma)
        {
            if (needComma)
                sb.Append(", ");

            sb.Append(value);
            needComma = true;
        }
    }

    private static void GenerateMethodAccessor(CodeWriter writer, PrivateAccessorSpec accessor)
    {
        var args = "";
        if (accessor.Parameters.Length > 0)
        {
            args = string.Join(", ", accessor.Parameters.AsArray().Select(p =>
            {
                var modifier = p.RefKind switch
                {
                    ParameterRefKind.Ref => "ref ",
                    ParameterRefKind.Out => "out ",
                    ParameterRefKind.In => "in ",
                    _ => "",
                };
                return $"{modifier}{p.Type.FullyQualifiedName} {p.Name}";
            }));
        }

        if (accessor.IsRegularConstructor)
        {
            writer.AppendLine($"[global::System.Runtime.CompilerServices.UnsafeAccessor(global::System.Runtime.CompilerServices.UnsafeAccessorKind.Constructor, Name = {SymbolDisplay.FormatLiteral(accessor.TargetName, true)})]");
            writer.AppendLine($"public static extern {accessor.ContainingType.FullyQualifiedName} {accessor.Name}({args});");
        }
        else
        {
            if (!string.IsNullOrEmpty(args))
                args = ", " + args;

            writer.AppendLine($"[global::System.Runtime.CompilerServices.UnsafeAccessor(global::System.Runtime.CompilerServices.UnsafeAccessorKind.Method, Name = {SymbolDisplay.FormatLiteral(accessor.TargetName, true)})]");
            if (accessor.Kind == PrivateAccessorKind.Constructor)
                writer.AppendLine($"public static extern void {accessor.Name}({accessor.ContainingType.MaybeRef()}{accessor.ContainingType.FullyQualifiedName} self{args});");
            else
                writer.AppendLine($"public static extern {accessor.ReturnType?.FullyQualifiedName} {accessor.Name}({accessor.ContainingType.MaybeRef()}{accessor.ContainingType.FullyQualifiedName} self{args});");
        }
    }

    private static void GenerateFieldAccessor(CodeWriter writer, PrivateAccessorSpec accessor)
    {
        writer.AppendLine($"[global::System.Runtime.CompilerServices.UnsafeAccessor(global::System.Runtime.CompilerServices.UnsafeAccessorKind.Field, Name = {SymbolDisplay.FormatLiteral(accessor.TargetName, true)})]");
        writer.AppendLine($"public static extern ref {accessor.ReturnType!.FullyQualifiedName} {accessor.Name}({accessor.ContainingType.MaybeRef()}{accessor.ContainingType.FullyQualifiedName} self);");
    }
}