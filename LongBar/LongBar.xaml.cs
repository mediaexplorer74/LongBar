﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Microsoft.Win32;
using System.Runtime.InteropServices;
using System.Reflection;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using System.IO;
using System.Windows.Threading;
using System.Diagnostics;
using System.Threading;

namespace LongBar
{
  /// <summary>
  /// Interaction logic for LongBar.xaml
  /// </summary>
  public partial class LongBarMain : Window
  {
      [DllImport("user32.dll")]
      public static extern IntPtr SendMessageW(IntPtr hWnd, UInt32 msg, UInt32 wParam, IntPtr lParam);
      [DllImport("user32.dll")]
      private static extern int FindWindowW(string className, string windowName);

    [DllImport("shell32.dll")]
    public static extern IntPtr ShellExecute(
        IntPtr hwnd,
        string lpOperation,
        string lpFile,
        string lpParameters,
        string lpDirectory,
        int nShowCmd);

    [DllImport("gdi32.dll")]
    static extern IntPtr CreateRectRgn(int nLeftRect, int nTopRect, int nRightRect,
       int nBottomRect);

    public IntPtr Handle;
    static internal Settings sett;
    private Options options;
    private string path = System.IO.Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
    //public static string userPath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"LongBar Project Group\LongBar");
    public static List<Tile> Tiles = new List<Tile>();

    public Shadow shadow = new Shadow();
    private Library library;

    internal struct Settings
    {
      public bool startup;
      public Slate.General.Sidebar.Side side;
      public string theme;
      public string locale;
      public int width;
      public bool topMost;
      public bool enableGlass;
      public bool enableShadow;
      public bool locked;
      public string[] tiles;
      public string[] heights;
      public string[] pinnedTiles;
      public bool showErrors;
      public bool overlapTaskbar;
      public string screen;
      public string path;
      public bool enableSnowFall;
      public bool enableUpdates;
      public bool debug;
      public string tileToDebug;
    }

    public LongBarMain()
    {
      InitializeComponent();
      options = new Options(this);
      this.Closed += new EventHandler(LongBar_Closed);
      this.SourceInitialized += new EventHandler(LongBar_SourceInitialized);
      this.ContentRendered += new EventHandler(LongBar_ContentRendered);
      this.MouseMove += new MouseEventHandler(LongBar_MouseMove);
      this.MouseDoubleClick += new MouseButtonEventHandler(LongBar_MouseDoubleClick);
      this.DragEnter += new DragEventHandler(LongBar_DragEnter);
      this.Drop += new DragEventHandler(LongBar_Drop);
    }

    private void LongBar_Closed(object sender, EventArgs e)
    {
        shadow.Close();

        if (Slate.General.Sidebar.Overlapped && sett.side == Slate.General.Sidebar.Side.Right)
            Slate.General.Sidebar.UnOverlapTaskbar();
      Slate.General.SystemTray.RemoveIcon();
      Slate.General.Sidebar.AppbarRemove();
      WriteSettings();

      RoutedEventArgs args = new RoutedEventArgs(UserControl.UnloadedEvent);
      foreach (Tile tile in TilesGrid.Children)
          tile.RaiseEvent(args);
      TilesGrid.Children.Clear();
    }

    private void LongBar_SourceInitialized(object sender, EventArgs e)
    {
        Handle = new WindowInteropHelper(this).Handle;
        ReadSettings();
        Slate.Themes.ThemesManager.LoadTheme(LongBar.LongBarMain.sett.path, sett.theme);
        object enableGlass = Slate.Themes.ThemesManager.GetThemeParameter(LongBar.LongBarMain.sett.path, sett.theme, "boolean", "EnableGlass");
        if (enableGlass != null && !Convert.ToBoolean(enableGlass))
            sett.enableGlass = false;
        object useSystemColor = Slate.Themes.ThemesManager.GetThemeParameter(LongBar.LongBarMain.sett.path, sett.theme, "boolean", "UseSystemGlassColor");
        if (useSystemColor != null && Convert.ToBoolean(useSystemColor))
        {
            int color;
            bool opaque;
            Slate.DWM.DwmManager.DwmGetColorizationColor(out color, out opaque);
            Bg.Fill = new SolidColorBrush(Color.FromArgb(System.Drawing.Color.FromArgb(color).A, System.Drawing.Color.FromArgb(color).R, System.Drawing.Color.FromArgb(color).G, System.Drawing.Color.FromArgb(color).B));
            Slate.General.Sidebar.DwmColorChanged += new EventHandler(SideBar_DwmColorChanged);
        }

        Slate.Localization.LocaleManager.LoadLocale(LongBar.LongBarMain.sett.path, sett.locale);

        this.Width = sett.width;
        Slate.General.SystemTray.AddIcon(this);
        this.WindowStyle = WindowStyle.ToolWindow;
        Slate.General.Sidebar.SetSidebar(this, sett.side, false, sett.overlapTaskbar, sett.screen);
        SetSide(sett.side);
        this.MaxWidth = SystemParameters.PrimaryScreenWidth / 2;
        this.MinWidth = 31;
        if (Environment.OSVersion.Version.Major == 6)
        {
            Slate.DWM.DwmManager.RemoveFromFlip3D(Handle);
            if (Environment.OSVersion.Version.Minor == 1)
            {
                Slate.DWM.DwmManager.RemoveFromAeroPeek(Handle);
            }
        }

        Slate.General.SystemTray.SidebarvisibleChanged += new Slate.General.SystemTray.SidebarvisibleChangedEventHandler(SystemTray_SidebarvisibleChanged);

        GetTiles();
    }

    void SystemTray_SidebarvisibleChanged(bool value)
    {
        if (value)
            shadow.Visibility = Visibility.Visible;
        else
            shadow.Visibility = Visibility.Collapsed;
    }

