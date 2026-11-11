// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Concurrent;
using System.Runtime.Versioning;
using System.Threading;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Text;
using Android.Views;
using Android.Views.InputMethods;
using Android.Widget;
using Org.Libsdl.App;
using osu.Framework.Graphics.Primitives;
using osu.Framework.Input;
using osu.Framework.Platform;
using ClipboardManager = Android.Content.ClipboardManager;
using ICharSequence = Java.Lang.ICharSequence;

namespace osu.Android.Input
{
    /// <summary>
    /// Native Android <see cref="TextInputSource"/> that uses a dedicated password input <see cref="View"/>
    /// and clean <see cref="EditorInfo"/> flags instead of SDL3's shared <c>SDLDummyEdit</c>, ensuring Android
    /// switches to the system password / secure keyboard without exposing typed characters in IME suggestions.
    /// </summary>
    internal sealed class AndroidTextInputSource : TextInputSource, IDisposable
    {
        private const int height_padding = 15;
        private const int ime_flag_no_personalized_learning = 0x01000000;
        private const int combined_dead_key = unchecked((int)0x80000000);
        private const string sdl_dummy_edit_class = "org.libsdl.app.SDLDummyEdit";

        private const int context_menu_select_all = 0x0102001f;
        private const int context_menu_cut = 0x01020020;
        private const int context_menu_copy = 0x01020021;
        private const int context_menu_paste = 0x01020022;
        private const int context_menu_paste_as_plain_text = 0x01020031;

        private readonly OsuGameActivity activity;
        private readonly GameHost host;
        private readonly ConcurrentQueue<PendingTextEvent> pendingTextEvents = new ConcurrentQueue<PendingTextEvent>();

        private LayoutHierarchyGuard? hierarchyGuard;
        private AndroidInputView? textInputView;
        private AndroidInputView? passwordInputView;
        private AndroidInputView? activeView;
        private TextInputProperties? activeProperties;

        private int sessionGeneration;
        private long windowFrame;
        private long minTextEventFrame;

        private int lastRectX;
        private int lastRectY;
        private int lastRectW = 1;
        private int lastRectH = 1;

        private bool isDisposed;

        public AndroidTextInputSource(OsuGameActivity activity, GameHost host)
        {
            this.activity = activity;
            this.host = host;

            activity.AttachTextInputSource(this);

            if (host.Window != null)
                host.Window.Update += onWindowUpdate;
        }

        internal void RestoreFocusIfActive()
        {
            activity.RunOnUiThread(() =>
            {
                if (isDisposed || activeView == null || !activeView.IsInputActive)
                    return;

                if (!activeView.HasFocus)
                {
                    activeView.ResetConnectionState();
                    activeView.RequestFocus();

                    var imm = activity.GetSystemService(Context.InputMethodService) as InputMethodManager;
                    imm?.RestartInput(activeView);
                }
            });
        }

        protected override void ActivateTextInput(TextInputProperties properties)
        {
            startOrUpdateInput(properties, forceRestart: true);
        }

        protected override void EnsureTextInputActivated(TextInputProperties properties)
        {
            startOrUpdateInput(properties, forceRestart: false);
        }

        protected override void DeactivateTextInput()
        {
            int generation = Interlocked.Increment(ref sessionGeneration);

            activity.RunOnUiThread(() =>
            {
                if (generation != Volatile.Read(ref sessionGeneration) || isDisposed)
                    return;

                IBinder? windowToken = activeView?.WindowToken
                                       ?? passwordInputView?.WindowToken
                                       ?? textInputView?.WindowToken;

                textInputView?.DeactivateAndPark();
                passwordInputView?.DeactivateAndPark();

                activeView = null;
                activeProperties = null;

                if (windowToken != null)
                {
                    var imm = activity.GetSystemService(Context.InputMethodService) as InputMethodManager;
                    imm?.HideSoftInputFromWindow(windowToken, HideSoftInputFlags.None);
                }

                activity.GameSurface?.RequestFocus();
            });
        }

