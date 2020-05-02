using AVDump3Lib.Information.MetaInfo;
using AVDump3Lib.Information.MetaInfo.Core;
using AVDump3Lib.Misc;
using ExtKnot.StringInvariants;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

namespace AVDump3Lib.Information.InfoProvider {
	public class MediaInfoLibNativeMethods : IDisposable {
		#region "NativeCode"
		public enum StreamTypes {
			General,
			Video,
			Audio,
			Text,
			Other,
			Image,
			Menu,
		}

		public enum InfoTypes {
			Name,
			Text,
			Measure,
			Options,
			NameText,
			MeasureText,
			Info,
			HowTo
		}

		public enum InfoOptions {
			ShowInInform,
			Support,
			ShowInSupported,
			TypeOfValue
		}

		public enum InfoFileOptions {
			FileOption_Nothing = 0x00,
			FileOption_NoRecursive = 0x01,
			FileOption_CloseAll = 0x02,
			FileOption_Max = 0x04
		};

		public enum Status {
			None = 0x00,
			Accepted = 0x01,
			Filled = 0x02,
			Updated = 0x04,
			Finalized = 0x08,
		}

		//Import of DLL functions. DO NOT USE until you know what you do (MediaInfo DLL do NOT use CoTaskMemAlloc to allocate memory)
		[DllImport("MediaInfo")]
		private static extern IntPtr MediaInfo_New();
		[DllImport("MediaInfo")]
		private static extern void MediaInfo_Delete(IntPtr handle);
		[DllImport("MediaInfo")]
		private static extern IntPtr MediaInfo_Open(IntPtr handle, IntPtr fileName);
		[DllImport("MediaInfo")]
		private static extern IntPtr MediaInfo_Open_Buffer_Init(IntPtr handle, long fileSize, long fileOffset);
		[DllImport("MediaInfo")]
		private static extern IntPtr MediaInfo_Open_Buffer_Continue(IntPtr handle, IntPtr buffer, IntPtr bufferSize);
		[DllImport("MediaInfo")]
		private static extern long MediaInfo_Open_Buffer_Continue_GoTo_Get(IntPtr handle);
		[DllImport("MediaInfo")]
		private static extern IntPtr MediaInfo_Open_Buffer_Finalize(IntPtr handle);
		[DllImport("MediaInfo")]
		private static extern void MediaInfo_Close(IntPtr handle);
		[DllImport("MediaInfo")]
		private static extern IntPtr MediaInfo_Inform(IntPtr handle, IntPtr reserved);
		[DllImport("MediaInfo")]
		private static extern IntPtr MediaInfo_GetI(IntPtr handle, IntPtr streamType, IntPtr streamIndex, IntPtr parameter, IntPtr infoType);
		[DllImport("MediaInfo")]
		private static extern IntPtr MediaInfo_Get(IntPtr handle, IntPtr streamType, IntPtr streamIndex, IntPtr parameter, IntPtr infoType, IntPtr searchType);
		[DllImport("MediaInfo", CharSet = CharSet.Unicode)]
		private static extern IntPtr MediaInfo_Option(IntPtr handle, IntPtr option, IntPtr value);
		[DllImport("MediaInfo")]
		private static extern IntPtr MediaInfo_State_Get(IntPtr handle);
		[DllImport("MediaInfo")]
		private static extern IntPtr MediaInfo_Count_Get(IntPtr handle, IntPtr streamType, IntPtr streamIndex);
		#endregion

		public IntPtr Handle { get; private set; }
		public bool UsingUTF32Encoding { get; private set; }

		private static string UTF32PtrToString(IntPtr ptr) {
			var length = 0;
			while(Marshal.ReadInt32(ptr, length) != 0) length += 4;

			var buffer = new byte[length];
			Marshal.Copy(ptr, buffer, 0, buffer.Length);
			return new UTF32Encoding(!BitConverter.IsLittleEndian, false, false).GetString(buffer);
		}

