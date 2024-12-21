using System;
using System.IO;

public class ActivityProfile
{
    private readonly string mdFilePath;
    private readonly string content;

    public ActivityProfile(string profilePath)
    {
        this.mdFilePath = profilePath ?? throw new ArgumentNullException(nameof(profilePath));
        this.content = GetProfile();
    }

    private string GetProfile()
    {
        try
        {
            var projectBasePath = AppDomain.CurrentDomain.BaseDirectory;
            var fullPath = Path.Combine(projectBasePath, mdFilePath);
            return File.ReadAllText(fullPath);
        }
        catch (FileNotFoundException)
        {
            throw new FileNotFoundException($"Profile file not found: {mdFilePath}");
        }
        catch (Exception ex)
        {
            throw new Exception($"Error loading profile file: {ex.Message}", ex);
        }
    }

    public string ReadAsTempFile(Dictionary<string, string>? parameters)
    {
        var tempFile = Path.GetTempFileName();
        File.WriteAllText(tempFile, ReadAsString(parameters));
        return tempFile;
    }

    public string ReadAsString(Dictionary<string, string>? parameters)
    {
        if (parameters == null)
            return content;

        string result = content;
        foreach (var param in parameters)
        {
            string placeholder = $"{{{{{param.Key}}}}}";
            result = result.Replace(placeholder, param.Value);
        }
        return result;
    }
}