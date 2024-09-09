﻿#region License Header
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
#endregion

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using PrettyPrompt.Completion;
using PrettyPrompt.Consoles;
using PrettyPrompt.Documents;
using PrettyPrompt.Highlighting;

namespace PrettyPrompt;

/// <summary>
/// A callback your application can provide to define custom behavior when a key is pressed.
/// <seealso cref="PromptCallbacks.GetKeyPressCallbacks"/>
/// </summary>
/// <param name="text">The user's input text</param>
/// <param name="caret">The index of the text caret in the input text</param>
/// <param name="cancellationToken">Cancellation token</param>
/// <returns>
/// <list type="bullet">
/// <item>
/// If a non-null <see cref="KeyPressCallbackResult"/> is returned, the prompt will be submitted.
/// The <see cref="KeyPressCallbackResult"/> will be returned by the current
/// <see cref="Prompt.ReadLineAsync()"/> function.
/// </item>
/// <item>
/// If a null <see cref="KeyPressCallbackResult"/> is returned, then the user will remain on the
/// current prompt.
/// </item>
/// </list>
/// </returns>
public delegate Task<KeyPressCallbackResult?> KeyPressCallbackAsync(string text, int caret, CancellationToken cancellationToken);

public interface IPromptCallbacks
{
    /// <summary>
    /// Looks up "Callback Functions" for  particular key press.
    /// The callback function will be invoked when the keys are pressed, with the current prompt
    /// text and the caret position within the text. ConsoleModifiers can be omitted if not required.
    /// </summary>
    /// If the prompt should be submitted as a result of the user's key press, then a non-null <see cref="KeyPressCallbackResult"/> may
    /// be returned from the <see cref="KeyPressCallbackAsync"/> function. If a null result is returned, then the user will remain on
    /// the current input prompt.
    bool TryGetKeyPressCallbacks(ConsoleKeyInfo keyInfo, [NotNullWhen(true)] out KeyPressCallbackAsync? result);

    /// <summary>
    /// Provides syntax-highlighting for input text.
    /// </summary>
    /// <param name="text">The text to be highlighted</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A collection of formatting instructions</returns>
    Task<IReadOnlyCollection<FormatSpan>> HighlightCallbackAsync(string text, CancellationToken cancellationToken);

    /// <summary>
    /// Determines which part of document will be replaced by inserted completion item.
    /// If not specified, default word detection is used.
    /// </summary>
    /// <param name="text">The user's input text</param>
    /// <param name="caret">The index of the text caret in the input text</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Span of text that will be replaced by inserted completion item.</returns>
    Task<TextSpan> GetSpanToReplaceByCompletionAsync(string text, int caret, CancellationToken cancellationToken);

    /// <summary>
    /// Provides to auto-completion items for specified position in the input text.
    /// </summary>
    /// <param name="text">The user's input text</param>
    /// <param name="caret">The index of the text caret in the input text</param>
    /// <param name="spanToBeReplaced">Span of text that will be replaced by inserted completion item</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A list of possible completions that will be displayed in the autocomplete menu.</returns>
    Task<IReadOnlyList<CompletionItem>> GetCompletionItemsAsync(string text, int caret, TextSpan spanToBeReplaced, CancellationToken cancellationToken);

    /// <summary>
    /// Controls when the completion window should open.
    /// If not specified, C#-like intellisense style behavior is used.
    /// </summary>
    /// <param name="text">The user's input text</param>
    /// <param name="caret">The index of the text caret in the input text</param>
    /// <param name="keyPress">Key press after which we are asking whether completion list should be automatically open.</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A value indicating whether the completion window should automatically open.</returns>
    Task<bool> ShouldOpenCompletionWindowAsync(string text, int caret, KeyPress keyPress, CancellationToken cancellationToken);

    /// <summary>
    /// Optionaly transforms key presses to another ones.
    /// </summary>
    /// <param name="text">The user's input text</param>
    /// <param name="caret">The index of the text caret in the input text</param>
    /// <param name="keyPress">Key press pattern in question</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Potentialy transformed key press.</returns>
    Task<KeyPress> TransformKeyPressAsync(string text, int caret, KeyPress keyPress, CancellationToken cancellationToken);

    /// <summary>
    /// Provides overload items for specified position in the input text if available otherwise empty collection is returned.
    /// </summary>
    /// <param name="text">The user's input text</param>
    /// <param name="caret">The index of the text caret in the input text</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A list of possible completions that will be displayed in the autocomplete menu.</returns>
    Task<(IReadOnlyList<OverloadItem>, int ArgumentIndex)> GetOverloadsAsync(string text, int caret, CancellationToken cancellationToken);

