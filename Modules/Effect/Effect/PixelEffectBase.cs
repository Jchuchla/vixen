﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Runtime.Remoting.Messaging;
using System.Threading;
using NLog;
using Vixen.Attributes;
using Vixen.Data.Value;
using Vixen.Intent;
using Vixen.Sys;
using Vixen.Sys.Attribute;
using VixenModules.App.Curves;
using VixenModules.Effect.Effect.Location;
using VixenModules.EffectEditor.EffectDescriptorAttributes;
using VixenModules.Property.Location;
using VixenModules.Property.Orientation;
using VixenModules.Property.Video;

namespace VixenModules.Effect.Effect
{
	/// <summary>
	/// This class provides some additional utility functions for all the grid style efects
	/// commonly thought of as pixel effects.  
	/// </summary>
	public abstract class PixelEffectBase : BaseEffect
	{

		protected const short FrameTime = 50;
		protected static Logger Logging = LogManager.GetCurrentClassLogger();
		protected readonly List<int> StringPixelCounts = new List<int>();
		protected List<ElementLocation> ElementLocations;
        protected bool VideoElementDetected = false;
		private EffectIntents _elementData;
		private int _stringCount;
		private int _maxPixelsPerString;
		private Curve _baseLevelCurve = new Curve(CurveType.Flat100);
		private bool _elementsCached;
		private List<Element> _cachedElements;

        public void CacheElementEnumerator()
		{
			_cachedElements = TargetNodes.First()?.Distinct().ToList();
			_elementsCached = true;
		}

		public void UloadElementCache()
		{
			_cachedElements = null;
			_elementsCached = false;
		}

		protected override EffectIntents _Render()
		{
			return _elementData;
		}

		protected override void _PreRender(CancellationTokenSource tokenSource = null)
		{
			if (TargetPositioning == TargetPositioningType.Strings)
			{
				ConfigureStringBuffer();
			}
			else if(TargetPositioning == TargetPositioningType.Locations)
			{
				ConfigureVirtualBuffer();
			}
			else
			{
				ConfigureVideoBuffer();
			}
			
			SetupRender();
			int bufferSize = StringPixelCounts.Sum();
			EffectIntents data = new EffectIntents(bufferSize);
			foreach (ElementNode node in TargetNodes)
			{
				if (node != null)
					RenderNode(node, ref data);
			}
			_elementData = data;
			CleanUpRender();
			ElementLocations = null;
		}

		[ReadOnly(true)]
		[Browsable(false)]
		[ProviderCategory(@"Setup", 0)]
		[ProviderDisplayName(@"StringCount")]
		[ProviderDescription(@"StringCount")]
		[PropertyOrder(0)]
		public int StringCount
		{
			get { return _stringCount; }
			private set
			{
				_stringCount = value;
				IsDirty = true;
				OnPropertyChanged();
			}
		}

		[ReadOnly(true)]
		[Browsable(false)]
		[ProviderCategory(@"Setup", 0)]
		[ProviderDisplayName(@"PixelsPerString")]
		[ProviderDescription(@"PixelsPerString")]
		[PropertyEditor("Label")]
		[PropertyOrder(1)]
		public int MaxPixelsPerString
		{
			get { return _maxPixelsPerString; }
			private set
			{
				_maxPixelsPerString = value;
				IsDirty = true;
				OnPropertyChanged();
			}
		}

		[Value]
		[Browsable(false)]
		[ProviderCategory(@"Setup", 0)]
		[ProviderDisplayName(@"TargetPositioning")]
		[ProviderDescription(@"TargetPositioning")]
		[PropertyOrder(2)]
		public TargetPositioningType TargetPositioning
		{
			get { return EffectModuleData.TargetPositioning; }
			set
			{
				EffectModuleData.TargetPositioning = value;
				if (TargetPositioning == TargetPositioningType.Locations)
				{
					StringOrientation = StringOrientation.Vertical;
				}
				else
				{
					SetOrientation();
				}
				UpdateStringOrientationAttributes(true);
				TargetPositioningChanged();
				IsDirty = true;
				OnPropertyChanged();
			}
		}

		[ProviderCategory(@"Setup", 0)]
		[ProviderDisplayName(@"Orientation")]
		[ProviderDescription(@"Orientation")]
		[PropertyOrder(3)]
		public abstract StringOrientation StringOrientation { get; set; }

