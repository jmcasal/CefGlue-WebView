﻿using System;
using System.ComponentModel;
using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace WebViewControl {

    partial class WebView : UserControl {

        partial void ExtraInitialize() {
            Content = chromium;

            Loaded += OnLoaded;
            Unloaded += OnUnloaded;

            FocusManager.SetIsFocusScope(this, true);
            FocusManager.SetFocusedElement(this, FocusableElement);
        }

        internal IInputElement FocusableElement => chromium;

        private bool IsInDesignMode => DesignerProperties.GetIsInDesignMode(this);

        protected override void OnPreviewKeyDown(KeyEventArgs e) {
            if (AllowDeveloperTools && e.Key == Key.F12) {
                ToggleDeveloperTools();
                e.Handled = true;
            }
        }

        private void OnLoaded(object sender, RoutedEventArgs e) {
            PresentationSource.AddSourceChangedHandler(this, OnPresentationSourceChanged);
            var source = PresentationSource.FromVisual(this);
            UpdatePresentationSource(source, source); // pass same source, to make sure events are not registerer more than once
        }

        private void OnUnloaded(object sender, RoutedEventArgs e) {
            PresentationSource.RemoveSourceChangedHandler(this, OnPresentationSourceChanged);
        }

        private void OnPresentationSourceChanged(object sender, SourceChangedEventArgs e) {
            UpdatePresentationSource(e.OldSource, e.NewSource);
        }

        private void UpdatePresentationSource(PresentationSource oldSource, PresentationSource newSource) {
            if (oldSource?.RootVisual is Window oldWindow) {
                oldWindow.Closed -= OnHostWindowClosed;
            }
            if (newSource != null) {
                if (newSource?.RootVisual is Window newWindow) {
                    newWindow.Closed += OnHostWindowClosed;
                }
            }
        }

        private void OnHostWindowClosed(object sender, EventArgs e) {
            ((Window)sender).Closed -= OnHostWindowClosed;
            Dispose();
        }

        private void ForwardException(ExceptionDispatchInfo exceptionInfo) {
            // don't use invoke async, as it won't forward the exception to the dispatcher unhandled exception event
            Dispatcher.BeginInvoke((Action)(() => {
                if (!isDisposing) {
                    exceptionInfo?.Throw();
                }
            }));
        }

        private T ExecuteInUI<T>(Func<T> action) {
            return Dispatcher.Invoke(action);
        }

        private void AsyncExecuteInUI(Action action) {
            if (isDisposing) {
                return;
            }
            // use async call to avoid dead-locks, otherwise if the source action tries to to evaluate js it would block
            Dispatcher.InvokeAsync(
                () => {
                    if (!isDisposing) {
                        ExecuteWithAsyncErrorHandling(action);
                    }
                },
                DispatcherPriority.Normal,
                AsyncCancellationTokenSource.Token);
        }

        private static bool IsFrameworkAssemblyName(string name) {
            return name == "PresentationFramework" || name == "PresentationCore" || name == "mscorlib" || name == "System.Xaml" || name == "WindowsBase";
        }
    }
}