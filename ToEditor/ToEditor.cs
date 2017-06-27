#region The MIT License
/*
The MIT License

Copyright (c) 2008 matarillo

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE.
*/
#endregion

using System;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace Matarillo
{
    [ClassInterface(ClassInterfaceType.None)]
    [Guid("5A6771EB-A51F-47d6-A798-B0FB7D3052A9")]
    public class ToEditor : IToEditor
    {
        private string tempFile;
        private FileSystemWatcher watcher;
        private Process proc;
        private object textChangedHandler;
        private object exitedHandler;

        public bool Edit(string text)
        {
            try
            {
                string editorPath = GetEditorPathFromConfig();
                if (!File.Exists(editorPath))
                {
                    return false;
                }

                tempFile = Path.GetTempFileName();
                File.WriteAllText(tempFile, text, Encoding.UTF8);

                ProcessStartInfo info = new ProcessStartInfo(editorPath, "\"" + tempFile + "\"");
                proc = Process.Start(info);
                proc.Exited += new EventHandler(proc_Exited);
                proc.EnableRaisingEvents = true;

                watcher = new FileSystemWatcher();
                watcher.Path = Path.GetDirectoryName(tempFile);
                watcher.Filter = Path.GetFileName(tempFile);
                watcher.NotifyFilter = NotifyFilters.LastWrite;
                watcher.Changed += new FileSystemEventHandler(watcher_Changed);
                watcher.EnableRaisingEvents = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
                return false;
            }
            return true;
        }

        public void Close()
        {
            if (proc != null && !proc.HasExited)
            {
                proc.CloseMainWindow();
                proc.WaitForExit();
            }
        }

        public object OnTextChanged
        {
            get
            {
                return textChangedHandler;
            }
            set
            {
                textChangedHandler = value;
            }
        }

        public object OnExited
        {
            get
            {
                return exitedHandler;
            }
            set
            {
                exitedHandler = value;
            }
        }

        private void proc_Exited(object sender, EventArgs e)
        {
            if (watcher != null)
            {
                watcher.EnableRaisingEvents = false;
                watcher.Dispose();
                watcher = null;
            }
            Exited();
        }

        private void watcher_Changed(object sender, FileSystemEventArgs e)
        {
            try
            {
                string text = File.ReadAllText(tempFile, Encoding.UTF8);
                TextChanged(text);
            }
            catch
            {
                // ignore
            }
        }

        private static string GetEditorPathFromConfig()
        {
            const string EDITOR_KEY = "EditorPath";
            string editorPath = null;
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string path = Path.Combine(Path.Combine(appData, "ToEditor"), "app.config");
            ExeConfigurationFileMap map = new ExeConfigurationFileMap();
            map.ExeConfigFilename = path;
            Configuration cfg = ConfigurationManager.OpenMappedExeConfiguration(map, ConfigurationUserLevel.None);
            if (cfg.HasFile)
            {
                KeyValueConfigurationElement ce = cfg.AppSettings.Settings[EDITOR_KEY];
                editorPath = (ce != null) ? ce.Value : null;
            }
            if (!File.Exists(editorPath))
            {
                MessageBox.Show("エディタが選択されていません。", "ToEditor", MessageBoxButtons.OK, MessageBoxIcon.Information);
                Preference pref = new Preference();
                DialogResult dr = pref.ShowDialog();
                editorPath = pref.EditorPath;
                KeyValueConfigurationElement ce = cfg.AppSettings.Settings[EDITOR_KEY];
                if (ce == null)
                {
                    cfg.AppSettings.Settings.Add(EDITOR_KEY, editorPath);
                }
                else
                {
                    ce.Value = editorPath;
                }
                cfg.Save();
            }
            return editorPath;
        }

        private const BindingFlags CALLBACK
            = BindingFlags.IgnoreCase
            | BindingFlags.IgnoreReturn
            | BindingFlags.Instance
            | BindingFlags.InvokeMethod
            | BindingFlags.Public;

        private void Exited()
        {
            if (proc != null)
            {
                proc.Dispose();
                proc = null;
            }
            if (exitedHandler == null)
            {
                return;
            }
            Type t = exitedHandler.GetType();
            try
            {
                t.InvokeMember("[DispID=0]", CALLBACK, null, exitedHandler, new object[] { });
            }
            catch
            {
                // ignore
            }
        }

        private void TextChanged(string text)
        {
            if (textChangedHandler == null)
            {
                return;
            }
            Type t = textChangedHandler.GetType();
            try
            {
                t.InvokeMember("[DispID=0]", CALLBACK, null, textChangedHandler, new object[] { text });
            }
            catch
            {
                // ignore
            }
        }
    }
}
