using System.Reflection;
using Microsoft.Extensions.Logging;

public abstract class InstructionAgent: BaseAgent
{
    private readonly ILogger<InstructionAgent> _logger;
    private readonly string[]? _instructions;
    private readonly InstructionLoader? _instructionLoader;

    protected InstructionAgent(string[]? instructions = null): base()
    {
        _logger = Globals.LogFactory.CreateLogger<InstructionAgent>();

        var instructionsAttr = GetType().GetCustomAttribute<InstructionsAttribute>();
        _instructions = instructions ?? instructionsAttr?.Instructions;
        
        if (_instructions != null)
        {
            _instructionLoader = new InstructionLoader(_instructions);
        }
    }

    protected async Task<string> LoadInstruction(string instructionName)
    {
        if (_instructionLoader == null)
        {
            throw new InvalidOperationException("Instructions are not initialized");
        }
        var instruction = await _instructionLoader.LoadInstruction(instructionName);
        return instruction.Content;
    }

    protected async Task<string> LoadInstruction(int index = 0)
    {
        if (_instructionLoader == null)
        {
            throw new InvalidOperationException("Instructions are not initialized");
        }
        var instruction = await _instructionLoader.LoadInstruction(index);
        return instruction.Content;
    }
}