    /// <summary>
    /// Completion item commit can still be discarded based on current position of caret in document. This method is called only
    /// when completion item is going to be submited.
    /// </summary>
    /// <param name="text">The user's input text</param>
    /// <param name="caret">The index of the text caret in the input text</param>
    /// <param name="keyPress">Key press pattern in question</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns><see langword="true"/> if the completion item should be submitted or <see langword="false"/> otherwise.</returns>
    Task<bool> ConfirmCompletionCommit(string text, int caret, KeyPress keyPress, CancellationToken cancellationToken);

    /// <summary>
    /// Provides way to automatically format input text.
    /// </summary>
    /// <param name="text">The user's input text</param>
    /// <param name="caret">The index of the text caret in the input text</param>
    /// <param name="keyPress">Key press pattern in question</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Formatted input and new caret position.</returns>
    Task<(string Text, int Caret)> FormatInput(string text, int caret, KeyPress keyPress, CancellationToken cancellationToken);

    bool IsControlChar(KeyPress keyPress);
}

public class PromptCallbacks : IPromptCallbacks
{
    private (KeyPressPattern Pattern, KeyPressCallbackAsync Callback)[]? keyPressCallbacks;

    bool IPromptCallbacks.TryGetKeyPressCallbacks(ConsoleKeyInfo keyInfo, [NotNullWhen(true)] out KeyPressCallbackAsync? result)
    {
        keyPressCallbacks ??= GetKeyPressCallbacks().ToArray();
        foreach (var (pattern, callback) in keyPressCallbacks)
        {
            if (pattern.Matches(keyInfo))
            {
                result = callback;
                return true;
            }
        }
        result = null;
        return false;
    }

    Task<IReadOnlyCollection<FormatSpan>> IPromptCallbacks.HighlightCallbackAsync(string text, CancellationToken cancellationToken)
        => HighlightCallbackAsync(text, cancellationToken);

    async Task<TextSpan> IPromptCallbacks.GetSpanToReplaceByCompletionAsync(string text, int caret, CancellationToken cancellationToken)
    {
        Debug.Assert(caret >= 0 && caret <= text.Length);

        var span = await GetSpanToReplaceByCompletionAsync(text, caret, cancellationToken).ConfigureAwait(false);
        if (!new TextSpan(0, text.Length).Contains(span))
        {
            throw new InvalidOperationException("Resulting TextSpan has to be inside the document.");
        }
        if (!span.Contains(new TextSpan(caret, 0)))
        {
            throw new InvalidOperationException("Resulting TextSpan has to contain current caret position.");
        }
        return span;
    }

    Task<IReadOnlyList<CompletionItem>> IPromptCallbacks.GetCompletionItemsAsync(string text, int caret, TextSpan spanToBeReplaced, CancellationToken cancellationToken)
    {
        Debug.Assert(caret >= 0 && caret <= text.Length);

        return GetCompletionItemsAsync(text, caret, spanToBeReplaced, cancellationToken);
    }

    Task<bool> IPromptCallbacks.ShouldOpenCompletionWindowAsync(string text, int caret, KeyPress keyPress, CancellationToken cancellationToken)
    {
        Debug.Assert(caret >= 0 && caret <= text.Length);

        return ShouldOpenCompletionWindowAsync(text, caret, keyPress, cancellationToken);
    }

    Task<KeyPress> IPromptCallbacks.TransformKeyPressAsync(string text, int caret, KeyPress keyPress, CancellationToken cancellationToken)
    {
        Debug.Assert(caret >= 0 && caret <= text.Length);

        return TransformKeyPressAsync(text, caret, keyPress, cancellationToken);
    }

    Task<bool> IPromptCallbacks.ConfirmCompletionCommit(string text, int caret, KeyPress keyPress, CancellationToken cancellationToken)
    {
        Debug.Assert(caret >= 0 && caret <= text.Length);

        return ConfirmCompletionCommit(text, caret, keyPress, cancellationToken);
    }

    Task<(string Text, int Caret)> IPromptCallbacks.FormatInput(string text, int caret, KeyPress keyPress, CancellationToken cancellationToken)
    {
        Debug.Assert(caret >= 0 && caret <= text.Length);

        return FormatInput(text, caret, keyPress, cancellationToken);
    }

    Task<(IReadOnlyList<OverloadItem>, int ArgumentIndex)> IPromptCallbacks.GetOverloadsAsync(string text, int caret, CancellationToken cancellationToken)
    {
        Debug.Assert(caret >= 0 && caret <= text.Length);

        return GetOverloadsAsync(text, caret, cancellationToken);
    }

