using System.Diagnostics;
using CompanyBrain.Application.Results;
using CompanyBrain.Models;
using FluentResults;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace CompanyBrain.Services;

/// <summary>
/// Service for cloning and managing git repositories as resource templates.
/// </summary>
public sealed class GitRepositoryService
{
    private readonly string rootPath;
    private readonly ILogger<GitRepositoryService> logger;

    public GitRepositoryService(string rootPath, ILogger<GitRepositoryService>? logger = null)
    {
        this.rootPath = rootPath;
        this.logger = logger ?? NullLogger<GitRepositoryService>.Instance;
    }

    public async Task<Result<ClonedRepositoryResult>> CloneRepositoryAsync(
        string repositoryUrl,
        string templateName,
        string? branch,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryUrl);
        ArgumentException.ThrowIfNullOrWhiteSpace(templateName);

        var templatesPath = Path.Combine(rootPath, "templates");
        Directory.CreateDirectory(templatesPath);

        var sanitizedName = SanitizeTemplateName(templateName);
        var targetPath = Path.Combine(templatesPath, sanitizedName);
        var alreadyExisted = Directory.Exists(targetPath);

        if (alreadyExisted)
        {
            logger.LogInformation("Template directory already exists at '{TargetPath}'. Pulling latest changes.", targetPath);
            var pullResult = await PullRepositoryAsync(targetPath, cancellationToken);
            if (pullResult.IsFailed)
            {
                return pullResult.ToResult<ClonedRepositoryResult>();
            }
        }
        else
        {
            logger.LogInformation("Cloning repository '{RepositoryUrl}' to '{TargetPath}'.", repositoryUrl, targetPath);
            var cloneResult = await ExecuteGitCloneAsync(repositoryUrl, targetPath, branch, cancellationToken);
            if (cloneResult.IsFailed)
            {
                return cloneResult.ToResult<ClonedRepositoryResult>();
            }
        }

        var files = GetRepositoryFiles(targetPath);
        var actualBranch = branch ?? "main";