    void SideBar_DwmColorChanged(object sender, EventArgs e)
    {

        int color;
        bool opaque;
        Slate.DWM.DwmManager.DwmGetColorizationColor(out color, out opaque);
        Bg.Fill = new SolidColorBrush(Color.FromArgb(System.Drawing.Color.FromArgb(color).A, System.Drawing.Color.FromArgb(color).R, System.Drawing.Color.FromArgb(color).G, System.Drawing.Color.FromArgb(color).B));

    }

    private void LongBar_ContentRendered(object sender, EventArgs e)
    {
      OpacityMaskGradStop.BeginAnimation(GradientStop.OffsetProperty, ((DoubleAnimation)this.Resources["LoadAnimOffset"]));
      OpacityMaskGradStop1.BeginAnimation(GradientStop.OffsetProperty, ((DoubleAnimation)this.Resources["LoadAnimOffset1"]));
      this.BeginAnimation(UIElement.OpacityProperty, ((DoubleAnimation)this.Resources["DummyAnim"]));
    }

    private void LoadAnimation_Completed(object sender, EventArgs e)
    {
      if (Slate.DWM.DwmManager.IsGlassAvailable() && sett.enableGlass)
        Slate.DWM.DwmManager.EnableGlass(ref Handle, IntPtr.Zero);

      shadow.Height = this.Height;
      shadow.Top = this.Top;

      if (sett.enableShadow)
      {
          shadow.Show();
      }

      if (sett.enableSnowFall)
      {
          EnableSnowFall();
      }

      if (sett.enableUpdates)
      {

          foreach (string file in Directory.GetFiles(System.IO.Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "*.old", SearchOption.TopDirectoryOnly))
          {
              File.Delete(file);
          }

          foreach (string file in Directory.GetFiles(sett.path, "*.old", SearchOption.AllDirectories))
          {
              File.Delete(file);
          }

          ThreadStart threadStarter = delegate
          {

              Slate.Updates.UpdatesManager.UpdateInfo updateInfo = Slate.Updates.UpdatesManager.CheckForUpdates(Assembly.GetExecutingAssembly().GetName().Version.Build);
              if (updateInfo.Build != null && updateInfo.Description != null)
              {
                  TaskDialogs.UpdateDialog.ShowDialog(updateInfo.Build, updateInfo.Description);
              }
          };
          Thread thread = new Thread(threadStarter);
          thread.SetApartmentState(ApartmentState.STA);
          thread.Start();
      }
    }

    private void DummyAnimation_Completed(object sender, EventArgs e)
    {
      switch (sett.side)
      {
        case Slate.General.Sidebar.Side.Left:
          LeftSideItem.IsChecked = true;
          break;
        case Slate.General.Sidebar.Side.Right:
          RightSideItem.IsChecked = true;
          break;
      }

      LoadTilesAtStartup();
    }
 
