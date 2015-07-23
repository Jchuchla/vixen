﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Common.Resources.Properties;

namespace VixenModules.Editor.TimedSequenceEditor
{
	public partial class BulkEffectMoveForm : Form
	{
		private const string TimeFormat = @"mm\:ss\.fff";
		public BulkEffectMoveForm():this(TimeSpan.Zero)
		{
			
		}

		public BulkEffectMoveForm(TimeSpan startTime)
		{
			InitializeComponent();
			btnCancel.BackgroundImage = Resources.HeadingBackgroundImage;
			btnOk.BackgroundImage = Resources.HeadingBackgroundImage;
			Icon = Resources.Icon_Vixen3;
			Start = startTime;
			End = startTime;
			Offset = TimeSpan.Zero;
		}

		private void BulkEffectMoveForm_Load(object sender, EventArgs e)
		{
			txtStartTime.Mask = txtEndTime.Mask = txtOffset.Mask = @"00:00.000";
		}

		public TimeSpan End
		{
			get
			{
				TimeSpan endTime;
				TimeSpan.TryParseExact(txtEndTime.Text, TimeFormat, CultureInfo.InvariantCulture, out endTime);
				return endTime;
			}
			set
			{
				txtEndTime.Text = value.ToString(TimeFormat);
			}
		}
		public TimeSpan Start
		{
			set
			{
				txtStartTime.Text = value.ToString(TimeFormat);
			}
			get
			{
				TimeSpan start;
				TimeSpan.TryParseExact(txtStartTime.Text, TimeFormat, CultureInfo.InvariantCulture, out start);
				return start;
			}
		}

		public TimeSpan Offset
		{
			get
			{
				TimeSpan offset;
				TimeSpan.TryParseExact(txtOffset.Text, TimeFormat, CultureInfo.InvariantCulture, out offset);
				return offset;
			}
			set
			{
				txtOffset.Text = value.ToString(TimeFormat);
			}
		}

		public bool IsForward
		{
			get { return radioButtonForward.Checked; }
		}


		private void txtStartTime_MaskInputRejected(object sender, MaskInputRejectedEventArgs e)
		{
			if (txtStartTime.MaskFull)
			{
				toolTip.ToolTipTitle = "Invalid Time";
				toolTip.Show("You cannot enter any more data into the time field.", txtStartTime, 0, -20, 5000);
			}
			else if (e.Position == txtStartTime.Mask.Length)
			{
				toolTip.ToolTipTitle = "Invalid Time";
				toolTip.Show("You cannot add extra number to the end of this time.", txtStartTime, 0, -20, 5000);
			}
			else
			{
				toolTip.ToolTipTitle = "Invalid Time";
				toolTip.Show("You can only add numeric characters (0-9) into this time field.", txtStartTime, 0, -20, 5000);
			}
			btnOk.Enabled = false;

		}

		private void txtEndTime_MaskInputRejected(object sender, MaskInputRejectedEventArgs e)
		{
			if (txtEndTime.MaskFull)
			{
				toolTip.ToolTipTitle = "Invalid Time";
				toolTip.Show("You cannot enter any more data into the time field.", txtEndTime, 0, -20, 5000);
			}
			else if (e.Position == txtEndTime.Mask.Length)
			{
				toolTip.ToolTipTitle = "Invalid Time";
				toolTip.Show("You cannot add extra number to the end of this time.", txtEndTime, 0, -20, 5000);
			}
			else
			{
				toolTip.ToolTipTitle = "Invalid Time";
				toolTip.Show("You can only add numeric characters (0-9) into this time field.", txtEndTime, 0, -20, 5000);
			}
			btnOk.Enabled = false;
		}

