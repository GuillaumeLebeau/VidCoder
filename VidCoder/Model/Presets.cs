﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.Linq;
using System.Text;
using System.IO;
using System.Xml.Serialization;
using System.Xml;
using System.Xml.Linq;
using System.Threading;
using System.Globalization;
using HandBrake.Interop;
using HandBrake.Interop.Model;
using HandBrake.Interop.Model.Encoding;

namespace VidCoder.Model
{
	public static class Presets
	{
		private const int CurrentPresetVersion = 4;

		private static readonly string UserPresetsFolder = Path.Combine(Utilities.AppFolder, "UserPresets");
		private static readonly string BuiltInPresetsPath = "BuiltInPresets.xml";
		private static XmlSerializer presetListSerializer = new XmlSerializer(typeof(PresetCollection));
		private static XmlSerializer presetSerializer = new XmlSerializer(typeof(Preset));
		private static object userPresetSync = new object();

		static Presets()
		{
			int databaseVersion = DatabaseConfig.GetConfigInt("Version", Database.Connection);
			if (databaseVersion < Utilities.CurrentDatabaseVersion)
			{
				var presets = GetPresetListFromDb();

				foreach (Preset preset in presets)
				{
					UpgradePreset(preset);
				}

				var presetXmlList = presets.Select(SerializePreset).ToList();

				using (SQLiteTransaction transaction = Database.Connection.BeginTransaction())
				{
					SavePresets(presetXmlList, Database.Connection);
					DatabaseConfig.SetConfigValue("Version", Utilities.CurrentDatabaseVersion, Database.Connection);
					transaction.Commit();
				}
			}
		}

		public static List<Preset> BuiltInPresets
		{
			get
			{
				using (var stream = new FileStream(BuiltInPresetsPath, FileMode.Open, FileAccess.Read))
				{
					var presetCollection = presetListSerializer.Deserialize(stream) as PresetCollection;
					return presetCollection.Presets;
				}
			}
		}

		public static List<Preset> UserPresets
		{
			get
			{
				lock (userPresetSync)
				{
					var result = GetPresetListFromDb();

					if (result.Count == 0 && Directory.Exists(UserPresetsFolder))
					{
						// If we have no DB presets, check for old .xml files in the UserPresets folder
						string[] presetFiles = Directory.GetFiles(UserPresetsFolder);
						foreach (string presetFile in presetFiles)
						{
							Preset preset = LoadPresetFile(presetFile);
							if (preset != null)
							{
								result.Add(preset);
							}
						}
					}

					return result;
				}
			}

			set
			{
				var presetXmlList = value.Select(SerializePreset).ToList();

				// Do the actual save asynchronously.
				ThreadPool.QueueUserWorkItem(SaveUserPresetsBackground, presetXmlList);
			}
		}



		/// <summary>
		/// Serializes a preset to XML. Does not include wrapper.
		/// </summary>
		/// <param name="preset">The preset to serialize.</param>
		/// <returns>The serialized XML of the preset.</returns>
		public static string SerializePreset(Preset preset)
		{
			var xmlBuilder = new StringBuilder();
			using (XmlWriter writer = XmlWriter.Create(xmlBuilder))
			{
				presetSerializer.Serialize(writer, preset);
			}

			return xmlBuilder.ToString();
		}

		/// <summary>
		/// Loads in a preset from a file.
		/// </summary>
		/// <param name="presetFile">The file to load the preset from.</param>
		/// <returns>The parsed preset from the file, or null if the preset is invalid.</returns>
		public static Preset LoadPresetFile(string presetFile)
		{
			if (Path.GetExtension(presetFile).ToLowerInvariant() != ".xml")
			{
				return null;
			}

			try
			{
				XDocument doc = XDocument.Load(presetFile);
				if (doc.Element("UserPreset") == null)
				{
					return null;
				}

				XElement presetElement = doc.Element("UserPreset").Element("Preset");
				int version = int.Parse(doc.Element("UserPreset").Attribute("Version").Value);

				using (XmlReader reader = presetElement.CreateReader())
				{
					var preset = presetSerializer.Deserialize(reader) as Preset;
					if (version < CurrentPresetVersion)
					{
						UpgradePreset(preset);
					}

					return preset;
				}
			}
			catch (XmlException)
			{
				return null;
			}
		}

		/// <summary>
		/// Load in a preset from an XML string.
		/// </summary>
		/// <param name="presetXml">The XML of the preset to load in (without wrapper XML).</param>
		/// <returns>The loaded Preset.</returns>
		public static Preset LoadPresetXmlString(string presetXml)
		{
			try
			{
				using (var stringReader = new StringReader(presetXml))
				{
					using (var xmlReader = new XmlTextReader(stringReader))
					{
						var preset = presetSerializer.Deserialize(xmlReader) as Preset;

						return preset;
					}
				}
			}
			catch (XmlException exception)
			{
				System.Windows.MessageBox.Show(
					"Could not load preset: " +
					exception +
					Environment.NewLine +
					Environment.NewLine +
					presetXml);
			}

			return null;
		}