    private void LoadTilesAtStartup()
    {
        if (!sett.debug)
        {
            if (sett.tiles != null && Tiles != null && sett.tiles.Length > 0 && Tiles.Count > 0)
            {
                for (int i = 0; i < sett.tiles.Length; i++)
                {
                    string tileName = sett.tiles[i];
                    foreach (Tile tile in Tiles)
                    {
                        if (tile.File.Substring(tile.File.LastIndexOf(@"\") + 1) == tileName)
                        {
                            double tileHeight = double.NaN;
                            if (sett.heights != null && sett.heights.Length > i)
                            {
                                if (sett.heights[i].EndsWith("M"))
                                {
                                    tileHeight = double.Parse(sett.heights[i].Replace("M", string.Empty));
                                    tile.minimized = true;
                                }
                                else
                                    tileHeight = double.Parse(sett.heights[i]);
                            }
                            if (!double.IsNaN(tileHeight))
                                tile.Load(sett.side, tileHeight);
                            else
                                tile.Load(sett.side, double.NaN);
                            if (!tile.hasErrors)
                                TilesGrid.Children.Add(tile);
                        }
                    }
                }
            }

            if (sett.pinnedTiles != null && Tiles != null && sett.pinnedTiles.Length > 0 && Tiles.Count > 0)
            {
                for (int i = 0; i < sett.pinnedTiles.Length; i++)
                {
                    foreach (Tile tile in Tiles)
                    {
                        if (tile.File.EndsWith(sett.pinnedTiles[i]))
                        {
                            tile.pinned = true;
                            tile.Load(sett.side, double.NaN);

                            tile.Header.Visibility = System.Windows.Visibility.Collapsed;
                            DockPanel.SetDock(tile.Splitter, Dock.Top);
                            ((MenuItem)tile.ContextMenu.Items[0]).IsChecked = true;

                            if (!tile.hasErrors)
                            {
                                PinGrid.Children.Add(tile);
                            }
                        }
                    }
                }
            }
        }
        else
        {
            if (Tiles.Count > 0)
            {
                Tiles[0].Load(sett.side, double.NaN);
                TilesGrid.Children.Add(Tiles[0]);
            }
        }
       /* if (sett.tiles != null && sett.heights != null && sett.tiles.Length > 0)
        {
            foreach (Tile tile in Tiles)
            {
                for (int i = 0; i < sett.tiles.Length; i++)
                {
                    string tileName = sett.tiles[i];
                    if (tile.File.Substring(tile.File.LastIndexOf(@"\") + 1) == tileName)
                    {
                        double tileHeight = double.NaN;
                        if (sett.heights != null)
                        {
                            if (sett.heights[i].EndsWith("M"))
                            {
                                tileHeight = double.Parse(sett.heights[i].Replace("M", string.Empty));
                                tile.minimized = true;
                            }
                            else
                                tileHeight = double.Parse(sett.heights[i]);
                        }
                        if (!double.IsNaN(tileHeight))
                            tile.Load(sett.side, tileHeight);
                        else
                            tile.Load(sett.side, double.NaN);
                        if (!tile.hasErrors)
                            TilesGrid.Children.Add(tile);
                    }
                }
                if (sett.pinnedTiles != null && sett.pinnedTiles.Length > 0)
                {
                    for (int n = 0; n < sett.pinnedTiles.Length; n++)
                    {
                        if (tile.File.EndsWith(sett.pinnedTiles[n]))
                        {
                            tile.pinned = true;
                            tile.Load(sett.side, double.NaN);

                            tile.Header.Visibility = System.Windows.Visibility.Collapsed;
                            DockPanel.SetDock(tile.Splitter, Dock.Top);
                            ((MenuItem)tile.ContextMenu.Items[0]).IsChecked = true;

                            if (!tile.hasErrors)
                            {
                                PinGrid.Children.Add(tile);
                            }
                        }
                    }
                }
            }
        }*/
    }

    private void GetTiles()
    {
        if (!sett.debug)
        {
            if (System.IO.Directory.Exists(sett.path + @"\Library"))
                foreach (string dir in System.IO.Directory.GetDirectories(sett.path + @"\Library"))
                {
                    string file = string.Format(@"{0}\{1}.dll", dir, System.IO.Path.GetFileName(dir));
                    if (System.IO.File.Exists(file))
                    {
                        Tiles.Add(new Tile(file));
                        if (Tiles[Tiles.Count - 1].hasErrors)
                            Tiles.RemoveAt(Tiles.Count - 1);
                        else
                        {
                            MenuItem item = new MenuItem();
                            if (Tiles[Tiles.Count - 1].Info != null)
                                item.Header = Tiles[Tiles.Count - 1].Info.Name;
                            item.Click += new RoutedEventHandler(AddTileSubItem_Click);
                            Image icon = new Image();
                            icon.Source = Tiles[Tiles.Count - 1].TitleIcon.Source;
                            icon.Width = 25;
                            icon.Height = 25;
                            item.Icon = icon;
                            AddTileItem.Items.Add(item);
                        }
                    }
                }
        }
        else
        {
            Tiles.Add(new Tile(sett.tileToDebug));
            if (Tiles[Tiles.Count - 1].hasErrors)
                Tiles.RemoveAt(Tiles.Count - 1);
            else
            {
                MenuItem item = new MenuItem();
                if (Tiles[Tiles.Count - 1].Info != null)
                    item.Header = Tiles[Tiles.Count - 1].Info.Name;
                item.Click += new RoutedEventHandler(AddTileSubItem_Click);
                Image icon = new Image();
                icon.Source = Tiles[Tiles.Count - 1].TitleIcon.Source;
                icon.Width = 25;
                icon.Height = 25;
                item.Icon = icon;
                AddTileItem.Items.Add(item);
            }
        }
    }

    public void AddTileSubItem_Click(object sender, RoutedEventArgs e)
    {
      int index = AddTileItem.Items.IndexOf(sender);
      if (!((MenuItem)AddTileItem.Items[index]).IsChecked)
      {
        Tiles[index].Load(sett.side, double.NaN);
        if (!Tiles[index].hasErrors)
        {
            TilesGrid.Children.Insert(0, Tiles[index]);
            ((MenuItem)AddTileItem.Items[index]).IsChecked = true;
        }
      }
      else
      {
        Tiles[index].Unload();
        ((MenuItem)AddTileItem.Items[index]).IsChecked = false;
      }
    }

    public static void ReadSettings()
    {
      sett.side = Slate.General.Sidebar.Side.Right;
      sett.theme = "Slate";
      sett.locale = "English";
      sett.width = 150;
      sett.topMost = false;
      sett.enableGlass = true;
      sett.enableShadow = true;
      sett.locked = false;
      sett.overlapTaskbar = false;
      sett.showErrors = true;
      sett.screen = "Primary";
      sett.path = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
      sett.enableSnowFall = false;
      sett.enableUpdates = true;

      if (System.IO.File.Exists("Settings.ini"))
      {
          string line = "";
          StreamReader reader = System.IO.File.OpenText("Settings.ini");
          while (!reader.EndOfStream)
          {
              line = reader.ReadLine();
              if (line.StartsWith("Autostart"))
              {
                  sett.startup = Convert.ToBoolean(line.Split('=')[1]);
              }

              if (line.StartsWith("Side"))
              {
                  sett.side = (Slate.General.Sidebar.Side)Convert.ToInt32(line.Split('=')[1]);
              }

              if (line.StartsWith("Theme"))
              {
                  sett.theme = line.Split('=')[1];
              }

              if (line.StartsWith("Language"))
              {
                  sett.locale = line.Split('=')[1];
              }

              if (line.StartsWith("Width"))
              {
                  sett.width = Convert.ToInt32(line.Split('=')[1]);
              }

              if (line.StartsWith("TopMost"))
              {
                  sett.topMost = Convert.ToBoolean(line.Split('=')[1]);
              }

              if (line.StartsWith("EnableGlass"))
              {
                  sett.enableGlass = Convert.ToBoolean(line.Split('=')[1]);
              }

              if (line.StartsWith("EnableShadow"))
              {
                  sett.enableShadow = Convert.ToBoolean(line.Split('=')[1]);
              }

              if (line.StartsWith("Locked"))
              {
                  sett.locked = Convert.ToBoolean(line.Split('=')[1]);
              }

              if (line.StartsWith("OverlapTaskbar"))
              {
                  sett.overlapTaskbar = Convert.ToBoolean(line.Split('=')[1]);
              }

              if (line.StartsWith("ShowErrors"))
              {
                  sett.showErrors = Convert.ToBoolean(line.Split('=')[1]);
              }

              if (line.StartsWith("Screen"))
              {
                  sett.screen = line.Split('=')[1];
              }

              if (line.StartsWith("Path"))
              {
                  if (line.Split('=')[1] != "\\")
                     sett.path = line.Split('=')[1];
              }

              if (line.StartsWith("EnableSnowFall"))
              {
                  sett.enableSnowFall = Convert.ToBoolean(line.Split('=')[1]);
              }

              if (line.StartsWith("EnableUpdates"))
              {
                  sett.enableUpdates = Convert.ToBoolean(line.Split('=')[1]);
              }

              if (line.StartsWith("Tiles"))
              {
                  string s = line.Substring(line.IndexOf(":") + 2, line.Length - line.IndexOf(":") - 2);
                  sett.tiles = s.Split(new char[] {';'},  StringSplitOptions.RemoveEmptyEntries);
              }

              if (line.StartsWith("Heights"))
              {
                  string s = line.Substring(line.IndexOf(":") + 2, line.Length - line.IndexOf(":") - 2);
                  sett.heights = s.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
              }

              if (line.StartsWith("PinnedTiles"))
              {
                  string s = line.Substring(line.IndexOf(":") + 2, line.Length - line.IndexOf(":") - 2);
                  sett.pinnedTiles = s.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
              }
          }
      }

      /*sett.startup = Properties.Settings.Default.Startup;
      sett.side = (Slate.General.Sidebar.Side)Properties.Settings.Default.Side;
      sett.theme = Properties.Settings.Default.Theme;
      sett.locale = Properties.Settings.Default.Language;
      sett.width = Properties.Settings.Default.Width;
      sett.topMost = Properties.Settings.Default.TopMost;
      sett.enableGlass = Properties.Settings.Default.EnableGlass;
      sett.locked = Properties.Settings.Default.Locked;
      sett.overlapTaskbar = Properties.Settings.Default.OverlapTaskbar;
      sett.showErrors = Properties.Settings.Default.ShowErrors;
      sett.screen = Properties.Settings.Default.Screen;

      if (Properties.Settings.Default.Tiles != null)
      {
          sett.tiles = new string[Properties.Settings.Default.Tiles.Count];
          sett.heights = new string[Properties.Settings.Default.Heights.Count];

          for (int i = 0; i < Properties.Settings.Default.Tiles.Count; i++)
              sett.tiles[i] = Properties.Settings.Default.Tiles[i];

          for (int n = 0; n < Properties.Settings.Default.Heights.Count; n++)
              sett.heights[n] = Properties.Settings.Default.Heights[n];
      }

      if (Properties.Settings.Default.PinnedTiles != null)
      {
          sett.pinnedTiles = new string[Properties.Settings.Default.PinnedTiles.Count];
          for (int i = 0; i < Properties.Settings.Default.PinnedTiles.Count; i++)
              sett.pinnedTiles[i] = Properties.Settings.Default.PinnedTiles[i];
      }
      /*try
      {
          RegistryKey key = Registry.CurrentUser.OpenSubKey("Software", RegistryKeyPermissionCheck.ReadWriteSubTree).OpenSubKey("LongBar", false);
          sett.startup = (((int)key.GetValue("Startup", 0))==1);
          sett.side = (Slate.General.Sidebar.Side)key.GetValue("Side", 0);
          sett.theme = (string)key.GetValue("Theme", "Slate");
          sett.locale = (string)key.GetValue("Locale", "English");
          sett.width = (int)key.GetValue("Width", 150);
          sett.topMost = (((int)key.GetValue("TopMost", 0)) == 1);
          sett.enableGlass = (((int)key.GetValue("EnableGlass", 0)) == 1);
          sett.locked = (((int)key.GetValue("Locked", 0)) == 1);
          sett.overlapTaskbar = (((int)key.GetValue("OverlapTaskbar", 0)) == 1);
          if (key.GetValueKind("Tiles") == RegistryValueKind.MultiString)
              sett.tiles = key.GetValue("Tiles") as string[];
          else
              sett.tiles[0] = key.GetValue("Tiles") as string;
          sett.heights = (string[])key.GetValue("Heights");
          sett.pinnedTile = (string)key.GetValue("PinnedTile");

          sett.showErrors = (((int)key.GetValue("ShowErrors", 0)) == 1);
          sett.screen = (string)key.GetValue("Screen", "Primary");
          key.Close();
      }
      catch {}*/
    }

    private void WriteSettings()
    {
      sett.width = (int)this.Width;

      if (System.IO.File.Exists("Settings.ini"))
          System.IO.File.WriteAllText("Settings.ini", "");

      StreamWriter writer = System.IO.File.AppendText("Settings.ini");

      //Array.Resize(ref sett.tiles, TilesGrid.Children.Count - 1);
      Array.Resize(ref sett.tiles, TilesGrid.Children.Count);
      //Array.Resize(ref sett.heights, TilesGrid.Children.Count - 1);
      Array.Resize(ref sett.heights, TilesGrid.Children.Count);

      //Properties.Settings.Default.Tiles = new System.Collections.Specialized.StringCollection();
      //Properties.Settings.Default.Heights = new System.Collections.Specialized.StringCollection();

      if (TilesGrid.Children.Count > 0)
      {

          for (int i = 0; i < TilesGrid.Children.Count; i++)
          {
              sett.tiles[i] = System.IO.Path.GetFileName(Tiles[Tiles.IndexOf(((Tile)TilesGrid.Children[i]))].File);
              //Properties.Settings.Default.Tiles.Add(System.IO.Path.GetFileName(Tiles[Tiles.IndexOf(((Tile)TilesGrid.Children[i]))].File));
              if (Tiles[Tiles.IndexOf(((Tile)TilesGrid.Children[i]))].minimized)
                  sett.heights[i] = Tiles[Tiles.IndexOf(((Tile)TilesGrid.Children[i]))].normalHeight.ToString() + "M";
                  //Properties.Settings.Default.Heights.Add(Tiles[Tiles.IndexOf(((Tile)TilesGrid.Children[i]))].normalHeight.ToString() + "M");
              else
                  sett.heights[i] = Tiles[Tiles.IndexOf(((Tile)TilesGrid.Children[i]))].Height.ToString();
                  //Properties.Settings.Default.Heights.Add(Tiles[Tiles.IndexOf(((Tile)TilesGrid.Children[i]))].Height.ToString());
          }
      }

      if (PinGrid.Children.Count > 0)
      {
          //Properties.Settings.Default.PinnedTiles = new System.Collections.Specialized.StringCollection();
          sett.pinnedTiles = new string[PinGrid.Children.Count];

          for (int i = 0; i < PinGrid.Children.Count; i++)
          {
              sett.pinnedTiles[i] = System.IO.Path.GetFileName(Tiles[Tiles.IndexOf(((Tile)PinGrid.Children[i]))].File);
              //Properties.Settings.Default.PinnedTiles.Add(System.IO.Path.GetFileName(Tiles[Tiles.IndexOf(((Tile)PinGrid.Children[i]))].File));
          }
      }

      writer.WriteLine("Autostart=" + sett.startup.ToString());
      writer.WriteLine("Side=" + ((int)sett.side).ToString());
      writer.WriteLine("Theme=" + sett.theme);
      writer.WriteLine("Language=" + sett.locale.ToString());
      writer.WriteLine("Width=" + sett.width.ToString());
      writer.WriteLine("TopMost=" + sett.topMost.ToString());
      writer.WriteLine("EnableGlass=" + sett.enableGlass.ToString());
      writer.WriteLine("EnableShadow=" + sett.enableShadow.ToString());
      writer.WriteLine("Locked=" + sett.locked.ToString());
      writer.WriteLine("OverlapTaskbar=" + sett.overlapTaskbar.ToString());
      writer.WriteLine("ShowErrors=" + sett.showErrors.ToString());
      writer.WriteLine("Screen=" + sett.screen.ToString());
      if (sett.path == System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location))
          writer.WriteLine(@"Path=\");
      else
          writer.WriteLine("Path=" + sett.path);

      writer.WriteLine("EnableUpdates=" + sett.enableUpdates);

      if (sett.tiles != null && sett.tiles.Length > 0)
      {
          writer.Write("Tiles: ");
          for (int i = 0; i < sett.tiles.Length; i++)
          {
              writer.Write(sett.tiles[i] + ";");
          }
          writer.WriteLine();
      }

      if (sett.heights != null && sett.heights.Length > 0)
      {
          writer.Write("Heights: ");
          for (int i = 0; i < sett.heights.Length; i++)
          {
              writer.Write(sett.heights[i] + ";");
          }
          writer.WriteLine();
      }

      if (sett.pinnedTiles != null && sett.pinnedTiles.Length > 0)
      {
          writer.Write("PinnedTiles: ");
          for (int i = 0; i < sett.pinnedTiles.Length; i++)
          {
              writer.Write(sett.pinnedTiles[i] + ";");
          }
          writer.WriteLine();
      }

      writer.Flush();
      writer.Close();

      /*if (PinGrid.Children.Count > 0)
      {
          Properties.Settings.Default.PinnedTiles = new System.Collections.Specialized.StringCollection();

          for (int i = 0; i < PinGrid.Children.Count; i++)
          {
              Properties.Settings.Default.Tiles.Add(System.IO.Path.GetFileName(Tiles[Tiles.IndexOf(((Tile)TilesGrid.Children[i]))].File));
              if (Tiles[Tiles.IndexOf(((Tile)TilesGrid.Children[i]))].minimized)
                  Properties.Settings.Default.Heights.Add(Tiles[Tiles.IndexOf(((Tile)TilesGrid.Children[i]))].normalHeight.ToString() + "M");
              else
                  Properties.Settings.Default.Heights.Add(Tiles[Tiles.IndexOf(((Tile)TilesGrid.Children[i]))].Height.ToString());
              Properties.Settings.Default.PinnedTiles.Add(System.IO.Path.GetFileName(Tiles[Tiles.IndexOf(((Tile)PinGrid.Children[i]))].File));

          }
      }*/

      /*Properties.Settings.Default.Startup = sett.startup;
      Properties.Settings.Default.Side = (int)sett.side;
      Properties.Settings.Default.Theme = sett.theme;
      Properties.Settings.Default.Language = sett.locale;
      Properties.Settings.Default.Width = sett.width;
      Properties.Settings.Default.TopMost = sett.topMost;
      Properties.Settings.Default.EnableGlass = sett.enableGlass;
      Properties.Settings.Default.Locked = sett.locked;
      Properties.Settings.Default.OverlapTaskbar = sett.overlapTaskbar;
      Properties.Settings.Default.ShowErrors = sett.showErrors;
      Properties.Settings.Default.Screen = sett.screen;
      Properties.Settings.Default.Save();
      /*try
      {
        RegistryKey key = Registry.CurrentUser.OpenSubKey("Software", RegistryKeyPermissionCheck.ReadWriteSubTree);
        key.CreateSubKey("LongBar", RegistryKeyPermissionCheck.ReadWriteSubTree);
        key = Registry.CurrentUser.OpenSubKey("Software", RegistryKeyPermissionCheck.ReadWriteSubTree).OpenSubKey("LongBar", true);
        key.SetValue("Startup", sett.startup ? 1 : 0, RegistryValueKind.DWord);
        key.SetValue("Side", ((int)sett.side), RegistryValueKind.DWord);
        key.SetValue("Theme", sett.theme, RegistryValueKind.String);
        key.SetValue("Locale", sett.locale, RegistryValueKind.String);
        key.SetValue("Width", sett.width, RegistryValueKind.DWord);
        key.SetValue("TopMost", sett.topMost ? 1 : 0, RegistryValueKind.DWord);
        key.SetValue("EnableGlass", sett.enableGlass ? 1 : 0, RegistryValueKind.DWord);
        key.SetValue("Locked", sett.locked ? 1: 0, RegistryValueKind.DWord);
        key.SetValue("OverlapTaskbar", sett.overlapTaskbar ? 1 : 0, RegistryValueKind.DWord);
        key.SetValue("Tiles", sett.tiles, RegistryValueKind.MultiString);
        key.SetValue("Version", "2.0", RegistryValueKind.String);
        key.SetValue("Heights", sett.heights, RegistryValueKind.MultiString);
        if (PinGrid.Children.Count > 0)
            sett.pinnedTile = System.IO.Path.GetFileName(Tiles[Tiles.IndexOf(((Tile)PinGrid.Children[0]))].File);
        else
            sett.pinnedTile = "None";
        key.SetValue("PinnedTile", sett.pinnedTile, RegistryValueKind.String);
        key.SetValue("ShowErrors", sett.showErrors, RegistryValueKind.DWord);
        key.SetValue("Screen", sett.screen, RegistryValueKind.String);
        //Writing tiles params //LongBar 2.1
        //foreach (Tile tile in TilesGrid.Children)
        //{
        //    key = Registry.CurrentUser.OpenSubKey("Software").OpenSubKey("LongBar").OpenSubKey(tile.Info.Name, true);
        //    if (key == null)
        //        key = Registry.CurrentUser.OpenSubKey("Software").OpenSubKey("LongBar", RegistryKeyPermissionCheck.ReadWriteSubTree).CreateSubKey(tile.Info.Name);
        //    key.SetValue("Height", tile.Height);
        //    key.SetValue("IsMinimized", tile.minimized);
        //    key.SetValue("IsPinned", tile.pinned);
        //}
        key.Close();
      }
      catch { }*/
    }

    private void LongBar_MouseMove(object sender, MouseEventArgs e)
    {
      switch (sett.side)
      {
        case Slate.General.Sidebar.Side.Right:
          if (e.GetPosition(this).X <= 5 && !sett.locked)
          {
            base.Cursor = Cursors.SizeWE;
            if (e.LeftButton == MouseButtonState.Pressed)
            {
              SendMessageW(Handle, 274, 61441, IntPtr.Zero);
              sett.width = (int)this.Width;
              if (sett.topMost)
                Slate.General.Sidebar.SizeAppbar();
              else
                Slate.General.Sidebar.SetPos();
            }
          }
          else if (base.Cursor != Cursors.Arrow)
            base.Cursor = Cursors.Arrow;
          break;
        case Slate.General.Sidebar.Side.Left:
          if (e.GetPosition(this).X >= this.Width - 5 && !sett.locked)
          {
            base.Cursor = Cursors.SizeWE;
            if (e.LeftButton == MouseButtonState.Pressed)
            {
              SendMessageW(Handle, 274, 61442, IntPtr.Zero);
              sett.width = (int)this.Width;
              if (sett.topMost)
                Slate.General.Sidebar.SizeAppbar();
              else
                  Slate.General.Sidebar.SetPos();
            }
          }
          else if (base.Cursor != Cursors.Arrow)
            base.Cursor = Cursors.Arrow;
          break;
      }
    }

    private void LongBar_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
      switch (sett.side)
      {
          case Slate.General.Sidebar.Side.Right:
          if (e.GetPosition(this).X <= 5 && !sett.locked)
          {
            this.Width = 150;
            if (sett.topMost)
                Slate.General.Sidebar.SizeAppbar();
            else
                Slate.General.Sidebar.SetPos();

            shadow.Left = this.Left - shadow.Width;
          }
          break;
          case Slate.General.Sidebar.Side.Left:
          if (e.GetPosition(this).X >= this.Width - 5 && !sett.locked)
          {
            this.Width = 150;
            if (sett.topMost)
                Slate.General.Sidebar.SizeAppbar();
            else
                Slate.General.Sidebar.SetPos();

            shadow.Left = this.Left + this.Width;
          }
          break;
      }
      if (Keyboard.IsKeyDown(Key.LeftShift))
          ShowNotification();
    }

    private void DropDownMenu_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
      Menu.IsOpen = true;
    }

    private void CloseButton_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
      this.Close();
    }

