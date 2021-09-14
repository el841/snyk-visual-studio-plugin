﻿namespace Snyk.VisualStudio.Extension.UI.Toolwindow
{
    using System;
    using System.Diagnostics;
    using System.Threading;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Documents;
    using Microsoft.VisualStudio;
    using Microsoft.VisualStudio.Shell;
    using Microsoft.VisualStudio.Shell.Interop;
    using Snyk.Code.Library.Domain.Analysis;
    using Snyk.VisualStudio.Extension.CLI;
    using Snyk.VisualStudio.Extension.Service;
    using Snyk.VisualStudio.Extension.Settings;
    using Snyk.VisualStudio.Extension.Theme;
    using Snyk.VisualStudio.Extension.UI.Notifications;
    using Snyk.VisualStudio.Extension.UI.Tree;

    /// <summary>
    /// Interaction logic for SnykToolWindowControl.
    /// </summary>
    public partial class SnykToolWindowControl : UserControl
    {
        private SnykToolWindow toolWindow;

        private ISnykServiceProvider serviceProvider;

        private ToolWindowContext context;

        /// <summary>
        /// Initializes a new instance of the <see cref="SnykToolWindowControl"/> class.
        /// </summary>
        /// /// <param name="toolWindow">Tool window instance..</param>
        public SnykToolWindowControl(SnykToolWindow toolWindow)
        {
            this.toolWindow = toolWindow;

            this.InitializeComponent();

            this.context = new ToolWindowContext(this);
        }

        /// <summary>
        /// Gets a value indicating whether VulnerabilitiesTree.
        /// </summary>
        public SnykFilterableTree VulnerabilitiesTree => this.resultsTree;

        /// <summary>
        /// Initialize event listeners for UI.
        /// </summary>
        /// <param name="serviceProvider">Service provider implementation.</param>
        public void InitializeEventListeners(ISnykServiceProvider serviceProvider)
        {
            this.serviceProvider = serviceProvider;

            SnykActivityLogger logger = serviceProvider.ActivityLogger;

            logger.LogInformation("Enter InitializeEventListenersAsync() method.");

            logger.LogInformation("Initialize Solultion Event Listeners");

            SnykSolutionService solutionService = serviceProvider.SolutionService;

            solutionService.SolutionEvents.AfterCloseSolution += this.OnAfterCloseSolution;

            logger.LogInformation("Initialize CLI Event Listeners");

            SnykTasksService tasksService = serviceProvider.TasksService;

            tasksService.OssScanError += this.OnCliDisplayError;
            tasksService.SnykCodeScanError += this.OnSnykCodeDisplayError;
            tasksService.SnykCodeDisabled += this.OnSnykCodeDisabledHandler;
            tasksService.ScanningCancelled += this.OnScanningCancelled;
            tasksService.CliScanningStarted += this.OnCliScanningStarted;
            tasksService.SnykCodeScanningStarted += this.OnSnykCodeScanningStarted;
            tasksService.CliScanningUpdate += this.OnCliScanningUpdate;
            tasksService.SnykCodeScanningUpdate += this.OnSnykCodeScanningUpdate;
            //tasksService.ScanningFinished += this.OnScanningFinished;

            logger.LogInformation("Initialize Download Event Listeners");

            //tasksService.DownloadStarted += this.OnDownloadStarted;
            //tasksService.DownloadFinished += this.OnDownloadFinished;
            //tasksService.DownloadUpdate += this.OnDownloadUpdate;
            //tasksService.DownloadCancelled += this.OnDownloadCancelled;

            this.Loaded += tasksService.OnUiLoaded;

            serviceProvider.VsThemeService.ThemeChanged += this.OnVsThemeChanged;

            serviceProvider.Options.SettingsChanged += this.OnSettingsChanged;

            logger.LogInformation("Leave InitializeEventListenersAsync() method.");
        }

        /// <summary>
        /// AfterBackgroundSolutionLoadComplete event handler. Switch context to RunScanState.
        /// </summary>
        /// <param name="sender">Source object.</param>
        /// <param name="eventArgs">Event args.</param>
        public void OnAfterBackgroundSolutionLoadComplete(object sender, EventArgs eventArgs) => this.context.TransitionTo(RunScanState.Instance);

        /// <summary>
        /// AfterCloseSolution event handler. Switch context to RunScanState.
        /// </summary>
        /// <param name="sender">Source object.</param>
        /// <param name="eventArgs">Event args.</param>
        public void OnAfterCloseSolution(object sender, EventArgs eventArgs) => this.context.TransitionTo(RunScanState.Instance);

        /// <summary>
        /// Scanning update event handler. Append CLI results to tree.
        /// </summary>
        /// <param name="sender">Source object.</param>
        /// <param name="eventArgs">Event args.</param>
        public void OnCliScanningUpdate(object sender, SnykCliScanEventArgs eventArgs) => this.AppendCliResultToTree(eventArgs.Result);

        /// <summary>
        /// Scanning update event handler. Append CLI results to tree.
        /// </summary>
        /// <param name="sender">Source object.</param>
        /// <param name="eventArgs">Event args.</param>
        public void OnSnykCodeScanningUpdate(object sender, SnykCodeScanEventArgs eventArgs) => this.AppendSnykCodeResultToTree(eventArgs.Result);

        /// <summary>
        /// Cli ScanningStarted event handler..
        /// </summary>
        /// <param name="sender">Source object.</param>
        /// <param name="eventArgs">Event args.</param>
        public void OnCliScanningStarted(object sender, SnykCliScanEventArgs eventArgs)
        {
            this.DisplayMainMessage("Scanning project for vulnerabilities...");

            ThreadHelper.JoinableTaskFactory.Run(async () =>
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                this.resultsGrid.Visibility = Visibility.Visible;

                this.resultsTree.CliRootNode.SetScanningTitle();
            });
        }

        /// <summary>
        /// SnykCode ScanningStarted event handler. Switch context to ScanningState.
        /// </summary>
        /// <param name="sender">Source object.</param>
        /// <param name="eventArgs">Event args.</param>
        public void OnSnykCodeScanningStarted(object sender, SnykCodeScanEventArgs eventArgs)
        {
            this.DisplayMainMessage("Scanning project for vulnerabilities...");

            ThreadHelper.JoinableTaskFactory.Run(async () =>
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                this.resultsTree.CodeSequrityRootNode.SetScanningTitle();
                this.resultsTree.CodeQualityRootNode.SetScanningTitle();
            });
        }

        /// <summary>
        /// ScanningFinished event handler. Switch context to ScanResultsState.
        /// </summary>
        /// <param name="sender">Source object.</param>
        /// <param name="eventArgs">Event args.</param>
        public void OnScanningFinished(object sender, SnykCliScanEventArgs eventArgs) => this.context.TransitionTo(ScanResultsState.Instance);

        /// <summary>
        /// Handle Cli error.
        /// </summary>
        /// <param name="sender">Source object.</param>
        /// <param name="eventArgs">Event args.</param>
        public void OnCliDisplayError(object sender, SnykCliScanEventArgs eventArgs) => ThreadHelper.JoinableTaskFactory.Run(async () =>
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            this.resultsTree.CliRootNode.SetErrorTitle();

            NotificationService.Instance.ShowWarningInfoBar("Snyk Open Source error: " + eventArgs.Error.Message);
        });

        /// <summary>
        /// Initialize tool window control.
        /// </summary>
        /// <param name="serviceProvider">Snyk service provider instance.</param>
        public void Initialize(ISnykServiceProvider serviceProvider) => this.UpdateTreeNodeItemsState();

        /// <summary>
        /// Handle SnykCode error.
        /// </summary>
        /// <param name="sender">Source object.</param>
        /// <param name="eventArgs">Event args.</param>
        public void OnSnykCodeDisplayError(object sender, SnykCodeScanEventArgs eventArgs) => ThreadHelper.JoinableTaskFactory.Run(async () =>
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            this.resultsTree.CodeQualityRootNode.SetErrorTitle();
            this.resultsTree.CodeSequrityRootNode.SetErrorTitle();

            NotificationService.Instance.ShowWarningInfoBar("SnykCode error: " + eventArgs.Error);
        });

        /// <summary>
        /// Handle SnykCode disabled.
        /// </summary>
        /// <param name="sender">Source object.</param>
        /// <param name="eventArgs">Event args.</param>
        public void OnSnykCodeDisabledHandler(object sender, SnykCodeScanEventArgs eventArgs) => ThreadHelper.JoinableTaskFactory.Run(async () =>
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            this.resultsTree.CodeQualityRootNode.State = RootTreeNodeState.DisabledForOrganization;
            this.resultsTree.CodeSequrityRootNode.State = RootTreeNodeState.DisabledForOrganization;
        });

        /// <summary>
        /// ScanningCancelled event handler. Switch context to ScanResultsState.
        /// </summary>
        /// <param name="sender">Source object.</param>
        /// <param name="eventArgs">Event args.</param>
        public void OnScanningCancelled(object sender, SnykCliScanEventArgs eventArgs) => this.context.TransitionTo(RunScanState.Instance);

        /// <summary>
        /// DownloadStarted event handler. Switch context to DownloadState.
        /// </summary>
        /// <param name="sender">Source object.</param>
        /// <param name="eventArgs">Event args.</param>
        public void OnDownloadStarted(object sender, SnykCliDownloadEventArgs eventArgs)
        {
            if (eventArgs.IsUpdateDownload)
            {
                this.context.TransitionTo(UpdateDownloadState.Instance);
            }
            else
            {
                this.context.TransitionTo(DownloadState.Instance);
            }
        }

        /// <summary>
        /// DownloadFinished event handler. Call SetInitialState() method.
        /// </summary>
        /// <param name="sender">Source object.</param>
        /// <param name="eventArgs">Event args.</param>
        public void OnDownloadFinished(object sender, SnykCliDownloadEventArgs eventArgs) => this.SetInitialState();

        /// <summary>
        /// DownloadUpdate event handler. Call UpdateDonwloadProgress() method.
        /// </summary>
        /// <param name="sender">Source object.</param>
        /// <param name="eventArgs">Event args.</param>
        public void OnDownloadUpdate(object sender, SnykCliDownloadEventArgs eventArgs) => this.UpdateDownloadProgress(eventArgs.Progress);

        /// <summary>
        /// DownloadCancelled event handler. Call SetInitialState() method.
        /// </summary>
        /// <param name="sender">Source object.</param>
        /// <param name="eventArgs">Event args.</param>
        public void OnDownloadCancelled(object sender, SnykCliDownloadEventArgs eventArgs) => this.SetInitialState();

        /// <summary>
        /// VsThemeChanged event handler. Call Adapt methods for <see cref="HtmlRichTextBox"/> controls.
        /// </summary>
        /// <param name="sender">Source object.</param>
        /// <param name="eventArgs">Event args.</param>
        public void OnVsThemeChanged(object sender, SnykVsThemeChangedEventArgs eventArgs)
        {
            this.errorMessage.AdaptForeground();
            this.errorPath.AdaptForeground();

            //this.overview.AdaptForeground(); todo: fix theme cheged for description panel.
        }

        /// <summary>
        /// Show tool window.
        /// </summary>
        public void ShowToolWindow() => ThreadHelper.JoinableTaskFactory.Run(async () =>
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            IVsWindowFrame windowFrame = (IVsWindowFrame)toolWindow.Frame;

            ErrorHandler.ThrowOnFailure(windowFrame.Show());
        });

        /// <summary>
        /// Cancel current task by user request.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        public void CancelIfCancellationRequested(CancellationToken token)
        {
            if (token.IsCancellationRequested)
            {
                token.ThrowIfCancellationRequested();
            }
        }

        /// <summary>
        /// Hide run scan message.
        /// </summary>
        public void HideRunScanMessage() => ThreadHelper.JoinableTaskFactory.Run(async () =>
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            this.noVulnerabilitiesAddedMessageGrid.Visibility = Visibility.Collapsed;
        });

        /// <summary>
        /// Display run scan message.
        /// </summary>
        public void DisplayRunScanMessage() => ThreadHelper.JoinableTaskFactory.Run(async () =>
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            this.noVulnerabilitiesAddedMessageGrid.Visibility = Visibility.Visible;

            this.VulnerabilitiesTree.Visibility = Visibility.Visible;
        });

        /// <summary>
        /// Display main message.
        /// </summary>
        /// <param name="text">Main message text.</param>
        public void DisplayMainMessage(string text) => ThreadHelper.JoinableTaskFactory.Run(async () =>
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            this.message.Text = text;

            this.messageGrid.Visibility = Visibility.Visible;
        });

        /// <summary>
        /// Hide main message.
        /// </summary>
        public void HideMainMessage() => ThreadHelper.JoinableTaskFactory.Run(async () =>
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            this.message.Text = string.Empty;

            this.messageGrid.Visibility = Visibility.Collapsed;
        });

        /// <summary>
        /// Display error message.
        /// </summary>
        /// <param name="cliError"><see cref="CliError"/> object.</param>
        public void DisplayError(CliError cliError) => ThreadHelper.JoinableTaskFactory.Run(async () =>
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            this.errorPanel.Visibility = Visibility.Visible;

            this.errorMessage.Html = cliError.Message;
            this.errorPath.Html = cliError.Path;
        });

        /// <summary>
        /// Clean vulnerability tree and transition state to RunScanState.
        /// </summary>
        public void Clean() => ThreadHelper.JoinableTaskFactory.Run(async () =>
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            this.resultsTree.Clear();

            this.context.TransitionTo(RunScanState.Instance);

            this.UpdateTreeNodeItemsState();
        });

        /// <summary>
        /// Hide issues messages.
        /// </summary>
        public void HideIssueMessages()
        {
            this.selectIssueMessageGrid.Visibility = Visibility.Collapsed;
            this.noIssuesMessageGrid.Visibility = Visibility.Collapsed;
        }

        private void OnSettingsChanged(object sender, SnykSettingsChangedEventArgs e) => this.UpdateTreeNodeItemsState();

        private void UpdateTreeNodeItemsState() => ThreadHelper.JoinableTaskFactory.Run(async () =>
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            var options = this.serviceProvider.Options;

            this.resultsTree.CliRootNode.State = options.OssEnabled? RootTreeNodeState.Enabled : RootTreeNodeState.Disabled;

            if (!await this.serviceProvider.ApiService.IsSnyCodeEnabledAsync())
            {
                this.resultsTree.CodeQualityRootNode.State = RootTreeNodeState.DisabledForOrganization;
                this.resultsTree.CodeSequrityRootNode.State = RootTreeNodeState.DisabledForOrganization;

                this.resultsTree.Refresh();
            }
            else
            {
                this.resultsTree.CodeQualityRootNode.State = options.SnykCodeQualityEnabled ? RootTreeNodeState.Enabled : RootTreeNodeState.Disabled;
                this.resultsTree.CodeSequrityRootNode.State = options.SnykCodeSecurityEnabled ? RootTreeNodeState.Enabled : RootTreeNodeState.Disabled;
            }
        });

        /// <summary>
        /// On link click handler. It open provided link.
        /// </summary>
        /// <param name="sender">Source object.</param>
        /// <param name="e">Event args.</param>
        private void OnHyperlinkClick(object sender, RoutedEventArgs e)
        {
            var destination = ((Hyperlink)e.OriginalSource).NavigateUri;

            using (Process browser = new Process())
            {
                browser.StartInfo = new ProcessStartInfo
                {
                    FileName = destination.ToString(),
                    UseShellExecute = true,
                    ErrorDialog = true,
                };

                browser.Start();
            }
        }

        /// <summary>
        /// Append CLI results to tree.
        /// </summary>
        /// <param name="cliResult">CLI result.</param>
        private void AppendCliResultToTree(CliResult cliResult)
        {
            if (cliResult.CliVulnerabilitiesList == null)
            {
                return;
            }

            this.context.TransitionTo(ScanResultsState.Instance);

            ThreadHelper.JoinableTaskFactory.Run(async () =>
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                this.resultsTree.AppendVulnerabilities(cliResult);

                this.serviceProvider.AnalyticsService.LogOpenSourceAnalysisReadyEvent(
                    cliResult.CriticalSeverityCount,
                    cliResult.HighSeverityCount,
                    cliResult.MediumSeverityCount,
                    cliResult.LowSeverityCount);
            });
        }

        private void AppendSnykCodeResultToTree(AnalysisResult analysisResult)
        {
            this.context.TransitionTo(ScanResultsState.Instance);

            ThreadHelper.JoinableTaskFactory.Run(async () =>
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                this.resultsTree.AppendIssues(analysisResult);

                // TODO: Add SnykCode analytics event.
                // this.serviceProvider.AnalyticsService.LogOpenSourceAnalysisReadyEvent(
                //    cliResult.CriticalSeverityCount,
                //    cliResult.HighSeverityCount,
                //    cliResult.MediumSeverityCount,
                //    cliResult.LowSeverityCount);
            });
        }

        /// <summary>
        /// Update progress bar.
        /// </summary>
        /// <param name="value">Progress bar value.</param>
        private void UpdateDownloadProgress(int value) => ThreadHelper.JoinableTaskFactory.Run(async () =>
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            this.progressBar.Value = value;

            this.message.Text = $"Downloading latest Snyk CLI release {value}%...";
        });

        private void VulnerabilitiesTree_SelectetVulnerabilityChanged(object sender, RoutedEventArgs args)
        {
            ThreadHelper.JoinableTaskFactory.Run(async () =>
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                this.HideIssueMessages();

                TreeNode treeNode = null;

                if (this.resultsTree.SelectedItem is OssVulnerabilityTreeNode)
                {
                    var ossTreeNode = this.resultsTree.SelectedItem as OssVulnerabilityTreeNode;

                    treeNode = ossTreeNode;

                    var vulnerability = ossTreeNode.Vulnerability;

                    if (vulnerability != null)
                    {
                        this.descriptionPanel.Visibility = Visibility.Visible;

                        this.descriptionPanel.Vulnerability = vulnerability;

                        this.serviceProvider.AnalyticsService.LogUserSeesAnIssueEvent(vulnerability.Id, vulnerability.Severity);
                    }
                    else
                    {
                        this.descriptionPanel.Visibility = Visibility.Collapsed;
                    }
                }

                if (this.resultsTree.SelectedItem is SnykCodeVulnerabilityTreeNode)
                {
                    this.descriptionPanel.Visibility = Visibility.Visible;

                    var snykCodeTreeNode = this.resultsTree.SelectedItem as SnykCodeVulnerabilityTreeNode;

                    this.descriptionPanel.Suggestion = snykCodeTreeNode.Suggestion;

                    treeNode = snykCodeTreeNode;

                    var suggestion = snykCodeTreeNode.Suggestion;

                    VsCodeService.Instance.OpenAndNavigate(
                        this.serviceProvider.SolutionService.GetFileFullPath(suggestion.FileName),
                        suggestion.Rows.Item1 - 1,
                        suggestion.Columns.Item1 - 1,
                        suggestion.Rows.Item2 - 1,
                        suggestion.Columns.Item2);
                }

                if (treeNode == null)
                {
                    this.descriptionPanel.Visibility = Visibility.Collapsed;

                    this.selectIssueMessageGrid.Visibility = Visibility.Visible;

                    return;
                }
            });
        }

        private void RunButton_Click(object sender, RoutedEventArgs e) => SnykTasksService.Instance.Scan();

        private void StopButton_Click(object sender, RoutedEventArgs e) => SnykTasksService.Instance.CancelCurrentTask();

        private void CleanButton_Click(object sender, RoutedEventArgs e) => this.context.TransitionTo(RunScanState.Instance);

        private void SnykToolWindow_Loaded(object sender, RoutedEventArgs e)
        {
            if (this.context.IsEmptyState())
            {
                this.SetInitialState();
            }

            this.UpdateTreeNodeItemsState();
        }

        private void SetInitialState()
        {
            this.serviceProvider.AnalyticsService.AnalyticsEnabled = this.serviceProvider.Options.UsageAnalyticsEnabled;

            if (string.IsNullOrEmpty(this.serviceProvider.GetApiToken()))
            {
                this.context.TransitionTo(OverviewState.Instance);

                this.serviceProvider.AnalyticsService.LogUserLandedOnTheWelcomePageEvent();
            }
            else
            {
                this.context.TransitionTo(RunScanState.Instance);
            }
        }

        private void ConnectToSnykLink_Click(object sender, RoutedEventArgs e)
        {
            Action<string> successCallbackAction = (apiToken) =>
            {
                ThreadHelper.JoinableTaskFactory.Run(async () =>
                {
                    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                    this.connectVSToSnykLink.IsEnabled = true;

                    this.connectVSToSnykProgressBar.Visibility = Visibility.Collapsed;
                });

                this.serviceProvider.Options.ApiToken = apiToken;

                this.context.TransitionTo(RunScanState.Instance);
            };

            Action<string> errorCallbackAction = (error) =>
            {
                ThreadHelper.JoinableTaskFactory.Run(async () =>
                {
                    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                    this.connectVSToSnykLink.IsEnabled = true;

                    this.connectVSToSnykProgressBar.Visibility = Visibility.Collapsed;
                });

                this.context.TransitionTo(OverviewState.Instance);
            };

            ThreadHelper.JoinableTaskFactory.Run(async () =>
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                this.connectVSToSnykLink.IsEnabled = false;

                this.connectVSToSnykProgressBar.Visibility = Visibility.Visible;
            });

            this.serviceProvider.Options.Authenticate(successCallbackAction, errorCallbackAction);
        }
    }
}