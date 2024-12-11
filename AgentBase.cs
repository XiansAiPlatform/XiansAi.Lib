using System.Threading.Tasks;

namespace Flowmaxer.Common
{
    /// <summary>
    /// Base class for all agents. Agents are activities that can be invoked by workflows.
    /// They should be lightweight, stateless, and deterministic if possible.
    /// </summary>
    public abstract class AgentBase
    {
        /// <summary>
        /// Each agent should implement its core logic by overriding this method.
        /// </summary>
        /// <param name="input">Input data for the agent.</param>
        /// <returns>A result string or a JSON serialized result object.</returns>
        public abstract Task<string> ExecuteAsync(string input);
    }
}