		[Browsable(false)]
		public virtual Color BaseColor { 
			get { return Color.Transparent; }
			set { }
		}

		[Browsable(false)]
		public virtual Curve BaseLevelCurve {
			get { return _baseLevelCurve; }
			set { }
		}

		[Browsable(false)]
		public virtual bool UseBaseColor { get; set; }

		/// <summary>
		/// Override to do things when the target locations changes 
		/// </summary>
		protected virtual void TargetPositioningChanged()
		{
			
		}

		protected void UpdateStringOrientationAttributes(bool refresh = false)
		{
			Dictionary<string, bool> propertyStates = new Dictionary<string, bool>(1)
			{
				{"StringOrientation", TargetPositioning.Equals(TargetPositioningType.Strings)}
			};
			SetBrowsable(propertyStates);
			if (refresh)
			{
				TypeDescriptor.Refresh(this);
			}
		}

		protected void EnableTargetPositioning(bool enable, bool refresh = false)
		{
			Dictionary<string, bool> propertyStates = new Dictionary<string, bool>(1)
			{
				{"TargetPositioning", enable}
			};
			SetBrowsable(propertyStates);
			if (refresh)
			{
				TypeDescriptor.Refresh(this);
			}
		}

		private void CalculatePixelsPerString(IEnumerable<ElementNode> nodes)
		{
			StringPixelCounts.Clear();
			foreach (var node in nodes)
			{
				StringPixelCounts.Add(node.Count());
			}
		}

		private int CalculateMaxStringCount(IEnumerable<ElementNode> nodes)
		{
			return nodes.Count();
		}

		protected IEnumerable<ElementNode> FindLeafParents()
		{
			var nodes = new List<ElementNode>();
			var nonLeafElements = Enumerable.Empty<ElementNode>();

			if (TargetNodes.FirstOrDefault() != null)
			{
				nonLeafElements = TargetNodes.SelectMany(x => x.GetNonLeafEnumerator());
				foreach (var elementNode in TargetNodes)
				{
					foreach (var leafNode in elementNode.GetLeafEnumerator())
					{
						nodes.AddRange(leafNode.Parents);
					}
				}

			}
			//Some nodes can have multiple node parents with odd groupings so this fancy linq query makes sure that the parent
			//node is part of the Target nodes lineage.
			return nodes.Distinct().Intersect(nonLeafElements);
		}

		protected override void TargetNodesChanged()
        {
            if (CheckNodesForVideoProperty())
            {
                TargetPositioning = TargetPositioningType.Video;
                return;
            }
            if (TargetPositioning == TargetPositioningType.Strings)
			{
				SetOrientation();
			}
			CalculateStringCounts();
		}

		protected void SetOrientation()
		{
			var orientation = GetOrientation();
			if (orientation.Item1)
			{
				StringOrientation = orientation.Item2;
			}
		}

		private void ConfigureVideoBuffer()
		{
			if (!_frameSize.IsEmpty)
			{
				_bufferHt = _frameSize.Width;
				_bufferWi = _frameSize.Height;
			}
		}

		private void ConfigureStringBuffer()
		{
			_bufferHt = StringCount;
			_bufferWi = MaxPixelsPerString;
			_bufferHtOffset = 0;
			_bufferWiOffset = 0;
		}

		private void CalculateStringCounts()
		{
			var nodes = FindLeafParents();
			CalculatePixelsPerString(nodes);
			MaxPixelsPerString = StringPixelCounts.Concat(new[] {0}).Max();
			StringCount = CalculateMaxStringCount(nodes);
		}

		private void ConfigureVirtualBuffer()
		{
			ElementLocations = TargetNodes.SelectMany(x => x.GetLeafEnumerator()).Select(x => new ElementLocation(x)).ToList();
			var xMax = ElementLocations.Max(p => p.X);
			var xMin = ElementLocations.Min(p => p.X);
			var yMax = ElementLocations.Max(p => p.Y);
			var yMin = ElementLocations.Min(p => p.Y);

			_bufferWi = (yMax - yMin) + 1;
			_bufferHt = (xMax - xMin) + 1;
			_bufferWiOffset = yMin;
			_bufferHtOffset = xMin;
		}

		protected int StringCountOffset { get; set; }
		protected int MaxPixelsPerStringOffset { get; set; }

