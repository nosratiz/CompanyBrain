using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using Microsoft.Extensions.Logging;

namespace CompanyBrain.Dashboard.Scripting;

/// <summary>
/// Service for executing custom tool C# scripts in a sandboxed Roslyn environment.
/// Provides security constraints and timeout handling.
/// </summary>
public sealed partial class ScriptRunnerService(ILogger<ScriptRunnerService> logger)
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(5);
    
    /// <summary>
    /// Blocked namespace patterns for security.
    /// Scripts cannot access these namespaces.
    /// </summary>
    private static readonly string[] BlockedNamespaces =
    [
        "System.Diagnostics.Process",
        "System.Runtime.InteropServices",
        "System.Reflection.Emit",
        "System.Net.Sockets",
        "System.Net.Http",
        "Microsoft.Win32",
        "System.Security.Cryptography",
        "System.Threading.Thread",
        "System.AppDomain",
        "System.Environment.Exit",
        "System.Runtime.Loader"
    ];
    
    /// <summary>
    /// Blocked method patterns for additional security.
    /// </summary>
    private static readonly string[] BlockedPatterns =
    [
        @"Process\s*\.\s*(Start|Kill)",
        @"Environment\s*\.\s*(Exit|FailFast|SetEnvironmentVariable)",
        @"Assembly\s*\.\s*(Load|LoadFrom|LoadFile)",
        @"Activator\s*\.\s*CreateInstance",
        @"Type\s*\.\s*GetType\s*\(\s*[""']",
        @"Thread\s*\.\s*(Start|Abort|Sleep)",
        @"Task\s*\.\s*Run",
        @"Parallel\s*\.",
        @"Marshal\s*\.",
        @"GC\s*\.\s*Collect",
        @"AppDomain\s*\.",
        @"#r\s+",  // Block assembly references
        @"#load\s+"  // Block script loading
    ];
    
    /// <summary>
    /// Compiled regex for security validation.
    /// </summary>
    private static readonly Regex BlockedPatternRegex = new(
        string.Join("|", BlockedPatterns),
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    
    /// <summary>
    /// Safe imports available to scripts.
    /// </summary>
    private static readonly string[] SafeImports =
    [
        "System",
        "System.Collections.Generic",
        "System.Linq",
        "System.Text",
        "System.Text.Json",
        "System.Text.RegularExpressions",
        "System.IO"
    ];
    
    /// <summary>
    /// Safe assemblies that scripts can reference.
    /// </summary>
    private static readonly Assembly[] SafeAssemblies =
    [
        typeof(object).Assembly,                          // mscorlib/System.Private.CoreLib
        typeof(Enumerable).Assembly,                      // System.Linq
        typeof(System.Text.Json.JsonSerializer).Assembly, // System.Text.Json
        typeof(Regex).Assembly,                           // System.Text.RegularExpressions
        typeof(File).Assembly,                            // System.IO.FileSystem
        typeof(List<>).Assembly,                          // System.Collections
        typeof(Console).Assembly                          // System.Console
    ];
    
    /// <summary>
    /// Executes a C# script with the provided context.
    /// </summary>
    /// <param name="code">The C# script code to execute.</param>
    /// <param name="context">The execution context with arguments and configuration.</param>
    /// <param name="cancellationToken">Cancellation token for the overall operation.</param>
    /// <returns>The result of the script execution.</returns>
    public async Task<ScriptExecutionResult> ExecuteAsync(
        string code,
        ScriptExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        
        // Security validation
        var securityCheck = ValidateCodeSecurity(code, context.IsWriteEnabled);
        if (!securityCheck.IsValid)
        {
            stopwatch.Stop();
            logger.LogWarning(
                "Script security validation failed for tool {ToolName}: {Reason}",
                context.ToolName,
                securityCheck.Reason);
            
            return ScriptExecutionResult.SecurityViolation(securityCheck.Reason!, stopwatch.Elapsed);
        }
        
        // Create a linked cancellation token with timeout
        using var timeoutCts = new CancellationTokenSource(DefaultTimeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken, 
            timeoutCts.Token,
            context.CancellationToken);
        
        try
        {
            logger.LogDebug(
                "Executing script for tool {ToolName} with {ArgCount} arguments",
                context.ToolName,
                context.Args.Count);
            
            var options = ScriptOptions.Default
                .WithImports(SafeImports)
                .WithReferences(SafeAssemblies)
                .WithAllowUnsafe(false)
                .WithCheckOverflow(true)
                .WithOptimizationLevel(Microsoft.CodeAnalysis.OptimizationLevel.Release);
            
            // Execute the script with the context as the global object
            var result = await CSharpScript.EvaluateAsync<object?>(
                code,
                options,
                globals: context,
                globalsType: typeof(ScriptExecutionContext),
                cancellationToken: linkedCts.Token);
            
            stopwatch.Stop();
            
            logger.LogInformation(
                "Script for tool {ToolName} completed successfully in {ElapsedMs}ms",
                context.ToolName,
                stopwatch.ElapsedMilliseconds);
            
            return ScriptExecutionResult.Ok(result, stopwatch.Elapsed);
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
        {
            stopwatch.Stop();
            logger.LogWarning(
                "Script for tool {ToolName} timed out after {ElapsedMs}ms",
                context.ToolName,
                stopwatch.ElapsedMilliseconds);
            
            return ScriptExecutionResult.Timeout(stopwatch.Elapsed);
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();
            logger.LogInformation(
                "Script for tool {ToolName} was cancelled after {ElapsedMs}ms",
                context.ToolName,
                stopwatch.ElapsedMilliseconds);
            
            return ScriptExecutionResult.Fail("Script execution was cancelled.", stopwatch.Elapsed);
        }
        catch (CompilationErrorException ex)
        {
            stopwatch.Stop();
            var errors = string.Join(Environment.NewLine, ex.Diagnostics.Select(d => d.ToString()));
            
            logger.LogWarning(
                ex,
                "Script compilation failed for tool {ToolName}: {Errors}",
                context.ToolName,
                errors);
            
            return ScriptExecutionResult.Fail(
                $"Script compilation failed: {errors}",
                stopwatch.Elapsed,
                ex);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            logger.LogError(
                ex,
                "Script execution failed for tool {ToolName}",
                context.ToolName);
            
            return ScriptExecutionResult.Fail(
                $"Script execution failed: {ex.Message}",
                stopwatch.Elapsed,
                ex);
        }
    }
    
    /// <summary>
    /// Validates that the code doesn't contain blocked patterns or namespaces.
    /// </summary>
    private SecurityValidationResult ValidateCodeSecurity(string code, bool isWriteEnabled)
    {
        // Check for blocked namespaces
        foreach (var ns in BlockedNamespaces)
        {
            if (code.Contains(ns, StringComparison.OrdinalIgnoreCase))
            {
                return SecurityValidationResult.Invalid($"Access to '{ns}' is not allowed.");
            }
        }
        
        // Check for blocked patterns
        var match = BlockedPatternRegex.Match(code);
        if (match.Success)
        {
            return SecurityValidationResult.Invalid($"Pattern '{match.Value}' is not allowed for security reasons.");
        }
        
        // If not write-enabled, check for file write operations
        if (!isWriteEnabled)
        {
            if (FileWritePatternRegex().IsMatch(code))
            {
                return SecurityValidationResult.Invalid(
                    "File write operations are not allowed. Enable 'Write-Enabled' for this tool if write access is needed.");
            }
        }
        
        return SecurityValidationResult.Valid();
    }
    
    [GeneratedRegex(
        @"File\s*\.\s*(Write|Append|Create|Delete|Move|Copy)|" +
        @"Directory\s*\.\s*(Create|Delete|Move)|" +
        @"FileStream|" +
        @"StreamWriter|" +
        @"BinaryWriter",
        RegexOptions.IgnoreCase)]
    private static partial Regex FileWritePatternRegex();
    
    /// <summary>
    /// Result of security validation.
    /// </summary>
    private readonly struct SecurityValidationResult
    {
        public bool IsValid { get; }
        public string? Reason { get; }
        
        private SecurityValidationResult(bool isValid, string? reason)
        {
            IsValid = isValid;
            Reason = reason;
        }
        
        public static SecurityValidationResult Valid() => new(true, null);
        public static SecurityValidationResult Invalid(string reason) => new(false, reason);
    }
}
