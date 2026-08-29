using System.Windows;
using System.Windows.Controls;
using GDCPluginManager.Core.Models;

namespace GDCPluginManager.Client.Views;

public partial class SocialLinksPanel : UserControl
{
    public static readonly DependencyProperty SocialLinksProperty =
        DependencyProperty.Register(nameof(SocialLinks), typeof(SocialLinks), typeof(SocialLinksPanel));

    public SocialLinks? SocialLinks
    {
        get => (SocialLinks?)GetValue(SocialLinksProperty);
        set => SetValue(SocialLinksProperty, value);
    }

    public SocialLinksPanel() => InitializeComponent();
}