        public override void SetImeRectangle(RectangleF rectangle)
        {
            float scale = host.Window?.Scale ?? 1f;
            RectangleF scaled = scale > 0f ? rectangle / scale : rectangle;

            int x = (int)Math.Round(scaled.X);
            int y = (int)Math.Round(scaled.Y);
            int w = Math.Max(1, (int)Math.Round(scaled.Width));
            int h = Math.Max(1, (int)Math.Round(scaled.Height) + height_padding);

            if (x == Volatile.Read(ref lastRectX)
                && y == Volatile.Read(ref lastRectY)
                && w == Volatile.Read(ref lastRectW)
                && h == Volatile.Read(ref lastRectH))
            {
                return;
            }

            Volatile.Write(ref lastRectX, x);
            Volatile.Write(ref lastRectY, y);
            Volatile.Write(ref lastRectW, w);
            Volatile.Write(ref lastRectH, h);

            activity.RunOnUiThread(() =>
            {
                if (isDisposed || activeView == null)
                    return;

                applyLayoutBounds(activeView,
                    Volatile.Read(ref lastRectX),
                    Volatile.Read(ref lastRectY),
                    Volatile.Read(ref lastRectW),
                    Volatile.Read(ref lastRectH));
            });
        }

        public override void ResetIme()
        {
            base.ResetIme();

            int generation = Volatile.Read(ref sessionGeneration);

            activity.RunOnUiThread(() =>
            {
                if (generation != Volatile.Read(ref sessionGeneration) || isDisposed || activeView == null)
                    return;

                activeView.ResetConnectionState();

                var imm = activity.GetSystemService(Context.InputMethodService) as InputMethodManager;
                imm?.RestartInput(activeView);
            });
        }

        private void startOrUpdateInput(TextInputProperties properties, bool forceRestart)
        {
            int generation = Interlocked.Increment(ref sessionGeneration);

            activity.RunOnUiThread(() =>
            {
                if (generation != Volatile.Read(ref sessionGeneration) || isDisposed)
                    return;

                if (activity.InputRootLayout is not RelativeLayout rootLayout)
                    return;

                ensureLayoutGuarded(rootLayout);

                bool usePasswordView = properties.Type.IsPassword();
                AndroidInputView targetView = usePasswordView
                    ? ensurePasswordView(rootLayout)
                    : ensureTextView(rootLayout);

                AndroidInputView? otherView = usePasswordView ? textInputView : passwordInputView;
                bool viewSwitched = activeView != targetView;
                bool propertiesChanged = activeProperties == null || activeProperties.Value != properties;

                if (viewSwitched && otherView != null)
                    otherView.DeactivateAndPark();

                activeView = targetView;
                activeProperties = properties;

                applyLayoutBounds(targetView,
                    Volatile.Read(ref lastRectX),
                    Volatile.Read(ref lastRectY),
                    Volatile.Read(ref lastRectW),
                    Volatile.Read(ref lastRectH));

                bool hadFocus = targetView.HasFocus;
                bool needsRestart = forceRestart || viewSwitched || propertiesChanged || !targetView.IsInputActive || !hadFocus;
                targetView.Configure(properties, resetState: needsRestart);

                if (!hadFocus)
                    targetView.RequestFocus();

                var imm = activity.GetSystemService(Context.InputMethodService) as InputMethodManager;
                if (imm == null)
                    return;

                if (needsRestart)
                    imm.RestartInput(targetView);

                imm.ShowSoftInput(targetView, 0);
            });
        }

        private void ensureLayoutGuarded(RelativeLayout rootLayout)
        {
            if (hierarchyGuard != null)
                return;

            for (int i = 0; i < rootLayout.ChildCount; i++)
                neutralizeSdlDummyEdit(rootLayout.GetChildAt(i));

            hierarchyGuard = new LayoutHierarchyGuard();
            rootLayout.SetOnHierarchyChangeListener(hierarchyGuard);
        }

