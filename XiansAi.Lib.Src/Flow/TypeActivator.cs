using System;
using System.Linq;

namespace XiansAi.Flow;

internal static class TypeActivator
{
    public static object CreateWithOptionalArgs(Type targetType, params object[] args)
    {
        if (targetType == null) throw new ArgumentNullException(nameof(targetType));
        args ??= Array.Empty<object>();

        // Try to find a constructor that matches the provided args
        if (args.Length > 0)
        {
            var matchingCtor = targetType
                .GetConstructors()
                .FirstOrDefault(ctor => ParametersMatch(ctor.GetParameters(), args));

            if (matchingCtor != null)
            {
                return Activator.CreateInstance(targetType, args)
                       ?? throw new InvalidOperationException($"Failed to create instance of {targetType.Name}");
            }
        }

        // Fallback to parameterless constructor if available
        var parameterlessCtor = targetType.GetConstructor(Type.EmptyTypes);
        if (parameterlessCtor != null)
        {
            return Activator.CreateInstance(targetType)
                   ?? throw new InvalidOperationException($"Failed to create instance of {targetType.Name}");
        }

        // As a last resort, attempt with provided args (will throw a clear exception if unsuitable)
        return Activator.CreateInstance(targetType, args)
               ?? throw new InvalidOperationException($"Failed to create instance of {targetType.Name}");
    }

    private static bool ParametersMatch(System.Reflection.ParameterInfo[] parameters, object[] args)
    {
        if (parameters.Length != args.Length) return false;
        for (var i = 0; i < parameters.Length; i++)
        {
            var parameterType = parameters[i].ParameterType;
            var argument = args[i];

            if (argument is null)
            {
                if (parameterType.IsValueType && Nullable.GetUnderlyingType(parameterType) == null)
                {
                    return false;
                }
                continue;
            }

            if (!parameterType.IsAssignableFrom(argument.GetType()))
            {
                return false;
            }
        }
        return true;
    }
}

