﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using GalaSoft.MvvmLight.Messaging;
using HandBrake.Interop;
using HandBrake.Interop.Model;
using HandBrake.Interop.Model.Encoding;
using Microsoft.Practices.Unity;
using VidCoder.Messages;
using VidCoder.Model;
using VidCoder.ViewModel.Components;

namespace VidCoder.ViewModel
{
	public class VideoPanelViewModel : PanelViewModel
	{
		private const int DefaultVideoBitrateKbps = 900;
		private const int DefaultTargetSizeMB = 700;

		private OutputPathViewModel outputPathVM = Unity.Container.Resolve<OutputPathViewModel>();

		private List<HBEncoder> encoderChoices;

		private HBEncoder selectedEncoder;
		private List<double> framerateChoices;
		private int displayTargetSize;
		private int displayVideoBitrate;

		private List<ComboChoice> x264ProfileChoices;
		private List<ComboChoice> x264PresetChoices;
		private List<ComboChoice> x264TuneChoices;

		public VideoPanelViewModel(EncodingViewModel encodingViewModel)
			: base(encodingViewModel)
		{
			this.encoderChoices = new List<HBEncoder>();

			this.x264ProfileChoices = new List<ComboChoice>
			{
				new ComboChoice(null, "-None-"),
				new ComboChoice("baseline", "Baseline"),
				new ComboChoice("main", "Main"),
				new ComboChoice("high", "High"),
			};

			this.x264PresetChoices = new List<ComboChoice>
			{
				new ComboChoice(null, "-None-"),
				new ComboChoice("ultrafast", "Ultra Fast"),
				new ComboChoice("superfast", "Super Fast"),
				new ComboChoice("veryfast", "Very Fast"),
				new ComboChoice("faster", "Faster"),
				new ComboChoice("fast", "Fast"),
				new ComboChoice("medium", "Medium"),
				new ComboChoice("slow", "Slow"),
				new ComboChoice("slower", "Slower"),
				new ComboChoice("veryslow", "Very Slow"),
				new ComboChoice("placebo", "Placebo"),
			};

			this.x264TuneChoices = new List<ComboChoice>
			{
				new ComboChoice(null, "-None-"),
				new ComboChoice("film", "Film"),
				new ComboChoice("animation", "Animation"),
				new ComboChoice("grain", "Grain"),
				new ComboChoice("stillimage", "Still Image"),
				new ComboChoice("psnr", "PSNR"),
				new ComboChoice("ssim", "SSIM"),
				new ComboChoice("fastdecode", "Fast Decode"),
				new ComboChoice("zerolatency", "Zero Latency"),
			};

			this.framerateChoices = new List<double>
			{
				0,
				5,
				10,
				15,
				23.976,
				24,
				25,
				29.97
			};

			Messenger.Default.Register<AudioInputChangedMessage>(
				this,
				message =>
					{
						this.NotifyAudioInputChanged();
					});

			Messenger.Default.Register<SelectedTitleChangedMessage>(
				this,
				message =>
					{
						this.NotifyAudioInputChanged();
					});
		}

		public string InputType
		{
			get
			{
				if (this.HasSourceData)
				{
					return DisplayConversions.DisplayInputType(this.SelectedTitle.InputType);
				}

				return string.Empty;
			}
		}

		public string InputVideoCodec
		{
			get
			{
				if (this.HasSourceData)
				{
					return DisplayConversions.DisplayVideoCodecName(this.SelectedTitle.VideoCodecName);
				}

				return string.Empty;
			}
		}

		public string InputFramerate
		{
			get
			{
				if (this.HasSourceData)
				{
					return string.Format("{0:0.###} FPS", this.SelectedTitle.Framerate);
				}

				return string.Empty;
			}
		}

		public List<HBEncoder> EncoderChoices
		{
			get
			{
				return this.encoderChoices;
			}
		}

