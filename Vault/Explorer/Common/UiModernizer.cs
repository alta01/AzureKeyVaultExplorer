namespace Microsoft.Vault.Explorer.Common
{
    using System.Drawing;
    using System.Reflection;
    using System.Windows.Forms;

    internal static class UiModernizer
    {
        private static readonly Font DefaultUiFont = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);

        public static void Apply(Form form)
        {
            if (form == null)
            {
                return;
            }

            form.AutoScaleMode = AutoScaleMode.Dpi;
            form.Font = DefaultUiFont;
            ApplyControlTree(form);
        }

        private static void ApplyControlTree(Control root)
        {
            foreach (Control control in root.Controls)
            {
                switch (control)
                {
                    case Button button:
                        button.FlatStyle = FlatStyle.System;
                        break;
                    case ListView listView:
                        EnableDoubleBuffer(listView);
                        listView.FullRowSelect = true;
                        break;
                }

                ApplyControlTree(control);
            }
        }

        private static void EnableDoubleBuffer(Control control)
        {
            typeof(Control).GetProperty("DoubleBuffered", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(control, true, null);
        }
    }
}