		private static IntPtr StringToUTF32Ptr(string Str) {
			Encoding codec = new UTF32Encoding(!BitConverter.IsLittleEndian, false, false);
			var length = codec.GetByteCount(Str);
			var buffer = new byte[length + 4];
			codec.GetBytes(Str, 0, Str.Length, buffer, 0);
			var ptr = Marshal.AllocHGlobal(buffer.Length);
			Marshal.Copy(buffer, 0, ptr, buffer.Length);
			return ptr;
		}

		public MediaInfoLibNativeMethods() {
			try {
				Handle = MediaInfo_New();
			} catch {
				Handle = IntPtr.Zero;
			}
			if(Environment.OSVersion.ToString().IndexOf("Windows") == -1) {
				UsingUTF32Encoding = true;
				Option("setlocale_LC_CTYPE", "");
			} else {
				UsingUTF32Encoding = false;
			}
		}

		public int Open(string fileName) {
			if(Handle == IntPtr.Zero) return 0;

			var fileNamePtr = UsingUTF32Encoding ? StringToUTF32Ptr(fileName) : Marshal.StringToHGlobalUni(fileName);
			var retVal = (int)MediaInfo_Open(Handle, fileNamePtr);
			Marshal.FreeHGlobal(fileNamePtr);
			return retVal;
		}

		public int OpenBufferInit(long fileSize, long fileOffset) {
			if(Handle == IntPtr.Zero) return 0;
			return (int)MediaInfo_Open_Buffer_Init(Handle, fileSize, fileOffset);
		}

		public int OpenBufferContinue(IntPtr buffer, IntPtr bufferSize) {
			if(Handle == IntPtr.Zero) return 0;
			return (int)MediaInfo_Open_Buffer_Continue(Handle, buffer, bufferSize);
		}

		public long OpenBufferContinueGotoGet() {
			if(Handle == IntPtr.Zero) return 0;
			return MediaInfo_Open_Buffer_Continue_GoTo_Get(Handle);
		}

		public int OpenBufferFinalize() {
			if(Handle == IntPtr.Zero) return 0;
			return (int)MediaInfo_Open_Buffer_Finalize(Handle);
		}

		public void Close() {
			if(Handle == IntPtr.Zero) return;
			MediaInfo_Close(Handle);
		}

		public string Inform() {
			if(Handle == IntPtr.Zero) return "Unable to load MediaInfo library";

			return UsingUTF32Encoding
				? UTF32PtrToString(MediaInfo_Inform(Handle, IntPtr.Zero))
				: Marshal.PtrToStringUni(MediaInfo_Inform(Handle, IntPtr.Zero));
		}

		public string Get(string parameter, StreamTypes streamType = StreamTypes.General, int streamIndex = 0, InfoTypes infoType = InfoTypes.Text, InfoTypes searchType = InfoTypes.Name) {
			if(Handle == IntPtr.Zero) return "Unable to load MediaInfo library";

			if(UsingUTF32Encoding) {
				var parameterPtr = StringToUTF32Ptr(parameter);
				var retVal = UTF32PtrToString(MediaInfo_Get(Handle, (IntPtr)streamType, (IntPtr)streamIndex, parameterPtr, (IntPtr)infoType, (IntPtr)searchType));
				Marshal.FreeHGlobal(parameterPtr);
				return retVal;

			} else {
				var parameterPtr = Marshal.StringToHGlobalUni(parameter);
				var retVal = Marshal.PtrToStringUni(MediaInfo_Get(Handle, (IntPtr)streamType, (IntPtr)streamIndex, parameterPtr, (IntPtr)infoType, (IntPtr)searchType));
				Marshal.FreeHGlobal(parameterPtr);
				return retVal;
			}
		}