		public HBEncoder SelectedEncoder
		{
			get
			{
				return this.selectedEncoder;
			}

			set
			{
				if (value != null && value != this.selectedEncoder)
				{
					HBEncoder oldEncoder = this.selectedEncoder;

					this.selectedEncoder = value;
					this.Profile.VideoEncoder = this.selectedEncoder.ShortName;
					this.RaisePropertyChanged("SelectedEncoder");
					this.RaisePropertyChanged("X264SettingsVisible");
					this.RaisePropertyChanged("QualitySliderMin");
					this.RaisePropertyChanged("QualitySliderMax");
					this.RaisePropertyChanged("QualitySliderLeftText");
					this.RaisePropertyChanged("QualitySliderRightText");
					this.IsModified = true;

					// Move the quality number to something equivalent for the new encoder.
					if (oldEncoder != null)
					{
						double oldQualityFraction = 0.0;

						switch (oldEncoder.ShortName)
						{
							case "x264":
								oldQualityFraction = 1.0 - this.Quality / 51.0;
								break;
							case "ffmpeg4":
							case "ffmpeg2":
								oldQualityFraction = 1.0 - this.Quality / 31.0;
								break;
							case "theora":
								oldQualityFraction = this.Quality / 63.0;
								break;
							default:
								throw new InvalidOperationException("Unrecognized encoder.");
						}

						switch (value.ShortName)
						{
							case "x264":
								this.Quality = Math.Round((1.0 - oldQualityFraction) * 51.0);
								break;
							case "ffmpeg4":
							case "ffmpeg2":
								this.Quality = Math.Max(1.0, Math.Round((1.0 - oldQualityFraction) * 31.0));
								break;
							case "theora":
								this.Quality = Math.Round(oldQualityFraction * 63);
								break;
							default:
								throw new InvalidOperationException("Unrecognized encoder.");
						}
					}
				}
			}
		}

		public bool X264SettingsVisible
		{
			get
			{
				return this.SelectedEncoder.ShortName == "x264";
			}
		}

		public List<double> FramerateChoices
		{
			get
			{
				return this.framerateChoices;
			}
		}

		public double SelectedFramerate
		{
			get
			{
				return this.Profile.Framerate;
			}

			set
			{
				this.Profile.Framerate = value;
				this.RaisePropertyChanged("SelectedFramerate");
				this.RaisePropertyChanged("PeakFramerateVisible");
				this.IsModified = true;
			}
		}

		public bool PeakFramerate
		{
			get
			{
				return this.Profile.PeakFramerate;
			}

			set
			{
				this.Profile.PeakFramerate = value;
				this.RaisePropertyChanged("PeakFramerate");
				this.IsModified = true;
			}
		}

		public bool PeakFramerateVisible
		{
			get
			{
				return this.SelectedFramerate != 0;
			}
		}

		public bool TwoPassEncoding
		{
			get
			{
				return this.Profile.TwoPass;
			}

			set
			{
				this.Profile.TwoPass = value;
				this.RaisePropertyChanged("TwoPassEncoding");
				this.RaisePropertyChanged("TurboFirstPass");
				this.RaisePropertyChanged("TurboFirstPassEnabled");
				this.IsModified = true;
			}
		}

		public bool TwoPassEncodingEnabled
		{
			get
			{
				return this.VideoEncodeRateType != VideoEncodeRateType.ConstantQuality;
			}
		}

		public bool TurboFirstPass
		{
			get
			{
				if (!this.TwoPassEncoding)
				{
					return false;
				}

				return Profile.TurboFirstPass;
			}

			set
			{
				this.Profile.TurboFirstPass = value;
				this.RaisePropertyChanged("TurboFirstPass");
				this.IsModified = true;
			}
		}

		public bool TurboFirstPassEnabled
		{
			get
			{
				return this.VideoEncodeRateType != VideoEncodeRateType.ConstantQuality && this.TwoPassEncoding;
			}
		}

		public VideoEncodeRateType VideoEncodeRateType
		{
			get
			{
				return this.Profile.VideoEncodeRateType;
			}

			set
			{
				VideoEncodeRateType oldRateType = this.Profile.VideoEncodeRateType;

				this.Profile.VideoEncodeRateType = value;
				this.RaisePropertyChanged("VideoEncodeRateType");

				if (value == VideoEncodeRateType.ConstantQuality)
				{
					// Set up a default quality.
					switch (this.SelectedEncoder.ShortName)
					{
						case "x264":
							this.Quality = 20;
							break;
						case "ffmpeg4":
						case "ffmpeg2":
							this.Quality = 12;
							break;
						case "theora":
							this.Quality = 38;
							break;
						default:
							break;
					}

					// Disable two-pass options
					this.Profile.TwoPass = false;
					this.Profile.TurboFirstPass = false;
					this.RaisePropertyChanged("TwoPassEncoding");
					this.RaisePropertyChanged("TurboFirstPass");
				}

				this.RaisePropertyChanged("TwoPassEncodingEnabled");
				this.RaisePropertyChanged("TurboFirstPassEnabled");

				if (value == VideoEncodeRateType.AverageBitrate)
				{
					if (oldRateType == VideoEncodeRateType.ConstantQuality)
					{
						if (this.Profile.VideoBitrate == 0)
						{
							this.VideoBitrate = DefaultVideoBitrateKbps;
						}
					}
					else if (oldRateType == VideoEncodeRateType.TargetSize)
					{
						if (this.displayVideoBitrate == 0)
						{
							this.VideoBitrate = DefaultVideoBitrateKbps;
						}
						else
						{
							this.VideoBitrate = this.displayVideoBitrate;
						}
					}

					this.RaisePropertyChanged("VideoBitrate");
					this.RaisePropertyChanged("TargetSize");
				}

				if (value == VideoEncodeRateType.TargetSize)
				{
					if (oldRateType == VideoEncodeRateType.ConstantQuality)
					{
						if (this.Profile.TargetSize == 0)
						{
							this.TargetSize = DefaultTargetSizeMB;
						}
					}
					else if (oldRateType == VideoEncodeRateType.AverageBitrate)
					{
						if (this.displayTargetSize == 0)
						{
							this.TargetSize = DefaultTargetSizeMB;
						}
						else
						{
							this.TargetSize = this.displayTargetSize;
						}
					}

					this.RaisePropertyChanged("TargetSize");
					this.RaisePropertyChanged("VideoBitrate");
				}

				this.outputPathVM.GenerateOutputFileName();
				this.IsModified = true;
			}
		}

