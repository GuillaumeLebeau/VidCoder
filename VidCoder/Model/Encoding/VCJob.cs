﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HandBrake.ApplicationServices.Interop.Json.Encode;
using HandBrake.ApplicationServices.Interop.Json.Scan;
using HandBrake.ApplicationServices.Interop.Model;
using HandBrake.ApplicationServices.Interop.Model.Encoding;
using HandBrake.ApplicationServices.Interop.Model.Preview;

namespace VidCoder.Model.Encoding
{
	using System.Xml.Serialization;
	using Omu.ValueInjecter;

	/// <summary>
	/// An analogue for HBInterop's EncodingJob.
	/// </summary>
	public class VCJob
	{
		public SourceType SourceType { get; set; }
		public string SourcePath { get; set; }

		/// <summary>
		/// Gets or sets the 1-based index of the title to encode.
		/// </summary>
		public int Title { get; set; }

		/// <summary>
		/// Gets or sets the angle to encode. 0 for default, 1+ for specified angle.
		/// </summary>
		public int Angle { get; set; }

		public VideoRangeType RangeType { get; set; }
		public int ChapterStart { get; set; }
		public int ChapterEnd { get; set; }

		public double SecondsStart { get; set; }
		public double SecondsEnd { get; set; }

		public int FramesStart { get; set; }
		public int FramesEnd { get; set; }

		/// <summary>
		/// Gets or sets the list of chosen audio tracks (1-based)
		/// </summary>
		public List<int> ChosenAudioTracks { get; set; }
		public VCSubtitles Subtitles { get; set; }
		public bool UseDefaultChapterNames { get; set; }
		public List<string> CustomChapterNames { get; set; }

		public string OutputPath { get; set; }

		public VCProfile EncodingProfile { get; set; }

		// The length of video to encode.
		[XmlIgnore]
		public TimeSpan Length { get; set; }

		[XmlElement("Length")]
		public string XmlLength
		{
			get { return this.Length.ToString(); }
			set { this.Length = TimeSpan.Parse(value); }
		}

		public VCJob Clone()
		{
			var clone = new VCJob
			{
				SourceType = this.SourceType,
				SourcePath = this.SourcePath,
				Title = this.Title,
				Angle = this.Angle,
				RangeType = this.RangeType,
				ChapterStart = this.ChapterStart,
				ChapterEnd = this.ChapterEnd,
				SecondsStart = this.SecondsStart,
				SecondsEnd = this.SecondsEnd,
				FramesStart = this.FramesStart,
				FramesEnd = this.FramesEnd,
				ChosenAudioTracks = new List<int>(this.ChosenAudioTracks),
				Subtitles = this.Subtitles,
				UseDefaultChapterNames = this.UseDefaultChapterNames,
				OutputPath = this.OutputPath,
				EncodingProfile = this.EncodingProfile,
				Length = this.Length
			};

			return clone;
		}
	}
}
