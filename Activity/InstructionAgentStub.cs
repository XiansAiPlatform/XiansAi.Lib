using System.Reflection;
using Microsoft.Extensions.Logging;
using XiansAi.Server;

namespace XiansAi.Activity;

/// <summary>
/// Base class for activities that require instruction loading.
/// </summary>
public class InstructionActivity: BaseActivity
{
    private readonly ILogger<InstructionActivity> _logger;
    private readonly InstructionLoader _instructionLoader;

    protected InstructionActivity(): base()
    {
        _logger = Globals.LogFactory?.CreateLogger<InstructionActivity>() 
            ?? throw new InvalidOperationException($"[{GetType().Name}] LogFactory not initialized");
        _instructionLoader = new InstructionLoader();
    }

    /// <summary>
    /// Retrieves instructions defined in the InstructionsAttribute of the implementing class.
    /// </summary>
    /// <returns>Array of instruction names</returns>
    /// <exception cref="InvalidOperationException">Thrown when instructions are missing or empty</exception>
    protected string[] GetDependingInstructions()
    {
        Console.WriteLine($"Getting instructions for {CurrentActivityMethod?.Name}");
        var instructionsAttr = CurrentActivityMethod?.GetCustomAttribute<InstructionsAttribute>();
        Console.WriteLine($"Instructions attribute: {instructionsAttr}");
        if (instructionsAttr?.Instructions == null || instructionsAttr.Instructions.Length == 0) 
        {
            _logger.LogError($"[{GetType().Name}] Instructions attribute is missing or empty");
            throw new InvalidOperationException($"[{GetType().Name}] Instructions attribute is missing or empty");
        }

        return instructionsAttr.Instructions;
    }

    /// <summary>
    /// Loads an instruction by its index from the depending instructions.
    /// </summary>
    /// <param name="index">1-based index of the instruction to load</param>
    /// <returns>The content of the loaded instruction</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when index is less than 1</exception>
    /// <exception cref="InvalidOperationException">Thrown when instruction loading fails</exception>
    protected async Task<string?> GetInstruction(int index = 1)
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
            var instruction = await _instructionLoader.Load(instructionName);
            
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
