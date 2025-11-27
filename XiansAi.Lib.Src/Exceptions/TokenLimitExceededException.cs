namespace XiansAi.Exceptions;

public class TokenLimitExceededException : Exception
{
    public TokenLimitExceededException(string message) : base(message)
    {
    }
}