		private void txtOffset_MaskInputRejected(object sender, MaskInputRejectedEventArgs e)
		{
			if (txtOffset.MaskFull)
			{
				toolTip.ToolTipTitle = "Invalid Time";
				toolTip.Show("You cannot enter any more data into the time field.", txtStartTime, 0, -20, 5000);
			}
			else if (e.Position == txtOffset.Mask.Length)
			{
				toolTip.ToolTipTitle = "Invalid Time";
				toolTip.Show("You cannot add extra numbers to the end of this time.", txtStartTime, 0, -20, 5000);
			}
			else
			{
				toolTip.ToolTipTitle = "Invalid Time";
				toolTip.Show("You can only add numeric characters (0-9) into this time field.", txtStartTime, 0, -20, 5000);
			}
			btnOk.Enabled = false;

		}

		private void txtStartTime_KeyDown(object sender, KeyEventArgs e)
		{
			toolTip.Hide(txtStartTime);
			btnOk.Enabled = true;
		}

		private void txtStartTime_KeyUp(object sender, KeyEventArgs e)
		{
			TimeSpan start;
			if (TimeSpan.TryParseExact(txtStartTime.Text, TimeFormat, CultureInfo.InvariantCulture, out start))
			{
				btnOk.Enabled = true;
			}
			else
			{
				btnOk.Enabled = false;
			}

		}

		private void txtEndTime_KeyDown(object sender, KeyEventArgs e)
		{
			toolTip.Hide(txtEndTime);

		}

		private void txtEndTime_KeyUp(object sender, KeyEventArgs e)
		{
			TimeSpan duration;
			if (TimeSpan.TryParseExact(txtEndTime.Text, TimeFormat, CultureInfo.InvariantCulture, out duration) && End >= Start)
			{
				btnOk.Enabled = true;
			}
			else
			{
				btnOk.Enabled = false;
			}
		}

		private void txtOffset_KeyDown(object sender, KeyEventArgs e)
		{
			toolTip.Hide(txtEndTime);

		}

		private void txtOffset_KeyUp(object sender, KeyEventArgs e)
		{
			TimeSpan duration;
			if (TimeSpan.TryParseExact(txtOffset.Text, TimeFormat, CultureInfo.InvariantCulture, out duration))
			{
				btnOk.Enabled = true;
			}
			else
			{
				btnOk.Enabled = false;
			}
		}

		private void buttonBackground_MouseHover(object sender, EventArgs e)
		{
			var btn = (Button)sender;
			btn.BackgroundImage = Resources.HeadingBackgroundImageHover;
		}

		private void buttonBackground_MouseLeave(object sender, EventArgs e)
		{
			var btn = (Button)sender;
			btn.BackgroundImage = Resources.HeadingBackgroundImage;
		}

		#region Draw lines and GroupBox borders
		//set color for box borders.
		private Color _borderColor = Color.FromArgb(136, 136, 136);

		public Color BorderColor
		{
			get { return _borderColor; }
			set { _borderColor = value; }
		}

		private void groupBoxes_Paint(object sender, PaintEventArgs e)
		{
			//used to draw the boards and text for the groupboxes to change the default box color.
			//get the text size in groupbox
			Size tSize = TextRenderer.MeasureText((sender as GroupBox).Text, Font);

			e.Graphics.Clear(BackColor);
			//draw the border
			Rectangle borderRect = e.ClipRectangle;
			borderRect.Y = (borderRect.Y + (tSize.Height / 2));
			borderRect.Height = (borderRect.Height - (tSize.Height / 2));
			ControlPaint.DrawBorder(e.Graphics, borderRect, _borderColor, ButtonBorderStyle.Solid);

			//draw the text
			Rectangle textRect = e.ClipRectangle;
			textRect.X = (textRect.X + 6);
			textRect.Width = tSize.Width + 10;
			textRect.Height = tSize.Height;
			e.Graphics.FillRectangle(new SolidBrush(BackColor), textRect);
			e.Graphics.DrawString((sender as GroupBox).Text, Font, new SolidBrush(Color.FromArgb(221, 221, 221)), textRect);
		}
		#endregion
	}
}
