using System.ComponentModel;
using Xians.Lib.Agents.Messaging;

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
        // User message related functionality
        await _context.ReplyAsync($"The current date and time is: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        var now = DateTime.Now;
        return $"The current date and time is: {now:yyyy-MM-dd HH:mm:ss}";
    }

    [Description("Get the order data.")]
    public async Task<string> GetOrderData(int orderNumber)
    {
        await Task.CompletedTask;
        // Returning elaborated dummy info for demonstration
        return $"Order #{orderNumber}:\n" +
               $"- Customer: John Doe\n" +
               $"- Item: Widget Pro X100\n" +
               $"- Quantity: 3\n" +
               $"- Status: Shipped\n" +
               $"- Estimated Delivery: {DateTime.Today.AddDays(3):yyyy-MM-dd}\n" +
               $"- Total: $299.97";
    }
}