    private void CloseItem_Click(object sender, RoutedEventArgs e)
    {
      this.Close();
    }

    private void LockItem_Checked(object sender, RoutedEventArgs e)
    {
      sett.locked = true;
    }
    
    private void LockItem_Unchecked(object sender, RoutedEventArgs e)
    {
      sett.locked = false;
    }

    private void LeftSideItem_Click(object sender, RoutedEventArgs e)
    {
      if (!LeftSideItem.IsChecked)
      {
        RightSideItem.IsChecked = false;
        SetSide(Slate.General.Sidebar.Side.Left);
        sett.side = Slate.General.Sidebar.Side.Left;
        LeftSideItem.IsChecked = true;
      }
    }

    private void RightSideItem_Click(object sender, RoutedEventArgs e)
    {
      if (!RightSideItem.IsChecked)
      {
        LeftSideItem.IsChecked = false;
        SetSide(Slate.General.Sidebar.Side.Right);
        sett.side = Slate.General.Sidebar.Side.Right;
        RightSideItem.IsChecked = true;
      }
    }

    public void SetSide(Slate.General.Sidebar.Side side)
    {
      switch (side)
      {
        case Slate.General.Sidebar.Side.Left:
           Slate.General.Sidebar.SetSidebar(this, Slate.General.Sidebar.Side.Left, sett.topMost, sett.overlapTaskbar, sett.screen);
           Bg.FlowDirection = FlowDirection.RightToLeft;
           BgHighlight.FlowDirection = FlowDirection.RightToLeft;
           BgHighlight.HorizontalAlignment = HorizontalAlignment.Right;
           Highlight.FlowDirection = FlowDirection.RightToLeft;
           Highlight.HorizontalAlignment = HorizontalAlignment.Right;

           shadow.Left = this.Left + this.Width;
           shadow.FlowDirection = FlowDirection.RightToLeft;

          foreach (Tile tile in TilesGrid.Children)
              tile.ChangeSide(Slate.General.Sidebar.Side.Left);
          break;
        case Slate.General.Sidebar.Side.Right:
          Slate.General.Sidebar.SetSidebar(this, Slate.General.Sidebar.Side.Right, sett.topMost, sett.overlapTaskbar, sett.screen);
          Bg.FlowDirection = FlowDirection.LeftToRight;
          BgHighlight.FlowDirection = FlowDirection.LeftToRight;
          BgHighlight.HorizontalAlignment = HorizontalAlignment.Left;
          Highlight.FlowDirection = FlowDirection.LeftToRight;
          Highlight.HorizontalAlignment = HorizontalAlignment.Left;

          shadow.Left = this.Left - shadow.Width;
          shadow.FlowDirection = FlowDirection.LeftToRight;

          foreach (Tile tile in TilesGrid.Children)
              tile.ChangeSide(Slate.General.Sidebar.Side.Right);
          break;
      }
    }

