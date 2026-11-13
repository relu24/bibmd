// bibmd in the browser: the reader on a <canvas>. The page (wwwroot/main.js) starts .NET, which
// opens the window here and returns; the browser draws it on requestAnimationFrame. The document
// comes from the page, through Viewer.Open: the file the URL names (?doc=…), or one dropped on it.
using Bibui.Platform.Browser;
using Bibui.Raster.Managed.Backend;
using Bibui.Core.Runtime;
using BibMd;

[assembly: System.Runtime.Versioning.SupportedOSPlatform("browser")]

App.RegisterFonts();
var runtime = BibuiRuntime.Create();
runtime.SetTheme(App.ThemeFor(dark: true));
var window = await BrowserWindow.OpenAsync("bibmd");
runtime.WakeUp = window.Wake;
runtime.WindowClipboard = (window.GetClipboardText, window.SetClipboardText);
Viewer.Start(runtime, window, new ManagedRenderer(window.Cpu) { DamageTracking = true });