		protected abstract void SetupRender();
		protected abstract void RenderEffect(int frameNum, IPixelFrameBuffer frameBuffer);

		/// <summary>
		/// Called by for effects that support location based rendering when it is enabled
		/// The normal array based logic inverts the effect data. This si the formula to convert the x, y coordinates 
		/// in order to do similar math
		/// This inverts the coordinate
		/// y = Math.Abs((BufferHtOffset - y) + (BufferHt - 1 + BufferHtOffset));
		/// 
		/// This offsets it to be zero based like the others
		///	y = y - BufferHtOffset;
		///	x = x - BufferWiOffset;
		/// See full example in Butteryfly or partial in Colorwash. 
		/// </summary>
		/// <param name="numFrames"></param>
		/// <param name="frameBuffer"></param>
		protected virtual void RenderEffectByLocation(int numFrames, PixelLocationFrameBuffer frameBuffer)
		{
			throw new NotImplementedException();
		}

		protected abstract void CleanUpRender();

		private int _bufferHt;
		protected int BufferHt
		{
			get
			{
				return StringOrientation == StringOrientation.Horizontal ? _bufferHt : _bufferWi;
			}

		}

		private int _bufferWi;
		protected int BufferWi
		{
			get
			{
				return StringOrientation == StringOrientation.Horizontal ? _bufferWi : _bufferHt;
			}
		}

        private Size _frameSize;       
        protected Size FrameSize
        {
            get
            {
                return _frameSize;
            }
        }

        private int _bufferHtOffset;
		protected int BufferHtOffset
		{
			get
			{
				return StringOrientation == StringOrientation.Horizontal ? _bufferHtOffset : _bufferWiOffset;
			}
		}

		private int _bufferWiOffset;
		protected int BufferWiOffset {
			get
			{
				return StringOrientation == StringOrientation.Horizontal ? _bufferWiOffset : _bufferHtOffset;
			}
		}


		protected EffectIntents RenderNode(ElementNode node, ref EffectIntents effectIntents)
		{
            //change to switch case and check for video type.
            switch (TargetPositioning)
            {
                case TargetPositioningType.Locations:
                    return RenderNodeByLocation(node, ref effectIntents);
                case TargetPositioningType.Video:
                    return RenderNodeByVideo(node, ref effectIntents);
                default:
                    return RenderNodeByStrings(node, ref effectIntents);
            }            
		}

		protected EffectIntents RenderNodeByLocation(ElementNode node, ref EffectIntents effectIntents)
		{
			int nFrames = GetNumberFrames();
			if (nFrames <= 0 | BufferWi == 0 || BufferHt == 0) return effectIntents;
			PixelLocationFrameBuffer buffer = new PixelLocationFrameBuffer(ElementLocations, nFrames);
			
			TimeSpan startTime = TimeSpan.Zero;

			// generate all the pixels
			RenderEffectByLocation(nFrames, buffer);

			// create the intents
			var frameTs = new TimeSpan(0, 0, 0, 0, FrameTime);

			foreach (var elementLocation in ElementLocations)
			{
				var frameData = buffer.GetFrameDataAt(elementLocation.X, elementLocation.Y);
				IIntent intent = new StaticArrayIntent<RGBValue>(frameTs, frameData, TimeSpan);
				effectIntents.AddIntentForElement(elementLocation.ElementNode.Element.Id, intent, startTime);
			}
			
			return effectIntents;
		}

