using System.Reflection;
using System.Reflection.Emit;
using Xians.Lib.Temporal.Workflows;

namespace Xians.Lib.Agents.Workflows;

/// <summary>
/// Builder for creating dynamic workflow types that extend BuiltinWorkflow at runtime.
/// Uses Reflection.Emit to generate IL code for workflow classes with custom [Workflow] attributes.
/// </summary>
internal static class DynamicWorkflowTypeBuilder
{
    /// <summary>
    /// Prefix used for dynamic assembly names. Used to identify built-in workflow types.
    /// </summary>
    public const string DynamicAssemblyPrefix = "DynamicWorkflows_";
    
    // Cache for dynamically created workflow types to avoid recreating the same type multiple times
    private static readonly Dictionary<string, Type> _typeCache = new();
    private static readonly object _cacheLock = new();

    /// <summary>
    /// Creates or retrieves a cached dynamic type that extends BuiltinWorkflow with a runtime-specified [Workflow] attribute.
    /// </summary>
    /// <param name="workflowTypeName">The workflow type name in the format "AgentName:WorkflowName"</param>
    /// <returns>A dynamically created type that extends BuiltinWorkflow</returns>
    /// <exception cref="InvalidOperationException">Thrown when type creation fails</exception>
    public static Type GetOrCreateType(string workflowTypeName)
    {
        lock (_cacheLock)
        {
            if (_typeCache.TryGetValue(workflowTypeName, out var cachedType))
            {
                return cachedType;
            }

            var createdType = CreateType(workflowTypeName);
            _typeCache[workflowTypeName] = createdType;
            return createdType;
        }
    }

    /// <summary>
    /// Creates a new dynamic type that extends BuiltinWorkflow.
    /// </summary>
    private static Type CreateType(string workflowTypeName)
    {
        var assemblyBuilder = CreateAssembly();
        var moduleBuilder = assemblyBuilder.DefineDynamicModule("DynamicWorkflowModule");
        var typeBuilder = DefineType(moduleBuilder, workflowTypeName);

        ApplyWorkflowAttribute(typeBuilder, workflowTypeName);
        DefineConstructor(typeBuilder);
        DefineRunAsyncMethod(typeBuilder);

        var createdType = typeBuilder.CreateType();
        if (createdType == null)
        {
            throw new InvalidOperationException($"Failed to create dynamic workflow type for '{workflowTypeName}'");
        }

        return createdType;
    }