    public void SetLocale(string locale)
    {
        Slate.Localization.LocaleManager.LoadLocale(LongBar.LongBarMain.sett.path, locale);
        Slate.General.SystemTray.SetLocale();
        foreach (Tile tile in TilesGrid.Children)
          tile.ChangeLocale(locale);
    }

    public void SetTheme(string theme)
    {
        Slate.Themes.ThemesManager.LoadTheme(LongBar.LongBarMain.sett.path, theme);

        object useSystemColor = Slate.Themes.ThemesManager.GetThemeParameter(LongBar.LongBarMain.sett.path, sett.theme, "boolean", "UseSystemGlassColor");
        if (useSystemColor != null && Convert.ToBoolean(useSystemColor))
        {
            int color;
            bool opaque;
            Slate.DWM.DwmManager.DwmGetColorizationColor(out color, out opaque);
            //HwndSource.FromHwnd(Handle).CompositionTarget.BackgroundColor = Color.FromArgb(System.Drawing.Color.FromArgb(color).A,System.Drawing.Color.FromArgb(color).R,System.Drawing.Color.FromArgb(color).G,System.Drawing.Color.FromArgb(color).B);
            Bg.Fill = new SolidColorBrush(Color.FromArgb(System.Drawing.Color.FromArgb(color).A, System.Drawing.Color.FromArgb(color).R, System.Drawing.Color.FromArgb(color).G, System.Drawing.Color.FromArgb(color).B));
            Slate.General.Sidebar.DwmColorChanged += new EventHandler(SideBar_DwmColorChanged);
        }
        else
        {
            Bg.SetResourceReference(Rectangle.StyleProperty, "Background");
            Slate.General.Sidebar.DwmColorChanged -= new EventHandler(SideBar_DwmColorChanged);
        }

        string file = string.Format(@"{0}\{1}.theme.xaml", sett.path, theme);

        foreach (Tile tile in TilesGrid.Children)
            tile.ChangeTheme(file);
    }