        private static void neutralizeSdlDummyEdit(View? view)
        {
            if (view == null || !string.Equals(view.Class?.Name, sdl_dummy_edit_class, StringComparison.Ordinal))
                return;

            view.Focusable = false;
            view.FocusableInTouchMode = false;
            view.Visibility = ViewStates.Gone;
        }

        private AndroidInputView ensureTextView(RelativeLayout rootLayout)
        {
            if (textInputView != null)
                return textInputView;

            textInputView = new AndroidInputView(activity, this, dedicatedPassword: false);
            rootLayout.AddView(textInputView, new RelativeLayout.LayoutParams(0, 0));
            return textInputView;
        }

        private AndroidInputView ensurePasswordView(RelativeLayout rootLayout)
        {
            if (passwordInputView != null)
                return passwordInputView;

            passwordInputView = new AndroidInputView(activity, this, dedicatedPassword: true);
            rootLayout.AddView(passwordInputView, new RelativeLayout.LayoutParams(0, 0));
            return passwordInputView;
        }

        private static void applyLayoutBounds(AndroidInputView view, int x, int y, int w, int h)
        {
            if (view.Visibility != ViewStates.Visible)
                view.Visibility = ViewStates.Visible;

            if (view.LayoutParameters is RelativeLayout.LayoutParams existing
                && existing.LeftMargin == x
                && existing.TopMargin == y
                && existing.Width == w
                && existing.Height == h)
            {
                return;
            }

            view.LayoutParameters = new RelativeLayout.LayoutParams(w, h)
            {
                LeftMargin = x,
                TopMargin = y,
            };
        }

        private void onWindowUpdate()
        {
            long frame = Interlocked.Increment(ref windowFrame);

            while (pendingTextEvents.TryPeek(out PendingTextEvent next))
            {
                if (next.MinFrame > frame)
                    break;

                if (!pendingTextEvents.TryDequeue(out PendingTextEvent evt))
                    break;

                switch (evt.Kind)
                {
                    case TextEventKind.TextInput:
                        TriggerTextInput(evt.Text);
                        break;

                    case TextEventKind.ImeComposition:
                        TriggerImeComposition(evt.Text, evt.SelectionStart, evt.SelectionLength);
                        break;

                    case TextEventKind.ImeResult:
                        TriggerImeResult(evt.Text);
                        break;
                }
            }
        }

        private void enqueueTextEvent(TextEventKind kind, string text, int selectionStart, int selectionLength)
        {
            pendingTextEvents.Enqueue(new PendingTextEvent(Volatile.Read(ref minTextEventFrame), kind, text, selectionStart, selectionLength));
        }

        private void commitPlainText(string text)
        {
            if (string.IsNullOrEmpty(text))
                return;

            string sanitized = text.Replace("\r", string.Empty, StringComparison.Ordinal);
            bool hasNewline = sanitized.Contains('\n', StringComparison.Ordinal);

            if (hasNewline)
                sanitized = sanitized.Replace("\n", string.Empty, StringComparison.Ordinal);

            if (sanitized.Length > 0)
                enqueueTextEvent(TextEventKind.TextInput, sanitized, 0, 0);

            if (hasNewline && string.Equals(text, "\n", StringComparison.Ordinal))
                emitKeyPress(Keycode.Enter);
        }

        private void commitImeResult(string text)
        {
            string sanitized = text.Replace("\r", string.Empty, StringComparison.Ordinal);
            bool hasNewline = sanitized.Contains('\n', StringComparison.Ordinal);

            if (hasNewline)
                sanitized = sanitized.Replace("\n", string.Empty, StringComparison.Ordinal);

            enqueueTextEvent(TextEventKind.ImeResult, sanitized, 0, 0);

            if (hasNewline && string.Equals(text, "\n", StringComparison.Ordinal))
                emitKeyPress(Keycode.Enter);
        }