		public string Get(int parameter, StreamTypes streamType = StreamTypes.General, int streamIndex = 0, InfoTypes infoType = InfoTypes.Text) {
			if(Handle == IntPtr.Zero) return "Unable to load MediaInfo library";

			return UsingUTF32Encoding
				? UTF32PtrToString(MediaInfo_GetI(Handle, (IntPtr)streamType, (IntPtr)streamIndex, (IntPtr)parameter, (IntPtr)infoType))
				: Marshal.PtrToStringUni(MediaInfo_GetI(Handle, (IntPtr)streamType, (IntPtr)streamIndex, (IntPtr)parameter, (IntPtr)infoType));
		}
		public string Option(string option, string value = "") {
			if(Handle == IntPtr.Zero) return "Unable to load MediaInfo library";

			if(UsingUTF32Encoding) {
				var optionPtr = StringToUTF32Ptr(option);
				var valuePtr = StringToUTF32Ptr(value);
				var retVal = UTF32PtrToString(MediaInfo_Option(Handle, optionPtr, valuePtr));
				Marshal.FreeHGlobal(optionPtr);
				Marshal.FreeHGlobal(valuePtr);
				return retVal;

			} else {
				var optionPtr = Marshal.StringToHGlobalUni(option);
				var valuePtr = Marshal.StringToHGlobalUni(value);
				var retVal = Marshal.PtrToStringUni(MediaInfo_Option(Handle, optionPtr, valuePtr));
				Marshal.FreeHGlobal(optionPtr);
				Marshal.FreeHGlobal(valuePtr);
				return retVal;
			}
		}

		public int GetState() {
			if(Handle == IntPtr.Zero) return 0;
			return (int)MediaInfo_State_Get(Handle);
		}

		public int GetCount(StreamTypes streamType, int streamIndex = -1) {
			if(Handle == IntPtr.Zero) return 0;
			return (int)MediaInfo_Count_Get(Handle, (IntPtr)streamType, (IntPtr)streamIndex);
		}

		public void Dispose() {
			if(Handle == IntPtr.Zero) return;
			MediaInfo_Delete(Handle);
		}
	}


	public class MediaInfoLibProvider : MediaProvider {

