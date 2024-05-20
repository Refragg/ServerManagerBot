using System;
using Terminal.Gui;

namespace ServerManagerBot;

public partial class ServerShell : Terminal.Gui.Window
{
    private Terminal.Gui.TextView logsView;
    private Terminal.Gui.Label inputLabel;
    private Terminal.Gui.Label statusLabel;
    private Terminal.Gui.TextField inputField;
    
    private void InitializeComponent()
    {
        this.logsView = new Terminal.Gui.TextView();
        this.inputLabel = new Terminal.Gui.Label();
        this.statusLabel = new Terminal.Gui.Label();
        this.inputField = new TextField();
        
        this.Width = Dim.Fill(0);
        this.Height = Dim.Fill(0);
        this.X = 0;
        this.Y = 0;
        this.Visible = true;
        this.Modal = false;
        this.IsMdiContainer = false;
        this.Border.BorderStyle = Terminal.Gui.BorderStyle.None;
        this.Border.Effect3D = false;
        this.Border.Effect3DBrush = null;
        this.Border.DrawMarginFrame = false;
        this.TextAlignment = Terminal.Gui.TextAlignment.Left;
        this.Title = "";
        
        this.logsView.Width = Dim.Percent(100f);
        this.logsView.Height = Dim.Height(this) - 2;
        this.logsView.X = 0;
        this.logsView.Y = 0;
        this.logsView.Visible = true;
        this.logsView.ReadOnly = true;
        this.logsView.WordWrap = false;
        this.logsView.Data = "logsView";
        this.logsView.Text = "";
        this.logsView.TextAlignment = Terminal.Gui.TextAlignment.Left;
        this.logsView.AllowsTab = false;
        this.Add(this.logsView);
        
        this.inputLabel.AutoSize = true;
        this.inputLabel.Height = 1;
        this.inputLabel.X = 0;
        this.inputLabel.Y = Pos.Bottom(this) - 1;
        this.inputLabel.Visible = true;
        this.inputLabel.Data = "inputLabel";
        this.inputLabel.Text = "Input > ";
        this.inputLabel.TextAlignment = Terminal.Gui.TextAlignment.Left;
        this.Add(this.inputLabel);
        
        this.statusLabel.Width = Dim.Percent(100f);
        this.statusLabel.Height = 1;
        this.statusLabel.X = 0;
        this.statusLabel.Y = Pos.Bottom(this) - 2;
        this.statusLabel.Visible = false;
        this.statusLabel.Data = "statusLabel";
        this.statusLabel.Text = "PAUSED";
        this.statusLabel.TextAlignment = Terminal.Gui.TextAlignment.Centered;
        this.Add(this.statusLabel);
        
        this.inputField.Width = Dim.Percent(100f) - this.inputLabel.Width;
        this.inputField.Height = 1;
        this.inputField.X = Pos.Right(this.inputLabel);
        this.inputField.Y = Pos.Bottom(this) - 1;
        this.inputField.Visible = true;
        this.inputField.Data = "inputField";
        this.inputField.Text = "";
        this.inputField.TextAlignment = Terminal.Gui.TextAlignment.Left;
        this.Add(this.inputField);
    }
}