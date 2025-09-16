using Microsoft.SemanticKernel;

namespace Agentri.Flow;

public interface IKernelModifier
{
    Task<Kernel> ModifyKernelAsync(Kernel kernel);
}