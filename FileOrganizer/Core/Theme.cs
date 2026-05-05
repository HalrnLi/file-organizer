namespace FileOrganizer.Core;

public static class Theme
{
    // Surface colors (darkest → lightest)
    public static Color Bg       = Color.FromArgb(26, 29, 35);    // page bg
    public static Color Surface  = Color.FromArgb(35, 38, 47);    // window/panel bg
    public static Color Surface2 = Color.FromArgb(45, 48, 56);    // input bg
    public static Color Surface3 = Color.FromArgb(55, 58, 66);    // hover state

    // Text
    public static Color Fg      = Color.FromArgb(228, 230, 234);
    public static Color Muted   = Color.FromArgb(129, 136, 150);

    // Borders
    public static Color Border      = Color.FromArgb(50, 53, 62);
    public static Color BorderFocus = Color.FromArgb(64, 67, 76);

    // Accent
    public static Color Accent     = Color.FromArgb(46, 168, 86);  // green
    public static Color AccentDim  = Color.FromArgb(35, 130, 65);

    // Semantic
    public static Color Danger     = Color.FromArgb(194, 59, 59);
    public static Color DangerDim  = Color.FromArgb(150, 40, 40);

    // Font
    public static Font UiFont      = new("Segoe UI", 12f);
    public static Font UiFontSmall = new("Segoe UI", 11f);
    public static Font UiFontMono  = new("Consolas", 11f);
    public static Font UiFontMonoSmall = new("Consolas", 10f);
    public static Font UiFontBold  = new("Segoe UI", 12f, FontStyle.Bold);

    public static void ApplyToForm(Form form)
    {
        form.BackColor = Surface;
        form.ForeColor = Fg;
    }

    /// <summary>Style a Button to match the dark theme.</summary>
    public static void StyleButton(Button btn, bool primary = false, bool danger = false)
    {
        btn.FlatStyle = FlatStyle.Flat;
        btn.FlatAppearance.BorderSize = 1;
        btn.FlatAppearance.BorderColor = Border;
        btn.FlatAppearance.MouseOverBackColor = Surface3;

        btn.Font = UiFont;
        btn.Cursor = Cursors.Hand;

        if (primary)
        {
            btn.BackColor = Accent;
            btn.ForeColor = Color.FromArgb(26, 29, 35);
            btn.FlatAppearance.BorderColor = Accent;
            btn.FlatAppearance.MouseOverBackColor = Color.FromArgb(56, 188, 96);
            btn.Font = UiFontBold;
        }
        else if (danger)
        {
            btn.ForeColor = Danger;
            btn.BackColor = Surface2;
            btn.FlatAppearance.BorderColor = Border;
            btn.FlatAppearance.MouseOverBackColor = Color.FromArgb(70, 45, 45);
        }
        else
        {
            btn.BackColor = Surface2;
            btn.ForeColor = Fg;
        }
    }

    /// <summary>Style a TextBox to match the dark theme.</summary>
    public static void StyleTextBox(TextBox tb, bool mono = false)
    {
        tb.BackColor = Surface2;
        tb.ForeColor = Fg;
        tb.BorderStyle = BorderStyle.FixedSingle;
        tb.Font = mono ? UiFontMono : UiFont;
    }

    /// <summary>Style a ComboBox to match the dark theme.</summary>
    public static void StyleComboBox(ComboBox cb)
    {
        cb.BackColor = Surface2;
        cb.ForeColor = Fg;
        cb.FlatStyle = FlatStyle.Flat;
        cb.Font = UiFont;
    }

    /// <summary>Style a Label.</summary>
    public static void StyleLabel(Label lbl, bool muted = false, bool mono = false)
    {
        lbl.ForeColor = muted ? Muted : Fg;
        lbl.Font = mono ? UiFontMonoSmall : (muted ? UiFontSmall : UiFont);
        lbl.BackColor = Color.Transparent;
    }

    /// <summary>Style a CheckBox for dark theme.</summary>
    public static void StyleCheckBox(CheckBox cb)
    {
        cb.ForeColor = Fg;
        cb.BackColor = Color.Transparent;
        cb.Font = UiFont;
        cb.FlatStyle = FlatStyle.Flat;
        cb.FlatAppearance.BorderSize = 0;
        cb.FlatAppearance.CheckedBackColor = Accent;
    }

    /// <summary>Style a ListBox for dark theme.</summary>
    public static void StyleListBox(ListBox lb)
    {
        lb.BackColor = Surface2;
        lb.ForeColor = Fg;
        lb.BorderStyle = BorderStyle.FixedSingle;
        lb.Font = UiFontMonoSmall;
    }

    /// <summary>Style a DataGridView for dark theme.</summary>
    public static void StyleDataGridView(DataGridView grid)
    {
        grid.BackgroundColor = Surface;
        grid.BorderStyle = BorderStyle.None;
        grid.GridColor = Color.FromArgb(40, 43, 50);
        grid.DefaultCellStyle.BackColor = Surface;
        grid.DefaultCellStyle.ForeColor = Fg;
        grid.DefaultCellStyle.Font = UiFont;
        grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(55, 75, 60);
        grid.DefaultCellStyle.SelectionForeColor = Accent;
        grid.ColumnHeadersDefaultCellStyle.BackColor = Surface2;
        grid.ColumnHeadersDefaultCellStyle.ForeColor = Muted;
        grid.ColumnHeadersDefaultCellStyle.Font = UiFontSmall;
        grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single;
        grid.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleLeft;
        grid.EnableHeadersVisualStyles = false;
        grid.RowHeadersVisible = false;
        grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
    }

    /// <summary>Style a ProgressBar for dark theme.</summary>
    public static void StyleProgressBar(ProgressBar pb)
    {
        pb.BackColor = Surface2;
        pb.ForeColor = Accent;
        pb.Style = ProgressBarStyle.Continuous;
    }
}