        private void updateImeComposition(string text, int start, int length)
        {
            string sanitized = text.Replace("\r", string.Empty, StringComparison.Ordinal)
                                   .Replace("\n", string.Empty, StringComparison.Ordinal);

            int clampedStart = Math.Clamp(start, 0, sanitized.Length);
            int clampedLength = Math.Clamp(length, 0, sanitized.Length - clampedStart);

            enqueueTextEvent(TextEventKind.ImeComposition, sanitized, clampedStart, clampedLength);
        }

        private void deleteSurrounding(int beforeLength, int afterLength)
        {
            for (int i = 0; i < beforeLength; i++)
                emitKeyPress(Keycode.Del);

            for (int i = 0; i < afterLength; i++)
                emitKeyPress(Keycode.ForwardDel);
        }

        private void emitKeyPress(Keycode keyCode)
        {
            long currentFrame = Volatile.Read(ref windowFrame);
            SDLActivity.OnNativeKeyDown((int)keyCode);
            SDLActivity.OnNativeKeyUp((int)keyCode);
            Volatile.Write(ref minTextEventFrame, currentFrame + 2);
        }

        private bool handleContextMenuAction(int id, bool allowExport)
        {
            switch (id)
            {
                case context_menu_paste:
                case context_menu_paste_as_plain_text:
                {
                    if (activity.GetSystemService(Context.ClipboardService) is not ClipboardManager clipboard)
                        return false;

                    ClipData? clip = clipboard.PrimaryClip;
                    if (clip == null || clip.ItemCount <= 0)
                        return false;

                    string? pasted = clip.GetItemAt(0)?.CoerceToText(activity)?.ToString();
                    if (!string.IsNullOrEmpty(pasted))
                        commitPlainText(pasted);

                    return true;
                }

                case context_menu_select_all:
                    emitModifiedKeyPress(Keycode.CtrlLeft, Keycode.A);
                    return true;

                case context_menu_copy:
                    if (!allowExport)
                        return false;

                    emitModifiedKeyPress(Keycode.CtrlLeft, Keycode.C);
                    return true;

                case context_menu_cut:
                    if (!allowExport)
                        return false;

                    emitModifiedKeyPress(Keycode.CtrlLeft, Keycode.X);
                    return true;

                default:
                    return false;
            }
        }

        private void emitModifiedKeyPress(Keycode modifier, Keycode key)
        {
            long currentFrame = Volatile.Read(ref windowFrame);
            SDLActivity.OnNativeKeyDown((int)modifier);
            SDLActivity.OnNativeKeyDown((int)key);
            SDLActivity.OnNativeKeyUp((int)key);
            SDLActivity.OnNativeKeyUp((int)modifier);
            Volatile.Write(ref minTextEventFrame, currentFrame + 2);
        }

        private bool handleKeyEvent(KeyEvent e)
        {
            Keycode keyCode = e.KeyCode;

            switch (keyCode)
            {
                // Allow Android to handle system media volume keys natively while a TextBox is focused.
                case Keycode.VolumeDown:
                case Keycode.VolumeUp:
                case Keycode.VolumeMute:
                    return false;
            }

#pragma warning disable CA1422 // KeyEventActions.Multiple and KeyEvent.Characters are deprecated on API 29+, kept for legacy IME fallback on API 24-28
            if (e.Action == KeyEventActions.Multiple)
            {
                if (keyCode == Keycode.Unknown && !string.IsNullOrEmpty(e.Characters))
                {
                    commitPlainText(e.Characters);
                    return true;
                }

                if (keyCode == Keycode.Del)
                {
                    deleteSurrounding(Math.Max(1, e.RepeatCount), 0);
                    return true;
                }

                return false;
            }
#pragma warning restore CA1422

            if (!e.IsCtrlPressed && !e.IsAltPressed && !e.IsMetaPressed && tryGetPrintableText(e, out string printedText))
            {
                if (e.Action == KeyEventActions.Down)
                    commitPlainText(printedText);

                return true;
            }

            if (e.Action == KeyEventActions.Down)
            {
                long currentFrame = Volatile.Read(ref windowFrame);
                SDLActivity.OnNativeKeyDown((int)keyCode);

                // Physical keyboards on Android may omit KeyUp for Del while an InputConnection is active.
                if (keyCode == Keycode.Del && e.DeviceId > 0)
                    SDLActivity.OnNativeKeyUp((int)keyCode);

                Volatile.Write(ref minTextEventFrame, currentFrame + 2);
                return true;
            }

            if (e.Action == KeyEventActions.Up)
            {
                SDLActivity.OnNativeKeyUp((int)keyCode);
                return true;
            }

            return false;
        }

