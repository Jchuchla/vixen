﻿using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Common.Controls;
using OpenTK;
using OpenTK.Graphics.OpenGL;
using Vixen.Sys;
using VixenModules.Preview.VixenPreview.OpenGL.Constructs.Shaders;

namespace VixenModules.Preview.VixenPreview.OpenGL
{

	public partial class OpenGlPreviewForm : Form, IDisplayForm
	{
		private static NLog.Logger Logging = NLog.LogManager.GetCurrentClassLogger();

		private static int _width = 1280, _height = 720;
		private static float _focalDepth = 0;
		private static float _aspectRatio;

		private static ShaderProgram _program;
		
		private Background _background;
		private const double Fov = 45.0;
		private const float FarDistance = 4000f;
		private const float NearDistance = 1f;
		private bool _mouseDown;
		private int _prevX, _prevY;
		private Camera _camera;
		private bool _needsUpdate = true;

		public OpenGlPreviewForm(VixenPreviewData data)
		{
			Data = data;
			InitializeComponent();
			glControl.MouseWheel += GlControl_MouseWheel;
		}

		private const int CP_NOCLOSE_BUTTON = 0x200;
		protected override CreateParams CreateParams
		{
			get
			{
				CreateParams myCp = base.CreateParams;
				myCp.ClassStyle = myCp.ClassStyle | CP_NOCLOSE_BUTTON;
				return myCp;
			}
		}

		/// <summary>
		/// Clean up any resources being used.
		/// </summary>
		/// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
		protected override void Dispose(bool disposing)
		{

			glControl.MouseWheel -= GlControl_MouseWheel;
			_program.DisposeChildren = true;
			_program.Dispose();

			_background.Dispose();
			
			if (disposing && (components != null))
			{
				components.Dispose();
			}
			base.Dispose(disposing);
		}

		protected override void OnLoad(EventArgs e)
		{
			Initialize();
		}

		private void EnableFeatures()
		{
			// enable depth testing to ensure correct z-ordering of our fragments
			GL.Enable(EnableCap.DepthTest);
			GL.Enable(EnableCap.PointSprite);
			GL.Enable(EnableCap.Blend);
			GL.BlendFunc(BlendingFactorSrc.SrcAlpha, BlendingFactorDest.OneMinusSrcAlpha);
			GL.Enable(EnableCap.ProgramPointSize);
		}

		private void Initialize()
		{

			EnableFeatures();

			GL.MatrixMode(MatrixMode.Projection);
			GL.LoadIdentity();

			// compile the shader program
			_program = new ShaderProgram(VertexShader, FragmentShader);

			//InitializeBackground;
			_background = new Background(Data);
			if (_background.HasBackground)
			{
				ClientSize = new Size(_background.Width, _background.Height);
				_width = _background.Width;
				_height = _background.Height;
			}
			else
			{
				ClientSize = new Size(_width, _height);
			}

			_aspectRatio = (float)_width / _height;

			_focalDepth = (float)( 1 / Math.Tan(ConvertToRadians(Fov / 2)) * (ClientSize.Height / 2.0));

			// create our camera
			_camera = new Camera(new Vector3(ClientSize.Width / 2f, ClientSize.Height / 2f, _focalDepth), Quaternion.Identity);
			_camera.SetDirection(new Vector3(0, 0, -1));

			//RestoreWindowState();

			Logging.Info("OpenGL v {0}", GL.GetString(StringName.Version));
			Logging.Info("Vendor {0}, Renderer {1}", GL.GetString(StringName.Vendor), GL.GetString(StringName.Renderer));
			Logging.Info("Extensions {0}", GL.GetString(StringName.Extensions));
			glControl_Resize(this, EventArgs.Empty);
		}

		
		#region IDisplayForm

		public VixenPreviewData Data { get; set; }
		public void Setup()
		{

		}

		public void Close()
		{
			
		}

		public void UpdatePreview()
		{
			if (VixenSystem.Elements.ElementsHaveState)
			{
				OnRenderFrame();
				_needsUpdate = true;
			}
			else
			{
				if (_needsUpdate)
				{
					OnRenderFrame();
					_needsUpdate = false;
				}
			}
		}

		#endregion

		private void glControl_Resize(object sender, EventArgs e)
		{
			if (glControl.ClientSize.Height == 0)
				glControl.ClientSize = new Size(glControl.ClientSize.Width, 100);

			_width = glControl.ClientSize.Width;
			_height = glControl.ClientSize.Height;
			glControl.Invalidate();
			//SaveWindowState();
		}

