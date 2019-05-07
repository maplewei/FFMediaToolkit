﻿namespace FFMediaToolkit.Common
{
    using System;
    using FFMediaToolkit.Helpers;
    using FFmpeg.AutoGen;

    /// <summary>
    /// Represent a multimedia file context
    /// </summary>
    public unsafe sealed class MediaContainer : MediaObject, IDisposable
    {
        // TODO: Custom dictionary support: private AVDictionary* avDict;
        private bool isDisposed;

        private MediaContainer(AVFormatContext* format, VideoStream stream, MediaAccess acces)
        {
            FormatContextPointer = format;
            Video = stream;
            Access = acces;
        }

        /// <summary>
        /// Gets the video stream in the container. To set the stream in encoding mode, please use the <see cref="AddVideoStream(VideoEncoderSettings)"/> method.
        /// </summary>
        public VideoStream Video { get; private set; }

        /// <summary>
        /// Gets a pointer to the underlying <see cref="AVFormatContext"/>
        /// </summary>
        internal AVFormatContext* FormatContextPointer { get; private set; }

        /// <summary>
        /// Creates an empty FFmpeg format container for encoding.
        /// After you add media streams configurations, you have to call the <see cref="LockFile(string)"/> before pushing frames
        /// </summary>
        /// <param name="path">A</param>
        /// <returns>B</returns>
        public static MediaContainer CreateOutput(string path)
        {
            var format = ffmpeg.av_guess_format(null, path, null); // ->ThrowIfNull(() => new InvalidOperationException("Cannot find this output format"));
            var formatContext = ffmpeg.avformat_alloc_context();
            formatContext->oformat = format;
            return new MediaContainer(formatContext, null, MediaAccess.WriteInit);
        }

        /// <summary>
        /// Adds a new video stream to the container. Usable only in encoding, before locking file
        /// </summary>
        /// <param name="config">The stream configuration</param>
        public void AddVideoStream(VideoEncoderSettings config)
        {
            CheckAccess(MediaAccess.WriteInit);
            if (Video != null)
            {
                throw new InvalidOperationException("The video stream was already created");
            }

            Video = VideoStream.CreateNew(this, config);
        }

        /// <summary>
        /// Creates a media file for this container and writes format header into it. Usable only in encoding
        /// </summary>
        /// <param name="path">A path to create the file</param>
        public void LockFile(string path)
        {
            CheckAccess(MediaAccess.WriteInit);
            if (Video == null /*&& AudioStream == null*/)
            {
                throw new InvalidOperationException("Cannot create empty media file. You have to add video or audio stream before locking the file");
            }

            ffmpeg.avio_open(&FormatContextPointer->pb, path, ffmpeg.AVIO_FLAG_WRITE).ThrowIfError("opening the file");
            ffmpeg.avformat_write_header(FormatContextPointer, null);

            Access = MediaAccess.Write;
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            if (isDisposed)
            {
                return;
            }

            Video.Dispose();

            if (Access == MediaAccess.Write)
            {
                ffmpeg.av_write_trailer(FormatContextPointer);
                ffmpeg.avio_close(FormatContextPointer->pb);
            }

            if (FormatContextPointer != null)
            {
                ffmpeg.avformat_free_context(FormatContextPointer);
                FormatContextPointer = null;
            }

            isDisposed = true;
        }

        /// <summary>
        /// Writes specified packet to the container. Uses <see cref="ffmpeg.av_interleaved_write_frame(AVFormatContext*, AVPacket*)"/>
        /// </summary>
        /// <param name="packet">Media packet to write</param>
        public void WritePacket(MediaPacket packet)
        {
            CheckAccess(MediaAccess.Write);
            ffmpeg.av_interleaved_write_frame(FormatContextPointer, packet);
        }
    }
}