        private static bool tryGetPrintableText(KeyEvent e, out string printedText)
        {
            printedText = string.Empty;

            switch (e.KeyCode)
            {
                case Keycode.Enter:
                case Keycode.NumpadEnter:
                case Keycode.Tab:
                case Keycode.Del:
                case Keycode.ForwardDel:
                case Keycode.Escape:
                    return false;
            }

            int unicode = e.UnicodeChar;

            if (unicode <= 0 || (unicode & combined_dead_key) != 0)
                return false;

            if (unicode < 0x20 || unicode == 0x7f)
                return false;

            printedText = char.ConvertFromUtf32(unicode);
            return true;
        }

        private static InputTypes buildInputType(TextInputProperties properties)
        {
            switch (properties.Type)
            {
                case TextInputType.Password:
                    // Matches Android native `textPassword` (0x00000081) without AutoCorrect, AutoComplete, or MultiLine,
                    // ensuring Android keyboards and OEM secure keyboards enter dedicated system password mode.
                    return InputTypes.ClassText | InputTypes.TextVariationPassword;

                case TextInputType.NumericalPassword:
                    // Matches Android native `numberPassword` (0x00000012).
                    return InputTypes.ClassNumber | InputTypes.NumberVariationPassword;

                case TextInputType.Number:
                    return InputTypes.ClassNumber | InputTypes.NumberVariationNormal | InputTypes.NumberFlagSigned;

                case TextInputType.Decimal:
                    return InputTypes.ClassNumber | InputTypes.NumberVariationNormal | InputTypes.NumberFlagSigned | InputTypes.NumberFlagDecimal;

                case TextInputType.EmailAddress:
                    return InputTypes.ClassText | InputTypes.TextVariationEmailAddress | InputTypes.TextFlagNoSuggestions;

                case TextInputType.Username:
                case TextInputType.Code:
                    return InputTypes.ClassText | InputTypes.TextVariationNormal | InputTypes.TextFlagNoSuggestions;

                case TextInputType.Name:
                {
                    InputTypes type = InputTypes.ClassText | InputTypes.TextVariationPersonName;

                    if (properties.AutoCapitalisation)
                        type |= InputTypes.TextFlagCapWords;

                    if (!properties.AllowIme)
                        type |= InputTypes.TextFlagNoSuggestions;

                    return type;
                }

                default:
                case TextInputType.Text:
                {
                    InputTypes type = InputTypes.ClassText | InputTypes.TextVariationNormal;

                    if (properties.AutoCapitalisation)
                        type |= InputTypes.TextFlagCapSentences;

                    if (properties.AllowIme)
                        type |= InputTypes.TextFlagAutoCorrect;
                    else
                        type |= InputTypes.TextFlagNoSuggestions;

                    return type;
                }
            }
        }

        private static ImeFlags buildImeOptions(TextInputProperties properties)
        {
            int flags = (int)ImeFlags.NoExtractUi
                        | (int)ImeFlags.NoFullscreen
                        | (int)ImeAction.Done;

            if (properties.Type.IsPassword()
                || !properties.AllowIme
                || properties.Type == TextInputType.Username
                || properties.Type == TextInputType.Code)
            {
                flags |= ime_flag_no_personalized_learning;
            }

            return (ImeFlags)flags;
        }

