using System.Reflection;

internal static class ApiContract
{
    internal static string[] Describe(Assembly assembly)
    {
        static string TypeName(Type t) => t.IsGenericType ? t.GetGenericTypeDefinition().FullName!.Split('`')[0] + "<" + string.Join(",", t.GetGenericArguments().Select(TypeName)) + ">" : t.FullName ?? t.Name;
        var result = new List<string>();
        foreach (var type in assembly.GetExportedTypes().OrderBy(x => x.FullName, StringComparer.Ordinal))
        {
            result.Add($"TYPE {TypeName(type)} : {type.BaseType?.FullName} [{string.Join(",", type.GetInterfaces().Select(TypeName).OrderBy(x => x, StringComparer.Ordinal))}]");
            const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
            foreach (var member in type.GetMembers(flags).OrderBy(x => x.ToString(), StringComparer.Ordinal))
            {
                if (member is MethodInfo method && method.IsSpecialName) continue;
                string details = member switch
                {
                    PropertyInfo p => $"{TypeName(p.PropertyType)} {p.Name} {{ {(p.GetMethod?.IsPublic == true ? "get; " : "")}{(p.SetMethod?.IsPublic == true ? "set; " : "")} }}",
                    FieldInfo f when f.IsLiteral => $"{f} = {Convert.ToString(f.GetRawConstantValue(), System.Globalization.CultureInfo.InvariantCulture)}",
                    _ => member.ToString() ?? member.Name
                };
                result.Add($"  {member.MemberType}: {details}");
            }
        }
        return result.ToArray();
    }
}