        return Result.Ok(new ClonedRepositoryResult(
            sanitizedName,
            repositoryUrl,
            targetPath,
            actualBranch,
            files.Count,
            alreadyExisted));
    }

    public Task<IReadOnlyList<ResourceTemplate>> ListTemplatesAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var templatesPath = Path.Combine(rootPath, "templates");
        if (!Directory.Exists(templatesPath))
        {
            return Task.FromResult<IReadOnlyList<ResourceTemplate>>([]);
        }

        var templates = Directory.GetDirectories(templatesPath)
            .Select(dir =>
            {
                var name = Path.GetFileName(dir);
                var files = GetRepositoryFiles(dir);
                var repoUrl = GetRepositoryUrl(dir);
                var branch = GetCurrentBranch(dir);

                return new ResourceTemplate(
                    name,
                    repoUrl ?? "unknown",
                    dir,
                    branch ?? "unknown",
                    Directory.GetLastWriteTimeUtc(dir),
                    files);
            })
            .ToList();

        return Task.FromResult<IReadOnlyList<ResourceTemplate>>(templates);
    }

    public async Task<Result<string>> GetTemplateFileContentAsync(
        string templateName,
        string relativePath,
        CancellationToken cancellationToken)
    {
        var templatesPath = Path.Combine(rootPath, "templates");
        var templatePath = Path.Combine(templatesPath, SanitizeTemplateName(templateName));

        if (!Directory.Exists(templatePath))
        {
            return Result.Fail<string>(new NotFoundAppError($"Template not found: {templateName}"));
        }

        var filePath = Path.Combine(templatePath, relativePath);
        if (!filePath.StartsWith(templatePath, StringComparison.OrdinalIgnoreCase))
        {
            return Result.Fail<string>(new ValidationAppError("Invalid file path: path traversal not allowed."));
        }

        if (!File.Exists(filePath))
        {
            return Result.Fail<string>(new NotFoundAppError($"File not found in template: {relativePath}"));
        }

        var content = await File.ReadAllTextAsync(filePath, cancellationToken);
        return Result.Ok(content);
    }

    public Result DeleteTemplate(string templateName, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var templatesPath = Path.Combine(rootPath, "templates");
        var templatePath = Path.Combine(templatesPath, SanitizeTemplateName(templateName));

        if (!Directory.Exists(templatePath))
        {
            return Result.Fail(new NotFoundAppError($"Template not found: {templateName}"));
        }

        try
        {
            Directory.Delete(templatePath, recursive: true);
            logger.LogInformation("Deleted template '{TemplateName}' from '{TemplatePath}'.", templateName, templatePath);
            return Result.Ok();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to delete template '{TemplateName}'.", templateName);
            return Result.Fail(new ValidationAppError($"Failed to delete template: {ex.Message}"));
        }
    }

    private async Task<Result> ExecuteGitCloneAsync(
        string repositoryUrl,
        string targetPath,
        string? branch,
        CancellationToken cancellationToken)
    {
        var branchArgs = string.IsNullOrWhiteSpace(branch) ? "" : $"-b {branch}";
        var arguments = $"clone {branchArgs} --depth 1 \"{repositoryUrl}\" \"{targetPath}\"";

        return await ExecuteGitCommandAsync(arguments, null, cancellationToken);
    }

    private async Task<Result> PullRepositoryAsync(string repositoryPath, CancellationToken cancellationToken)
    {
        var resetResult = await ExecuteGitCommandAsync("reset --hard HEAD", repositoryPath, cancellationToken);
        if (resetResult.IsFailed)
        {
            return resetResult;
        }

        return await ExecuteGitCommandAsync("pull --ff-only", repositoryPath, cancellationToken);
    }

    private async Task<Result> ExecuteGitCommandAsync(
        string arguments,
        string? workingDirectory,
        CancellationToken cancellationToken)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            if (!string.IsNullOrEmpty(workingDirectory))
            {
                startInfo.WorkingDirectory = workingDirectory;
            }

            using var process = new Process { StartInfo = startInfo };
            process.Start();

            var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
            var error = await process.StandardError.ReadToEndAsync(cancellationToken);

            await process.WaitForExitAsync(cancellationToken);

            if (process.ExitCode != 0)
            {
                logger.LogWarning("Git command failed: {Error}", error);
                return Result.Fail(new UpstreamAppError($"Git command failed: {error}"));
            }

            logger.LogDebug("Git command output: {Output}", output);
            return Result.Ok();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to execute git command: {Arguments}", arguments);
            return Result.Fail(new UpstreamAppError($"Failed to execute git: {ex.Message}"));
        }
    }

    private static IReadOnlyList<string> GetRepositoryFiles(string repositoryPath)
    {
        if (!Directory.Exists(repositoryPath))
        {
            return [];
        }

        return Directory.GetFiles(repositoryPath, "*", SearchOption.AllDirectories)
            .Where(file => !file.Contains(Path.Combine(repositoryPath, ".git")))
            .Select(file => Path.GetRelativePath(repositoryPath, file))
            .OrderBy(file => file, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string? GetRepositoryUrl(string repositoryPath)
    {
        try
        {
            var configPath = Path.Combine(repositoryPath, ".git", "config");
            if (!File.Exists(configPath))
            {
                return null;
            }

            var lines = File.ReadAllLines(configPath);
            for (var i = 0; i < lines.Length; i++)
            {
                if (lines[i].Trim().StartsWith("url = ", StringComparison.OrdinalIgnoreCase))
                {
                    return lines[i].Trim()[6..].Trim();
                }
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    private static string? GetCurrentBranch(string repositoryPath)
    {
        try
        {
            var headPath = Path.Combine(repositoryPath, ".git", "HEAD");
            if (!File.Exists(headPath))
            {
                return null;
            }

            var content = File.ReadAllText(headPath).Trim();
            if (content.StartsWith("ref: refs/heads/", StringComparison.OrdinalIgnoreCase))
            {
                return content[16..];
            }

            return content[..7]; // Short commit hash
        }
        catch
        {
            return null;
        }
    }

    private static string SanitizeTemplateName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = new string(name.Where(c => !invalid.Contains(c)).ToArray());
        return string.IsNullOrWhiteSpace(sanitized) ? "template" : sanitized;
    }
}
