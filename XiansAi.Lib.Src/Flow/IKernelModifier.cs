using Microsoft.SemanticKernel;

namespace XiansAi.Flow;

public interface IKernelModifier
{
    Task<Kernel> ModifyKernelAsync(Kernel kernel);
}