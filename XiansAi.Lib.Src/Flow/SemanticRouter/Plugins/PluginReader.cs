using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using XiansAi.Activity;
using XiansAi.Knowledge;
using XiansAi.Messaging;

namespace XiansAi.Flow.Router.Plugins;

internal class PluginReaderLogger { }

/// <summary>
/// Base class for all plugins that provides common functionality.
/// </summary>
internal static class PluginReader
{
    private static readonly ILogger _logger = Globals.LogFactory.CreateLogger<PluginReaderLogger>();


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
            if (method.GetCustomAttribute<KnowledgeAttribute>() != null)
            {
                functions.Add(CreateKernelFunctionFromKnowledgeAttribute(method));
            }
            else
            {
                var parameterMetadata = CreateParameterMetadata(method);
                var capabilityAttribute = method.GetCustomAttribute<CapabilityAttribute>()?.Description
                    ?? throw new Exception($"Capability for method `{method.Name}` has no description. Check your Capability method `{method.Name}`.");
                var returnParameter = new KernelReturnParameterMetadata
                {
                    Description = method.GetCustomAttribute<ReturnsAttribute>()?.Description
                        ?? throw new Exception($"Return parameter for method `{method.Name}` has no description. Check your Capability method `{method.Name}`."),
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
        }

        return functions;
    }

    /// <summary>
    /// Gets all methods marked with either CapabilityAttribute or CapabilityKnowledgeAttribute.
    /// </summary>
    /// <param name="pluginType">The plugin type to extract methods from.</param>
    /// <returns>Methods marked with capability-related attributes.</returns>
    private static IEnumerable<MethodInfo> GetCapabilityMethods(Type pluginType)
    {
        var bindingFlags = BindingFlags.Public | BindingFlags.Static;
        if (!pluginType.IsAbstract || !pluginType.IsSealed)
        {
            bindingFlags = BindingFlags.Public | BindingFlags.Instance;
        }

        return pluginType.GetMethods(bindingFlags)
            .Where(m => m.GetCustomAttributes(typeof(CapabilityAttribute), false).Any() ||
                       m.GetCustomAttributes(typeof(KnowledgeAttribute), false).Any());
    }

    /// <summary>
    /// Creates a kernel function from a method marked with CapabilityKnowledgeAttribute.
    /// </summary>
    /// <param name="method">The method with the CapabilityKnowledgeAttribute.</param>
    /// <param name="target">Optional target instance for instance methods.</param>
    /// <returns>A kernel function created from the knowledge file.</returns>
    private static KernelFunction CreateKernelFunctionFromKnowledgeAttribute(MethodInfo method, object? target = null)
    {
        var knowledgeAttribute = method.GetCustomAttribute<KnowledgeAttribute>()
            ?? throw new Exception($"Method `{method.Name}` does not have KnowledgeAttribute.");

        // Load the knowledge from the JSON file
        var knowledge = CapabilityKnowledgeLoader.Load(knowledgeAttribute.Knowledge[0]);

        if (knowledge == null)
        {
            throw new Exception($"Knowledge for method `{method.Name}` not found.");
        }

        // Create parameter metadata from the knowledge model
        var parameters = method.GetParameters();
        var parameterMetadata = new List<KernelParameterMetadata>();

        foreach (var parameter in parameters)
        {
            if (string.IsNullOrEmpty(parameter.Name))
                continue;

            if (!knowledge.Parameters.TryGetValue(parameter.Name, out var description))
                throw new Exception($"Parameter `{parameter.Name}` for method `{method.Name}` is not described in the knowledge file.");

            parameterMetadata.Add(new KernelParameterMetadata(parameter.Name)
            {
                Description = description,
                ParameterType = parameter.ParameterType
            });
        }

        // Create return parameter metadata
        var returnParameter = new KernelReturnParameterMetadata
        {
            Description = knowledge.Returns,
            ParameterType = method.ReturnType
        };

        // Create the function
        var function = KernelFunctionFactory.CreateFromMethod(
            method: method,
            description: knowledge.Description,
            parameters: parameterMetadata,
            returnParameter: returnParameter,
            target: target
        );

        return function;
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
                var description = paramAttribute?.Description ?? throw new Exception($"Parameter `{parameter.Name}` has no description. Check your Capability method `{method.Name}`.");

                parameterMetadata.Add(new KernelParameterMetadata(parameter.Name)
                {
                    Description = description,
                    ParameterType = parameter.ParameterType,
                    IsRequired = !parameter.IsOptional,
                    Name = parameter.Name,
                    DefaultValue = parameter.DefaultValue
                });
            }
        }

        return parameterMetadata;
    }

    /// <summary>
    /// Gets all the kernel functions defined in a non-static plugin class.
    /// </summary>
    /// <param name="pluginType">The plugin type to extract functions from.</param>
    /// <param name="instance">The instance of the plugin to extract functions from.</param>
    /// <returns>A collection of kernel functions.</returns>
    public static IEnumerable<KernelFunction> GetFunctionsFromInstanceType(Type pluginType, object instance)
    {
        try
        {
            if (instance == null)
            {
                throw new Exception($"Failed to create instance of {pluginType.Name}");
            }

            var functions = new List<KernelFunction>();
            var methods = pluginType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(m => m.GetCustomAttributes(typeof(CapabilityAttribute), false).Any() ||
                        m.GetCustomAttributes(typeof(KnowledgeAttribute), false).Any());

            foreach (var method in methods)
            {
                if (method.GetCustomAttribute<KnowledgeAttribute>() != null)
                {
                    var function = CreateKernelFunctionFromKnowledgeAttribute(method, instance);
                    functions.Add(function);
                }
                else
                {
                    var parameterMetadata = CreateParameterMetadata(method);
                    var capabilityAttribute = method.GetCustomAttribute<CapabilityAttribute>()?.Description
                        ?? throw new Exception($"Capability for method `{method.Name}` has no description. Check your Capability method `{method.Name}`.");
                    var returnParameter = new KernelReturnParameterMetadata
                    {
                        Description = method.GetCustomAttribute<ReturnsAttribute>()?.Description
                            ?? throw new Exception($"Return parameter for method `{method.Name}` has no description. Check your Capability method `{method.Name}`."),
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
            }

            return functions;
        }
        catch (MissingMethodException ex)
        {
            _logger.LogError(ex, "Failed to create instance of {PluginType}", pluginType.Name);
            throw new Exception($"Instance plugins must have a constructor that takes a MessageThread parameter.", ex);
        }
    }
}