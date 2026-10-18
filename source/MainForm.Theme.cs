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

internal sealed partial class MainForm
{
    private static int NormalizeTheme(int mode)
    {
        if (mode == Theme.LightMode || mode == Theme.DarkMode)
        {
            return mode;
        }

        return Theme.SystemMode;
    }

    private void SetThemeMode(int mode)
    {
        _themeMode = NormalizeTheme(mode);
        ApplyTheme();
        SettingsChanged(this, EventArgs.Empty);
    }

    private void ApplyTheme()
    {
        _dark = Theme.Resolve(_themeMode);
        Theme.Apply(this, _dark);
        ApplyTrayTheme(_dark);
        ApplyTitleBarTheme(_dark);
        UpdateThemeMenuChecks();
    }

    private void ApplyTitleBarTheme(bool dark)
    {
        if (IsHandleCreated)
        {
            Native.SetImmersiveDarkMode(Handle, dark);
        }
    }

    private void UpdateThemeMenuChecks()
    {
        _appearanceSystem.Checked = _themeMode == Theme.SystemMode;
        _appearanceLight.Checked = _themeMode == Theme.LightMode;
        _appearanceDark.Checked = _themeMode == Theme.DarkMode;
    }

    private void ApplyTrayTheme(bool dark)
    {
        ContextMenuStrip menu = _tray.ContextMenuStrip;
        if (menu == null)
        {
            return;
        }

        ApplyMenuTheme(menu, dark);
    }

    private void ApplyMenuTheme(ToolStrip menu, bool dark)
    {
        menu.Renderer = dark ? (ToolStripRenderer)_darkMenuRenderer : null;

        foreach (ToolStripItem item in menu.Items)
        {
            item.ForeColor = dark ? Theme.DarkText : SystemColors.ControlText;

            ToolStripMenuItem menuItem = item as ToolStripMenuItem;
            if (menuItem != null)
            {
                ApplyMenuTheme(menuItem.DropDown, dark);
            }
        }
    }
}
