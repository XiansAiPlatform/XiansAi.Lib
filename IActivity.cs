using Temporalio.Activities;

namespace Flowmaxer.Common
{
    public interface IActivity
    {
        Task<string> ExecuteAsync(string input);
    }
}