        public void Dispose()
        {
            if (isDisposed)
                return;

            isDisposed = true;
            Interlocked.Increment(ref sessionGeneration);

            activity.AttachTextInputSource(null);

            if (host.Window != null)
                host.Window.Update -= onWindowUpdate;

            activity.RunOnUiThread(() =>
            {
                if (activity.InputRootLayout is ViewGroup rootLayout)
                {
                    rootLayout.SetOnHierarchyChangeListener(null);

                    if (textInputView != null)
                        rootLayout.RemoveView(textInputView);

                    if (passwordInputView != null)
                        rootLayout.RemoveView(passwordInputView);
                }

                hierarchyGuard?.Dispose();
                textInputView?.Dispose();
                passwordInputView?.Dispose();
                hierarchyGuard = null;
                textInputView = null;
                passwordInputView = null;
                activeView = null;
            });
        }

        private sealed class LayoutHierarchyGuard : Java.Lang.Object, ViewGroup.IOnHierarchyChangeListener
        {
            public LayoutHierarchyGuard()
            {
            }

            public LayoutHierarchyGuard(IntPtr javaReference, JniHandleOwnership transfer)
                : base(javaReference, transfer)
            {
            }

            public void OnChildViewAdded(View? parent, View? child) => neutralizeSdlDummyEdit(child);

            public void OnChildViewRemoved(View? parent, View? child)
            {
            }
        }

        private enum TextEventKind
        {
            TextInput,
            ImeComposition,
            ImeResult,
        }

        private readonly record struct PendingTextEvent(
            long MinFrame,
            TextEventKind Kind,
            string Text,
            int SelectionStart,
            int SelectionLength);

        private sealed class AndroidInputView : View
        {
            private readonly AndroidTextInputSource? owner;
            private readonly bool dedicatedPassword;

            private TextInputProperties configuredProperties;
            private ManagedInputConnection? currentConnection;

            public bool IsInputActive { get; private set; }

            public AndroidInputView(Context context, AndroidTextInputSource owner, bool dedicatedPassword)
                : base(context)
            {
                this.owner = owner;
                this.dedicatedPassword = dedicatedPassword;

                Focusable = true;
                FocusableInTouchMode = true;
                Clickable = false;
                LongClickable = false;
                SetWillNotDraw(true);

                if (OperatingSystem.IsAndroidVersionAtLeast(26))
                    disableFocusHighlightApi26(this);
            }

            public AndroidInputView(IntPtr javaReference, JniHandleOwnership transfer)
                : base(javaReference, transfer)
            {
            }

            [SupportedOSPlatform("android26.0")]
            private static void disableFocusHighlightApi26(View view)
            {
                view.DefaultFocusHighlightEnabled = false;
            }

            public void Configure(TextInputProperties properties, bool resetState)
            {
                configuredProperties = properties;
                IsInputActive = true;

                if (resetState)
                    ResetConnectionState();
            }

            public void ResetConnectionState()
            {
                currentConnection?.Invalidate();
                currentConnection = null;
            }

            public void DeactivateAndPark()
            {
                IsInputActive = false;
                ResetConnectionState();

                LayoutParameters = new RelativeLayout.LayoutParams(0, 0);
            }

            public override bool OnCheckIsTextEditor() => IsInputActive;

            public override bool DispatchTouchEvent(MotionEvent? e) => false;

            public override bool OnTouchEvent(MotionEvent? e) => false;

            public override IInputConnection? OnCreateInputConnection(EditorInfo? outAttrs)
            {
                if (!IsInputActive || owner == null || outAttrs == null)
                    return null;

                currentConnection?.Invalidate();

                outAttrs.InputType = buildInputType(configuredProperties);
                outAttrs.ImeOptions = buildImeOptions(configuredProperties);
                outAttrs.InitialSelStart = 0;
                outAttrs.InitialSelEnd = 0;

                currentConnection = dedicatedPassword
                    ? new PasswordInputConnection(this, owner)
                    : new NormalInputConnection(this, owner);

                return currentConnection;
            }

            public override bool OnKeyDown(Keycode keyCode, KeyEvent? e)
            {
                if (IsInputActive && e != null && owner?.handleKeyEvent(e) == true)
                    return true;

                return base.OnKeyDown(keyCode, e);
            }