    /// <summary>
    /// Creates a dynamic assembly for the workflow type.
    /// </summary>
    private static AssemblyBuilder CreateAssembly()
    {
        var assemblyName = new AssemblyName($"{DynamicAssemblyPrefix}{Guid.NewGuid():N}");
        return AssemblyBuilder.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run);
    }

    /// <summary>
    /// Defines the type that extends BuiltinWorkflow.
    /// </summary>
    private static TypeBuilder DefineType(ModuleBuilder moduleBuilder, string workflowTypeName)
    {
        var sanitizedName = SanitizeTypeName(workflowTypeName);
        var typeName = $"DynamicBuiltInWorkflow_{sanitizedName}_{Guid.NewGuid():N}";

        return moduleBuilder.DefineType(
            typeName,
            TypeAttributes.Public | TypeAttributes.Class,
            typeof(BuiltinWorkflow));
    }

    /// <summary>
    /// Sanitizes the workflow type name for use as a class name.
    /// </summary>
    private static string SanitizeTypeName(string workflowTypeName)
    {
        return workflowTypeName
            .Replace(":", "_")
            .Replace(" ", "_")
            .Replace("-", "_");
    }

    /// <summary>
    /// Applies the [Workflow] attribute to the type with the specified workflow name.
    /// </summary>
    private static void ApplyWorkflowAttribute(TypeBuilder typeBuilder, string workflowTypeName)
    {
        var workflowAttributeConstructor = typeof(Temporalio.Workflows.WorkflowAttribute)
            .GetConstructor(new[] { typeof(string) });

        if (workflowAttributeConstructor == null)
        {
            throw new InvalidOperationException("WorkflowAttribute constructor not found");
        }

        var attributeBuilder = new CustomAttributeBuilder(
            workflowAttributeConstructor,
            new object[] { workflowTypeName });

        typeBuilder.SetCustomAttribute(attributeBuilder);
    }

    /// <summary>
    /// Defines a parameterless constructor that calls the base constructor.
    /// </summary>
    private static void DefineConstructor(TypeBuilder typeBuilder)
    {
        var constructorBuilder = typeBuilder.DefineConstructor(
            MethodAttributes.Public,
            CallingConventions.Standard,
            Type.EmptyTypes);

        var baseConstructor = typeof(BuiltinWorkflow).GetConstructor(Type.EmptyTypes);
        if (baseConstructor == null)
        {
            throw new InvalidOperationException("BuiltinWorkflow parameterless constructor not found");
        }

        var il = constructorBuilder.GetILGenerator();
        il.Emit(OpCodes.Ldarg_0);                  // Load 'this'
        il.Emit(OpCodes.Call, baseConstructor);    // Call base constructor
        il.Emit(OpCodes.Ret);                      // Return
    }

    /// <summary>
    /// Defines the RunAsync method override with [WorkflowRun] attribute.
    /// </summary>
    private static void DefineRunAsyncMethod(TypeBuilder typeBuilder)
    {
        var baseRunAsyncMethod = GetBaseRunAsyncMethod();
        var methodBuilder = DefineMethodOverride(typeBuilder, baseRunAsyncMethod);

        ApplyWorkflowRunAttribute(methodBuilder);
        EmitRunAsyncMethodBody(methodBuilder, baseRunAsyncMethod);
    }

    /// <summary>
    /// Gets the base RunAsync method from BuiltinWorkflow.
    /// </summary>
    private static MethodInfo GetBaseRunAsyncMethod()
    {
        var method = typeof(BuiltinWorkflow).GetMethod(
            "RunAsync",
            BindingFlags.Public | BindingFlags.Instance,
            null,
            Type.EmptyTypes,
            null);

        if (method == null)
        {
            throw new InvalidOperationException("RunAsync method not found on BuiltinWorkflow");
        }

        return method;
    }

    /// <summary>
    /// Defines the method override for RunAsync.
    /// </summary>
    private static MethodBuilder DefineMethodOverride(TypeBuilder typeBuilder, MethodInfo baseMethod)
    {
        return typeBuilder.DefineMethod(
            "RunAsync",
            MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.HideBySig | MethodAttributes.ReuseSlot,
            typeof(Task),
            Type.EmptyTypes);
    }

    /// <summary>
    /// Applies the [WorkflowRun] attribute to the method.
    /// </summary>
    private static void ApplyWorkflowRunAttribute(MethodBuilder methodBuilder)
    {
        var workflowRunAttributeConstructor = typeof(Temporalio.Workflows.WorkflowRunAttribute)
            .GetConstructor(Type.EmptyTypes);

        if (workflowRunAttributeConstructor != null)
        {
            var attributeBuilder = new CustomAttributeBuilder(
                workflowRunAttributeConstructor,
                Array.Empty<object>());
            methodBuilder.SetCustomAttribute(attributeBuilder);
        }
    }

    /// <summary>
    /// Emits the IL code for the RunAsync method body that calls base.RunAsync().
    /// </summary>
    private static void EmitRunAsyncMethodBody(MethodBuilder methodBuilder, MethodInfo baseMethod)
    {
        var il = methodBuilder.GetILGenerator();
        il.Emit(OpCodes.Ldarg_0);           // Load 'this'
        il.Emit(OpCodes.Call, baseMethod);  // Call base.RunAsync()
        il.Emit(OpCodes.Ret);               // Return the Task
    }

    /// <summary>
    /// Creates or retrieves a cached dynamic type that extends TaskWorkflow with a runtime-specified [Workflow] attribute.
    /// </summary>
    /// <param name="workflowTypeName">The workflow type name in the format "AgentName:Task Workflow"</param>
    /// <returns>A dynamically created type that extends TaskWorkflow</returns>
    /// <exception cref="InvalidOperationException">Thrown when type creation fails</exception>
    public static Type GetOrCreateTaskWorkflowType(string workflowTypeName)
    {
        lock (_cacheLock)
        {
            if (_typeCache.TryGetValue(workflowTypeName, out var cachedType))
            {
                return cachedType;
            }

            var createdType = CreateTaskWorkflowType(workflowTypeName);
            _typeCache[workflowTypeName] = createdType;
            return createdType;
        }
    }

    /// <summary>
    /// Creates a new dynamic type that extends TaskWorkflow.
    /// </summary>
    private static Type CreateTaskWorkflowType(string workflowTypeName)
    {
        var assemblyBuilder = CreateAssembly();
        var moduleBuilder = assemblyBuilder.DefineDynamicModule("DynamicTaskWorkflowModule");
        var typeBuilder = DefineTaskWorkflowType(moduleBuilder, workflowTypeName);

        ApplyWorkflowAttribute(typeBuilder, workflowTypeName);
        DefineTaskWorkflowConstructor(typeBuilder);
        DefineTaskWorkflowRunAsyncMethod(typeBuilder);

        var createdType = typeBuilder.CreateType();
        if (createdType == null)
        {
            throw new InvalidOperationException($"Failed to create dynamic task workflow type for '{workflowTypeName}'");
        }

        return createdType;
    }

    /// <summary>
    /// Defines the type that extends TaskWorkflow.
    /// </summary>
    private static TypeBuilder DefineTaskWorkflowType(ModuleBuilder moduleBuilder, string workflowTypeName)
    {
        var sanitizedName = SanitizeTypeName(workflowTypeName);
        var typeName = $"DynamicTaskWorkflow_{sanitizedName}_{Guid.NewGuid():N}";

        return moduleBuilder.DefineType(
            typeName,
            TypeAttributes.Public | TypeAttributes.Class,
            typeof(Temporal.Workflows.TaskWorkflow));
    }

    /// <summary>
    /// Defines a parameterless constructor that calls the base constructor for TaskWorkflow.
    /// </summary>
    private static void DefineTaskWorkflowConstructor(TypeBuilder typeBuilder)
    {
        var constructorBuilder = typeBuilder.DefineConstructor(
            MethodAttributes.Public,
            CallingConventions.Standard,
            Type.EmptyTypes);

        var baseConstructor = typeof(Temporal.Workflows.TaskWorkflow).GetConstructor(Type.EmptyTypes);
        if (baseConstructor == null)
        {
            throw new InvalidOperationException("TaskWorkflow parameterless constructor not found");
        }

        var il = constructorBuilder.GetILGenerator();
        il.Emit(OpCodes.Ldarg_0);                  // Load 'this'
        il.Emit(OpCodes.Call, baseConstructor);    // Call base constructor
        il.Emit(OpCodes.Ret);                      // Return
    }

    /// <summary>
    /// Defines the RunAsync method override with [WorkflowRun] attribute for TaskWorkflow.
    /// </summary>
    private static void DefineTaskWorkflowRunAsyncMethod(TypeBuilder typeBuilder)
    {
        var requestType = typeof(Temporal.Workflows.TaskWorkflow).Assembly
            .GetType("Xians.Lib.Agents.Tasks.Models.TaskWorkflowRequest");
        var resultType = typeof(Temporal.Workflows.TaskWorkflow).Assembly
            .GetType("Xians.Lib.Agents.Tasks.Models.TaskWorkflowResult");

        if (requestType == null || resultType == null)
        {
            throw new InvalidOperationException("TaskWorkflowRequest or TaskWorkflowResult type not found");
        }

        var baseRunAsyncMethod = typeof(Temporal.Workflows.TaskWorkflow).GetMethod(
            "RunAsync",
            BindingFlags.Public | BindingFlags.Instance,
            null,
            new[] { requestType },
            null);

        if (baseRunAsyncMethod == null)
        {
            throw new InvalidOperationException("RunAsync method not found on TaskWorkflow");
        }

        var methodBuilder = typeBuilder.DefineMethod(
            "RunAsync",
            MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.HideBySig | MethodAttributes.ReuseSlot,
            typeof(Task<>).MakeGenericType(resultType),
            new[] { requestType });

        // Define parameter
        methodBuilder.DefineParameter(1, ParameterAttributes.None, "request");

        // Apply [WorkflowRun] attribute to the method
        ApplyWorkflowRunAttribute(methodBuilder);

        // Emit IL code to call base.RunAsync(request)
        var il = methodBuilder.GetILGenerator();
        il.Emit(OpCodes.Ldarg_0);                      // Load 'this'
        il.Emit(OpCodes.Ldarg_1);                      // Load 'request' parameter
        il.Emit(OpCodes.Call, baseRunAsyncMethod);     // Call base.RunAsync(request)
        il.Emit(OpCodes.Ret);                          // Return the Task<TaskWorkflowResult>
    }

    /// <summary>
    /// Clears the type cache. Intended for testing purposes only.
    /// </summary>
    internal static void ClearCacheForTests()
    {
        lock (_cacheLock)
        {
            _typeCache.Clear();
        }
    }
}