		protected EffectIntents RenderNodeByStrings(ElementNode node, ref EffectIntents effectIntents)
		{
			int nFrames = GetNumberFrames();
			if (nFrames <= 0 | BufferWi==0 || BufferHt==0) return new EffectIntents();
			var buffer = new PixelFrameBuffer(BufferWi, BufferHt, UseBaseColor?BaseColor:Color.Transparent);
			int bufferSize = StringPixelCounts.Sum();			
			TimeSpan startTime = TimeSpan.Zero;

            // set up arrays to hold the generated colors
            var pixels = new RGBValue[bufferSize][];
            for (int eidx = 0; eidx < bufferSize; eidx++)
                pixels[eidx] = new RGBValue[nFrames];

            // generate all the pixels
            for (int frameNum = 0; frameNum < nFrames; frameNum++)
			{
				if (UseBaseColor)
				{
					var level = BaseLevelCurve.GetValue(GetEffectTimeIntervalPosition(frameNum)*100)/100;
					buffer.ClearBuffer(level);
				}
				else
				{
					buffer.ClearBuffer();
				}
				
				RenderEffect(frameNum, buffer);

                // peel off this frames pixels...
                if (StringOrientation == StringOrientation.Horizontal)
				{
					int i = 0;
					for (int y = 0; y < BufferHt; y++)
					{
						for (int x = 0; x < StringPixelCounts[y]; x++)
						{
							if (i >= pixels.Length) break;
							pixels[i][frameNum] = new RGBValue(buffer.GetColorAt(x,y));
							i++;
						}
					}
				}
				else
				{
					int i = 0;
					for (int x = 0; x < BufferWi; x++)
					{
						for (int y = 0; y < StringPixelCounts[x]; y++)
						{
							if (i >= pixels.Length) break;
							pixels[i][frameNum] = new RGBValue(buffer.GetColorAt(x, y));
							i++;
						}
					}
				}
			}

            var frameTs = new TimeSpan(0, 0, 0, 0, FrameTime);
            var elements = _elementsCached ? _cachedElements : node.Distinct().ToList();
            int numElements = elements.Count;

            for (int eidx = 0; eidx < numElements; eidx++)
            {
                IIntent intent = new StaticArrayIntent<RGBValue>(frameTs, pixels[eidx], TimeSpan);
                effectIntents.AddIntentForElement(elements[eidx].Id, intent, startTime);
            }

            return effectIntents;
        }

        protected EffectIntents RenderNodeByVideo(ElementNode node, ref EffectIntents effectIntents)
        {
            int nFrames = GetNumberFrames();
            if (nFrames <= 0 | BufferWi == 0 || BufferHt == 0) return new EffectIntents();
            var buffer = new PixelVideoFrameBuffer(BufferWi, BufferHt, UseBaseColor ? BaseColor : Color.Transparent);
            var updateInterval = VixenSystem.DefaultUpdateTimeSpan;
            TimeSpan startTime = TimeSpan.Zero;
            
            // set up array to hold the generated bitmaps
            BitmapValue[] frameArray = new BitmapValue[nFrames];

			// generate all the pixels in the buffer
			for (int frameNum = 0; frameNum < nFrames; frameNum++)
            {
				buffer.Reset();
				buffer.BeginUpdates();
                if (UseBaseColor)
                {
                    var level = BaseLevelCurve.GetValue(GetEffectTimeIntervalPosition(frameNum) * 100) / 100;
                    buffer.ClearBuffer(level);
                }
				else
				{
					buffer.ClearBuffer();
				}

				RenderEffect(frameNum, buffer);

				buffer.EndUpdates();
				frameArray[frameNum] = new BitmapValue(buffer.Bitmap);
            }
			
            // create the intents
            //var frameTs = new TimeSpan(0, 0, 0, 0, FrameTime);
            //If used this way, substitute FrameTime in place of UpdateInterval below.
            IIntent intent = new StaticArrayIntent<BitmapValue>(updateInterval, frameArray, TimeSpan);
            effectIntents.AddIntentForElement(node.Element.Id, intent, startTime);

            return effectIntents;
        }

        protected double GetEffectTimeIntervalPosition(int frame)
		{
			double position;
			if (TimeSpan == TimeSpan.Zero)
			{
				position = 0;
			}
			else
			{
				position = (frame * FrameTime) / TimeSpan.TotalMilliseconds;
			}
			return position;
		}

		protected int GetNumberFrames()
		{
			if (TimeSpan == TimeSpan.Zero)
			{
				return 0;
			}
			return (int)(TimeSpan.TotalMilliseconds / FrameTime);
		}

		protected double GetRemainingTime(int frame)
		{
			return (TimeSpan.TotalMilliseconds -
			        TimeSpan.TotalMilliseconds / 100 * (GetEffectTimeIntervalPosition(frame) * 100));
		}
		
		protected double CalculateAcceleration(double ratio, double accel)
		{
			if (accel == 0) return ratio;

			double pctAccel = (Math.Abs(accel) - 1.0) / 9.0;
			double newAccel1 = pctAccel * 5 + (1.0 - pctAccel) * 1.5;
			double newAccel2 = 1.5 + (ratio * newAccel1);
			double finalAccel = pctAccel * newAccel2 + (1.0 - pctAccel) * newAccel1;

			if (accel > 0)
			{
				return Math.Pow(ratio, finalAccel);
			}
			else
			{
				return (1.0 - Math.Pow(1.0 - ratio, newAccel1));
			}
		}