		private void Populate(MediaInfoLibNativeMethods mil) {
			static string removeNonNumerics(string s) => Regex.Replace(s, "[^-,.0-9]", "");
			static string splitTakeFirst(string s) => s.Split('\\', '/', '|')[0];
			//string nonEmpty(string a, string b) => string.IsNullOrEmpty(a) ? b : a;

			Add(FileSizeType, () => mil.Get("FileSize"), s => s.ToInvInt64(), splitTakeFirst, removeNonNumerics);
			Add(DurationType, () => mil.Get("Duration"), s => s.ToInvDouble() / 1000, splitTakeFirst, removeNonNumerics);
			Add(FileExtensionType, () => mil.Get("FileExtension"), s => s.ToUpperInvariant(), splitTakeFirst); //TODO: Add multiple if multiple
			Add(WritingAppType, () => mil.Get("Encoded_Application"));
			Add(MuxingAppType, () => mil.Get("Encoded_Library"));


			bool hasAudio = false, hasVideo = false, hasSubtitle = false;
			foreach(var streamType in new[] { MediaInfoLibNativeMethods.StreamTypes.Video, MediaInfoLibNativeMethods.StreamTypes.Audio, MediaInfoLibNativeMethods.StreamTypes.Text }) {
				var streamCount = mil.GetCount(streamType);

				for(var streamIndex = 0; streamIndex < streamCount; streamIndex++) {
					string streamGet(string key) => mil.Get(key, streamType, streamIndex)?.Trim();

					ulong? id = null;
					if(!string.IsNullOrEmpty(streamGet("UniqueID"))) {
						id = streamGet("UniqueID").ToInvUInt64();
					}
					if(!id.HasValue && !string.IsNullOrEmpty(streamGet("ID"))) {
						id = streamGet("ID").Split('-')[0].ToInvUInt64();
					}

					MetaInfoContainer stream = null;
					switch(streamType) {
						case MediaInfoLibNativeMethods.StreamTypes.Video:
							stream = new MetaInfoContainer(id ?? (ulong)Nodes.Count(x => x.Type == ChaptersType), VideoStreamType); hasVideo = true;
							Add(stream, MediaStream.StatedSampleRateType, () => streamGet("FrameRate").ToInvDouble());
							Add(stream, MediaStream.SampleCountType, () => streamGet("FrameCount").ToInvInt64());
							Add(stream, VideoStream.PixelAspectRatioType, () => streamGet("PixelAspectRatio").ToInvDouble());
							Add(stream, VideoStream.PixelDimensionsType, () => new Dimensions(streamGet("Width").ToInvInt32(), streamGet("Height").ToInvInt32()));
							Add(stream, VideoStream.DisplayAspectRatioType, () => streamGet("DisplayAspectRatio").ToInvDouble());
							Add(stream, VideoStream.ColorBitDepthType, () => streamGet("BitDepth").ToInvInt32());

							Add(stream, MediaStream.AverageSampleRateType, () => streamGet("FrameRate_Mode").Equals("VFR", StringComparison.OrdinalIgnoreCase) ? streamGet("FrameRate").ToInvDouble() : default);
							Add(stream, MediaStream.MaxSampleRateType, () => streamGet("FrameRate_Maximum").ToInvDouble());
							Add(stream, MediaStream.MinSampleRateType, () => streamGet("FrameRate_Minimum").ToInvDouble());

							Add(stream, VideoStream.IsInterlacedType, () => streamGet("ScanType").Equals("Interlaced", StringComparison.OrdinalIgnoreCase));
							Add(stream, VideoStream.HasVariableFrameRateType, () => streamGet("FrameRate_Mode").Equals("VFR", StringComparison.OrdinalIgnoreCase));
							Add(stream, VideoStream.ChromaSubsamplingType, () => new ChromeSubsampling(streamGet("ChromaSubsampling")));
							//Add(stream, VideoStream.ColorSpaceType, () => streamGet("ColorSpace"));
							AddNode(stream);
							break;

						case MediaInfoLibNativeMethods.StreamTypes.Audio:
							stream = new MetaInfoContainer(id ?? (ulong)Nodes.Count(x => x.Type == AudioStreamType), AudioStreamType); hasAudio = true;
							Add(stream, MediaStream.StatedSampleRateType, () => streamGet("SamplingRate").ToInvDouble());
							Add(stream, MediaStream.SampleCountType, () => streamGet("SamplingCount").ToInvInt32());
							Add(stream, AudioStream.ChannelCountType, () => streamGet("Channel(s)").ToInvInt32());
							AddNode(stream);
							break;

						case MediaInfoLibNativeMethods.StreamTypes.Text:
							stream = new MetaInfoContainer(id ?? (ulong)Nodes.Count(x => x.Type == SubtitleStreamType), SubtitleStreamType); hasSubtitle = true;
							AddNode(stream);
							break;

						default:
							stream = new MetaInfoContainer(id ?? (ulong)Nodes.Count(x => x.Type == MediaStreamType), MediaStreamType);
							AddNode(stream);
							break;
					}

					Add(stream, MediaStream.SizeType, () => streamGet("StreamSize").ToInvInt64());
					Add(stream, MediaStream.TitleType, () => streamGet("Title"));
					Add(stream, MediaStream.IsForcedType, () => streamGet("Forced").Equals("yes", StringComparison.OrdinalIgnoreCase));
					Add(stream, MediaStream.IsDefaultType, () => streamGet("Default").Equals("yes", StringComparison.OrdinalIgnoreCase));
					Add(stream, MediaStream.IdType, () => streamGet("UniqueID").ToInvUInt64());
					Add(stream, MediaStream.LanguageType, () => streamGet("Language"));
					Add(stream, MediaStream.DurationType, () => streamGet("Duration"), s => TimeSpan.FromSeconds(s.ToInvDouble() / 1000), (Func<string, string>)splitTakeFirst);
					Add(stream, MediaStream.BitrateType, () => streamGet("BitRate").ToInvDouble());
					Add(stream, MediaStream.ContainerCodecIdWithCodecPrivateType, () => streamGet("CodecID"));
					Add(stream, MediaStream.CodecIdType, () => streamGet("Format"));
					Add(stream, MediaStream.CodecAdditionalFeaturesType, () => streamGet("Format_AdditionalFeatures"));
					Add(stream, MediaStream.CodecCommercialIdType, () => streamGet("Format_Commercial"));
					Add(stream, MediaStream.CodecProfileType, () => streamGet("Format_Profile"));
					Add(stream, MediaStream.CodecVersionType, () => streamGet("Format_Version"));
					Add(stream, MediaStream.CodecNameType, () => streamGet("Format-Info"));
					Add(stream, MediaStream.EncoderSettingsType, () => streamGet("Encoded_Library_Settings"));
					Add(stream, MediaStream.EncoderNameType, () => streamGet("Encoded_Library"));
					Add(stream, MediaStream.StatedBitrateModeType, () => streamGet("BitRate_Mode"));
				}
			}

			AddSuggestedFileExtension(mil, hasAudio, hasVideo, hasSubtitle);

			var menuStreamCount = mil.GetCount(MediaInfoLibNativeMethods.StreamTypes.Menu);
			for(var i = 0; i < menuStreamCount; i++) {
				PopulateChapters(mil, i);
			}
		}

