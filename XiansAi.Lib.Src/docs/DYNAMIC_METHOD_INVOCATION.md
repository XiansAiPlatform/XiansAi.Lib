# Dynamic Method Invocation Guide

## Overview

The `DynamicMethodInvoker` provides robust dynamic method invocation with automatic JSON parameter conversion and method overload resolution.

## Method Name Matching

### Case Insensitive Matching
- Method names are matched case-insensitively
- `"processData"`, `"ProcessData"`, `"PROCESSDATA"` all match the same method

### Method Resolution Order
1. **Exact parameter count match** - Methods with exact parameter count are tried first
2. **Optional parameter handling** - Methods with optional parameters are considered
3. **First match wins** - The first compatible method signature is selected

## Parameter Matching

### JSON Parameter Formats

#### Single Parameter
```json
"Hello World"
```
```json
42
```
```json
{"name": "John", "age": 30}
```

#### Multiple Parameters (Array)
```json
["param1", 42, true]
```
```json
["Hello", {"id": 123}, [1, 2, 3]]
```

#### Complex Objects
```json
[{
  "userId": 123,
  "action": "update",
  "data": {
    "field1": "value1",
    "field2": "value2"
  }
}]
```

### Type Conversion Rules

| JSON Type | .NET Types Supported |
|-----------|---------------------|
| `string` | `string`, `char`, `Guid`, `DateTime`, Enums |
| `number` | `int`, `long`, `double`, `decimal`, `float` |
| `boolean` | `bool` |
| `null` | Nullable types, reference types |
| `object` | Complex objects (POCO), `Dictionary<>` |
| `array` | Arrays, `List<>`, `IEnumerable<>` |

### Method Signature Matching Examples

#### Example Class
```csharp
public class UserProcessor
{
    public UserProcessor(MessageThread messageThread) { }

    // Method overloads
    public string ProcessUser(string name) { }
    public string ProcessUser(string name, int age) { }
    public string ProcessUser(string name, int age, bool isActive = true) { }
    public Task<string> ProcessUserAsync(UserData userData) { }
}
```

#### Parameter Matching Scenarios

**Scenario 1: Single Parameter**
```json
"John Doe"
```
- Matches: `ProcessUser(string name)`

**Scenario 2: Two Parameters**
```json
["John Doe", 30]
```
- Matches: `ProcessUser(string name, int age)`

**Scenario 3: Optional Parameter**
```json
["John Doe", 30]
```
- Matches: `ProcessUser(string name, int age, bool isActive = true)`
- `isActive` gets default value `true`

**Scenario 4: Complex Object**
```json
[{"name": "John", "age": 30, "email": "john@example.com"}]
```
- Matches: `ProcessUserAsync(UserData userData)`
- JSON is deserialized to `UserData` object

## Error Handling

### Exception Types

| Exception | Description |
|-----------|-------------|
| `MethodNotFoundException` | Method name not found in target type |
| `MethodMatchException` | No method overload matches provided parameters |
| `ParameterParsingException` | Invalid JSON parameter format |
| `TypeConversionException` | Cannot convert JSON value to target type |
| `InstanceCreationException` | Cannot create instance of target type |

### Error Examples

**Invalid Method Name**
```
MethodNotFoundException: Method 'InvalidMethod' not found in type 'UserProcessor'
```

**Parameter Count Mismatch**
```
MethodMatchException: No method overload found that matches 3 parameter(s). 
Available methods: ProcessUser(String), ProcessUser(String, Int32)
```

**Type Conversion Error**
```
TypeConversionException: Failed to convert JSON value to Int32: 
The JSON value could not be converted to System.Int32
```

## Best Practices

### 1. Parameter Design
- Use consistent parameter ordering across method overloads
- Provide sensible default values for optional parameters
- Consider using complex objects for methods with many parameters

### 2. Error Handling
```csharp
try
{
    var result = await DynamicMethodInvoker.InvokeMethodAsync(
        typeof(UserProcessor), messageThread, "ProcessUser", parameters);
}
catch (MethodNotFoundException ex)
{
    // Handle method not found
}
catch (ParameterParsingException ex)
{
    // Handle invalid JSON
}
catch (TypeConversionException ex)
{
    // Handle type conversion issues
}
```

### 3. JSON Parameter Examples

**Good Examples:**
```json
// Simple parameters
["John", 30, true]

// Complex object
[{"user": {"id": 1, "name": "John"}, "options": {"validate": true}}]

// Mixed types
["operation", 123, {"metadata": {"source": "api"}}]
```

**Avoid:**
```json
// Ambiguous types (use explicit types in method signatures)
[null, undefined, ""]

// Deeply nested complex structures (consider flattening)
[{"level1": {"level2": {"level3": {"value": "deep"}}}}]
```

## Constructor Requirements

Target classes must have one of:
1. Constructor accepting `MessageThread` parameter
2. Constructor accepting `IMessageThread` parameter  
3. Parameterless constructor

## Async Method Support

- Methods returning `Task` are automatically awaited
- Methods returning `Task<T>` return the unwrapped result
- Synchronous methods work without modification

## Performance Considerations

- Method reflection is performed on each invocation
- Consider caching `MethodInfo` objects for frequently called methods
- JSON parsing overhead for complex objects
- Type conversion overhead for primitive types