		public int TargetSize
		{
			get
			{
				if (this.VideoEncodeRateType == VideoEncodeRateType.AverageBitrate)
				{
					if (this.MainViewModel.HasVideoSource && this.MainViewModel.JobCreationAvailable && this.VideoBitrate > 0)
					{
						this.displayTargetSize = (int)Math.Round(this.MainViewModel.ScanInstance.CalculateFileSize(this.MainViewModel.EncodeJob, this.VideoBitrate));
					}
					else
					{
						this.displayTargetSize = 0;
					}

					return this.displayTargetSize;
				}

				return this.Profile.TargetSize;
			}

			set
			{
				this.Profile.TargetSize = value;
				this.RaisePropertyChanged("TargetSize");
				this.RaisePropertyChanged("VideoBitrate");
				this.outputPathVM.GenerateOutputFileName();
				this.IsModified = true;
			}
		}

		public int VideoBitrate
		{
			get
			{
				if (this.VideoEncodeRateType == VideoEncodeRateType.ConstantQuality)
				{
					return DefaultVideoBitrateKbps;
				}

				if (this.VideoEncodeRateType == VideoEncodeRateType.TargetSize)
				{
					if (this.MainViewModel.HasVideoSource && this.MainViewModel.JobCreationAvailable && this.TargetSize > 0)
					{
						this.displayVideoBitrate = this.MainViewModel.ScanInstance.CalculateBitrate(this.MainViewModel.EncodeJob, this.TargetSize);
					}
					else
					{
						this.displayVideoBitrate = 0;
					}

					return this.displayVideoBitrate;
				}

				return this.Profile.VideoBitrate;
			}

			set
			{
				this.Profile.VideoBitrate = value;
				this.RaisePropertyChanged("VideoBitrate");
				this.RaisePropertyChanged("TargetSize");
				this.outputPathVM.GenerateOutputFileName();
				this.IsModified = true;
			}
		}

		public double Quality
		{
			get
			{
				return this.Profile.Quality;
			}

			set
			{
				this.Profile.Quality = value;
				this.RaisePropertyChanged("Quality");
				this.outputPathVM.GenerateOutputFileName();
				this.IsModified = true;
			}
		}

		public int QualitySliderMin
		{
			get
			{
				switch (this.SelectedEncoder.ShortName)
				{
					case "x264":
						return 0;
					case "ffmpeg4":
					case "ffmpeg2":
						return 1;
					case "theora":
						return 0;
					default:
						return 0;
				}
			}
		}

		public int QualitySliderMax
		{
			get
			{
				switch (this.SelectedEncoder.ShortName)
				{
					case "x264":
						return 51;
					case "ffmpeg4":
					case "ffmpeg2":
						return 31;
					case "theora":
						return 63;
					default:
						return 0;
				}
			}
		}

		public string QualitySliderLeftText
		{
			get
			{
				switch (this.SelectedEncoder.ShortName)
				{
					case "x264":
					case "ffmpeg4":
					case "ffmpeg2":
						return "High quality";
					case "theora":
						return "Low quality";
					default:
						return string.Empty;
				}
			}
		}

		public string QualitySliderRightText
		{
			get
			{
				switch (this.SelectedEncoder.ShortName)
				{
					case "x264":
					case "ffmpeg4":
					case "ffmpeg2":
						return "Low quality";
					case "theora":
						return "High quality";
					default:
						return string.Empty;
				}
			}
		}

