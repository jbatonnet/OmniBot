using Microsoft.Extensions.Logging;

using SIPSorcery;
using SIPSorceryMedia.FFmpeg;

namespace OmniBot.SIPSorcery
{
    public class SIPSorceryInitializer
    {
        private static bool initialized = false;

        public static void Initialize(ILoggerFactory loggerFactory = null)
        {
            initialized = true;

            FFmpegInit.Initialise(FfmpegLogLevelEnum.AV_LOG_DEBUG, null, loggerFactory?.CreateLogger<SIPSorceryInitializer>());

            if (loggerFactory != null)
                LogFactory.Set(loggerFactory);
        }
        public static void Initialize(string ffmpegLibraryPath, ILoggerFactory loggerFactory = null)
        {
            initialized = true;

            FFmpegInit.Initialise(FfmpegLogLevelEnum.AV_LOG_DEBUG, ffmpegLibraryPath, loggerFactory?.CreateLogger<SIPSorceryInitializer>());

            if (loggerFactory != null)
                LogFactory.Set(loggerFactory);
        }
        internal static void CheckInitiatlization()
        {
            if (!initialized)
                throw new Exception();
        }
    }
}