    private void LockItem_Click(object sender, RoutedEventArgs e)
    {
      if (sett.locked)
      {
        LockItem.Header = TryFindResource("Lock");
        sett.locked = false;
      }
      else
      {
        LockItem.Header = TryFindResource("Unlock");
        sett.locked = true;
      }
    }

    private void SettingsItem_Click(object sender, RoutedEventArgs e)
    {
      if (options.IsVisible)
      {
        options.Activate();
        return;
      }
      options = new Options(this);
      options.ShowDialog();
    }

    private void Menu_Opened(object sender, RoutedEventArgs e)
    {
      if (sett.locked)
        LockItem.Header = TryFindResource("Unlock");
      else
        LockItem.Header = TryFindResource("Lock");

      if (TilesGrid.Children.Count == 0)
          RemoveTilesItem.IsEnabled = false;
      else
          RemoveTilesItem.IsEnabled = true;

      if (System.IO.Directory.Exists(sett.path + @"\Library") && Tiles.Count != System.IO.Directory.GetDirectories(sett.path + @"\Library").Length)
          foreach (string d in System.IO.Directory.GetDirectories(sett.path + @"\Library"))
        {
          string file = string.Format(@"{0}\{1}.dll", d, System.IO.Path.GetFileName(d));
          if(!CheckTileAdded(file))
            if (System.IO.File.Exists(file))
            {
              Tiles.Add(new Tile(file));
              if (Tiles[Tiles.Count - 1].hasErrors)
                Tiles.RemoveAt(Tiles.Count - 1);
              else
              {
                MenuItem item = new MenuItem();
                if (Tiles[Tiles.Count - 1].Info != null)
                  item.Header = Tiles[Tiles.Count - 1].Info.Name;
                item.Click += new RoutedEventHandler(AddTileSubItem_Click);
                AddTileItem.Items.Add(item);
              }
            }
        }
      for (int i = 0; i < Tiles.Count; i++)
        if (Tiles[i].isLoaded)
          ((MenuItem)AddTileItem.Items[i]).IsChecked = true;
        else
          ((MenuItem)AddTileItem.Items[i]).IsChecked = false;
      if (AddTileItem.Items.Count > 0)
        AddTileItem.IsEnabled = true;
      else
        AddTileItem.IsEnabled = false;
    }