            public override bool OnKeyUp(Keycode keyCode, KeyEvent? e)
            {
                if (IsInputActive && e != null && owner?.handleKeyEvent(e) == true)
                    return true;

                return base.OnKeyUp(keyCode, e);
            }

            public override bool OnKeyMultiple(Keycode keyCode, int repeatCount, KeyEvent? e)
            {
                if (IsInputActive && e != null && owner?.handleKeyEvent(e) == true)
                    return true;

                return base.OnKeyMultiple(keyCode, repeatCount, e);
            }
        }

        private abstract class ManagedInputConnection : BaseInputConnection
        {
            protected readonly AndroidTextInputSource? Owner;

            private volatile bool isValid = true;

            protected bool IsCurrent => isValid;

            protected ManagedInputConnection(View targetView, bool fullEditor, AndroidTextInputSource owner)
                : base(targetView, fullEditor)
            {
                Owner = owner;
            }

            protected ManagedInputConnection(IntPtr javaReference, JniHandleOwnership transfer)
                : base(javaReference, transfer)
            {
            }

            public virtual void Invalidate()
            {
                isValid = false;
            }

            public override bool PerformEditorAction(ImeAction editorAction)
            {
                if (!IsCurrent || Owner == null)
                    return false;

                Owner.emitKeyPress(Keycode.Enter);
                return true;
            }

            public override bool SendKeyEvent(KeyEvent? e)
            {
                if (!IsCurrent || Owner == null || e == null)
                    return false;

                Owner.handleKeyEvent(e);
                return true;
            }
        }

        /// <summary>
        /// Stateless <see cref="BaseInputConnection"/> (<c>fullEditor: false</c>) dedicated to password entry so
        /// plaintext password characters are never retained in an Android <see cref="IEditable"/> buffer.
        /// </summary>
        private sealed class PasswordInputConnection : ManagedInputConnection
        {
            private string currentComposing = string.Empty;

            public PasswordInputConnection(View targetView, AndroidTextInputSource owner)
                : base(targetView, false, owner)
            {
            }

            public PasswordInputConnection(IntPtr javaReference, JniHandleOwnership transfer)
                : base(javaReference, transfer)
            {
            }

            public override void Invalidate()
            {
                base.Invalidate();
                currentComposing = string.Empty;
            }

            public override bool CommitText(ICharSequence? text, int newCursorPosition)
            {
                if (!IsCurrent || Owner == null)
                    return false;

                bool hadComposition = currentComposing.Length > 0;
                string committed = text?.ToString() ?? string.Empty;
                currentComposing = string.Empty;

                if (hadComposition)
                    Owner.commitImeResult(committed);
                else if (committed.Length > 0)
                    Owner.commitPlainText(committed);

                return true;
            }

            public override bool SetComposingText(ICharSequence? text, int newCursorPosition)
            {
                if (!IsCurrent || Owner == null)
                    return false;

                string composing = text?.ToString() ?? string.Empty;
                currentComposing = composing;
                Owner.updateImeComposition(composing, composing.Length, 0);
                return true;
            }

            public override bool FinishComposingText()
            {
                if (!IsCurrent || Owner == null)
                    return false;

                if (currentComposing.Length > 0)
                {
                    string finalized = currentComposing;
                    currentComposing = string.Empty;
                    Owner.commitImeResult(finalized);
                }

                return true;
            }

            public override bool DeleteSurroundingText(int beforeLength, int afterLength)
            {
                if (!IsCurrent || Owner == null)
                    return false;

                Owner.deleteSurrounding(beforeLength, afterLength);
                return true;
            }

            public override bool DeleteSurroundingTextInCodePoints(int beforeLength, int afterLength)
            {
                if (!IsCurrent || Owner == null)
                    return false;

                Owner.deleteSurrounding(beforeLength, afterLength);
                return true;
            }

