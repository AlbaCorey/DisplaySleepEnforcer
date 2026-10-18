/*
 * Display Sleep Enforcer - force display sleep, including while locked.
 * Copyright (C) 2026 AlbaCorey
 *
 * This program is free software: you can redistribute it and/or modify it under
 * the terms of the GNU General Public License as published by the Free Software
 * Foundation, either version 3 of the License, or (at your option) any later
 * version.
 *
 * This program is distributed in the hope that it will be useful, but WITHOUT
 * ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS
 * FOR A PARTICULAR PURPOSE. See the GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License along with
 * this program. If not, see <https://www.gnu.org/licenses/>.
 */

using System;
using System.Drawing;
using System.Windows.Forms;
using Microsoft.Win32;

internal static class Theme
{
    public const int SystemMode = 0;
    public const int LightMode = 1;
    public const int DarkMode = 2;

    public static readonly Color DarkBackground = Color.FromArgb(32, 32, 32);
    public static readonly Color DarkSurface = Color.FromArgb(45, 45, 48);
    public static readonly Color DarkText = Color.FromArgb(235, 235, 235);
    public static readonly Color DarkBorder = Color.FromArgb(82, 82, 82);
    public static readonly Color DarkButton = Color.FromArgb(51, 51, 55);
    public static readonly Color DarkHighlight = Color.FromArgb(62, 62, 66);

    public static bool IsSystemDark()
    {
        try
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", false))
            {
                if (key != null)
                {
                    object value = key.GetValue("AppsUseLightTheme");
                    if (value != null)
                    {
                        return Convert.ToInt32(value) == 0;
                    }
                }
            }
        }
        catch
        {
        }

        return false;
    }

    public static bool Resolve(int mode)
    {
        if (mode == DarkMode)
        {
            return true;
        }

        if (mode == LightMode)
        {
            return false;
        }

        return IsSystemDark();
    }

    public static void Apply(Form form, bool dark)
    {
        form.BackColor = dark ? DarkBackground : SystemColors.Control;
        form.ForeColor = dark ? DarkText : SystemColors.ControlText;

        foreach (Control control in form.Controls)
        {
            ApplyToControl(control, dark);
        }
    }

    private static void ApplyToControl(Control control, bool dark)
    {
        Button button = control as Button;
        if (button != null)
        {
            button.FlatStyle = dark ? FlatStyle.Flat : FlatStyle.Standard;
            button.UseVisualStyleBackColor = !dark;
            button.BackColor = dark ? DarkButton : SystemColors.Control;
            button.ForeColor = dark ? DarkText : SystemColors.ControlText;
            button.FlatAppearance.BorderColor = dark ? DarkBorder : SystemColors.Control;
            button.FlatAppearance.BorderSize = dark ? 1 : 0;
            button.FlatAppearance.MouseOverBackColor = dark ? DarkHighlight : SystemColors.Control;
            button.FlatAppearance.MouseDownBackColor = dark ? DarkBorder : SystemColors.Control;
        }
        else if (control is TextBox)
        {
            control.BackColor = dark ? DarkSurface : Color.White;
            control.ForeColor = dark ? DarkText : SystemColors.WindowText;
        }
        else if (control is NumericUpDown)
        {
            control.BackColor = dark ? DarkSurface : SystemColors.Window;
            control.ForeColor = dark ? DarkText : SystemColors.WindowText;
        }
        else if (control is Label)
        {
            control.BackColor = Color.Transparent;
            control.ForeColor = dark ? DarkText : SystemColors.ControlText;
        }
        else if (control is CheckBox)
        {
            control.BackColor = dark ? DarkBackground : SystemColors.Control;
            control.ForeColor = dark ? DarkText : SystemColors.ControlText;
        }
        else
        {
            control.BackColor = dark ? DarkBackground : SystemColors.Control;
            control.ForeColor = dark ? DarkText : SystemColors.ControlText;
        }

        foreach (Control child in control.Controls)
        {
            ApplyToControl(child, dark);
        }
    }
}

internal sealed class DarkColorTable : ProfessionalColorTable
{
    public override Color ToolStripDropDownBackground { get { return Theme.DarkSurface; } }
    public override Color MenuItemSelected { get { return Theme.DarkHighlight; } }
    public override Color MenuItemSelectedGradientBegin { get { return Theme.DarkHighlight; } }
    public override Color MenuItemSelectedGradientEnd { get { return Theme.DarkHighlight; } }
    public override Color MenuItemPressedGradientBegin { get { return Theme.DarkHighlight; } }
    public override Color MenuItemPressedGradientMiddle { get { return Theme.DarkHighlight; } }
    public override Color MenuItemPressedGradientEnd { get { return Theme.DarkHighlight; } }
    public override Color MenuItemBorder { get { return Theme.DarkHighlight; } }
    public override Color MenuBorder { get { return Theme.DarkBorder; } }
    public override Color CheckBackground { get { return Theme.DarkSurface; } }
    public override Color CheckSelectedBackground { get { return Theme.DarkHighlight; } }
    public override Color CheckPressedBackground { get { return Theme.DarkHighlight; } }
    public override Color ImageMarginGradientBegin { get { return Theme.DarkSurface; } }
    public override Color ImageMarginGradientMiddle { get { return Theme.DarkSurface; } }
    public override Color ImageMarginGradientEnd { get { return Theme.DarkSurface; } }
    public override Color SeparatorDark { get { return Theme.DarkBorder; } }
    public override Color SeparatorLight { get { return Theme.DarkSurface; } }
}

internal sealed class DarkMenuRenderer : ToolStripProfessionalRenderer
{
    public DarkMenuRenderer()
        : base(new DarkColorTable())
    {
    }

    protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
    {
        Rectangle bounds = e.ImageRectangle;
        using (Pen pen = new Pen(Theme.DarkText, 2f))
        {
            pen.StartCap = System.Drawing.Drawing2D.LineCap.Round;
            pen.EndCap = System.Drawing.Drawing2D.LineCap.Round;
            Point[] points = new Point[]
            {
                new Point(bounds.Left + 2, bounds.Top + bounds.Height / 2),
                new Point(bounds.Left + bounds.Width / 2, bounds.Bottom - 4),
                new Point(bounds.Right - 2, bounds.Top + 3)
            };
            e.Graphics.DrawLines(pen, points);
        }
    }
}