    private bool CheckTileAdded(string file)
    {
      foreach (Tile tile in Tiles)
        if (tile.File == file)
          return true;
      return false;
    }

    private void MinimizeItem_Click(object sender, RoutedEventArgs e)
    {
        if (!Slate.General.SystemTray.SidebarVisible)
            Slate.General.SystemTray.SidebarVisible = true;
        else Slate.General.SystemTray.SidebarVisible = false;
    }

    private void LongBar_DragEnter(object sender, DragEventArgs e)
    {
      if(e.Data.GetDataPresent(DataFormats.FileDrop))
        e.Effects = DragDropEffects.Copy;
    }

    private void LongBar_Drop(object sender, DragEventArgs e)
    {
      string[] files = (string[])e.Data.GetData(DataFormats.FileDrop, true);
      if(files.Length>0)
          for (int i = 0; i < files.Length; i++)
          {
              if (files[i].EndsWith(".tile"))
              {
                  FileInfo info = new FileInfo(files[i]);
                  TaskDialogs.TileInstallDialog.ShowDialog(this, info.Name, files[i]);
              }
              if (files[i].EndsWith(".locale.xaml"))
              {
                  if (Slate.Localization.LocaleManager.InstallLocale(LongBar.LongBarMain.sett.path, files[i]))
                  {
                      MessageBox.Show("Localization was succesfully installed!", "Installing localization", MessageBoxButton.OK, MessageBoxImage.Information);
                      string name = System.IO.Path.GetFileName(files[i]);
                      sett.locale = name.Substring(0, name.IndexOf(@".locale.xaml"));
                      SetLocale(sett.locale);
                  }
                  else
                      MessageBox.Show("Can't install localization.", "Installing localization", MessageBoxButton.OK, MessageBoxImage.Error);
              }
              if (files[i].EndsWith(".theme.xaml"))
              {
                  if (Slate.Themes.ThemesManager.InstallTheme(LongBar.LongBarMain.sett.path, files[i]))
                  {
                      MessageBox.Show("Theme was succesfully installed!", "Installing theme", MessageBoxButton.OK, MessageBoxImage.Information);
                      string name = System.IO.Path.GetFileName(files[i]);
                      sett.theme = name.Substring(0, name.IndexOf(@".theme.xaml"));
                      SetTheme(sett.theme);
                  }
                  else
                      MessageBox.Show("Can't install theme.", "Installing theme", MessageBoxButton.OK, MessageBoxImage.Error);
              }
          }
    }

