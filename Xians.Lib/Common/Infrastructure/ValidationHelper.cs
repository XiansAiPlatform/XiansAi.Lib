namespace Xians.Lib.Common.Infrastructure;

/// <summary>
/// Provides common validation methods for parameter checking across the SDK.
/// </summary>
internal static class ValidationHelper
{
    /// <summary>
    /// Validates that a required string parameter is not null or whitespace.
    /// </summary>
    /// <param name="value">The value to validate.</param>
    /// <param name="paramName">The parameter name.</param>
    /// <exception cref="ArgumentException">Thrown when the value is null or whitespace.</exception>
    public static void ValidateRequired(string? value, string paramName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{paramName} cannot be null or empty", paramName);
        }
    }

    /// <summary>
    /// Validates that a string parameter does not exceed the maximum length.
    /// </summary>
    /// <param name="value">The value to validate.</param>
    /// <param name="paramName">The parameter name.</param>
    /// <param name="maxLength">The maximum allowed length.</param>
    /// <exception cref="ArgumentException">Thrown when the value exceeds the maximum length.</exception>
    public static void ValidateMaxLength(string? value, string paramName, int maxLength)
    {
        if (value != null && value.Length > maxLength)
        {
            throw new ArgumentException($"{paramName} exceeds maximum length of {maxLength} characters", paramName);
        }
    }

    /// <summary>
    /// Validates a required string parameter with maximum length constraint.
    /// </summary>
    /// <param name="value">The value to validate.</param>
    /// <param name="paramName">The parameter name.</param>
    /// <param name="maxLength">The maximum allowed length.</param>
    /// <exception cref="ArgumentException">Thrown when the value is null, empty, or exceeds the maximum length.</exception>
    public static void ValidateRequiredWithMaxLength(string? value, string paramName, int maxLength)
    {
        ValidateRequired(value, paramName);
        ValidateMaxLength(value, paramName, maxLength);
    }

    /// <summary>
    /// Validates that a required object parameter is not null.
    /// </summary>
    /// <param name="value">The value to validate.</param>
    /// <param name="paramName">The parameter name.</param>
    /// <exception cref="ArgumentNullException">Thrown when the value is null.</exception>
    public static void ValidateNotNull(object? value, string paramName)
    {
        if (value == null)
        {
            throw new ArgumentNullException(paramName);
        }
    }

    /// <summary>
    /// Validates that a TimeSpan is greater than zero.
    /// </summary>
    /// <param name="value">The value to validate.</param>
    /// <param name="paramName">The parameter name.</param>
    /// <exception cref="ArgumentException">Thrown when the value is zero or negative.</exception>
    public static void ValidatePositiveTimeSpan(TimeSpan value, string paramName)
    {
        if (value <= TimeSpan.Zero)
        {
            throw new ArgumentException($"{paramName} must be greater than zero", paramName);
        }
    }

    /// <summary>
    /// Validates that a numeric value is greater than zero.
    /// </summary>
    /// <param name="value">The value to validate.</param>
    /// <param name="paramName">The parameter name.</param>
    /// <exception cref="ArgumentException">Thrown when the value is zero or negative.</exception>
    public static void ValidatePositive(int value, string paramName)
    {
        if (value <= 0)
        {
            throw new ArgumentException($"{paramName} must be greater than zero", paramName);
        }
    }

    /// <summary>
    /// Validates that a numeric value is non-negative (>= 0).
    /// </summary>
    /// <param name="value">The value to validate.</param>
    /// <param name="paramName">The parameter name.</param>
    /// <exception cref="ArgumentException">Thrown when the value is negative.</exception>
    public static void ValidateNonNegative(int value, string paramName)
    {
        if (value < 0)
        {
            throw new ArgumentException($"{paramName} must be non-negative", paramName);
        }
    }

    /// <summary>
    /// Validates that a numeric value is within a specified range (inclusive).
    /// </summary>
    /// <param name="value">The value to validate.</param>
    /// <param name="paramName">The parameter name.</param>
    /// <param name="min">The minimum allowed value (inclusive).</param>
    /// <param name="max">The maximum allowed value (inclusive).</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the value is outside the range.</exception>
    public static void ValidateRange(int value, string paramName, int min, int max)
    {
        if (value < min || value > max)
        {
            throw new ArgumentOutOfRangeException(paramName, value, 
                $"{paramName} must be between {min} and {max} (inclusive)");
        }
    }

    /// <summary>
    /// Validates that a collection is not null or empty.
    /// </summary>
    /// <param name="collection">The collection to validate.</param>
    /// <param name="paramName">The parameter name.</param>
    /// <exception cref="ArgumentException">Thrown when the collection is null or empty.</exception>
    public static void ValidateNotNullOrEmpty<T>(IEnumerable<T>? collection, string paramName)
    {
        if (collection == null || !collection.Any())
        {
            throw new ArgumentException($"{paramName} cannot be null or empty", paramName);
        }
    }
}

