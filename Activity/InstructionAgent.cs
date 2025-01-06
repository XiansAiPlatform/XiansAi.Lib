using System.Reflection;
using Microsoft.Extensions.Logging;
using XiansAi.Server;

namespace XiansAi.Activity;

public abstract class InstructionActivity: ActivityBase
{
    private readonly ILogger<InstructionActivity> _logger;
    private readonly string[]? _instructions;
    private readonly InstructionLoader? _instructionLoader;

    protected InstructionActivity(): base()
    {
        _logger = Globals.LogFactory.CreateLogger<InstructionActivity>();

        var instructionsAttr = GetType().GetCustomAttribute<InstructionsAttribute>();

        if (instructionsAttr != null) {
            _instructions = instructionsAttr.Instructions;
            _instructionLoader = new InstructionLoader();
        }
    }

    protected async Task<string> LoadInstruction(int index = 1)
    {
        if (_instructions == null || index <= 0 || index > _instructions.Length) {
            _logger.LogError($"Index {index} is out of range for instructions.");
            throw new InvalidOperationException($"Index {index} is out of range for instructions");
        }
        var instructionName = _instructions[index - 1];

        if (_instructionLoader == null) {
            _logger.LogError("InstructionLoader is not initialized");
            throw new InvalidOperationException("InstructionLoader is not initialized. Did you forget to set the InstructionsAttribute?");
        }

        var instruction = await _instructionLoader.LoadInstruction(instructionName);

        _logger.LogInformation($"Loaded instruction {instructionName} from server");

        // If instruction loaded from local file, id might be null
        var currentActivity = GetCurrentActivity();
        if (currentActivity != null && instruction.Id != null) {
            currentActivity.InstructionIds.Add(instruction.Id);
        } else {
            _logger.LogWarning("No current activity found, or instruction id is null, skipping instruction id addition");
        }
        return instruction.Content;
    }
}
