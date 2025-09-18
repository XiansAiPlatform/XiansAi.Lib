using Microsoft.SemanticKernel;
using XiansAi.Messaging;

namespace XiansAi.Flow;

public interface IKernelModifier
{
    Task<Kernel> ModifyKernelAsync(Kernel kernel, MessageThread messageThread);
}