		public List<ComboChoice> X264ProfileChoices
		{
			get
			{
				return this.x264ProfileChoices;
			}
		}

		public List<ComboChoice> X264PresetChoices
		{
			get
			{
				return this.x264PresetChoices;
			}
		} 

		public List<ComboChoice> X264TuneChoices
		{
			get
			{
				return this.x264TuneChoices;
			}
		} 

		public string X264Profile
		{
			get
			{
				return this.Profile.X264Profile;
			}

			set
			{
				this.Profile.X264Profile = value;
				this.RaisePropertyChanged("X264Profile");
				this.IsModified = true;
			}
		}

		public string X264Preset
		{
			get
			{
				return this.Profile.X264Preset;
			}

			set
			{
				this.Profile.X264Preset = value;
				this.RaisePropertyChanged("X264Preset");
				this.IsModified = true;
			}
		}

		public string X264Tune
		{
			get
			{
				return this.Profile.X264Tune;
			}

			set
			{
				this.Profile.X264Tune = value;
				this.RaisePropertyChanged("X264Tune");
				this.IsModified = true;
			}
		}

		public void NotifyOutputFormatChanged(Container outputFormat)
		{
			// Refresh encoder choices based on output format change
			HBEncoder oldEncoder = this.selectedEncoder;

			this.RefreshEncoderChoices(outputFormat);

			if (this.EncoderChoices.Contains(oldEncoder))
			{
				// Review: is this needed to set this?
				this.SelectedEncoder = oldEncoder;
			}
			else
			{
				this.SelectedEncoder = this.EncoderChoices[0];
			}
		}

		private void RefreshEncoderChoices(Container outputFormat)
		{
			this.encoderChoices = new List<HBEncoder>();

			foreach (HBEncoder encoder in Encoders.VideoEncoders)
			{
				if ((encoder.CompatibleContainers & outputFormat) > 0)
				{
					this.EncoderChoices.Add(encoder);
				}
			}

			this.RaisePropertyChanged("EncoderChoices");
		}

		public void NotifyProfileChanged()
		{
			this.RefreshEncoderChoices(this.Profile.OutputFormat);

			this.selectedEncoder = this.EncoderChoices.Single(e => e.ShortName == this.Profile.VideoEncoder);
		}

		public void NotifyAllChanged()
		{
			this.RaisePropertyChanged("SelectedEncoder");
			this.RaisePropertyChanged("SelectedFramerate");
			this.RaisePropertyChanged("PeakFramerate");
			this.RaisePropertyChanged("PeakFramerateVisible");
			this.RaisePropertyChanged("TwoPassEncoding");
			this.RaisePropertyChanged("TurboFirstPass");
			this.RaisePropertyChanged("TwoPassEncodingEnabled");
			this.RaisePropertyChanged("TurboFirstPassEnabled");
			this.RaisePropertyChanged("VideoEncodeRateType");
			this.RaisePropertyChanged("TargetSize");
			this.RaisePropertyChanged("VideoBitrate");
			this.RaisePropertyChanged("Quality");
			this.RaisePropertyChanged("QualitySliderMin");
			this.RaisePropertyChanged("QualitySliderMax");
			this.RaisePropertyChanged("QualitySliderLeftText");
			this.RaisePropertyChanged("QualitySliderRightText");
			this.RaisePropertyChanged("X264Profile");
			this.RaisePropertyChanged("X264Preset");
			this.RaisePropertyChanged("X264Tune");
			this.RaisePropertyChanged("X264SettingsVisible");
		}

		/// <summary>
		/// Re-calculates video bitrate if needed.
		/// </summary>
		public void UpdateVideoBitrate()
		{
			if (this.VideoEncodeRateType == VideoEncodeRateType.TargetSize)
			{
				this.RaisePropertyChanged("VideoBitrate");
			}
		}

		/// <summary>
		/// Re-calculates target size if needed.
		/// </summary>
		public void UpdateTargetSize()
		{
			if (this.VideoEncodeRateType == VideoEncodeRateType.AverageBitrate)
			{
				this.RaisePropertyChanged("TargetSize");
			}
		}

		public override void NotifySelectedTitleChanged()
		{
			this.RaisePropertyChanged("InputType");
			this.RaisePropertyChanged("InputVideoCodec");
			this.RaisePropertyChanged("InputFramerate");

			base.NotifySelectedTitleChanged();
		}

		private void NotifyAudioInputChanged()
		{
			this.UpdateVideoBitrate();
			this.UpdateTargetSize();
		}
	}
}
