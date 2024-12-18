using Godot;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Text;
using WinClass;
using Gtk;

public partial class GDWidget : Godot.Node
{
  [DllImport("user32.dll", CharSet = CharSet.Auto)]
  static extern long GetWindowLongA(IntPtr hWnd, int nIndex);

  [DllImport("user32.dll", CharSet = CharSet.Auto)]
  static extern long SetWindowLongA(IntPtr hWnd, int nIndex, long style);

  [DllImport("user32.dll", CharSet = CharSet.Auto)]
  static extern bool ShowWindow(IntPtr hwnd, int nCmdShow);

  [DllImport("user32.dll", CharSet = CharSet.Auto)]
  static extern bool IsWindowVisible(IntPtr hwnd);

  // Import Win32 API functions
  [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
  static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

  [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
  static extern bool UnregisterHotKey(IntPtr hWnd, int id);

  // Handle message for hotkey
  [DllImport("user32.dll", SetLastError = true)]
  static extern int GetMessage(ref MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

  [DllImport("user32.dll", SetLastError = true)]
  static extern bool TranslateMessage(ref MSG lpMsg);

  [DllImport("user32.dll", SetLastError = true)]
  static extern IntPtr DispatchMessage(ref MSG lpMsg);

  // Flag to indicate whether to keep listening for hotkeys
  private bool _keepListening = true;

  // Define hotkey constants
  private const int HOTKEY_ID = 1;

  public override void _Ready()
  {
    // Load the icon from a PNG file (or .ico file)
    IntPtr hwnd = (IntPtr)DisplayServer.WindowGetNativeHandle(DisplayServer.HandleType.WindowHandle);

    // Start GTK event loop in a separate thread
    Thread trayThread = new Thread(SetupTrayIcon);
    trayThread.IsBackground = true; // Run in background so it doesn't block the main thread
    trayThread.Start();

    SetFrameless(hwnd);
    SetOnTop(hwnd);
    SetTool(hwnd);
    // Start the hotkey listener in a separate thread
    Thread hotkeyThread = new Thread(ResgiterWinVar);
    hotkeyThread.IsBackground = true; // Run in background so it doesn't block the main thread
    hotkeyThread.Start();

    GD.Print("Finished setting up app.");
  }

  private void SetupTrayIcon()
  {
    GD.Print("Setting up tray icon...");
    Application.Init();

    # pragma warning disable 0612
    var trayIcon = new StatusIcon{
      Pixbuf = new Gdk.Pixbuf("icon.png"),
             TooltipText = "GD Widget Tray"
    };

    trayIcon.Activate += OnTrayIconActivate;
    trayIcon.PopupMenu += OnTrayIconPopupMenu;
    # pragma warning restore 0612

    Application.Run();
  }

  private void OnTrayIconActivate(object sender, EventArgs e)
  {
    GD.Print("Tray icon activated!");
    // Toggle the Godot window or perform other actions
    ToggleWindow();
  }

  [Obsolete]
  private void OnTrayIconPopupMenu(object o, EventArgs args)
  {
    GD.Print("Tray icon popup menu!");
    Menu menu = new Menu();

    // Add menu items
    MenuItem showItem = new MenuItem("Show");
    showItem.Activated += (s, e) => DisplayWindow();
    menu.Append(showItem);

    MenuItem exitItem = new MenuItem("Exit");
    exitItem.Activated += (s, e) =>
    {
      Application.Quit();
      GetTree().Quit();
    };
    menu.Append(exitItem);

    menu.ShowAll();

    // Get the position for the popup menu (GTK requires positioning)
    Gdk.Screen screen = Gdk.Screen.Default;
    Gdk.Rectangle rect = new Gdk.Rectangle(0, 0, 0, 0);

    // Popup the menu
    menu.Popup(null, null, (Menu widget, out int x, out int y, out bool pushIn) =>
    {
        Gdk.Display.Default.GetPointer(out x, out y, out _);
        pushIn = true; // Menu should be pushed into the screen if out of bounds
    }, 0, Gtk.Global.CurrentEventTime);
  }

  private void SetOnTop(IntPtr hwnd)
  {
    DisplayServer.WindowSetFlag(
        DisplayServer.WindowFlags.AlwaysOnTop,
        true
        );
  }

  private void SetFrameless(IntPtr hwnd)
  {
    DisplayServer.WindowSetFlag(
        DisplayServer.WindowFlags.Borderless,
        true
        );
  }

  private void SetTool(IntPtr hwnd)
  {
    var style = GetWindowLongA(hwnd, Constant.GWL_EXSTYLE);
    style |= Constant.WS_EX_TOOLWINDOW;
    style &= ~Constant.WS_EX_APPWINDOW;
    SetWindowLongA(hwnd, Constant.GWL_EXSTYLE, style);
  }

  public override void _ExitTree()
  {
    GD.Print("Removing global hotkey...");
    UnregisterHotKey(
        IntPtr.Zero,
        HOTKEY_ID
        );

    // Stop listening for hotkeys
    _keepListening = false;
  }

  private void ToggleWindow()
  {
    IntPtr hwnd = (IntPtr)DisplayServer.WindowGetNativeHandle(DisplayServer.HandleType.WindowHandle);
    if (IsWindowVisible(hwnd))
    {
      HideWindow(hwnd);
    }
    else
    {
      DisplayWindow(hwnd);
    }
  }

  private void DisplayWindow()
  {
    IntPtr hwnd = (IntPtr)DisplayServer.WindowGetNativeHandle(DisplayServer.HandleType.WindowHandle);
    ShowWindow(hwnd, Constant.SW_SHOW);
  }

  private void DisplayWindow(IntPtr hwnd)
  {
    ShowWindow(hwnd, Constant.SW_SHOW);
  }

  private void HideWindow(IntPtr hwnd)
  {
    ShowWindow(hwnd, Constant.SW_HIDE);
  }

  private void PrintMessage(MSG msg)
  {
    GD.Print("Window Handle: " + msg.hwnd);
    GD.Print("Message: " + msg.message);
    GD.Print("wParam: " + msg.wParam);
    GD.Print("lParam: " + msg.lParam);
    GD.Print("Time: " + msg.time);
    GD.Print("Point: " + msg.pt);
  }
  
  // Listen for hotkey press (using message loop)
  private void ResgiterWinVar()
  {
    GD.Print("Setting up global hotkey...");
    // Register Ctrl + Win + \ hotkey
    if (RegisterHotKey(
          IntPtr.Zero,
          HOTKEY_ID,
          Constant.MOD_CONTROL | Constant.MOD_WIN,
          Constant.VK_OEM_5
          ))
    {
      GD.Print("Hotkey registered successfully!");
    }
    else
    {
      GD.Print("Failed to register hotkey.");
    }

    GD.Print("Listening for hotkey press...");
    MSG msg = new MSG();

    // Keep listening for hotkey as long as the flag is true
    while (_keepListening)
    {
      int result = GetMessage(
          ref msg,
          /*hwnd,*/
          IntPtr.Zero,
          0,
          0
          );
      GD.Print("result: " + result);
      if (result != 0)
      {
        if (msg.message == Constant.WM_HOTKEY) // WM_HOTKEY message
        {
          // Check if the hotkey pressed is the one we registered
          if ((msg.lParam & 0xFFFF) == HOTKEY_ID)
          {
            ToggleWindow();
          }
        }
        TranslateMessage(ref msg);
        DispatchMessage(ref msg);
      }
    }
  }
}
