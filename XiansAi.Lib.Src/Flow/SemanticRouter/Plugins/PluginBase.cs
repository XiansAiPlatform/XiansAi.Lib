using Microsoft.SemanticKernel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Agentri.Flow.Router.Plugins;

/// <summary>
/// Base class for all plugins that provides common functionality.
/// </summary>
public abstract class PluginBase<T>
{
    /// <summary>
    /// Gets all the kernel functions defined in a plugin class.
    /// </summary>
    /// <param name="pluginType">The plugin type to extract functions from.</param>
    /// <returns>A collection of kernel functions.</returns>
    public static IEnumerable<KernelFunction> GetFunctions()
    {
        var pluginType = typeof(T);
        var functions = new List<KernelFunction>();
        var methods = GetCapabilityMethods(pluginType);

        foreach (var method in methods)
        {
            var parameterMetadata = CreateParameterMetadata(method);
            var capabilityAttribute = method.GetCustomAttribute<CapabilityAttribute>();
            
            var function = KernelFunctionFactory.CreateFromMethod(
                method: method,
                description: capabilityAttribute!.Description,
                parameters: parameterMetadata
            );
            
            functions.Add(function);
        }

        return functions;
    }

    /// <summary>
    /// Gets all methods marked with the CapabilityAttribute.
    /// </summary>
    /// <param name="pluginType">The plugin type to extract methods from.</param>
    /// <returns>Methods marked with CapabilityAttribute.</returns>
    private static IEnumerable<MethodInfo> GetCapabilityMethods(Type pluginType)
    {
        return pluginType.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(m => m.GetCustomAttributes(typeof(CapabilityAttribute), false).Any());
    }

    /// <summary>
    /// Creates parameter metadata for a method.
    /// </summary>
    /// <param name="method">The method to extract parameter metadata from.</param>
    /// <returns>List of parameter metadata.</returns>
    private static List<KernelParameterMetadata> CreateParameterMetadata(MethodInfo method)
    {
        var parameters = method.GetParameters();
        var parameterMetadata = new List<KernelParameterMetadata>();
        
        // Get all Parameter attributes for this method
        var parameterAttributes = method.GetCustomAttributes<ParameterAttribute>().ToList();
        
        foreach (var parameter in parameters)
        {
            if (!string.IsNullOrEmpty(parameter.Name))
            {
                // Try to find matching parameter attribute
                var paramAttribute = parameterAttributes.FirstOrDefault(p => p.Name == parameter.Name);
                var description = paramAttribute?.Description ?? throw new Exception($"Parameter {parameter.Name} has no description. Check your Capability method {method.Name} for the {parameter.Name} parameter.");
                
                parameterMetadata.Add(new KernelParameterMetadata(parameter.Name) {
                    Description = description,
                    ParameterType = parameter.ParameterType
                });
            }
        }

        return parameterMetadata;
    }
} 