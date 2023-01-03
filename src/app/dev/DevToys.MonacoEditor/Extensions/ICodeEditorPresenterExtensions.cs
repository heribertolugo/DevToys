﻿using System.Runtime.CompilerServices;
using DevToys.UI.Framework.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace DevToys.MonacoEditor.Extensions;

internal static class ICodeEditorPresenterExtensions
{
    public static async Task RunScriptAsync(
        this ICodeEditorPresenter view,
        string script,
        [CallerMemberName] string? member = null,
        [CallerFilePath] string? file = null,
        [CallerLineNumber] int line = 0)
    {
        await view.RunScriptAsync<object>(script, member, file, line);
    }

    public static async Task<T?> RunScriptAsync<T>(
        this ICodeEditorPresenter view,
        string script,
        [CallerMemberName] string? member = null,
        [CallerFilePath] string? file = null,
        [CallerLineNumber] int line = 0)
    {
        string start = "try {\n";
        if (typeof(T) != typeof(object))
        {
            script = script.Trim(';');
            start += "return JSON.stringify(" + script + ");";
        }
        else
        {
            start += "return " + script;
        }
        string fullscript = start +
            "\n} catch (err) { return JSON.stringify({ wv_internal_error: true, message: err.message, description: err.description, number: err.number, stack: err.stack }); }";

        if (view.DispatcherQueue.HasThreadAccess)
        {
            try
            {
                return await RunScriptHelperAsync<T>(view, fullscript);
            }
            catch (Exception e)
            {
                throw new JavaScriptExecutionException(member, file, line, script, e);
            }
        }
        else
        {
            return await view.DispatcherQueue.RunOnUIThreadAsync(async () =>
            {
                try
                {
                    return await RunScriptHelperAsync<T>(view, fullscript);
                }
                catch (Exception e)
                {
                    throw new JavaScriptExecutionException(member, file, line, script, e);
                }
            });
        }
    }

    private static async Task<T?> RunScriptHelperAsync<T>(ICodeEditorPresenter view, string script)
    {
        string returnstring = await view.InvokeScriptAsync(script);

        //if (JsonObject.TryParse(returnstring, out JsonObject result))
        //{
        //    if (result.ContainsKey("wv_internal_error") && result["wv_internal_error"].ValueType == JsonValueType.Boolean && result["wv_internal_error"].GetBoolean())
        //    {
        //        throw new JavaScriptInnerException(result["message"].GetString(), result["stack"].GetString());
        //    }
        //}

        // TODO: Need to decode the error correctly
        if (!string.IsNullOrEmpty(returnstring))
        {
            if (returnstring.Contains("wv_internal_error"))
            {
                throw new JavaScriptInnerException(returnstring, "");
            }

            if (returnstring != "null")
            {
                return JsonConvert.DeserializeObject<T>(returnstring);
            }
        }

        return default;
    }

    private static readonly JsonSerializerSettings _settings = new()
    {
        NullValueHandling = NullValueHandling.Ignore,
        ContractResolver = new CamelCasePropertyNamesContractResolver()
    };

    public static async Task InvokeScriptAsync(
        this ICodeEditorPresenter view,
        string method,
        object arg,
        bool serialize = true,
        [CallerMemberName] string? member = null,
        [CallerFilePath] string? file = null,
        [CallerLineNumber] int line = 0)
    {
        await view.InvokeScriptAsync<object>(method, arg, serialize, member, file, line);
    }

    public static async Task InvokeScriptAsync(
        this ICodeEditorPresenter view,
        string method,
        object[] args,
        bool serialize = true,
        [CallerMemberName] string? member = null,
        [CallerFilePath] string? file = null,
        [CallerLineNumber] int line = 0)
    {
        await view.InvokeScriptAsync<object>(method, args, serialize, member, file, line);
    }

    public static async Task<T?> InvokeScriptAsync<T>(
        this ICodeEditorPresenter view,
        string method,
        object arg,
        bool serialize = true,
        [CallerMemberName] string? member = null,
        [CallerFilePath] string? file = null,
        [CallerLineNumber] int line = 0)
    {
        return await view.InvokeScriptAsync<T>(method, new object[] { arg }, serialize, member, file, line);
    }

    public static async Task<T?> InvokeScriptAsync<T>(
        this ICodeEditorPresenter view,
        string method,
        object[] args,
        bool serialize = true,
        [CallerMemberName] string? member = null,
        [CallerFilePath] string? file = null,
        [CallerLineNumber] int line = 0)
    {
        string?[] sanitizedargs;

        try
        {
            Debug.WriteLine($"Begin invoke script (serialize - {serialize})");
            if (serialize)
            {
                sanitizedargs
                    = args.Select(
                        item =>
                        {
                            if (item is int || item is double)
                            {
                                return item.ToString();
                            }
                            else if (item is string)
                            {
                                return JsonConvert.ToString(item);
                            }
                            else
                            {
                                // TODO: Need JSON.parse?
                                return JsonConvert.SerializeObject(item, _settings);
                            }
                        })
                    .ToArray();
            }
            else
            {
                sanitizedargs = args.Select(item => item.ToString()).ToArray();
            }

            string script = method + "(editorContext, " + string.Join(", ", sanitizedargs) + ");";

            Debug.WriteLine($"Script {script})");

            return await RunScriptAsync<T>(view, script, member, file, line);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error {ex.Message} {ex.StackTrace} {ex.InnerException?.Message})");
            return default(T);
        }
    }
}