		private void PopulateChapters(MediaInfoLibNativeMethods mil, int streamIndex) {
			var menuType = MediaInfoLibNativeMethods.StreamTypes.Menu;
			var chapters = new MetaInfoContainer((ulong)streamIndex, ChaptersType);

			static ulong conv(string str) {
				var timeParts = str.Split(new char[] { ':', '.' }).Select(s => s.Trim().ToInvUInt64()).ToArray();
				return (((timeParts[0] * 60 + timeParts[1]) * 60 + timeParts[2]) * 1000 + timeParts[3]) * 1000000;
			}

			var format = mil.Get("Format", menuType, streamIndex);
			var languageChapters = mil.Get("Language", menuType, streamIndex);
			Add(chapters, Chapters.FormatType, (format + " -- " + (string.IsNullOrEmpty(format) ? "nero" : "mov")).Trim());

			var entryCount = mil.GetCount(MediaInfoLibNativeMethods.StreamTypes.Menu, streamIndex);
			if(int.TryParse(mil.Get("Chapters_Pos_Begin", menuType, streamIndex), out var indexStart) && int.TryParse(mil.Get("Chapters_Pos_End", menuType, streamIndex), out var indexEnd)) {

				//MIL Offset Bug workaround
				var offsetFixTries = 20;
				while(offsetFixTries-- > 0 && !mil.Get(indexStart, menuType, streamIndex, MediaInfoLibNativeMethods.InfoTypes.Name).Split('-').All(x => x.Contains(':', StringComparison.Ordinal))) {
					indexStart++;
					indexEnd++;
				}
				if(offsetFixTries == 0) {
					return;
				}

				for(; indexStart < indexEnd; indexStart++) {
					var chapter = new MetaInfoContainer((ulong)indexStart, Chapters.ChapterType);
					chapters.AddNode(chapter);

					var timeStamps = mil.Get(indexStart, menuType, streamIndex, MediaInfoLibNativeMethods.InfoTypes.Name).Split('-');
					var timeStamp = conv(timeStamps[0].Trim());

					Add(chapter, Chapter.TimeStartType, timeStamp / 1000d);
					//Add(chapter, Chapter.TimeEndType, );

					var title = mil.Get(indexStart, menuType, streamIndex);

					var languages = new List<string>();
					if((uint)title.IndexOf(':') < 5) {
						var language = title.Contains(':') ? title.Substring(0, title.IndexOf(':'))  : "";
						if(!string.IsNullOrEmpty(language)) languages.Add(language);
						title = title.Substring(language.Length + 1);

					} else if(!string.IsNullOrEmpty(languageChapters)) {
						languages.Add(languageChapters);
					}



					Add(chapter, Chapter.TitleType, new ChapterTitle(title, languages, Array.Empty<string>()));
				}
			}

			AddNode(chapters);
		}


