using System.ComponentModel;
using Xians.Lib.Agents.Messaging;
using Xians.Lib.Agents.Core;

public class MafSubAgentTools
{
    private readonly UserMessageContext _context;

    public MafSubAgentTools(UserMessageContext context)
    {
        _context = context;
    }

    [Description("Get the current date and time.")]
    public async Task<string> GetCurrentDateTime()
    {
        await _context.ReplyAsync($"The current date and time is: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        var now = DateTime.Now;
        return $"The current date and time is: {now:yyyy-MM-dd HH:mm:ss}";
    }

    [Description("Get the target market description.")]
    public async Task<string> GetTargetMarketDescription()
    {
        var targetMarketDescription = await XiansContext.CurrentAgent.Knowledge.GetAsync("Market Description");
        return targetMarketDescription?.Content ?? "I couldn't find the target market description.";
    }
}
