using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ViewAppxPackage;

/// <summary>
/// Interface for App Action providers following Microsoft's App Actions pattern.
/// This interface defines the contract for handling App Actions in the absence
/// of a built-in IActionProvider interface in the current Windows App SDK version.
/// </summary>
public interface IActionProvider
{
    /// <summary>
    /// Handles execution of an App Action
    /// </summary>
    /// <param name="actionId">The identifier of the action to execute</param>
    /// <param name="parameters">Parameters for the action</param>
    /// <returns>A task representing the result of the action execution</returns>
    Task<ActionResult> HandleActionAsync(string actionId, IDictionary<string, object> parameters);
}

/// <summary>
/// Represents the result of an App Action execution
/// </summary>
public class ActionResult
{
    public bool IsSuccess { get; set; }
    public string Content { get; set; } = "";
    public string ErrorMessage { get; set; } = "";

    public static ActionResult CreateSuccess(string content)
    {
        return new ActionResult { IsSuccess = true, Content = content };
    }

    public static ActionResult CreateError(string errorMessage)
    {
        return new ActionResult { IsSuccess = false, ErrorMessage = errorMessage };
    }
}