    private void GetTilesItem_Click(object sender, RoutedEventArgs e)
    {
        if (library != null && library.IsLoaded) 
            library.Activate();
        else
        {
            library = new Library(this);
            library.Show();
        }
        //ShellExecute(IntPtr.Zero, "open", "http://cid-820d4d5cef8566bf.skydrive.live.com/browse.aspx/LongBar%20Project/Library%202.0", "", "", 1);
    }

    private void RemoveTilesItem_Click(object sender, RoutedEventArgs e)
    {
        for (int i = 0; i < TilesGrid.Children.Count; i++)
        {
            int index = Tiles.IndexOf((Tile)TilesGrid.Children[i]);
            Tiles[index].Unload();
            ((MenuItem)AddTileItem.Items[index]).IsChecked = false;
        }
    }

      public static int GetElementIndexByYCoord(StackPanel panel, double y)
      {
          Point pos;
          for (int i = 0; i < panel.Children.Count; i++)
          {
              pos = panel.Children[i].PointToScreen(new Point(0, 0));
              if (y > pos.Y && y < pos.Y + ((FrameworkElement)panel.Children[i]).Height)
                  return i;
          }
          if (y < panel.PointToScreen(new Point(0,0)).Y)
              return -1;
          else
              return 100500;
      }

      private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
      {
          shadow.Height = this.Height;
          shadow.Top = this.Top;
          switch (sett.side)
          {
              case Slate.General.Sidebar.Side.Right:
                  shadow.Left = this.Left - shadow.Width;
                  break;
              case Slate.General.Sidebar.Side.Left:
                  shadow.Left = this.Left + this.Width;
                  break;
          }
      }

      public void EnableSnowFall()
      {
          if (SnowFallCanvas.Visibility == Visibility.Collapsed)
          {
              SnowFallCanvas.Visibility = Visibility.Visible;
              SnowFallCanvas.Width = this.Width;
              Random r = new Random(Environment.TickCount);
              for (int i = 0; i < 50; i++)
              {
                  SnowFall.SnowFlake snowFlake = new LongBar.SnowFall.SnowFlake();
                  snowFlake.SetValue(Canvas.LeftProperty, (double)r.Next((int)this.Width));
                  snowFlake.SetValue(Canvas.TopProperty, (double)r.Next((int)this.Height));
                  snowFlake.Width = 10 + r.Next(15);
                  snowFlake.Height = snowFlake.Width;
                  snowFlake.Visibility = Visibility.Visible;
                  SnowFallCanvas.Children.Add(snowFlake);
                  snowFlake.Enabled = true;
              }
          }
          /*foreach (LongBar.SnowFall.SnowFlake snowFlake in SnowFallCanvas.Children)
          {
              snowFlake.Enabled = true;
          }*/
      }

      public void DisableSnowFall()
      {
          SnowFallCanvas.Visibility = Visibility.Collapsed;
          foreach (LongBar.SnowFall.SnowFlake snowFlake in SnowFallCanvas.Children)
          {
              snowFlake.Enabled = false;
          }
          SnowFallCanvas.Children.Clear();
      }

      private void LongBar_Activated(object sender, EventArgs e)
      {
          //shadow.Activate();
      }

      private void Window_Deactivated(object sender, EventArgs e)
      {
          
      }

      public static void ShowNotification()
      {
          Notify notify = new Notify();
          notify.Left = System.Windows.Forms.SystemInformation.WorkingArea.Right - notify.Width;
          notify.Top = System.Windows.Forms.SystemInformation.WorkingArea.Bottom - notify.Height;
          notify.MouseLeftButtonDown += new MouseButtonEventHandler(notify_MouseLeftButtonDown);
          notify.Show();
      }

      static void notify_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
      {
          ((Window)sender).Close();
      }
  }
}
