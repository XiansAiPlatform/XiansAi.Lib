using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using XiansAi.Messaging;

namespace XiansAi.Router.Plugins;

public class PluginReaderLogger {}

/// <summary>
/// Base class for all plugins that provides common functionality.
/// </summary>
public static class PluginReader
{

    private static readonly ILogger _logger = Globals.LogFactory.CreateLogger<PluginReaderLogger>();

    public static IEnumerable<KernelFunction> GetFunctions(string pluginName, MessageThread messageThread)
    {
        // Try to get the type directly first
        var pluginType = Type.GetType(pluginName);
        
        // If not found, search through all loaded assemblies
        if (pluginType == null)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                pluginType = assembly.GetType(pluginName);
                if (pluginType != null)
                    break;
            }
        }
        
        if (pluginType == null)
        {
            throw new Exception($"Plugin type {pluginName} not found.");
        }
        if (pluginType.IsAbstract && pluginType.IsSealed)
        {
            // static plugin
            _logger.LogInformation("Getting functions from static type {PluginType}", pluginType.Name);
            return GetFunctionsFromStaticType(pluginType);
        }
        else
        {
            _logger.LogInformation("Getting functions from instance type {PluginType}", pluginType.Name);
            return GetFunctionsFromInstanceType(pluginType, messageThread);
        }
    }

    /// <summary>
    /// Gets all the kernel functions defined in a plugin class.
    /// </summary>
    /// <param name="pluginType">The plugin type to extract functions from.</param>
    /// <returns>A collection of kernel functions.</returns>
    public static IEnumerable<KernelFunction> GetFunctionsFromStaticType(Type pluginType)
    {
        var functions = new List<KernelFunction>();
        var methods = GetCapabilityMethods(pluginType);

        foreach (var method in methods)
        {
            var parameterMetadata = CreateParameterMetadata(method);
            var capabilityAttribute = method.GetCustomAttribute<CapabilityAttribute>()?.Description 
                ?? throw new Exception($"Capability for method {method.Name} has no description.");
            var returnParameter = new KernelReturnParameterMetadata {
                Description = method.GetCustomAttribute<ReturnsAttribute>()?.Description 
                    ?? throw new Exception($"Return parameter for method {method.Name} has no description."),
                ParameterType = method.ReturnType
            };
            
            var function = KernelFunctionFactory.CreateFromMethod(
                method: method,
                description: capabilityAttribute,
                parameters: parameterMetadata,
                returnParameter: returnParameter
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
                var description = paramAttribute?.Description ?? throw new Exception($"Parameter {parameter.Name} has no description.");
                
                parameterMetadata.Add(new KernelParameterMetadata(parameter.Name) {
                    Description = description,
                    ParameterType = parameter.ParameterType
                });
            }
        }

        return parameterMetadata;
    }

    /// <summary>
    /// Gets all the kernel functions defined in a non-static plugin class.
    /// </summary>
    /// <param name="pluginType">The plugin type to extract functions from.</param>
    /// <param name="messageThread">The message thread to pass to the plugin instance, if needed.</param>
    /// <returns>A collection of kernel functions.</returns>
    public static IEnumerable<KernelFunction> GetFunctionsFromInstanceType(Type pluginType, MessageThread messageThread)
    {
        var instance = Activator.CreateInstance(pluginType, messageThread) 
            ?? throw new Exception($"Failed to create instance of {pluginType.Name}");
                
        var functions = new List<KernelFunction>();
        var methods = pluginType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.GetCustomAttributes(typeof(CapabilityAttribute), false).Any());

        foreach (var method in methods)
        {
            var parameterMetadata = CreateParameterMetadata(method);
            var capabilityAttribute = method.GetCustomAttribute<CapabilityAttribute>()?.Description 
                ?? throw new Exception($"Capability for method {method.Name} has no description.");
            var returnParameter = new KernelReturnParameterMetadata {
                Description = method.GetCustomAttribute<ReturnsAttribute>()?.Description 
                    ?? throw new Exception($"Return parameter for method {method.Name} has no description."),
                ParameterType = method.ReturnType
            };
            
            var function = KernelFunctionFactory.CreateFromMethod(
                method: method,
                description: capabilityAttribute,
                parameters: parameterMetadata,
                returnParameter: returnParameter,
                target: instance
            );
            
            functions.Add(function);
        }

        return functions;
    
    }
} 