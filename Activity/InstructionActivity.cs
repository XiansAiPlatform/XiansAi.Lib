using System.Reflection;
using Microsoft.Extensions.Logging;
using XiansAi.Server;

namespace XiansAi.Activity;

/// <summary>
/// Base class for activities that require instruction loading.
/// </summary>
public abstract class InstructionActivity: AbstractActivity
{
    private readonly ILogger<InstructionActivity> _logger;

    protected InstructionActivity(): base()
    {
        _logger = Globals.LogFactory?.CreateLogger<InstructionActivity>() 
            ?? throw new InvalidOperationException($"[{GetType().Name}] LogFactory not initialized");
    }

    /// <summary>
    /// Retrieves instructions defined in the InstructionsAttribute of the implementing class.
    /// </summary>
    /// <returns>Array of instruction names</returns>
    /// <exception cref="InvalidOperationException">Thrown when instructions are missing or empty</exception>
    protected string[] GetDependingInstructions()
    {
        Console.WriteLine($"Getting instructions for {CurrentActivityMethod?.Name}");
        var methodInfo = CurrentActivityMethod;
        if (methodInfo == null)
        {
            _logger.LogError($"[{GetType().Name}] CurrentActivityMethod is null");
            throw new InvalidOperationException($"[{GetType().Name}] CurrentActivityMethod is null");
        }
        // Attempt to find the InstructionsAttribute on the method
        var instructionsAttr = methodInfo.GetCustomAttribute<InstructionsAttribute>();
        if (instructionsAttr?.Instructions != null && instructionsAttr.Instructions.Length > 0)
        {
            return instructionsAttr.Instructions;
        }

        // If not, attempt to find the InstructionsAttribute on the interface declarations
        var interfaceMethods = this.GetType().GetInterfaces()
            .SelectMany(interfaceType => interfaceType.GetMethods())
            .Where(m => m.Name == methodInfo.Name && m.GetParameters().Length == methodInfo.GetParameters().Length)
            .ToList();

        foreach (var interfaceMethod in interfaceMethods)
        {
            instructionsAttr = interfaceMethod.GetCustomAttribute<InstructionsAttribute>();
            if (instructionsAttr?.Instructions != null && instructionsAttr.Instructions.Length > 0)
            {
                return instructionsAttr.Instructions;
            }
        }

        _logger.LogError($"[{GetType().Name}.{methodInfo.Name}] Instructions attribute is missing or empty on interface methods");
        throw new InvalidOperationException($"[{GetType().Name}.{methodInfo.Name}] Instructions attribute is missing or empty on interface methods");
    }

    /// <summary>
    /// Loads an instruction by its index from the depending instructions.
    /// </summary>
    /// <param name="index">1-based index of the instruction to load</param>
    /// <returns>The content of the loaded instruction</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when index is less than 1</exception>
    /// <exception cref="InvalidOperationException">Thrown when instruction loading fails</exception>
    protected async Task<string?> GetInstructionAsync(int index = 1)
    {
        if (index < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(index), $"[{GetType().Name}] Index must be greater than 0");
        }

        try
        {
            var instructions = GetDependingInstructions();
            if (index > instructions.Length)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(index), 
                    $"[{GetType().Name}] Index {index} exceeds instruction count of {instructions.Length}");
            }

            var instructionName = instructions[index - 1];
            return await GetInstruction(instructionName);
        }
        catch (Exception ex) when (ex is not ArgumentOutOfRangeException)
        {
            _logger.LogError(ex, $"[{GetType().Name}] Failed to load instruction");
            throw new InvalidOperationException($"[{GetType().Name}] Failed to load instruction", ex);
        }
    }

    public class TempInstructionFile : IDisposable
    {
        private readonly string _filePath;
        private readonly ILogger _logger;
        private bool _disposed;

        public string FilePath => _filePath;

        public TempInstructionFile(string filePath, ILogger logger)
        {
            _filePath = filePath;
            _logger = logger;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                try
                {
                    if (File.Exists(_filePath))
                    {
                        File.Delete(_filePath);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Failed to delete temporary instruction file: {_filePath}");
                }
                _disposed = true;
            }
        }
    }

    public async Task<TempInstructionFile> GetInstructionAsTempFile(int index = 1)
    {
        return await GetInstructionAsTempFile(new Dictionary<string, string>(), index);
    }

    /// <summary>
    /// Loads an instruction by its index from the depending instructions and returns the path to a temporary file containing the instruction.
    /// </summary>
    /// <param name="index">1-based index of the instruction to load</param>
    /// <param name="parameters">Dictionary of parameters to replace in the instruction</param>
    /// <returns>The path to the temporary file containing the instruction</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when index is less than 1</exception>
    /// <exception cref="InvalidOperationException">Thrown when instruction loading fails</exception>
    public virtual async Task<TempInstructionFile> GetInstructionAsTempFile(IDictionary<string, string> parameters, int index = 1)
    {
        var instruction = await GetInstructionAsync(index);
        if (instruction == null) throw new InvalidOperationException($"[{GetType().Name}] Failed to load instruction");

        foreach (var parameter in parameters)
        {
            instruction = instruction.Replace($"{{{parameter.Key}}}", parameter.Value);
        }

        var tempFile = Path.GetTempFileName();
        File.WriteAllText(tempFile, instruction);
        return new TempInstructionFile(tempFile, _logger);
    }

    /// <summary>
    /// Loads an instruction by its name.
    /// </summary>
    /// <param name="instructionName">Name of the instruction to load</param>
    /// <returns>The content of the loaded instruction</returns>
    /// <exception cref="InvalidOperationException">Thrown when instruction loading fails</exception>
    private async Task<string?> GetInstruction(string instructionName)
    {
        try
        {
            var instruction = await new InstructionLoader().Load(instructionName);
            
            if (instruction == null)
            {
                _logger.LogError($"[{GetType().Name}] Failed to load instruction: {instructionName}");
                return null;
            }

            var currentActivity = GetCurrentActivity();
            if (currentActivity != null && instruction.Id != null)
            {
                currentActivity.InstructionIds.Add(instruction.Id);
                _logger.LogDebug($"[{GetType().Name}] Associated instruction {instruction.Id} with activity");
            }
            else if (currentActivity != null)
            {
                _logger.LogWarning($"[{GetType().Name}] Local instruction file, skipping ID association");
            }

            return instruction.Content;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"[{GetType().Name}] Failed to load instruction");
            throw new InvalidOperationException($"[{GetType().Name}] Failed to load instruction", ex);
        }
    }
}