		private void AddSuggestedFileExtension(MediaInfoLibNativeMethods mil, bool hasAudio, bool hasVideo, bool hasSubtitle) {
			var milInfo = (mil.Get("Format/Extensions") ?? "").ToLowerInvariant();
			var fileExt = (mil.Get("FileExtension") ?? "").ToLowerInvariant();
			if(milInfo.Contains("asf") && milInfo.Contains("wmv") && milInfo.Contains("wma")) {
				if(!hasVideo && hasAudio && !hasSubtitle) {
					Add(SuggestedFileExtensionType, "wma");
				} else {
					Add(SuggestedFileExtensionType, "wmv");
				}
			} else if(milInfo.Contains("ts") && milInfo.Contains("m2t")) {
				if(fileExt.Equals(".ts")) Add(SuggestedFileExtensionType, "ts"); //Blame worf

			} else if(milInfo.Contains("mpeg") && milInfo.Contains("mpg")) {
				if(!hasVideo || !hasAudio && hasAudio) {
					Add(SuggestedFileExtensionType, "sub");
				} else {
					Add(SuggestedFileExtensionType, fileExt.Equals("mpeg") ? "mpeg" : "mpg");
				}
			} else if((milInfo.Contains("mp1") && milInfo.Contains("mp2") && milInfo.Contains("mp3")) || milInfo.Contains("wav")) {
				switch(mil.Get("Format_Profile", MediaInfoLibNativeMethods.StreamTypes.Audio)) {
					case "Layer 1": Add(SuggestedFileExtensionType, "mp1"); break;
					case "Layer 2": Add(SuggestedFileExtensionType, "mp2"); break;
					case "Layer 3": Add(SuggestedFileExtensionType, "mp3"); break;
				}

			} else if(milInfo.Contains("mp4") && milInfo.Contains("m4a") && milInfo.Contains("m4v")) {
				if(hasSubtitle || (hasVideo && hasAudio)) {
					Add(SuggestedFileExtensionType, "mp4");
				} else if(hasVideo && !hasAudio) {
					Add(SuggestedFileExtensionType, "m4v");
				} else if(!hasVideo && hasAudio) {
					Add(SuggestedFileExtensionType, "m4a");
				}
			}

			if(Select(SuggestedFileExtensionType) == null) {
				Add(SuggestedFileExtensionType, milInfo);
			}
		}

		public MediaInfoLibProvider(string filePath)
			: base("MediaInfoLibProvider") {

			using var mil = new MediaInfoLibNativeMethods();
			if(File.Exists(filePath)) {
				var retVal = mil.Open(filePath);

				if(retVal == 1) {
					Populate(mil);
				} else {
					throw new InvalidOperationException("MediaInfoLib couldn't open the file");
				}
			}
		}

		private void Add(MetaInfoItemType<string> type, string value) {
			if(!string.IsNullOrWhiteSpace(value)) {
				base.Add(type, value);
			}
		}


		private void Add<T>(MetaInfoItemType<T> type, Func<string> getValue, Func<string, T> transform, params Func<string, string>[] processingChain) {
			Add(this, type, getValue, transform, processingChain);
		}
		private void Add<T>(MetaInfoItemType<T> type, Func<T> getValue) {
			Add(this, type, getValue);
		}
		private void Add<T>(MetaInfoContainer container, MetaInfoItemType<T> type, Func<string> getValue, Func<string, T> transform, params Func<string, string>[] processingChain) {
			Add(container, type, () => transform(processingChain.Aggregate(getValue(), (val, chain) => chain(val))));
		}
		private void Add<T>(MetaInfoContainer container, MetaInfoItemType<T> type, Func<T> getValue) {
			T value;
			try {
				value = getValue();
			} catch {
				return;
			}

			if(value is string && string.IsNullOrWhiteSpace(value as string)) {
				return;
			}

			Add(container, type, value);
		}
	}
}
