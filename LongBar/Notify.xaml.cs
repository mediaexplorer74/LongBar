﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Interop;
using System.Windows.Media.Animation;

namespace LongBar
{
    /// <summary>
    /// Interaction logic for Notify.xaml
    /// </summary>
    public partial class Notify : Window
    {
        public Notify()
        {
            InitializeComponent();
        }

        private void Window_SourceInitialized(object sender, EventArgs e)
        {
            IntPtr handle = new WindowInteropHelper(this).Handle;
            if (Slate.DWM.DwmManager.IsGlassAvailable() && LongBarMain.sett.enableGlass)
                Slate.DWM.DwmManager.EnableGlass(ref handle, IntPtr.Zero);
        }

        public new void Show()
        {
            DoubleAnimation loadAnim = (DoubleAnimation)FindResource("LoadAnim");
            loadAnim.From = this.Top + 40;
            loadAnim.To = this.Top;
            base.Show();

            this.BeginAnimation(TopProperty, loadAnim);
        }
    }
}
