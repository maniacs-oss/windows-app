﻿using System;
using System.Collections.Generic;
using wallabag.ViewModels;
using Windows.ApplicationModel.Core;
using Windows.Foundation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

// Die Elementvorlage "Leere Seite" ist unter http://go.microsoft.com/fwlink/?LinkId=234238 dokumentiert.

namespace wallabag.Views
{
    /// <summary>
    /// Eine leere Seite, die eigenständig verwendet oder zu der innerhalb eines Rahmens navigiert werden kann.
    /// </summary>
    public sealed partial class ItemPage : Page
    {
        private const string _scriptName = "changeHtmlAttributes";
        private bool _isCommandBarVisible = false;

        public ItemPageViewModel ViewModel { get { return DataContext as ItemPageViewModel; } }

        public ItemPage()
        {
            this.InitializeComponent();

            CoreApplicationViewTitleBar coreTitleBar = CoreApplication.GetCurrentView().TitleBar;
            coreTitleBar.IsVisibleChanged += (s, e) => backButton.Visibility = s.IsVisible ? Visibility.Visible : Visibility.Collapsed;
        }

        private async void IncreaseFontSize(object sender, RoutedEventArgs e)
        {
            ViewModel.FontSize += 1;
            await InvokeChangeHtmlAttributesAsync();
        }
        private async void DecreaseFontSize(object sender, RoutedEventArgs e)
        {
            ViewModel.FontSize -= 1;
            await InvokeChangeHtmlAttributesAsync();
        }
        private async void ChangeColorSchemeToLight(object sender, RoutedEventArgs e)
        {
            ViewModel.ColorScheme = "light";
            await InvokeChangeHtmlAttributesAsync();
        }
        private async void ChangeColorSchemeToDark(object sender, RoutedEventArgs e)
        {
            ViewModel.ColorScheme = "dark";
            await InvokeChangeHtmlAttributesAsync();
        }
        private async void ChangeFontFamily(object sender, RoutedEventArgs e)
        {
            if (ViewModel.FontFamily.Equals("serif"))
                ViewModel.FontFamily = "sans";
            else
                ViewModel.FontFamily = "serif";
            await InvokeChangeHtmlAttributesAsync();
        }
        private async void ChangeTextAlignment(object sender, RoutedEventArgs e)
        {
            var senderButton = sender as Button;

            if (ViewModel.TextAlignment.Equals("left"))
            {
                ViewModel.TextAlignment = "justify";
                senderButton.Content = "\uE1A1";
            }
            else
            {
                ViewModel.TextAlignment = "left";
                senderButton.Content = "\uE1A2";
            }

            await InvokeChangeHtmlAttributesAsync();
        }

        private IAsyncOperation<string> InvokeChangeHtmlAttributesAsync()
        {
            var arguments = new List<string>();
            arguments.Add(ViewModel.ColorScheme);
            arguments.Add(ViewModel.FontFamily);
            arguments.Add(ViewModel.FontSize.ToString());
            arguments.Add(ViewModel.TextAlignment);

            ViewModel.UpdateBrushes();
            return HtmlViewer.InvokeScriptAsync(_scriptName, arguments);
        }

        private void openCommandsButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_isCommandBarVisible)
            {
                ShowFullCommandBarStoryboard.Begin();
                _isCommandBarVisible = true;
            }
            else
            {
                HideCommandBarStoryboard.Begin();
                _isCommandBarVisible = false;                
            }
        }
    }
}