		/// <summary>
		/// Saves a user preset to a file. Includes wrapper XML.
		/// </summary>
		/// <param name="preset">The preset to save.</param>
		/// <param name="filePath">The path to save the preset to.</param>
		/// <returns>True if the save succeeded.</returns>
		public static bool SavePresetToFile(Preset preset, string filePath)
		{
			try
			{
				string presetXml = SerializePreset(preset);

				XElement element = XElement.Parse(presetXml);
				var doc = new XDocument(
					new XElement("UserPreset",
					             new XAttribute("Version", CurrentPresetVersion.ToString(CultureInfo.InvariantCulture)),
					             element));

				doc.Save(filePath);
				return true;
			}
			catch (XmlException exception)
			{
				System.Windows.MessageBox.Show(string.Format("Could not save preset '{0}':{1}{2}", preset.Name, Environment.NewLine, exception));
			}

			return false;
		}

		/// <summary>
		/// Saves the given preset data.
		/// </summary>
		/// <param name="presetXmlListObject">List&lt;Tuple&lt;string, string&gt;&gt; with the file name and XML string to save.</param>
		private static void SaveUserPresetsBackground(object presetXmlListObject)
		{
			lock (userPresetSync)
			{
				if (Directory.Exists(UserPresetsFolder))
				{
					string[] existingFiles = Directory.GetFiles(UserPresetsFolder);
					foreach (string existingFile in existingFiles)
					{
						File.Delete(existingFile);
					}

					Directory.Delete(UserPresetsFolder);
				}

				var presetXmlList = presetXmlListObject as List<string>;

				SQLiteConnection connection = Database.CreateConnection();

				using (SQLiteTransaction transaction = connection.BeginTransaction())
				{
					SavePresets(presetXmlList, connection);
					transaction.Commit();
				}
			}
		}

		private static void SavePresets(List<string> presetXmlList, SQLiteConnection connection)
		{
			Database.ExecuteNonQuery("DELETE FROM presetsXml", connection);

			var insertCommand = new SQLiteCommand("INSERT INTO presetsXml (xml) VALUES (?)", connection);
			SQLiteParameter insertXmlParam = insertCommand.Parameters.Add("xml", DbType.String);

			foreach (string presetXml in presetXmlList)
			{
				insertXmlParam.Value = presetXml;
				insertCommand.ExecuteNonQuery();
			}
		}

		private static List<Preset> GetPresetListFromDb()
		{
			var result = new List<Preset>();

			var selectPresetsCommand = new SQLiteCommand("SELECT * FROM presetsXml", Database.Connection);
			using (SQLiteDataReader reader = selectPresetsCommand.ExecuteReader())
			{
				while (reader.Read())
				{
					string presetXml = reader.GetString("xml");
					result.Add(LoadPresetXmlString(presetXml));
				}
			}
			return result;
		}

		private static void UpgradePreset(Preset preset)
		{
			// Upgrade preset: translate old Enum-based values to short name strings
			if (!Encoders.VideoEncoders.Any(e => e.ShortName == preset.EncodingProfile.VideoEncoder))
			{
				string newVideoEncoder = "x264";
				switch (preset.EncodingProfile.VideoEncoder)
				{
					case "X264":
						newVideoEncoder = "x264";
						break;
					case "FFMpeg":
						newVideoEncoder = "ffmpeg4";
						break;
					case "FFMpeg2":
						newVideoEncoder = "ffmpeg2";
						break;
					case "Theora":
						newVideoEncoder = "theora";
						break;
				}

				preset.EncodingProfile.VideoEncoder = newVideoEncoder;
			}

			foreach (AudioEncoding encoding in preset.EncodingProfile.AudioEncodings)
			{
				if (!Encoders.AudioEncoders.Any(e => e.ShortName == encoding.Encoder))
				{
					string newAudioEncoder = "faac";
					switch (encoding.Encoder)
					{
						case "Faac":
							newAudioEncoder = "faac";
							break;
						case "Lame":
							newAudioEncoder = "lame";
							break;
						case "Ac3":
							newAudioEncoder = "ffac3";
							break;
						case "Passthrough":
							newAudioEncoder = "copy";
							break;
						case "Ac3Passthrough":
							newAudioEncoder = "copy:ac3";
							break;
						case "DtsPassthrough":
							newAudioEncoder = "copy:dts";
							break;
						case "Vorbis":
							newAudioEncoder = "vorbis";
							break;
					}

					encoding.Encoder = newAudioEncoder;
				}

				if (!Encoders.Mixdowns.Any(m => m.ShortName == encoding.Mixdown))
				{
					string newMixdown = "dpl2";
					switch (encoding.Mixdown)
					{
						case "Auto":
							newMixdown = "none";
							break;
						case "None":
							newMixdown = "none";
							break;
						case "Mono":
							newMixdown = "mono";
							break;
						case "Stereo":
							newMixdown = "stereo";
							break;
						case "DolbySurround":
							newMixdown = "dpl1";
							break;
						case "DolbyProLogicII":
							newMixdown = "dpl2";
							break;
						case "SixChannelDiscrete":
							newMixdown = "6ch";
							break;
					}

					encoding.Mixdown = newMixdown;
				}
			}
		}
	}
}