		private void glControl_KeyDown(object sender, KeyEventArgs e)
		{
			if (e.KeyCode == Keys.I || e.KeyCode == Keys.O)
			{
				int direction = 3;
				if (e.KeyCode != Keys.O)
				{
					direction *= -5;
				}

				var distance = _camera.Position.Z + direction;
				if (distance > FarDistance || distance < NearDistance)
				{
					return;
				}

				_camera.MoveRelative(new Vector3(0, 0, direction));
				glControl.Invalidate();
			}

			
		}

		private void GlControl_MouseWheel(object sender, MouseEventArgs e)
		{
			int factor = 30;
			if ((ModifierKeys & Keys.Shift) == Keys.Shift)
			{
				factor = 10;
			}
			int direction = e.Delta * SystemInformation.MouseWheelScrollLines / factor;

			var distance = _camera.Position.Z + direction;
			Console.Out.WriteLine(distance);
			if (distance > FarDistance || distance < NearDistance)
			{
				return;
			}

			_camera.MoveRelative(new Vector3(0,0, direction));
			
			glControl.Invalidate();
		}


		private void glControl_MouseMove(object sender, MouseEventArgs e)
		{
			if (e.X == _prevX && e.Y == _prevY) return;

			// move the camera when the mouse is down
			if (_mouseDown && (e.Button == MouseButtons.Right))
			{
				float yaw = (_prevX - e.X) * 0.05f;
				_camera.Yaw(yaw);

				float pitch = (_prevY - e.Y) * 0.05f;
				_camera.Pitch(pitch);

				_prevX = e.X;
				_prevY = e.Y;

				glControl.Invalidate();
			}
			else if(_mouseDown && e.Button == MouseButtons.Left)
			{
				
				var moveFactor = (_camera.Position.Z / FarDistance) * 3;
				float yaw = (_prevX - e.X) * moveFactor;
				float pitch = (_prevY - e.Y) * moveFactor;
				_camera.Move(new Vector3(yaw, -pitch, 0f));
				
				_prevX = e.X;
				_prevY = e.Y;

				glControl.Invalidate();
			}
		}

		private void glControl_MouseDown(object sender, MouseEventArgs e)
		{
			//if (e.Button != MouseButtons.Right) return;

			// this method gets called whenever a new mouse button event happens
			_mouseDown = true;

			// if the mouse has just been clicked then we hide the cursor and store the position
			if (_mouseDown)
			{
				Cursor = Cursors.Hand;
				_prevX = e.X;
				_prevY = e.Y;
			}
		}

		private void glControl_MouseUp(object sender, MouseEventArgs e)
		{
			Cursor = DefaultCursor;
			_mouseDown = false;
		}

		private void PreviewWindow_FormClosing(object sender, System.Windows.Forms.FormClosingEventArgs e)
		{
			Close();
		}

		private void glControl_Paint(object sender, System.Windows.Forms.PaintEventArgs e)
		{
			OnRenderFrame();
		}

		private Matrix4 CreatePerspective()
		{
			var perspective = Matrix4.CreatePerspectiveFieldOfView((float)ConvertToRadians(Fov), _aspectRatio, 1f, 7500f);
			return perspective;
		}


		private void OnRenderFrame()
		{
			var perspective = CreatePerspective();
			
			glControl.MakeCurrent();
			ClearScreen();
			

			if (VixenSystem.Elements.ElementsHaveState)
			{
				var mvp = Matrix4.Identity * _camera.ViewMatrix * perspective;
				DrawPoints(mvp);
			}

			_background.Draw(perspective, _camera.ViewMatrix);
			glControl.SwapBuffers();
			glControl.Context.MakeCurrent(null);

		}

		private static void ClearScreen()
		{
			// set up the OpenGL viewport and clear both the color and depth bits
			GL.Viewport(0, 0, _width, _height);
			GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
		}

		private void DrawPoints(Matrix4 mvp)
		{
			// use our shader program
			_program.Use();

			_program["mvp"].SetValue(mvp);

			float scale = _focalDepth / _camera.Position.Z;

			var sizeScale = ((float)_width / _background.Width + (float)_height / _background.Height) / 2f;

			scale *= sizeScale;

			scale = scale >= .1f ? scale : .1f;

			Console.Out.WriteLine("Scale {0}", scale);
			_program["pointScale"].SetValue(scale);
		
			foreach (var dataDisplayItem in Data.DisplayItems)
			{
				dataDisplayItem.Shape.Draw(_program, _background.Height);
			}
		}

		private double ConvertToRadians(double angle)
		{
			return angle * Math.PI / 180;
		}