    /// <summary>
    /// This method is called only once and provides list of key press patterns with "Callback Functions".
    /// The callback function will be invoked when the keys are pressed, with the current prompt
    /// text and the caret position within the text. ConsoleModifiers can be omitted if not required.
    /// </summary>
    /// If the prompt should be submitted as a result of the user's key press, then a non-null <see cref="KeyPressCallbackResult"/> may
    /// be returned from the <see cref="KeyPressCallbackAsync"/> function. If a null result is returned, then the user will remain on
    /// the current input prompt.
    protected virtual IEnumerable<(KeyPressPattern Pattern, KeyPressCallbackAsync Callback)> GetKeyPressCallbacks()
        => Array.Empty<(KeyPressPattern, KeyPressCallbackAsync)>();

    /// <inheritdoc cref="IPromptCallbacks.HighlightCallbackAsync"/>
    protected virtual Task<IReadOnlyCollection<FormatSpan>> HighlightCallbackAsync(string text, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyCollection<FormatSpan>>(Array.Empty<FormatSpan>());

    /// <inheritdoc cref="IPromptCallbacks.GetSpanToReplaceByCompletionAsync"/>
    protected virtual Task<TextSpan> GetSpanToReplaceByCompletionAsync(string text, int caret, CancellationToken cancellationToken)
    {
        int wordStart = caret;
        for (int i = wordStart - 1; i >= 0; i--)
        {
            if (IsWordCharacter(text[i], text, caret))
            {
                --wordStart;
            }
            else
            {
                break;
            }
        }
        if (wordStart < 0) wordStart = 0;

        int wordEnd = caret;
        for (int i = caret; i < text.Length; i++)
        {
            if (IsWordCharacter(text[i], text, caret))
            {
                ++wordEnd;
            }
            else
            {
                break;
            }
        }

        return Task.FromResult(TextSpan.FromBounds(wordStart, wordEnd));
    }
    protected virtual bool IsWordCharacter(char c, string text, int caret) => char.IsLetterOrDigit(c) || c == '_';

    /// <inheritdoc cref="IPromptCallbacks.GetCompletionItemsAsync"/>
    protected virtual Task<IReadOnlyList<CompletionItem>> GetCompletionItemsAsync(string text, int caret, TextSpan spanToBeReplaced, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<CompletionItem>>(Array.Empty<CompletionItem>());

    /// <inheritdoc cref="IPromptCallbacks.ShouldOpenCompletionWindowAsync"/>
    protected virtual Task<bool> ShouldOpenCompletionWindowAsync(string text, int caret, KeyPress keyPress, CancellationToken cancellationToken)
    {
        if (caret > 0 && text[caret - 1] is '.' or '(') // typical "intellisense behavior", opens for new methods and parameters
        {
            return Task.FromResult(true);
        }

        if (caret == 1 && !char.IsWhiteSpace(text[0]) // 1 word character typed in brand new prompt
            && (text.Length == 1 || !char.IsLetterOrDigit(text[1]))) // if there's more than one character on the prompt, but we're typing a new word at the beginning (e.g. "a| bar")
        {
            return Task.FromResult(true);
        }

        // open when we're starting a new "word" in the prompt.
        return Task.FromResult(caret - 2 >= 0 && char.IsWhiteSpace(text[caret - 2]) && char.IsLetter(text[caret - 1]));
    }

    /// <inheritdoc cref="IPromptCallbacks.TransformKeyPressAsync(string, int, KeyPress, CancellationToken)"/>
    protected virtual Task<KeyPress> TransformKeyPressAsync(string text, int caret, KeyPress keyPress, CancellationToken cancellationToken)
        => Task.FromResult(keyPress);

    /// <inheritdoc cref="IPromptCallbacks.ConfirmCompletionCommit(string, int, KeyPress, CancellationToken)"/>
    protected virtual Task<bool> ConfirmCompletionCommit(string text, int caret, KeyPress keyPress, CancellationToken cancellationToken)
        => Task.FromResult(true);

    /// <inheritdoc cref="IPromptCallbacks.FormatInput(string, int, KeyPress, CancellationToken)"/>
    protected virtual Task<(string Text, int Caret)> FormatInput(string text, int caret, KeyPress keyPress, CancellationToken cancellationToken)
        => Task.FromResult((text, caret));

    /// <inheritdoc cref="GetOverloadsAsync(string, int, CancellationToken)"/>
    protected virtual Task<(IReadOnlyList<OverloadItem>, int ArgumentIndex)> GetOverloadsAsync(string text, int caret, CancellationToken cancellationToken)
        => Task.FromResult<(IReadOnlyList<OverloadItem>, int ArgumentIndex)>((Array.Empty<OverloadItem>(), 0));

    public virtual bool IsControlChar(KeyPress keyPress)
    {
        return char.IsControl(keyPress.ConsoleKeyInfo.KeyChar);
    }
}