            public override bool PerformContextMenuAction(int id)
            {
                if (!IsCurrent || Owner == null)
                    return false;

                return Owner.handleContextMenuAction(id, allowExport: false);
            }
        }

        /// <summary>
        /// Full-editor <see cref="BaseInputConnection"/> for non-password text inputs, supporting IME composition,
        /// swipe typing, and CJK/Thai input methods.
        /// </summary>
        private sealed class NormalInputConnection : ManagedInputConnection
        {
            private string currentComposing = string.Empty;

            public NormalInputConnection(View targetView, AndroidTextInputSource owner)
                : base(targetView, true, owner)
            {
            }

            public NormalInputConnection(IntPtr javaReference, JniHandleOwnership transfer)
                : base(javaReference, transfer)
            {
            }

            public override void Invalidate()
            {
                base.Invalidate();
                currentComposing = string.Empty;
                Editable?.Clear();
            }

            public override bool CommitText(ICharSequence? text, int newCursorPosition)
            {
                if (!IsCurrent || Owner == null)
                    return false;

                bool hadComposition = currentComposing.Length > 0;
                base.CommitText(text, newCursorPosition);

                string committed = text?.ToString() ?? string.Empty;
                currentComposing = string.Empty;

                if (hadComposition)
                    Owner.commitImeResult(committed);
                else if (committed.Length > 0)
                    Owner.commitPlainText(committed);

                return true;
            }

            public override bool SetComposingText(ICharSequence? text, int newCursorPosition)
            {
                if (!IsCurrent || Owner == null)
                    return false;

                base.SetComposingText(text, newCursorPosition);

                string composing = text?.ToString() ?? string.Empty;
                currentComposing = composing;
                Owner.updateImeComposition(composing, composing.Length, 0);
                return true;
            }

            public override bool FinishComposingText()
            {
                if (!IsCurrent || Owner == null)
                    return false;

                base.FinishComposingText();

                if (currentComposing.Length > 0)
                {
                    string finalized = currentComposing;
                    currentComposing = string.Empty;
                    Owner.commitImeResult(finalized);
                }

                return true;
            }

            public override bool SetComposingRegion(int start, int end)
            {
                if (!IsCurrent || Owner == null)
                    return false;

                if (currentComposing.Length > 0)
                {
                    string finalized = currentComposing;
                    currentComposing = string.Empty;
                    Owner.commitImeResult(finalized);
                }

                IEditable? editable = Editable;

                if (editable != null && start < end)
                {
                    string fullText = editable.ToString() ?? string.Empty;
                    int actualStart = Math.Max(0, Math.Min(start, end));
                    int actualEnd = Math.Min(fullText.Length, Math.Max(start, end));
                    int cursor = Selection.GetSelectionEnd(editable);

                    if (actualEnd > actualStart && actualEnd == cursor)
                    {
                        string regionText = fullText.Substring(actualStart, actualEnd - actualStart);
                        base.SetComposingRegion(actualStart, actualEnd);

                        Owner.deleteSurrounding(actualEnd - actualStart, 0);
                        currentComposing = regionText;
                        Owner.updateImeComposition(regionText, regionText.Length, 0);
                        return true;
                    }
                }

                return base.SetComposingRegion(start, end);
            }

            public override bool DeleteSurroundingText(int beforeLength, int afterLength)
            {
                if (!IsCurrent || Owner == null)
                    return false;

                base.DeleteSurroundingText(beforeLength, afterLength);
                Owner.deleteSurrounding(beforeLength, afterLength);
                return true;
            }

            public override bool DeleteSurroundingTextInCodePoints(int beforeLength, int afterLength)
            {
                if (!IsCurrent || Owner == null)
                    return false;

                base.DeleteSurroundingTextInCodePoints(beforeLength, afterLength);
                Owner.deleteSurrounding(beforeLength, afterLength);
                return true;
            }

            public override bool PerformContextMenuAction(int id)
            {
                if (!IsCurrent || Owner == null)
                    return false;

                return Owner.handleContextMenuAction(id, allowExport: true);
            }
        }
    }
}
