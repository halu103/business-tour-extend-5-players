using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;

namespace FiveRealmsInstallerTests
{
    // Read-only smoke check: never show a form, run a message loop, click a
    // control, or invoke an install/uninstall/verify operation. Constructors
    // may read Steam locations; they do not change the game folder.
    internal static class InstallerEnglishUiSmoke
    {
        [STAThread]
        private static int Main(string[] args)
        {
            try
            {
                Require(args.Length == 3, "Expected SetupExe, UninstallExe and Version.");
                Require(Thread.CurrentThread.GetApartmentState() == ApartmentState.STA,
                    "Hidden forms must be checked on an STA thread.");
                Check(args[0], "SetupProgram", "SetupForm", "Setup", "Install", args[2]);
                Check(args[1], "UninstallProgram", "RemovalForm", "Uninstall", "Uninstall", args[2]);
                Console.WriteLine("PASS: both compiled installers use English UI culture without changing number/date culture; hidden form text and normal/minimum layouts passed.");
                return 0;
            }
            catch (Exception error)
            {
                var wrapped = error as TargetInvocationException;
                if (wrapped != null && wrapped.InnerException != null) error = wrapped.InnerException;
                Console.Error.WriteLine("FAIL: " + error.Message);
                return 1;
            }
        }

        private static void Check(string executable, string programName, string formName,
            string caption, string action, string version)
        {
            Assembly assembly = Assembly.LoadFile(Path.GetFullPath(executable));
            CultureInfo savedCulture = Thread.CurrentThread.CurrentCulture;
            CultureInfo savedUiCulture = Thread.CurrentThread.CurrentUICulture;
            try
            {
                // A non-English starting culture makes accidental reliance on
                // the developer's Windows language visible to this check.
                Thread.CurrentThread.CurrentCulture = CultureInfo.GetCultureInfo("vi-VN");
                Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo("vi-VN");
                CultureInfo numberCulture = Thread.CurrentThread.CurrentCulture;
                Type program = assembly.GetType("BusinessTourFiveRealmsInstaller." + programName, true);
                MethodInfo entry = program.GetMethod("Main", BindingFlags.Static | BindingFlags.NonPublic);
                Require(entry != null && entry.ReturnType == typeof(int), "Installer entry point is missing.");
                ParameterInfo[] parameters = entry.GetParameters();
                Require(parameters.Length == 1 && parameters[0].ParameterType == typeof(string[]),
                    "Installer entry point has an unexpected signature.");
                TextWriter originalOutput = Console.Out;
                string output;
                using (var capture = new StringWriter(CultureInfo.InvariantCulture))
                {
                    try
                    {
                        Console.SetOut(capture);
                        // --version exits before locks, payload handling or
                        // maintenance operations. No other arguments are used.
                        int exitCode = (int)entry.Invoke(null, new object[] { new[] { "--version" } });
                        Require(exitCode == 0, caption + " --version did not exit successfully.");
                        output = capture.ToString().Trim();
                    }
                    finally { Console.SetOut(originalOutput); }
                }
                Require(output == version, caption + " binary version differs from " + version + ": " + output);
                Require(Thread.CurrentThread.CurrentUICulture.Name == "en",
                    caption + " did not select English UI culture.");
                Require(Object.ReferenceEquals(Thread.CurrentThread.CurrentCulture, numberCulture),
                    caption + " changed number/date formatting culture.");

                Type type = assembly.GetType("BusinessTourFiveRealmsInstaller." + formName, true);
                using (var form = (Form)Activator.CreateInstance(type, true))
                {
                    Require(!form.Visible, caption + " constructed a visible window.");
                    form.ShowInTaskbar = false;
                    Require(form.Text == "Business Tour Five Realms - " + caption, caption + " window title changed.");
                    RequireAscii(form.Text, caption + " window title");
                    var buttons = new Dictionary<string, Button>(StringComparer.Ordinal);
                    bool folderLabel = false;
                    bool versionLabel = false;
                    foreach (Control control in form.Controls)
                    {
                        var button = control as Button;
                        var label = control as Label;
                        if (button != null)
                        {
                            RequireAscii(button.Text, caption + " button");
                            Require(!buttons.ContainsKey(button.Text), caption + " contains duplicate button captions.");
                            buttons.Add(button.Text, button);
                        }
                        if (label != null)
                        {
                            RequireAscii(label.Text, caption + " label");
                            if (label.Text == "Game folder:") folderLabel = true;
                            if (label.Text.EndsWith(" " + version, StringComparison.Ordinal)) versionLabel = true;
                        }
                    }
                    Require(folderLabel && versionLabel, caption + " is missing its folder/version label.");
                    Require(buttons.Count == 4, caption + " must contain four action buttons.");
                    foreach (string expected in new[] { "Browse...", "Check files", action, "Close" })
                        Require(buttons.ContainsKey(expected), caption + " is missing the " + expected + " button.");
                    Require(Object.ReferenceEquals(form.AcceptButton, buttons[action]), caption + " default action changed.");
                    Require(Object.ReferenceEquals(form.CancelButton, buttons["Close"]), caption + " close action changed.");

                    Size normal = form.Size;
                    CheckLayout(form, caption, normal, buttons);
                    CheckLayout(form, caption, form.MinimumSize, buttons);
                    CheckLayout(form, caption, normal, buttons);
                    Require(!form.Visible, caption + " layout check made the window visible.");
                }
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = savedCulture;
                Thread.CurrentThread.CurrentUICulture = savedUiCulture;
            }
        }

        private static void CheckLayout(Form form, string caption, Size size, Dictionary<string, Button> buttons)
        {
            Require(size.Width > 0 && size.Height > 0, caption + " has no usable minimum size.");
            form.Size = size;
            form.PerformLayout();
            Rectangle client = form.ClientRectangle;
            foreach (Control control in form.Controls)
            {
                Require(control.Width > 0 && control.Height > 0, caption + " has a collapsed control.");
                Require(control.Left >= 0 && control.Top >= 0 && control.Right <= client.Right && control.Bottom <= client.Bottom,
                    caption + " control falls outside the " + size.Width + "x" + size.Height + " window: " + control.Text);
                var label = control as Label;
                if (label != null)
                    Require(label.Width >= label.PreferredSize.Width && label.Height >= label.PreferredSize.Height,
                        caption + " label is clipped: " + label.Text);
            }
            var actionButtons = new List<Button>(buttons.Values);
            for (int first = 0; first < actionButtons.Count; first++)
            {
                Button button = actionButtons[first];
                Size textSize = TextRenderer.MeasureText(button.Text, button.Font);
                Require(textSize.Width + 12 <= button.ClientSize.Width && textSize.Height + 6 <= button.ClientSize.Height,
                    caption + " button text is clipped: " + button.Text);
                for (int second = first + 1; second < actionButtons.Count; second++)
                    Require(!button.Bounds.IntersectsWith(actionButtons[second].Bounds), caption + " buttons overlap.");
            }
        }

        private static void RequireAscii(string text, string name)
        {
            foreach (char character in text)
                Require(character <= 127, name + " contains non-English text: " + text);
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
