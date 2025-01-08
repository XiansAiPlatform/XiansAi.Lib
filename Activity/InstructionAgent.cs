using System.Reflection;
using Microsoft.Extensions.Logging;
using XiansAi.Server;

namespace XiansAi.Activity;

public abstract class InstructionActivity: ActivityBase
{
    private readonly ILogger<InstructionActivity> _logger;
    private readonly InstructionLoader _instructionLoader;

    protected InstructionActivity(): base()
    {
        _logger = Globals.LogFactory.CreateLogger<InstructionActivity>();
        _instructionLoader = new InstructionLoader();
    }

    protected string[] GetDependingInstructions()
    {
        var instructionsAttr = GetType().GetCustomAttribute<InstructionsAttribute>();

        if (instructionsAttr == null) {
            return [];
        }

        var instructions = instructionsAttr.Instructions;

        if (instructions.Length == 0) {
            _logger.LogError("Instructions attribute can not be empty");
            throw new InvalidOperationException("Instructions attribute can not be empty");
        }
        return instructions;
        
    }

    protected async Task<string?> LoadInstruction(int index = 1)
    {
        var instructions = GetDependingInstructions();
        if (instructions == null || index > instructions.Length) {
            _logger.LogError($"Index {index} is out of range for instructions.");
            throw new InvalidOperationException($"Index {index} is out of range for instructions");
        }
        var instructionName = instructions[index - 1];
        // Load from server
        var instruction = await _instructionLoader.Load(instructionName);

        var currentActivity = GetCurrentActivity();
        if (currentActivity != null) {
            // If instruction loaded from local file, id might be null
            if (instruction?.Id != null) {
                currentActivity.InstructionIds.Add(instruction.Id);
            } else {
                _logger.LogWarning("Instruction is a local file, skipping instruction id association to activity");
            }
        }
        return instruction?.Content;
    }
}