		protected double GetStepAngle(int width, int height)
		{
			double step = 0.5;
			int biggest = Math.Max(width, height);
			if (biggest > 50)
			{
				step = 0.4;
			}
			if (biggest > 150)
			{
				step = 0.3;
			}
			if (biggest > 250)
			{
				step = 0.2;
			}
			if (biggest > 350)
			{
				step = 0.1;
			}
			return step;
		}

		/// <summary>
		/// Takes an arbitrary value greater than equal to minimum and less than equal to maximum and translates it to a corresponding 0 - 100 value 
		/// suitable for use in a curve
		/// </summary>
		/// <param name="value"></param>
		/// <param name="maximum"></param>
		/// <param name="minimum"></param>
		/// <returns></returns>
		public static double ScaleValueToCurve(double value, double maximum, double minimum)
		{
			return ConvertRange(minimum, maximum, 0, 100, value);
		}

		private static double ConvertRange(double originalStart, double originalEnd, double newStart, double newEnd, double value) // value to convert
		{
			double scale = (newEnd - newStart) / (originalEnd - originalStart);
			return newStart + (value - originalStart) * scale;
		}

		/// <summary>
		/// Takes an arbitrary value greater than equal to 0 and less than equal to 100 and translates it to a corresponding minimum - maximum value 
		/// suitable for use in a range 
		/// </summary>
		/// <param name="value"></param>
		/// <param name="maximum"></param>
		/// <param name="minimum"></param>
		/// <returns></returns>
		protected static double ScaleCurveToValue(double value, double maximum, double minimum)
		{
			return ConvertRange(0, 100, minimum, maximum, value);
		}

		protected static bool IsAngleBetween(double a, double b, double n)
		{
			n = (360 + (n % 360)) % 360;
			a = (3600000 + a) % 360;
			b = (3600000 + b) % 360;

			if (a < b)
			{
				return a <= n && n <= b;
			}
			return a <= n || n <= b;

		}

		protected static double DegreesDiffernce(double angle1, double angle2)
		{
			return Math.Min(360 - Math.Abs(angle1 - angle2), Math.Abs(angle1 - angle2));
		}

		protected static double GetAngleDegree(Point origin, int x, int y)
		{
			var n = 270 - (Math.Atan2(origin.Y - y, origin.X - x)) * 180 / Math.PI;
			return n % 360;
		}

		protected static double DistanceFromPoint(Point origin, int x, int y)
		{
			return Math.Sqrt(Math.Pow((x - origin.X), 2) + Math.Pow((y - origin.Y), 2));
		}

		protected static double DistanceFromPoint(Point origin, Point point)
		{
			return Math.Sqrt(Math.Pow((point.X - origin.X), 2) + Math.Pow((point.Y - origin.Y), 2));
		}

		protected static double AddDegrees(double angle, double degrees)
		{
			var newAngle = (angle + degrees) % 360;
			if (newAngle < 0)
			{
				newAngle += 360;
			}

			return newAngle;
        }

        protected bool CheckNodesForVideoProperty()
        {
            //Get the property for this node to use the size dimensions.
            _frameSize = GetElementVideoSize(TargetNodes.FirstOrDefault());
            VideoElementDetected = !FrameSize.IsEmpty;
            SetBrowsable("TargetPositioning", FrameSize.IsEmpty);
            SetBrowsable("Orientation", FrameSize.IsEmpty);
            TypeDescriptor.Refresh(this);
            if (TargetPositioning == TargetPositioningType.Video && FrameSize.IsEmpty) TargetPositioning = TargetPositioningType.Strings;
            return !_frameSize.IsEmpty;
        }

        protected Size GetElementVideoSize(ElementNode node)
        {
            if (node != null && node.Properties.Contains(VideoDescriptor.ModuleId))
            {
                VideoModule videoProperty = node.Properties.Get(VideoDescriptor.ModuleId) as VideoModule;
                return new Size(videoProperty.Width, videoProperty.Height);
            }
            return Size.Empty;
        }
    }
}