		private void SaveWindowState()
		{
			XMLProfileSettings xml = new XMLProfileSettings();
			xml.PutSetting(XMLProfileSettings.SettingType.AppSettings, string.Format("{0}/WindowHeight", Name), glControl.ClientSize.Height);
			xml.PutSetting(XMLProfileSettings.SettingType.AppSettings, string.Format("{0}/WindowWidth", Name), glControl.ClientSize.Width);

			xml.PutSetting(XMLProfileSettings.SettingType.AppSettings, string.Format("{0}/WindowLocationX", Name), Location.X);
			xml.PutSetting(XMLProfileSettings.SettingType.AppSettings, string.Format("{0}/WindowLocationY", Name), Location.Y);
			xml.PutSetting(XMLProfileSettings.SettingType.AppSettings, string.Format("{0}/CameraPositionX", Name), _camera.Position.X);
			xml.PutSetting(XMLProfileSettings.SettingType.AppSettings, string.Format("{0}/CameraPositionY", Name), _camera.Position.Y);
			xml.PutSetting(XMLProfileSettings.SettingType.AppSettings, string.Format("{0}/CameraPositionZ", Name), _camera.Position.Z);
			xml.PutSetting(XMLProfileSettings.SettingType.AppSettings, string.Format("{0}/WindowState", Name), WindowState.ToString());
		}

		private void RestoreWindowState()
		{
			WindowState = FormWindowState.Normal;
			StartPosition = FormStartPosition.WindowsDefaultBounds;
			XMLProfileSettings xml = new XMLProfileSettings();

			var desktopBounds =
				new Rectangle(
					new Point(
						xml.GetSetting(XMLProfileSettings.SettingType.AppSettings, string.Format("{0}/WindowLocationX", Name), Location.X),
						xml.GetSetting(XMLProfileSettings.SettingType.AppSettings, string.Format("{0}/WindowLocationY", Name), Location.Y)),
					new Size(
						xml.GetSetting(XMLProfileSettings.SettingType.AppSettings, string.Format("{0}/WindowWidth", Name), glControl.ClientSize.Width),
						xml.GetSetting(XMLProfileSettings.SettingType.AppSettings, string.Format("{0}/WindowHeight", Name), glControl.ClientSize.Height)));

			if (desktopBounds.Width > 50 && desktopBounds.Height > 50)
			{
				if (IsVisibleOnAnyScreen(desktopBounds))
				{
					StartPosition = FormStartPosition.Manual;
					DesktopBounds = desktopBounds;

					if (xml.GetSetting(XMLProfileSettings.SettingType.AppSettings, string.Format("{0}/WindowState", Name), "Normal").Equals("Maximized"))
					{
						WindowState = FormWindowState.Maximized;
					}
				}
				else
				{
					// this resets the upper left corner of the window to windows standards
					StartPosition = FormStartPosition.WindowsDefaultLocation;

					// we can still apply the saved size
					Size = new Size(desktopBounds.Width, desktopBounds.Height);
				}
			}

			ClientSize = new Size(desktopBounds.Width, desktopBounds.Height);

			_camera.Position = new Vector3(
				xml.GetSetting(XMLProfileSettings.SettingType.AppSettings, string.Format("{0}/CameraPositionX", Name),
					_camera.Position.X),
				xml.GetSetting(XMLProfileSettings.SettingType.AppSettings, string.Format("{0}/CameraPositionY", Name),
					_camera.Position.Y),
				xml.GetSetting(XMLProfileSettings.SettingType.AppSettings, string.Format("{0}/CameraPositionZ", Name),
					_camera.Position.Z));

		}

		private bool IsVisibleOnAnyScreen(Rectangle rect)
		{
			return Screen.AllScreens.Any(screen => screen.WorkingArea.IntersectsWith(rect));
		}

		public static string VertexShader = @"
		#version 130

		in vec3 vertexPosition;
		in vec4 vertexColor;
		
		out vec4 color;
		out float pSize;

		uniform float pointSize;
		uniform float pointScale;
		uniform mat4 mvp;

		void main(void)
		{
			color = vertexColor;

			gl_Position = mvp * vec4(vertexPosition, 1);

			gl_PointSize = pointSize * pointScale;
			
			if(pointSize < 1)
			{
				gl_PointSize = 1;
			}
			
			pSize = gl_PointSize; //pass through
			
		}
		";
		
		public static string FragmentShader = @"
		#version 330

		in vec4 color;
		in float pSize;
		out vec4 gl_FragColor;

		void main(void)
		{
			if(pSize > 1) //We only need to round points that are bigger than 1
			{
				vec2 circCoord = 2.0 * gl_PointCoord - 1.0;
				if (dot(circCoord, circCoord) > 1)
				{
					discard;
				}
			}

			gl_FragColor = color;

		}
		";

	}
}
